using System;
using System.Collections.Generic;
using ShooterMover.Application.Missions.Rooms.Content;
using ShooterMover.Domain.Common;
using UnityEngine;

namespace ShooterMover.UI.Game
{
    /// <summary>
    /// Applies the final movement limit after walking and dashing have chosen a velocity.
    /// Only authored Tile-layer cells count as floor. Floor-looking art on another layer
    /// remains non-walkable.
    /// </summary>
    [DefaultExecutionOrder(1200)]
    [DisallowMultipleComponent]
    public sealed class PlayerFloorGuard : MonoBehaviour
    {
        private const float SolidSkin = 0.01f;
        private const float MovementTolerance = 0.000001f;

        private readonly List<Vector2Int> roomCells = new List<Vector2Int>();
        private readonly RaycastHit2D[] solidHits = new RaycastHit2D[16];

        private Rigidbody2D body;
        private CircleCollider2D bodyCollider;
        private FloorGrid floor;
        private ContactFilter2D solidFilter;
        private Vector2 lastValidPosition;
        private bool hasLastValidPosition;
        private bool bound;

        public void Bind(
            Rigidbody2D playerBody,
            CircleCollider2D playerCollider)
        {
            if (bound)
            {
                throw new InvalidOperationException(
                    "player-floor-guard-duplicate-binding");
            }

            body = playerBody
                ?? throw new ArgumentNullException(nameof(playerBody));
            bodyCollider = playerCollider
                ?? throw new ArgumentNullException(nameof(playerCollider));
            if (!ReferenceEquals(body.gameObject, bodyCollider.gameObject))
            {
                throw new InvalidOperationException(
                    "player-floor-guard-collider-must-share-body");
            }

            solidFilter = ContactFilter2D.NoFilter();
            solidFilter.useTriggers = false;
            bound = true;
        }

        public void LoadRoom(
            RoomContentBundle roomContent,
            StableId roomStableId,
            Vector2 requestedPosition)
        {
            if (!bound)
            {
                throw new InvalidOperationException(
                    "player-floor-guard-not-bound");
            }
            if (roomContent == null)
            {
                throw new ArgumentNullException(nameof(roomContent));
            }
            if (roomStableId == null)
            {
                throw new ArgumentNullException(nameof(roomStableId));
            }

            roomCells.Clear();
            IReadOnlyList<RoomVisualPlacementContent> visuals = roomContent.Visuals;
            for (int index = 0; index < visuals.Count; index++)
            {
                RoomVisualPlacementContent visual = visuals[index];
                if (visual == null
                    || visual.RoomStableId != roomStableId
                    || visual.Layer != RoomContentVisualLayer.Tile)
                {
                    continue;
                }

                float x = (float)visual.LocalPosition.X;
                float y = (float)visual.LocalPosition.Y;
                int cellX = Mathf.RoundToInt(x);
                int cellY = Mathf.RoundToInt(y);
                if (Mathf.Abs(x - cellX) > 0.001f
                    || Mathf.Abs(y - cellY) > 0.001f)
                {
                    throw new InvalidOperationException(
                        "playable-level-floor-cell-off-grid:" + roomStableId);
                }
                roomCells.Add(new Vector2Int(cellX, cellY));
            }

            LoadFloor(roomCells, requestedPosition);
        }

        public void LoadFloor(
            IEnumerable<Vector2Int> floorCells,
            Vector2 requestedPosition)
        {
            if (!bound)
            {
                throw new InvalidOperationException(
                    "player-floor-guard-not-bound");
            }

            floor = new FloorGrid(floorCells);
            RoomFloor.Set(floor);
            body.linearVelocity = Vector2.zero;
            if (!floor.HasCells)
            {
                body.position = requestedPosition;
                lastValidPosition = requestedPosition;
                hasLastValidPosition = true;
                return;
            }

            Vector2 centerOffset = ResolveCenterOffset();
            float radius = ResolveRadius();
            Vector2 requestedCenter = requestedPosition + centerOffset;
            Vector2 acceptedPosition = requestedPosition;
            if (!floor.FitsCircle(requestedCenter, radius))
            {
                Vector2 nearestCenter;
                if (!floor.TryFindNearestCellCenter(
                        requestedCenter,
                        radius,
                        out nearestCenter))
                {
                    throw new InvalidOperationException(
                        "playable-level-floor-has-no-player-position");
                }
                acceptedPosition = nearestCenter - centerOffset;
            }

            body.position = acceptedPosition;
            lastValidPosition = acceptedPosition;
            hasLastValidPosition = true;
        }

        public void ApplyMovement(float fixedDeltaTime)
        {
            if (!bound || body == null || floor == null) return;

            if (!floor.HasCells)
            {
                HoldLastPosition();
                return;
            }

            Vector2 requestedVelocity = body.linearVelocity;
            Vector2 centerOffset = ResolveCenterOffset();
            float radius = ResolveRadius();
            Vector2 currentPosition = body.position;
            Vector2 currentCenter = currentPosition + centerOffset;
            if (!floor.FitsCircle(currentCenter, radius))
            {
                if (!TryRestoreValidPosition(
                        centerOffset,
                        radius,
                        currentCenter,
                        out currentPosition,
                        out currentCenter))
                {
                    body.linearVelocity = Vector2.zero;
                    return;
                }
            }

            lastValidPosition = currentPosition;
            hasLastValidPosition = true;
            body.linearVelocity = ChooseSafeVelocity(
                currentCenter,
                requestedVelocity,
                fixedDeltaTime,
                radius);
        }

