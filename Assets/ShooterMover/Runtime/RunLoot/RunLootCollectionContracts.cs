using System;
using System.Text;
using ShooterMover.Domain.Common;

namespace ShooterMover.RunLoot
{
    public sealed class RunLootCollectionCommand
    {
        public RunLootCollectionCommand(
            StableId collectionOperationStableId,
            StableId pickupStableId,
            StableId generatedRewardChildStableId,
            StableId runStableId,
            long runLifecycleGeneration,
            StableId collectorEntityStableId,
            StableId collectorParticipantStableId,
            string expectedPickupFingerprint)
        {
            CollectionOperationStableId = collectionOperationStableId
                ?? throw new ArgumentNullException(nameof(collectionOperationStableId));
            PickupStableId = pickupStableId
                ?? throw new ArgumentNullException(nameof(pickupStableId));
            GeneratedRewardChildStableId = generatedRewardChildStableId
                ?? throw new ArgumentNullException(nameof(generatedRewardChildStableId));
            RunStableId = runStableId
                ?? throw new ArgumentNullException(nameof(runStableId));
            if (runLifecycleGeneration < 0L)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(runLifecycleGeneration));
            }

            RunLifecycleGeneration = runLifecycleGeneration;
            CollectorEntityStableId = collectorEntityStableId;
            CollectorParticipantStableId = collectorParticipantStableId;
            ExpectedPickupFingerprint = expectedPickupFingerprint == null
                ? string.Empty
                : expectedPickupFingerprint.Trim();
            Fingerprint = RunLoot.Hash(ToCanonicalString());
        }

        public StableId CollectionOperationStableId { get; }
        public StableId PickupStableId { get; }
        public StableId GeneratedRewardChildStableId { get; }
        public StableId RunStableId { get; }
        public long RunLifecycleGeneration { get; }
        public StableId CollectorEntityStableId { get; }
        public StableId CollectorParticipantStableId { get; }
        public string ExpectedPickupFingerprint { get; }
        public string Fingerprint { get; }

        public string ToCanonicalString()
        {
            var builder = new StringBuilder("schema=run-pickup-collection-command-v1");
            RunLoot.Append(
                builder,
                "operation",
                CollectionOperationStableId);
            RunLoot.Append(builder, "pickup", PickupStableId);
            RunLoot.Append(
                builder,
                "generated-child",
                GeneratedRewardChildStableId);
            RunLoot.Append(builder, "run", RunStableId);
            RunLoot.Append(
                builder,
                "run-generation",
                RunLifecycleGeneration);
            RunLoot.Append(
                builder,
                "collector-entity",
                CollectorEntityStableId);
            RunLoot.Append(
                builder,
                "collector-participant",
                CollectorParticipantStableId);
            RunLoot.Append(
                builder,
                "expected-pickup",
                ExpectedPickupFingerprint);
            return builder.ToString();
        }
    }

    public sealed class RunLootCollectionFact
    {
        public RunLootCollectionFact(
            RunLootSnapshot availablePickup,
            RunLootCollectionCommand command,
            long collectionOrder,
            long authoritativeTick)
        {
            AvailablePickup = availablePickup
                ?? throw new ArgumentNullException(nameof(availablePickup));
            Command = command ?? throw new ArgumentNullException(nameof(command));
            if (availablePickup.State != RunLootState.Available)
                throw new ArgumentException(
                    "Collection facts must originate from an available pickup.",
                    nameof(availablePickup));
            if (collectionOrder < 1L)
                throw new ArgumentOutOfRangeException(nameof(collectionOrder));
            if (authoritativeTick < 0L)
                throw new ArgumentOutOfRangeException(nameof(authoritativeTick));

            CollectionOrder = collectionOrder;
            AuthoritativeTick = authoritativeTick;
            Fingerprint = RunLoot.Hash(ToCanonicalString());
        }

        public RunLootSnapshot AvailablePickup { get; }
        public RunLootCollectionCommand Command { get; }
        public long CollectionOrder { get; }
        public long AuthoritativeTick { get; }
        public string Fingerprint { get; }

        public string ToCanonicalString()
        {
            var builder = new StringBuilder("schema=run-pickup-collection-fact-v1");
            RunLoot.Append(
                builder,
                "available-pickup",
                AvailablePickup.Fingerprint);
            RunLoot.Append(builder, "command", Command.Fingerprint);
            RunLoot.Append(builder, "order", CollectionOrder);
            RunLoot.Append(builder, "tick", AuthoritativeTick);
            return builder.ToString();
        }
    }

    public sealed class RunLootSessionRecordResult
    {
        public RunLootSessionRecordResult(
            RunLootSessionRecordStatus status,
            RunLootCollectionFact fact,
            string diagnostic)
        {
            if (!Enum.IsDefined(typeof(RunLootSessionRecordStatus), status))
                throw new ArgumentOutOfRangeException(nameof(status));
            Status = status;
            Fact = fact;
            Diagnostic = diagnostic ?? string.Empty;
        }

        public RunLootSessionRecordStatus Status { get; }
        public RunLootCollectionFact Fact { get; }
        public string Diagnostic { get; }
        public bool IsAccepted
        {
            get
            {
                return Status == RunLootSessionRecordStatus.Accepted
                    || Status == RunLootSessionRecordStatus.ExactReplay;
            }
        }
    }

    public sealed class RunLootCollectionResult
    {
        public RunLootCollectionResult(
            RunLootCollectionStatus status,
            RunLootCollectionCommand command,
            RunLootSnapshot pickup,
            RunLootCollectionFact collectionFact,
            string diagnostic)
        {
            if (!Enum.IsDefined(typeof(RunLootCollectionStatus), status))
                throw new ArgumentOutOfRangeException(nameof(status));
            Status = status;
            Command = command;
            Pickup = pickup;
            CollectionFact = collectionFact;
            Diagnostic = diagnostic ?? string.Empty;
        }

        public RunLootCollectionStatus Status { get; }
        public RunLootCollectionCommand Command { get; }
        public RunLootSnapshot Pickup { get; }
        public RunLootCollectionFact CollectionFact { get; }
        public string Diagnostic { get; }
        public bool IsCollected
        {
            get
            {
                return Status == RunLootCollectionStatus.Collected
                    || Status == RunLootCollectionStatus.ExactReplay;
            }
        }
    }
}
