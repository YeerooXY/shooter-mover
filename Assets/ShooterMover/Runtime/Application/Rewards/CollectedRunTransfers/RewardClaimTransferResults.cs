using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using ShooterMover.Application.Flow.Game;
using ShooterMover.Application.Persistence.Composition;
using ShooterMover.Application.Rewards.Application;
using ShooterMover.Domain.Common;

namespace ShooterMover.Application.Rewards.CollectedRunTransfers
{
    /// <summary>
    /// Immutable Results-facing projection. Retry identity addresses the durable prepared
    /// custody record; no batch or execution delegate is retained by Results.
    /// </summary>
    public sealed class RewardClaimTransferResultsView
    {
        private readonly ReadOnlyCollection<StableId> appliedRewardStableIds;

        private RewardClaimTransferResultsView(
            StableId custodyStableId,
            StableId transferOperationStableId,
            string batchFingerprint,
            string applicationPlanFingerprint,
            StableId runStableId,
            long acceptedLifecycleGeneration,
            StableId selectedCharacterStableId,
            RewardClaimTransferStatus status,
            IEnumerable<StableId> appliedRewardStableIds,
            string receiptFingerprint,
            string resultingStateFingerprint,
            long accountRevision,
            string accountFingerprint,
            long characterRevision,
            string characterFingerprint,
            RewardClaimTransferPersistenceStatus persistenceStatus,
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
            if (!Enum.IsDefined(typeof(RewardClaimTransferStatus), status))
                throw new ArgumentOutOfRangeException(nameof(status));
            if (!Enum.IsDefined(
                typeof(RewardClaimTransferPersistenceStatus),
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
                "schema=reward-claim-transfer-results-v2");
            RewardClaimTransfer.Append(builder, "custody", CustodyStableId);
            RewardClaimTransfer.Append(builder, "operation", TransferOperationStableId);
            RewardClaimTransfer.Append(builder, "batch", BatchFingerprint);
            RewardClaimTransfer.Append(builder, "plan", ApplicationPlanFingerprint);
            RewardClaimTransfer.Append(builder, "run", RunStableId);
            RewardClaimTransfer.Append(builder, "lifecycle", AcceptedLifecycleGeneration);
            RewardClaimTransfer.Append(builder, "character", SelectedCharacterStableId);
            RewardClaimTransfer.Append(builder, "status", (int)Status);
            for (int index = 0; index < rewards.Count; index++)
                RewardClaimTransfer.Append(
                    builder,
                    "applied:" + index.ToString(CultureInfo.InvariantCulture),
                    rewards[index]);
            RewardClaimTransfer.Append(builder, "receipt", ReceiptFingerprint);
            RewardClaimTransfer.Append(builder, "state", ResultingStateFingerprint);
            RewardClaimTransfer.Append(builder, "account-revision", AccountRevision);
            RewardClaimTransfer.Append(builder, "account", AccountFingerprint);
            RewardClaimTransfer.Append(builder, "character-revision", CharacterRevision);
            RewardClaimTransfer.Append(builder, "character-state", CharacterFingerprint);
            RewardClaimTransfer.Append(builder, "persistence", (int)PersistenceStatus);
            RewardClaimTransfer.Append(builder, "diagnostic", Diagnostic);
            RewardClaimTransfer.Append(builder, "compensation", CompensationDiagnostic);
            RewardClaimTransfer.Append(builder, "retry", ExactRetryAllowed ? 1 : 0);
            Fingerprint =
                RewardClaimTransfer.Hash(builder.ToString());
        }

        public StableId CustodyStableId { get; }
        public StableId TransferOperationStableId { get; }
        public string BatchFingerprint { get; }
        public string ApplicationPlanFingerprint { get; }
        public StableId RunStableId { get; }
        public long AcceptedLifecycleGeneration { get; }
        public StableId SelectedCharacterStableId { get; }
        public RewardClaimTransferStatus Status { get; }
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
        public RewardClaimTransferPersistenceStatus PersistenceStatus
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
                return Status == RewardClaimTransferStatus.Applied
                    || Status == RewardClaimTransferStatus.ExactReplay;
            }
        }

