using System;
using ShooterMover.Domain.Weapons.Execution;

namespace ShooterMover.Domain.Weapons.Guidance
{
    public enum WeaponGuidanceDecisionStatus
    {
        Unguided = 1,
        WaitingForActivation = 2,
        Paused = 3,
        NoTarget = 4,
        Tracking = 5,
    }

    /// <summary>
    /// Immutable per-projectile guidance state. It is reusable for every projectile kind and owns
    /// no movement, physics, collision, or presentation behavior.
    /// </summary>
    public sealed class WeaponGuidanceState
    {
        private WeaponGuidanceState(
            WeaponVector2 acquisitionAimDirection,
            WeaponVector2 direction,
            double elapsedSeconds,
            double pauseRemainingSeconds,
            WeaponGuidanceTargetReference trackedTarget)
        {
            AcquisitionAimDirection = RequireDirection(
                acquisitionAimDirection,
                nameof(acquisitionAimDirection));
            Direction = RequireDirection(direction, nameof(direction));
            RequireFiniteNonNegative(elapsedSeconds, nameof(elapsedSeconds));
            RequireFiniteNonNegative(pauseRemainingSeconds, nameof(pauseRemainingSeconds));

            ElapsedSeconds = elapsedSeconds;
            PauseRemainingSeconds = pauseRemainingSeconds;
            TrackedTarget = trackedTarget;
        }

        public WeaponVector2 AcquisitionAimDirection { get; }
        public WeaponVector2 Direction { get; }
        public double ElapsedSeconds { get; }
        public double PauseRemainingSeconds { get; }
        public WeaponGuidanceTargetReference TrackedTarget { get; }

        public static WeaponGuidanceState Create(
            WeaponVector2 initialDirection,
            WeaponGuidanceTargetReference initialTarget = null)
        {
            WeaponVector2 direction = RequireDirection(initialDirection, nameof(initialDirection));
            return new WeaponGuidanceState(direction, direction, 0d, 0d, initialTarget);
        }

        /// <summary>
        /// Applies an externally resolved ricochet direction and pauses homing for the requested
        /// duration. The exact tracked target is preserved while guidance is paused.
        /// </summary>
        public WeaponGuidanceState PauseAfterRicochet(
            WeaponVector2 reflectedDirection,
            double pauseSeconds)
        {
            WeaponVector2 direction = RequireDirection(
                reflectedDirection,
                nameof(reflectedDirection));
            RequireFiniteNonNegative(pauseSeconds, nameof(pauseSeconds));

            return new WeaponGuidanceState(
                direction,
                direction,
                ElapsedSeconds,
                Math.Max(PauseRemainingSeconds, pauseSeconds),
                TrackedTarget);
        }

        public WeaponGuidanceState Resume()
        {
            return new WeaponGuidanceState(
                AcquisitionAimDirection,
                Direction,
                ElapsedSeconds,
                0d,
                TrackedTarget);
        }

        internal WeaponGuidanceState Advance(
            WeaponVector2 direction,
            double elapsedSeconds,
            double pauseRemainingSeconds,
            WeaponGuidanceTargetReference trackedTarget)
        {
            return new WeaponGuidanceState(
                AcquisitionAimDirection,
                direction,
                elapsedSeconds,
                pauseRemainingSeconds,
                trackedTarget);
        }

        private static WeaponVector2 RequireDirection(WeaponVector2 value, string parameterName)
        {
            if (value == null)
            {
                throw new ArgumentNullException(parameterName);
            }
            if (!value.IsFinite || value.LengthSquared <= 0d)
            {
                throw new ArgumentException(
                    "Weapon guidance directions must be finite and non-zero.",
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

    public sealed class WeaponGuidanceDecision
    {
        internal WeaponGuidanceDecision(
            WeaponGuidanceDecisionStatus status,
            WeaponGuidanceState nextState,
            WeaponGuidanceTargetSnapshot resolvedTarget)
        {
            Status = status;
            NextState = nextState ?? throw new ArgumentNullException(nameof(nextState));
            ResolvedTarget = resolvedTarget;
        }

        public WeaponGuidanceDecisionStatus Status { get; }
        public WeaponGuidanceState NextState { get; }
        public WeaponVector2 Direction { get { return NextState.Direction; } }
        public WeaponGuidanceTargetSnapshot ResolvedTarget { get; }
        public bool HasResolvedTarget { get { return ResolvedTarget != null; } }
    }
}
