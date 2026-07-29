using System;
using ShooterMover.Domain.Guns;

namespace ShooterMover.Application.Guns.Execution
{
    public sealed partial class GunFiringScheduler
    {
        private GunFiringDecision Release(
            GunFiringRequest request,
            GunFiringSessionState previousState,
            GunFiringTrackState track,
            string effectiveFingerprint,
            string requestFingerprint,
            bool sameEffectiveProfile)
        {
            bool hasPhase = track.HasCadencePhase;
            long originTick = track.CadenceOriginTick;
            long nextOrdinal = track.NextCadenceOrdinal;
            long nextTick = track.NextCadenceTick;
            if (!sameEffectiveProfile)
            {
                if (hasPhase && request.Command.SimulationTick < nextTick)
                {
                    originTick = nextTick;
                    nextOrdinal = 0L;
                }
                else
                {
                    hasPhase = false;
                    originTick = -1L;
                    nextOrdinal = 0L;
                    nextTick = -1L;
                }
            }

            GunFiringTrackState updatedTrack = track.WithTransition(
                effectiveFingerprint,
                false,
                hasPhase,
                originTick,
                nextOrdinal,
                nextTick,
                track.NextGlobalShotSequence,
                request.Command.SimulationTick);
            return CompleteTransition(
                request,
                previousState,
                updatedTrack,
                requestFingerprint,
                effectiveFingerprint,
                GunFiringScheduleStatus.Released,
                string.Empty,
                "gun-firing-release-state-invalid");
        }

        private GunFiringDecision Press(
            GunFiringRequest request,
            GunFiringSessionState previousState,
            GunFiringTrackState track,
            string effectiveFingerprint,
            string requestFingerprint,
            bool sameEffectiveProfile)
        {
            if (track.TriggerHeld)
            {
                return GunFiringDecision.Reject(
                    GunFiringScheduleStatus.TriggerTransitionRejected,
                    previousState,
                    "gun-firing-trigger-already-held");
            }

            bool supportsHolding = request.Gun.FireSettings.Mode == GunFireMode.Automatic
                || request.Gun.FireSettings.Mode == GunFireMode.Continuous;
            if (track.HasCadencePhase
                && request.Command.SimulationTick < track.NextCadenceTick)
            {
                if (!supportsHolding)
                {
                    return GunFiringDecision.Reject(
                        GunFiringScheduleStatus.CooldownActive,
                        previousState,
                        "gun-firing-cadence-active");
                }

                long originTick = track.CadenceOriginTick;
                long nextOrdinal = track.NextCadenceOrdinal;
                if (!sameEffectiveProfile)
                {
                    originTick = track.NextCadenceTick;
                    nextOrdinal = 0L;
                }

                GunFiringTrackState waitingTrack = track.WithTransition(
                    effectiveFingerprint,
                    true,
                    true,
                    originTick,
                    nextOrdinal,
                    track.NextCadenceTick,
                    track.NextGlobalShotSequence,
                    request.Command.SimulationTick);
                return CompleteTransition(
                    request,
                    previousState,
                    waitingTrack,
                    requestFingerprint,
                    effectiveFingerprint,
                    GunFiringScheduleStatus.WaitingForCadence,
                    string.Empty,
                    "gun-firing-waiting-state-invalid");
            }

            AcceptedSchedule schedule;
            long nextCadenceOrdinal;
            long nextCadenceTick;
            long nextShotSequence;
            GunFiringScheduleStatus status;
            string code;
            if (!TryBuildPressedSchedule(
                request,
                effectiveFingerprint,
                requestFingerprint,
                track.NextGlobalShotSequence,
                out schedule,
                out nextCadenceOrdinal,
                out nextCadenceTick,
                out nextShotSequence,
                out status,
                out code))
            {
                return GunFiringDecision.Reject(status, previousState, code);
            }

            return Accept(
                request,
                previousState,
                track,
                effectiveFingerprint,
                requestFingerprint,
                schedule,
                nextCadenceOrdinal,
                nextCadenceTick,
                nextShotSequence);
        }

