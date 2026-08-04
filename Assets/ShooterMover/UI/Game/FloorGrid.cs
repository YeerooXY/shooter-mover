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
        private const float SweepStep = 0.1f;
        private const int MaximumSweepSteps = 512;
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
            int requestedSteps = RequiredSweepSteps(displacement);
            if (requestedSteps > MaximumSweepSteps)
            {
                return Vector2.zero;
            }

            // Rigidbody2D receives one velocity for the entire physics step, so every
            // candidate below is swept as the same straight path physics will follow.
            // A simulated two-part route must never be collapsed into a corner-cutting
            // diagonal velocity.
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

            int steps = RequiredSweepSteps(displacement);
            Vector2 step = displacement / steps;
            Vector2 accepted = Vector2.zero;
            for (int index = 0; index < steps; index++)
            {
                Vector2 candidate = center + accepted + step;
                if (!FitsCircle(candidate, radius)) break;
                accepted += step;
            }
            return accepted;
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

        private static int RequiredSweepSteps(Vector2 displacement)
        {
            return Mathf.Max(
                1,
                Mathf.CeilToInt(displacement.magnitude / SweepStep));
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
            float minX = cell.x - TileHalfSize;
            float maxX = cell.x + TileHalfSize;
            float minY = cell.y - TileHalfSize;
            float maxY = cell.y + TileHalfSize;
            float closestX = Mathf.Clamp(center.x, minX, maxX);
            float closestY = Mathf.Clamp(center.y, minY, maxY);
            float dx = center.x - closestX;
            float dy = center.y - closestY;
            float safeRadius = Mathf.Max(0f, radius - GeometryTolerance);
            return dx * dx + dy * dy < safeRadius * safeRadius;
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
