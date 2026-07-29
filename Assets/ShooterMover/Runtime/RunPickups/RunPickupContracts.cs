using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using ShooterMover.Contracts.Rewards;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Rewards.Model;

namespace ShooterMover.RunPickups
{
    public enum RunPickupState
    {
        PendingSourcePosition = 1,
        Available = 2,
        Collected = 3,
        Cancelled = 4,
        Rejected = 5,
    }

    public enum RunPickupRealizationStatus
    {
        Realized = 1,
        ExactReplay = 2,
        PendingSourcePosition = 3,
        Rejected = 4,
        ConflictingDuplicate = 5,
    }

    public enum RunPickupCollectionStatus
    {
        Collected = 1,
        ExactReplay = 2,
        Rejected = 3,
        ConflictingDuplicate = 4,
        WrongRun = 5,
        StaleLifecycle = 6,
        WrongPickupChildPairing = 7,
        UnauthorizedCollector = 8,
        PickupUnavailable = 9,
        FingerprintMismatch = 10,
    }

    public enum RunPickupSessionRecordStatus
    {
        Accepted = 1,
        ExactReplay = 2,
        Rejected = 3,
        ConflictingDuplicate = 4,
        WrongRun = 5,
        StaleLifecycle = 6,
        RunEnded = 7,
        UnauthorizedCollector = 8,
    }

    public sealed class RunPickupWorldSpawnContext
    {
        public RunPickupWorldSpawnContext(
            StableId roomStableId,
            double positionX,
            double positionY,
            string sourcePositionFingerprint)
        {
            RoomStableId = roomStableId
                ?? throw new ArgumentNullException(nameof(roomStableId));
            if (!IsFinite(positionX) || !IsFinite(positionY))
            {
                throw new ArgumentOutOfRangeException(nameof(positionX));
            }
            if (string.IsNullOrWhiteSpace(sourcePositionFingerprint))
            {
                throw new ArgumentException(
                    "An authoritative source-position fingerprint is required.",
                    nameof(sourcePositionFingerprint));
            }

            PositionX = positionX;
            PositionY = positionY;
            SourcePositionFingerprint = sourcePositionFingerprint.Trim();
            Fingerprint = RunPickup.Hash(ToCanonicalString());
        }

        public StableId RoomStableId { get; }
        public double PositionX { get; }
        public double PositionY { get; }
        public string SourcePositionFingerprint { get; }
        public string Fingerprint { get; }

        public string ToCanonicalString()
        {
            var builder = new StringBuilder("schema=run-pickup-world-spawn-v1");
            RunPickup.Append(builder, "room", RoomStableId);
            RunPickup.Append(builder, "x", PositionX);
            RunPickup.Append(builder, "y", PositionY);
            RunPickup.Append(
                builder,
                "source-position",
                SourcePositionFingerprint);
            return builder.ToString();
        }

