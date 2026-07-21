using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using ShooterMover.Application.Persistence.Accounts;
using ShooterMover.Application.Persistence.Components;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Persistence.Accounts;

namespace ShooterMover.Application.Persistence.Composition
{
    /// <summary>
    /// One selected-character runtime graph. It composes existing subsystem authorities
    /// and their merged save-component adapters; it owns no replacement subsystem truth.
    /// </summary>
    public interface ICharacterRuntimeGraphV1 : IDisposable
    {
        CharacterInstanceSnapshotV1 Character { get; }

        IReadOnlyList<ISaveComponentAdapterV1> SaveAdapters { get; }

        bool IsDisposed { get; }

        void MarkPersisted(CharacterInstanceSnapshotV1 character);
    }

    public interface ICharacterRuntimeGraphFactoryV1
    {
        ICharacterRuntimeGraphV1 CreateRestoreTarget(
            CharacterInstanceSnapshotV1 character);
    }

    public interface IStarterCharacterRuntimeGraphFactoryV1
    {
        ICharacterRuntimeGraphV1 CreateStarter(
            int slotIndex,
            StableId exactCharacterInstanceStableId,
            StableId classDefinitionStableId,
            string displayName,
            object legacyContext);
    }

    public enum CharacterCompositionStatusV1
    {
        Selected = 1,
        Persisted = 2,
        ExactNoChange = 3,
        Migrated = 4,
        Rejected = 5,
    }

    public sealed class CharacterCompositionResultV1
    {
        public CharacterCompositionResultV1(
            CharacterCompositionStatusV1 status,
            string diagnostic,
            PlayerAccountSnapshotV1 account,
            CharacterInstanceSnapshotV1 character)
        {
            Status = status;
            Diagnostic = diagnostic ?? string.Empty;
            Account = account;
            Character = character;
        }

        public CharacterCompositionStatusV1 Status { get; }

        public string Diagnostic { get; }

        public PlayerAccountSnapshotV1 Account { get; }

        public CharacterInstanceSnapshotV1 Character { get; }

        public bool Succeeded
        {
            get { return Status != CharacterCompositionStatusV1.Rejected; }
        }
    }

    /// <summary>
    /// Account-to-Hub composition boundary. It restores only the selected exact account
    /// slot through SAVE-ADAPTERS-001, keeps all other slots opaque, and persists confirmed
    /// mutations through PlayerAccountSaveAuthorityV1 plus the injected atomic store.
    /// </summary>
    public sealed class CharacterCompositionCoordinatorV1 : IDisposable
    {
        private static readonly ReadOnlyCollection<StableId>
            requiredCharacterComponentIds =
                new ReadOnlyCollection<StableId>(new List<StableId>
                {
                    KnownSaveComponentDefinitionsV1.PlayerExperience()
                        .ComponentStableId,
                    KnownSaveComponentDefinitionsV1.PlayerHoldings()
                        .ComponentStableId,
                    KnownSaveComponentDefinitionsV1.MoneyWallet()
                        .ComponentStableId,
                    KnownSaveComponentDefinitionsV1.ScrapWallet()
                        .ComponentStableId,
                    KnownSaveComponentDefinitionsV1.RankedSkillAllocation()
                        .ComponentStableId,
                    KnownSaveComponentDefinitionsV1.ExactInstanceLoadout()
                        .ComponentStableId,
                });

        private readonly PlayerAccountSaveAuthorityV1 accountAuthority;
        private readonly ICharacterRuntimeGraphFactoryV1 runtimeFactory;
        private readonly PlayerAccountRestoreCoordinatorV1 restoreCoordinator;
        private readonly Func<PlayerAccountSnapshotV1, PlayerAccountStoreResultV1>
            saveAccount;
        private ICharacterRuntimeGraphV1 activeRuntime;
        private int activeSlotIndex = -1;
        private bool disposed;

