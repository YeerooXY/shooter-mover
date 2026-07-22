using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;
using ShooterMover.Contracts.Rewards;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Rewards.Model;

namespace ShooterMover.Application.Runs.Session
{
    public enum RunSessionRewardCollectionStatusV1
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
    public sealed class RunSessionCollectedRewardV1
    {
        public RunSessionCollectedRewardV1(
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
            RewardGrantKindV1 rewardKind,
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
            if (!Enum.IsDefined(typeof(RewardGrantKindV1), rewardKind))
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
            Fingerprint = RunSessionFingerprintV1.Hash(ToCanonicalString());
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
        public RewardGrantKindV1 RewardKind { get; }
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
            RunSessionFingerprintV1.Append(builder, "pickup", PickupStableId);
            RunSessionFingerprintV1.Append(builder, "child", GeneratedRewardChildStableId);
            RunSessionFingerprintV1.Append(builder, "source-grant", SourceGrantStableId);
            RunSessionFingerprintV1.Append(builder, "drop-operation", DropOperationStableId);
            RunSessionFingerprintV1.Append(builder, "terminal-event", TerminalEventStableId);
            RunSessionFingerprintV1.Append(builder, "triggering-event", TriggeringEventStableId);
            RunSessionFingerprintV1.Append(builder, "run", RunStableId);
            RunSessionFingerprintV1.Append(builder, "run-generation", RunLifecycleGeneration);
            RunSessionFingerprintV1.Append(builder, "source-entity", SourceEntityStableId);
            RunSessionFingerprintV1.Append(builder, "source-placement", SourcePlacementStableId);
            RunSessionFingerprintV1.Append(builder, "source-generation", SourceLifecycleGeneration);
            RunSessionFingerprintV1.Append(builder, "source-definition", SourceDefinitionStableId);
            RunSessionFingerprintV1.Append(builder, "attributed-participant", AttributedParticipantStableId);
            RunSessionFingerprintV1.Append(builder, "reward-kind", (int)RewardKind);
            RunSessionFingerprintV1.Append(builder, "content", ContentStableId);
            RunSessionFingerprintV1.Append(builder, "quantity", Quantity);
            RunSessionFingerprintV1.Append(builder, "generated-batch", GeneratedBatchFingerprint);
            RunSessionFingerprintV1.Append(builder, "generated-reward", GeneratedRewardFingerprint);
            RunSessionFingerprintV1.Append(builder, "room", RoomStableId);
            RunSessionFingerprintV1.Append(builder, "world-x", WorldPositionX);
            RunSessionFingerprintV1.Append(builder, "world-y", WorldPositionY);
            RunSessionFingerprintV1.Append(builder, "world-spawn", WorldSpawnFingerprint);
            RunSessionFingerprintV1.Append(builder, "available-pickup", AvailablePickupFingerprint);
            RunSessionFingerprintV1.Append(builder, "collector-entity", CollectorEntityStableId);
            RunSessionFingerprintV1.Append(builder, "collector-participant", CollectorParticipantStableId);
            RunSessionFingerprintV1.Append(builder, "collection-operation", CollectionOperationStableId);
            RunSessionFingerprintV1.Append(builder, "collection-order", CollectionOrder);
            RunSessionFingerprintV1.Append(builder, "collected-tick", CollectedAtAuthoritativeTick);
            return builder.ToString();
        }

        private static bool IsFinite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }
    }

    public sealed class RunSessionRewardCollectionResultV1
    {
        public RunSessionRewardCollectionResultV1(
            RunSessionRewardCollectionStatusV1 status,
            RunSessionCollectedRewardV1 reward,
            string rejectionCode)
        {
            if (!Enum.IsDefined(typeof(RunSessionRewardCollectionStatusV1), status))
                throw new ArgumentOutOfRangeException(nameof(status));
            Status = status;
            Reward = reward;
            RejectionCode = rejectionCode ?? string.Empty;
        }

        public RunSessionRewardCollectionStatusV1 Status { get; }
        public RunSessionCollectedRewardV1 Reward { get; }
        public string RejectionCode { get; }
        public bool Accepted
        {
            get
            {
                return Status == RunSessionRewardCollectionStatusV1.Collected
                    || Status == RunSessionRewardCollectionStatusV1.ExactReplay;
            }
        }
    }

    public interface IRunSessionCollectedRewardAuthorityV1
    {
        StableId RunStableId { get; }
        long LifecycleGeneration { get; }
        long AuthoritativeTick { get; }
        bool IsActive { get; }
        StableId PlayerActorStableId { get; }
        StableId PlayerParticipantStableId { get; }
        long NextCollectedRewardOrder { get; }

        RunSessionRewardCollectionResultV1 RecordCollectedRunReward(
            RunSessionCollectedRewardV1 reward);
        IReadOnlyList<RunSessionCollectedRewardV1> ExportCollectedRunRewards();
    }
}