        private static bool IsFinite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }
    }

    public sealed class RunPickupGeneratedReward
    {
        public RunPickupGeneratedReward(
            StableId rewardInstanceStableId,
            int ordinal,
            StableId sourceGrantStableId,
            RewardGrantKind kind,
            StableId contentStableId,
            long quantity,
            string generatedRewardFingerprint)
        {
            RewardInstanceStableId = rewardInstanceStableId
                ?? throw new ArgumentNullException(nameof(rewardInstanceStableId));
            if (ordinal < 0) throw new ArgumentOutOfRangeException(nameof(ordinal));
            SourceGrantStableId = sourceGrantStableId
                ?? throw new ArgumentNullException(nameof(sourceGrantStableId));
            if (!Enum.IsDefined(typeof(RewardGrantKind), kind))
                throw new ArgumentOutOfRangeException(nameof(kind));
            ContentStableId = contentStableId
                ?? throw new ArgumentNullException(nameof(contentStableId));
            if (quantity < 1L) throw new ArgumentOutOfRangeException(nameof(quantity));
            if (string.IsNullOrWhiteSpace(generatedRewardFingerprint))
            {
                throw new ArgumentException(
                    "The exact generated child fingerprint is required.",
                    nameof(generatedRewardFingerprint));
            }

            Ordinal = ordinal;
            Kind = kind;
            Quantity = quantity;
            GeneratedRewardFingerprint = generatedRewardFingerprint.Trim();
            Fingerprint = RunPickup.Hash(ToCanonicalString());
        }

        public StableId RewardInstanceStableId { get; }
        public int Ordinal { get; }
        public StableId SourceGrantStableId { get; }
        public RewardGrantKind Kind { get; }
        public StableId ContentStableId { get; }
        public long Quantity { get; }
        public string GeneratedRewardFingerprint { get; }
        public string Fingerprint { get; }

        public string ToCanonicalString()
        {
            var builder = new StringBuilder("schema=run-pickup-generated-reward-v1");
            RunPickup.Append(builder, "instance", RewardInstanceStableId);
            RunPickup.Append(builder, "ordinal", Ordinal);
            RunPickup.Append(builder, "grant", SourceGrantStableId);
            RunPickup.Append(builder, "kind", (int)Kind);
            RunPickup.Append(builder, "content", ContentStableId);
            RunPickup.Append(builder, "quantity", Quantity);
            RunPickup.Append(
                builder,
                "generated-fingerprint",
                GeneratedRewardFingerprint);
            return builder.ToString();
        }
    }

    public sealed class RunPickupGeneratedBatch
    {
        private readonly ReadOnlyCollection<RunPickupGeneratedReward> rewards;

        public RunPickupGeneratedBatch(
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
            string batchFingerprint,
            IEnumerable<RunPickupGeneratedReward> generatedRewards)
        {
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
            if (string.IsNullOrWhiteSpace(batchFingerprint))
            {
                throw new ArgumentException(
                    "The exact generated batch fingerprint is required.",
                    nameof(batchFingerprint));
            }

            var copy = new List<RunPickupGeneratedReward>();
            foreach (RunPickupGeneratedReward reward in generatedRewards
                ?? throw new ArgumentNullException(nameof(generatedRewards)))
            {
                if (reward == null)
                {
                    throw new ArgumentException(
                        "Generated reward children cannot contain null.",
                        nameof(generatedRewards));
                }
                copy.Add(reward);
            }
            copy.Sort(delegate(
                RunPickupGeneratedReward left,
                RunPickupGeneratedReward right)
            {
                return left.Ordinal.CompareTo(right.Ordinal);
            });

            var ordinals = new HashSet<int>();
            var childIds = new HashSet<StableId>();
            for (int index = 0; index < copy.Count; index++)
            {
                if (!ordinals.Add(copy[index].Ordinal)
                    || !childIds.Add(copy[index].RewardInstanceStableId))
                {
                    throw new ArgumentException(
                        "Generated reward ordinals and exact child identities must be unique.",
                        nameof(generatedRewards));
                }
            }

            TriggeringEventStableId = triggeringEventStableId;
            RunLifecycleGeneration = runLifecycleGeneration;
            SourcePlacementStableId = sourcePlacementStableId;
            SourceLifecycleGeneration = sourceLifecycleGeneration;
            BatchFingerprint = batchFingerprint.Trim();
            rewards = new ReadOnlyCollection<RunPickupGeneratedReward>(copy);
            Fingerprint = RunPickup.Hash(ToCanonicalString());
        }

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
        public string BatchFingerprint { get; }
        public IReadOnlyList<RunPickupGeneratedReward> GeneratedRewards
        {
            get { return rewards; }
        }
        public string Fingerprint { get; }

        public string ToCanonicalString()
        {
            var builder = new StringBuilder("schema=run-pickup-generated-batch-v1");
            RunPickup.Append(builder, "drop-operation", DropOperationStableId);
            RunPickup.Append(builder, "terminal-event", TerminalEventStableId);
            RunPickup.Append(builder, "triggering-event", TriggeringEventStableId);
            RunPickup.Append(builder, "run", RunStableId);
            RunPickup.Append(
                builder,
                "run-generation",
                RunLifecycleGeneration);
            RunPickup.Append(builder, "source-entity", SourceEntityStableId);
            RunPickup.Append(
                builder,
                "source-placement",
                SourcePlacementStableId);
            RunPickup.Append(
                builder,
                "source-generation",
                SourceLifecycleGeneration);
            RunPickup.Append(
                builder,
                "source-definition",
                SourceDefinitionStableId);
            RunPickup.Append(
                builder,
                "participant",
                AttributedParticipantStableId);
            RunPickup.Append(builder, "batch-fingerprint", BatchFingerprint);
            for (int index = 0; index < rewards.Count; index++)
            {
                RunPickup.Append(
                    builder,
                    "reward:" + index.ToString(CultureInfo.InvariantCulture),
                    rewards[index].Fingerprint);
            }
            return builder.ToString();
        }
    }
}
