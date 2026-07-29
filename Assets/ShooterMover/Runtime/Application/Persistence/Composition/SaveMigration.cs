using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using ShooterMover.Application.Persistence.Accounts;
using ShooterMover.Application.Persistence.SaveParts;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Persistence.Accounts;

namespace ShooterMover.Application.Persistence.Composition
{
    /// <summary>
    /// Immutable description of one legacy route-profile slot. LegacyContext is opaque to
    /// the migration coordinator and is interpreted only by the injected starter-runtime
    /// factory. This keeps PlayerPrefs and Unity out of the durable application layer.
    /// </summary>
    public sealed class LegacyCharacterProfile
    {
        public LegacyCharacterProfile(
            int slotIndex,
            string displayName,
            StableId sourceCharacterDefinitionStableId,
            StableId classDefinitionStableId,
            string sourceFingerprint,
            object legacyContext)
        {
            PlayerAccountSnapshot.ValidateSlotIndex(slotIndex);
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

    public sealed class SaveMigrationResult
    {
        public SaveMigrationResult(
            CharacterSetupStatus status,
            string diagnostic,
            PlayerAccountSnapshot account,
            IEnumerable<int> migratedSlots)
        {
            Status = status;
            Diagnostic = diagnostic ?? string.Empty;
            Account = account;
            MigratedSlots = new ReadOnlyCollection<int>(
                new List<int>(migratedSlots ?? Array.Empty<int>()));
        }

        public CharacterSetupStatus Status { get; }

        public string Diagnostic { get; }

        public PlayerAccountSnapshot Account { get; }

        public IReadOnlyList<int> MigratedSlots { get; }

