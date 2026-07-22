using System;
using System.Collections.Generic;
using ShooterMover.Domain.Common;

namespace ShooterMover.Application.Rewards.CollectedRunTransfers
{
    /// <summary>
    /// Exactly-once coordinator for one honest atomic RAP/BOX plan. Durable prepared
    /// custody is confirmed before mutation. A post-replacement persistence uncertainty
    /// is fatal and is never disguised as a compensated retry.
    /// </summary>
    public sealed class CollectedRunRewardTransferCoordinatorV2
    {
        public const string ApplicationPlanAuthorityKey =
            "collected-run-application-plan-v2";

        private readonly ICollectedRunRewardAtomicBatchAuthorityPortV1 authority;
        private readonly ICollectedRunRewardTransferPersistencePortV1 persistence;

        public CollectedRunRewardTransferCoordinatorV2(
            ICollectedRunRewardAtomicBatchAuthorityPortV1 authority,
            ICollectedRunRewardTransferPersistencePortV1 persistence)
        {
            this.authority = authority ?? throw new ArgumentNullException(nameof(authority));
            this.persistence = persistence ?? throw new ArgumentNullException(nameof(persistence));
        }

        public CollectedRunRewardTransferResultV1 Apply(
            CollectedRunRewardAtomicPlanV2 plan)
        {
            if (plan == null) throw new ArgumentNullException(nameof(plan));
            CollectedRunRewardPreparedTransferV1 prepared = plan.PreparedTransfer;
            PermanentRewardTransferStateV1 before = TryExportState();

            CollectedRunRewardTransferReceiptV1 existing;
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

            CollectedRunRewardTransferResultV1 overlap =
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

            CollectedRunRewardTransferPersistenceResultV1 custody;
            try
            {
                custody = persistence.PersistPreparedCustody(prepared);
            }
            catch (Exception exception)
            {
                return Reject(
                    plan,
                    "collected-run-transfer-custody-save-threw:"
                    + exception.GetType().Name,
                    false,
                    before);
            }
            if (custody == null)
            {
                return Reject(
                    plan,
                    "collected-run-transfer-custody-save-result-null",
                    false,
                    before);
            }
            if (custody.DurableStateUncertain)
            {
                return Fatal(
                    plan,
                    "collected-run-transfer-custody-durable-state-uncertain:"
                    + custody.Diagnostic,
                    string.Empty,
                    custody,
                    before);
            }
            if (!custody.Succeeded)
            {
                return Reject(
                    plan,
                    "collected-run-transfer-custody-save-rejected:"
                    + custody.Diagnostic,
                    true,
                    before,
                    custody);
            }

            CollectedRunRewardTransferPreflightResultV1 preflight;
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

            ICollectedRunRewardTransferCompensationV1 compensation;
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
                CollectedRunRewardAtomicApplyResultV1 applied =
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

                var authorityFingerprints = new Dictionary<string, string>(
                    applied.AuthorityFingerprints,
                    StringComparer.Ordinal);
                authorityFingerprints[ApplicationPlanAuthorityKey] =
                    plan.Fingerprint;
                var receipt = new CollectedRunRewardTransferReceiptV1(
                    plan.TransferOperationStableId,
                    plan.BatchFingerprint,
                    plan.RunStableId,
                    prepared.LifecycleGeneration,
                    prepared.AcceptedMissionResultStableId,
                    prepared.AcceptedMissionResultFingerprint,
                    plan.SelectedCharacterStableId,
                    applied.AppliedRewardStableIds,
                    authorityFingerprints);

                CollectedRunRewardTransferReceiptRecordResultV1 recorded =
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

                CollectedRunRewardPreparedTransferV1 persistedPrepared =
                    prepared.MarkPersisted(receipt.Fingerprint);
                CollectedRunRewardTransferPersistenceResultV1 persisted =
                    persistence.PersistAppliedAndVerify(
                        persistedPrepared,
                        receipt);
                if (persisted == null)
                {
                    return RejectAfterCompensation(
                        plan,
                        compensation,
                        "collected-run-transfer-final-save-result-null",
                        custody);
                }
                if (persisted.DurableStateUncertain)
                {
                    return Fatal(
                        plan,
                        "collected-run-transfer-final-save-durable-state-uncertain:"
                            + persisted.Diagnostic,
                        "live-compensation-intentionally-not-attempted",
                        persisted,
                        TryExportState());
                }
                if (!persisted.Succeeded)
                {
                    return RejectAfterCompensation(
                        plan,
                        compensation,
                        "collected-run-transfer-final-save-rejected:"
                            + persisted.Diagnostic,
                        persisted);
                }

                return new CollectedRunRewardTransferResultV1(
                    CollectedRunRewardTransferStatusV1.Applied,
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

        private CollectedRunRewardTransferResultV1 ReplayOrConflict(
            CollectedRunRewardAtomicPlanV2 plan,
            CollectedRunRewardTransferReceiptV1 receipt,
            PermanentRewardTransferStateV1 state)
        {
            string recordedPlan;
            bool matches = receipt != null
                && string.Equals(receipt.BatchFingerprint, plan.BatchFingerprint, StringComparison.Ordinal)
                && receipt.RunStableId == plan.RunStableId
                && receipt.SelectedCharacterStableId == plan.SelectedCharacterStableId
                && receipt.AuthorityFingerprints.TryGetValue(
                    ApplicationPlanAuthorityKey,
                    out recordedPlan)
                && string.Equals(recordedPlan, plan.Fingerprint, StringComparison.Ordinal);
            if (!matches)
            {
                return Conflict(
                    plan,
                    "collected-run-transfer-durable-operation-conflict",
                    state,
                    receipt);
            }
            return new CollectedRunRewardTransferResultV1(
                CollectedRunRewardTransferStatusV1.ExactReplay,
                plan.TransferOperationStableId,
                plan.BatchFingerprint,
                plan.RunStableId,
                plan.SelectedCharacterStableId,
                receipt,
                state,
                new CollectedRunRewardTransferPersistenceResultV1(
                    CollectedRunRewardTransferPersistenceStatusV1.AlreadyPersisted,
                    state == null ? 0L : state.AccountRevision,
                    state == null ? string.Empty : state.AccountFingerprint,
                    state == null ? 0L : state.CharacterRevision,
                    state == null ? string.Empty : state.CharacterFingerprint,
                    string.Empty),
                string.Empty,
                string.Empty,
                false);
        }

        private CollectedRunRewardTransferResultV1 ValidateNoDurableOverlap(
            CollectedRunRewardAtomicPlanV2 plan,
            PermanentRewardTransferStateV1 before)
        {
            for (int index = 0; index < plan.Rewards.Count; index++)
            {
                CollectedRunRewardTransferReceiptV1 existing;
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

        private CollectedRunRewardTransferResultV1 RejectAfterCompensation(
            CollectedRunRewardAtomicPlanV2 plan,
            ICollectedRunRewardTransferCompensationV1 compensation,
            string diagnostic,
            CollectedRunRewardTransferPersistenceResultV1 persistenceResult)
        {
            CollectedRunRewardTransferRestoreResultV1 restored;
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
                    TryExportState());
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
                    TryExportState());
            }
            return Reject(
                plan,
                diagnostic,
                true,
                TryExportState(),
                persistenceResult,
                restored.Diagnostic);
        }

        private static CollectedRunRewardTransferResultV1 Reject(
            CollectedRunRewardAtomicPlanV2 plan,
            string diagnostic,
            bool retryAllowed,
            PermanentRewardTransferStateV1 state,
            CollectedRunRewardTransferPersistenceResultV1 persistenceResult = null,
            string compensationDiagnostic = "")
        {
            return new CollectedRunRewardTransferResultV1(
                CollectedRunRewardTransferStatusV1.Rejected,
                plan.TransferOperationStableId,
                plan.BatchFingerprint,
                plan.RunStableId,
                plan.SelectedCharacterStableId,
                null,
                state,
                persistenceResult
                    ?? CollectedRunRewardTransferPersistenceResultV1.NotAttempted(string.Empty),
                string.IsNullOrWhiteSpace(diagnostic)
                    ? "collected-run-transfer-rejected"
                    : diagnostic,
                compensationDiagnostic,
                retryAllowed);
        }

        private static CollectedRunRewardTransferResultV1 Conflict(
            CollectedRunRewardAtomicPlanV2 plan,
            string diagnostic,
            PermanentRewardTransferStateV1 state,
            CollectedRunRewardTransferReceiptV1 receipt)
        {
            return new CollectedRunRewardTransferResultV1(
                CollectedRunRewardTransferStatusV1.ConflictingDuplicate,
                plan.TransferOperationStableId,
                plan.BatchFingerprint,
                plan.RunStableId,
                plan.SelectedCharacterStableId,
                receipt,
                state,
                CollectedRunRewardTransferPersistenceResultV1.NotAttempted(string.Empty),
                diagnostic,
                string.Empty,
                false);
        }

        private static CollectedRunRewardTransferResultV1 Fatal(
            CollectedRunRewardAtomicPlanV2 plan,
            string diagnostic,
            string compensationDiagnostic,
            CollectedRunRewardTransferPersistenceResultV1 persistenceResult,
            PermanentRewardTransferStateV1 state)
        {
            return new CollectedRunRewardTransferResultV1(
                CollectedRunRewardTransferStatusV1.FatalCompensationFailure,
                plan.TransferOperationStableId,
                plan.BatchFingerprint,
                plan.RunStableId,
                plan.SelectedCharacterStableId,
                null,
                state,
                persistenceResult
                    ?? CollectedRunRewardTransferPersistenceResultV1.NotAttempted(string.Empty),
                diagnostic,
                compensationDiagnostic,
                false);
        }

        private PermanentRewardTransferStateV1 TryExportState()
        {
            try { return authority.ExportState(); }
            catch { return null; }
        }
    }
}
