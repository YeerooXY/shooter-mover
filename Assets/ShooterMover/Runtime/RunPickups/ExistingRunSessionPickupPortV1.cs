using System;
using ShooterMover.Application.Runs.Session;
using ShooterMover.Domain.Common;

namespace ShooterMover.RunPickups
{
    /// <summary>
    /// Narrow adapter to the existing Run Session aggregate. The aggregate remains the
    /// lifecycle/participant/fact-admission authority. The exact typed reward child and
    /// collection journal remain owned by RunLocalPickupAuthorityV1.
    /// </summary>
    public sealed class ExistingRunSessionPickupPortV1 : IRunPickupRunSessionPortV1
    {
        private readonly RunSessionAggregateV1 aggregate;

        public ExistingRunSessionPickupPortV1(RunSessionAggregateV1 aggregate)
        {
            this.aggregate = aggregate
                ?? throw new ArgumentNullException(nameof(aggregate));
        }

        public StableId RunStableId { get { return aggregate.RunStableId; } }
        public long LifecycleGeneration { get { return aggregate.LifecycleGeneration; } }
        public long AuthoritativeTick { get { return aggregate.AuthoritativeTick; } }
        public bool IsActive
        {
            get
            {
                return aggregate.LifecycleState
                    == RunSessionLifecycleStateV1.Active;
            }
        }

        public StableId PlayerActorStableId
        {
            get { return ExportPlayerSnapshot().ActorInstanceStableId; }
        }

        public StableId PlayerParticipantStableId
        {
            get { return ExportPlayerSnapshot().ParticipantStableId; }
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

            RunPickupCollectionCommandV1 command = fact.Command;
            if (command.RunStableId != aggregate.RunStableId)
            {
                return new RunPickupSessionRecordResultV1(
                    RunPickupSessionRecordStatusV1.WrongRun,
                    fact,
                    "run-pickup-session-wrong-run");
            }
            if (command.RunLifecycleGeneration != aggregate.LifecycleGeneration)
            {
                return new RunPickupSessionRecordResultV1(
                    RunPickupSessionRecordStatusV1.StaleLifecycle,
                    fact,
                    "run-pickup-session-lifecycle-mismatch");
            }
            if (!IsActive)
            {
                return new RunPickupSessionRecordResultV1(
                    RunPickupSessionRecordStatusV1.RunEnded,
                    fact,
                    "run-pickup-session-run-ended");
            }

            RunPlayerRuntimeSnapshotV1 player = ExportPlayerSnapshot();
            if (command.CollectorEntityStableId == null
                || command.CollectorParticipantStableId == null
                || command.CollectorEntityStableId
                    != player.ActorInstanceStableId
                || command.CollectorParticipantStableId
                    != player.ParticipantStableId)
            {
                return new RunPickupSessionRecordResultV1(
                    RunPickupSessionRecordStatusV1.UnauthorizedCollector,
                    fact,
                    "run-pickup-session-collector-unauthorized");
            }

            RunSessionFactAdmissionResultV1 admission = aggregate.AdmitFact(
                new RunSessionFactEnvelopeV1(
                    command.CollectionOperationStableId,
                    command.RunStableId,
                    command.RunLifecycleGeneration,
                    RunSessionFactKindV1.Contact,
                    fact.Fingerprint));
            if (admission == null)
            {
                return new RunPickupSessionRecordResultV1(
                    RunPickupSessionRecordStatusV1.Rejected,
                    fact,
                    "run-pickup-session-admission-null");
            }

            switch (admission.Status)
            {
                case RunSessionFactAdmissionStatusV1.Accepted:
                    return new RunPickupSessionRecordResultV1(
                        RunPickupSessionRecordStatusV1.Accepted,
                        fact,
                        string.Empty);
                case RunSessionFactAdmissionStatusV1.ExactReplay:
                    return new RunPickupSessionRecordResultV1(
                        RunPickupSessionRecordStatusV1.ExactReplay,
                        fact,
                        string.Empty);
                case RunSessionFactAdmissionStatusV1.WrongRun:
                    return new RunPickupSessionRecordResultV1(
                        RunPickupSessionRecordStatusV1.WrongRun,
                        fact,
                        admission.RejectionCode);
                case RunSessionFactAdmissionStatusV1.StaleLifecycle:
                    return new RunPickupSessionRecordResultV1(
                        RunPickupSessionRecordStatusV1.StaleLifecycle,
                        fact,
                        admission.RejectionCode);
                case RunSessionFactAdmissionStatusV1.RunEnded:
                    return new RunPickupSessionRecordResultV1(
                        RunPickupSessionRecordStatusV1.RunEnded,
                        fact,
                        admission.RejectionCode);
                case RunSessionFactAdmissionStatusV1.ConflictingDuplicate:
                    return new RunPickupSessionRecordResultV1(
                        RunPickupSessionRecordStatusV1.ConflictingDuplicate,
                        fact,
                        admission.RejectionCode);
                default:
                    return new RunPickupSessionRecordResultV1(
                        RunPickupSessionRecordStatusV1.Rejected,
                        fact,
                        "run-pickup-session-admission-status-invalid");
            }
        }

        private RunPlayerRuntimeSnapshotV1 ExportPlayerSnapshot()
        {
            RunPlayerRuntimeSnapshotV1 snapshot =
                aggregate.RuntimePorts.Player.ExportSnapshot();
            if (snapshot == null)
            {
                throw new InvalidOperationException(
                    "The Run Session player port returned no snapshot.");
            }
            return snapshot;
        }
    }

    /// <summary>
    /// Pickup-specific composition seam that avoids editing shared production composition
    /// while enemy attack work is active in parallel.
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
