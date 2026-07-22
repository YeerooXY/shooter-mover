using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using ShooterMover.Domain.Common;

namespace ShooterMover.Application.Rewards.CollectedRunTransfers
{
    /// <summary>
    /// Immutable Results-facing proof of whether one exact collected-run batch reached
    /// permanent character state and durable storage.
    /// </summary>
    public sealed class CollectedRunRewardTransferResultsProjectionV1
    {
        private readonly ReadOnlyCollection<StableId> appliedRewardStableIds;

        private CollectedRunRewardTransferResultsProjectionV1(
            StableId transferOperationStableId,
            string batchFingerprint,
            StableId runStableId,
            long acceptedLifecycleGeneration,
            StableId selectedCharacterStableId,
            CollectedRunRewardTransferStatusV1 status,
            IEnumerable<StableId> appliedRewardStableIds,
            string receiptFingerprint,
            string resultingStateFingerprint,
            long accountRevision,
            string accountFingerprint,
            long characterRevision,
            string characterFingerprint,
            CollectedRunRewardTransferPersistenceStatusV1 persistenceStatus,
            string diagnostic,
            string compensationDiagnostic,
            bool exactRetryAllowed)
        {
            TransferOperationStableId = transferOperationStableId
                ?? throw new ArgumentNullException(
                    nameof(transferOperationStableId));
            if (string.IsNullOrWhiteSpace(batchFingerprint))
                throw new ArgumentException(
                    "A transfer batch fingerprint is required.",
                    nameof(batchFingerprint));
            RunStableId = runStableId
                ?? throw new ArgumentNullException(nameof(runStableId));
            if (acceptedLifecycleGeneration < 0L)
                throw new ArgumentOutOfRangeException(
                    nameof(acceptedLifecycleGeneration));
            SelectedCharacterStableId = selectedCharacterStableId
                ?? throw new ArgumentNullException(
                    nameof(selectedCharacterStableId));
            if (!Enum.IsDefined(
                typeof(CollectedRunRewardTransferStatusV1),
                status))
            {
                throw new ArgumentOutOfRangeException(nameof(status));
            }
            if (!Enum.IsDefined(
                typeof(CollectedRunRewardTransferPersistenceStatusV1),
                persistenceStatus))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(persistenceStatus));
            }
            if (accountRevision < 0L)
                throw new ArgumentOutOfRangeException(
                    nameof(accountRevision));
            if (characterRevision < 0L)
                throw new ArgumentOutOfRangeException(
                    nameof(characterRevision));

            var rewards = new List<StableId>(
                appliedRewardStableIds ?? Array.Empty<StableId>());
            if (rewards.Exists(item => item == null))
                throw new ArgumentException(
                    "Applied reward identities cannot contain null.",
                    nameof(appliedRewardStableIds));
            rewards.Sort((left, right) => left.CompareTo(right));
            for (int index = 1; index < rewards.Count; index++)
            {
                if (rewards[index - 1] == rewards[index])
                    throw new ArgumentException(
                        "Applied reward identities must be unique.",
                        nameof(appliedRewardStableIds));
            }

            BatchFingerprint = batchFingerprint.Trim();
            AcceptedLifecycleGeneration = acceptedLifecycleGeneration;
            Status = status;
            this.appliedRewardStableIds =
                new ReadOnlyCollection<StableId>(rewards);
            ReceiptFingerprint = receiptFingerprint ?? string.Empty;
            ResultingStateFingerprint =
                resultingStateFingerprint ?? string.Empty;
            AccountRevision = accountRevision;
            AccountFingerprint = accountFingerprint ?? string.Empty;
            CharacterRevision = characterRevision;
            CharacterFingerprint = characterFingerprint ?? string.Empty;
            PersistenceStatus = persistenceStatus;
            Diagnostic = diagnostic ?? string.Empty;
            CompensationDiagnostic = compensationDiagnostic ?? string.Empty;
            ExactRetryAllowed = exactRetryAllowed;

