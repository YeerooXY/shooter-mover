using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;
using ShooterMover.Contracts.Rewards;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Rewards.Model;

namespace ShooterMover.Application.Runs.Session
{
    public enum RunSessionRewardCollectionStatus
    {
        Collected = 1,
        ExactReplay = 2,
        Rejected = 3,
        ConflictingDuplicate = 4,
        WrongRun = 5,
        StaleLifecycle = 6,
        RunEnded = 7,
        UnauthorizedCollector = 8,
    }

    /// <summary>
    /// Exact immutable run-local record of one generated reward child collected during
    /// one Run Session lifecycle. This is mission state only, never permanent holdings.
    /// </summary>
    public sealed class RunSessionCollectedReward
    {
        public RunSessionCollectedReward(
            StableId pickupStableId,
            StableId generatedRewardChildStableId,
            StableId sourceGrantStableId,
            StableId dropOperationStableId,
            StableId terminalEventStableId,
            StableId triggeringEventStableId,
            StableId runStableId,
            long runLifecycleGeneration,
            StableId sourceEntityStableId,
            StableId sourcePlacementStableId,
            long sourceLifecycleGeneration,
            StableId sourceDefinitionStableId,
            StableId attributedParticipantStableId,
            RewardGrantKind rewardKind,
            StableId contentStableId,
            long quantity,
            string generatedBatchFingerprint,
            string generatedRewardFingerprint,
            StableId roomStableId,
            double worldPositionX,
            double worldPositionY,
            string worldSpawnFingerprint,
            string availablePickupFingerprint,
            StableId collectorEntityStableId,
            StableId collectorParticipantStableId,
            StableId collectionOperationStableId,
            long collectionOrder,
            long collectedAtAuthoritativeTick)
        {
            PickupStableId = pickupStableId
                ?? throw new ArgumentNullException(nameof(pickupStableId));
            GeneratedRewardChildStableId = generatedRewardChildStableId
                ?? throw new ArgumentNullException(nameof(generatedRewardChildStableId));
            SourceGrantStableId = sourceGrantStableId
                ?? throw new ArgumentNullException(nameof(sourceGrantStableId));
            DropOperationStableId = dropOperationStableId
                ?? throw new ArgumentNullException(nameof(dropOperationStableId));
            TerminalEventStableId = terminalEventStableId
                ?? throw new ArgumentNullException(nameof(terminalEventStableId));
            RunStableId = runStableId
                ?? throw new ArgumentNullException(nameof(runStableId));
            if (runLifecycleGeneration < 0L)
                throw new ArgumentOutOfRangeException(nameof(runLifecycleGeneration));
            SourceEntityStableId = sourceEntityStableId
                ?? throw new ArgumentNullException(nameof(sourceEntityStableId));
            if (sourceLifecycleGeneration < 0L)
                throw new ArgumentOutOfRangeException(nameof(sourceLifecycleGeneration));
            SourceDefinitionStableId = sourceDefinitionStableId
                ?? throw new ArgumentNullException(nameof(sourceDefinitionStableId));
            AttributedParticipantStableId = attributedParticipantStableId
                ?? throw new ArgumentNullException(nameof(attributedParticipantStableId));
            if (!Enum.IsDefined(typeof(RewardGrantKind), rewardKind))
                throw new ArgumentOutOfRangeException(nameof(rewardKind));
            ContentStableId = contentStableId
                ?? throw new ArgumentNullException(nameof(contentStableId));
            if (quantity < 1L) throw new ArgumentOutOfRangeException(nameof(quantity));
            if (string.IsNullOrWhiteSpace(generatedBatchFingerprint))
                throw new ArgumentException("Generated batch fingerprint is required.", nameof(generatedBatchFingerprint));
            if (string.IsNullOrWhiteSpace(generatedRewardFingerprint))
                throw new ArgumentException("Generated reward fingerprint is required.", nameof(generatedRewardFingerprint));
            RoomStableId = roomStableId
                ?? throw new ArgumentNullException(nameof(roomStableId));
            if (!IsFinite(worldPositionX) || !IsFinite(worldPositionY))
                throw new ArgumentOutOfRangeException(nameof(worldPositionX));
            if (string.IsNullOrWhiteSpace(worldSpawnFingerprint))
                throw new ArgumentException("World-spawn fingerprint is required.", nameof(worldSpawnFingerprint));
            if (string.IsNullOrWhiteSpace(availablePickupFingerprint))
                throw new ArgumentException("Available pickup fingerprint is required.", nameof(availablePickupFingerprint));
            CollectorEntityStableId = collectorEntityStableId
                ?? throw new ArgumentNullException(nameof(collectorEntityStableId));
            CollectorParticipantStableId = collectorParticipantStableId
                ?? throw new ArgumentNullException(nameof(collectorParticipantStableId));
            CollectionOperationStableId = collectionOperationStableId
                ?? throw new ArgumentNullException(nameof(collectionOperationStableId));
            if (collectionOrder < 1L)
                throw new ArgumentOutOfRangeException(nameof(collectionOrder));
            if (collectedAtAuthoritativeTick < 0L)
                throw new ArgumentOutOfRangeException(nameof(collectedAtAuthoritativeTick));

            TriggeringEventStableId = triggeringEventStableId;
            RunLifecycleGeneration = runLifecycleGeneration;
            SourcePlacementStableId = sourcePlacementStableId;
            SourceLifecycleGeneration = sourceLifecycleGeneration;
            RewardKind = rewardKind;
            Quantity = quantity;
            GeneratedBatchFingerprint = generatedBatchFingerprint.Trim();
            GeneratedRewardFingerprint = generatedRewardFingerprint.Trim();
            WorldPositionX = worldPositionX;
            WorldPositionY = worldPositionY;
            WorldSpawnFingerprint = worldSpawnFingerprint.Trim();
            AvailablePickupFingerprint = availablePickupFingerprint.Trim();
            CollectionOrder = collectionOrder;
            CollectedAtAuthoritativeTick = collectedAtAuthoritativeTick;
            Fingerprint = RunSessionFingerprint.Hash(ToCanonicalString());
        }

