using System;
using System.Collections.Generic;
using ShooterMover.Domain.Common;

namespace ShooterMover.Application.Rewards.CollectedRunTransfers
{
    /// <summary>
    /// Exactly-once coordinator for one honest atomic RAP/BOX plan. Durable Prepared
    /// custody is confirmed before mutation. Post-replacement uncertainty is fatal and
    /// never disguised as a compensated retry.
    /// </summary>
    public sealed class RewardClaimTransferFlow
    {
        public const string ApplicationPlanAuthorityKey =
            "collected-run-application-plan-v2";

        private readonly IRewardClaimAtomicBatchStatePort authority;
        private readonly IRewardClaimTransferPersistencePort persistence;

        public RewardClaimTransferFlow(
            IRewardClaimAtomicBatchStatePort authority,
            IRewardClaimTransferPersistencePort persistence)
        {
            this.authority = authority
                ?? throw new ArgumentNullException(nameof(authority));
            this.persistence = persistence
                ?? throw new ArgumentNullException(nameof(persistence));
        }

        public RewardClaimTransferResult Apply(
            RewardClaimAtomicPlan plan)
        {
            if (plan == null) throw new ArgumentNullException(nameof(plan));
            RewardClaimPreparedTransfer prepared =
                plan.PreparedTransfer;
            PermanentRewardTransferState before = TryExportState();

            RewardClaimTransferReceipt existing;
            try
            {
                if (authority.TryGetDurableReceipt(
                    plan.TransferOperationStableId,
                    out existing))
                {
                    return ReplayOrConflict(plan, existing, before);
                }
            }
            catch (Exception exception)
            {
                return Reject(
                    plan,
                    "collected-run-transfer-receipt-lookup-threw:"
                    + exception.GetType().Name,
                    false,
                    before);
            }

            RewardClaimTransferResult overlap =
                ValidateNoDurableOverlap(plan, before);
            if (overlap != null) return overlap;
            if (!persistence.IsAvailable)
            {
                return Reject(
                    plan,
                    "collected-run-transfer-persistence-unavailable",
                    false,
                    before);
            }

            RewardClaimTransferPersistenceResult custody;
            try
            {
                custody = persistence.PersistPreparedCustody(prepared);
            }
            catch (Exception exception)
            {
                RewardClaimTransferPersistenceResult uncertain =
                    UncertainPersistence(
                        "collected-run-transfer-custody-save-threw:"
                        + exception.GetType().Name);
                return Fatal(
                    plan,
                    uncertain.Diagnostic,
                    "live-compensation-intentionally-not-attempted",
                    uncertain,
                    before,
                    null);
            }
            if (custody == null)
            {
                RewardClaimTransferPersistenceResult uncertain =
                    UncertainPersistence(
                        "collected-run-transfer-custody-save-result-null");
                return Fatal(
                    plan,
                    uncertain.Diagnostic,
                    "live-compensation-intentionally-not-attempted",
                    uncertain,
                    before,
                    null);
            }
            if (custody.DurableStateUncertain)
            {
                return Fatal(
                    plan,
                    "collected-run-transfer-custody-durable-state-uncertain:"
                    + custody.Diagnostic,
                    "live-compensation-intentionally-not-attempted",
                    custody,
                    before,
                    null);
            }
            if (!custody.Succeeded)
            {
                return Reject(
                    plan,
                    "collected-run-transfer-custody-save-rejected-before-replacement:"
                    + custody.Diagnostic,
                    true,
                    before,
                    custody);
            }

            RewardClaimTransferPreflightResult preflight;
            try
            {
                preflight = authority.Preflight(plan);
            }
            catch (Exception exception)
            {
                return Reject(
                    plan,
                    "collected-run-transfer-preflight-threw:"
                    + exception.GetType().Name,
                    true,
                    before,
                    custody);
            }
            if (preflight == null || !preflight.Succeeded)
            {
                return Reject(
                    plan,
                    preflight == null
                        ? "collected-run-transfer-preflight-result-null"
                        : preflight.Diagnostic,
                    true,
                    before,
                    custody);
            }

            IRewardClaimTransferCompensation compensation;
            try
            {
                compensation = authority.CaptureCompensation();
            }
            catch (Exception exception)
            {
                return Reject(
                    plan,
                    "collected-run-transfer-compensation-capture-threw:"
                    + exception.GetType().Name,
                    true,
                    before,
                    custody);
            }
            if (compensation == null
                || string.IsNullOrWhiteSpace(compensation.Fingerprint))
            {
                return Reject(
                    plan,
                    "collected-run-transfer-compensation-invalid",
                    true,
                    before,
                    custody);
            }

            try
            {
                RewardClaimAtomicApplyResult applied =
                    authority.ApplyAtomicBatch(plan);
                if (applied == null || !applied.Succeeded)
                {
                    return RejectAfterCompensation(
                        plan,
                        compensation,
                        applied == null
                            ? "collected-run-transfer-atomic-apply-result-null"
                            : "collected-run-transfer-atomic-apply-rejected:"
                                + applied.Diagnostic,
                        custody);
                }

                var authorityFingerprints =
                    new Dictionary<string, string>(StringComparer.Ordinal);
                foreach (KeyValuePair<string, string> pair in
                    applied.AuthorityFingerprints)
                {
                    authorityFingerprints.Add(pair.Key, pair.Value);
                }
                authorityFingerprints[ApplicationPlanAuthorityKey] =
                    plan.Fingerprint;
                var receipt = new RewardClaimTransferReceipt(
                    plan.TransferOperationStableId,
                    plan.BatchFingerprint,
                    plan.RunStableId,
                    prepared.LifecycleGeneration,
                    prepared.AcceptedMissionResultStableId,
                    prepared.AcceptedMissionResultFingerprint,
                    plan.SelectedCharacterStableId,
                    applied.AppliedRewardStableIds,
                    authorityFingerprints);

                RewardClaimTransferReceiptRecordResult recorded =
                    authority.RecordReceipt(receipt);
                if (recorded == null
                    || !recorded.Succeeded
                    || recorded.Receipt == null
                    || !string.Equals(
                        recorded.Receipt.Fingerprint,
                        receipt.Fingerprint,
                        StringComparison.Ordinal))
                {
                    return RejectAfterCompensation(
                        plan,
                        compensation,
                        recorded == null
                            ? "collected-run-transfer-receipt-result-null"
                            : "collected-run-transfer-receipt-rejected:"
                                + recorded.Diagnostic,
                        custody);
                }

                RewardClaimTransferPersistenceResult persisted;
                try
                {
                    persisted = persistence.PersistAppliedAndVerify(
                        prepared.MarkPersisted(receipt.Fingerprint),
                        receipt);
                }
                catch (Exception exception)
                {
                    RewardClaimTransferPersistenceResult uncertain =
                        UncertainPersistence(
                            "collected-run-transfer-final-save-threw:"
                            + exception.GetType().Name);
                    return Fatal(
                        plan,
                        uncertain.Diagnostic,
                        "live-compensation-intentionally-not-attempted",
                        uncertain,
                        TryExportState(),
                        receipt);
                }
                if (persisted == null)
                {
                    RewardClaimTransferPersistenceResult uncertain =
                        UncertainPersistence(
                            "collected-run-transfer-final-save-result-null");
                    return Fatal(
                        plan,
                        uncertain.Diagnostic,
                        "live-compensation-intentionally-not-attempted",
                        uncertain,
                        TryExportState(),
                        receipt);
                }
                if (persisted.DurableStateUncertain)
                {
                    return Fatal(
                        plan,
                        "collected-run-transfer-final-save-durable-state-uncertain:"
                        + persisted.Diagnostic,
                        "live-compensation-intentionally-not-attempted",
                        persisted,
                        TryExportState(),
                        receipt);
                }
                if (!persisted.Succeeded)
                {
                    return RejectAfterCompensation(
                        plan,
                        compensation,
                        "collected-run-transfer-final-save-rejected-before-replacement:"
                        + persisted.Diagnostic,
                        persisted);
                }

                return new RewardClaimTransferResult(
                    RewardClaimTransferStatus.Applied,
                    plan.TransferOperationStableId,
                    plan.BatchFingerprint,
                    plan.RunStableId,
                    plan.SelectedCharacterStableId,
                    receipt,
                    TryExportState(),
                    persisted,
                    string.Empty,
                    string.Empty,
                    false);
            }
            catch (Exception exception)
            {
                return RejectAfterCompensation(
                    plan,
                    compensation,
                    "collected-run-transfer-atomic-apply-threw:"
                    + exception.GetType().Name,
                    custody);
            }
        }

