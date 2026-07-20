using System;
using ShooterMover.Application.Inventory.LoadoutScreen;
using ShooterMover.Application.Rewards.Strongboxes;
using ShooterMover.Contracts.Holdings;
using ShooterMover.Contracts.Progression.Experience;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Economy.Money;
using ShooterMover.Domain.Economy.Scrap;
using ShooterMover.Domain.Persistence.Accounts;
using ShooterMover.Domain.Progression.Skills;

namespace ShooterMover.Application.Persistence.Components
{
    public enum SaveComponentValidationStatusV1
    {
        Accepted = 1,
        Rejected = 2,
    }

    public sealed class SaveComponentValidationResultV1
    {
        private SaveComponentValidationResultV1(
            SaveComponentValidationStatusV1 status,
            string rejectionCode)
        {
            Status = status;
            RejectionCode = rejectionCode ?? string.Empty;
        }

        public SaveComponentValidationStatusV1 Status { get; }

        public string RejectionCode { get; }

        public bool Succeeded
        {
            get { return Status == SaveComponentValidationStatusV1.Accepted; }
        }

        public static SaveComponentValidationResultV1 Accept()
        {
            return new SaveComponentValidationResultV1(
                SaveComponentValidationStatusV1.Accepted,
                string.Empty);
        }

        public static SaveComponentValidationResultV1 Reject(string rejectionCode)
        {
            return new SaveComponentValidationResultV1(
                SaveComponentValidationStatusV1.Rejected,
                string.IsNullOrWhiteSpace(rejectionCode)
                    ? "save-component-validation-rejected"
                    : rejectionCode.Trim());
        }
    }

    public sealed class SaveComponentApplyResultV1
    {
        private SaveComponentApplyResultV1(bool succeeded, string rejectionCode)
        {
            Succeeded = succeeded;
            RejectionCode = rejectionCode ?? string.Empty;
        }

        public bool Succeeded { get; }

        public string RejectionCode { get; }

        public static SaveComponentApplyResultV1 Applied()
        {
            return new SaveComponentApplyResultV1(true, string.Empty);
        }

        public static SaveComponentApplyResultV1 Rejected(string rejectionCode)
        {
            return new SaveComponentApplyResultV1(
                false,
                string.IsNullOrWhiteSpace(rejectionCode)
                    ? "save-component-apply-rejected"
                    : rejectionCode.Trim());
        }
    }

    public sealed class SaveComponentDefinitionV1
    {
        public SaveComponentDefinitionV1(
            StableId componentStableId,
            int schemaVersion,
            string contentVersion,
            bool isRequired)
        {
            ComponentStableId = componentStableId
                ?? throw new ArgumentNullException(nameof(componentStableId));
            if (schemaVersion < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(schemaVersion));
            }
            if (string.IsNullOrWhiteSpace(contentVersion))
            {
                throw new ArgumentException(
                    "A component content version is required.",
                    nameof(contentVersion));
            }

            SchemaVersion = schemaVersion;
            ContentVersion = contentVersion.Trim();
            IsRequired = isRequired;
        }

        public StableId ComponentStableId { get; }

        public int SchemaVersion { get; }

        public string ContentVersion { get; }

