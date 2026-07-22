using System;
using ShooterMover.Application.Runs.Session;
using ShooterMover.Domain.Common;

namespace ShooterMover.RunPickups
{
    /// <summary>
    /// Narrow adapter to the existing Run Session aggregate. The aggregate remains the
    /// lifecycle, participant, fact-admission, collection-order, and exact collected-reward
    /// journal authority. The pickup authority retains only its immutable world projection.
    /// </summary>
    public sealed class ExistingRunSessionPickupPortV1 : IRunPickupRunSessionPortV1
    {
        private readonly IRunSessionCollectedRewardAuthorityV1 aggregate;

        public ExistingRunSessionPickupPortV1(
            IRunSessionCollectedRewardAuthorityV1 aggregate)
        {
            this.aggregate = aggregate
                ?? throw new ArgumentNullException(nameof(aggregate));
        }

        public StableId RunStableId { get { return aggregate.RunStableId; } }
        public long LifecycleGeneration { get { return aggregate.LifecycleGeneration; } }
        public long AuthoritativeTick { get { return aggregate.AuthoritativeTick; } }
        public bool IsActive { get { return aggregate.IsActive; } }
        public StableId PlayerActorStableId { get { return aggregate.PlayerActorStableId; } }
        public StableId PlayerParticipantStableId
        {
            get { return aggregate.PlayerParticipantStableId; }
        }

        public bool TryReadContext(
            out RunPickupRunSessionContextV1 context,
            out string diagnostic)
        {
            context = null;
            diagnostic = string.Empty;
            try
            {
                context = new RunPickupRunSessionContextV1(
                    aggregate.RunStableId,
                    aggregate.LifecycleGeneration,
                    aggregate.AuthoritativeTick,
                    aggregate.IsActive,
                    aggregate.PlayerActorStableId,
                    aggregate.PlayerParticipantStableId,
                    aggregate.NextCollectedRewardOrder);
                return true;
            }
            catch (Exception exception)
            {
                diagnostic = "run-pickup-session-context-unavailable:"
                    + exception.GetType().Name
                    + ":"
                    + exception.Message;
                return false;
            }
        }

        public RunPickupSessionRecordResultV1 RecordCollection(
            RunPickupCollectionFactV1 fact)
        {
            if (fact == null)
            {
                return new RunPickupSessionRecordResultV1(
                    RunPickupSessionRecordStatusV1.Rejected,
                    null,
                    "run-pickup-session-fact-null");
            }

            RunPickupSnapshotV1 pickup = fact.AvailablePickup;
            RunPickupCollectionCommandV1 command = fact.Command;
            RunPickupWorldSpawnContextV1 world = pickup.WorldSpawnContext;
            if (world == null)
            {
                return new RunPickupSessionRecordResultV1(
                    RunPickupSessionRecordStatusV1.Rejected,
                    fact,
                    "run-pickup-session-world-context-missing");
            }

            RunSessionCollectedRewardV1 exactReward;
            try
            {
                exactReward = new RunSessionCollectedRewardV1(
                    pickup.PickupStableId,
                    pickup.Reward.RewardInstanceStableId,
                    pickup.Reward.SourceGrantStableId,
                    pickup.Batch.DropOperationStableId,
                    pickup.Batch.TerminalEventStableId,
                    pickup.Batch.TriggeringEventStableId,
                    pickup.Batch.RunStableId,
                    pickup.Batch.RunLifecycleGeneration,
                    pickup.Batch.SourceEntityStableId,
                    pickup.Batch.SourcePlacementStableId,
                    pickup.Batch.SourceLifecycleGeneration,
                    pickup.Batch.SourceDefinitionStableId,
                    pickup.Batch.AttributedParticipantStableId,
                    pickup.Reward.Kind,
                    pickup.Reward.ContentStableId,
                    pickup.Reward.Quantity,
                    pickup.Batch.BatchFingerprint,
                    pickup.Reward.GeneratedRewardFingerprint,
                    world.RoomStableId,
                    world.PositionX,
                    world.PositionY,
                    world.Fingerprint,
                    pickup.Fingerprint,
                    command.CollectorEntityStableId,
                    command.CollectorParticipantStableId,
                    command.CollectionOperationStableId,
                    fact.CollectionOrder,
                    fact.AuthoritativeTick);
            }
            catch (Exception exception)
            {
                return new RunPickupSessionRecordResultV1(
                    RunPickupSessionRecordStatusV1.Rejected,
                    fact,
                    "run-pickup-session-exact-record-invalid:" + exception.Message);
            }

            RunSessionRewardCollectionResultV1 result;
            try
            {
                result = aggregate.RecordCollectedRunReward(exactReward);
            }
            catch (Exception exception)
            {
                return new RunPickupSessionRecordResultV1(
                    RunPickupSessionRecordStatusV1.Rejected,
                    fact,
                    "run-pickup-session-exact-record-exception:" + exception.Message);
            }
            if (result == null)
            {
                return new RunPickupSessionRecordResultV1(
                    RunPickupSessionRecordStatusV1.Rejected,
                    fact,
                    "run-pickup-session-exact-record-null");
            }

            return new RunPickupSessionRecordResultV1(
                MapStatus(result.Status),
                fact,
                result.RejectionCode);
        }

