using System;
using System.Collections.Generic;
using ShooterMover.Domain.Common;

namespace ShooterMover.Application.Rewards.CollectedRunTransfers
{
    /// <summary>
    /// Exactly-once orchestration boundary from one immutable collected-reward batch to
    /// the existing selected-character authorities and account save. This coordinator
    /// owns replay decisions only; all permanent mutable truth remains behind its ports.
    /// </summary>
    public sealed class CollectedRunRewardTransferCoordinatorV1
    {
        private sealed class ReplayRecord
        {
            public ReplayRecord(
                string batchFingerprint,
                CollectedRunRewardTransferResultV1 result)
            {
                BatchFingerprint = batchFingerprint
                    ?? throw new ArgumentNullException(
                        nameof(batchFingerprint));
                Result = result
                    ?? throw new ArgumentNullException(nameof(result));
            }

            public string BatchFingerprint { get; }
            public CollectedRunRewardTransferResultV1 Result { get; }
        }

        private readonly ICollectedRunRewardTransferAuthorityPortV1
            authority;
        private readonly ICollectedRunRewardTransferPersistencePortV1
            persistence;
        private readonly Dictionary<StableId, ReplayRecord> replay =
            new Dictionary<StableId, ReplayRecord>();

        public CollectedRunRewardTransferCoordinatorV1(
            ICollectedRunRewardTransferAuthorityPortV1 authority,
            ICollectedRunRewardTransferPersistencePortV1 persistence)
        {
            this.authority = authority
                ?? throw new ArgumentNullException(nameof(authority));
            this.persistence = persistence
                ?? throw new ArgumentNullException(nameof(persistence));
        }

        public CollectedRunRewardTransferResultV1 Apply(
            CollectedRunRewardTransferBatchV1 batch)
        {
            if (batch == null)
            {
                return new CollectedRunRewardTransferResultV1(
                    CollectedRunRewardTransferStatusV1.Rejected,
                    null,
                    string.Empty,
                    null,
                    null,
                    null,
                    TryExportState(),
                    CollectedRunRewardTransferPersistenceResultV1
                        .NotAttempted(
                            "collected-run-transfer-batch-null"),
                    "collected-run-transfer-batch-null",
                    string.Empty,
                    false);
            }

            CollectedRunRewardTransferResultV1 durableReplay =
                TryResolveDurableReplay(batch);
            if (durableReplay != null)
            {
                replay[batch.TransferOperationStableId] =
                    new ReplayRecord(
                        batch.Fingerprint,
                        durableReplay);
                return durableReplay;
            }

            ReplayRecord prior;
            if (replay.TryGetValue(
                batch.TransferOperationStableId,
                out prior))
            {
                if (!string.Equals(
                    prior.BatchFingerprint,
                    batch.Fingerprint,
                    StringComparison.Ordinal))
                {
                    return Conflict(
                        batch,
                        "collected-run-transfer-operation-conflict");
                }
                if (prior.Result.Succeeded)
                {
                    return prior.Result.AsExactReplay(
                        TryExportState(),
                        prior.Result.Persistence);
                }
                if (!prior.Result.ExactRetryAllowed)
                    return prior.Result;
            }

            CollectedRunRewardTransferResultV1 result =
                ApplyFirst(batch);
            replay[batch.TransferOperationStableId] =
                new ReplayRecord(batch.Fingerprint, result);
            return result;
        }

        private CollectedRunRewardTransferResultV1
            TryResolveDurableReplay(
                CollectedRunRewardTransferBatchV1 batch)
        {
            CollectedRunRewardTransferReceiptV1 receipt;
            try
            {
                if (!authority.TryGetDurableReceipt(
                    batch.TransferOperationStableId,
                    out receipt))
                {
                    return null;
                }
            }
            catch (Exception exception)
            {
                return Reject(
                    batch,
                    "collected-run-transfer-receipt-lookup-threw:"
                        + exception.GetType().Name,
                    true);
            }

            if (!ReceiptMatchesBatch(receipt, batch))
            {
                return Conflict(
                    batch,
                    "collected-run-transfer-durable-receipt-conflict");
            }

            PermanentRewardTransferStateV1 current =
                TryExportState();
            return new CollectedRunRewardTransferResultV1(
                CollectedRunRewardTransferStatusV1.ExactReplay,
                batch.TransferOperationStableId,
                batch.Fingerprint,
                batch.RunStableId,
                batch.SelectedCharacterStableId,
                receipt,
                current,
                AlreadyPersisted(current),
                string.Empty,
                string.Empty,
                false);
        }

