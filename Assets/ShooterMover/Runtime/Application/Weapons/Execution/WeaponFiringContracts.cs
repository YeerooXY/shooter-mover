using System;
using ShooterMover.Domain.Weapons;
using ShooterMover.Domain.Weapons.Execution;

namespace ShooterMover.Application.Weapons.Execution
{
    public enum WeaponTriggerSignal
    {
        Pressed = 1,
        Held = 2,
        Released = 3,
    }

    public enum WeaponFiringEmissionKind
    {
        ProjectileShot = 1,
        ContinuousDamageTick = 2,
    }

    public sealed partial class WeaponFiringClock
    {
        public WeaponFiringClock(int ticksPerSecond)
        {
            if (ticksPerSecond < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(ticksPerSecond));
            }

            TicksPerSecond = ticksPerSecond;
        }

        public int TicksPerSecond { get; }
    }

    public sealed class WeaponFiringRequest
    {
        public WeaponFiringRequest(
            EffectiveWeapon weapon,
            WeaponFireCommand command,
            RunParticipantId participantId,
            WeaponTriggerSignal triggerSignal)
        {
            Weapon = weapon ?? throw new ArgumentNullException(nameof(weapon));
            Command = command ?? throw new ArgumentNullException(nameof(command));
            ParticipantId = participantId ?? throw new ArgumentNullException(nameof(participantId));
            if (!Enum.IsDefined(typeof(WeaponTriggerSignal), triggerSignal))
            {
                throw new ArgumentOutOfRangeException(nameof(triggerSignal));
            }

            TriggerSignal = triggerSignal;
        }

        public EffectiveWeapon Weapon { get; }
        public WeaponFireCommand Command { get; }
        public RunParticipantId ParticipantId { get; }
        public WeaponTriggerSignal TriggerSignal { get; }
    }

    public enum WeaponFiringScheduleStatus
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

    public enum WeaponFiringDecisionKind
    {
        AcceptedEmission = 1,
        ReplayedEmission = 2,
        SuccessfulTransition = 3,
        ReplayedTransition = 4,
        Rejection = 5,
    }

    public sealed class WeaponFiringDecision
    {
        private WeaponFiringDecision(
            WeaponFiringDecisionKind kind,
            WeaponFiringScheduleStatus status,
            WeaponFiringScheduler.AcceptedSchedule acceptedSchedule,
            WeaponFiringSessionState nextState,
            string resultCode)
        {
            Kind = kind;
            Status = status;
            AcceptedSchedule = acceptedSchedule;
            NextState = nextState;
            ResultCode = resultCode ?? string.Empty;
        }

        public WeaponFiringDecisionKind Kind { get; }
        public WeaponFiringScheduleStatus Status { get; }
        public WeaponFiringScheduler.AcceptedSchedule AcceptedSchedule { get; }
        public WeaponFiringSessionState NextState { get; }
        public string ResultCode { get; }
        public string RejectionCode { get { return ResultCode; } }

        public bool IsReplay
        {
            get
            {
                return Kind == WeaponFiringDecisionKind.ReplayedEmission
                    || Kind == WeaponFiringDecisionKind.ReplayedTransition;
            }
        }

        public bool IsAcceptance
        {
            get
            {
                return Kind == WeaponFiringDecisionKind.AcceptedEmission
                    || Kind == WeaponFiringDecisionKind.ReplayedEmission;
            }
        }

        public bool IsSuccessfulTransition
        {
            get
            {
                return Kind != WeaponFiringDecisionKind.Rejection;
            }
        }

        internal static WeaponFiringDecision Accept(
            WeaponFiringScheduler.AcceptedSchedule schedule,
            WeaponFiringSessionState nextState)
        {
            return new WeaponFiringDecision(
                WeaponFiringDecisionKind.AcceptedEmission,
                WeaponFiringScheduleStatus.Accepted,
                schedule ?? throw new ArgumentNullException(nameof(schedule)),
                nextState ?? throw new ArgumentNullException(nameof(nextState)),
                string.Empty);
        }

        internal static WeaponFiringDecision Replay(
            WeaponFiringScheduler.AcceptedSchedule schedule,
            WeaponFiringSessionState currentState)
        {
            return new WeaponFiringDecision(
                WeaponFiringDecisionKind.ReplayedEmission,
                WeaponFiringScheduleStatus.Replayed,
                schedule ?? throw new ArgumentNullException(nameof(schedule)),
                currentState ?? throw new ArgumentNullException(nameof(currentState)),
                string.Empty);
        }

        internal static WeaponFiringDecision ReplayTransition(
            WeaponFiringScheduleStatus originalStatus,
            WeaponFiringSessionState currentState,
            string resultCode)
        {
            ValidateTransitionStatus(originalStatus);
            return new WeaponFiringDecision(
                WeaponFiringDecisionKind.ReplayedTransition,
                originalStatus,
                null,
                currentState ?? throw new ArgumentNullException(nameof(currentState)),
                resultCode);
        }

        internal static WeaponFiringDecision Transition(
            WeaponFiringScheduleStatus status,
            WeaponFiringSessionState nextState,
            string resultCode)
        {
            ValidateTransitionStatus(status);
            return new WeaponFiringDecision(
                WeaponFiringDecisionKind.SuccessfulTransition,
                status,
                null,
                nextState ?? throw new ArgumentNullException(nameof(nextState)),
                resultCode);
        }

        internal static WeaponFiringDecision Reject(
            WeaponFiringScheduleStatus status,
            WeaponFiringSessionState unchangedState,
            string rejectionCode)
        {
            if (status == WeaponFiringScheduleStatus.Accepted
                || status == WeaponFiringScheduleStatus.Replayed
                || status == WeaponFiringScheduleStatus.WaitingForCadence
                || status == WeaponFiringScheduleStatus.Released)
            {
                throw new ArgumentOutOfRangeException(nameof(status));
            }
            if (string.IsNullOrWhiteSpace(rejectionCode))
            {
                throw new ArgumentException(
                    "A stable rejection code is required.",
                    nameof(rejectionCode));
            }

            return new WeaponFiringDecision(
                WeaponFiringDecisionKind.Rejection,
                status,
                null,
                unchangedState,
                rejectionCode);
        }

        private static void ValidateTransitionStatus(WeaponFiringScheduleStatus status)
        {
            if (status != WeaponFiringScheduleStatus.WaitingForCadence
                && status != WeaponFiringScheduleStatus.Released)
            {
                throw new ArgumentOutOfRangeException(nameof(status));
            }
        }
    }
}
