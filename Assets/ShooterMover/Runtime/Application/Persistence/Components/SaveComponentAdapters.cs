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
    public enum SaveComponentValidationStatus
    {
        Accepted = 1,
        Rejected = 2,
    }

    public sealed class SaveComponentValidationResult
    {
        private SaveComponentValidationResult(
            SaveComponentValidationStatus status,
            string rejectionCode)
        {
            Status = status;
            RejectionCode = rejectionCode ?? string.Empty;
        }

        public SaveComponentValidationStatus Status { get; }

        public string RejectionCode { get; }

        public bool Succeeded
        {
            get { return Status == SaveComponentValidationStatus.Accepted; }
        }

        public static SaveComponentValidationResult Accept()
        {
            return new SaveComponentValidationResult(
                SaveComponentValidationStatus.Accepted,
                string.Empty);
        }

        public static SaveComponentValidationResult Reject(string rejectionCode)
        {
            return new SaveComponentValidationResult(
                SaveComponentValidationStatus.Rejected,
                string.IsNullOrWhiteSpace(rejectionCode)
                    ? "save-component-validation-rejected"
                    : rejectionCode.Trim());
        }
    }

    public sealed class SaveComponentApplyResult
    {
        private SaveComponentApplyResult(bool succeeded, string rejectionCode)
        {
            Succeeded = succeeded;
            RejectionCode = rejectionCode ?? string.Empty;
        }

        public bool Succeeded { get; }

        public string RejectionCode { get; }

        public static SaveComponentApplyResult Applied()
        {
            return new SaveComponentApplyResult(true, string.Empty);
        }

        public static SaveComponentApplyResult Rejected(string rejectionCode)
        {
            return new SaveComponentApplyResult(
                false,
                string.IsNullOrWhiteSpace(rejectionCode)
                    ? "save-component-apply-rejected"
                    : rejectionCode.Trim());
        }
    }

    public sealed class SaveComponentDefinition
    {
        public SaveComponentDefinition(
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

    public interface ISaveComponentPayloadCodec<TSnapshot>
        where TSnapshot : class
    {
        string ContractId { get; }

        string Encode(TSnapshot snapshot);

        bool TryDecode(
            string canonicalPayload,
            out TSnapshot snapshot,
            out string rejectionCode);

        SaveComponentValidationResult Validate(TSnapshot snapshot);
    }

    public enum SaveComponentCommitStatus
    {
        Applied = 1,
        FailedAndCompensated = 2,
        FailedCompensationIncomplete = 3,
    }

    public sealed class SaveComponentCommitResult
    {
        public SaveComponentCommitResult(
            SaveComponentCommitStatus status,
            string rejectionCode,
            bool previousSnapshotConfirmed)
        {
            Status = status;
            RejectionCode = rejectionCode ?? string.Empty;
            PreviousSnapshotConfirmed = previousSnapshotConfirmed;
        }

        public SaveComponentCommitStatus Status { get; }

        public string RejectionCode { get; }

        public bool PreviousSnapshotConfirmed { get; }

        public bool Succeeded
        {
            get { return Status == SaveComponentCommitStatus.Applied; }
        }
    }

    public sealed class SaveComponentRollbackResult
    {
        public SaveComponentRollbackResult(
            bool restored,
            string rejectionCode)
        {
            Restored = restored;
            RejectionCode = rejectionCode ?? string.Empty;
        }

        public bool Restored { get; }

        public string RejectionCode { get; }
    }

    public interface IPreparedSaveComponentRestore : IDisposable
    {
        StableId ComponentStableId { get; }

        bool CommitAttempted { get; }

        bool CommitSucceeded { get; }

        SaveComponentCommitResult Commit();

        SaveComponentRollbackResult Rollback();
    }

    public sealed class SaveComponentPrepareResult
    {
        private SaveComponentPrepareResult(
            bool succeeded,
            string rejectionCode,
            IPreparedSaveComponentRestore preparedRestore)
        {
            Succeeded = succeeded;
            RejectionCode = rejectionCode ?? string.Empty;
            PreparedRestore = preparedRestore;
        }

        public bool Succeeded { get; }

        public string RejectionCode { get; }

        public IPreparedSaveComponentRestore PreparedRestore { get; }

        public static SaveComponentPrepareResult Prepared(
            IPreparedSaveComponentRestore preparedRestore)
        {
            return new SaveComponentPrepareResult(
                true,
                string.Empty,
                preparedRestore
                    ?? throw new ArgumentNullException(nameof(preparedRestore)));
        }

        public static SaveComponentPrepareResult Rejected(string rejectionCode)
        {
            return new SaveComponentPrepareResult(
                false,
                string.IsNullOrWhiteSpace(rejectionCode)
                    ? "save-component-prepare-rejected"
                    : rejectionCode.Trim(),
                null);
        }
    }

    public interface ISaveComponentBridge
    {
        SaveComponentDefinition Definition { get; }

        SaveComponentSnapshot ExportComponent();

        SaveComponentPrepareResult PrepareRestore(
            SaveComponentSnapshot component);
    }

    /// <summary>
    /// Typed wrapper over one existing authority snapshot. The supplied apply/import
    /// delegate should be internally atomic whenever the underlying authority supports
    /// that contract. Aggregate correctness does not rely on that assumption: a failed
    /// commit is immediately compensated with the captured prior immutable snapshot and
    /// restoration is confirmed by re-exporting and comparing explicit codec bytes.
    /// </summary>
    public sealed class StateSnapshotSaveComponentBridge<TSnapshot> :
        ISaveComponentBridge
        where TSnapshot : class
    {
        private readonly ISaveComponentPayloadCodec<TSnapshot> codec;
        private readonly Func<TSnapshot> exportSnapshot;
        private readonly Func<TSnapshot, SaveComponentValidationResult>
            validateSnapshot;
        private readonly Func<TSnapshot, SaveComponentApplyResult>
            applySnapshot;

        public StateSnapshotSaveComponentBridge(
            SaveComponentDefinition definition,
            ISaveComponentPayloadCodec<TSnapshot> codec,
            Func<TSnapshot> exportSnapshot,
            Func<TSnapshot, SaveComponentValidationResult> validateSnapshot,
            Func<TSnapshot, SaveComponentApplyResult> applySnapshot)
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

        public SaveComponentDefinition Definition { get; }

        public SaveComponentSnapshot ExportComponent()
        {
            TSnapshot snapshot = exportSnapshot();
            RequireValid(snapshot, "authority-export");
            string payload = codec.Encode(snapshot);
            RequirePayloadBound(payload);
            return new SaveComponentSnapshot(
                Definition.ComponentStableId,
                Definition.SchemaVersion,
                Definition.ContentVersion,
                payload);
        }

        public SaveComponentPrepareResult PrepareRestore(
            SaveComponentSnapshot component)
        {
            if (component == null)
            {
                return SaveComponentPrepareResult.Rejected(
                    "save-component-missing");
            }
            if (component.ComponentStableId != Definition.ComponentStableId)
            {
                return SaveComponentPrepareResult.Rejected(
                    "save-component-id-mismatch");
            }
            if (component.SchemaVersion != Definition.SchemaVersion)
            {
                return SaveComponentPrepareResult.Rejected(
                    "save-component-schema-unsupported");
            }
            if (!string.Equals(
                component.ContentVersion,
                Definition.ContentVersion,
                StringComparison.Ordinal))
            {
                return SaveComponentPrepareResult.Rejected(
                    "save-component-content-version-unsupported");
            }
            if (Encoding.UTF8.GetByteCount(component.CanonicalPayload)
                > SavePersistenceLimits.MaximumComponentPayloadBytes)
            {
                return SaveComponentPrepareResult.Rejected(
                    "component-payload-too-large");
            }

            TSnapshot decoded;
            string decodeError;
            if (!codec.TryDecode(
                component.CanonicalPayload,
                out decoded,
                out decodeError))
            {
                return SaveComponentPrepareResult.Rejected(decodeError);
            }

            SaveComponentValidationResult semantic =
                validateSnapshot(decoded);
            if (semantic == null || !semantic.Succeeded)
            {
                return SaveComponentPrepareResult.Rejected(
                    semantic == null
                        ? "save-component-semantic-validator-null"
                        : semantic.RejectionCode);
            }

            TSnapshot previous = exportSnapshot();
            try
            {
                RequireValid(previous, "authority-current");
                return SaveComponentPrepareResult.Prepared(
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
                return SaveComponentPrepareResult.Rejected(
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
            SaveComponentValidationResult codecValidation =
                codec.Validate(snapshot);
            if (codecValidation == null || !codecValidation.Succeeded)
            {
                throw new InvalidOperationException(
                    prefix + "-codec-validation-failed:"
                        + (codecValidation == null
                            ? "null"
                            : codecValidation.RejectionCode));
            }
            SaveComponentValidationResult semantic =
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
                    > SavePersistenceLimits.MaximumComponentPayloadBytes)
            {
                throw new InvalidOperationException(
                    "component-payload-too-large");
            }
        }

        private sealed class PreparedRestore :
            IPreparedSaveComponentRestore
        {
            private readonly ISaveComponentPayloadCodec<TSnapshot> codec;
            private readonly TSnapshot next;
            private readonly TSnapshot previous;
            private readonly string previousPayload;
            private readonly Func<TSnapshot> export;
            private readonly Func<TSnapshot, SaveComponentApplyResult> apply;
            private bool disposed;

            public PreparedRestore(
                StableId componentStableId,
                ISaveComponentPayloadCodec<TSnapshot> codec,
                TSnapshot next,
                TSnapshot previous,
                Func<TSnapshot> export,
                Func<TSnapshot, SaveComponentApplyResult> apply)
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

            public SaveComponentCommitResult Commit()
            {
                ThrowIfDisposed();
                if (CommitAttempted)
                {
                    return CommitSucceeded
                        ? new SaveComponentCommitResult(
                            SaveComponentCommitStatus.Applied,
                            string.Empty,
                            false)
                        : new SaveComponentCommitResult(
                            IsPreviousConfirmed()
                                ? SaveComponentCommitStatus
                                    .FailedAndCompensated
                                : SaveComponentCommitStatus
                                    .FailedCompensationIncomplete,
                            "save-component-commit-already-attempted",
                            IsPreviousConfirmed());
                }

                CommitAttempted = true;
                string failureCode = string.Empty;
                try
                {
                    SaveComponentApplyResult result = apply(next);
                    if (result != null && result.Succeeded)
                    {
                        CommitSucceeded = true;
                        return new SaveComponentCommitResult(
                            SaveComponentCommitStatus.Applied,
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

                SaveComponentRollbackResult compensation =
                    RestorePrevious("failing-component-compensation");
                return new SaveComponentCommitResult(
                    compensation.Restored
                        ? SaveComponentCommitStatus.FailedAndCompensated
                        : SaveComponentCommitStatus
                            .FailedCompensationIncomplete,
                    failureCode
                        + (string.IsNullOrEmpty(compensation.RejectionCode)
                            ? string.Empty
                            : ";compensation="
                                + compensation.RejectionCode),
                    compensation.Restored);
            }

            public SaveComponentRollbackResult Rollback()
            {
                ThrowIfDisposed();
                if (!CommitSucceeded)
                {
                    bool alreadyRestored = IsPreviousConfirmed();
                    return new SaveComponentRollbackResult(
                        alreadyRestored,
                        alreadyRestored
                            ? string.Empty
                            : "rollback-requested-before-successful-commit");
                }

                SaveComponentRollbackResult result =
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

            private SaveComponentRollbackResult RestorePrevious(
                string phase)
            {
                string applyFailure = string.Empty;
                try
                {
                    SaveComponentApplyResult result = apply(previous);
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
                return new SaveComponentRollbackResult(
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
                    SaveComponentValidationResult validation =
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

    public static class KnownSaveComponentDefinitions
    {
        public static SaveComponentDefinition PlayerExperience(
            bool required = true)
        {
            return Definition(
                "player-experience",
                "player-experience-explicit-v1",
                required,
                100);
        }

        public static SaveComponentDefinition PlayerHoldings(
            bool required = true)
        {
            return Definition(
                "player-holdings",
                "player-holdings-explicit-v1",
                required,
                200);
        }

        public static SaveComponentDefinition MoneyWallet(
            bool required = true)
        {
            return Definition(
                "money-wallet",
                "money-wallet-explicit-v1",
                required,
                300);
        }

        public static SaveComponentDefinition ScrapWallet(
            bool required = true)
        {
            return Definition(
                "scrap-wallet",
                "scrap-wallet-explicit-v1",
                required,
                400);
        }

        public static SaveComponentDefinition RankedSkillAllocation(
            bool required = true)
        {
            return Definition(
                "ranked-skill-allocation",
                "ranked-skill-allocation-explicit-v2",
                required,
                500);
        }

        public static SaveComponentDefinition ExactInstanceLoadout(
            bool required = true)
        {
            return Definition(
                "exact-instance-loadout",
                "inventory-loadout-explicit-v1",
                required,
                600);
        }

        public static SaveComponentDefinition StrongboxState(
            bool required = false)
        {
            return Definition(
                "strongbox-state",
                "strongbox-opening-explicit-v1",
                required,
                700);
        }

        private static SaveComponentDefinition Definition(
            string value,
            string contentVersion,
            bool required,
            int restoreOrder)
        {
            return new SaveComponentDefinition(
                StableId.Create("save-component", value),
                1,
                contentVersion,
                required,
                restoreOrder);
        }
    }

    public static class KnownSaveComponentAdapters
    {
        public static StateSnapshotSaveComponentBridge<
            PlayerExperienceSnapshot> PlayerExperience(
            Func<PlayerExperienceSnapshot> exportSnapshot,
            Func<PlayerExperienceSnapshot, SaveComponentValidationResult>
                validateSnapshot,
            Func<PlayerExperienceSnapshot, SaveComponentApplyResult>
                applySnapshot,
            bool required = true)
        {
            return Adapter(
                KnownSaveComponentDefinitions.PlayerExperience(required),
                KnownSaveComponentCodecs.PlayerExperience,
                exportSnapshot,
                validateSnapshot,
                applySnapshot);
        }

        public static StateSnapshotSaveComponentBridge<
            PlayerHoldingsSnapshot> PlayerHoldings(
            Func<PlayerHoldingsSnapshot> exportSnapshot,
            Func<PlayerHoldingsSnapshot, SaveComponentValidationResult>
                validateSnapshot,
            Func<PlayerHoldingsSnapshot, SaveComponentApplyResult>
                applySnapshot,
            bool required = true)
        {
            return Adapter(
                KnownSaveComponentDefinitions.PlayerHoldings(required),
                KnownSaveComponentCodecs.PlayerHoldings,
                exportSnapshot,
                validateSnapshot,
                applySnapshot);
        }

        public static StateSnapshotSaveComponentBridge<MoneyWalletSnapshot>
            MoneyWallet(
                Func<MoneyWalletSnapshot> exportSnapshot,
                Func<MoneyWalletSnapshot, SaveComponentValidationResult>
                    validateSnapshot,
                Func<MoneyWalletSnapshot, SaveComponentApplyResult>
                    applySnapshot,
                bool required = true)
        {
            return Adapter(
                KnownSaveComponentDefinitions.MoneyWallet(required),
                KnownSaveComponentCodecs.MoneyWallet,
                exportSnapshot,
                validateSnapshot,
                applySnapshot);
        }

        public static StateSnapshotSaveComponentBridge<ScrapSnapshot>
            ScrapWallet(
                Func<ScrapSnapshot> exportSnapshot,
                Func<ScrapSnapshot, SaveComponentValidationResult>
                    validateSnapshot,
                Func<ScrapSnapshot, SaveComponentApplyResult>
                    applySnapshot,
                bool required = true)
        {
            return Adapter(
                KnownSaveComponentDefinitions.ScrapWallet(required),
                KnownSaveComponentCodecs.ScrapWallet,
                exportSnapshot,
                validateSnapshot,
                applySnapshot);
        }

        public static StateSnapshotSaveComponentBridge<
            RankedSkillAllocationSnapshot> RankedSkillAllocation(
            Func<RankedSkillAllocationSnapshot> exportSnapshot,
            Func<RankedSkillAllocationSnapshot,
                SaveComponentValidationResult> validateSnapshot,
            Func<RankedSkillAllocationSnapshot, SaveComponentApplyResult>
                applySnapshot,
            bool required = true)
        {
            return Adapter(
                KnownSaveComponentDefinitions.RankedSkillAllocation(required),
                KnownSaveComponentCodecs.RankedSkillAllocation,
                exportSnapshot,
                validateSnapshot,
                applySnapshot);
        }

        public static StateSnapshotSaveComponentBridge<
            InventoryLoadoutStateSnapshot> ExactInstanceLoadout(
            Func<InventoryLoadoutStateSnapshot> exportSnapshot,
            Func<InventoryLoadoutStateSnapshot,
                SaveComponentValidationResult> validateSnapshot,
            Func<InventoryLoadoutStateSnapshot,
                SaveComponentApplyResult> applySnapshot,
            bool required = true)
        {
            return Adapter(
                KnownSaveComponentDefinitions.ExactInstanceLoadout(required),
                KnownSaveComponentCodecs.ExactInstanceLoadout,
                exportSnapshot,
                validateSnapshot,
                applySnapshot);
        }

        public static StateSnapshotSaveComponentBridge<
            StrongboxOpeningSnapshot> StrongboxState(
            Func<StrongboxOpeningSnapshot> exportSnapshot,
            Func<StrongboxOpeningSnapshot,
                SaveComponentValidationResult> validateSnapshot,
            Func<StrongboxOpeningSnapshot, SaveComponentApplyResult>
                applySnapshot,
            bool required = false)
        {
            return Adapter(
                KnownSaveComponentDefinitions.StrongboxState(required),
                KnownSaveComponentCodecs.StrongboxState,
                exportSnapshot,
                validateSnapshot,
                applySnapshot);
        }

        private static StateSnapshotSaveComponentBridge<TSnapshot>
            Adapter<TSnapshot>(
                SaveComponentDefinition definition,
                ISaveComponentPayloadCodec<TSnapshot> codec,
                Func<TSnapshot> exportSnapshot,
                Func<TSnapshot, SaveComponentValidationResult>
                    validateSnapshot,
                Func<TSnapshot, SaveComponentApplyResult> applySnapshot)
            where TSnapshot : class
        {
            return new StateSnapshotSaveComponentBridge<TSnapshot>(
                definition,
                codec,
                exportSnapshot,
                validateSnapshot,
                applySnapshot);
        }
    }
}