        private CollectedRunRewardTransferResultV1 ApplyFirst(
            CollectedRunRewardTransferBatchV1 batch)
        {
            PermanentRewardTransferStateV1 before;
            try
            {
                before = authority.ExportState();
            }
            catch (Exception exception)
            {
                return Reject(
                    batch,
                    "collected-run-transfer-state-export-threw:"
                        + exception.GetType().Name,
                    true);
            }

            string identityError = ValidateExpectedState(
                batch,
                before);
            if (!string.IsNullOrEmpty(identityError))
                return Reject(batch, identityError, false, before);

            if (!persistence.IsAvailable)
            {
                return Reject(
                    batch,
                    "collected-run-transfer-persistence-unavailable",
                    true,
                    before);
            }

            CollectedRunRewardTransferResultV1 overlap =
                ValidateNoDurableOverlap(batch, before);
            if (overlap != null)
                return overlap;

            CollectedRunRewardTransferPreflightResultV1
                preflight;
            try
            {
                preflight = authority.Preflight(batch);
            }
            catch (Exception exception)
            {
                return Reject(
                    batch,
                    "collected-run-transfer-preflight-threw:"
                        + exception.GetType().Name,
                    true,
                    before);
            }
            if (preflight == null || !preflight.Succeeded)
            {
                return Reject(
                    batch,
                    preflight == null
                        ? "collected-run-transfer-preflight-null"
                        : preflight.Diagnostic,
                    false,
                    before);
            }

            ICollectedRunRewardTransferCompensationV1
                compensation;
            try
            {
                compensation = authority.CaptureCompensation();
            }
            catch (Exception exception)
            {
                return Reject(
                    batch,
                    "collected-run-transfer-compensation-capture-threw:"
                        + exception.GetType().Name,
                    true,
                    before);
            }
            if (compensation == null
                || string.IsNullOrWhiteSpace(
                    compensation.Fingerprint))
            {
                return Reject(
                    batch,
                    "collected-run-transfer-compensation-invalid",
                    true,
                    before);
            }

            var appliedRewardIds = new List<StableId>();
            try
            {
                for (int index = 0;
                    index < batch.Rewards.Count;
                    index++)
                {
                    CollectedRunRewardTransferItemV1 reward =
                        batch.Rewards[index];
                    string target =
                        authority.ResolveAuthorityTarget(reward);
                    if (string.IsNullOrWhiteSpace(target))
                    {
                        return RejectAfterCompensation(
                            batch,
                            compensation,
                            "collected-run-transfer-authority-target-missing:"
                                + reward.RewardInstanceStableId,
                            CollectedRunRewardTransferPersistenceResultV1
                                .NotAttempted(string.Empty));
                    }

                    var child =
                        new CollectedRunRewardTransferChildCommandV1(
                            batch,
                            reward,
                            index,
                            target);
                    CollectedRunRewardTransferChildResultV1
                        childResult = authority.Apply(child);
                    if (childResult == null
                        || !childResult.Succeeded)
                    {
                        return RejectAfterCompensation(
                            batch,
                            compensation,
                            childResult == null
                                ? "collected-run-transfer-child-result-null:"
                                    + reward.RewardInstanceStableId
                                : "collected-run-transfer-child-rejected:"
                                    + reward.RewardInstanceStableId
                                    + ":"
                                    + childResult.Diagnostic,
                            CollectedRunRewardTransferPersistenceResultV1
                                .NotAttempted(string.Empty));
                    }
                    appliedRewardIds.Add(
                        reward.RewardInstanceStableId);
                }

                PermanentRewardTransferStateV1 afterRewards =
                    authority.ExportState();
                var authorityFingerprints =
                    new Dictionary<string, string>(
                        StringComparer.Ordinal);
                foreach (KeyValuePair<string, string> pair in
                    afterRewards.AuthorityFingerprints)
                {
                    authorityFingerprints.Add(
                        pair.Key,
                        pair.Value);
                }

                var receipt =
                    new CollectedRunRewardTransferReceiptV1(
                        batch.TransferOperationStableId,
                        batch.Fingerprint,
                        batch.RunStableId,
                        batch.AcceptedLifecycleGeneration,
                        batch.AcceptedMissionResultStableId,
                        batch.AcceptedMissionResult.Fingerprint,
                        batch.SelectedCharacterStableId,
                        appliedRewardIds,
                        authorityFingerprints);

                CollectedRunRewardTransferReceiptRecordResultV1
                    receiptResult =
                        authority.RecordReceipt(receipt);
                if (receiptResult == null
                    || !receiptResult.Succeeded
                    || receiptResult.Receipt == null
                    || !string.Equals(
                        receiptResult.Receipt.Fingerprint,
                        receipt.Fingerprint,
                        StringComparison.Ordinal))
                {
                    return RejectAfterCompensation(
                        batch,
                        compensation,
                        receiptResult == null
                            ? "collected-run-transfer-receipt-result-null"
                            : "collected-run-transfer-receipt-rejected:"
                                + receiptResult.Diagnostic,
                        CollectedRunRewardTransferPersistenceResultV1
                            .NotAttempted(string.Empty));
                }

                CollectedRunRewardTransferPersistenceResultV1
                    persisted = persistence.PersistAndVerify(
                        batch.DeriveSaveOperationStableId(),
                        receipt);
                if (persisted == null || !persisted.Succeeded)
                {
                    return RejectAfterCompensation(
                        batch,
                        compensation,
                        persisted == null
                            ? "collected-run-transfer-persistence-result-null"
                            : "collected-run-transfer-persistence-rejected:"
                                + persisted.Diagnostic,
                        persisted
                            ?? CollectedRunRewardTransferPersistenceResultV1
                                .NotAttempted(string.Empty));
                }

                PermanentRewardTransferStateV1 finalState =
                    authority.ExportState();
                return new CollectedRunRewardTransferResultV1(
                    CollectedRunRewardTransferStatusV1.Applied,
                    batch.TransferOperationStableId,
                    batch.Fingerprint,
                    batch.RunStableId,
                    batch.SelectedCharacterStableId,
                    receipt,
                    finalState,
                    persisted,
                    string.Empty,
                    string.Empty,
                    false);
            }
            catch (Exception exception)
            {
                return RejectAfterCompensation(
                    batch,
                    compensation,
                    "collected-run-transfer-apply-threw:"
                        + exception.GetType().Name,
                    CollectedRunRewardTransferPersistenceResultV1
                        .NotAttempted(string.Empty));
            }
        }

