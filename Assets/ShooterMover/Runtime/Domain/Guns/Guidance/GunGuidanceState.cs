using System;
using ShooterMover.Domain.Guns.Execution;

namespace ShooterMover.Domain.Guns.Guidance
{
    public enum GunGuidanceDecisionStatus
    {
        Unguided = 1,
        WaitingForActivation = 2,
        Paused = 3,
        NoTarget = 4,
        Tracking = 5,
    }

    public enum GunGuidanceAcquisitionState
    {
        NotAcquired = 1,
        Tracking = 2,
        WaitingForReacquisition = 3,
        LostWithoutReacquisition = 4,
    }

    /// <summary>
    /// Immutable per-projectile guidance state. It is reusable for every projectile kind and owns
    /// no movement, physics, collision, or presentation behavior.
    /// </summary>
    public sealed class GunGuidanceState
    {
        private GunGuidanceState(
            GunVector2 acquisitionAimDirection,
            GunVector2 direction,
            double elapsedSeconds,
            double pauseRemainingSeconds,
            GunGuidanceAcquisitionState acquisitionState,
            GunTargetReference trackedTarget)
        {
            AcquisitionAimDirection = RequireDirection(
                acquisitionAimDirection,
                nameof(acquisitionAimDirection));
            Direction = RequireDirection(direction, nameof(direction));
            RequireFiniteNonNegative(elapsedSeconds, nameof(elapsedSeconds));
            RequireFiniteNonNegative(pauseRemainingSeconds, nameof(pauseRemainingSeconds));
            if (!Enum.IsDefined(typeof(GunGuidanceAcquisitionState), acquisitionState))
            {
                throw new ArgumentOutOfRangeException(nameof(acquisitionState));
            }
            if (acquisitionState == GunGuidanceAcquisitionState.Tracking
                && trackedTarget == null)
            {
                throw new ArgumentException(
                    "Tracking guidance state requires an exact target reference.",
                    nameof(trackedTarget));
            }
            if ((acquisitionState == GunGuidanceAcquisitionState.NotAcquired
                    || acquisitionState
                        == GunGuidanceAcquisitionState.LostWithoutReacquisition)
                && trackedTarget != null)
            {
                throw new ArgumentException(
                    "This guidance acquisition state cannot retain a target reference.",
                    nameof(trackedTarget));
            }

            ElapsedSeconds = elapsedSeconds;
            PauseRemainingSeconds = pauseRemainingSeconds;
            AcquisitionState = acquisitionState;
            TrackedTarget = trackedTarget;
        }

        public GunVector2 AcquisitionAimDirection { get; }
        public GunVector2 Direction { get; }
        public double ElapsedSeconds { get; }
        public double PauseRemainingSeconds { get; }
        public GunGuidanceAcquisitionState AcquisitionState { get; }
        public GunTargetReference TrackedTarget { get; }

        public static GunGuidanceState Create(
            GunVector2 initialDirection,
            GunTargetReference initialTarget = null)
        {
            GunVector2 direction = RequireDirection(initialDirection, nameof(initialDirection));
            GunGuidanceAcquisitionState acquisitionState = initialTarget == null
                ? GunGuidanceAcquisitionState.NotAcquired
                : GunGuidanceAcquisitionState.Tracking;
            return new GunGuidanceState(
                direction,
                direction,
                0d,
                0d,
                acquisitionState,
                initialTarget);
        }

        /// <summary>
        /// Applies an externally resolved ricochet direction and pauses homing for the requested
        /// duration. The exact target and acquisition lifecycle are preserved while guidance pauses.
        /// </summary>
        public GunGuidanceState PauseAfterRicochet(
            GunVector2 reflectedDirection,
            double pauseSeconds)
        {
            GunVector2 direction = RequireDirection(
                reflectedDirection,
                nameof(reflectedDirection));
            RequireFiniteNonNegative(pauseSeconds, nameof(pauseSeconds));

            return new GunGuidanceState(
                direction,
                direction,
                ElapsedSeconds,
                Math.Max(PauseRemainingSeconds, pauseSeconds),
                AcquisitionState,
                TrackedTarget);
        }

        public GunGuidanceState Resume()
        {
            return new GunGuidanceState(
                AcquisitionAimDirection,
                Direction,
                ElapsedSeconds,
                0d,
                AcquisitionState,
                TrackedTarget);
        }

        internal GunGuidanceState Advance(
            GunVector2 direction,
            double elapsedSeconds,
            double pauseRemainingSeconds,
            GunTargetReference trackedTarget)
        {
            return Advance(
                direction,
                elapsedSeconds,
                pauseRemainingSeconds,
                AcquisitionState,
                trackedTarget);
        }

        internal GunGuidanceState Advance(
            GunVector2 direction,
            double elapsedSeconds,
            double pauseRemainingSeconds,
            GunGuidanceAcquisitionState acquisitionState,
            GunTargetReference trackedTarget)
        {
            return new GunGuidanceState(
                AcquisitionAimDirection,
                direction,
                elapsedSeconds,
                pauseRemainingSeconds,
                acquisitionState,
                trackedTarget);
        }

        private static GunVector2 RequireDirection(GunVector2 value, string parameterName)
        {
            if (value == null)
            {
                throw new ArgumentNullException(parameterName);
            }
            if (!value.IsFinite || value.LengthSquared <= 0d)
            {
                throw new ArgumentException(
                    "Gun guidance directions must be finite and non-zero.",
                    parameterName);
            }
            return value.Normalized;
        }

        private static void RequireFiniteNonNegative(double value, string parameterName)
        {
            if (double.IsNaN(value) || double.IsInfinity(value) || value < 0d)
            {
                throw new ArgumentOutOfRangeException(parameterName);
            }
        }
    }

    public sealed class GunGuidanceDecision
    {
        internal GunGuidanceDecision(
            GunGuidanceDecisionStatus status,
            GunGuidanceState nextState,
            GunGuidanceTargetSnapshot resolvedTarget)
        {
            Status = status;
            NextState = nextState ?? throw new ArgumentNullException(nameof(nextState));
            ResolvedTarget = resolvedTarget;
        }

        public GunGuidanceDecisionStatus Status { get; }
        public GunGuidanceState NextState { get; }
        public GunVector2 Direction { get { return NextState.Direction; } }
        public GunGuidanceTargetSnapshot ResolvedTarget { get; }
        public bool HasResolvedTarget { get { return ResolvedTarget != null; } }
    }
}
