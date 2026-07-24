using System;
using ShooterMover.Domain.Weapons.Execution;

namespace ShooterMover.Domain.Weapons.Guidance
{
    internal static class WeaponGuidanceGeometry
    {
        private const double RadiansToDegrees = 180d / Math.PI;

        public static WeaponVector2 Difference(WeaponVector2 to, WeaponVector2 from)
        {
            return new WeaponVector2(to.X - from.X, to.Y - from.Y);
        }

        public static double DistanceSquared(WeaponVector2 left, WeaponVector2 right)
        {
            double x = left.X - right.X;
            double y = left.Y - right.Y;
            return (x * x) + (y * y);
        }

        public static double Alignment(WeaponVector2 direction, WeaponVector2 offset)
        {
            WeaponVector2 normalizedDirection = direction.Normalized;
            WeaponVector2 normalizedOffset = offset.Normalized;
            return (normalizedDirection.X * normalizedOffset.X)
                + (normalizedDirection.Y * normalizedOffset.Y);
        }

        public static WeaponVector2 RotateTowards(
            WeaponVector2 currentDirection,
            WeaponVector2 desiredDirection,
            double maximumTurnDegrees)
        {
            if (double.IsNaN(maximumTurnDegrees)
                || double.IsInfinity(maximumTurnDegrees)
                || maximumTurnDegrees < 0d)
            {
                throw new ArgumentOutOfRangeException(nameof(maximumTurnDegrees));
            }

            WeaponVector2 current = currentDirection.Normalized;
            WeaponVector2 desired = desiredDirection.Normalized;
            if (maximumTurnDegrees <= 0d || desired.LengthSquared <= 0d)
            {
                return current;
            }

            double cross = (current.X * desired.Y) - (current.Y * desired.X);
            double dot = (current.X * desired.X) + (current.Y * desired.Y);
            double signedAngle = Math.Atan2(cross, dot) * RadiansToDegrees;
            double appliedAngle = Math.Max(
                -maximumTurnDegrees,
                Math.Min(maximumTurnDegrees, signedAngle));

            if (appliedAngle.Equals(signedAngle))
            {
                return desired;
            }

            return current.RotateDegrees(appliedAngle).Normalized;
        }
    }
}