        private void FixedUpdate()
        {
            ApplyMovement(Time.fixedDeltaTime);
        }

        private Vector2 ChooseSafeVelocity(
            Vector2 center,
            Vector2 requestedVelocity,
            float fixedDeltaTime,
            float radius)
        {
            Vector2 accepted = LimitCandidate(
                center,
                requestedVelocity,
                fixedDeltaTime,
                radius);
            Vector2 horizontal = LimitCandidate(
                center,
                new Vector2(requestedVelocity.x, 0f),
                fixedDeltaTime,
                radius);
            Vector2 vertical = LimitCandidate(
                center,
                new Vector2(0f, requestedVelocity.y),
                fixedDeltaTime,
                radius);

            if (Mathf.Abs(requestedVelocity.x) >= Mathf.Abs(requestedVelocity.y))
            {
                accepted = Longer(accepted, horizontal);
                accepted = Longer(accepted, vertical);
            }
            else
            {
                accepted = Longer(accepted, vertical);
                accepted = Longer(accepted, horizontal);
            }

            return accepted;
        }

        private Vector2 LimitCandidate(
            Vector2 center,
            Vector2 velocity,
            float fixedDeltaTime,
            float radius)
        {
            Vector2 floorSafe = floor.LimitVelocity(
                center,
                velocity,
                fixedDeltaTime,
                radius);
            return LimitAgainstSolidColliders(
                floorSafe,
                fixedDeltaTime);
        }

        private Vector2 LimitAgainstSolidColliders(
            Vector2 velocity,
            float fixedDeltaTime)
        {
            if (fixedDeltaTime <= 0f
                || velocity.sqrMagnitude <= MovementTolerance)
            {
                return Vector2.zero;
            }

            Vector2 displacement = velocity * fixedDeltaTime;
            float distance = displacement.magnitude;
            if (distance <= MovementTolerance)
            {
                return Vector2.zero;
            }

            Vector2 direction = displacement / distance;
            int hitCount = body.Cast(
                direction,
                solidFilter,
                solidHits,
                distance + SolidSkin);
            float allowedDistance = distance;
            for (int index = 0; index < hitCount; index++)
            {
                RaycastHit2D hit = solidHits[index];
                if (hit.collider == null
                    || hit.collider.isTrigger
                    || ReferenceEquals(hit.collider, bodyCollider)
                    || ReferenceEquals(hit.rigidbody, body))
                {
                    continue;
                }

                // A real wall can already be touching or slightly overlapping the player
                // after the previous physics solve. Do not let a zero-distance cast block
                // movement that clearly points away from that wall.
                if (hit.distance <= SolidSkin
                    && Vector2.Dot(direction, hit.normal) > 0f)
                {
                    continue;
                }

                allowedDistance = Mathf.Min(
                    allowedDistance,
                    Mathf.Max(0f, hit.distance - SolidSkin));
            }

            if (allowedDistance <= MovementTolerance)
            {
                return Vector2.zero;
            }

            return direction * (allowedDistance / fixedDeltaTime);
        }

        private bool TryRestoreValidPosition(
            Vector2 centerOffset,
            float radius,
            Vector2 currentCenter,
            out Vector2 restoredPosition,
            out Vector2 restoredCenter)
        {
            if (hasLastValidPosition)
            {
                Vector2 lastCenter = lastValidPosition + centerOffset;
                if (floor.FitsCircle(lastCenter, radius))
                {
                    restoredPosition = lastValidPosition;
                    restoredCenter = lastCenter;
                    body.position = restoredPosition;
                    return true;
                }
            }

            Vector2 nearestCenter;
            if (floor.TryFindNearestCellCenter(
                    currentCenter,
                    radius,
                    out nearestCenter))
            {
                restoredPosition = nearestCenter - centerOffset;
                restoredCenter = nearestCenter;
                body.position = restoredPosition;
                return true;
            }

            restoredPosition = body.position;
            restoredCenter = currentCenter;
            return false;
        }

        private void HoldLastPosition()
        {
            if (hasLastValidPosition)
            {
                body.position = lastValidPosition;
            }
            body.linearVelocity = Vector2.zero;
        }

        private Vector2 ResolveCenterOffset()
        {
            Vector3 worldCenter = bodyCollider.transform.TransformPoint(
                bodyCollider.offset);
            return new Vector2(worldCenter.x, worldCenter.y) - body.position;
        }

        private float ResolveRadius()
        {
            Vector3 scale = bodyCollider.transform.lossyScale;
            float largestScale = Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y));
            return Mathf.Max(0.01f, bodyCollider.radius * largestScale);
        }

        private static Vector2 Longer(Vector2 current, Vector2 candidate)
        {
            return candidate.sqrMagnitude > current.sqrMagnitude + MovementTolerance
                ? candidate
                : current;
        }
    }
}