        private static RunPickupSessionRecordStatusV1 MapStatus(
            RunSessionRewardCollectionStatusV1 status)
        {
            switch (status)
            {
                case RunSessionRewardCollectionStatusV1.Collected:
                    return RunPickupSessionRecordStatusV1.Accepted;
                case RunSessionRewardCollectionStatusV1.ExactReplay:
                    return RunPickupSessionRecordStatusV1.ExactReplay;
                case RunSessionRewardCollectionStatusV1.ConflictingDuplicate:
                    return RunPickupSessionRecordStatusV1.ConflictingDuplicate;
                case RunSessionRewardCollectionStatusV1.WrongRun:
                    return RunPickupSessionRecordStatusV1.WrongRun;
                case RunSessionRewardCollectionStatusV1.StaleLifecycle:
                    return RunPickupSessionRecordStatusV1.StaleLifecycle;
                case RunSessionRewardCollectionStatusV1.RunEnded:
                    return RunPickupSessionRecordStatusV1.RunEnded;
                case RunSessionRewardCollectionStatusV1.UnauthorizedCollector:
                    return RunPickupSessionRecordStatusV1.UnauthorizedCollector;
                default:
                    return RunPickupSessionRecordStatusV1.Rejected;
            }
        }
    }

    /// <summary>
    /// Pickup-specific composition seam over the exact production Run Session and exact
    /// committed terminal-source positions.
    /// </summary>
    public sealed class RunPickupLiveCompositionV1
    {
        private RunPickupLiveCompositionV1(
            ExistingRunSessionPickupPortV1 runSessionPort,
            RunLocalPickupAuthorityV1 authority,
            PendingTerminalDropPickupConsumerV1 pendingConsumer)
        {
            RunSessionPort = runSessionPort;
            Authority = authority;
            PendingConsumer = pendingConsumer;
        }

        public ExistingRunSessionPickupPortV1 RunSessionPort { get; }
        public RunLocalPickupAuthorityV1 Authority { get; }
        public PendingTerminalDropPickupConsumerV1 PendingConsumer { get; }

        public static RunPickupLiveCompositionV1 Create(
            RunSessionAggregateV1 runSession,
            IRunPickupSourcePositionPortV1 sourcePositions)
        {
            var port = new ExistingRunSessionPickupPortV1(
                runSession ?? throw new ArgumentNullException(nameof(runSession)));
            var authority = new RunLocalPickupAuthorityV1(
                port,
                sourcePositions
                    ?? throw new ArgumentNullException(nameof(sourcePositions)));
            return new RunPickupLiveCompositionV1(
                port,
                authority,
                new PendingTerminalDropPickupConsumerV1(authority));
        }
    }
}
