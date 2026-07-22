using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using ShooterMover.Application.Flow.Production;
using ShooterMover.Application.Persistence.Composition;
using ShooterMover.Application.Rewards.Application;
using ShooterMover.Domain.Common;

namespace ShooterMover.Application.Rewards.CollectedRunTransfers
{
    /// <summary>
    /// Immutable Results-facing projection. Retry identity addresses the durable prepared
    /// custody record; no batch or execution delegate is retained by Results.
    /// </summary>
    public sealed class CollectedRunRewardTransferResultsProjectionV1
    {
        private readonly ReadOnlyCollection<StableId> appliedRewardStableIds;

        private CollectedRunRewardTransferResultsProjectionV1(
            StableId custodyStableId,
            StableId transferOperationStableId,
            string batchFingerprint,
            string applicationPlanFingerprint,
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
            CustodyStableId = custodyStableId
                ?? throw new ArgumentNullException(nameof(custodyStableId));
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
            if (!Enum.IsDefined(typeof(CollectedRunRewardTransferStatusV1), status))
                throw new ArgumentOutOfRangeException(nameof(status));
            if (!Enum.IsDefined(
                typeof(CollectedRunRewardTransferPersistenceStatusV1),
                persistenceStatus))
            {
                throw new ArgumentOutOfRangeException(nameof(persistenceStatus));
            }
            if (accountRevision < 0L || characterRevision < 0L)
                throw new ArgumentOutOfRangeException(nameof(accountRevision));

            var rewards = new List<StableId>(
                appliedRewardStableIds ?? Array.Empty<StableId>());
            if (rewards.Exists(item => item == null))
                throw new ArgumentException(
                    "Applied reward identities cannot contain null.",
                    nameof(appliedRewardStableIds));
            rewards.Sort();
            for (int index = 1; index < rewards.Count; index++)
                if (rewards[index - 1] == rewards[index])
                    throw new ArgumentException(
                        "Applied reward identities must be unique.",
                        nameof(appliedRewardStableIds));

            BatchFingerprint = batchFingerprint.Trim();
            ApplicationPlanFingerprint =
                applicationPlanFingerprint ?? string.Empty;
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
                "schema=collected-run-reward-transfer-results-v2");
            CollectedRunRewardTransferCanonicalV1.Append(builder, "custody", CustodyStableId);
            CollectedRunRewardTransferCanonicalV1.Append(builder, "operation", TransferOperationStableId);
            CollectedRunRewardTransferCanonicalV1.Append(builder, "batch", BatchFingerprint);
            CollectedRunRewardTransferCanonicalV1.Append(builder, "plan", ApplicationPlanFingerprint);
            CollectedRunRewardTransferCanonicalV1.Append(builder, "run", RunStableId);
            CollectedRunRewardTransferCanonicalV1.Append(builder, "lifecycle", AcceptedLifecycleGeneration);
            CollectedRunRewardTransferCanonicalV1.Append(builder, "character", SelectedCharacterStableId);
            CollectedRunRewardTransferCanonicalV1.Append(builder, "status", (int)Status);
            for (int index = 0; index < rewards.Count; index++)
                CollectedRunRewardTransferCanonicalV1.Append(
                    builder,
                    "applied:" + index.ToString(CultureInfo.InvariantCulture),
                    rewards[index]);
            CollectedRunRewardTransferCanonicalV1.Append(builder, "receipt", ReceiptFingerprint);
            CollectedRunRewardTransferCanonicalV1.Append(builder, "state", ResultingStateFingerprint);
            CollectedRunRewardTransferCanonicalV1.Append(builder, "account-revision", AccountRevision);
            CollectedRunRewardTransferCanonicalV1.Append(builder, "account", AccountFingerprint);
            CollectedRunRewardTransferCanonicalV1.Append(builder, "character-revision", CharacterRevision);
            CollectedRunRewardTransferCanonicalV1.Append(builder, "character-state", CharacterFingerprint);
            CollectedRunRewardTransferCanonicalV1.Append(builder, "persistence", (int)PersistenceStatus);
            CollectedRunRewardTransferCanonicalV1.Append(builder, "diagnostic", Diagnostic);
            CollectedRunRewardTransferCanonicalV1.Append(builder, "compensation", CompensationDiagnostic);
            CollectedRunRewardTransferCanonicalV1.Append(builder, "retry", ExactRetryAllowed ? 1 : 0);
            Fingerprint =
                CollectedRunRewardTransferCanonicalV1.Hash(builder.ToString());
        }