        private RewardClaimTransferResult ReplayOrConflict(
            RewardClaimAtomicPlan plan,
            RewardClaimTransferReceipt receipt,
            PermanentRewardTransferState state)
        {
            string recordedPlan;
            bool matches = receipt != null
                && string.Equals(
                    receipt.BatchFingerprint,
                    plan.BatchFingerprint,
                    StringComparison.Ordinal)
                && receipt.RunStableId == plan.RunStableId
                && receipt.SelectedCharacterStableId
                    == plan.SelectedCharacterStableId
                && receipt.AuthorityFingerprints.TryGetValue(
                    ApplicationPlanAuthorityKey,
                    out recordedPlan)
                && string.Equals(
                    recordedPlan,
                    plan.Fingerprint,
                    StringComparison.Ordinal);
            if (!matches)
            {
                return Conflict(
                    plan,
                    "collected-run-transfer-durable-operation-conflict",
                    state,
                    receipt);
            }
            return new RewardClaimTransferResult(
                RewardClaimTransferStatus.ExactReplay,
                plan.TransferOperationStableId,
                plan.BatchFingerprint,
                plan.RunStableId,
                plan.SelectedCharacterStableId,
                receipt,
                state,
                new RewardClaimTransferPersistenceResult(
                    RewardClaimTransferPersistenceStatus
                        .AlreadyPersisted,
                    state == null ? 0L : state.AccountRevision,
                    state == null
                        ? string.Empty
                        : state.AccountFingerprint,
                    state == null ? 0L : state.CharacterRevision,
                    state == null
                        ? string.Empty
                        : state.CharacterFingerprint,
                    string.Empty),
                string.Empty,
                string.Empty,
                false);
        }

