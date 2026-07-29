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
    public sealed class ExistingRunSessionPickupPort : IRunPickupRunSessionPort
    {
        private readonly IRunSessionCollectedRewardState aggregate;

        public ExistingRunSessionPickupPort(
            IRunSessionCollectedRewardState aggregate)
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
            out RunPickupRunSessionContext context,
            out string diagnostic)
        {
            context = null;
            diagnostic = string.Empty;
            try
            {
                context = new RunPickupRunSessionContext(
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

        public RunPickupSessionRecordResult RecordCollection(
            RunPickupCollectionFact fact)
        {
            if (fact == null)
            {
                return new RunPickupSessionRecordResult(
                    RunPickupSessionRecordStatus.Rejected,
                    null,
                    "run-pickup-session-fact-null");
            }

            RunPickupSnapshot pickup = fact.AvailablePickup;
            RunPickupCollectionCommand command = fact.Command;
            RunPickupWorldSpawnContext world = pickup.WorldSpawnContext;
            if (world == null)
            {
                return new RunPickupSessionRecordResult(
                    RunPickupSessionRecordStatus.Rejected,
                    fact,
                    "run-pickup-session-world-context-missing");
            }

            RunSessionCollectedReward exactReward;
            try
            {
                exactReward = new RunSessionCollectedReward(
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
                return new RunPickupSessionRecordResult(
                    RunPickupSessionRecordStatus.Rejected,
                    fact,
                    "run-pickup-session-exact-record-invalid:" + exception.Message);
            }

            RunSessionRewardCollectionResult result;
            try
            {
                result = aggregate.RecordCollectedRunReward(exactReward);
            }
            catch (Exception exception)
            {
                return new RunPickupSessionRecordResult(
                    RunPickupSessionRecordStatus.Rejected,
                    fact,
                    "run-pickup-session-exact-record-exception:" + exception.Message);
            }
            if (result == null)
            {
                return new RunPickupSessionRecordResult(
                    RunPickupSessionRecordStatus.Rejected,
                    fact,
                    "run-pickup-session-exact-record-null");
            }

            return new RunPickupSessionRecordResult(
                MapStatus(result.Status),
                fact,
                result.RejectionCode);
        }

        private static RunPickupSessionRecordStatus MapStatus(
            RunSessionRewardCollectionStatus status)
        {
            switch (status)
            {
                case RunSessionRewardCollectionStatus.Collected:
                    return RunPickupSessionRecordStatus.Accepted;
                case RunSessionRewardCollectionStatus.ExactReplay:
                    return RunPickupSessionRecordStatus.ExactReplay;
                case RunSessionRewardCollectionStatus.ConflictingDuplicate:
                    return RunPickupSessionRecordStatus.ConflictingDuplicate;
                case RunSessionRewardCollectionStatus.WrongRun:
                    return RunPickupSessionRecordStatus.WrongRun;
                case RunSessionRewardCollectionStatus.StaleLifecycle:
                    return RunPickupSessionRecordStatus.StaleLifecycle;
                case RunSessionRewardCollectionStatus.RunEnded:
                    return RunPickupSessionRecordStatus.RunEnded;
                case RunSessionRewardCollectionStatus.UnauthorizedCollector:
                    return RunPickupSessionRecordStatus.UnauthorizedCollector;
                default:
                    return RunPickupSessionRecordStatus.Rejected;
            }
        }
    }

    /// <summary>
    /// Pickup-specific composition seam over the exact production Run Session and exact
    /// committed terminal-source positions.
    /// </summary>
    public sealed class RunPickupLiveSetup
    {
        private RunPickupLiveSetup(
            ExistingRunSessionPickupPort runSessionPort,
            RunLocalPickupState authority,
            PendingTerminalDropPickupConsumer pendingConsumer)
        {
            RunSessionPort = runSessionPort;
            Authority = authority;
            PendingConsumer = pendingConsumer;
        }

        public ExistingRunSessionPickupPort RunSessionPort { get; }
        public RunLocalPickupState Authority { get; }
        public PendingTerminalDropPickupConsumer PendingConsumer { get; }

        public static RunPickupLiveSetup Create(
            RunSessionAggregate runSession,
            IRunPickupSourcePositionPort sourcePositions)
        {
            var port = new ExistingRunSessionPickupPort(
                runSession ?? throw new ArgumentNullException(nameof(runSession)));
            var authority = new RunLocalPickupState(
                port,
                sourcePositions
                    ?? throw new ArgumentNullException(nameof(sourcePositions)));
            return new RunPickupLiveSetup(
                port,
                authority,
                new PendingTerminalDropPickupConsumer(authority));
        }
    }
}
