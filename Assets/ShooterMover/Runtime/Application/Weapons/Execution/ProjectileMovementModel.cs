using System;
using ShooterMover.Domain.Weapons.Execution;

namespace ShooterMover.Application.Weapons.Execution
{
    /// <summary>
    /// Pure kinematic advancement. Direction comes from WeaponGuidanceState; this component does
    /// not steer, resolve impacts, consume pierce, terminate, or emit effects.
    /// </summary>
    public sealed class ProjectileMovementModel
    {
        public ProjectileMovementResult AdvanceTime(
            ProjectileLifecycleState state,
            double deltaSeconds)
        {
            if (double.IsNaN(deltaSeconds)
                || double.IsInfinity(deltaSeconds)
                || deltaSeconds < 0d)
            {
                throw new ArgumentOutOfRangeException(nameof(deltaSeconds));
            }
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            return AdvanceDistance(state, state.Speed * deltaSeconds);
        }

        public ProjectileMovementResult AdvanceDistance(
            ProjectileLifecycleState state,
            double requestedDistance)
        {
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }
            if (!state.IsActive)
            {
                throw new InvalidOperationException(
                    "Only an active projectile may advance movement.");
            }
            if (double.IsNaN(requestedDistance)
                || double.IsInfinity(requestedDistance)
                || requestedDistance < 0d)
            {
                throw new ArgumentOutOfRangeException(nameof(requestedDistance));
            }

            double remainingRange = state.RemainingRange;
            double travelledDistance = Math.Min(requestedDistance, remainingRange);
            WeaponVector2 position = new WeaponVector2(
                state.Position.X + (state.Direction.X * travelledDistance),
                state.Position.Y + (state.Direction.Y * travelledDistance));
            bool reachedRangeLimit = travelledDistance >= remainingRange;
            double totalDistance = reachedRangeLimit
                ? state.Profile.Projectile.Range
                : state.DistanceTravelled + travelledDistance;
            ProjectileLifecycleState moved = state.WithKinematics(position, totalDistance);

            return new ProjectileMovementResult(
                moved,
                requestedDistance,
                travelledDistance,
                reachedRangeLimit);
        }
    }
}
