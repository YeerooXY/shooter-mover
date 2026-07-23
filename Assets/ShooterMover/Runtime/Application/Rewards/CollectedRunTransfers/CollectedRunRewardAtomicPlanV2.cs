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
    public sealed class CollectedRunRewardAtomicPlanV2
    {
        private readonly ReadOnlyCollection<RewardGrantApplicationPayloadV1> payloads;
        private readonly ReadOnlyCollection<StrongboxInstanceContextV1> strongboxContexts;

        public CollectedRunRewardAtomicPlanV2(
            CollectedRunRewardPreparedTransferV1 preparedTransfer,
            RewardCommitCommandV1 commitCommand,
            RewardClaimCommandV1 claimCommand,
            IEnumerable<RewardGrantApplicationPayloadV1> payloads,
            IEnumerable<StrongboxInstanceContextV1> strongboxContexts)
        {
            PreparedTransfer = preparedTransfer
                ?? throw new ArgumentNullException(nameof(preparedTransfer));
            if (preparedTransfer.State == CollectedRunRewardPreparedTransferStateV1.AwaitingAcceptedEnd)
                throw new ArgumentException("Atomic plans require an accepted prepared transfer.", nameof(preparedTransfer));
            CommitCommand = commitCommand ?? throw new ArgumentNullException(nameof(commitCommand));
            ClaimCommand = claimCommand ?? throw new ArgumentNullException(nameof(claimCommand));
            if (CommitCommand.SourceOperationStableId != preparedTransfer.TransferOperationStableId
                || ClaimCommand.CommitmentStableId != CommitCommand.CommitmentStableId)
            {
                throw new ArgumentException("RAP commands do not belong to the exact prepared transfer.");
            }

            var payloadCopy = new List<RewardGrantApplicationPayloadV1>(
                payloads ?? throw new ArgumentNullException(nameof(payloads)));
            if (payloadCopy.Exists(item => item == null))
                throw new ArgumentException("Atomic plan payloads cannot contain null.", nameof(payloads));
            payloadCopy.Sort();
            var contextCopy = new List<StrongboxInstanceContextV1>(
                strongboxContexts ?? throw new ArgumentNullException(nameof(strongboxContexts)));
            if (contextCopy.Exists(item => item == null))
                throw new ArgumentException("Atomic plan strongbox contexts cannot contain null.", nameof(strongboxContexts));
            contextCopy.Sort();
            this.payloads = new ReadOnlyCollection<RewardGrantApplicationPayloadV1>(payloadCopy);
            this.strongboxContexts = new ReadOnlyCollection<StrongboxInstanceContextV1>(contextCopy);

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

        public CollectedRunRewardPreparedTransferV1 PreparedTransfer { get; }
        public RewardCommitCommandV1 CommitCommand { get; }
        public RewardClaimCommandV1 ClaimCommand { get; }
        public IReadOnlyList<RewardGrantApplicationPayloadV1> Payloads { get { return payloads; } }
        public IReadOnlyList<StrongboxInstanceContextV1> StrongboxContexts { get { return strongboxContexts; } }
        public string Fingerprint { get; }
        public StableId TransferOperationStableId { get { return PreparedTransfer.TransferOperationStableId; } }
        public string BatchFingerprint { get { return PreparedTransfer.BatchFingerprint; } }
        public StableId RunStableId { get { return PreparedTransfer.RunStableId; } }
        public StableId SelectedCharacterStableId { get { return PreparedTransfer.SelectedCharacterStableId; } }
        public IReadOnlyList<CollectedRunRewardTransferItemV1> Rewards { get { return PreparedTransfer.Rewards; } }

        public static string ComputeBatchFingerprint(
            StableId transferOperationStableId,
            StableId runStableId,
            long lifecycleGeneration,
            StableId missionResultStableId,
            string missionResultFingerprint,
            StableId selectedCharacterStableId,
            long expectedCharacterRevision,
            string expectedCharacterFingerprint,
            IReadOnlyList<CollectedRunRewardTransferItemV1> rewards)
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
            var ordered = new List<CollectedRunRewardTransferItemV1>(
                rewards ?? throw new ArgumentNullException(nameof(rewards)));
            ordered.Sort((left, right) =>
            {
                int identity = left.RewardInstanceStableId.CompareTo(right.RewardInstanceStableId);
                return identity != 0 ? identity : string.CompareOrdinal(left.Fingerprint, right.Fingerprint);
            });
            var builder = new StringBuilder("schema=collected-run-reward-transfer-batch-v2");
            CollectedRunRewardTransferCanonicalV1.Append(builder, "operation", transferOperationStableId);
            CollectedRunRewardTransferCanonicalV1.Append(builder, "run", runStableId);
            CollectedRunRewardTransferCanonicalV1.Append(builder, "lifecycle", lifecycleGeneration);
            CollectedRunRewardTransferCanonicalV1.Append(builder, "mission-result-id", missionResultStableId);
            CollectedRunRewardTransferCanonicalV1.Append(builder, "mission-result", missionResultFingerprint);
            CollectedRunRewardTransferCanonicalV1.Append(builder, "character", selectedCharacterStableId);
            CollectedRunRewardTransferCanonicalV1.Append(builder, "character-revision", expectedCharacterRevision);
            CollectedRunRewardTransferCanonicalV1.Append(builder, "character-fingerprint", expectedCharacterFingerprint);
            for (int index = 0; index < ordered.Count; index++)
                CollectedRunRewardTransferCanonicalV1.Append(
                    builder,
                    "reward:" + index.ToString(CultureInfo.InvariantCulture),
                    ordered[index].Fingerprint);
            return CollectedRunRewardTransferCanonicalV1.Hash(builder.ToString());
        }

        public static string ComputeFingerprint(
            string batchFingerprint,
            RewardCommitCommandV1 commitCommand,
            RewardClaimCommandV1 claimCommand,
            IReadOnlyList<RewardGrantApplicationPayloadV1> payloads,
            IReadOnlyList<StrongboxInstanceContextV1> strongboxContexts)
        {
            if (string.IsNullOrWhiteSpace(batchFingerprint))
                throw new ArgumentException("The exact batch fingerprint is required.", nameof(batchFingerprint));
            if (commitCommand == null) throw new ArgumentNullException(nameof(commitCommand));
            if (claimCommand == null) throw new ArgumentNullException(nameof(claimCommand));
            var orderedPayloads = new List<RewardGrantApplicationPayloadV1>(
                payloads ?? throw new ArgumentNullException(nameof(payloads)));
            orderedPayloads.Sort();
            var orderedContexts = new List<StrongboxInstanceContextV1>(
                strongboxContexts ?? throw new ArgumentNullException(nameof(strongboxContexts)));
            orderedContexts.Sort();
            var builder = new StringBuilder("schema=collected-run-reward-atomic-plan-v2");
            CollectedRunRewardTransferCanonicalV1.Append(builder, "batch", batchFingerprint);
            CollectedRunRewardTransferCanonicalV1.Append(builder, "commit", commitCommand.Fingerprint);
            CollectedRunRewardTransferCanonicalV1.Append(builder, "claim", claimCommand.Fingerprint);
            for (int index = 0; index < orderedPayloads.Count; index++)
                CollectedRunRewardTransferCanonicalV1.Append(
                    builder,
                    "payload:" + index.ToString(CultureInfo.InvariantCulture),
                    orderedPayloads[index].Fingerprint);
            for (int index = 0; index < orderedContexts.Count; index++)
                CollectedRunRewardTransferCanonicalV1.Append(
                    builder,
                    "strongbox:" + index.ToString(CultureInfo.InvariantCulture),
                    orderedContexts[index].Fingerprint);
            return CollectedRunRewardTransferCanonicalV1.Hash(builder.ToString());
        }
    }
}
