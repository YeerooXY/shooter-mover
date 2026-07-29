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

namespace ShooterMover.Application.Persistence.SaveParts
{
    public enum SavePartValidationStatus
    {
        Accepted = 1,
        Rejected = 2,
    }

    public sealed class SavePartValidationResult
    {
        private SavePartValidationResult(
            SavePartValidationStatus status,
            string rejectionCode)
        {
            Status = status;
            RejectionCode = rejectionCode ?? string.Empty;
        }

        public SavePartValidationStatus Status { get; }

        public string RejectionCode { get; }

        public bool Succeeded
        {
            get { return Status == SavePartValidationStatus.Accepted; }
        }

        public static SavePartValidationResult Accept()
        {
            return new SavePartValidationResult(
                SavePartValidationStatus.Accepted,
                string.Empty);
        }

        public static SavePartValidationResult Reject(string rejectionCode)
        {
            return new SavePartValidationResult(
                SavePartValidationStatus.Rejected,
                string.IsNullOrWhiteSpace(rejectionCode)
                    ? "save-part-validation-rejected"
                    : rejectionCode.Trim());
        }
    }

    public sealed class SavePartApplyResult
    {
        private SavePartApplyResult(bool succeeded, string rejectionCode)
        {
            Succeeded = succeeded;
            RejectionCode = rejectionCode ?? string.Empty;
        }

        public bool Succeeded { get; }

        public string RejectionCode { get; }

        public static SavePartApplyResult Applied()
        {
            return new SavePartApplyResult(true, string.Empty);
        }

        public static SavePartApplyResult Rejected(string rejectionCode)
        {
            return new SavePartApplyResult(
                false,
                string.IsNullOrWhiteSpace(rejectionCode)
                    ? "save-part-apply-rejected"
                    : rejectionCode.Trim());
        }
    }