        private GunFiringDecision Hold(
            GunFiringRequest request,
            GunFiringSessionState previousState,
            GunFiringTrackState track,
            string effectiveFingerprint,
            string requestFingerprint,
            bool sameEffectiveProfile)
        {
            bool supportsHolding = request.Gun.FireSettings.Mode == GunFireMode.Automatic
                || request.Gun.FireSettings.Mode == GunFireMode.Continuous;
            if (!supportsHolding)
            {
                return GunFiringDecision.Reject(
                    GunFiringScheduleStatus.TriggerTransitionRejected,
                    previousState,
                    "gun-firing-held-signal-unsupported-for-mode");
            }
            if (!track.TriggerHeld)
            {
                return GunFiringDecision.Reject(
                    GunFiringScheduleStatus.TriggerTransitionRejected,
                    previousState,
                    "gun-firing-held-without-press");
            }
            if (!sameEffectiveProfile)
            {
                return GunFiringDecision.Reject(
                    GunFiringScheduleStatus.IdentityMismatch,
                    previousState,
                    "gun-firing-effective-profile-changed-while-trigger-held");
            }
            if (!track.HasCadencePhase)
            {
                return GunFiringDecision.Reject(
                    GunFiringScheduleStatus.InvalidState,
                    previousState,
                    "gun-firing-held-without-cadence-phase");
            }

            if (request.Command.SimulationTick < track.NextCadenceTick)
            {
                GunFiringTrackState waitingTrack = track.WithTransition(
                    track.EffectiveGunFingerprint,
                    true,
                    true,
                    track.CadenceOriginTick,
                    track.NextCadenceOrdinal,
                    track.NextCadenceTick,
                    track.NextGlobalShotSequence,
                    request.Command.SimulationTick);
                return CompleteTransition(
                    request,
                    previousState,
                    waitingTrack,
                    requestFingerprint,
                    effectiveFingerprint,
                    GunFiringScheduleStatus.WaitingForCadence,
                    string.Empty,
                    "gun-firing-waiting-state-invalid");
            }

            AcceptedSchedule schedule;
            long nextCadenceOrdinal;
            long nextCadenceTick;
            long nextShotSequence;
            bool noEmissionDue;
            GunFiringScheduleStatus status;
            string code;
            if (!TryBuildHeldCatchUpSchedule(
                request,
                track,
                effectiveFingerprint,
                requestFingerprint,
                out schedule,
                out nextCadenceOrdinal,
                out nextCadenceTick,
                out nextShotSequence,
                out noEmissionDue,
                out status,
                out code))
            {
                return GunFiringDecision.Reject(status, previousState, code);
            }

            if (noEmissionDue)
            {
                GunFiringTrackState waitingTrack = track.WithTransition(
                    track.EffectiveGunFingerprint,
                    true,
                    true,
                    track.CadenceOriginTick,
                    nextCadenceOrdinal,
                    nextCadenceTick,
                    track.NextGlobalShotSequence,
                    request.Command.SimulationTick);
                return CompleteTransition(
                    request,
                    previousState,
                    waitingTrack,
                    requestFingerprint,
                    effectiveFingerprint,
                    GunFiringScheduleStatus.WaitingForCadence,
                    string.Empty,
                    "gun-firing-waiting-state-invalid");
            }

            return Accept(
                request,
                previousState,
                track,
                effectiveFingerprint,
                requestFingerprint,
                schedule,
                nextCadenceOrdinal,
                nextCadenceTick,
                nextShotSequence);
        }

