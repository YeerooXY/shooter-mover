using System;
using System.Text;
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
            bool isRequired,
            int restoreOrder)
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

            if (restoreOrder < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(restoreOrder));
            }
            SchemaVersion = schemaVersion;
            ContentVersion = contentVersion.Trim();
            IsRequired = isRequired;
            RestoreOrder = restoreOrder;
        }

        public StableId ComponentStableId { get; }

        public int SchemaVersion { get; }

        public string ContentVersion { get; }

        public bool IsRequired { get; }

        public int RestoreOrder { get; }
    }

    public interface ISaveComponentPayloadCodecV1<TSnapshot>
        where TSnapshot : class
    {
        string ContractId { get; }

        string Encode(TSnapshot snapshot);

        bool TryDecode(
            string canonicalPayload,
            out TSnapshot snapshot,
            out string rejectionCode);

        SaveComponentValidationResultV1 Validate(TSnapshot snapshot);
    }

    public enum SaveComponentCommitStatusV1
    {
        Applied = 1,
        FailedAndCompensated = 2,
        FailedCompensationIncomplete = 3,
    }

    public sealed class SaveComponentCommitResultV1
    {
        public SaveComponentCommitResultV1(
            SaveComponentCommitStatusV1 status,
            string rejectionCode,
            bool previousSnapshotConfirmed)
        {
            Status = status;
            RejectionCode = rejectionCode ?? string.Empty;
            PreviousSnapshotConfirmed = previousSnapshotConfirmed;
        }

        public SaveComponentCommitStatusV1 Status { get; }

        public string RejectionCode { get; }

        public bool PreviousSnapshotConfirmed { get; }

        public bool Succeeded
        {
            get { return Status == SaveComponentCommitStatusV1.Applied; }
        }
    }

    public sealed class SaveComponentRollbackResultV1
    {
        public SaveComponentRollbackResultV1(
            bool restored,
            string rejectionCode)
        {
            Restored = restored;
            RejectionCode = rejectionCode ?? string.Empty;
        }

        public bool Restored { get; }

        public string RejectionCode { get; }
    }

    public interface IPreparedSaveComponentRestoreV1 : IDisposable
    {
        StableId ComponentStableId { get; }

        bool CommitAttempted { get; }

        bool CommitSucceeded { get; }

        SaveComponentCommitResultV1 Commit();

        SaveComponentRollbackResultV1 Rollback();
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
    /// Typed wrapper over one existing authority snapshot. The supplied apply/import
    /// delegate should be internally atomic whenever the underlying authority supports
    /// that contract. Aggregate correctness does not rely on that assumption: a failed
    /// commit is immediately compensated with the captured prior immutable snapshot and
    /// restoration is confirmed by re-exporting and comparing explicit codec bytes.
    /// </summary>
    public sealed class AuthoritySnapshotSaveComponentAdapterV1<TSnapshot> :
        ISaveComponentAdapterV1
        where TSnapshot : class
    {
        private readonly ISaveComponentPayloadCodecV1<TSnapshot> codec;
        private readonly Func<TSnapshot> exportSnapshot;
        private readonly Func<TSnapshot, SaveComponentValidationResultV1>
            validateSnapshot;
        private readonly Func<TSnapshot, SaveComponentApplyResultV1>
            applySnapshot;

        public AuthoritySnapshotSaveComponentAdapterV1(
            SaveComponentDefinitionV1 definition,
            ISaveComponentPayloadCodecV1<TSnapshot> codec,
            Func<TSnapshot> exportSnapshot,
            Func<TSnapshot, SaveComponentValidationResultV1> validateSnapshot,
            Func<TSnapshot, SaveComponentApplyResultV1> applySnapshot)
        {
            Definition = definition
                ?? throw new ArgumentNullException(nameof(definition));
            this.codec = codec
                ?? throw new ArgumentNullException(nameof(codec));
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
            RequireValid(snapshot, "authority-export");
            string payload = codec.Encode(snapshot);
            RequirePayloadBound(payload);
            return new SaveComponentSnapshotV1(
                Definition.ComponentStableId,
                Definition.SchemaVersion,
                Definition.ContentVersion,
                payload);
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
            if (Encoding.UTF8.GetByteCount(component.CanonicalPayload)
                > SavePersistenceLimitsV1.MaximumComponentPayloadBytes)
            {
                return SaveComponentPrepareResultV1.Rejected(
                    "component-payload-too-large");
            }

            TSnapshot decoded;
            string decodeError;
            if (!codec.TryDecode(
                component.CanonicalPayload,
                out decoded,
                out decodeError))
            {
                return SaveComponentPrepareResultV1.Rejected(decodeError);
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
            try
            {
                RequireValid(previous, "authority-current");
                return SaveComponentPrepareResultV1.Prepared(
                    new PreparedRestore(
                        Definition.ComponentStableId,
                        codec,
                        decoded,
                        previous,
                        exportSnapshot,
                        applySnapshot));
            }
            catch (Exception exception)
            {
                return SaveComponentPrepareResultV1.Rejected(
                    "save-component-current-snapshot-invalid:"
                        + exception.GetType().Name);
            }
        }

        private void RequireValid(TSnapshot snapshot, string prefix)
        {
            if (snapshot == null)
            {
                throw new InvalidOperationException(prefix + "-snapshot-null");
            }
            SaveComponentValidationResultV1 codecValidation =
                codec.Validate(snapshot);
            if (codecValidation == null || !codecValidation.Succeeded)
            {
                throw new InvalidOperationException(
                    prefix + "-codec-validation-failed:"
                        + (codecValidation == null
                            ? "null"
                            : codecValidation.RejectionCode));
            }
            SaveComponentValidationResultV1 semantic =
                validateSnapshot(snapshot);
            if (semantic == null || !semantic.Succeeded)
            {
                throw new InvalidOperationException(
                    prefix + "-semantic-validation-failed:"
                        + (semantic == null
                            ? "null"
                            : semantic.RejectionCode));
            }
        }

        private static void RequirePayloadBound(string payload)
        {
            if (payload == null
                || Encoding.UTF8.GetByteCount(payload)
                    > SavePersistenceLimitsV1.MaximumComponentPayloadBytes)
            {
                throw new InvalidOperationException(
                    "component-payload-too-large");
            }
        }

        private sealed class PreparedRestore :
            IPreparedSaveComponentRestoreV1
        {
            private readonly ISaveComponentPayloadCodecV1<TSnapshot> codec;
            private readonly TSnapshot next;
            private readonly TSnapshot previous;
            private readonly string previousPayload;
            private readonly Func<TSnapshot> export;
            private readonly Func<TSnapshot, SaveComponentApplyResultV1> apply;
            private bool disposed;

            public PreparedRestore(
                StableId componentStableId,
                ISaveComponentPayloadCodecV1<TSnapshot> codec,
                TSnapshot next,
                TSnapshot previous,
                Func<TSnapshot> export,
                Func<TSnapshot, SaveComponentApplyResultV1> apply)
            {
                ComponentStableId = componentStableId;
                this.codec = codec;
                this.next = next;
                this.previous = previous;
                previousPayload = codec.Encode(previous);
                this.export = export;
                this.apply = apply;
            }

            public StableId ComponentStableId { get; }

            public bool CommitAttempted { get; private set; }

            public bool CommitSucceeded { get; private set; }

            public SaveComponentCommitResultV1 Commit()
            {
                ThrowIfDisposed();
                if (CommitAttempted)
                {
                    return CommitSucceeded
                        ? new SaveComponentCommitResultV1(
                            SaveComponentCommitStatusV1.Applied,
                            string.Empty,
                            false)
                        : new SaveComponentCommitResultV1(
                            IsPreviousConfirmed()
                                ? SaveComponentCommitStatusV1
                                    .FailedAndCompensated
                                : SaveComponentCommitStatusV1
                                    .FailedCompensationIncomplete,
                            "save-component-commit-already-attempted",
                            IsPreviousConfirmed());
                }

                CommitAttempted = true;
                string failureCode = string.Empty;
                try
                {
                    SaveComponentApplyResultV1 result = apply(next);
                    if (result != null && result.Succeeded)
                    {
                        CommitSucceeded = true;
                        return new SaveComponentCommitResultV1(
                            SaveComponentCommitStatusV1.Applied,
                            string.Empty,
                            false);
                    }
                    failureCode = result == null
                        ? "save-component-apply-result-null"
                        : result.RejectionCode;
                }
                catch (Exception exception)
                {
                    failureCode = "save-component-apply-threw:"
                        + exception.GetType().Name;
                }

                SaveComponentRollbackResultV1 compensation =
                    RestorePrevious("failing-component-compensation");
                return new SaveComponentCommitResultV1(
                    compensation.Restored
                        ? SaveComponentCommitStatusV1.FailedAndCompensated
                        : SaveComponentCommitStatusV1
                            .FailedCompensationIncomplete,
                    failureCode
                        + (string.IsNullOrEmpty(compensation.RejectionCode)
                            ? string.Empty
                            : ";compensation="
                                + compensation.RejectionCode),
                    compensation.Restored);
            }

            public SaveComponentRollbackResultV1 Rollback()
            {
                ThrowIfDisposed();
                if (!CommitSucceeded)
                {
                    bool alreadyRestored = IsPreviousConfirmed();
                    return new SaveComponentRollbackResultV1(
                        alreadyRestored,
                        alreadyRestored
                            ? string.Empty
                            : "rollback-requested-before-successful-commit");
                }

                SaveComponentRollbackResultV1 result =
                    RestorePrevious("earlier-component-rollback");
                if (result.Restored)
                {
                    CommitSucceeded = false;
                }
                return result;
            }

            public void Dispose()
            {
                disposed = true;
            }

            private SaveComponentRollbackResultV1 RestorePrevious(
                string phase)
            {
                string applyFailure = string.Empty;
                try
                {
                    SaveComponentApplyResultV1 result = apply(previous);
                    if (result == null || !result.Succeeded)
                    {
                        applyFailure = result == null
                            ? phase + "-apply-result-null"
                            : phase + "-apply-rejected:"
                                + result.RejectionCode;
                    }
                }
                catch (Exception exception)
                {
                    applyFailure = phase + "-apply-threw:"
                        + exception.GetType().Name;
                }

                bool confirmed = IsPreviousConfirmed();
                return new SaveComponentRollbackResultV1(
                    confirmed,
                    confirmed
                        ? string.Empty
                        : string.IsNullOrEmpty(applyFailure)
                            ? phase + "-fingerprint-not-restored"
                            : applyFailure + ";"
                                + phase + "-fingerprint-not-restored");
            }

            private bool IsPreviousConfirmed()
            {
                try
                {
                    TSnapshot current = export();
                    if (current == null)
                    {
                        return false;
                    }
                    SaveComponentValidationResultV1 validation =
                        codec.Validate(current);
                    return validation != null
                        && validation.Succeeded
                        && string.Equals(
                            codec.Encode(current),
                            previousPayload,
                            StringComparison.Ordinal);
                }
                catch
                {
                    return false;
                }
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
                "player-experience-explicit-v1",
                required,
                100);
        }

        public static SaveComponentDefinitionV1 PlayerHoldings(
            bool required = true)
        {
            return Definition(
                "player-holdings",
                "player-holdings-explicit-v1",
                required,
                200);
        }

        public static SaveComponentDefinitionV1 MoneyWallet(
            bool required = true)
        {
            return Definition(
                "money-wallet",
                "money-wallet-explicit-v1",
                required,
                300);
        }

        public static SaveComponentDefinitionV1 ScrapWallet(
            bool required = true)
        {
            return Definition(
                "scrap-wallet",
                "scrap-wallet-explicit-v1",
                required,
                400);
        }

        public static SaveComponentDefinitionV1 RankedSkillAllocation(
            bool required = true)
        {
            return Definition(
                "ranked-skill-allocation",
                "ranked-skill-allocation-explicit-v2",
                required,
                500);
        }

        public static SaveComponentDefinitionV1 ExactInstanceLoadout(
            bool required = true)
        {
            return Definition(
                "exact-instance-loadout",
                "inventory-loadout-explicit-v1",
                required,
                600);
        }

        public static SaveComponentDefinitionV1 StrongboxState(
            bool required = false)
        {
            return Definition(
                "strongbox-state",
                "strongbox-opening-explicit-v1",
                required,
                700);
        }

        private static SaveComponentDefinitionV1 Definition(
            string value,
            string contentVersion,
            bool required,
            int restoreOrder)
        {
            return new SaveComponentDefinitionV1(
                StableId.Create("save-component", value),
                1,
                contentVersion,
                required,
                restoreOrder);
        }
    }

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
            return Adapter(
                KnownSaveComponentDefinitionsV1.PlayerExperience(required),
                KnownSaveComponentCodecsV1.PlayerExperience,
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
            return Adapter(
                KnownSaveComponentDefinitionsV1.PlayerHoldings(required),
                KnownSaveComponentCodecsV1.PlayerHoldings,
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
            return Adapter(
                KnownSaveComponentDefinitionsV1.MoneyWallet(required),
                KnownSaveComponentCodecsV1.MoneyWallet,
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
            return Adapter(
                KnownSaveComponentDefinitionsV1.ScrapWallet(required),
                KnownSaveComponentCodecsV1.ScrapWallet,
                exportSnapshot,
                validateSnapshot,
                applySnapshot);
        }

        public static AuthoritySnapshotSaveComponentAdapterV1<
            RankedSkillAllocationSnapshotV2> RankedSkillAllocation(
            Func<RankedSkillAllocationSnapshotV2> exportSnapshot,
            Func<RankedSkillAllocationSnapshotV2,
                SaveComponentValidationResultV1> validateSnapshot,
            Func<RankedSkillAllocationSnapshotV2, SaveComponentApplyResultV1>
                applySnapshot,
            bool required = true)
        {
            return Adapter(
                KnownSaveComponentDefinitionsV1.RankedSkillAllocation(required),
                KnownSaveComponentCodecsV1.RankedSkillAllocation,
                exportSnapshot,
                validateSnapshot,
                applySnapshot);
        }

        public static AuthoritySnapshotSaveComponentAdapterV1<
            InventoryLoadoutAuthoritySnapshotV1> ExactInstanceLoadout(
            Func<InventoryLoadoutAuthoritySnapshotV1> exportSnapshot,
            Func<InventoryLoadoutAuthoritySnapshotV1,
                SaveComponentValidationResultV1> validateSnapshot,
            Func<InventoryLoadoutAuthoritySnapshotV1,
                SaveComponentApplyResultV1> applySnapshot,
            bool required = true)
        {
            return Adapter(
                KnownSaveComponentDefinitionsV1.ExactInstanceLoadout(required),
                KnownSaveComponentCodecsV1.ExactInstanceLoadout,
                exportSnapshot,
                validateSnapshot,
                applySnapshot);
        }

        public static AuthoritySnapshotSaveComponentAdapterV1<
            StrongboxOpeningSnapshotV1> StrongboxState(
            Func<StrongboxOpeningSnapshotV1> exportSnapshot,
            Func<StrongboxOpeningSnapshotV1,
                SaveComponentValidationResultV1> validateSnapshot,
            Func<StrongboxOpeningSnapshotV1, SaveComponentApplyResultV1>
                applySnapshot,
            bool required = false)
        {
            return Adapter(
                KnownSaveComponentDefinitionsV1.StrongboxState(required),
                KnownSaveComponentCodecsV1.StrongboxState,
                exportSnapshot,
                validateSnapshot,
                applySnapshot);
        }

        private static AuthoritySnapshotSaveComponentAdapterV1<TSnapshot>
            Adapter<TSnapshot>(
                SaveComponentDefinitionV1 definition,
                ISaveComponentPayloadCodecV1<TSnapshot> codec,
                Func<TSnapshot> exportSnapshot,
                Func<TSnapshot, SaveComponentValidationResultV1>
                    validateSnapshot,
                Func<TSnapshot, SaveComponentApplyResultV1> applySnapshot)
            where TSnapshot : class
        {
            return new AuthoritySnapshotSaveComponentAdapterV1<TSnapshot>(
                definition,
                codec,
                exportSnapshot,
                validateSnapshot,
                applySnapshot);
        }
    }
}
