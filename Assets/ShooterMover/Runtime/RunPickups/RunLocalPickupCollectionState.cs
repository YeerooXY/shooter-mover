using System;

namespace ShooterMover.RunPickups
{
    public sealed partial class RunLocalPickupState
    {
        public RunPickupCollectionResult Collect(
            RunPickupCollectionCommand command)
        {
            if (command == null)
            {
                return new RunPickupCollectionResult(
                    RunPickupCollectionStatus.Rejected,
                    null,
                    null,
                    null,
                    "run-pickup-collection-command-null");
            }

            lock (gate)
            {
                RunPickupRunSessionContext sessionContext;
                string sessionDiagnostic;
                if (!TryReadRunSessionContext(
                    out sessionContext,
                    out sessionDiagnostic))
                {
                    return RejectedCollection(
                        RunPickupCollectionStatus.Rejected,
                        command,
                        null,
                        sessionDiagnostic);
                }

                if (command.RunStableId != sessionContext.RunStableId)
                {
                    return RejectedCollection(
                        RunPickupCollectionStatus.WrongRun,
                        command,
                        null,
                        "run-pickup-collection-wrong-run");
                }
                if (command.RunLifecycleGeneration
                    != sessionContext.LifecycleGeneration)
                {
                    return RejectedCollection(
                        RunPickupCollectionStatus.StaleLifecycle,
                        command,
                        null,
                        command.RunLifecycleGeneration
                            < sessionContext.LifecycleGeneration
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
                        return new RunPickupCollectionResult(
                            RunPickupCollectionStatus.ExactReplay,
                            command,
                            replay.Result.Pickup,
                            replay.Result.CollectionFact,
                            string.Empty);
                    }
                    return new RunPickupCollectionResult(
                        RunPickupCollectionStatus.ConflictingDuplicate,
                        command,
                        replay.Result.Pickup,
                        replay.Result.CollectionFact,
                        "run-pickup-collection-operation-conflict");
                }

                if (!sessionContext.IsActive)
                {
                    return RejectedCollection(
                        RunPickupCollectionStatus.Rejected,
                        command,
                        null,
                        "run-pickup-collection-run-ended");
                }

                RunPickupSnapshot pickup;
                if (!byPickup.TryGetValue(command.PickupStableId, out pickup)
                    || pickup.Batch.RunStableId != sessionContext.RunStableId
                    || pickup.Batch.RunLifecycleGeneration
                        != sessionContext.LifecycleGeneration)
                {
                    return RejectedCollection(
                        RunPickupCollectionStatus.PickupUnavailable,
                        command,
                        null,
                        "run-pickup-collection-pickup-missing");
                }
                if (pickup.Reward.RewardInstanceStableId
                    != command.GeneratedRewardChildStableId)
                {
                    return RejectedCollection(
                        RunPickupCollectionStatus.WrongPickupChildPairing,
                        command,
                        pickup,
                        "run-pickup-collection-child-pairing-mismatch");
                }
                if (command.CollectorEntityStableId == null
                    || command.CollectorParticipantStableId == null
                    || command.CollectorEntityStableId
                        != sessionContext.PlayerActorStableId
                    || command.CollectorParticipantStableId
                        != sessionContext.PlayerParticipantStableId)
                {
                    return RejectedCollection(
                        RunPickupCollectionStatus.UnauthorizedCollector,
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
                        RunPickupCollectionStatus.FingerprintMismatch,
                        command,
                        pickup,
                        "run-pickup-collection-fingerprint-mismatch");
                }
                if (pickup.State != RunPickupState.Available)
                {
                    return RejectedCollection(
                        RunPickupCollectionStatus.PickupUnavailable,
                        command,
                        pickup,
                        "run-pickup-collection-not-available:" + pickup.State);
                }

                long nextSequence = sessionContext.NextCollectionOrder;
                long tick = sessionContext.AuthoritativeTick;
                var collectionFact = new RunPickupCollectionFact(
                    pickup,
                    command,
                    nextSequence,
                    tick);

                RunPickupSessionRecordResult sessionResult;
                try
                {
                    sessionResult = runSession.RecordCollection(collectionFact);
                }
                catch (Exception exception)
                {
                    return RejectedCollection(
                        RunPickupCollectionStatus.Rejected,
                        command,
                        pickup,
                        "run-pickup-session-record-exception:"
                        + exception.GetType().Name
                        + ":"
                        + exception.Message);
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

                RunPickupSnapshot collected = pickup.WithCollected(
                    command.CollectorEntityStableId,
                    command.CollectorParticipantStableId,
                    command.CollectionOperationStableId,
                    nextSequence,
                    tick);
                byPickup[command.PickupStableId] = collected;

                var accepted = new RunPickupCollectionResult(
                    RunPickupCollectionStatus.Collected,
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
