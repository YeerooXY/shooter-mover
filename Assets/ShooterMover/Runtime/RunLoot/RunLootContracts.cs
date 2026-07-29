using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using ShooterMover.Contracts.Rewards;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Rewards.Model;

namespace ShooterMover.RunLoot
{
    public enum RunLootState
    {
        PendingSourcePosition = 1,
        Available = 2,
        Collected = 3,
        Cancelled = 4,
        Rejected = 5,
    }

    public enum RunLootRealizationStatus
    {
        Realized = 1,
        ExactReplay = 2,
        PendingSourcePosition = 3,
        Rejected = 4,
        ConflictingDuplicate = 5,
    }

    public enum RunLootCollectionStatus
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

    public enum RunLootSessionRecordStatus
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

    public sealed class RunLootWorldSpawnContext
    {
        public RunLootWorldSpawnContext(
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
            Fingerprint = RunLoot.Hash(ToCanonicalString());
        }

        public StableId RoomStableId { get; }
        public double PositionX { get; }
        public double PositionY { get; }
        public string SourcePositionFingerprint { get; }
        public string Fingerprint { get; }

        public string ToCanonicalString()
        {
            var builder = new StringBuilder("schema=run-pickup-world-spawn-v1");
            RunLoot.Append(builder, "room", RoomStableId);
            RunLoot.Append(builder, "x", PositionX);
            RunLoot.Append(builder, "y", PositionY);
            RunLoot.Append(
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

    public sealed class RunLootGeneratedReward
    {
        public RunLootGeneratedReward(
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
            Fingerprint = RunLoot.Hash(ToCanonicalString());
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
            RunLoot.Append(builder, "instance", RewardInstanceStableId);
            RunLoot.Append(builder, "ordinal", Ordinal);
            RunLoot.Append(builder, "grant", SourceGrantStableId);
            RunLoot.Append(builder, "kind", (int)Kind);
            RunLoot.Append(builder, "content", ContentStableId);
            RunLoot.Append(builder, "quantity", Quantity);
            RunLoot.Append(
                builder,
                "generated-fingerprint",
                GeneratedRewardFingerprint);
            return builder.ToString();
        }
    }

    public sealed class RunLootGeneratedBatch
    {
        private readonly ReadOnlyCollection<RunLootGeneratedReward> rewards;

        public RunLootGeneratedBatch(
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
            IEnumerable<RunLootGeneratedReward> generatedRewards)
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

            var copy = new List<RunLootGeneratedReward>();
            foreach (RunLootGeneratedReward reward in generatedRewards
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
                RunLootGeneratedReward left,
                RunLootGeneratedReward right)
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
            rewards = new ReadOnlyCollection<RunLootGeneratedReward>(copy);
            Fingerprint = RunLoot.Hash(ToCanonicalString());
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
        public IReadOnlyList<RunLootGeneratedReward> GeneratedRewards
        {
            get { return rewards; }
        }
        public string Fingerprint { get; }

        public string ToCanonicalString()
        {
            var builder = new StringBuilder("schema=run-pickup-generated-batch-v1");
            RunLoot.Append(builder, "drop-operation", DropOperationStableId);
            RunLoot.Append(builder, "terminal-event", TerminalEventStableId);
            RunLoot.Append(builder, "triggering-event", TriggeringEventStableId);
            RunLoot.Append(builder, "run", RunStableId);
            RunLoot.Append(
                builder,
                "run-generation",
                RunLifecycleGeneration);
            RunLoot.Append(builder, "source-entity", SourceEntityStableId);
            RunLoot.Append(
                builder,
                "source-placement",
                SourcePlacementStableId);
            RunLoot.Append(
                builder,
                "source-generation",
                SourceLifecycleGeneration);
            RunLoot.Append(
                builder,
                "source-definition",
                SourceDefinitionStableId);
            RunLoot.Append(
                builder,
                "participant",
                AttributedParticipantStableId);
            RunLoot.Append(builder, "batch-fingerprint", BatchFingerprint);
            for (int index = 0; index < rewards.Count; index++)
            {
                RunLoot.Append(
                    builder,
                    "reward:" + index.ToString(CultureInfo.InvariantCulture),
                    rewards[index].Fingerprint);
            }
            return builder.ToString();
        }
    }
}