        private CollectedRunRewardTransferResultV1
            ValidateNoDurableOverlap(
                CollectedRunRewardTransferBatchV1 batch,
                PermanentRewardTransferStateV1 before)
        {
            for (int index = 0;
                index < batch.Rewards.Count;
                index++)
            {
                CollectedRunRewardTransferReceiptV1 existing;
                try
                {
                    if (!authority.TryGetDurableReceiptForReward(
                        batch.Rewards[index]
                            .RewardInstanceStableId,
                        out existing))
                    {
                        continue;
                    }
                }
                catch (Exception exception)
                {
                    return Reject(
                        batch,
                        "collected-run-transfer-overlap-lookup-threw:"
                            + exception.GetType().Name,
                        true,
                        before);
                }

                if (ReceiptMatchesBatch(existing, batch))
                {
                    return Conflict(
                        batch,
                        "collected-run-transfer-durable-receipt-index-inconsistent",
                        before);
                }
                return Reject(
                    batch,
                    "collected-run-transfer-partial-overlap:"
                        + batch.Rewards[index]
                            .RewardInstanceStableId,
                    false,
                    before);
            }
            return null;
        }

        private CollectedRunRewardTransferResultV1
            RejectAfterCompensation(
                CollectedRunRewardTransferBatchV1 batch,
                ICollectedRunRewardTransferCompensationV1
                    compensation,
                string originalDiagnostic,
                CollectedRunRewardTransferPersistenceResultV1
                    persistenceResult)
        {
            CollectedRunRewardTransferRestoreResultV1 restored;
            try
            {
                restored = authority.Restore(compensation);
            }
            catch (Exception exception)
            {
                return Fatal(
                    batch,
                    originalDiagnostic,
                    "collected-run-transfer-restore-threw:"
                        + exception.GetType().Name,
                    persistenceResult);
            }

            if (restored == null || !restored.Restored)
            {
                return Fatal(
                    batch,
                    originalDiagnostic,
                    restored == null
                        ? "collected-run-transfer-restore-result-null"
                        : restored.Diagnostic,
                    persistenceResult);
            }

            return new CollectedRunRewardTransferResultV1(
                CollectedRunRewardTransferStatusV1.Rejected,
                batch.TransferOperationStableId,
                batch.Fingerprint,
                batch.RunStableId,
                batch.SelectedCharacterStableId,
                null,
                TryExportState(),
                persistenceResult
                    ?? CollectedRunRewardTransferPersistenceResultV1
                        .NotAttempted(string.Empty),
                NormalizeDiagnostic(
                    originalDiagnostic,
                    "collected-run-transfer-rejected"),
                restored.Diagnostic,
                true);
        }

        private CollectedRunRewardTransferResultV1 Fatal(
            CollectedRunRewardTransferBatchV1 batch,
            string originalDiagnostic,
            string restorationDiagnostic,
            CollectedRunRewardTransferPersistenceResultV1
                persistenceResult)
        {
            return new CollectedRunRewardTransferResultV1(
                CollectedRunRewardTransferStatusV1
                    .FatalCompensationFailure,
                batch.TransferOperationStableId,
                batch.Fingerprint,
                batch.RunStableId,
                batch.SelectedCharacterStableId,
                null,
                TryExportState(),
                persistenceResult
                    ?? CollectedRunRewardTransferPersistenceResultV1
                        .NotAttempted(string.Empty),
                NormalizeDiagnostic(
                    originalDiagnostic,
                    "collected-run-transfer-failed"),
                NormalizeDiagnostic(
                    restorationDiagnostic,
                    "collected-run-transfer-restoration-failed"),
                false);
        }

