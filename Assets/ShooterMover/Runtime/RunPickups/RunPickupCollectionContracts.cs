using System;
using System.Text;
using ShooterMover.Domain.Common;

namespace ShooterMover.RunPickups
{
    public sealed class RunPickupCollectionCommand
    {
        public RunPickupCollectionCommand(
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
            Fingerprint = RunPickup.Hash(ToCanonicalString());
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
            RunPickup.Append(
                builder,
                "operation",
                CollectionOperationStableId);
            RunPickup.Append(builder, "pickup", PickupStableId);
            RunPickup.Append(
                builder,
                "generated-child",
                GeneratedRewardChildStableId);
            RunPickup.Append(builder, "run", RunStableId);
            RunPickup.Append(
                builder,
                "run-generation",
                RunLifecycleGeneration);
            RunPickup.Append(
                builder,
                "collector-entity",
                CollectorEntityStableId);
            RunPickup.Append(
                builder,
                "collector-participant",
                CollectorParticipantStableId);
            RunPickup.Append(
                builder,
                "expected-pickup",
                ExpectedPickupFingerprint);
            return builder.ToString();
        }
    }

    public sealed class RunPickupCollectionFact
    {
        public RunPickupCollectionFact(
            RunPickupSnapshot availablePickup,
            RunPickupCollectionCommand command,
            long collectionOrder,
            long authoritativeTick)
        {
            AvailablePickup = availablePickup
                ?? throw new ArgumentNullException(nameof(availablePickup));
            Command = command ?? throw new ArgumentNullException(nameof(command));
            if (availablePickup.State != RunPickupState.Available)
                throw new ArgumentException(
                    "Collection facts must originate from an available pickup.",
                    nameof(availablePickup));
            if (collectionOrder < 1L)
                throw new ArgumentOutOfRangeException(nameof(collectionOrder));
            if (authoritativeTick < 0L)
                throw new ArgumentOutOfRangeException(nameof(authoritativeTick));

            CollectionOrder = collectionOrder;
            AuthoritativeTick = authoritativeTick;
            Fingerprint = RunPickup.Hash(ToCanonicalString());
        }

        public RunPickupSnapshot AvailablePickup { get; }
        public RunPickupCollectionCommand Command { get; }
        public long CollectionOrder { get; }
        public long AuthoritativeTick { get; }
        public string Fingerprint { get; }

        public string ToCanonicalString()
        {
            var builder = new StringBuilder("schema=run-pickup-collection-fact-v1");
            RunPickup.Append(
                builder,
                "available-pickup",
                AvailablePickup.Fingerprint);
            RunPickup.Append(builder, "command", Command.Fingerprint);
            RunPickup.Append(builder, "order", CollectionOrder);
            RunPickup.Append(builder, "tick", AuthoritativeTick);
            return builder.ToString();
        }
    }

    public sealed class RunPickupSessionRecordResult
    {
        public RunPickupSessionRecordResult(
            RunPickupSessionRecordStatus status,
            RunPickupCollectionFact fact,
            string diagnostic)
        {
            if (!Enum.IsDefined(typeof(RunPickupSessionRecordStatus), status))
                throw new ArgumentOutOfRangeException(nameof(status));
            Status = status;
            Fact = fact;
            Diagnostic = diagnostic ?? string.Empty;
        }

        public RunPickupSessionRecordStatus Status { get; }
        public RunPickupCollectionFact Fact { get; }
        public string Diagnostic { get; }
        public bool IsAccepted
        {
            get
            {
                return Status == RunPickupSessionRecordStatus.Accepted
                    || Status == RunPickupSessionRecordStatus.ExactReplay;
            }
        }
    }

    public sealed class RunPickupCollectionResult
    {
        public RunPickupCollectionResult(
            RunPickupCollectionStatus status,
            RunPickupCollectionCommand command,
            RunPickupSnapshot pickup,
            RunPickupCollectionFact collectionFact,
            string diagnostic)
        {
            if (!Enum.IsDefined(typeof(RunPickupCollectionStatus), status))
                throw new ArgumentOutOfRangeException(nameof(status));
            Status = status;
            Command = command;
            Pickup = pickup;
            CollectionFact = collectionFact;
            Diagnostic = diagnostic ?? string.Empty;
        }

        public RunPickupCollectionStatus Status { get; }
        public RunPickupCollectionCommand Command { get; }
        public RunPickupSnapshot Pickup { get; }
        public RunPickupCollectionFact CollectionFact { get; }
        public string Diagnostic { get; }
        public bool IsCollected
        {
            get
            {
                return Status == RunPickupCollectionStatus.Collected
                    || Status == RunPickupCollectionStatus.ExactReplay;
            }
        }
    }
}