        public static RewardClaimTransferResultsView Create(
            RewardClaimPreparedTransfer prepared,
            RewardClaimTransferResult result)
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
            RewardClaimTransferPersistenceResult persistence =
                result.Persistence
                ?? RewardClaimTransferPersistenceResult
                    .NotAttempted(string.Empty);
            return new RewardClaimTransferResultsView(
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

        public static RewardClaimTransferResultsView
            PreparationFailure(
                RewardClaimPreparedTransfer awaiting,
                string diagnostic)
        {
            if (awaiting == null)
                throw new ArgumentNullException(nameof(awaiting));
            StableId operation =
                RewardClaimTransfer.DeriveStableId(
                    "operation",
                    "collected-run-transfer-preparation-failed",
                    awaiting.CustodyStableId.ToString());
            return new RewardClaimTransferResultsView(
                awaiting.CustodyStableId,
                operation,
                awaiting.Fingerprint,
                string.Empty,
                awaiting.RunStableId,
                awaiting.LifecycleGeneration,
                awaiting.SelectedCharacterStableId,
                RewardClaimTransferStatus.PreparationFailed,
                Array.Empty<StableId>(),
                string.Empty,
                string.Empty,
                0L,
                string.Empty,
                0L,
                string.Empty,
                RewardClaimTransferPersistenceStatus.NotAttempted,
                string.IsNullOrWhiteSpace(diagnostic)
                    ? "collected-run-transfer-preparation-failed"
                    : diagnostic,
                string.Empty,
                false);
        }
    }

    public sealed class RetryRewardClaimTransferCommand
    {
        public RetryRewardClaimTransferCommand(
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
            Fingerprint = RewardClaimTransfer.Hash(
                "schema=retry-reward-claim-transfer-v2|"
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

    public static class RewardClaimResultsBridge
    {
        private static readonly object Gate = new object();
        private static RewardClaimTransferResultsView current;

        public static RewardClaimTransferResultsView Current
        {
            get { lock (Gate) return current; }
        }

        public static void Publish(
            RewardClaimPreparedTransfer prepared,
            RewardClaimTransferResult result)
        {
            lock (Gate)
                current =
                    RewardClaimTransferResultsView.Create(
                        prepared,
                        result);
        }

        public static void PublishPreparationFailure(
            RewardClaimPreparedTransfer awaiting,
            string diagnostic)
        {
            lock (Gate)
                current =
                    RewardClaimTransferResultsView
                        .PreparationFailure(awaiting, diagnostic);
        }

        public static bool TryRetry(
            RetryRewardClaimTransferCommand command,
            out RewardClaimTransferResultsView projection)
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

            RewardClaimTransferResultsView next;
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
            out RewardClaimTransferResultsView projection)
        {
            projection = null;
            CharacterLiveGraph graph;
            CharacterSetupFlow composition;
            RewardApplicationActions rewardApplication;
            RewardClaimPreparedTransferStore preparedAuthority;
            RewardClaimTransferReceiptState receipts;
            if (!RewardClaimLiveRegistry.TryResolveRuntime(
                    selectedCharacterStableId,
                    out graph,
                    out composition,
                    out rewardApplication,
                    out preparedAuthority,
                    out receipts))
            {
                return false;
            }
            IReadOnlyList<RewardClaimPreparedTransfer> recoverable =
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
            out RewardClaimTransferResultsView projection)
        {
            projection = null;
            CharacterLiveGraph graph;
            CharacterSetupFlow composition;
            RewardApplicationActions rewardApplication;
            RewardClaimPreparedTransferStore preparedAuthority;
            RewardClaimTransferReceiptState receipts;
            if (!RewardClaimLiveRegistry.TryResolveRuntime(
                    selectedCharacterStableId,
                    out graph,
                    out composition,
                    out rewardApplication,
                    out preparedAuthority,
                    out receipts))
            {
                return false;
            }
            RewardClaimPreparedTransfer prepared;
            if (!preparedAuthority.TryGetByCustody(custodyStableId, out prepared)
                || prepared == null
                || prepared.State
                    == RewardClaimPreparedTransferState.AwaitingAcceptedEnd)
            {
                return false;
            }
            RewardClaimAtomicPlan plan;
            string diagnostic;
            if (!RewardClaimTransferPreparationFactory
                .TryBuildPlanFromPrepared(
                    prepared,
                    graph,
                    rewardApplication,
                    out plan,
                    out diagnostic))
            {
                return false;
            }
            var authority = new RewardClaimAtomicState(
                graph,
                rewardApplication,
                preparedAuthority,
                receipts);
            var persistence = new RewardClaimPersistence(
                composition,
                preparedAuthority,
                receipts,
                selectedCharacterStableId);
            var service = new RewardClaimTransferActions(
                plan,
                authority,
                persistence);
            RewardClaimTransferResult result = service.Apply();
            if (result == null) return false;
            projection =
                RewardClaimTransferResultsView.Create(
                    prepared,
                    result);
            return true;
        }
    }
}
