using System;
using System.Collections.Generic;
using UnityEngine;

namespace ShooterMover.UI.Game
{
    /// <summary>
    /// The set of one-by-one floor cells that can support the player.
    /// Decorative floor-looking objects are deliberately not added here.
    /// </summary>
    public sealed class FloorGrid
    {
        private const float TileHalfSize = 0.5f;
        private const float MaximumDisplacement = 51.2f;
        private const int SafePositionSearchIterations = 20;
        private const float GeometryTolerance = 0.0001f;

        private readonly HashSet<Vector2Int> cells;

        public FloorGrid(IEnumerable<Vector2Int> floorCells)
        {
            if (floorCells == null)
            {
                throw new ArgumentNullException(nameof(floorCells));
            }

            cells = new HashSet<Vector2Int>(floorCells);
        }

        public bool HasCells
        {
            get { return cells.Count > 0; }
        }

        public bool FitsCircle(Vector2 center, float radius)
        {
            if (!HasCells || !IsFinite(center) || !IsFinite(radius) || radius < 0f)
            {
                return false;
            }
            if (radius <= GeometryTolerance)
            {
                return ContainsPoint(center);
            }

            int minX = Mathf.FloorToInt(center.x - radius - TileHalfSize);
            int maxX = Mathf.CeilToInt(center.x + radius + TileHalfSize);
            int minY = Mathf.FloorToInt(center.y - radius - TileHalfSize);
            int maxY = Mathf.CeilToInt(center.y + radius + TileHalfSize);

            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    var cell = new Vector2Int(x, y);
                    if (!CircleOverlapsCell(center, radius, cell)) continue;
                    if (!cells.Contains(cell)) return false;
                }
            }
            return true;
        }

        public Vector2 LimitVelocity(
            Vector2 center,
            Vector2 velocity,
            float fixedDeltaTime,
            float radius)
        {
            if (!HasCells
                || !IsFinite(center)
                || !IsFinite(velocity)
                || !IsFinite(fixedDeltaTime)
                || fixedDeltaTime <= 0f
                || velocity.sqrMagnitude < 0.000001f
                || !FitsCircle(center, radius))
            {
                return Vector2.zero;
            }

            Vector2 displacement = velocity * fixedDeltaTime;
            float distance = displacement.magnitude;
            if (!IsFinite(displacement)
                || !IsFinite(distance)
                || distance > MaximumDisplacement)
            {
                return Vector2.zero;
            }

            // Rigidbody2D follows one straight velocity during this physics step.
            // Each candidate is therefore checked as that exact continuous segment,
            // rather than as sampled or piecewise movement that could cut a corner.
            Vector2 accepted = SweepStraight(center, displacement, radius);
            Vector2 horizontal = SweepStraight(
                center,
                new Vector2(displacement.x, 0f),
                radius);
            Vector2 vertical = SweepStraight(
                center,
                new Vector2(0f, displacement.y),
                radius);

            if (Mathf.Abs(displacement.x) >= Mathf.Abs(displacement.y))
            {
                accepted = Longer(accepted, horizontal);
                accepted = Longer(accepted, vertical);
            }
            else
            {
                accepted = Longer(accepted, vertical);
                accepted = Longer(accepted, horizontal);
            }

            return accepted / fixedDeltaTime;
        }

        public bool TryFindNearestPosition(
            Vector2 origin,
            float radius,
            out Vector2 position)
        {
            position = Vector2.zero;
            if (!IsFinite(origin) || !IsFinite(radius) || radius < 0f)
            {
                return false;
            }

            bool found = false;
            float bestDistance = float.PositiveInfinity;
            Vector2Int bestCell = default(Vector2Int);

            foreach (Vector2Int cell in cells)
            {
                Vector2 candidate = new Vector2(cell.x, cell.y);
                if (!FitsCircle(candidate, radius)) continue;

                float distance = (candidate - origin).sqrMagnitude;
                if (found
                    && distance > bestDistance + GeometryTolerance)
                {
                    continue;
                }
                if (found
                    && Mathf.Abs(distance - bestDistance) <= GeometryTolerance
                    && !ComesBefore(cell, bestCell))
                {
                    continue;
                }

                found = true;
                bestDistance = distance;
                bestCell = cell;
                position = candidate;
            }
            return found;
        }

        private Vector2 SweepStraight(
            Vector2 center,
            Vector2 displacement,
            float radius)
        {
            if (displacement.sqrMagnitude < 0.000001f)
            {
                return Vector2.zero;
            }

            Vector2 destination = center + displacement;
            if (IsSegmentSupported(center, destination, radius))
            {
                return displacement;
            }

            float safe = 0f;
            float blocked = 1f;
            for (int index = 0; index < SafePositionSearchIterations; index++)
            {
                float fraction = (safe + blocked) * 0.5f;
                Vector2 candidate = center + displacement * fraction;
                if (IsSegmentSupported(center, candidate, radius))
                {
                    safe = fraction;
                }
                else
                {
                    blocked = fraction;
                }
            }
            return displacement * safe;
        }

        private bool IsSegmentSupported(
            Vector2 start,
            Vector2 end,
            float radius)
        {
            if (!FitsCircle(end, radius)) return false;

            float safeRadius = Mathf.Max(0f, radius - GeometryTolerance);
            if (safeRadius <= GeometryTolerance)
            {
                return true;
            }

            int minX = Mathf.FloorToInt(
                Mathf.Min(start.x, end.x) - safeRadius - TileHalfSize);
            int maxX = Mathf.CeilToInt(
                Mathf.Max(start.x, end.x) + safeRadius + TileHalfSize);
            int minY = Mathf.FloorToInt(
                Mathf.Min(start.y, end.y) - safeRadius - TileHalfSize);
            int maxY = Mathf.CeilToInt(
                Mathf.Max(start.y, end.y) + safeRadius + TileHalfSize);
            float radiusSquared = safeRadius * safeRadius;

            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    var cell = new Vector2Int(x, y);
                    if (cells.Contains(cell)) continue;
                    if (SegmentDistanceToCellSquared(start, end, cell)
                        < radiusSquared)
                    {
                        return false;
                    }
                }
            }
            return true;
        }

        private bool ContainsPoint(Vector2 point)
        {
            int minX = Mathf.FloorToInt(point.x - TileHalfSize);
            int maxX = Mathf.CeilToInt(point.x + TileHalfSize);
            int minY = Mathf.FloorToInt(point.y - TileHalfSize);
            int maxY = Mathf.CeilToInt(point.y + TileHalfSize);
            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    if (cells.Contains(new Vector2Int(x, y))
                        && Mathf.Abs(point.x - x) <= TileHalfSize + GeometryTolerance
                        && Mathf.Abs(point.y - y) <= TileHalfSize + GeometryTolerance)
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        private static float SegmentDistanceToCellSquared(
            Vector2 start,
            Vector2 end,
            Vector2Int cell)
        {
            float minX = cell.x - TileHalfSize;
            float maxX = cell.x + TileHalfSize;
            float minY = cell.y - TileHalfSize;
            float maxY = cell.y + TileHalfSize;
            if (SegmentIntersectsRectangle(
                    start,
                    end,
                    minX,
                    maxX,
                    minY,
                    maxY))
            {
                return 0f;
            }

            float distance = Mathf.Min(
                DistancePointToRectangleSquared(
                    start,
                    minX,
                    maxX,
                    minY,
                    maxY),
                DistancePointToRectangleSquared(
                    end,
                    minX,
                    maxX,
                    minY,
                    maxY));
            distance = Mathf.Min(
                distance,
                DistancePointToSegmentSquared(
                    new Vector2(minX, minY),
                    start,
                    end));
            distance = Mathf.Min(
                distance,
                DistancePointToSegmentSquared(
                    new Vector2(minX, maxY),
                    start,
                    end));
            distance = Mathf.Min(
                distance,
                DistancePointToSegmentSquared(
                    new Vector2(maxX, minY),
                    start,
                    end));
            return Mathf.Min(
                distance,
                DistancePointToSegmentSquared(
                    new Vector2(maxX, maxY),
                    start,
                    end));
        }

        private static bool SegmentIntersectsRectangle(
            Vector2 start,
            Vector2 end,
            float minX,
            float maxX,
            float minY,
            float maxY)
        {
            float minimumTime = 0f;
            float maximumTime = 1f;
            Vector2 movement = end - start;
            return ClipAxis(
                    start.x,
                    movement.x,
                    minX,
                    maxX,
                    ref minimumTime,
                    ref maximumTime)
                && ClipAxis(
                    start.y,
                    movement.y,
                    minY,
                    maxY,
                    ref minimumTime,
                    ref maximumTime);
        }

        private static bool ClipAxis(
            float start,
            float movement,
            float minimum,
            float maximum,
            ref float minimumTime,
            ref float maximumTime)
        {
            if (Mathf.Abs(movement) <= GeometryTolerance)
            {
                return start >= minimum && start <= maximum;
            }

            float first = (minimum - start) / movement;
            float second = (maximum - start) / movement;
            if (first > second)
            {
                float swap = first;
                first = second;
                second = swap;
            }

            minimumTime = Mathf.Max(minimumTime, first);
            maximumTime = Mathf.Min(maximumTime, second);
            return minimumTime <= maximumTime;
        }

        private static float DistancePointToRectangleSquared(
            Vector2 point,
            float minX,
            float maxX,
            float minY,
            float maxY)
        {
            float x = Mathf.Max(minX - point.x, 0f, point.x - maxX);
            float y = Mathf.Max(minY - point.y, 0f, point.y - maxY);
            return x * x + y * y;
        }

        private static float DistancePointToSegmentSquared(
            Vector2 point,
            Vector2 start,
            Vector2 end)
        {
            Vector2 segment = end - start;
            float lengthSquared = segment.sqrMagnitude;
            if (lengthSquared <= GeometryTolerance)
            {
                return (point - start).sqrMagnitude;
            }

            float time = Mathf.Clamp01(
                Vector2.Dot(point - start, segment) / lengthSquared);
            Vector2 nearest = start + segment * time;
            return (point - nearest).sqrMagnitude;
        }

        private static float DistancePointToCellSquared(
            Vector2 point,
            Vector2Int cell)
        {
            return DistancePointToRectangleSquared(
                point,
                cell.x - TileHalfSize,
                cell.x + TileHalfSize,
                cell.y - TileHalfSize,
                cell.y + TileHalfSize);
        }

        private static Vector2 Longer(Vector2 current, Vector2 candidate)
        {
            return candidate.sqrMagnitude > current.sqrMagnitude + GeometryTolerance
                ? candidate
                : current;
        }

        private static bool CircleOverlapsCell(
            Vector2 center,
            float radius,
            Vector2Int cell)
        {
            float safeRadius = Mathf.Max(0f, radius - GeometryTolerance);
            return DistancePointToCellSquared(center, cell)
                < safeRadius * safeRadius;
        }

        private static bool ComesBefore(Vector2Int candidate, Vector2Int current)
        {
            return candidate.y < current.y
                || (candidate.y == current.y && candidate.x < current.x);
        }

        private static bool IsFinite(Vector2 value)
        {
            return IsFinite(value.x) && IsFinite(value.y);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
