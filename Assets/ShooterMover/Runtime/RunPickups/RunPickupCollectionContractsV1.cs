using System;
using System.Text;
using ShooterMover.Domain.Common;

namespace ShooterMover.RunPickups
{
    public sealed class RunPickupCollectionCommandV1
    {
        public RunPickupCollectionCommandV1(
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
            Fingerprint = RunPickupCanonicalV1.Hash(ToCanonicalString());
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
            RunPickupCanonicalV1.Append(
                builder,
                "operation",
                CollectionOperationStableId);
            RunPickupCanonicalV1.Append(builder, "pickup", PickupStableId);
            RunPickupCanonicalV1.Append(
                builder,
                "generated-child",
                GeneratedRewardChildStableId);
            RunPickupCanonicalV1.Append(builder, "run", RunStableId);
            RunPickupCanonicalV1.Append(
                builder,
                "run-generation",
                RunLifecycleGeneration);
            RunPickupCanonicalV1.Append(
                builder,
                "collector-entity",
                CollectorEntityStableId);
            RunPickupCanonicalV1.Append(
                builder,
                "collector-participant",
                CollectorParticipantStableId);
            RunPickupCanonicalV1.Append(
                builder,
                "expected-pickup",
                ExpectedPickupFingerprint);
            return builder.ToString();
        }
    }

    public sealed class RunPickupCollectionFactV1
    {
        public RunPickupCollectionFactV1(
            RunPickupSnapshotV1 availablePickup,
            RunPickupCollectionCommandV1 command,
            long collectionOrder,
            long authoritativeTick)
        {
            AvailablePickup = availablePickup
                ?? throw new ArgumentNullException(nameof(availablePickup));
            Command = command ?? throw new ArgumentNullException(nameof(command));
            if (availablePickup.State != RunPickupStateV1.Available)
                throw new ArgumentException(
                    "Collection facts must originate from an available pickup.",
                    nameof(availablePickup));
            if (collectionOrder < 1L)
                throw new ArgumentOutOfRangeException(nameof(collectionOrder));
            if (authoritativeTick < 0L)
                throw new ArgumentOutOfRangeException(nameof(authoritativeTick));

            CollectionOrder = collectionOrder;
            AuthoritativeTick = authoritativeTick;
            Fingerprint = RunPickupCanonicalV1.Hash(ToCanonicalString());
        }

        public RunPickupSnapshotV1 AvailablePickup { get; }
        public RunPickupCollectionCommandV1 Command { get; }
        public long CollectionOrder { get; }
        public long AuthoritativeTick { get; }
        public string Fingerprint { get; }

        public string ToCanonicalString()
        {
            var builder = new StringBuilder("schema=run-pickup-collection-fact-v1");
            RunPickupCanonicalV1.Append(
                builder,
                "available-pickup",
                AvailablePickup.Fingerprint);
            RunPickupCanonicalV1.Append(builder, "command", Command.Fingerprint);
            RunPickupCanonicalV1.Append(builder, "order", CollectionOrder);
            RunPickupCanonicalV1.Append(builder, "tick", AuthoritativeTick);
            return builder.ToString();
        }
    }

    public sealed class RunPickupSessionRecordResultV1
    {
        public RunPickupSessionRecordResultV1(
            RunPickupSessionRecordStatusV1 status,
            RunPickupCollectionFactV1 fact,
            string diagnostic)
        {
            if (!Enum.IsDefined(typeof(RunPickupSessionRecordStatusV1), status))
                throw new ArgumentOutOfRangeException(nameof(status));
            Status = status;
            Fact = fact;
            Diagnostic = diagnostic ?? string.Empty;
        }

        public RunPickupSessionRecordStatusV1 Status { get; }
        public RunPickupCollectionFactV1 Fact { get; }
        public string Diagnostic { get; }
        public bool IsAccepted
        {
            get
            {
                return Status == RunPickupSessionRecordStatusV1.Accepted
                    || Status == RunPickupSessionRecordStatusV1.ExactReplay;
            }
        }
    }

    public sealed class RunPickupCollectionResultV1
    {
        public RunPickupCollectionResultV1(
            RunPickupCollectionStatusV1 status,
            RunPickupCollectionCommandV1 command,
            RunPickupSnapshotV1 pickup,
            RunPickupCollectionFactV1 collectionFact,
            string diagnostic)
        {
            if (!Enum.IsDefined(typeof(RunPickupCollectionStatusV1), status))
                throw new ArgumentOutOfRangeException(nameof(status));
            Status = status;
            Command = command;
            Pickup = pickup;
            CollectionFact = collectionFact;
            Diagnostic = diagnostic ?? string.Empty;
        }

        public RunPickupCollectionStatusV1 Status { get; }
        public RunPickupCollectionCommandV1 Command { get; }
        public RunPickupSnapshotV1 Pickup { get; }
        public RunPickupCollectionFactV1 CollectionFact { get; }
        public string Diagnostic { get; }
        public bool IsCollected
        {
            get
            {
                return Status == RunPickupCollectionStatusV1.Collected
                    || Status == RunPickupCollectionStatusV1.ExactReplay;
            }
        }
    }
}