        private RewardClaimTransferResult
            ValidateNoDurableOverlap(
                RewardClaimAtomicPlan plan,
                PermanentRewardTransferState before)
        {
            for (int index = 0; index < plan.Rewards.Count; index++)
            {
                RewardClaimTransferReceipt existing;
                try
                {
                    if (!authority.TryGetDurableReceiptForReward(
                        plan.Rewards[index].RewardInstanceStableId,
                        out existing))
                    {
                        continue;
                    }
                }
                catch (Exception exception)
                {
                    return Reject(
                        plan,
                        "collected-run-transfer-overlap-lookup-threw:"
                        + exception.GetType().Name,
                        false,
                        before);
                }
                return Conflict(
                    plan,
                    "collected-run-transfer-partial-or-cross-operation-overlap:"
                    + plan.Rewards[index].RewardInstanceStableId,
                    before,
                    existing);
            }
            return null;
        }

        private RewardClaimTransferResult RejectAfterCompensation(
            RewardClaimAtomicPlan plan,
            IRewardClaimTransferCompensation compensation,
            string diagnostic,
            RewardClaimTransferPersistenceResult persistenceResult)
        {
            RewardClaimTransferRestoreResult restored;
            try
            {
                restored = authority.Restore(compensation);
            }
            catch (Exception exception)
            {
                return Fatal(
                    plan,
                    diagnostic,
                    "collected-run-transfer-restore-threw:"
                    + exception.GetType().Name,
                    persistenceResult,
                    TryExportState(),
                    null);
            }
            if (restored == null || !restored.Restored)
            {
                return Fatal(
                    plan,
                    diagnostic,
                    restored == null
                        ? "collected-run-transfer-restore-result-null"
                        : restored.Diagnostic,
                    persistenceResult,
                    TryExportState(),
                    null);
            }
            return Reject(
                plan,
                diagnostic,
                true,
                TryExportState(),
                persistenceResult,
                restored.Diagnostic);
        }

        private static RewardClaimTransferResult Reject(
            RewardClaimAtomicPlan plan,
            string diagnostic,
            bool retryAllowed,
            PermanentRewardTransferState state,
            RewardClaimTransferPersistenceResult persistenceResult = null,
            string compensationDiagnostic = "")
        {
            return new RewardClaimTransferResult(
                RewardClaimTransferStatus.Rejected,
                plan.TransferOperationStableId,
                plan.BatchFingerprint,
                plan.RunStableId,
                plan.SelectedCharacterStableId,
                null,
                state,
                persistenceResult
                    ?? RewardClaimTransferPersistenceResult
                        .NotAttempted(string.Empty),
                string.IsNullOrWhiteSpace(diagnostic)
                    ? "collected-run-transfer-rejected"
                    : diagnostic,
                compensationDiagnostic,
                retryAllowed);
        }

        private static RewardClaimTransferResult Conflict(
            RewardClaimAtomicPlan plan,
            string diagnostic,
            PermanentRewardTransferState state,
            RewardClaimTransferReceipt receipt)
        {
            return new RewardClaimTransferResult(
                RewardClaimTransferStatus.ConflictingDuplicate,
                plan.TransferOperationStableId,
                plan.BatchFingerprint,
                plan.RunStableId,
                plan.SelectedCharacterStableId,
                receipt,
                state,
                RewardClaimTransferPersistenceResult
                    .NotAttempted(string.Empty),
                diagnostic,
                string.Empty,
                false);
        }

        private static RewardClaimTransferResult Fatal(
            RewardClaimAtomicPlan plan,
            string diagnostic,
            string compensationDiagnostic,
            RewardClaimTransferPersistenceResult persistenceResult,
            PermanentRewardTransferState state,
            RewardClaimTransferReceipt receipt)
        {
            return new RewardClaimTransferResult(
                RewardClaimTransferStatus
                    .FatalCompensationFailure,
                plan.TransferOperationStableId,
                plan.BatchFingerprint,
                plan.RunStableId,
                plan.SelectedCharacterStableId,
                receipt,
                state,
                persistenceResult
                    ?? RewardClaimTransferPersistenceResult
                        .NotAttempted(string.Empty),
                diagnostic,
                compensationDiagnostic,
                false);
        }

        private static RewardClaimTransferPersistenceResult
            UncertainPersistence(string diagnostic)
        {
            return new RewardClaimTransferPersistenceResult(
                RewardClaimTransferPersistenceStatus
                    .DurableStateUncertain,
                0L,
                string.Empty,
                0L,
                string.Empty,
                string.IsNullOrWhiteSpace(diagnostic)
                    ? "collected-run-transfer-durable-state-uncertain"
                    : diagnostic);
        }

        private PermanentRewardTransferState TryExportState()
        {
            try { return authority.ExportState(); }
            catch { return null; }
        }
    }
}
