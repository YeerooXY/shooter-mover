using System;
using ShooterMover.Domain.Weapons;

namespace ShooterMover.Application.Weapons.Execution
{
    /// <summary>
    /// Canonical deterministic firing state transition. The scheduler retains only immutable
    /// configuration; every gameplay state and bounded replay record is caller-owned.
    /// </summary>
    public sealed partial class WeaponFiringScheduler
    {
        public const int MaximumEmissionsPerSchedule = 4096;
        public const int DefaultReplayRetentionCapacity = 256;

        private readonly WeaponFiringClock clock;
        private readonly int replayRetentionCapacity;

        public WeaponFiringScheduler(WeaponFiringClock clock)
            : this(clock, DefaultReplayRetentionCapacity)
        {
        }

        public WeaponFiringScheduler(
            WeaponFiringClock clock,
            int replayRetentionCapacity)
        {
            this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
            if (replayRetentionCapacity < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(replayRetentionCapacity));
            }

            this.replayRetentionCapacity = replayRetentionCapacity;
        }

        public WeaponFiringClock Clock { get { return clock; } }
        public int ReplayRetentionCapacity { get { return replayRetentionCapacity; } }

        public WeaponFiringDecision Schedule(
            WeaponFiringRequest request,
            WeaponFiringSessionState previousState)
        {
            if (previousState == null || !previousState.HasValidFingerprint())
            {
                return WeaponFiringDecision.Reject(
                    WeaponFiringScheduleStatus.InvalidState,
                    previousState,
                    "weapon-firing-state-invalid");
            }
            if (previousState.ClockTicksPerSecond != 0
                && previousState.ClockTicksPerSecond != clock.TicksPerSecond)
            {
                return WeaponFiringDecision.Reject(
                    WeaponFiringScheduleStatus.InvalidState,
                    previousState,
                    "weapon-firing-state-clock-mismatch");
            }

            string requestValidationCode;
            if (!TryValidateRequest(request, out requestValidationCode))
            {
                return WeaponFiringDecision.Reject(
                    WeaponFiringScheduleStatus.InvalidRequest,
                    previousState,
                    requestValidationCode);
            }

            EffectiveWeapon weapon = request.Weapon;
            string effectiveFingerprint = EffectiveWeaponFiringFingerprint.Compute(weapon);
            string requestFingerprint = RequestFingerprint(request, effectiveFingerprint);

            WeaponFiringReplayRecord replay;
            if (previousState.TryFindReplay(
                request.Command.ActorId,
                request.Command.EquipmentInstanceId,
                request.Command.LifecycleGeneration,
                request.Command.FireOperationId,
                out replay))
            {
                if (!IsExactReplay(replay, requestFingerprint, effectiveFingerprint))
                {
                    return WeaponFiringDecision.Reject(
                        WeaponFiringScheduleStatus.ConflictingDuplicate,
                        previousState,
                        "weapon-firing-operation-conflicting-duplicate");
                }
                if (!replay.HasValidFingerprint())
                {
                    return WeaponFiringDecision.Reject(
                        WeaponFiringScheduleStatus.InvalidState,
                        previousState,
                        "weapon-firing-replay-record-invalid");
                }

                if (replay.HasAcceptedSchedule)
                {
                    if (!replay.AcceptedSchedule.HasValidFingerprint(weapon)
                        || !ScheduleMatchesAuthoredTiming(weapon, replay.AcceptedSchedule))
                    {
                        return WeaponFiringDecision.Reject(
                            WeaponFiringScheduleStatus.InvalidState,
                            previousState,
                            "weapon-firing-replay-record-invalid");
                    }

                    return WeaponFiringDecision.Replay(
                        replay.AcceptedSchedule,
                        previousState);
                }

                return WeaponFiringDecision.ReplayTransition(
                    replay.SuccessfulStatus,
                    previousState,
                    replay.ResultCode);
            }

            WeaponFiringTrackState track;
            if (!previousState.TryFindTrack(
                request.Command.ActorId,
                request.ParticipantId,
                request.Command.EquipmentInstanceId,
                weapon.DefinitionId,
                request.Command.LifecycleGeneration,
                out track))
            {
                WeaponFiringTrackState conflictingTrack;
                if (previousState.TryFindEquipmentLifecycleTrack(
                    request.Command.ActorId,
                    request.Command.EquipmentInstanceId,
                    request.Command.LifecycleGeneration,
                    out conflictingTrack))
                {
                    return WeaponFiringDecision.Reject(
                        WeaponFiringScheduleStatus.IdentityMismatch,
                        previousState,
                        "weapon-firing-state-identity-mismatch");
                }

                track = new WeaponFiringTrackState(
                    request.Command.ActorId,
                    request.ParticipantId,
                    request.Command.EquipmentInstanceId,
                    weapon.DefinitionId,
                    request.Command.LifecycleGeneration,
                    effectiveFingerprint,
                    false,
                    false,
                    -1L,
                    0L,
                    -1L,
                    0L,
                    -1L);
            }
            else if (!TryValidateTrackTiming(track, weapon, effectiveFingerprint))
            {
                return WeaponFiringDecision.Reject(
                    WeaponFiringScheduleStatus.InvalidState,
                    previousState,
                    "weapon-firing-track-timing-invalid");
            }

            if (track.IsReplayExpired(request.Command.SimulationTick))
            {
                return WeaponFiringDecision.Reject(
                    WeaponFiringScheduleStatus.ReplayExpired,
                    previousState,
                    "weapon-firing-replay-expired");
            }

            if (request.Command.SimulationTick < track.LastObservedSimulationTick)
            {
                return WeaponFiringDecision.Reject(
                    WeaponFiringScheduleStatus.TimeReversal,
                    previousState,
                    "weapon-firing-time-reversal");
            }

            bool sameEffectiveProfile = string.Equals(
                track.EffectiveWeaponFingerprint,
                effectiveFingerprint,
                StringComparison.Ordinal);
            if (track.TriggerHeld && !sameEffectiveProfile)
            {
                return WeaponFiringDecision.Reject(
                    WeaponFiringScheduleStatus.IdentityMismatch,
                    previousState,
                    "weapon-firing-effective-profile-changed-while-trigger-held");
            }

            if (request.TriggerSignal == WeaponTriggerSignal.Released)
            {
                return Release(request, previousState, track,
                    effectiveFingerprint, requestFingerprint, sameEffectiveProfile);
            }

            if (request.TriggerSignal == WeaponTriggerSignal.Pressed)
            {
                return Press(request, previousState, track,
                    effectiveFingerprint, requestFingerprint,
                    sameEffectiveProfile);
            }

            return Hold(request, previousState, track,
                effectiveFingerprint, requestFingerprint,
                sameEffectiveProfile);
        }
    }
}
