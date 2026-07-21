using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using ShooterMover.Contracts.Rewards;
using ShooterMover.Domain.Common;

namespace ShooterMover.RunPickups
{
    public enum RunPickupStateV1
    {
        PendingSourcePosition = 1,
        Available = 2,
        Collected = 3,
        Cancelled = 4,
        Rejected = 5,
    }

    public enum RunPickupRealizationStatusV1
    {
        Realized = 1,
        ExactReplay = 2,
        PendingSourcePosition = 3,
        Rejected = 4,
        ConflictingDuplicate = 5,
    }

    public enum RunPickupCollectionStatusV1
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

    public enum RunPickupSessionRecordStatusV1
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

    public sealed class RunPickupWorldSpawnContextV1
    {
        public RunPickupWorldSpawnContextV1(
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
            Fingerprint = RunPickupCanonicalV1.Hash(ToCanonicalString());
        }

        public StableId RoomStableId { get; }
        public double PositionX { get; }
        public double PositionY { get; }
        public string SourcePositionFingerprint { get; }
        public string Fingerprint { get; }

        public string ToCanonicalString()
        {
            var builder = new StringBuilder("schema=run-pickup-world-spawn-v1");
            RunPickupCanonicalV1.Append(builder, "room", RoomStableId);
            RunPickupCanonicalV1.Append(builder, "x", PositionX);
            RunPickupCanonicalV1.Append(builder, "y", PositionY);
            RunPickupCanonicalV1.Append(
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

    public sealed class RunPickupGeneratedRewardV1
    {
        public RunPickupGeneratedRewardV1(
            StableId rewardInstanceStableId,
            int ordinal,
            StableId sourceGrantStableId,
            RewardGrantKindV1 kind,
            StableId contentStableId,
            long quantity,
            string generatedRewardFingerprint)
        {
            RewardInstanceStableId = rewardInstanceStableId
                ?? throw new ArgumentNullException(nameof(rewardInstanceStableId));
            if (ordinal < 0) throw new ArgumentOutOfRangeException(nameof(ordinal));
            SourceGrantStableId = sourceGrantStableId
                ?? throw new ArgumentNullException(nameof(sourceGrantStableId));
            if (!Enum.IsDefined(typeof(RewardGrantKindV1), kind))
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
            Fingerprint = RunPickupCanonicalV1.Hash(ToCanonicalString());
        }

        public StableId RewardInstanceStableId { get; }
        public int Ordinal { get; }
        public StableId SourceGrantStableId { get; }
        public RewardGrantKindV1 Kind { get; }
        public StableId ContentStableId { get; }
        public long Quantity { get; }
        public string GeneratedRewardFingerprint { get; }
        public string Fingerprint { get; }

        public string ToCanonicalString()
        {
            var builder = new StringBuilder("schema=run-pickup-generated-reward-v1");
            RunPickupCanonicalV1.Append(builder, "instance", RewardInstanceStableId);
            RunPickupCanonicalV1.Append(builder, "ordinal", Ordinal);
            RunPickupCanonicalV1.Append(builder, "grant", SourceGrantStableId);
            RunPickupCanonicalV1.Append(builder, "kind", (int)Kind);
            RunPickupCanonicalV1.Append(builder, "content", ContentStableId);
            RunPickupCanonicalV1.Append(builder, "quantity", Quantity);
            RunPickupCanonicalV1.Append(
                builder,
                "generated-fingerprint",
                GeneratedRewardFingerprint);
            return builder.ToString();
        }
    }

    public sealed class RunPickupGeneratedBatchV1
    {
        private readonly ReadOnlyCollection<RunPickupGeneratedRewardV1> rewards;

        public RunPickupGeneratedBatchV1(
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
            IEnumerable<RunPickupGeneratedRewardV1> generatedRewards)
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

            var copy = new List<RunPickupGeneratedRewardV1>();
            foreach (RunPickupGeneratedRewardV1 reward in generatedRewards
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
                RunPickupGeneratedRewardV1 left,
                RunPickupGeneratedRewardV1 right)
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
            rewards = new ReadOnlyCollection<RunPickupGeneratedRewardV1>(copy);
            Fingerprint = RunPickupCanonicalV1.Hash(ToCanonicalString());
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
        public IReadOnlyList<RunPickupGeneratedRewardV1> GeneratedRewards
        {
            get { return rewards; }
        }
        public string Fingerprint { get; }

        public string ToCanonicalString()
        {
            var builder = new StringBuilder("schema=run-pickup-generated-batch-v1");
            RunPickupCanonicalV1.Append(builder, "drop-operation", DropOperationStableId);
            RunPickupCanonicalV1.Append(builder, "terminal-event", TerminalEventStableId);
            RunPickupCanonicalV1.Append(builder, "triggering-event", TriggeringEventStableId);
            RunPickupCanonicalV1.Append(builder, "run", RunStableId);
            RunPickupCanonicalV1.Append(
                builder,
                "run-generation",
                RunLifecycleGeneration);
            RunPickupCanonicalV1.Append(builder, "source-entity", SourceEntityStableId);
            RunPickupCanonicalV1.Append(
                builder,
                "source-placement",
                SourcePlacementStableId);
            RunPickupCanonicalV1.Append(
                builder,
                "source-generation",
                SourceLifecycleGeneration);
            RunPickupCanonicalV1.Append(
                builder,
                "source-definition",
                SourceDefinitionStableId);
            RunPickupCanonicalV1.Append(
                builder,
                "participant",
                AttributedParticipantStableId);
            RunPickupCanonicalV1.Append(builder, "batch-fingerprint", BatchFingerprint);
            for (int index = 0; index < rewards.Count; index++)
            {
                RunPickupCanonicalV1.Append(
                    builder,
                    "reward:" + index.ToString(CultureInfo.InvariantCulture),
                    rewards[index].Fingerprint);
            }
            return builder.ToString();
        }
    }
}