        public StableId PickupStableId { get; }
        public StableId GeneratedRewardChildStableId { get; }
        public StableId SourceGrantStableId { get; }
        public StableId DropOperationStableId { get; }
        public StableId TerminalEventStableId { get; }
        public StableId TriggeringEventStableId { get; }
        public StableId RunStableId { get; }
        public long RunLifecycleGeneration { get; }
        public StableId SourceEntityStableId { get; }
        public StableId SourcePlacementStableId { get; }
        public long SourceLifecycleGeneration { get; }
        public StableId SourceDefinitionStableId { get; }
        public StableId AttributedParticipantStableId { get; }
        public RewardGrantKind RewardKind { get; }
        public StableId ContentStableId { get; }
        public long Quantity { get; }
        public string GeneratedBatchFingerprint { get; }
        public string GeneratedRewardFingerprint { get; }
        public StableId RoomStableId { get; }
        public double WorldPositionX { get; }
        public double WorldPositionY { get; }
        public string WorldSpawnFingerprint { get; }
        public string AvailablePickupFingerprint { get; }
        public StableId CollectorEntityStableId { get; }
        public StableId CollectorParticipantStableId { get; }
        public StableId CollectionOperationStableId { get; }
        public long CollectionOrder { get; }
        public long CollectedAtAuthoritativeTick { get; }
        public string Fingerprint { get; }

        public string ToCanonicalString()
        {
            var builder = new StringBuilder("schema=run-session-collected-reward-v1");
            RunSessionFingerprint.Append(builder, "pickup", PickupStableId);
            RunSessionFingerprint.Append(builder, "child", GeneratedRewardChildStableId);
            RunSessionFingerprint.Append(builder, "source-grant", SourceGrantStableId);
            RunSessionFingerprint.Append(builder, "drop-operation", DropOperationStableId);
            RunSessionFingerprint.Append(builder, "terminal-event", TerminalEventStableId);
            RunSessionFingerprint.Append(builder, "triggering-event", TriggeringEventStableId);
            RunSessionFingerprint.Append(builder, "run", RunStableId);
            RunSessionFingerprint.Append(builder, "run-generation", RunLifecycleGeneration);
            RunSessionFingerprint.Append(builder, "source-entity", SourceEntityStableId);
            RunSessionFingerprint.Append(builder, "source-placement", SourcePlacementStableId);
            RunSessionFingerprint.Append(builder, "source-generation", SourceLifecycleGeneration);
            RunSessionFingerprint.Append(builder, "source-definition", SourceDefinitionStableId);
            RunSessionFingerprint.Append(builder, "attributed-participant", AttributedParticipantStableId);
            RunSessionFingerprint.Append(builder, "reward-kind", (int)RewardKind);
            RunSessionFingerprint.Append(builder, "content", ContentStableId);
            RunSessionFingerprint.Append(builder, "quantity", Quantity);
            RunSessionFingerprint.Append(builder, "generated-batch", GeneratedBatchFingerprint);
            RunSessionFingerprint.Append(builder, "generated-reward", GeneratedRewardFingerprint);
            RunSessionFingerprint.Append(builder, "room", RoomStableId);
            RunSessionFingerprint.Append(builder, "world-x", WorldPositionX);
            RunSessionFingerprint.Append(builder, "world-y", WorldPositionY);
            RunSessionFingerprint.Append(builder, "world-spawn", WorldSpawnFingerprint);
            RunSessionFingerprint.Append(builder, "available-pickup", AvailablePickupFingerprint);
            RunSessionFingerprint.Append(builder, "collector-entity", CollectorEntityStableId);
            RunSessionFingerprint.Append(builder, "collector-participant", CollectorParticipantStableId);
            RunSessionFingerprint.Append(builder, "collection-operation", CollectionOperationStableId);
            RunSessionFingerprint.Append(builder, "collection-order", CollectionOrder);
            RunSessionFingerprint.Append(builder, "collected-tick", CollectedAtAuthoritativeTick);
            return builder.ToString();
        }

        private static bool IsFinite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }
    }

    public sealed class RunSessionRewardCollectionResult
    {
        public RunSessionRewardCollectionResult(
            RunSessionRewardCollectionStatus status,
            RunSessionCollectedReward reward,
            string rejectionCode)
        {
            if (!Enum.IsDefined(typeof(RunSessionRewardCollectionStatus), status))
                throw new ArgumentOutOfRangeException(nameof(status));
            Status = status;
            Reward = reward;
            RejectionCode = rejectionCode ?? string.Empty;
        }

        public RunSessionRewardCollectionStatus Status { get; }
        public RunSessionCollectedReward Reward { get; }
        public string RejectionCode { get; }
        public bool Accepted
        {
            get
            {
                return Status == RunSessionRewardCollectionStatus.Collected
                    || Status == RunSessionRewardCollectionStatus.ExactReplay;
            }
        }
    }

    public interface IRunSessionCollectedRewardState
    {
        StableId RunStableId { get; }
        long LifecycleGeneration { get; }
        long AuthoritativeTick { get; }
        bool IsActive { get; }
        StableId PlayerActorStableId { get; }
        StableId PlayerParticipantStableId { get; }
        long NextCollectedRewardOrder { get; }

        RunSessionRewardCollectionResult RecordCollectedRunReward(
            RunSessionCollectedReward reward);
        IReadOnlyList<RunSessionCollectedReward> ExportCollectedRunRewards();
    }
}