        private GunFiringDecision Accept(
            GunFiringRequest request,
            GunFiringSessionState previousState,
            GunFiringTrackState previousTrack,
            string effectiveFingerprint,
            string requestFingerprint,
            AcceptedSchedule schedule,
            long nextCadenceOrdinal,
            long nextCadenceTick,
            long nextShotSequence)
        {
            long expectedNextShotSequence;
            if (schedule == null
                || !schedule.HasValidFingerprint(request.Gun)
                || !ScheduleMatchesAuthoredTiming(request.Gun, schedule)
                || schedule.FirstShotSequence != previousTrack.NextGlobalShotSequence
                || !TryAdd(schedule.LastShotSequence, 1L, out expectedNextShotSequence)
                || expectedNextShotSequence != nextShotSequence
                || schedule.NextCadenceOrdinal != nextCadenceOrdinal
                || schedule.NextCadenceTick != nextCadenceTick)
            {
                return GunFiringDecision.Reject(
                    GunFiringScheduleStatus.NumericalFailure,
                    previousState,
                    "gun-firing-state-transition-plan-mismatch");
            }

            long operationSequence = previousTrack.NextOperationSequence;
            long nextOperationSequence;
            if (!TryAdd(operationSequence, 1L, out nextOperationSequence))
            {
                return GunFiringDecision.Reject(
                    GunFiringScheduleStatus.NumericalFailure,
                    previousState,
                    "gun-firing-operation-sequence-overflow");
            }

            GunFiringTrackState updatedTrack = previousTrack.WithTransition(
                effectiveFingerprint,
                true,
                true,
                schedule.CadenceOriginTick,
                nextCadenceOrdinal,
                nextCadenceTick,
                nextShotSequence,
                request.Command.SimulationTick)
                .WithNextOperationSequence(nextOperationSequence);
            GunFiringReplayRecord replay = new GunFiringReplayRecord(
                updatedTrack.ActorId,
                updatedTrack.ParticipantId,
                updatedTrack.EquipmentInstanceId,
                updatedTrack.GunDefinitionId,
                updatedTrack.LifecycleGeneration,
                request.Command.FireOperationId,
                requestFingerprint,
                effectiveFingerprint,
                operationSequence,
                schedule);
            GunFiringSessionState nextState;
            if (!TryApplyTransition(
                previousState,
                updatedTrack,
                replay,
                out nextState))
            {
                return GunFiringDecision.Reject(
                    GunFiringScheduleStatus.NumericalFailure,
                    previousState,
                    "gun-firing-accepted-state-invalid");
            }

            return GunFiringDecision.Accept(schedule, nextState);
        }

        private GunFiringDecision CompleteTransition(
            GunFiringRequest request,
            GunFiringSessionState previousState,
            GunFiringTrackState updatedTrack,
            string requestFingerprint,
            string effectiveFingerprint,
            GunFiringScheduleStatus successfulStatus,
            string resultCode,
            string invalidStateCode)
        {
            long operationSequence = updatedTrack.NextOperationSequence;
            long nextOperationSequence;
            if (!TryAdd(operationSequence, 1L, out nextOperationSequence))
            {
                return GunFiringDecision.Reject(
                    GunFiringScheduleStatus.NumericalFailure,
                    previousState,
                    "gun-firing-operation-sequence-overflow");
            }
            updatedTrack = updatedTrack.WithNextOperationSequence(nextOperationSequence);

            GunFiringReplayRecord replay = new GunFiringReplayRecord(
                updatedTrack.ActorId,
                updatedTrack.ParticipantId,
                updatedTrack.EquipmentInstanceId,
                updatedTrack.GunDefinitionId,
                updatedTrack.LifecycleGeneration,
                request.Command.FireOperationId,
                requestFingerprint,
                effectiveFingerprint,
                operationSequence,
                successfulStatus,
                resultCode,
                request.Command.SimulationTick,
                updatedTrack.Fingerprint);
            GunFiringSessionState nextState;
            if (!TryApplyTransition(previousState, updatedTrack, replay, out nextState))
            {
                return GunFiringDecision.Reject(
                    GunFiringScheduleStatus.NumericalFailure,
                    previousState,
                    invalidStateCode);
            }

            return GunFiringDecision.Transition(
                successfulStatus,
                nextState,
                resultCode);
        }

        private bool TryValidateTrackTiming(
            GunFiringTrackState track,
            EffectiveGun gun,
            string effectiveFingerprint)
        {
            if (!track.HasCadencePhase
                || !string.Equals(
                    track.EffectiveGunFingerprint,
                    effectiveFingerprint,
                    StringComparison.Ordinal))
            {
                return true;
            }

            long expectedTick;
            return TryComputeCadenceTick(
                    gun,
                    track.CadenceOriginTick,
                    track.NextCadenceOrdinal,
                    out expectedTick)
                && expectedTick == track.NextCadenceTick;
        }

        private bool TryApplyTransition(
            GunFiringSessionState previousState,
            GunFiringTrackState updatedTrack,
            GunFiringReplayRecord replay,
            out GunFiringSessionState nextState)
        {
            nextState = null;
            try
            {
                nextState = previousState.WithTransition(
                    clock.TicksPerSecond,
                    replayRetentionCapacity,
                    updatedTrack,
                    replay);
                return nextState != null && nextState.HasValidFingerprint();
            }
            catch (ArgumentException)
            {
                nextState = null;
                return false;
            }
            catch (InvalidOperationException)
            {
                nextState = null;
                return false;
            }
        }
    }
}