    public sealed class SavePartDefinition
    {
        public SavePartDefinition(
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

    public interface ISavePartFormat<TSnapshot>
        where TSnapshot : class
    {
        string ContractId { get; }

        string Encode(TSnapshot snapshot);

        bool TryDecode(
            string canonicalPayload,
            out TSnapshot snapshot,
            out string rejectionCode);

        SavePartValidationResult Validate(TSnapshot snapshot);
    }

    public enum SavePartCommitStatus
    {
        Applied = 1,
        FailedAndCompensated = 2,
        FailedCompensationIncomplete = 3,
    }

    public sealed class SavePartCommitResult
    {
        public SavePartCommitResult(
            SavePartCommitStatus status,
            string rejectionCode,
            bool previousSnapshotConfirmed)
        {
            Status = status;
            RejectionCode = rejectionCode ?? string.Empty;
            PreviousSnapshotConfirmed = previousSnapshotConfirmed;
        }

        public SavePartCommitStatus Status { get; }

        public string RejectionCode { get; }

        public bool PreviousSnapshotConfirmed { get; }

        public bool Succeeded
        {
            get { return Status == SavePartCommitStatus.Applied; }
        }
    }

    public sealed class SavePartRollbackResult
    {
        public SavePartRollbackResult(
            bool restored,
            string rejectionCode)
        {
            Restored = restored;
            RejectionCode = rejectionCode ?? string.Empty;
        }

        public bool Restored { get; }

        public string RejectionCode { get; }
    }

    public interface IPreparedSavePartRestore : IDisposable
    {
        StableId ComponentStableId { get; }

        bool CommitAttempted { get; }

        bool CommitSucceeded { get; }

        SavePartCommitResult Commit();

        SavePartRollbackResult Rollback();
    }

    public sealed class SavePartPrepareResult
    {
        private SavePartPrepareResult(
            bool succeeded,
            string rejectionCode,
            IPreparedSavePartRestore preparedRestore)
        {
            Succeeded = succeeded;
            RejectionCode = rejectionCode ?? string.Empty;
            PreparedRestore = preparedRestore;
        }

        public bool Succeeded { get; }

        public string RejectionCode { get; }

        public IPreparedSavePartRestore PreparedRestore { get; }

        public static SavePartPrepareResult Prepared(
            IPreparedSavePartRestore preparedRestore)
        {
            return new SavePartPrepareResult(
                true,
                string.Empty,
                preparedRestore
                    ?? throw new ArgumentNullException(nameof(preparedRestore)));
        }

        public static SavePartPrepareResult Rejected(string rejectionCode)
        {
            return new SavePartPrepareResult(
                false,
                string.IsNullOrWhiteSpace(rejectionCode)
                    ? "save-part-prepare-rejected"
                    : rejectionCode.Trim(),
                null);
        }
    }

    public interface ISavePart
    {
        SavePartDefinition Definition { get; }

        SavePartSnapshot ExportComponent();

        SavePartPrepareResult PrepareRestore(
            SavePartSnapshot component);
    }

    /// <summary>
    /// Typed wrapper over one existing authority snapshot. The supplied apply/import
    /// delegate should be internally atomic whenever the underlying authority supports
    /// that contract. Aggregate correctness does not rely on that assumption: a failed
    /// commit is immediately compensated with the captured prior immutable snapshot and
    /// restoration is confirmed by re-exporting and comparing explicit codec bytes.
    /// </summary>
    public sealed class SnapshotSavePart<TSnapshot> :
        ISavePart
        where TSnapshot : class
    {
        private readonly ISavePartFormat<TSnapshot> codec;
        private readonly Func<TSnapshot> exportSnapshot;
        private readonly Func<TSnapshot, SavePartValidationResult>
            validateSnapshot;
        private readonly Func<TSnapshot, SavePartApplyResult>
            applySnapshot;

        public SnapshotSavePart(
            SavePartDefinition definition,
            ISavePartFormat<TSnapshot> codec,
            Func<TSnapshot> exportSnapshot,
            Func<TSnapshot, SavePartValidationResult> validateSnapshot,
            Func<TSnapshot, SavePartApplyResult> applySnapshot)
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

        public SavePartDefinition Definition { get; }

        public SavePartSnapshot ExportComponent()
        {
            TSnapshot snapshot = exportSnapshot();
            RequireValid(snapshot, "authority-export");
            string payload = codec.Encode(snapshot);
            RequirePayloadBound(payload);
            return new SavePartSnapshot(
                Definition.ComponentStableId,
                Definition.SchemaVersion,
                Definition.ContentVersion,
                payload);
        }

        public SavePartPrepareResult PrepareRestore(
            SavePartSnapshot component)
        {
            if (component == null)
            {
                return SavePartPrepareResult.Rejected(
                    "save-part-missing");
            }
            if (component.ComponentStableId != Definition.ComponentStableId)
            {
                return SavePartPrepareResult.Rejected(
                    "save-part-id-mismatch");
            }
            if (component.SchemaVersion != Definition.SchemaVersion)
            {
                return SavePartPrepareResult.Rejected(
                    "save-part-schema-unsupported");
            }
            if (!string.Equals(
                component.ContentVersion,
                Definition.ContentVersion,
                StringComparison.Ordinal))
            {
                return SavePartPrepareResult.Rejected(
                    "save-part-content-version-unsupported");
            }
            if (Encoding.UTF8.GetByteCount(component.CanonicalPayload)
                > SavePersistenceLimits.MaximumComponentPayloadBytes)
            {
                return SavePartPrepareResult.Rejected(
                    "component-payload-too-large");
            }

            TSnapshot decoded;
            string decodeError;
            if (!codec.TryDecode(
                component.CanonicalPayload,
                out decoded,
                out decodeError))
            {
                return SavePartPrepareResult.Rejected(decodeError);
            }

            SavePartValidationResult semantic =
                validateSnapshot(decoded);
            if (semantic == null || !semantic.Succeeded)
            {
                return SavePartPrepareResult.Rejected(
                    semantic == null
                        ? "save-part-semantic-validator-null"
                        : semantic.RejectionCode);
            }

            TSnapshot previous = exportSnapshot();
            try
            {
                RequireValid(previous, "authority-current");
                return SavePartPrepareResult.Prepared(
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
                return SavePartPrepareResult.Rejected(
                    "save-part-current-snapshot-invalid:"
                        + exception.GetType().Name);
            }
        }

        private void RequireValid(TSnapshot snapshot, string prefix)
        {
            if (snapshot == null)
            {
                throw new InvalidOperationException(prefix + "-snapshot-null");
            }
            SavePartValidationResult codecValidation =
                codec.Validate(snapshot);
            if (codecValidation == null || !codecValidation.Succeeded)
            {
                throw new InvalidOperationException(
                    prefix + "-codec-validation-failed:"
                        + (codecValidation == null
                            ? "null"
                            : codecValidation.RejectionCode));
            }
            SavePartValidationResult semantic =
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
            IPreparedSavePartRestore
        {
            private readonly ISavePartFormat<TSnapshot> codec;
            private readonly TSnapshot next;
            private readonly TSnapshot previous;
            private readonly string previousPayload;
            private readonly Func<TSnapshot> export;
            private readonly Func<TSnapshot, SavePartApplyResult> apply;
            private bool disposed;

            public PreparedRestore(
                StableId componentStableId,
                ISavePartFormat<TSnapshot> codec,
                TSnapshot next,
                TSnapshot previous,
                Func<TSnapshot> export,
                Func<TSnapshot, SavePartApplyResult> apply)
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

            public SavePartCommitResult Commit()
            {
                ThrowIfDisposed();
                if (CommitAttempted)
                {
                    return CommitSucceeded
                        ? new SavePartCommitResult(
                            SavePartCommitStatus.Applied,
                            string.Empty,
                            false)
                        : new SavePartCommitResult(
                            IsPreviousConfirmed()
                                ? SavePartCommitStatus
                                    .FailedAndCompensated
                                : SavePartCommitStatus
                                    .FailedCompensationIncomplete,
                            "save-part-commit-already-attempted",
                            IsPreviousConfirmed());
                }

                CommitAttempted = true;
                string failureCode = string.Empty;
                try
                {
                    SavePartApplyResult result = apply(next);
                    if (result != null && result.Succeeded)
                    {
                        CommitSucceeded = true;
                        return new SavePartCommitResult(
                            SavePartCommitStatus.Applied,
                            string.Empty,
                            false);
                    }
                    failureCode = result == null
                        ? "save-part-apply-result-null"
                        : result.RejectionCode;
                }
                catch (Exception exception)
                {
                    failureCode = "save-part-apply-threw:"
                        + exception.GetType().Name;
                }

                SavePartRollbackResult compensation =
                    RestorePrevious("failing-component-compensation");
                return new SavePartCommitResult(
                    compensation.Restored
                        ? SavePartCommitStatus.FailedAndCompensated
                        : SavePartCommitStatus
                            .FailedCompensationIncomplete,
                    failureCode
                        + (string.IsNullOrEmpty(compensation.RejectionCode)
                            ? string.Empty
                            : ";compensation="
                                + compensation.RejectionCode),
                    compensation.Restored);
            }

            public SavePartRollbackResult Rollback()
            {
                ThrowIfDisposed();
                if (!CommitSucceeded)
                {
                    bool alreadyRestored = IsPreviousConfirmed();
                    return new SavePartRollbackResult(
                        alreadyRestored,
                        alreadyRestored
                            ? string.Empty
                            : "rollback-requested-before-successful-commit");
                }

                SavePartRollbackResult result =
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

            private SavePartRollbackResult RestorePrevious(
                string phase)
            {
                string applyFailure = string.Empty;
                try
                {
                    SavePartApplyResult result = apply(previous);
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
                return new SavePartRollbackResult(
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
                    SavePartValidationResult validation =
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

    public static class GameSaveParts
    {
        public static SavePartDefinition PlayerExperience(
            bool required = true)
        {
            return Definition(
                "player-experience",
                "player-experience-explicit-v1",
                required,
                100);
        }

        public static SavePartDefinition PlayerHoldings(
            bool required = true)
        {
            return Definition(
                "player-holdings",
                "player-holdings-explicit-v1",
                required,
                200);
        }

        public static SavePartDefinition MoneyWallet(
            bool required = true)
        {
            return Definition(
                "money-wallet",
                "money-wallet-explicit-v1",
                required,
                300);
        }

        public static SavePartDefinition ScrapWallet(
            bool required = true)
        {
            return Definition(
                "scrap-wallet",
                "scrap-wallet-explicit-v1",
                required,
                400);
        }

        public static SavePartDefinition RankedSkillAllocation(
            bool required = true)
        {
            return Definition(
                "ranked-skill-allocation",
                "ranked-skill-allocation-explicit-v2",
                required,
                500);
        }

        public static SavePartDefinition ExactInstanceLoadout(
            bool required = true)
        {
            return Definition(
                "exact-instance-loadout",
                "inventory-loadout-explicit-v1",
                required,
                600);
        }

        public static SavePartDefinition StrongboxState(
            bool required = false)
        {
            return Definition(
                "strongbox-state",
                "strongbox-opening-explicit-v1",
                required,
                700);
        }

        private static SavePartDefinition Definition(
            string value,
            string contentVersion,
            bool required,
            int restoreOrder)
        {
            return new SavePartDefinition(
                StableId.Create("save-part", value),
                1,
                contentVersion,
                required,
                restoreOrder);
        }
    }

    public static class KnownSavePartAdapters
    {
        public static SnapshotSavePart<
            PlayerExperienceSnapshot> PlayerExperience(
            Func<PlayerExperienceSnapshot> exportSnapshot,
            Func<PlayerExperienceSnapshot, SavePartValidationResult>
                validateSnapshot,
            Func<PlayerExperienceSnapshot, SavePartApplyResult>
                applySnapshot,
            bool required = true)
        {
            return Adapter(
                GameSaveParts.PlayerExperience(required),
                GameSaveFormats.PlayerExperience,
                exportSnapshot,
                validateSnapshot,
                applySnapshot);
        }

        public static SnapshotSavePart<
            PlayerHoldingsSnapshot> PlayerHoldings(
            Func<PlayerHoldingsSnapshot> exportSnapshot,
            Func<PlayerHoldingsSnapshot, SavePartValidationResult>
                validateSnapshot,
            Func<PlayerHoldingsSnapshot, SavePartApplyResult>
                applySnapshot,
            bool required = true)
        {
            return Adapter(
                GameSaveParts.PlayerHoldings(required),
                GameSaveFormats.PlayerHoldings,
                exportSnapshot,
                validateSnapshot,
                applySnapshot);
        }

        public static SnapshotSavePart<MoneyWalletSnapshot>
            MoneyWallet(
                Func<MoneyWalletSnapshot> exportSnapshot,
                Func<MoneyWalletSnapshot, SavePartValidationResult>
                    validateSnapshot,
                Func<MoneyWalletSnapshot, SavePartApplyResult>
                    applySnapshot,
                bool required = true)
        {
            return Adapter(
                GameSaveParts.MoneyWallet(required),
                GameSaveFormats.MoneyWallet,
                exportSnapshot,
                validateSnapshot,
                applySnapshot);
        }

        public static SnapshotSavePart<ScrapSnapshot>
            ScrapWallet(
                Func<ScrapSnapshot> exportSnapshot,
                Func<ScrapSnapshot, SavePartValidationResult>
                    validateSnapshot,
                Func<ScrapSnapshot, SavePartApplyResult>
                    applySnapshot,
                bool required = true)
        {
            return Adapter(
                GameSaveParts.ScrapWallet(required),
                GameSaveFormats.ScrapWallet,
                exportSnapshot,
                validateSnapshot,
                applySnapshot);
        }

        public static SnapshotSavePart<
            RankedSkillAllocationSnapshot> RankedSkillAllocation(
            Func<RankedSkillAllocationSnapshot> exportSnapshot,
            Func<RankedSkillAllocationSnapshot,
                SavePartValidationResult> validateSnapshot,
            Func<RankedSkillAllocationSnapshot, SavePartApplyResult>
                applySnapshot,
            bool required = true)
        {
            return Adapter(
                GameSaveParts.RankedSkillAllocation(required),
                GameSaveFormats.RankedSkillAllocation,
                exportSnapshot,
                validateSnapshot,
                applySnapshot);
        }

        public static SnapshotSavePart<
            InventoryLoadoutStateSnapshot> ExactInstanceLoadout(
            Func<InventoryLoadoutStateSnapshot> exportSnapshot,
            Func<InventoryLoadoutStateSnapshot,
                SavePartValidationResult> validateSnapshot,
            Func<InventoryLoadoutStateSnapshot,
                SavePartApplyResult> applySnapshot,
            bool required = true)
        {
            return Adapter(
                GameSaveParts.ExactInstanceLoadout(required),
                GameSaveFormats.ExactInstanceLoadout,
                exportSnapshot,
                validateSnapshot,
                applySnapshot);
        }

        public static SnapshotSavePart<
            StrongboxOpeningSnapshot> StrongboxState(
            Func<StrongboxOpeningSnapshot> exportSnapshot,
            Func<StrongboxOpeningSnapshot,
                SavePartValidationResult> validateSnapshot,
            Func<StrongboxOpeningSnapshot, SavePartApplyResult>
                applySnapshot,
            bool required = false)
        {
            return Adapter(
                GameSaveParts.StrongboxState(required),
                GameSaveFormats.StrongboxState,
                exportSnapshot,
                validateSnapshot,
                applySnapshot);
        }

        private static SnapshotSavePart<TSnapshot>
            Adapter<TSnapshot>(
                SavePartDefinition definition,
                ISavePartFormat<TSnapshot> codec,
                Func<TSnapshot> exportSnapshot,
                Func<TSnapshot, SavePartValidationResult>
                    validateSnapshot,
                Func<TSnapshot, SavePartApplyResult> applySnapshot)
            where TSnapshot : class
        {
            return new SnapshotSavePart<TSnapshot>(
                definition,
                codec,
                exportSnapshot,
                validateSnapshot,
                applySnapshot);
        }
    }
}
