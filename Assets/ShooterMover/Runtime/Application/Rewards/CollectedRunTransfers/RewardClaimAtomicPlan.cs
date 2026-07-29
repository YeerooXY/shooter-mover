using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using ShooterMover.Contracts.Rewards.Application;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Rewards.Strongboxes;

namespace ShooterMover.Application.Rewards.CollectedRunTransfers
{
    /// <summary>
    /// One immutable whole-batch RAP/BOX application plan. The permanent authority applies
    /// this object exactly once; reward rows remain canonical audit members of the batch.
    /// </summary>
    public sealed class RewardClaimAtomicPlan
    {
        private readonly ReadOnlyCollection<RewardGrantApplicationPayload> payloads;
        private readonly ReadOnlyCollection<StrongboxInstanceContext> strongboxContexts;

        public RewardClaimAtomicPlan(
            RewardClaimPreparedTransfer preparedTransfer,
            RewardCommitCommand commitCommand,
            RewardClaimCommand claimCommand,
            IEnumerable<RewardGrantApplicationPayload> payloads,
            IEnumerable<StrongboxInstanceContext> strongboxContexts)
        {
            PreparedTransfer = preparedTransfer
                ?? throw new ArgumentNullException(nameof(preparedTransfer));
            if (preparedTransfer.State == RewardClaimPreparedTransferState.AwaitingAcceptedEnd)
                throw new ArgumentException("Atomic plans require an accepted prepared transfer.", nameof(preparedTransfer));
            CommitCommand = commitCommand ?? throw new ArgumentNullException(nameof(commitCommand));
            ClaimCommand = claimCommand ?? throw new ArgumentNullException(nameof(claimCommand));
            if (CommitCommand.SourceOperationStableId != preparedTransfer.TransferOperationStableId
                || ClaimCommand.CommitmentStableId != CommitCommand.CommitmentStableId)
            {
                throw new ArgumentException("RAP commands do not belong to the exact prepared transfer.");
            }

            var payloadCopy = new List<RewardGrantApplicationPayload>(
                payloads ?? throw new ArgumentNullException(nameof(payloads)));
            if (payloadCopy.Exists(item => item == null))
                throw new ArgumentException("Atomic plan payloads cannot contain null.", nameof(payloads));
            payloadCopy.Sort();
            var contextCopy = new List<StrongboxInstanceContext>(
                strongboxContexts ?? throw new ArgumentNullException(nameof(strongboxContexts)));
            if (contextCopy.Exists(item => item == null))
                throw new ArgumentException("Atomic plan strongbox contexts cannot contain null.", nameof(strongboxContexts));
            contextCopy.Sort();
            this.payloads = new ReadOnlyCollection<RewardGrantApplicationPayload>(payloadCopy);
            this.strongboxContexts = new ReadOnlyCollection<StrongboxInstanceContext>(contextCopy);

            Fingerprint = ComputeFingerprint(
                preparedTransfer.BatchFingerprint,
                commitCommand,
                claimCommand,
                payloadCopy,
                contextCopy);
            if (!string.Equals(
                Fingerprint,
                preparedTransfer.ApplicationPlanFingerprint,
                StringComparison.Ordinal))
            {
                throw new ArgumentException("The durable prepared plan fingerprint does not match the rebuilt atomic plan.", nameof(preparedTransfer));
            }
        }

        public RewardClaimPreparedTransfer PreparedTransfer { get; }
        public RewardCommitCommand CommitCommand { get; }
        public RewardClaimCommand ClaimCommand { get; }
        public IReadOnlyList<RewardGrantApplicationPayload> Payloads { get { return payloads; } }
        public IReadOnlyList<StrongboxInstanceContext> StrongboxContexts { get { return strongboxContexts; } }
        public string Fingerprint { get; }
        public StableId TransferOperationStableId { get { return PreparedTransfer.TransferOperationStableId; } }
        public string BatchFingerprint { get { return PreparedTransfer.BatchFingerprint; } }
        public StableId RunStableId { get { return PreparedTransfer.RunStableId; } }
        public StableId SelectedCharacterStableId { get { return PreparedTransfer.SelectedCharacterStableId; } }
        public IReadOnlyList<RewardClaimTransferItem> Rewards { get { return PreparedTransfer.Rewards; } }