            var builder = new StringBuilder(
                "schema=collected-run-reward-transfer-results-v1");
            CollectedRunRewardTransferCanonicalV1.Append(
                builder, "operation", TransferOperationStableId);
            CollectedRunRewardTransferCanonicalV1.Append(
                builder, "batch", BatchFingerprint);
            CollectedRunRewardTransferCanonicalV1.Append(
                builder, "run", RunStableId);
            CollectedRunRewardTransferCanonicalV1.Append(
                builder, "lifecycle", AcceptedLifecycleGeneration);
            CollectedRunRewardTransferCanonicalV1.Append(
                builder, "character", SelectedCharacterStableId);
            CollectedRunRewardTransferCanonicalV1.Append(
                builder, "status", (int)Status);
            CollectedRunRewardTransferCanonicalV1.Append(
                builder, "applied-count", rewards.Count);
            for (int index = 0; index < rewards.Count; index++)
            {
                CollectedRunRewardTransferCanonicalV1.Append(
                    builder,
                    "applied:" + index.ToString(
                        CultureInfo.InvariantCulture),
                    rewards[index]);
            }
            CollectedRunRewardTransferCanonicalV1.Append(
                builder, "receipt", ReceiptFingerprint);
            CollectedRunRewardTransferCanonicalV1.Append(
                builder, "resulting-state", ResultingStateFingerprint);
            CollectedRunRewardTransferCanonicalV1.Append(
                builder, "account-revision", AccountRevision);
            CollectedRunRewardTransferCanonicalV1.Append(
                builder, "account-fingerprint", AccountFingerprint);
            CollectedRunRewardTransferCanonicalV1.Append(
                builder, "character-revision", CharacterRevision);
            CollectedRunRewardTransferCanonicalV1.Append(
                builder, "character-fingerprint", CharacterFingerprint);
            CollectedRunRewardTransferCanonicalV1.Append(
                builder, "persistence", (int)PersistenceStatus);
            CollectedRunRewardTransferCanonicalV1.Append(
                builder, "diagnostic", Diagnostic);
            CollectedRunRewardTransferCanonicalV1.Append(
                builder, "compensation", CompensationDiagnostic);
            CollectedRunRewardTransferCanonicalV1.Append(
                builder, "retry", ExactRetryAllowed ? 1 : 0);
            Fingerprint =
                CollectedRunRewardTransferCanonicalV1.Hash(
                    builder.ToString());
        }

        public StableId TransferOperationStableId { get; }
        public string BatchFingerprint { get; }
        public StableId RunStableId { get; }
        public long AcceptedLifecycleGeneration { get; }
        public StableId SelectedCharacterStableId { get; }
        public CollectedRunRewardTransferStatusV1 Status { get; }
        public IReadOnlyList<StableId> AppliedRewardStableIds
        {
            get { return appliedRewardStableIds; }
        }
        public string ReceiptFingerprint { get; }
        public string ResultingStateFingerprint { get; }
        public long AccountRevision { get; }
        public string AccountFingerprint { get; }
        public long CharacterRevision { get; }
        public string CharacterFingerprint { get; }
        public CollectedRunRewardTransferPersistenceStatusV1
            PersistenceStatus { get; }
        public string Diagnostic { get; }
        public string CompensationDiagnostic { get; }
        public bool ExactRetryAllowed { get; }
        public string Fingerprint { get; }
        public bool IsComplete
        {
            get
            {
                return Status == CollectedRunRewardTransferStatusV1.Applied
                    || Status
                        == CollectedRunRewardTransferStatusV1.ExactReplay;
            }
        }

        public static CollectedRunRewardTransferResultsProjectionV1 Create(
            CollectedRunRewardTransferBatchV1 batch,
            CollectedRunRewardTransferResultV1 result)
        {
            if (batch == null)
                throw new ArgumentNullException(nameof(batch));
            if (result == null)
                throw new ArgumentNullException(nameof(result));
            if (result.OperationStableId
                    != batch.TransferOperationStableId
                || !string.Equals(
                    result.BatchFingerprint,
                    batch.Fingerprint,
                    StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    "The transfer result does not belong to the exact batch.",
                    nameof(result));
            }

            CollectedRunRewardTransferPersistenceResultV1 persistence =
                result.Persistence
                ?? CollectedRunRewardTransferPersistenceResultV1
                    .NotAttempted(string.Empty);
            return new CollectedRunRewardTransferResultsProjectionV1(
                batch.TransferOperationStableId,
                batch.Fingerprint,
                batch.RunStableId,
                batch.AcceptedLifecycleGeneration,
                batch.SelectedCharacterStableId,
                result.Status,
                result.Receipt == null
                    ? Array.Empty<StableId>()
                    : result.Receipt.AppliedRewardStableIds,
                result.Receipt == null
                    ? string.Empty
                    : result.Receipt.Fingerprint,
                result.ResultingState == null
                    ? string.Empty
                    : result.ResultingState.Fingerprint,
                persistence.AccountRevision,
                persistence.AccountFingerprint,
                persistence.CharacterRevision,
                persistence.CharacterFingerprint,
                persistence.Status,
                result.Diagnostic,
                result.CompensationDiagnostic,
                result.ExactRetryAllowed);
        }
    }

    public sealed class RetryCollectedRunRewardTransferCommandV1
    {
        public RetryCollectedRunRewardTransferCommandV1(
            StableId transferOperationStableId,
            string batchFingerprint)
        {
            TransferOperationStableId = transferOperationStableId
                ?? throw new ArgumentNullException(
                    nameof(transferOperationStableId));
            if (string.IsNullOrWhiteSpace(batchFingerprint))
                throw new ArgumentException(
                    "The exact batch fingerprint is required.",
                    nameof(batchFingerprint));
            BatchFingerprint = batchFingerprint.Trim();
            Fingerprint =
                CollectedRunRewardTransferCanonicalV1.Hash(
                    "schema=retry-collected-run-reward-transfer-v1|"
                    + TransferOperationStableId
                    + "|"
                    + BatchFingerprint);
        }

        public StableId TransferOperationStableId { get; }
        public string BatchFingerprint { get; }
        public string Fingerprint { get; }
    }

    /// <summary>
    /// Results-owned immutable projection and exact typed retry seam. The retry delegate
    /// reruns the same frozen application plan; it cannot accept replacement payloads.
    /// </summary>
    public static class ProductionCollectedRunRewardResultsBridge
    {
        private static readonly object Gate = new object();
        private static CollectedRunRewardTransferResultsProjectionV1 current;
        private static Func<CollectedRunRewardTransferResultV1> retry;
        private static CollectedRunRewardTransferBatchV1 batch;

        public static CollectedRunRewardTransferResultsProjectionV1 Current
        {
            get
            {
                lock (Gate) return current;
            }
        }

        public static void Publish(
            CollectedRunRewardTransferBatchV1 exactBatch,
            CollectedRunRewardTransferResultV1 result,
            Func<CollectedRunRewardTransferResultV1> exactRetry)
        {
            if (exactBatch == null)
                throw new ArgumentNullException(nameof(exactBatch));
            if (result == null)
                throw new ArgumentNullException(nameof(result));
            lock (Gate)
            {
                batch = exactBatch;
                current =
                    CollectedRunRewardTransferResultsProjectionV1.Create(
                        exactBatch,
                        result);
                retry = current.ExactRetryAllowed
                    ? exactRetry
                        ?? throw new ArgumentNullException(nameof(exactRetry))
                    : null;
            }
        }

        public static bool TryRetry(
            RetryCollectedRunRewardTransferCommandV1 command,
            out CollectedRunRewardTransferResultsProjectionV1 projection)
        {
            projection = null;
            lock (Gate)
            {
                if (command == null
                    || current == null
                    || batch == null
                    || retry == null
                    || !current.ExactRetryAllowed
                    || command.TransferOperationStableId
                        != current.TransferOperationStableId
                    || !string.Equals(
                        command.BatchFingerprint,
                        current.BatchFingerprint,
                        StringComparison.Ordinal))
                {
                    projection = current;
                    return false;
                }

                CollectedRunRewardTransferResultV1 result = retry();
                if (result == null)
                {
                    projection = current;
                    return false;
                }
                current =
                    CollectedRunRewardTransferResultsProjectionV1.Create(
                        batch,
                        result);
                if (!current.ExactRetryAllowed) retry = null;
                projection = current;
                return current.IsComplete;
            }
        }

        public static void Clear()
        {
            lock (Gate)
            {
                current = null;
                retry = null;
                batch = null;
            }
        }
    }
}