        public bool IsRequired { get; }
    }

    public interface IPreparedSaveComponentRestoreV1 : IDisposable
    {
        StableId ComponentStableId { get; }

        void Commit();

        void Rollback();
    }

    public sealed class SaveComponentPrepareResultV1
    {
        private SaveComponentPrepareResultV1(
            bool succeeded,
            string rejectionCode,
            IPreparedSaveComponentRestoreV1 preparedRestore)
        {
            Succeeded = succeeded;
            RejectionCode = rejectionCode ?? string.Empty;
            PreparedRestore = preparedRestore;
        }

        public bool Succeeded { get; }

        public string RejectionCode { get; }

        public IPreparedSaveComponentRestoreV1 PreparedRestore { get; }

        public static SaveComponentPrepareResultV1 Prepared(
            IPreparedSaveComponentRestoreV1 preparedRestore)
        {
            return new SaveComponentPrepareResultV1(
                true,
                string.Empty,
                preparedRestore
                    ?? throw new ArgumentNullException(nameof(preparedRestore)));
        }

        public static SaveComponentPrepareResultV1 Rejected(string rejectionCode)
        {
            return new SaveComponentPrepareResultV1(
                false,
                string.IsNullOrWhiteSpace(rejectionCode)
                    ? "save-component-prepare-rejected"
                    : rejectionCode.Trim(),
                null);
        }
    }

    public interface ISaveComponentAdapterV1
    {
        SaveComponentDefinitionV1 Definition { get; }

        SaveComponentSnapshotV1 ExportComponent();

        SaveComponentPrepareResultV1 PrepareRestore(
            SaveComponentSnapshotV1 component);
    }

    /// <summary>
    /// Wraps one existing immutable authority snapshot. Validation happens against a
    /// decoded copy before a prepared restore is returned. Live replacement is deferred
    /// until the aggregate coordinator commits every prepared component.
    /// </summary>
    public sealed class AuthoritySnapshotSaveComponentAdapterV1<TSnapshot> :
        ISaveComponentAdapterV1
        where TSnapshot : class
    {
        private readonly Func<TSnapshot> exportSnapshot;
        private readonly Func<TSnapshot, SaveComponentValidationResultV1>
            validateSnapshot;
        private readonly Func<TSnapshot, SaveComponentApplyResultV1>
            applySnapshot;

        public AuthoritySnapshotSaveComponentAdapterV1(
            SaveComponentDefinitionV1 definition,
            Func<TSnapshot> exportSnapshot,
            Func<TSnapshot, SaveComponentValidationResultV1> validateSnapshot,
            Func<TSnapshot, SaveComponentApplyResultV1> applySnapshot)
        {
            Definition = definition
                ?? throw new ArgumentNullException(nameof(definition));
            this.exportSnapshot = exportSnapshot
                ?? throw new ArgumentNullException(nameof(exportSnapshot));
            this.validateSnapshot = validateSnapshot
                ?? throw new ArgumentNullException(nameof(validateSnapshot));
            this.applySnapshot = applySnapshot
                ?? throw new ArgumentNullException(nameof(applySnapshot));
        }

        public SaveComponentDefinitionV1 Definition { get; }

        public SaveComponentSnapshotV1 ExportComponent()
        {
            TSnapshot snapshot = exportSnapshot();
            if (snapshot == null)
            {
                throw new InvalidOperationException(
                    "The existing authority returned a null save snapshot.");
            }

            SaveComponentValidationResultV1 integrity =
                CanonicalSnapshotIntegrityV1.Validate(snapshot);
            if (!integrity.Succeeded)
            {
                throw new InvalidOperationException(
                    "The existing authority exported an invalid snapshot: "
                    + integrity.RejectionCode);
            }

            return new SaveComponentSnapshotV1(
                Definition.ComponentStableId,
                Definition.SchemaVersion,
                Definition.ContentVersion,
                CanonicalSnapshotCodecV1.Serialize(snapshot));
        }

        public SaveComponentPrepareResultV1 PrepareRestore(
            SaveComponentSnapshotV1 component)
        {
            if (component == null)
            {
                return SaveComponentPrepareResultV1.Rejected(
                    "save-component-missing");
            }
            if (component.ComponentStableId != Definition.ComponentStableId)
            {
                return SaveComponentPrepareResultV1.Rejected(
                    "save-component-id-mismatch");
            }
            if (component.SchemaVersion != Definition.SchemaVersion)
            {
                return SaveComponentPrepareResultV1.Rejected(
                    "save-component-schema-unsupported");
            }
            if (!string.Equals(
                component.ContentVersion,
                Definition.ContentVersion,
                StringComparison.Ordinal))
            {
                return SaveComponentPrepareResultV1.Rejected(
                    "save-component-content-version-unsupported");
            }

            TSnapshot decoded;
            string decodeError;
            if (!CanonicalSnapshotCodecV1.TryDeserialize(
                component.CanonicalPayload,
                out decoded,
                out decodeError))
            {
                return SaveComponentPrepareResultV1.Rejected(
                    "save-component-payload-invalid:" + decodeError);
            }

            SaveComponentValidationResultV1 integrity =
                CanonicalSnapshotIntegrityV1.Validate(decoded);
            if (!integrity.Succeeded)
            {
                return SaveComponentPrepareResultV1.Rejected(
                    integrity.RejectionCode);
            }

            SaveComponentValidationResultV1 semantic =
                validateSnapshot(decoded);
            if (semantic == null || !semantic.Succeeded)
            {
                return SaveComponentPrepareResultV1.Rejected(
                    semantic == null
                        ? "save-component-semantic-validator-null"
                        : semantic.RejectionCode);
            }

            TSnapshot previous = exportSnapshot();
            if (previous == null)
            {
                return SaveComponentPrepareResultV1.Rejected(
                    "save-component-current-snapshot-null");
            }

            return SaveComponentPrepareResultV1.Prepared(
                new PreparedRestore(
                    Definition.ComponentStableId,
                    decoded,
                    previous,
                    applySnapshot));
        }

        private sealed class PreparedRestore :
            IPreparedSaveComponentRestoreV1
        {
            private readonly TSnapshot next;
            private readonly TSnapshot previous;
            private readonly Func<TSnapshot, SaveComponentApplyResultV1> apply;
            private bool committed;
            private bool disposed;

            public PreparedRestore(
                StableId componentStableId,
                TSnapshot next,
                TSnapshot previous,
                Func<TSnapshot, SaveComponentApplyResultV1> apply)
            {
                ComponentStableId = componentStableId;
                this.next = next;
                this.previous = previous;
                this.apply = apply;
            }

            public StableId ComponentStableId { get; }

            public void Commit()
            {
                ThrowIfDisposed();
                if (committed)
                {
                    return;
                }

                SaveComponentApplyResultV1 result = apply(next);
                if (result == null || !result.Succeeded)
                {
                    throw new InvalidOperationException(
                        result == null
                            ? "save-component-apply-result-null"
                            : result.RejectionCode);
                }
                committed = true;
            }

            public void Rollback()
            {
                ThrowIfDisposed();
                if (!committed)
                {
                    return;
                }

                SaveComponentApplyResultV1 result = apply(previous);
                if (result == null || !result.Succeeded)
                {
                    throw new InvalidOperationException(
                        result == null
                            ? "save-component-rollback-result-null"
                            : result.RejectionCode);
                }
                committed = false;
            }

            public void Dispose()
            {
                disposed = true;
            }

            private void ThrowIfDisposed()
            {
                if (disposed)
                {
                    throw new ObjectDisposedException(nameof(PreparedRestore));
                }
            }
        }
    }

    public static class KnownSaveComponentDefinitionsV1
    {
        public static SaveComponentDefinitionV1 PlayerExperience(
            bool required = true)
        {
            return Definition(
                "player-experience",
                "player-experience-snapshot-v1",
                required);
        }

        public static SaveComponentDefinitionV1 PlayerHoldings(
            bool required = true)
        {
            return Definition(
                "player-holdings",
                "player-holdings-snapshot-v1",
                required);
        }

        public static SaveComponentDefinitionV1 MoneyWallet(
            bool required = true)
        {
            return Definition("money-wallet", "money-wallet-snapshot-v1", required);
        }

        public static SaveComponentDefinitionV1 ScrapWallet(
            bool required = true)
        {
            return Definition("scrap-wallet", "scrap-wallet-snapshot-v1", required);
        }

        public static SaveComponentDefinitionV1 RankedSkillAllocation(
            bool required = true)
        {
            return Definition(
                "ranked-skill-allocation",
                "ranked-skill-allocation-v2",
                required);
        }

        public static SaveComponentDefinitionV1 ExactInstanceLoadout(
            bool required = true)
        {
            return Definition(
                "exact-instance-loadout",
                "inventory-loadout-snapshot-v1",
                required);
        }

        public static SaveComponentDefinitionV1 StrongboxState(
            bool required = false)
        {
            return Definition(
                "strongbox-state",
                "strongbox-opening-snapshot-v1",
                required);
        }

        public static SaveComponentDefinitionV1 CharacterStatistics(
            bool required = false)
        {
            return Definition(
                "character-statistics",
                "character-statistics-snapshot-v1",
                required);
        }

        private static SaveComponentDefinitionV1 Definition(
            string value,
            string contentVersion,
            bool required)
        {
            return new SaveComponentDefinitionV1(
                StableId.Create("save-component", value),
                1,
                contentVersion,
                required);
        }
    }

    /// <summary>
    /// Compile-time mappings to the canonical snapshots that already exist. The
    /// validation and apply delegates remain owned by composition, so these adapters
    /// never create replacement XP, inventory, wallet, skill, loadout, or BOX authorities.
    /// </summary>
    public static class KnownSaveComponentAdaptersV1
    {
        public static AuthoritySnapshotSaveComponentAdapterV1<
            PlayerExperienceSnapshotV1> PlayerExperience(
            Func<PlayerExperienceSnapshotV1> exportSnapshot,
            Func<PlayerExperienceSnapshotV1, SaveComponentValidationResultV1>
                validateSnapshot,
            Func<PlayerExperienceSnapshotV1, SaveComponentApplyResultV1>
                applySnapshot,
            bool required = true)
        {
            return new AuthoritySnapshotSaveComponentAdapterV1<
                PlayerExperienceSnapshotV1>(
                KnownSaveComponentDefinitionsV1.PlayerExperience(required),
                exportSnapshot,
                validateSnapshot,
                applySnapshot);
        }

        public static AuthoritySnapshotSaveComponentAdapterV1<
            PlayerHoldingsSnapshotV1> PlayerHoldings(
            Func<PlayerHoldingsSnapshotV1> exportSnapshot,
            Func<PlayerHoldingsSnapshotV1, SaveComponentValidationResultV1>
                validateSnapshot,
            Func<PlayerHoldingsSnapshotV1, SaveComponentApplyResultV1>
                applySnapshot,
            bool required = true)
        {
            return new AuthoritySnapshotSaveComponentAdapterV1<
                PlayerHoldingsSnapshotV1>(
                KnownSaveComponentDefinitionsV1.PlayerHoldings(required),
                exportSnapshot,
                validateSnapshot,
                applySnapshot);
        }

        public static AuthoritySnapshotSaveComponentAdapterV1<MoneyWalletSnapshot>
            MoneyWallet(
                Func<MoneyWalletSnapshot> exportSnapshot,
                Func<MoneyWalletSnapshot, SaveComponentValidationResultV1>
                    validateSnapshot,
                Func<MoneyWalletSnapshot, SaveComponentApplyResultV1>
                    applySnapshot,
                bool required = true)
        {
            return new AuthoritySnapshotSaveComponentAdapterV1<MoneyWalletSnapshot>(
                KnownSaveComponentDefinitionsV1.MoneyWallet(required),
                exportSnapshot,
                validateSnapshot,
                applySnapshot);
        }

        public static AuthoritySnapshotSaveComponentAdapterV1<ScrapSnapshotV1>
            ScrapWallet(
                Func<ScrapSnapshotV1> exportSnapshot,
                Func<ScrapSnapshotV1, SaveComponentValidationResultV1>
                    validateSnapshot,
                Func<ScrapSnapshotV1, SaveComponentApplyResultV1>
                    applySnapshot,
                bool required = true)
        {
            return new AuthoritySnapshotSaveComponentAdapterV1<ScrapSnapshotV1>(
                KnownSaveComponentDefinitionsV1.ScrapWallet(required),
                exportSnapshot,
                validateSnapshot,
                applySnapshot);
        }

        public static AuthoritySnapshotSaveComponentAdapterV1<
            RankedSkillAllocationSnapshotV2> RankedSkillAllocation(
            Func<RankedSkillAllocationSnapshotV2> exportSnapshot,
            Func<RankedSkillAllocationSnapshotV2, SaveComponentValidationResultV1>
                validateSnapshot,
            Func<RankedSkillAllocationSnapshotV2, SaveComponentApplyResultV1>
                applySnapshot,
            bool required = true)
        {
            return new AuthoritySnapshotSaveComponentAdapterV1<
                RankedSkillAllocationSnapshotV2>(
                KnownSaveComponentDefinitionsV1.RankedSkillAllocation(required),
                exportSnapshot,
                validateSnapshot,
                applySnapshot);
        }

        public static AuthoritySnapshotSaveComponentAdapterV1<
            InventoryLoadoutAuthoritySnapshotV1> ExactInstanceLoadout(
            Func<InventoryLoadoutAuthoritySnapshotV1> exportSnapshot,
            Func<InventoryLoadoutAuthoritySnapshotV1,
                SaveComponentValidationResultV1> validateSnapshot,
            Func<InventoryLoadoutAuthoritySnapshotV1, SaveComponentApplyResultV1>
                applySnapshot,
            bool required = true)
        {
            return new AuthoritySnapshotSaveComponentAdapterV1<
                InventoryLoadoutAuthoritySnapshotV1>(
                KnownSaveComponentDefinitionsV1.ExactInstanceLoadout(required),
                exportSnapshot,
                validateSnapshot,
                applySnapshot);
        }

        public static AuthoritySnapshotSaveComponentAdapterV1<
            StrongboxOpeningSnapshotV1> StrongboxState(
            Func<StrongboxOpeningSnapshotV1> exportSnapshot,
            Func<StrongboxOpeningSnapshotV1, SaveComponentValidationResultV1>
                validateSnapshot,
            Func<StrongboxOpeningSnapshotV1, SaveComponentApplyResultV1>
                applySnapshot,
            bool required = false)
        {
            return new AuthoritySnapshotSaveComponentAdapterV1<
                StrongboxOpeningSnapshotV1>(
                KnownSaveComponentDefinitionsV1.StrongboxState(required),
                exportSnapshot,
                validateSnapshot,
                applySnapshot);
        }

        public static AuthoritySnapshotSaveComponentAdapterV1<TSnapshot>
            CharacterStatistics<TSnapshot>(
            Func<TSnapshot> exportSnapshot,
            Func<TSnapshot, SaveComponentValidationResultV1> validateSnapshot,
            Func<TSnapshot, SaveComponentApplyResultV1> applySnapshot,
            bool required = false)
            where TSnapshot : class
        {
            return new AuthoritySnapshotSaveComponentAdapterV1<TSnapshot>(
                KnownSaveComponentDefinitionsV1.CharacterStatistics(required),
                exportSnapshot,
                validateSnapshot,
                applySnapshot);
        }
    }
}
