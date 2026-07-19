using System;
using System.Collections.Generic;

namespace ShooterMover.GameplayEntities.Enemies
{
    /// <summary>
    /// Converts externally gathered target and line-of-sight data into one immutable,
    /// geometrically consistent decision input. It performs no scene or physics query.
    /// </summary>
    public static class EnemyPerceptionBuilder
    {
        private const double ArcBoundaryTolerance = 0.000000000001d;

        public static EnemyPerceptionSnapshot Build(
            EnemyVector2 observerPosition,
            EnemyVector2 observerFacing,
            IEnumerable<EnemyPerceptionCandidate> candidates,
            double detectionRadius,
            double visionArcDegrees,
            long simulationTick)
        {
            if (candidates == null) throw new ArgumentNullException(nameof(candidates));
            RequireFiniteNonNegative(detectionRadius, nameof(detectionRadius));
            RequireArc(visionArcDegrees, nameof(visionArcDegrees));

            EnemyVector2 facing = observerFacing.Normalized;
            List<EnemyPerceivedTarget> perceived = new List<EnemyPerceivedTarget>();
            foreach (EnemyPerceptionCandidate candidate in candidates)
            {
                if (candidate == null)
                {
                    throw new ArgumentException("Candidates cannot contain null.", nameof(candidates));
                }

                EnemyVector2 offset = new EnemyVector2(
                    candidate.Position.X - observerPosition.X,
                    candidate.Position.Y - observerPosition.Y);
                double distance = offset.Length;
                EnemyVector2 direction = offset.Normalized;
                bool detected = distance <= detectionRadius;
                bool withinArc = IsWithinArc(facing, direction, visionArcDegrees);
                perceived.Add(
                    new EnemyPerceivedTarget(
                        candidate.EntityId,
                        candidate.FactionId,
                        candidate.Relationship,
                        candidate.Position,
                        candidate.Velocity,
                        distance,
                        direction,
                        candidate.HasLineOfSight,
                        detected,
                        withinArc));
            }

            return new EnemyPerceptionSnapshot(
                observerPosition,
                facing,
                perceived,
                simulationTick);
        }

        public static bool IsWithinArc(
            EnemyVector2 facing,
            EnemyVector2 direction,
            double arcDegrees)
        {
            RequireArc(arcDegrees, nameof(arcDegrees));
            EnemyVector2 normalizedFacing = facing.Normalized;
            EnemyVector2 normalizedDirection = direction.Normalized;
            if (normalizedDirection.Length == 0d || arcDegrees == 360d)
            {
                return true;
            }

            if (normalizedFacing.Length == 0d)
            {
                return false;
            }

            double halfArcRadians = arcDegrees * Math.PI / 360d;
            double threshold = Math.Cos(halfArcRadians);
            double dot = (normalizedFacing.X * normalizedDirection.X)
                + (normalizedFacing.Y * normalizedDirection.Y);
            return dot + ArcBoundaryTolerance >= threshold;
        }

        private static void RequireArc(double value, string parameterName)
        {
            if (double.IsNaN(value)
                || double.IsInfinity(value)
                || value <= 0d
                || value > 360d)
            {
                throw new ArgumentOutOfRangeException(parameterName);
            }
        }

        private static void RequireFiniteNonNegative(double value, string parameterName)
        {
            if (double.IsNaN(value) || double.IsInfinity(value) || value < 0d)
            {
                throw new ArgumentOutOfRangeException(parameterName);
            }
        }
    }
}