        public CharacterCompositionCoordinatorV1(
            PlayerAccountSaveAuthorityV1 accountAuthority,
            ICharacterRuntimeGraphFactoryV1 runtimeFactory,
            Func<PlayerAccountSnapshotV1, PlayerAccountStoreResultV1> saveAccount,
            Func<PlayerAccountSnapshotV1, SaveComponentValidationResultV1>
                validateAggregate = null)
        {
            this.accountAuthority = accountAuthority
                ?? throw new ArgumentNullException(nameof(accountAuthority));
            this.runtimeFactory = runtimeFactory
                ?? throw new ArgumentNullException(nameof(runtimeFactory));
            this.saveAccount = saveAccount
                ?? throw new ArgumentNullException(nameof(saveAccount));
            restoreCoordinator = new PlayerAccountRestoreCoordinatorV1(
                validateAggregate: validateAggregate
                    ?? PlayerAccountComponentSemanticsV1.Validate);
        }

        public PlayerAccountSnapshotV1 Account
        {
            get { return accountAuthority.Current; }
        }

        public ICharacterRuntimeGraphV1 ActiveRuntime
        {
            get { return activeRuntime; }
        }

        public int ActiveSlotIndex
        {
            get { return activeSlotIndex; }
        }

        public static IReadOnlyList<StableId> RequiredCharacterComponentIds
        {
            get { return requiredCharacterComponentIds; }
        }

        public CharacterCompositionResultV1 Select(int slotIndex)
        {
            ThrowIfDisposed();
            if (!IsSlotIndexValid(slotIndex))
            {
                return Reject("character-selection-slot-invalid", null);
            }

            PlayerAccountSnapshotV1 account = accountAuthority.Current;
            CharacterInstanceSnapshotV1 selected = account.CharacterAt(slotIndex);
            if (selected == null)
            {
                return Reject("character-selection-slot-empty", null);
            }

            // A previous graph may contain subscriptions, transient command replay state,
            // and scene bindings. It must be fully gone before a new graph is constructed.
            UnbindActive();

            ICharacterRuntimeGraphV1 candidate = null;
            try
            {
                candidate = runtimeFactory.CreateRestoreTarget(selected);
                string graphError;
                if (!TryValidateGraph(candidate, selected, out graphError))
                {
                    DisposeGraph(candidate);
                    return Reject(graphError, selected);
                }

                PlayerAccountRestoreResultV1 restored =
                    restoreCoordinator.Restore(
                        account,
                        BuildBindings(account, slotIndex, candidate));
                if (restored == null || !restored.Succeeded)
                {
                    DisposeGraph(candidate);
                    return Reject(
                        restored == null
                            ? "character-restore-result-null"
                            : "character-restore-rejected:"
                                + restored.RejectionCode,
                        selected);
                }

                candidate.MarkPersisted(selected);
                activeRuntime = candidate;
                activeSlotIndex = slotIndex;
                return new CharacterCompositionResultV1(
                    CharacterCompositionStatusV1.Selected,
                    string.Empty,
                    account,
                    selected);
            }
            catch (Exception exception)
            {
                DisposeGraph(candidate);
                return Reject(
                    "character-restore-threw:"
                        + exception.GetType().Name,
                    selected);
            }
        }