        public static string ComputeBatchFingerprint(
            StableId transferOperationStableId,
            StableId runStableId,
            long lifecycleGeneration,
            StableId missionResultStableId,
            string missionResultFingerprint,
            StableId selectedCharacterStableId,
            long expectedCharacterRevision,
            string expectedCharacterFingerprint,
            IReadOnlyList<RewardClaimTransferItem> rewards)
        {
            if (transferOperationStableId == null
                || runStableId == null
                || missionResultStableId == null
                || selectedCharacterStableId == null
                || string.IsNullOrWhiteSpace(missionResultFingerprint)
                || string.IsNullOrWhiteSpace(expectedCharacterFingerprint))
            {
                throw new ArgumentException("Complete accepted transfer identity is required.");
            }
            var ordered = new List<RewardClaimTransferItem>(
                rewards ?? throw new ArgumentNullException(nameof(rewards)));
            ordered.Sort((left, right) =>
            {
                int identity = left.RewardInstanceStableId.CompareTo(right.RewardInstanceStableId);
                return identity != 0 ? identity : string.CompareOrdinal(left.Fingerprint, right.Fingerprint);
            });
            var builder = new StringBuilder("schema=reward-claim-transfer-batch-v2");
            RewardClaimTransfer.Append(builder, "operation", transferOperationStableId);
            RewardClaimTransfer.Append(builder, "run", runStableId);
            RewardClaimTransfer.Append(builder, "lifecycle", lifecycleGeneration);
            RewardClaimTransfer.Append(builder, "mission-result-id", missionResultStableId);
            RewardClaimTransfer.Append(builder, "mission-result", missionResultFingerprint);
            RewardClaimTransfer.Append(builder, "character", selectedCharacterStableId);
            RewardClaimTransfer.Append(builder, "character-revision", expectedCharacterRevision);
            RewardClaimTransfer.Append(builder, "character-fingerprint", expectedCharacterFingerprint);
            for (int index = 0; index < ordered.Count; index++)
                RewardClaimTransfer.Append(
                    builder,
                    "reward:" + index.ToString(CultureInfo.InvariantCulture),
                    ordered[index].Fingerprint);
            return RewardClaimTransfer.Hash(builder.ToString());
        }

        public static string ComputeFingerprint(
            string batchFingerprint,
            RewardCommitCommand commitCommand,
            RewardClaimCommand claimCommand,
            IReadOnlyList<RewardGrantApplicationPayload> payloads,
            IReadOnlyList<StrongboxInstanceContext> strongboxContexts)
        {
            if (string.IsNullOrWhiteSpace(batchFingerprint))
                throw new ArgumentException("The exact batch fingerprint is required.", nameof(batchFingerprint));
            if (commitCommand == null) throw new ArgumentNullException(nameof(commitCommand));
            if (claimCommand == null) throw new ArgumentNullException(nameof(claimCommand));
            var orderedPayloads = new List<RewardGrantApplicationPayload>(
                payloads ?? throw new ArgumentNullException(nameof(payloads)));
            orderedPayloads.Sort();
            var orderedContexts = new List<StrongboxInstanceContext>(
                strongboxContexts ?? throw new ArgumentNullException(nameof(strongboxContexts)));
            orderedContexts.Sort();
            var builder = new StringBuilder("schema=reward-claim-atomic-plan-v2");
            RewardClaimTransfer.Append(builder, "batch", batchFingerprint);
            RewardClaimTransfer.Append(builder, "commit", commitCommand.Fingerprint);
            RewardClaimTransfer.Append(builder, "claim", claimCommand.Fingerprint);
            for (int index = 0; index < orderedPayloads.Count; index++)
                RewardClaimTransfer.Append(
                    builder,
                    "payload:" + index.ToString(CultureInfo.InvariantCulture),
                    orderedPayloads[index].Fingerprint);
            for (int index = 0; index < orderedContexts.Count; index++)
                RewardClaimTransfer.Append(
                    builder,
                    "strongbox:" + index.ToString(CultureInfo.InvariantCulture),
                    orderedContexts[index].Fingerprint);
            return RewardClaimTransfer.Hash(builder.ToString());
        }
    }
}
