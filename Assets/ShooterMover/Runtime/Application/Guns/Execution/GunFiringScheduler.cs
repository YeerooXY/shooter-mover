using System;
using ShooterMover.Domain.Guns;

namespace ShooterMover.Application.Guns.Execution
{
    /// <summary>
    /// Canonical deterministic firing state transition. The scheduler retains only immutable
    /// configuration; every gameplay state and bounded replay record is caller-owned.
    /// </summary>
    public sealed partial class GunFiringScheduler
    {
        public const int MaximumEmissionsPerSchedule = 4096;
        public const int DefaultReplayRetentionCapacity = 256;

        private readonly GunFiringClock clock;
        private readonly int replayRetentionCapacity;

        public GunFiringScheduler(GunFiringClock clock)
            : this(clock, DefaultReplayRetentionCapacity)
        {
        }

        public GunFiringScheduler(
            GunFiringClock clock,
            int replayRetentionCapacity)
        {
            this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
            if (replayRetentionCapacity < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(replayRetentionCapacity));
            }

            this.replayRetentionCapacity = replayRetentionCapacity;
        }

        public GunFiringClock Clock { get { return clock; } }
        public int ReplayRetentionCapacity { get { return replayRetentionCapacity; } }

        public GunFiringDecision Schedule(
            GunFiringRequest request,
            GunFiringSessionState previousState)
        {
            if (previousState == null || !previousState.HasValidFingerprint())
            {
                return GunFiringDecision.Reject(
                    GunFiringScheduleStatus.InvalidState,
                    previousState,
                    "gun-firing-state-invalid");
            }
            if (previousState.ClockTicksPerSecond != 0
                && previousState.ClockTicksPerSecond != clock.TicksPerSecond)
            {
                return GunFiringDecision.Reject(
                    GunFiringScheduleStatus.InvalidState,
                    previousState,
                    "gun-firing-state-clock-mismatch");
            }

            string requestValidationCode;
            if (!TryValidateRequest(request, out requestValidationCode))
            {
                return GunFiringDecision.Reject(
                    GunFiringScheduleStatus.InvalidRequest,
                    previousState,
                    requestValidationCode);
            }

            EffectiveGun gun = request.Gun;
            string effectiveFingerprint = EffectiveGunFiringFingerprint.Compute(gun);
            string requestFingerprint = RequestFingerprint(request, effectiveFingerprint);

            GunFiringReplayRecord replay;
            if (previousState.TryFindReplay(
                request.Command.ActorId,
                request.Command.EquipmentInstanceId,
                request.Command.LifecycleGeneration,
                request.Command.FireOperationId,
                out replay))
            {
                if (!IsExactReplay(replay, requestFingerprint, effectiveFingerprint))
                {
                    return GunFiringDecision.Reject(
                        GunFiringScheduleStatus.ConflictingDuplicate,
                        previousState,
                        "gun-firing-operation-conflicting-duplicate");
                }
                if (!replay.HasValidFingerprint())
                {
                    return GunFiringDecision.Reject(
                        GunFiringScheduleStatus.InvalidState,
                        previousState,
                        "gun-firing-replay-record-invalid");
                }

                if (replay.HasAcceptedSchedule)
                {
                    if (!replay.AcceptedSchedule.HasValidFingerprint(gun)
                        || !ScheduleMatchesAuthoredTiming(gun, replay.AcceptedSchedule))
                    {
                        return GunFiringDecision.Reject(
                            GunFiringScheduleStatus.InvalidState,
                            previousState,
                            "gun-firing-replay-record-invalid");
                    }

                    return GunFiringDecision.Replay(
                        replay.AcceptedSchedule,
                        previousState);
                }

                return GunFiringDecision.ReplayTransition(
                    replay.SuccessfulStatus,
                    previousState,
                    replay.ResultCode);
            }

            GunFiringTrackState track;
            if (!previousState.TryFindTrack(
                request.Command.ActorId,
                request.ParticipantId,
                request.Command.EquipmentInstanceId,
                gun.DefinitionId,
                request.Command.LifecycleGeneration,
                out track))
            {
                GunFiringTrackState conflictingTrack;
                if (previousState.TryFindEquipmentLifecycleTrack(
                    request.Command.ActorId,
                    request.Command.EquipmentInstanceId,
                    request.Command.LifecycleGeneration,
                    out conflictingTrack))
                {
                    return GunFiringDecision.Reject(
                        GunFiringScheduleStatus.IdentityMismatch,
                        previousState,
                        "gun-firing-state-identity-mismatch");
                }

                track = new GunFiringTrackState(
                    request.Command.ActorId,
                    request.ParticipantId,
                    request.Command.EquipmentInstanceId,
                    gun.DefinitionId,
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
            else if (!TryValidateTrackTiming(track, gun, effectiveFingerprint))
            {
                return GunFiringDecision.Reject(
                    GunFiringScheduleStatus.InvalidState,
                    previousState,
                    "gun-firing-track-timing-invalid");
            }

            if (track.IsReplayExpired(request.Command.SimulationTick))
            {
                return GunFiringDecision.Reject(
                    GunFiringScheduleStatus.ReplayExpired,
                    previousState,
                    "gun-firing-replay-expired");
            }

            if (request.Command.SimulationTick < track.LastObservedSimulationTick)
            {
                return GunFiringDecision.Reject(
                    GunFiringScheduleStatus.TimeReversal,
                    previousState,
                    "gun-firing-time-reversal");
            }

            bool sameEffectiveProfile = string.Equals(
                track.EffectiveGunFingerprint,
                effectiveFingerprint,
                StringComparison.Ordinal);
            if (track.TriggerHeld && !sameEffectiveProfile)
            {
                return GunFiringDecision.Reject(
                    GunFiringScheduleStatus.IdentityMismatch,
                    previousState,
                    "gun-firing-effective-profile-changed-while-trigger-held");
            }

            if (request.TriggerSignal == GunTriggerSignal.Released)
            {
                return Release(request, previousState, track,
                    effectiveFingerprint, requestFingerprint, sameEffectiveProfile);
            }

            if (request.TriggerSignal == GunTriggerSignal.Pressed)
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
