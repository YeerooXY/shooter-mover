using System;
using ShooterMover.Domain.Guns.Execution;

namespace ShooterMover.Domain.Guns.Guidance
{
    internal static class GunGuidanceGeometry
    {
        private const double RadiansToDegrees = 180d / Math.PI;

        public static GunVector2 Difference(GunVector2 to, GunVector2 from)
        {
            return new GunVector2(to.X - from.X, to.Y - from.Y);
        }

        public static double DistanceSquared(GunVector2 left, GunVector2 right)
        {
            double x = left.X - right.X;
            double y = left.Y - right.Y;
            return (x * x) + (y * y);
        }

        public static double Alignment(GunVector2 direction, GunVector2 offset)
        {
            GunVector2 normalizedDirection = direction.Normalized;
            GunVector2 normalizedOffset = offset.Normalized;
            return (normalizedDirection.X * normalizedOffset.X)
                + (normalizedDirection.Y * normalizedOffset.Y);
        }

        public static GunVector2 RotateTowards(
            GunVector2 currentDirection,
            GunVector2 desiredDirection,
            double maximumTurnDegrees)
        {
            if (double.IsNaN(maximumTurnDegrees)
                || double.IsInfinity(maximumTurnDegrees)
                || maximumTurnDegrees < 0d)
            {
                throw new ArgumentOutOfRangeException(nameof(maximumTurnDegrees));
            }

            GunVector2 current = currentDirection.Normalized;
            GunVector2 desired = desiredDirection.Normalized;
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
