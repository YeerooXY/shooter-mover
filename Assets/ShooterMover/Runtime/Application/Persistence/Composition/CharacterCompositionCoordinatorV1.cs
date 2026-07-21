using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
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
    /// Account-to-Hub composition boundary. Existing-slot selection durably persists the
    /// active graph before disposal. Empty-slot creation is a separate transaction:
    /// persist active, stage and restore the starter graph, durably commit the aggregate,
    /// then publish the new graph. Failures keep the old graph active and remove the staged
    /// character from both the authority and, where possible, durable storage.
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
        private static readonly ConditionalWeakTable<
            PlayerAccountSaveAuthorityV1,
            CharacterCompositionCoordinatorV1> coordinators =
                new ConditionalWeakTable<
                    PlayerAccountSaveAuthorityV1,
                    CharacterCompositionCoordinatorV1>();
        private static readonly object coordinatorGate = new object();

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

            lock (coordinatorGate)
            {
                coordinators.Remove(accountAuthority);
                coordinators.Add(accountAuthority, this);
            }
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

        internal static bool TryResolveActive(
            PlayerAccountSaveAuthorityV1 authority,
            out CharacterCompositionCoordinatorV1 coordinator)
        {
            coordinator = null;
            if (authority == null)
            {
                return false;
            }

            lock (coordinatorGate)
            {
                CharacterCompositionCoordinatorV1 resolved;
                if (!coordinators.TryGetValue(authority, out resolved)
                    || resolved == null
                    || resolved.disposed
                    || resolved.activeRuntime == null
                    || resolved.activeRuntime.IsDisposed)
                {
                    return false;
                }
                coordinator = resolved;
                return true;
            }
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

            if (activeRuntime != null
                && !activeRuntime.IsDisposed
                && activeSlotIndex == slotIndex
                && activeRuntime.Character != null
                && activeRuntime.Character.CharacterInstanceStableId
                    == selected.CharacterInstanceStableId)
            {
                return new CharacterCompositionResultV1(
                    CharacterCompositionStatusV1.Selected,
                    string.Empty,
                    account,
                    selected);
            }

            if (activeRuntime != null && !activeRuntime.IsDisposed)
            {
                CharacterCompositionResultV1 persisted = PersistActive(
                    SwitchSaveOperationId(slotIndex, selected));
                if (persisted == null || !persisted.Succeeded)
                {
                    return Reject(
                        persisted == null
                            ? "character-switch-save-result-null"
                            : "character-switch-save-rejected:"
                                + persisted.Diagnostic,
                        selected);
                }
                account = accountAuthority.Current;
                selected = account.CharacterAt(slotIndex);
                if (selected == null)
                {
                    return Reject(
                        "character-selection-slot-disappeared-after-save",
                        null);
                }
            }

            // The old graph is disposed only after persistence succeeded. It is fully gone
            // before the target graph factory can construct subscriptions or scene bindings.
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

        public CharacterCompositionResultV1 CreateAndSelect(
            LegacyCharacterProfileV1 profile)
        {
            ThrowIfDisposed();
            if (profile == null)
            {
                return Reject("character-create-profile-null", null);
            }
            if (accountAuthority.Current.CharacterAt(profile.SlotIndex) != null)
            {
                return Reject(
                    "character-create-target-slot-occupied:"
                        + profile.SlotIndex.ToString(CultureInfo.InvariantCulture),
                    accountAuthority.Current.CharacterAt(profile.SlotIndex));
            }

            IStarterCharacterRuntimeGraphFactoryV1 starterFactory =
                runtimeFactory as IStarterCharacterRuntimeGraphFactoryV1;
            if (starterFactory == null)
            {
                return Reject("character-create-starter-factory-missing", null);
            }

            if (activeRuntime != null && !activeRuntime.IsDisposed)
            {
                CharacterCompositionResultV1 persisted = PersistActive(
                    CreateSaveOperationId(profile));
                if (persisted == null || !persisted.Succeeded)
                {
                    return Reject(
                        persisted == null
                            ? "character-create-pre-save-result-null"
                            : "character-create-pre-save-rejected:"
                                + persisted.Diagnostic,
                        null);
                }
            }

            PlayerAccountSaveAuthoritySnapshotV1 rollback =
                accountAuthority.ExportSnapshot();
            ICharacterRuntimeGraphV1 candidate = null;
            CharacterInstanceSnapshotV1 createdCharacter = null;
            bool creationStoreInvoked = false;
            try
            {
                StableId exactCharacterId =
                    LegacyCharacterProfileMigrationV1.ExactCharacterId(
                        accountAuthority.Current.AccountStableId,
                        profile);
                candidate = starterFactory.CreateStarter(
                    profile.SlotIndex,
                    exactCharacterId,
                    profile.ClassDefinitionStableId,
                    profile.DisplayName,
                    profile.LegacyContext);

                IReadOnlyList<SaveComponentSnapshotV1> components =
                    PlayerAccountRestoreCoordinatorV1.ExportComponents(
                        candidate.SaveAdapters);
                createdCharacter = new CharacterInstanceSnapshotV1(
                    exactCharacterId,
                    profile.ClassDefinitionStableId,
                    profile.SlotIndex,
                    profile.DisplayName,
                    0L,
                    components);

                string graphError;
                if (!TryValidateGraph(
                    candidate,
                    createdCharacter,
                    out graphError))
                {
                    RollBackAuthority(rollback);
                    DisposeGraph(candidate);
                    return Reject(
                        "character-create-" + graphError,
                        createdCharacter);
                }

                PlayerAccountSaveResultV1 created = accountAuthority.Apply(
                    PlayerAccountSaveCommandV1.CreateCharacter(
                        CreateCharacterOperationId(profile, exactCharacterId),
                        accountAuthority.Current.Revision,
                        createdCharacter));
                if (created == null
                    || (created.Status != PlayerAccountSaveStatusV1.Applied
                        && created.Status
                            != PlayerAccountSaveStatusV1.ExactDuplicateNoChange))
                {
                    string rollbackError = RollBackAuthority(rollback);
                    DisposeGraph(candidate);
                    return Reject(
                        created == null
                            ? "character-create-account-result-null"
                            : "character-create-account-rejected:"
                                + created.RejectionCode
                                + SuffixRollback(rollbackError),
                        createdCharacter);
                }

                PlayerAccountSnapshotV1 stagedAccount = accountAuthority.Current;
                CharacterInstanceSnapshotV1 stagedCharacter =
                    stagedAccount.CharacterAt(profile.SlotIndex);
                PlayerAccountRestoreResultV1 restored =
                    restoreCoordinator.Restore(
                        stagedAccount,
                        BuildBindings(
                            stagedAccount,
                            profile.SlotIndex,
                            candidate));
                if (restored == null || !restored.Succeeded)
                {
                    string rollbackError = RollBackAuthority(rollback);
                    DisposeGraph(candidate);
                    return Reject(
                        restored == null
                            ? "character-create-restore-result-null"
                            : "character-create-restore-rejected:"
                                + restored.RejectionCode
                                + SuffixRollback(rollbackError),
                        createdCharacter);
                }

                candidate.MarkPersisted(stagedCharacter);

                PlayerAccountStoreResultV1 stored;
                try
                {
                    creationStoreInvoked = true;
                    stored = saveAccount(stagedAccount);
                }
                catch (Exception exception)
                {
                    string rollbackError = RollBackCreationDurably(rollback);
                    DisposeGraph(candidate);
                    return Reject(
                        "character-create-store-threw:"
                            + exception.GetType().Name
                            + SuffixRollback(rollbackError),
                        createdCharacter);
                }

                if (stored == null || !stored.Succeeded || stored.Snapshot == null)
                {
                    string rollbackError = RollBackCreationDurably(rollback);
                    DisposeGraph(candidate);
                    return Reject(
                        stored == null
                            ? "character-create-store-result-null"
                            : "character-create-store-rejected:"
                                + stored.RejectionCode
                                + SuffixRollback(rollbackError),
                        createdCharacter);
                }

                CharacterInstanceSnapshotV1 persistedCharacter =
                    stored.Snapshot.CharacterAt(profile.SlotIndex);
                if (!SameCharacterIdentity(
                    persistedCharacter,
                    createdCharacter))
                {
                    string rollbackError = RollBackCreationDurably(rollback);
                    DisposeGraph(candidate);
                    return Reject(
                        "character-create-store-snapshot-mismatch"
                            + SuffixRollback(rollbackError),
                        createdCharacter);
                }

                ICharacterRuntimeGraphV1 previous = activeRuntime;
                activeRuntime = candidate;
                activeSlotIndex = profile.SlotIndex;
                candidate = null;
                DisposeGraph(previous);
                return new CharacterCompositionResultV1(
                    CharacterCompositionStatusV1.Selected,
                    string.Empty,
                    stored.Snapshot,
                    persistedCharacter);
            }
            catch (Exception exception)
            {
                string rollbackError = creationStoreInvoked
                    ? RollBackCreationDurably(rollback)
                    : RollBackAuthority(rollback);
                DisposeGraph(candidate);
                return Reject(
                    "character-create-threw:"
                        + exception.GetType().Name
                        + SuffixRollback(rollbackError),
                    createdCharacter);
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
            lock (coordinatorGate)
            {
                CharacterCompositionCoordinatorV1 registered;
                if (coordinators.TryGetValue(accountAuthority, out registered)
                    && ReferenceEquals(registered, this))
                {
                    coordinators.Remove(accountAuthority);
                }
            }
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

        private StableId SwitchSaveOperationId(
            int targetSlotIndex,
            CharacterInstanceSnapshotV1 target)
        {
            string material = activeSlotIndex
                + "|"
                + activeRuntime.Character.CharacterInstanceStableId
                + "|"
                + targetSlotIndex
                + "|"
                + target.CharacterInstanceStableId;
            return DerivedOperationId(
                "operation.character-switch-save-",
                material);
        }

        private StableId CreateSaveOperationId(
            LegacyCharacterProfileV1 profile)
        {
            return DerivedOperationId(
                "operation.character-create-pre-save-",
                activeSlotIndex
                    + "|"
                    + (activeRuntime == null
                        ? string.Empty
                        : activeRuntime.Character.CharacterInstanceStableId.ToString())
                    + "|"
                    + profile.SlotIndex.ToString(CultureInfo.InvariantCulture)
                    + "|"
                    + profile.SourceFingerprint);
        }

        private static StableId CreateCharacterOperationId(
            LegacyCharacterProfileV1 profile,
            StableId exactCharacterId)
        {
            return DerivedOperationId(
                "operation.character-create-",
                profile.SlotIndex.ToString(CultureInfo.InvariantCulture)
                    + "|"
                    + exactCharacterId
                    + "|"
                    + profile.SourceFingerprint);
        }

        private static StableId ComponentOperationId(
            StableId saveOperationStableId,
            SaveComponentSnapshotV1 component,
            int index)
        {
            return DerivedOperationId(
                "operation.character-component-save-",
                saveOperationStableId
                    + "|"
                    + component.ComponentStableId
                    + "|"
                    + component.Fingerprint
                    + "|"
                    + index);
        }

        private static StableId DerivedOperationId(
            string prefix,
            string material)
        {
            using (SHA256 sha = SHA256.Create())
            {
                byte[] digest = sha.ComputeHash(
                    Encoding.UTF8.GetBytes(material ?? string.Empty));
                var builder = new StringBuilder(32);
                for (int offset = 0; offset < 16; offset++)
                {
                    builder.Append(digest[offset].ToString("x2"));
                }
                return StableId.Parse(prefix + builder);
            }
        }

        private string RollBackCreationDurably(
            PlayerAccountSaveAuthoritySnapshotV1 rollback)
        {
            string rollbackError = RollBackAuthority(rollback);
            if (!string.IsNullOrEmpty(rollbackError))
            {
                return rollbackError;
            }

            try
            {
                PlayerAccountStoreResultV1 restored =
                    saveAccount(accountAuthority.Current);
                if (restored == null)
                {
                    return "durable-rollback-result-null";
                }
                if (!restored.Succeeded || restored.Snapshot == null)
                {
                    return "durable-rollback-rejected:"
                        + restored.RejectionCode;
                }
                return string.Empty;
            }
            catch (Exception exception)
            {
                return "durable-rollback-threw:"
                    + exception.GetType().Name;
            }
        }

        private string RollBackAuthority(
            PlayerAccountSaveAuthoritySnapshotV1 rollback)
        {
            string rollbackError;
            accountAuthority.TryImport(rollback, out rollbackError);
            return rollbackError;
        }

        private static bool SameCharacterIdentity(
            CharacterInstanceSnapshotV1 left,
            CharacterInstanceSnapshotV1 right)
        {
            return left != null
                && right != null
                && left.SlotIndex == right.SlotIndex
                && left.CharacterInstanceStableId
                    == right.CharacterInstanceStableId
                && left.ClassDefinitionStableId
                    == right.ClassDefinitionStableId;
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
