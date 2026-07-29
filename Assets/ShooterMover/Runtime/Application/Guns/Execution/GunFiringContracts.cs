using System;
using ShooterMover.Domain.Guns;
using ShooterMover.Domain.Guns.Execution;

namespace ShooterMover.Application.Guns.Execution
{
    public enum GunTriggerSignal
    {
        Pressed = 1,
        Held = 2,
        Released = 3,
    }

    public enum GunFiringEmissionKind
    {
        ProjectileShot = 1,
        ContinuousDamageTick = 2,
    }

    public sealed partial class GunFiringClock
    {
        public GunFiringClock(int ticksPerSecond)
        {
            if (ticksPerSecond < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(ticksPerSecond));
            }

            TicksPerSecond = ticksPerSecond;
        }

        public int TicksPerSecond { get; }
    }

    public sealed class GunFiringRequest
    {
        public GunFiringRequest(
            EffectiveGun gun,
            GunFireCommand command,
            RunParticipantId participantId,
            GunTriggerSignal triggerSignal)
        {
            Gun = gun ?? throw new ArgumentNullException(nameof(gun));
            Command = command ?? throw new ArgumentNullException(nameof(command));
            ParticipantId = participantId ?? throw new ArgumentNullException(nameof(participantId));
            if (!Enum.IsDefined(typeof(GunTriggerSignal), triggerSignal))
            {
                throw new ArgumentOutOfRangeException(nameof(triggerSignal));
            }

            TriggerSignal = triggerSignal;
        }

        public EffectiveGun Gun { get; }
        public GunFireCommand Command { get; }
        public RunParticipantId ParticipantId { get; }
        public GunTriggerSignal TriggerSignal { get; }
    }

    public enum GunFiringScheduleStatus
    {
        Accepted = 1,
        Replayed = 2,
        WaitingForCadence = 3,
        Released = 4,
        InvalidRequest = 5,
        IdentityMismatch = 6,
        TriggerTransitionRejected = 7,
        CooldownActive = 8,
        TimeReversal = 9,
        ConflictingDuplicate = 10,
        UnsupportedConfiguration = 11,
        ScheduleCapacityExceeded = 12,
        NumericalFailure = 13,
        InvalidState = 14,
        ReplayExpired = 15,
    }

    public enum GunFiringDecisionKind
    {
        AcceptedEmission = 1,
        ReplayedEmission = 2,
        SuccessfulTransition = 3,
        ReplayedTransition = 4,
        Rejection = 5,
    }

    public sealed class GunFiringDecision
    {
        private GunFiringDecision(
            GunFiringDecisionKind kind,
            GunFiringScheduleStatus status,
            GunFiringScheduler.AcceptedSchedule acceptedSchedule,
            GunFiringSessionState nextState,
            string resultCode)
        {
            Kind = kind;
            Status = status;
            AcceptedSchedule = acceptedSchedule;
            NextState = nextState;
            ResultCode = resultCode ?? string.Empty;
        }

        public GunFiringDecisionKind Kind { get; }
        public GunFiringScheduleStatus Status { get; }
        public GunFiringScheduler.AcceptedSchedule AcceptedSchedule { get; }
        public GunFiringSessionState NextState { get; }
        public string ResultCode { get; }
        public string RejectionCode { get { return ResultCode; } }

        public bool IsReplay
        {
            get
            {
                return Kind == GunFiringDecisionKind.ReplayedEmission
                    || Kind == GunFiringDecisionKind.ReplayedTransition;
            }
        }

        public bool IsAcceptance
        {
            get
            {
                return Kind == GunFiringDecisionKind.AcceptedEmission
                    || Kind == GunFiringDecisionKind.ReplayedEmission;
            }
        }

        public bool IsSuccessfulTransition
        {
            get
            {
                return Kind != GunFiringDecisionKind.Rejection;
            }
        }

        internal static GunFiringDecision Accept(
            GunFiringScheduler.AcceptedSchedule schedule,
            GunFiringSessionState nextState)
        {
            return new GunFiringDecision(
                GunFiringDecisionKind.AcceptedEmission,
                GunFiringScheduleStatus.Accepted,
                schedule ?? throw new ArgumentNullException(nameof(schedule)),
                nextState ?? throw new ArgumentNullException(nameof(nextState)),
                string.Empty);
        }

        internal static GunFiringDecision Replay(
            GunFiringScheduler.AcceptedSchedule schedule,
            GunFiringSessionState currentState)
        {
            return new GunFiringDecision(
                GunFiringDecisionKind.ReplayedEmission,
                GunFiringScheduleStatus.Replayed,
                schedule ?? throw new ArgumentNullException(nameof(schedule)),
                currentState ?? throw new ArgumentNullException(nameof(currentState)),
                string.Empty);
        }

        internal static GunFiringDecision ReplayTransition(
            GunFiringScheduleStatus originalStatus,
            GunFiringSessionState currentState,
            string resultCode)
        {
            ValidateTransitionStatus(originalStatus);
            return new GunFiringDecision(
                GunFiringDecisionKind.ReplayedTransition,
                originalStatus,
                null,
                currentState ?? throw new ArgumentNullException(nameof(currentState)),
                resultCode);
        }

        internal static GunFiringDecision Transition(
            GunFiringScheduleStatus status,
            GunFiringSessionState nextState,
            string resultCode)
        {
            ValidateTransitionStatus(status);
            return new GunFiringDecision(
                GunFiringDecisionKind.SuccessfulTransition,
                status,
                null,
                nextState ?? throw new ArgumentNullException(nameof(nextState)),
                resultCode);
        }

        internal static GunFiringDecision Reject(
            GunFiringScheduleStatus status,
            GunFiringSessionState unchangedState,
            string rejectionCode)
        {
            if (status == GunFiringScheduleStatus.Accepted
                || status == GunFiringScheduleStatus.Replayed
                || status == GunFiringScheduleStatus.WaitingForCadence
                || status == GunFiringScheduleStatus.Released)
            {
                throw new ArgumentOutOfRangeException(nameof(status));
            }
            if (string.IsNullOrWhiteSpace(rejectionCode))
            {
                throw new ArgumentException(
                    "A stable rejection code is required.",
                    nameof(rejectionCode));
            }

            return new GunFiringDecision(
                GunFiringDecisionKind.Rejection,
                status,
                null,
                unchangedState,
                rejectionCode);
        }

        private static void ValidateTransitionStatus(GunFiringScheduleStatus status)
        {
            if (status != GunFiringScheduleStatus.WaitingForCadence
                && status != GunFiringScheduleStatus.Released)
            {
                throw new ArgumentOutOfRangeException(nameof(status));
            }
        }
    }
}