        private CollectedRunRewardTransferResultV1 Conflict(
            CollectedRunRewardTransferBatchV1 batch,
            string diagnostic,
            PermanentRewardTransferStateV1 state = null)
        {
            return new CollectedRunRewardTransferResultV1(
                CollectedRunRewardTransferStatusV1
                    .ConflictingDuplicate,
                batch.TransferOperationStableId,
                batch.Fingerprint,
                batch.RunStableId,
                batch.SelectedCharacterStableId,
                null,
                state ?? TryExportState(),
                CollectedRunRewardTransferPersistenceResultV1
                    .NotAttempted(diagnostic),
                diagnostic,
                string.Empty,
                false);
        }

        private CollectedRunRewardTransferResultV1 Reject(
            CollectedRunRewardTransferBatchV1 batch,
            string diagnostic,
            bool exactRetryAllowed,
            PermanentRewardTransferStateV1 state = null)
        {
            return new CollectedRunRewardTransferResultV1(
                CollectedRunRewardTransferStatusV1.Rejected,
                batch.TransferOperationStableId,
                batch.Fingerprint,
                batch.RunStableId,
                batch.SelectedCharacterStableId,
                null,
                state ?? TryExportState(),
                CollectedRunRewardTransferPersistenceResultV1
                    .NotAttempted(diagnostic),
                NormalizeDiagnostic(
                    diagnostic,
                    "collected-run-transfer-rejected"),
                string.Empty,
                exactRetryAllowed);
        }

        private PermanentRewardTransferStateV1
            TryExportState()
        {
            try
            {
                return authority.ExportState();
            }
            catch
            {
                return null;
            }
        }

        private static string ValidateExpectedState(
            CollectedRunRewardTransferBatchV1 batch,
            PermanentRewardTransferStateV1 state)
        {
            if (state == null)
                return "collected-run-transfer-state-null";
            if (state.SelectedCharacterStableId
                != batch.SelectedCharacterStableId)
            {
                return "collected-run-transfer-character-mismatch";
            }
            if (state.CharacterRevision
                != batch.ExpectedCharacterRevision)
            {
                return "collected-run-transfer-character-revision-stale";
            }
            if (!string.Equals(
                state.CharacterFingerprint,
                batch.ExpectedCharacterFingerprint,
                StringComparison.Ordinal))
            {
                return "collected-run-transfer-character-fingerprint-stale";
            }
            return string.Empty;
        }

        private static bool ReceiptMatchesBatch(
            CollectedRunRewardTransferReceiptV1 receipt,
            CollectedRunRewardTransferBatchV1 batch)
        {
            if (receipt == null)
                return false;
            if (receipt.OperationStableId
                    != batch.TransferOperationStableId
                || !string.Equals(
                    receipt.BatchFingerprint,
                    batch.Fingerprint,
                    StringComparison.Ordinal)
                || receipt.RunStableId != batch.RunStableId
                || receipt.AcceptedLifecycleGeneration
                    != batch.AcceptedLifecycleGeneration
                || receipt.MissionResultStableId
                    != batch.AcceptedMissionResultStableId
                || !string.Equals(
                    receipt.MissionResultFingerprint,
                    batch.AcceptedMissionResult.Fingerprint,
                    StringComparison.Ordinal)
                || receipt.SelectedCharacterStableId
                    != batch.SelectedCharacterStableId
                || receipt.AppliedRewardStableIds.Count
                    != batch.Rewards.Count)
            {
                return false;
            }

            for (int index = 0;
                index < batch.Rewards.Count;
                index++)
            {
                if (receipt.AppliedRewardStableIds[index]
                    != batch.Rewards[index]
                        .RewardInstanceStableId)
                {
                    return false;
                }
            }
            return true;
        }

        private static CollectedRunRewardTransferPersistenceResultV1
            AlreadyPersisted(
                PermanentRewardTransferStateV1 state)
        {
            return new CollectedRunRewardTransferPersistenceResultV1(
                CollectedRunRewardTransferPersistenceStatusV1
                    .AlreadyPersisted,
                state == null ? 0L : state.AccountRevision,
                state == null
                    ? string.Empty
                    : state.AccountFingerprint,
                state == null ? 0L : state.CharacterRevision,
                state == null
                    ? string.Empty
                    : state.CharacterFingerprint,
                string.Empty);
        }

        private static string NormalizeDiagnostic(
            string diagnostic,
            string fallback)
        {
            return string.IsNullOrWhiteSpace(diagnostic)
                ? fallback
                : diagnostic.Trim();
        }
    }
}
