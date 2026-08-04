using UnityEngine;

namespace ShooterMover.UnityAdapters.Enemies
{
    public static class CompactEnemySteering
    {
        public const float DefaultRangeTolerance = 0.5f;

        public static Vector2 ResolveRangeHold(
            Vector2 directionToTarget,
            float distance,
            float desiredRange,
            float tolerance = DefaultRangeTolerance)
        {
            if (directionToTarget.sqrMagnitude < 0.000001f)
            {
                return Vector2.zero;
            }

            Vector2 direct = directionToTarget.normalized;
            float safeRange = Mathf.Max(0f, desiredRange);
            float safeTolerance = Mathf.Max(0f, tolerance);
            if (distance > safeRange + safeTolerance)
            {
                return direct;
            }
            if (distance < safeRange - safeTolerance)
            {
                return -direct;
            }
            return Vector2.zero;
        }
    }
}