        public CharacterCompositionResultV1 PersistActive(
            StableId saveOperationStableId)
        {
            ThrowIfDisposed();
            if (saveOperationStableId == null)
            {
                return Reject("character-save-operation-id-missing", null);
            }
            if (activeRuntime == null
                || activeRuntime.IsDisposed
                || !IsSlotIndexValid(activeSlotIndex))
            {
                return Reject("character-save-no-active-runtime", null);
            }

            PlayerAccountSnapshotV1 before = accountAuthority.Current;
            CharacterInstanceSnapshotV1 beforeCharacter =
                before.CharacterAt(activeSlotIndex);
            if (beforeCharacter == null
                || beforeCharacter.CharacterInstanceStableId
                    != activeRuntime.Character.CharacterInstanceStableId)
            {
                return Reject(
                    "character-save-active-identity-mismatch",
                    beforeCharacter);
            }

            IReadOnlyList<SaveComponentSnapshotV1> exported;
            try
            {
                exported = PlayerAccountRestoreCoordinatorV1.ExportComponents(
                    activeRuntime.SaveAdapters);
            }
            catch (Exception exception)
            {
                return Reject(
                    "character-save-export-threw:"
                        + exception.GetType().Name,
                    beforeCharacter);
            }

            var changed = new List<SaveComponentSnapshotV1>();
            for (int index = 0; index < exported.Count; index++)
            {
                SaveComponentSnapshotV1 component = exported[index];
                SaveComponentSnapshotV1 existing;
                if (!beforeCharacter.TryGetComponent(
                        component.ComponentStableId,
                        out existing)
                    || !string.Equals(
                        existing.Fingerprint,
                        component.Fingerprint,
                        StringComparison.Ordinal))
                {
                    changed.Add(component);
                }
            }

            if (changed.Count == 0)
            {
                return new CharacterCompositionResultV1(
                    CharacterCompositionStatusV1.ExactNoChange,
                    string.Empty,
                    before,
                    beforeCharacter);
            }

            PlayerAccountSaveAuthoritySnapshotV1 rollback =
                accountAuthority.ExportSnapshot();
            changed.Sort((left, right) => string.CompareOrdinal(
                left.ComponentStableId.ToString(),
                right.ComponentStableId.ToString()));

            for (int index = 0; index < changed.Count; index++)
            {
                SaveComponentSnapshotV1 component = changed[index];
                PlayerAccountSaveResultV1 applied = accountAuthority.Apply(
                    PlayerAccountSaveCommandV1.UpsertCharacterComponent(
                        ComponentOperationId(
                            saveOperationStableId,
                            component,
                            index),
                        accountAuthority.Current.Revision,
                        activeSlotIndex,
                        beforeCharacter.CharacterInstanceStableId,
                        component));
                if (applied == null
                    || (applied.Status != PlayerAccountSaveStatusV1.Applied
                        && applied.Status
                            != PlayerAccountSaveStatusV1.ExactDuplicateNoChange))
                {
                    string rollbackError;
                    accountAuthority.TryImport(rollback, out rollbackError);
                    return Reject(
                        applied == null
                            ? "character-save-account-result-null"
                            : "character-save-account-rejected:"
                                + applied.RejectionCode
                                + SuffixRollback(rollbackError),
                        beforeCharacter);
                }
            }

            PlayerAccountStoreResultV1 stored;
            try
            {
                stored = saveAccount(accountAuthority.Current);
            }
            catch (Exception exception)
            {
                string rollbackError;
                accountAuthority.TryImport(rollback, out rollbackError);
                return Reject(
                    "character-save-store-threw:"
                        + exception.GetType().Name
                        + SuffixRollback(rollbackError),
                    beforeCharacter);
            }

            if (stored == null || !stored.Succeeded || stored.Snapshot == null)
            {
                string rollbackError;
                accountAuthority.TryImport(rollback, out rollbackError);
                return Reject(
                    stored == null
                        ? "character-save-store-result-null"
                        : "character-save-store-rejected:"
                            + stored.RejectionCode
                            + SuffixRollback(rollbackError),
                    beforeCharacter);
            }

            CharacterInstanceSnapshotV1 persisted =
                stored.Snapshot.CharacterAt(activeSlotIndex);
            activeRuntime.MarkPersisted(persisted);
            return new CharacterCompositionResultV1(
                CharacterCompositionStatusV1.Persisted,
                string.Empty,
                stored.Snapshot,
                persisted);
        }

        public void UnbindActive()
        {
            if (disposed)
            {
                return;
            }
            DisposeGraph(activeRuntime);
            activeRuntime = null;
            activeSlotIndex = -1;
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }
            UnbindActive();
            disposed = true;
        }

        private CharacterCompositionResultV1 Reject(
            string diagnostic,
            CharacterInstanceSnapshotV1 character)
        {
            return new CharacterCompositionResultV1(
                CharacterCompositionStatusV1.Rejected,
                diagnostic,
                accountAuthority.Current,
                character);
        }