        public StableId CustodyStableId { get; }
        public StableId TransferOperationStableId { get; }
        public string BatchFingerprint { get; }
        public string ApplicationPlanFingerprint { get; }
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
        public CollectedRunRewardTransferPersistenceStatusV1 PersistenceStatus
        {
            get;
        }
        public string Diagnostic { get; }
        public string CompensationDiagnostic { get; }
        public bool ExactRetryAllowed { get; }
        public string Fingerprint { get; }
        public bool IsComplete
        {
            get
            {
                return Status == CollectedRunRewardTransferStatusV1.Applied
                    || Status == CollectedRunRewardTransferStatusV1.ExactReplay;
            }
        }

        public static CollectedRunRewardTransferResultsProjectionV1 Create(
            CollectedRunRewardPreparedTransferV1 prepared,
            CollectedRunRewardTransferResultV1 result)
        {
            if (prepared == null)
                throw new ArgumentNullException(nameof(prepared));
            if (result == null)
                throw new ArgumentNullException(nameof(result));
            if (prepared.TransferOperationStableId == null
                || result.OperationStableId
                    != prepared.TransferOperationStableId
                || !string.Equals(
                    result.BatchFingerprint,
                    prepared.BatchFingerprint,
                    StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    "The transfer result does not belong to the durable custody record.",
                    nameof(result));
            }
            CollectedRunRewardTransferPersistenceResultV1 persistence =
                result.Persistence
                ?? CollectedRunRewardTransferPersistenceResultV1
                    .NotAttempted(string.Empty);
            return new CollectedRunRewardTransferResultsProjectionV1(
                prepared.CustodyStableId,
                prepared.TransferOperationStableId,
                prepared.BatchFingerprint,
                prepared.ApplicationPlanFingerprint,
                prepared.RunStableId,
                prepared.LifecycleGeneration,
                prepared.SelectedCharacterStableId,
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

        public static CollectedRunRewardTransferResultsProjectionV1
            PreparationFailure(
                CollectedRunRewardPreparedTransferV1 awaiting,
                string diagnostic)
        {
            if (awaiting == null)
                throw new ArgumentNullException(nameof(awaiting));
            StableId operation =
                CollectedRunRewardTransferCanonicalV1.DeriveStableId(
                    "operation",
                    "collected-run-transfer-preparation-failed",
                    awaiting.CustodyStableId.ToString());
            return new CollectedRunRewardTransferResultsProjectionV1(
                awaiting.CustodyStableId,
                operation,
                awaiting.Fingerprint,
                string.Empty,
                awaiting.RunStableId,
                awaiting.LifecycleGeneration,
                awaiting.SelectedCharacterStableId,
                CollectedRunRewardTransferStatusV1.PreparationFailed,
                Array.Empty<StableId>(),
                string.Empty,
                string.Empty,
                0L,
                string.Empty,
                0L,
                string.Empty,
                CollectedRunRewardTransferPersistenceStatusV1.NotAttempted,
                string.IsNullOrWhiteSpace(diagnostic)
                    ? "collected-run-transfer-preparation-failed"
                    : diagnostic,
                string.Empty,
                false);
        }
    }

    public sealed class RetryCollectedRunRewardTransferCommandV1
    {
        public RetryCollectedRunRewardTransferCommandV1(
            StableId custodyStableId,
            StableId transferOperationStableId,
            string batchFingerprint,
            string applicationPlanFingerprint)
        {
            CustodyStableId = custodyStableId
                ?? throw new ArgumentNullException(nameof(custodyStableId));
            TransferOperationStableId = transferOperationStableId
                ?? throw new ArgumentNullException(
                    nameof(transferOperationStableId));
            if (string.IsNullOrWhiteSpace(batchFingerprint)
                || string.IsNullOrWhiteSpace(applicationPlanFingerprint))
            {
                throw new ArgumentException(
                    "Exact batch and application-plan fingerprints are required.");
            }
            BatchFingerprint = batchFingerprint.Trim();
            ApplicationPlanFingerprint = applicationPlanFingerprint.Trim();
            Fingerprint = CollectedRunRewardTransferCanonicalV1.Hash(
                "schema=retry-collected-run-reward-transfer-v2|"
                + CustodyStableId
                + "|"
                + TransferOperationStableId
                + "|"
                + BatchFingerprint
                + "|"
                + ApplicationPlanFingerprint);
        }

        public StableId CustodyStableId { get; }
        public StableId TransferOperationStableId { get; }
        public string BatchFingerprint { get; }
        public string ApplicationPlanFingerprint { get; }
        public string Fingerprint { get; }
    }

    public static class ProductionCollectedRunRewardResultsBridge
    {
        private static readonly object Gate = new object();
        private static CollectedRunRewardTransferResultsProjectionV1 current;

        public static CollectedRunRewardTransferResultsProjectionV1 Current
        {
            get { lock (Gate) return current; }
        }

        public static void Publish(
            CollectedRunRewardPreparedTransferV1 prepared,
            CollectedRunRewardTransferResultV1 result)
        {
            lock (Gate)
                current =
                    CollectedRunRewardTransferResultsProjectionV1.Create(
                        prepared,
                        result);
        }

        public static void PublishPreparationFailure(
            CollectedRunRewardPreparedTransferV1 awaiting,
            string diagnostic)
        {
            lock (Gate)
                current =
                    CollectedRunRewardTransferResultsProjectionV1
                        .PreparationFailure(awaiting, diagnostic);
        }

        public static bool TryRetry(
            RetryCollectedRunRewardTransferCommandV1 command,
            out CollectedRunRewardTransferResultsProjectionV1 projection)
        {
            projection = Current;
            if (command == null
                || projection == null
                || !projection.ExactRetryAllowed
                || command.CustodyStableId != projection.CustodyStableId
                || command.TransferOperationStableId
                    != projection.TransferOperationStableId
                || !string.Equals(
                    command.BatchFingerprint,
                    projection.BatchFingerprint,
                    StringComparison.Ordinal)
                || !string.Equals(
                    command.ApplicationPlanFingerprint,
                    projection.ApplicationPlanFingerprint,
                    StringComparison.Ordinal))
            {
                return false;
            }

            CollectedRunRewardTransferResultsProjectionV1 next;
            if (!TryExecutePrepared(
                projection.SelectedCharacterStableId,
                command.CustodyStableId,
                out next))
            {
                projection = Current;
                return false;
            }
            lock (Gate) current = next;
            projection = next;
            return next.IsComplete;
        }

        public static bool TryRecoverFirstPrepared(
            StableId selectedCharacterStableId,
            out CollectedRunRewardTransferResultsProjectionV1 projection)
        {
            projection = null;
            ProductionCharacterRuntimeGraphV1 graph;
            CharacterCompositionCoordinatorV1 composition;
            RewardApplicationServiceV1 rewardApplication;
            CollectedRunRewardPreparedTransferAuthorityV1 preparedAuthority;
            CollectedRunRewardTransferReceiptAuthorityV1 receipts;
            if (!ProductionCollectedRunRewardRuntimeRegistryV2.TryResolveRuntime(
                    selectedCharacterStableId,
                    out graph,
                    out composition,
                    out rewardApplication,
                    out preparedAuthority,
                    out receipts))
            {
                return false;
            }
            IReadOnlyList<CollectedRunRewardPreparedTransferV1> recoverable =
                preparedAuthority.ExportRecoverable(selectedCharacterStableId);
            if (recoverable.Count == 0) return false;
            if (!TryExecutePrepared(
                selectedCharacterStableId,
                recoverable[0].CustodyStableId,
                out projection))
            {
                return false;
            }
            lock (Gate) current = projection;
            return true;
        }

        public static void Clear()
        {
            lock (Gate) current = null;
        }

        private static bool TryExecutePrepared(
            StableId selectedCharacterStableId,
            StableId custodyStableId,
            out CollectedRunRewardTransferResultsProjectionV1 projection)
        {
            projection = null;
            ProductionCharacterRuntimeGraphV1 graph;
            CharacterCompositionCoordinatorV1 composition;
            RewardApplicationServiceV1 rewardApplication;
            CollectedRunRewardPreparedTransferAuthorityV1 preparedAuthority;
            CollectedRunRewardTransferReceiptAuthorityV1 receipts;
            if (!ProductionCollectedRunRewardRuntimeRegistryV2.TryResolveRuntime(
                    selectedCharacterStableId,
                    out graph,
                    out composition,
                    out rewardApplication,
                    out preparedAuthority,
                    out receipts))
            {
                return false;
            }
            CollectedRunRewardPreparedTransferV1 prepared;
            if (!preparedAuthority.TryGetByCustody(custodyStableId, out prepared)
                || prepared == null
                || prepared.State
                    == CollectedRunRewardPreparedTransferStateV1.AwaitingAcceptedEnd)
            {
                return false;
            }
            CollectedRunRewardAtomicPlanV2 plan;
            string diagnostic;
            if (!CollectedRunRewardTransferPreparationFactoryV2
                .TryBuildPlanFromPrepared(
                    prepared,
                    graph,
                    rewardApplication,
                    out plan,
                    out diagnostic))
            {
                return false;
            }
            var authority = new ProductionCollectedRunRewardAtomicAuthorityV2(
                graph,
                rewardApplication,
                preparedAuthority,
                receipts);
            var persistence = new ProductionCollectedRunRewardPersistenceV2(
                composition,
                preparedAuthority,
                receipts,
                selectedCharacterStableId);
            var service = new ProductionCollectedRunRewardTransferServiceV2(
                plan,
                authority,
                persistence);
            CollectedRunRewardTransferResultV1 result = service.Apply();
            if (result == null) return false;
            projection =
                CollectedRunRewardTransferResultsProjectionV1.Create(
                    prepared,
                    result);
            return true;
        }
    }
}
