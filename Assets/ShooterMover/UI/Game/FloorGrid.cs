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
        private readonly List<Boundary> boundaries;

        public FloorGrid(IEnumerable<Vector2Int> floorCells)
        {
            if (floorCells == null)
            {
                throw new ArgumentNullException(nameof(floorCells));
            }

            cells = new HashSet<Vector2Int>(floorCells);
            boundaries = BuildBoundaries(cells);
        }

        public bool HasCells
        {
            get { return cells.Count > 0; }
        }

        public bool FitsCircle(Vector2 center, float radius)
        {
            if (!HasCells
                || !IsFinite(center)
                || !IsFinite(radius)
                || radius < 0f
                || !ContainsPoint(center))
            {
                return false;
            }

            float safeRadius = Mathf.Max(0f, radius - GeometryTolerance);
            float radiusSquared = safeRadius * safeRadius;
            for (int index = 0; index < boundaries.Count; index++)
            {
                Boundary boundary = boundaries[index];
                if (!CouldBeNear(center, boundary, safeRadius)) continue;
                if (DistancePointToSegmentSquared(
                        center,
                        boundary.Start,
                        boundary.End) < radiusSquared)
                {
                    return false;
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
            // Every candidate is checked as that exact continuous center path.
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
                if (found && distance > bestDistance + GeometryTolerance)
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
            float radiusSquared = safeRadius * safeRadius;
            for (int index = 0; index < boundaries.Count; index++)
            {
                Boundary boundary = boundaries[index];
                if (!CouldBeNear(start, end, boundary, safeRadius)) continue;
                if (DistanceSegmentToSegmentSquared(
                        start,
                        end,
                        boundary.Start,
                        boundary.End) < radiusSquared)
                {
                    return false;
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

        private static List<Boundary> BuildBoundaries(
            HashSet<Vector2Int> floorCells)
        {
            var result = new List<Boundary>();
            foreach (Vector2Int cell in floorCells)
            {
                float left = cell.x - TileHalfSize;
                float right = cell.x + TileHalfSize;
                float bottom = cell.y - TileHalfSize;
                float top = cell.y + TileHalfSize;

                if (!floorCells.Contains(cell + Vector2Int.left))
                {
                    result.Add(new Boundary(
                        new Vector2(left, bottom),
                        new Vector2(left, top)));
                }
                if (!floorCells.Contains(cell + Vector2Int.right))
                {
                    result.Add(new Boundary(
                        new Vector2(right, bottom),
                        new Vector2(right, top)));
                }
                if (!floorCells.Contains(cell + Vector2Int.down))
                {
                    result.Add(new Boundary(
                        new Vector2(left, bottom),
                        new Vector2(right, bottom)));
                }
                if (!floorCells.Contains(cell + Vector2Int.up))
                {
                    result.Add(new Boundary(
                        new Vector2(left, top),
                        new Vector2(right, top)));
                }
            }
            return result;
        }

        private static bool CouldBeNear(
            Vector2 point,
            Boundary boundary,
            float distance)
        {
            return point.x >= boundary.MinimumX - distance
                && point.x <= boundary.MaximumX + distance
                && point.y >= boundary.MinimumY - distance
                && point.y <= boundary.MaximumY + distance;
        }

        private static bool CouldBeNear(
            Vector2 start,
            Vector2 end,
            Boundary boundary,
            float distance)
        {
            return Mathf.Max(start.x, end.x) >= boundary.MinimumX - distance
                && Mathf.Min(start.x, end.x) <= boundary.MaximumX + distance
                && Mathf.Max(start.y, end.y) >= boundary.MinimumY - distance
                && Mathf.Min(start.y, end.y) <= boundary.MaximumY + distance;
        }

        private static float DistanceSegmentToSegmentSquared(
            Vector2 firstStart,
            Vector2 firstEnd,
            Vector2 secondStart,
            Vector2 secondEnd)
        {
            if (SegmentsIntersect(
                    firstStart,
                    firstEnd,
                    secondStart,
                    secondEnd))
            {
                return 0f;
            }

            float distance = Mathf.Min(
                DistancePointToSegmentSquared(
                    firstStart,
                    secondStart,
                    secondEnd),
                DistancePointToSegmentSquared(
                    firstEnd,
                    secondStart,
                    secondEnd));
            distance = Mathf.Min(
                distance,
                DistancePointToSegmentSquared(
                    secondStart,
                    firstStart,
                    firstEnd));
            return Mathf.Min(
                distance,
                DistancePointToSegmentSquared(
                    secondEnd,
                    firstStart,
                    firstEnd));
        }

        private static bool SegmentsIntersect(
            Vector2 firstStart,
            Vector2 firstEnd,
            Vector2 secondStart,
            Vector2 secondEnd)
        {
            Vector2 first = firstEnd - firstStart;
            Vector2 second = secondEnd - secondStart;
            Vector2 between = secondStart - firstStart;
            float cross = Cross(first, second);
            float collinear = Cross(between, first);

            if (Mathf.Abs(cross) <= GeometryTolerance)
            {
                if (Mathf.Abs(collinear) > GeometryTolerance)
                {
                    return false;
                }

                float lengthSquared = first.sqrMagnitude;
                if (lengthSquared <= GeometryTolerance * GeometryTolerance)
                {
                    return (firstStart - secondStart).sqrMagnitude
                        <= GeometryTolerance * GeometryTolerance;
                }

                float start = Vector2.Dot(between, first) / lengthSquared;
                float end = start + Vector2.Dot(second, first) / lengthSquared;
                if (start > end)
                {
                    float swap = start;
                    start = end;
                    end = swap;
                }
                return end >= -GeometryTolerance
                    && start <= 1f + GeometryTolerance;
            }

            float firstTime = Cross(between, second) / cross;
            float secondTime = Cross(between, first) / cross;
            return firstTime >= -GeometryTolerance
                && firstTime <= 1f + GeometryTolerance
                && secondTime >= -GeometryTolerance
                && secondTime <= 1f + GeometryTolerance;
        }

        private static float DistancePointToSegmentSquared(
            Vector2 point,
            Vector2 start,
            Vector2 end)
        {
            Vector2 segment = end - start;
            float lengthSquared = segment.sqrMagnitude;
            if (lengthSquared <= GeometryTolerance * GeometryTolerance)
            {
                return (point - start).sqrMagnitude;
            }

            float time = Mathf.Clamp01(
                Vector2.Dot(point - start, segment) / lengthSquared);
            Vector2 nearest = start + segment * time;
            return (point - nearest).sqrMagnitude;
        }

        private static float Cross(Vector2 first, Vector2 second)
        {
            return first.x * second.y - first.y * second.x;
        }

        private static Vector2 Longer(Vector2 current, Vector2 candidate)
        {
            return candidate.sqrMagnitude > current.sqrMagnitude + GeometryTolerance
                ? candidate
                : current;
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

        private readonly struct Boundary
        {
            public Boundary(Vector2 start, Vector2 end)
            {
                Start = start;
                End = end;
                MinimumX = Mathf.Min(start.x, end.x);
                MaximumX = Mathf.Max(start.x, end.x);
                MinimumY = Mathf.Min(start.y, end.y);
                MaximumY = Mathf.Max(start.y, end.y);
            }

            public Vector2 Start { get; }
            public Vector2 End { get; }
            public float MinimumX { get; }
            public float MaximumX { get; }
            public float MinimumY { get; }
            public float MaximumY { get; }
        }
    }
}
