using System;
using ShooterMover.Domain.Weapons;

namespace ShooterMover.Application.Weapons.Execution
{
    public sealed partial class WeaponFiringScheduler
    {
        private WeaponFiringDecision Release(
            WeaponFiringRequest request,
            WeaponFiringSessionState previousState,
            WeaponFiringTrackState track,
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

            WeaponFiringTrackState updatedTrack = track.WithTransition(
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
                WeaponFiringScheduleStatus.Released,
                string.Empty,
                "weapon-firing-release-state-invalid");
        }

        private WeaponFiringDecision Press(
            WeaponFiringRequest request,
            WeaponFiringSessionState previousState,
            WeaponFiringTrackState track,
            string effectiveFingerprint,
            string requestFingerprint,
            bool sameEffectiveProfile)
        {
            if (track.TriggerHeld)
            {
                return WeaponFiringDecision.Reject(
                    WeaponFiringScheduleStatus.TriggerTransitionRejected,
                    previousState,
                    "weapon-firing-trigger-already-held");
            }

            bool supportsHolding = request.Weapon.FireSettings.Mode == WeaponFireMode.Automatic
                || request.Weapon.FireSettings.Mode == WeaponFireMode.Continuous;
            if (track.HasCadencePhase
                && request.Command.SimulationTick < track.NextCadenceTick)
            {
                if (!supportsHolding)
                {
                    return WeaponFiringDecision.Reject(
                        WeaponFiringScheduleStatus.CooldownActive,
                        previousState,
                        "weapon-firing-cadence-active");
                }

                long originTick = track.CadenceOriginTick;
                long nextOrdinal = track.NextCadenceOrdinal;
                if (!sameEffectiveProfile)
                {
                    originTick = track.NextCadenceTick;
                    nextOrdinal = 0L;
                }

                WeaponFiringTrackState waitingTrack = track.WithTransition(
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
                    WeaponFiringScheduleStatus.WaitingForCadence,
                    string.Empty,
                    "weapon-firing-waiting-state-invalid");
            }

            AcceptedSchedule schedule;
            long nextCadenceOrdinal;
            long nextCadenceTick;
            long nextShotSequence;
            WeaponFiringScheduleStatus status;
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
                return WeaponFiringDecision.Reject(status, previousState, code);
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

        private WeaponFiringDecision Hold(
            WeaponFiringRequest request,
            WeaponFiringSessionState previousState,
            WeaponFiringTrackState track,
            string effectiveFingerprint,
            string requestFingerprint,
            bool sameEffectiveProfile)
        {
            bool supportsHolding = request.Weapon.FireSettings.Mode == WeaponFireMode.Automatic
                || request.Weapon.FireSettings.Mode == WeaponFireMode.Continuous;
            if (!supportsHolding)
            {
                return WeaponFiringDecision.Reject(
                    WeaponFiringScheduleStatus.TriggerTransitionRejected,
                    previousState,
                    "weapon-firing-held-signal-unsupported-for-mode");
            }
            if (!track.TriggerHeld)
            {
                return WeaponFiringDecision.Reject(
                    WeaponFiringScheduleStatus.TriggerTransitionRejected,
                    previousState,
                    "weapon-firing-held-without-press");
            }
            if (!sameEffectiveProfile)
            {
                return WeaponFiringDecision.Reject(
                    WeaponFiringScheduleStatus.IdentityMismatch,
                    previousState,
                    "weapon-firing-effective-profile-changed-while-trigger-held");
            }
            if (!track.HasCadencePhase)
            {
                return WeaponFiringDecision.Reject(
                    WeaponFiringScheduleStatus.InvalidState,
                    previousState,
                    "weapon-firing-held-without-cadence-phase");
            }

            if (request.Command.SimulationTick < track.NextCadenceTick)
            {
                WeaponFiringTrackState waitingTrack = track.WithTransition(
                    track.EffectiveWeaponFingerprint,
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
                    WeaponFiringScheduleStatus.WaitingForCadence,
                    string.Empty,
                    "weapon-firing-waiting-state-invalid");
            }

            AcceptedSchedule schedule;
            long nextCadenceOrdinal;
            long nextCadenceTick;
            long nextShotSequence;
            bool noEmissionDue;
            WeaponFiringScheduleStatus status;
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
                return WeaponFiringDecision.Reject(status, previousState, code);
            }

            if (noEmissionDue)
            {
                WeaponFiringTrackState waitingTrack = track.WithTransition(
                    track.EffectiveWeaponFingerprint,
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
                    WeaponFiringScheduleStatus.WaitingForCadence,
                    string.Empty,
                    "weapon-firing-waiting-state-invalid");
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

        private WeaponFiringDecision Accept(
            WeaponFiringRequest request,
            WeaponFiringSessionState previousState,
            WeaponFiringTrackState previousTrack,
            string effectiveFingerprint,
            string requestFingerprint,
            AcceptedSchedule schedule,
            long nextCadenceOrdinal,
            long nextCadenceTick,
            long nextShotSequence)
        {
            long expectedNextShotSequence;
            if (schedule == null
                || !schedule.HasValidFingerprint(request.Weapon)
                || !ScheduleMatchesAuthoredTiming(request.Weapon, schedule)
                || schedule.FirstShotSequence != previousTrack.NextGlobalShotSequence
                || !TryAdd(schedule.LastShotSequence, 1L, out expectedNextShotSequence)
                || expectedNextShotSequence != nextShotSequence
                || schedule.NextCadenceOrdinal != nextCadenceOrdinal
                || schedule.NextCadenceTick != nextCadenceTick)
            {
                return WeaponFiringDecision.Reject(
                    WeaponFiringScheduleStatus.NumericalFailure,
                    previousState,
                    "weapon-firing-state-transition-plan-mismatch");
            }

            long operationSequence = previousTrack.NextOperationSequence;
            long nextOperationSequence;
            if (!TryAdd(operationSequence, 1L, out nextOperationSequence))
            {
                return WeaponFiringDecision.Reject(
                    WeaponFiringScheduleStatus.NumericalFailure,
                    previousState,
                    "weapon-firing-operation-sequence-overflow");
            }

            WeaponFiringTrackState updatedTrack = previousTrack.WithTransition(
                effectiveFingerprint,
                true,
                true,
                schedule.CadenceOriginTick,
                nextCadenceOrdinal,
                nextCadenceTick,
                nextShotSequence,
                request.Command.SimulationTick)
                .WithNextOperationSequence(nextOperationSequence);
            WeaponFiringReplayRecord replay = new WeaponFiringReplayRecord(
                updatedTrack.ActorId,
                updatedTrack.ParticipantId,
                updatedTrack.EquipmentInstanceId,
                updatedTrack.WeaponDefinitionId,
                updatedTrack.LifecycleGeneration,
                request.Command.FireOperationId,
                requestFingerprint,
                effectiveFingerprint,
                operationSequence,
                schedule);
            WeaponFiringSessionState nextState;
            if (!TryApplyTransition(
                previousState,
                updatedTrack,
                replay,
                out nextState))
            {
                return WeaponFiringDecision.Reject(
                    WeaponFiringScheduleStatus.NumericalFailure,
                    previousState,
                    "weapon-firing-accepted-state-invalid");
            }

            return WeaponFiringDecision.Accept(schedule, nextState);
        }

        private WeaponFiringDecision CompleteTransition(
            WeaponFiringRequest request,
            WeaponFiringSessionState previousState,
            WeaponFiringTrackState updatedTrack,
            string requestFingerprint,
            string effectiveFingerprint,
            WeaponFiringScheduleStatus successfulStatus,
            string resultCode,
            string invalidStateCode)
        {
            long operationSequence = updatedTrack.NextOperationSequence;
            long nextOperationSequence;
            if (!TryAdd(operationSequence, 1L, out nextOperationSequence))
            {
                return WeaponFiringDecision.Reject(
                    WeaponFiringScheduleStatus.NumericalFailure,
                    previousState,
                    "weapon-firing-operation-sequence-overflow");
            }
            updatedTrack = updatedTrack.WithNextOperationSequence(nextOperationSequence);

            WeaponFiringReplayRecord replay = new WeaponFiringReplayRecord(
                updatedTrack.ActorId,
                updatedTrack.ParticipantId,
                updatedTrack.EquipmentInstanceId,
                updatedTrack.WeaponDefinitionId,
                updatedTrack.LifecycleGeneration,
                request.Command.FireOperationId,
                requestFingerprint,
                effectiveFingerprint,
                operationSequence,
                successfulStatus,
                resultCode,
                request.Command.SimulationTick,
                updatedTrack.Fingerprint);
            WeaponFiringSessionState nextState;
            if (!TryApplyTransition(previousState, updatedTrack, replay, out nextState))
            {
                return WeaponFiringDecision.Reject(
                    WeaponFiringScheduleStatus.NumericalFailure,
                    previousState,
                    invalidStateCode);
            }

            return WeaponFiringDecision.Transition(
                successfulStatus,
                nextState,
                resultCode);
        }

        private bool TryValidateTrackTiming(
            WeaponFiringTrackState track,
            EffectiveWeapon weapon,
            string effectiveFingerprint)
        {
            if (!track.HasCadencePhase
                || !string.Equals(
                    track.EffectiveWeaponFingerprint,
                    effectiveFingerprint,
                    StringComparison.Ordinal))
            {
                return true;
            }

            long expectedTick;
            return TryComputeCadenceTick(
                    weapon,
                    track.CadenceOriginTick,
                    track.NextCadenceOrdinal,
                    out expectedTick)
                && expectedTick == track.NextCadenceTick;
        }

        private bool TryApplyTransition(
            WeaponFiringSessionState previousState,
            WeaponFiringTrackState updatedTrack,
            WeaponFiringReplayRecord replay,
            out WeaponFiringSessionState nextState)
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
