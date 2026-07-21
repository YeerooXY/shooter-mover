using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
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
    /// Immutable description of one legacy route-profile slot. LegacyContext is opaque to
    /// the migration coordinator and is interpreted only by the injected starter-runtime
    /// factory. This keeps PlayerPrefs and Unity out of the durable application layer.
    /// </summary>
    public sealed class LegacyCharacterProfileV1
    {
        public LegacyCharacterProfileV1(
            int slotIndex,
            string displayName,
            StableId sourceCharacterDefinitionStableId,
            StableId classDefinitionStableId,
            string sourceFingerprint,
            object legacyContext)
        {
            PlayerAccountSnapshotV1.ValidateSlotIndex(slotIndex);
            if (string.IsNullOrWhiteSpace(displayName))
            {
                throw new ArgumentException(
                    "A legacy character display name is required.",
                    nameof(displayName));
            }
            if (string.IsNullOrWhiteSpace(sourceFingerprint))
            {
                throw new ArgumentException(
                    "A legacy profile fingerprint is required.",
                    nameof(sourceFingerprint));
            }

            SlotIndex = slotIndex;
            DisplayName = displayName.Trim();
            SourceCharacterDefinitionStableId =
                sourceCharacterDefinitionStableId
                ?? throw new ArgumentNullException(
                    nameof(sourceCharacterDefinitionStableId));
            ClassDefinitionStableId = classDefinitionStableId
                ?? throw new ArgumentNullException(nameof(classDefinitionStableId));
            SourceFingerprint = sourceFingerprint.Trim();
            LegacyContext = legacyContext;
        }

        public int SlotIndex { get; }

        public string DisplayName { get; }

        public StableId SourceCharacterDefinitionStableId { get; }

        public StableId ClassDefinitionStableId { get; }

        public string SourceFingerprint { get; }

        public object LegacyContext { get; }
    }

    public sealed class LegacyCharacterProfileMigrationResultV1
    {
        public LegacyCharacterProfileMigrationResultV1(
            CharacterCompositionStatusV1 status,
            string diagnostic,
            PlayerAccountSnapshotV1 account,
            IEnumerable<int> migratedSlots)
        {
            Status = status;
            Diagnostic = diagnostic ?? string.Empty;
            Account = account;
            MigratedSlots = new ReadOnlyCollection<int>(
                new List<int>(migratedSlots ?? Array.Empty<int>()));
        }

        public CharacterCompositionStatusV1 Status { get; }

        public string Diagnostic { get; }

        public PlayerAccountSnapshotV1 Account { get; }

        public IReadOnlyList<int> MigratedSlots { get; }

        public bool Succeeded
        {
            get { return Status != CharacterCompositionStatusV1.Rejected; }
        }
    }

    /// <summary>
    /// One-time route-profile migration. Exact character-instance IDs and account command
    /// IDs are derived from immutable legacy facts, so retrying before or after an
    /// interrupted durable save cannot duplicate a slot or starter equipment. Existing
    /// occupied slots are never overwritten. When the UI creates one new profile while a
    /// character graph is active, creation is delegated to the composition transaction so
    /// the active character is durably saved before the new slot can exist.
    /// </summary>
    public sealed class LegacyCharacterProfileMigrationV1
    {
        private readonly PlayerAccountSaveAuthorityV1 accountAuthority;
        private readonly IStarterCharacterRuntimeGraphFactoryV1 starterFactory;
        private readonly Func<PlayerAccountSnapshotV1, PlayerAccountStoreResultV1>
            saveAccount;

        public LegacyCharacterProfileMigrationV1(
            PlayerAccountSaveAuthorityV1 accountAuthority,
            IStarterCharacterRuntimeGraphFactoryV1 starterFactory,
            Func<PlayerAccountSnapshotV1, PlayerAccountStoreResultV1> saveAccount)
        {
            this.accountAuthority = accountAuthority
                ?? throw new ArgumentNullException(nameof(accountAuthority));
            this.starterFactory = starterFactory
                ?? throw new ArgumentNullException(nameof(starterFactory));
            this.saveAccount = saveAccount
                ?? throw new ArgumentNullException(nameof(saveAccount));
        }

        public LegacyCharacterProfileMigrationResultV1 Migrate(
            IEnumerable<LegacyCharacterProfileV1> legacyProfiles)
        {
            List<LegacyCharacterProfileV1> profiles =
                (legacyProfiles ?? Array.Empty<LegacyCharacterProfileV1>())
                .OrderBy(item => item.SlotIndex)
                .ToList();
            if (profiles.Any(item => item == null))
            {
                return Reject("legacy-profile-null");
            }
            if (profiles.Select(item => item.SlotIndex).Distinct().Count()
                != profiles.Count)
            {
                return Reject("legacy-profile-slot-duplicate");
            }
            if (profiles.Count == 0)
            {
                return new LegacyCharacterProfileMigrationResultV1(
                    CharacterCompositionStatusV1.ExactNoChange,
                    string.Empty,
                    accountAuthority.Current,
                    Array.Empty<int>());
            }

            // Startup migration has no active graph and follows the normal batch path below.
            // A single empty-slot request while a graph is active is real character creation
            // and must use the coordinator's persist-create-restore-save-publish transaction.
            CharacterCompositionCoordinatorV1 activeComposition;
            if (profiles.Count == 1
                && accountAuthority.Current.CharacterAt(
                    profiles[0].SlotIndex) == null
                && CharacterCompositionCoordinatorV1.TryResolveActive(
                    accountAuthority,
                    out activeComposition))
            {
                CharacterCompositionResultV1 created =
                    activeComposition.CreateAndSelect(profiles[0]);
                if (created == null || !created.Succeeded)
                {
                    return Reject(
                        created == null
                            ? "character-create-transaction-result-null"
                            : "character-create-transaction-rejected:"
                                + created.Diagnostic);
                }
                return new LegacyCharacterProfileMigrationResultV1(
                    CharacterCompositionStatusV1.Migrated,
                    string.Empty,
                    created.Account,
                    new[] { profiles[0].SlotIndex });
            }

            PlayerAccountSaveAuthoritySnapshotV1 rollback =
                accountAuthority.ExportSnapshot();
            var migrated = new List<int>();
            for (int index = 0; index < profiles.Count; index++)
            {
                LegacyCharacterProfileV1 profile = profiles[index];
                StableId exactCharacterId = ExactCharacterId(
                    accountAuthority.Current.AccountStableId,
                    profile);
                CharacterInstanceSnapshotV1 occupied =
                    accountAuthority.Current.CharacterAt(profile.SlotIndex);
                if (occupied != null)
                {
                    if (occupied.CharacterInstanceStableId == exactCharacterId
                        && occupied.ClassDefinitionStableId
                            == profile.ClassDefinitionStableId
                        && string.Equals(
                            occupied.DisplayName,
                            profile.DisplayName,
                            StringComparison.Ordinal))
                    {
                        continue;
                    }

                    RollBack(rollback);
                    return Reject(
                        "legacy-profile-target-slot-occupied:"
                            + profile.SlotIndex);
                }

                ICharacterRuntimeGraphV1 graph = null;
                try
                {
                    graph = starterFactory.CreateStarter(
                        profile.SlotIndex,
                        exactCharacterId,
                        profile.ClassDefinitionStableId,
                        profile.DisplayName,
                        profile.LegacyContext);
                    string graphError;
                    if (!TryValidateStarterGraph(
                        graph,
                        profile,
                        exactCharacterId,
                        out graphError))
                    {
                        Dispose(graph);
                        RollBack(rollback);
                        return Reject(graphError);
                    }

                    IReadOnlyList<SaveComponentSnapshotV1> components =
                        PlayerAccountRestoreCoordinatorV1.ExportComponents(
                            graph.SaveAdapters);
                    CharacterInstanceSnapshotV1 character =
                        new CharacterInstanceSnapshotV1(
                            exactCharacterId,
                            profile.ClassDefinitionStableId,
                            profile.SlotIndex,
                            profile.DisplayName,
                            0L,
                            components);
                    PlayerAccountSaveResultV1 created = accountAuthority.Apply(
                        PlayerAccountSaveCommandV1.CreateCharacter(
                            MigrationOperationId(
                                accountAuthority.Current.AccountStableId,
                                profile),
                            accountAuthority.Current.Revision,
                            character));
                    if (created == null
                        || (created.Status != PlayerAccountSaveStatusV1.Applied
                            && created.Status
                                != PlayerAccountSaveStatusV1
                                    .ExactDuplicateNoChange))
                    {
                        Dispose(graph);
                        RollBack(rollback);
                        return Reject(
                            created == null
                                ? "legacy-profile-create-result-null"
                                : "legacy-profile-create-rejected:"
                                    + created.RejectionCode);
                    }
                    graph.MarkPersisted(character);
                    migrated.Add(profile.SlotIndex);
                    Dispose(graph);
                }
                catch (Exception exception)
                {
                    Dispose(graph);
                    RollBack(rollback);
                    return Reject(
                        "legacy-profile-migration-threw:"
                            + exception.GetType().Name);
                }
            }

            if (migrated.Count == 0)
            {
                return new LegacyCharacterProfileMigrationResultV1(
                    CharacterCompositionStatusV1.ExactNoChange,
                    string.Empty,
                    accountAuthority.Current,
                    migrated);
            }

            PlayerAccountStoreResultV1 stored;
            try
            {
                stored = saveAccount(accountAuthority.Current);
            }
            catch (Exception exception)
            {
                RollBack(rollback);
                return Reject(
                    "legacy-profile-store-threw:"
                        + exception.GetType().Name);
            }
            if (stored == null || !stored.Succeeded || stored.Snapshot == null)
            {
                RollBack(rollback);
                return Reject(
                    stored == null
                        ? "legacy-profile-store-result-null"
                        : "legacy-profile-store-rejected:"
                            + stored.RejectionCode);
            }

            return new LegacyCharacterProfileMigrationResultV1(
                CharacterCompositionStatusV1.Migrated,
                string.Empty,
                stored.Snapshot,
                migrated);
        }

        public static StableId ExactCharacterId(
            StableId accountStableId,
            LegacyCharacterProfileV1 profile)
        {
            if (accountStableId == null)
            {
                throw new ArgumentNullException(nameof(accountStableId));
            }
            if (profile == null)
            {
                throw new ArgumentNullException(nameof(profile));
            }
            return StableId.Parse(
                "character-instance.migrated-" + Hash(
                    accountStableId
                    + "|"
                    + profile.SlotIndex.ToString(CultureInfo.InvariantCulture)
                    + "|"
                    + profile.SourceCharacterDefinitionStableId
                    + "|"
                    + profile.ClassDefinitionStableId
                    + "|"
                    + profile.SourceFingerprint));
        }

        private LegacyCharacterProfileMigrationResultV1 Reject(
            string diagnostic)
        {
            return new LegacyCharacterProfileMigrationResultV1(
                CharacterCompositionStatusV1.Rejected,
                diagnostic,
                accountAuthority.Current,
                Array.Empty<int>());
        }

        private static bool TryValidateStarterGraph(
            ICharacterRuntimeGraphV1 graph,
            LegacyCharacterProfileV1 profile,
            StableId exactCharacterId,
            out string rejectionCode)
        {
            if (graph == null || graph.IsDisposed || graph.Character == null)
            {
                rejectionCode = "legacy-starter-runtime-invalid";
                return false;
            }
            if (graph.Character.SlotIndex != profile.SlotIndex
                || graph.Character.CharacterInstanceStableId != exactCharacterId
                || graph.Character.ClassDefinitionStableId
                    != profile.ClassDefinitionStableId)
            {
                rejectionCode = "legacy-starter-runtime-identity-mismatch";
                return false;
            }
            if (graph.SaveAdapters == null)
            {
                rejectionCode = "legacy-starter-adapters-null";
                return false;
            }

            var ids = new HashSet<StableId>();
            for (int index = 0; index < graph.SaveAdapters.Count; index++)
            {
                ISaveComponentAdapterV1 adapter = graph.SaveAdapters[index];
                if (adapter == null
                    || adapter.Definition == null
                    || !ids.Add(adapter.Definition.ComponentStableId))
                {
                    rejectionCode = "legacy-starter-adapter-invalid";
                    return false;
                }
            }
            for (int index = 0;
                index < CharacterCompositionCoordinatorV1
                    .RequiredCharacterComponentIds.Count;
                index++)
            {
                StableId required = CharacterCompositionCoordinatorV1
                    .RequiredCharacterComponentIds[index];
                if (!ids.Contains(required))
                {
                    rejectionCode =
                        "legacy-starter-required-adapter-missing:" + required;
                    return false;
                }
            }

            rejectionCode = string.Empty;
            return true;
        }

        private static StableId MigrationOperationId(
            StableId accountStableId,
            LegacyCharacterProfileV1 profile)
        {
            return StableId.Parse(
                "operation.character-migration-" + Hash(
                    accountStableId
                    + "|"
                    + profile.SlotIndex.ToString(CultureInfo.InvariantCulture)
                    + "|"
                    + profile.SourceFingerprint));
        }

        private void RollBack(PlayerAccountSaveAuthoritySnapshotV1 rollback)
        {
            string ignored;
            accountAuthority.TryImport(rollback, out ignored);
        }

        private static void Dispose(ICharacterRuntimeGraphV1 graph)
        {
            if (graph != null && !graph.IsDisposed)
            {
                graph.Dispose();
            }
        }

        private static string Hash(string value)
        {
            using (SHA256 sha = SHA256.Create())
            {
                byte[] digest = sha.ComputeHash(
                    Encoding.UTF8.GetBytes(value ?? string.Empty));
                var builder = new StringBuilder(32);
                for (int index = 0; index < 16; index++)
                {
                    builder.Append(digest[index].ToString("x2"));
                }
                return builder.ToString();
            }
        }
    }
}