        public bool Succeeded
        {
            get { return Status != CharacterSetupStatus.Rejected; }
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
    public sealed class SaveMigration
    {
        private readonly PlayerAccountSaveState accountAuthority;
        private readonly IStarterCharacterLiveGraphFactory starterFactory;
        private readonly Func<PlayerAccountSnapshot, PlayerAccountStoreResult>
            saveAccount;

        public SaveMigration(
            PlayerAccountSaveState accountAuthority,
            IStarterCharacterLiveGraphFactory starterFactory,
            Func<PlayerAccountSnapshot, PlayerAccountStoreResult> saveAccount)
        {
            this.accountAuthority = accountAuthority
                ?? throw new ArgumentNullException(nameof(accountAuthority));
            this.starterFactory = starterFactory
                ?? throw new ArgumentNullException(nameof(starterFactory));
            this.saveAccount = saveAccount
                ?? throw new ArgumentNullException(nameof(saveAccount));
        }

        public SaveMigrationResult Migrate(
            IEnumerable<LegacyCharacterProfile> legacyProfiles)
        {
            List<LegacyCharacterProfile> profiles =
                (legacyProfiles ?? Array.Empty<LegacyCharacterProfile>())
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
                return new SaveMigrationResult(
                    CharacterSetupStatus.ExactNoChange,
                    string.Empty,
                    accountAuthority.Current,
                    Array.Empty<int>());
            }

            // A single empty-slot request is a character-creation transaction, including
            // first-character creation. Batch PlayerPrefs migration remains on the deterministic
            // migration path below so all legacy slots can be imported in one aggregate save.
            CharacterSetupFlow activeComposition;
            if (profiles.Count == 1
                && accountAuthority.Current.CharacterAt(
                    profiles[0].SlotIndex) == null
                && CharacterSetupFlow.TryResolve(
                    accountAuthority,
                    out activeComposition))
            {
                CharacterSetupResult created =
                    activeComposition.CreateAndSelect(profiles[0]);
                if (created == null || !created.Succeeded)
                {
                    return Reject(
                        created == null
                            ? "character-create-transaction-result-null"
                            : "character-create-transaction-rejected:"
                                + created.Diagnostic);
                }
                return new SaveMigrationResult(
                    CharacterSetupStatus.Migrated,
                    string.Empty,
                    created.Account,
                    new[] { profiles[0].SlotIndex });
            }

            PlayerAccountSaveStateSnapshot rollback =
                accountAuthority.ExportSnapshot();
            var migrated = new List<int>();
            for (int index = 0; index < profiles.Count; index++)
            {
                LegacyCharacterProfile profile = profiles[index];
                StableId exactCharacterId = ExactCharacterId(
                    accountAuthority.Current.AccountStableId,
                    profile);
                CharacterInstanceSnapshot occupied =
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

                ICharacterLiveGraph graph = null;
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

                    IReadOnlyList<SavePartSnapshot> components =
                        PlayerAccountRestoreFlow.ExportComponents(
                            graph.SaveAdapters);
                    CharacterInstanceSnapshot character =
                        new CharacterInstanceSnapshot(
                            exactCharacterId,
                            profile.ClassDefinitionStableId,
                            profile.SlotIndex,
                            profile.DisplayName,
                            0L,
                            components);
                    PlayerAccountSaveResult created = accountAuthority.Apply(
                        PlayerAccountSaveCommand.CreateCharacter(
                            MigrationOperationId(
                                accountAuthority.Current.AccountStableId,
                                profile),
                            accountAuthority.Current.Revision,
                            character));
                    if (created == null
                        || (created.Status != PlayerAccountSaveStatus.Applied
                            && created.Status
                                != PlayerAccountSaveStatus
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
                            + DescribeException(exception));
                }
            }

            if (migrated.Count == 0)
            {
                return new SaveMigrationResult(
                    CharacterSetupStatus.ExactNoChange,
                    string.Empty,
                    accountAuthority.Current,
                    migrated);
            }

            PlayerAccountStoreResult stored;
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

            return new SaveMigrationResult(
                CharacterSetupStatus.Migrated,
                string.Empty,
                stored.Snapshot,
                migrated);
        }

        public static StableId ExactCharacterId(
            StableId accountStableId,
            LegacyCharacterProfile profile)
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

        private SaveMigrationResult Reject(
            string diagnostic)
        {
            return new SaveMigrationResult(
                CharacterSetupStatus.Rejected,
                diagnostic,
                accountAuthority.Current,
                Array.Empty<int>());
        }

        private static bool TryValidateStarterGraph(
            ICharacterLiveGraph graph,
            LegacyCharacterProfile profile,
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
                ISavePart adapter = graph.SaveAdapters[index];
                if (adapter == null
                    || adapter.Definition == null
                    || !ids.Add(adapter.Definition.ComponentStableId))
                {
                    rejectionCode = "legacy-starter-adapter-invalid";
                    return false;
                }
            }
            for (int index = 0;
                index < CharacterSetupFlow
                    .RequiredCharacterComponentIds.Count;
                index++)
            {
                StableId required = CharacterSetupFlow
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
            LegacyCharacterProfile profile)
        {
            return StableId.Parse(
                "operation.character-migration-" + Hash(
                    accountStableId
                    + "|"
                    + profile.SlotIndex.ToString(CultureInfo.InvariantCulture)
                    + "|"
                    + profile.SourceFingerprint));
        }

        private void RollBack(PlayerAccountSaveStateSnapshot rollback)
        {
            string ignored;
            accountAuthority.TryImport(rollback, out ignored);
        }

        private static void Dispose(ICharacterLiveGraph graph)
        {
            if (graph != null && !graph.IsDisposed)
            {
                graph.Dispose();
            }
        }

        private static string DescribeException(Exception exception)
        {
            if (exception == null)
            {
                return "Exception";
            }

            Exception root = exception.GetBaseException() ?? exception;
            string description = exception.GetType().Name;
            if (!ReferenceEquals(root, exception))
            {
                description += "->" + root.GetType().Name;
            }

            if (string.IsNullOrWhiteSpace(root.Message))
            {
                return description;
            }

            string message = root.Message
                .Replace('\r', ' ')
                .Replace('\n', ' ')
                .Trim();
            if (message.Length > 256)
            {
                message = message.Substring(0, 256);
            }
            return description + ":" + message;
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
