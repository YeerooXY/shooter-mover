using System;

namespace ShooterMover.RunPickups
{
    public sealed partial class RunLocalPickupAuthorityV1
    {
        public RunPickupCollectionResultV1 Collect(
            RunPickupCollectionCommandV1 command)
        {
            if (command == null)
            {
                return new RunPickupCollectionResultV1(
                    RunPickupCollectionStatusV1.Rejected,
                    null,
                    null,
                    null,
                    "run-pickup-collection-command-null");
            }

            lock (gate)
            {
                if (command.RunStableId != runSession.RunStableId)
                {
                    return RejectedCollection(
                        RunPickupCollectionStatusV1.WrongRun,
                        command,
                        null,
                        "run-pickup-collection-wrong-run");
                }
                if (command.RunLifecycleGeneration != runSession.LifecycleGeneration)
                {
                    return RejectedCollection(
                        RunPickupCollectionStatusV1.StaleLifecycle,
                        command,
                        null,
                        command.RunLifecycleGeneration < runSession.LifecycleGeneration
                            ? "run-pickup-collection-stale-generation"
                            : "run-pickup-collection-future-generation");
                }
                CollectionReplayRecord replay;
                if (collectionReplay.TryGetValue(
                    command.CollectionOperationStableId,
                    out replay))
                {
                    if (string.Equals(
                        replay.CommandFingerprint,
                        command.Fingerprint,
                        StringComparison.Ordinal))
                    {
                        return new RunPickupCollectionResultV1(
                            RunPickupCollectionStatusV1.ExactReplay,
                            command,
                            replay.Result.Pickup,
                            replay.Result.CollectionFact,
                            string.Empty);
                    }
                    return new RunPickupCollectionResultV1(
                        RunPickupCollectionStatusV1.ConflictingDuplicate,
                        command,
                        replay.Result.Pickup,
                        replay.Result.CollectionFact,
                        "run-pickup-collection-operation-conflict");
                }

                if (!runSession.IsActive)
                {
                    return RejectedCollection(
                        RunPickupCollectionStatusV1.Rejected,
                        command,
                        null,
                        "run-pickup-collection-run-ended");
                }

                RunPickupSnapshotV1 pickup;
                if (!byPickup.TryGetValue(command.PickupStableId, out pickup)
                    || !IsCurrentLifecycle(pickup))
                {
                    return RejectedCollection(
                        RunPickupCollectionStatusV1.PickupUnavailable,
                        command,
                        null,
                        "run-pickup-collection-pickup-missing");
                }
                if (pickup.Reward.RewardInstanceStableId
                    != command.GeneratedRewardChildStableId)
                {
                    return RejectedCollection(
                        RunPickupCollectionStatusV1.WrongPickupChildPairing,
                        command,
                        pickup,
                        "run-pickup-collection-child-pairing-mismatch");
                }
                if (command.CollectorEntityStableId == null
                    || command.CollectorParticipantStableId == null
                    || command.CollectorEntityStableId
                        != runSession.PlayerActorStableId
                    || command.CollectorParticipantStableId
                        != runSession.PlayerParticipantStableId)
                {
                    return RejectedCollection(
                        RunPickupCollectionStatusV1.UnauthorizedCollector,
                        command,
                        pickup,
                        "run-pickup-collection-collector-unauthorized");
                }
                if (string.IsNullOrWhiteSpace(command.ExpectedPickupFingerprint)
                    || !string.Equals(
                        command.ExpectedPickupFingerprint,
                        pickup.Fingerprint,
                        StringComparison.Ordinal))
                {
                    return RejectedCollection(
                        RunPickupCollectionStatusV1.FingerprintMismatch,
                        command,
                        pickup,
                        "run-pickup-collection-fingerprint-mismatch");
                }
                if (pickup.State != RunPickupStateV1.Available)
                {
                    return RejectedCollection(
                        RunPickupCollectionStatusV1.PickupUnavailable,
                        command,
                        pickup,
                        "run-pickup-collection-not-available:" + pickup.State);
                }

                long nextSequence = checked(collectionSequence + 1L);
                long tick = runSession.AuthoritativeTick;
                var collectionFact = new RunPickupCollectionFactV1(
                    pickup,
                    command,
                    nextSequence,
                    tick);

                RunPickupSessionRecordResultV1 sessionResult;
                try
                {
                    sessionResult = runSession.RecordCollection(collectionFact);
                }
                catch (Exception exception)
                {
                    return RejectedCollection(
                        RunPickupCollectionStatusV1.Rejected,
                        command,
                        pickup,
                        "run-pickup-session-record-exception:" + exception.Message);
                }

                if (sessionResult == null || !sessionResult.IsAccepted)
                {
                    return RejectedCollection(
                        MapSessionRejection(sessionResult),
                        command,
                        pickup,
                        sessionResult == null
                            ? "run-pickup-session-record-null"
                            : sessionResult.Diagnostic);
                }

                RunPickupSnapshotV1 collected = pickup.WithCollected(
                    command.CollectorEntityStableId,
                    command.CollectorParticipantStableId,
                    command.CollectionOperationStableId,
                    nextSequence,
                    tick);
                byPickup[command.PickupStableId] = collected;
                collectionSequence = nextSequence;

                var accepted = new RunPickupCollectionResultV1(
                    RunPickupCollectionStatusV1.Collected,
                    command,
                    collected,
                    collectionFact,
                    string.Empty);
                collectionReplay.Add(
                    command.CollectionOperationStableId,
                    new CollectionReplayRecord(command.Fingerprint, accepted));
                return accepted;
            }
        }
    }
}