        private static bool TryValidateGraph(
            ICharacterRuntimeGraphV1 graph,
            CharacterInstanceSnapshotV1 selected,
            out string rejectionCode)
        {
            if (graph == null)
            {
                rejectionCode = "character-runtime-factory-returned-null";
                return false;
            }
            if (graph.IsDisposed)
            {
                rejectionCode = "character-runtime-factory-returned-disposed";
                return false;
            }
            if (graph.Character == null
                || graph.Character.CharacterInstanceStableId
                    != selected.CharacterInstanceStableId
                || graph.Character.ClassDefinitionStableId
                    != selected.ClassDefinitionStableId
                || graph.Character.SlotIndex != selected.SlotIndex)
            {
                rejectionCode = "character-runtime-factory-identity-mismatch";
                return false;
            }
            if (graph.SaveAdapters == null
                || graph.SaveAdapters.Any(item => item == null
                    || item.Definition == null))
            {
                rejectionCode = "character-runtime-adapter-null";
                return false;
            }

            var ids = new HashSet<StableId>();
            for (int index = 0; index < graph.SaveAdapters.Count; index++)
            {
                if (!ids.Add(
                    graph.SaveAdapters[index].Definition.ComponentStableId))
                {
                    rejectionCode = "character-runtime-adapter-duplicate";
                    return false;
                }
            }
            for (int index = 0;
                index < requiredCharacterComponentIds.Count;
                index++)
            {
                StableId required = requiredCharacterComponentIds[index];
                if (!ids.Contains(required))
                {
                    rejectionCode =
                        "character-runtime-required-adapter-missing:" + required;
                    return false;
                }
                if (!selected.Components.ContainsKey(required))
                {
                    rejectionCode =
                        "character-snapshot-required-component-missing:" + required;
                    return false;
                }
            }

            rejectionCode = string.Empty;
            return true;
        }

        private static IReadOnlyList<CharacterSaveRestoreBindingV1>
            BuildBindings(
                PlayerAccountSnapshotV1 account,
                int selectedSlotIndex,
                ICharacterRuntimeGraphV1 selectedGraph)
        {
            var bindings = new List<CharacterSaveRestoreBindingV1>();
            for (int slotIndex = 0;
                slotIndex < PlayerAccountSnapshotV1.CharacterSlotCount;
                slotIndex++)
            {
                CharacterInstanceSnapshotV1 character =
                    account.CharacterAt(slotIndex);
                if (character == null)
                {
                    continue;
                }
                bindings.Add(new CharacterSaveRestoreBindingV1(
                    slotIndex,
                    character.CharacterInstanceStableId,
                    slotIndex == selectedSlotIndex
                        ? selectedGraph.SaveAdapters
                        : Array.Empty<ISaveComponentAdapterV1>()));
            }
            return bindings;
        }

        private static StableId ComponentOperationId(
            StableId saveOperationStableId,
            SaveComponentSnapshotV1 component,
            int index)
        {
            string material = saveOperationStableId
                + "|"
                + component.ComponentStableId
                + "|"
                + component.Fingerprint
                + "|"
                + index;
            using (SHA256 sha = SHA256.Create())
            {
                byte[] digest = sha.ComputeHash(
                    Encoding.UTF8.GetBytes(material));
                var builder = new StringBuilder(32);
                for (int offset = 0; offset < 16; offset++)
                {
                    builder.Append(digest[offset].ToString("x2"));
                }
                return StableId.Parse(
                    "operation.character-component-save-" + builder);
            }
        }

        private static string SuffixRollback(string rollbackError)
        {
            return string.IsNullOrEmpty(rollbackError)
                ? string.Empty
                : ";account-rollback=" + rollbackError;
        }

        private static void DisposeGraph(ICharacterRuntimeGraphV1 graph)
        {
            if (graph != null && !graph.IsDisposed)
            {
                graph.Dispose();
            }
        }

        private static bool IsSlotIndexValid(int slotIndex)
        {
            return slotIndex >= 0
                && slotIndex < PlayerAccountSnapshotV1.CharacterSlotCount;
        }

        private void ThrowIfDisposed()
        {
            if (disposed)
            {
                throw new ObjectDisposedException(
                    nameof(CharacterCompositionCoordinatorV1));
            }
        }
    }
}
