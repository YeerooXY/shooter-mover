using System;
using UnityEngine;

namespace ShooterMover.UI.Game.Enemies
{
    /// <summary>
    /// Keeps enemies on the same authored walkable floor as the player without creating
    /// colliders. Missing-floor and no-move cells block movement but not projectiles.
    /// </summary>
    [DefaultExecutionOrder(1300)]
    [DisallowMultipleComponent]
    public sealed class EnemyFloorRules : MonoBehaviour
    {
        private const float PositionToleranceSquared = 0.000001f;

        private Rigidbody2D body;
        private CircleCollider2D bodyCollider;
        private Vector2 lastValidPosition;
        private int floorRevision = -1;
        private bool hasLastValidPosition;
        private bool bound;

        public void Bind(
            Rigidbody2D enemyBody,
            CircleCollider2D enemyCollider)
        {
            if (bound)
            {
                if (!ReferenceEquals(body, enemyBody)
                    || !ReferenceEquals(bodyCollider, enemyCollider))
                {
                    throw new InvalidOperationException(
                        "enemy-floor-rules-rebound");
                }
                return;
            }

            body = enemyBody
                ?? throw new ArgumentNullException(nameof(enemyBody));
            bodyCollider = enemyCollider
                ?? throw new ArgumentNullException(nameof(enemyCollider));
            if (!ReferenceEquals(body.gameObject, bodyCollider.gameObject))
            {
                throw new InvalidOperationException(
                    "enemy-floor-rules-collider-must-share-body");
            }

            bound = true;
        }

        public void ApplyMovement()
        {
            if (!bound || body == null || bodyCollider == null)
            {
                return;
            }

            FloorGrid floor;
            int currentRevision;
            if (!RoomFloor.TryGet(out floor, out currentRevision)
                || !floor.HasCells)
            {
                return;
            }

            Vector2 centerOffset = ResolveCenterOffset();
            float radius = ResolveRadius();
            Vector2 currentPosition = body.position;
            Vector2 currentCenter = currentPosition + centerOffset;

            if (floorRevision != currentRevision)
            {
                floorRevision = currentRevision;
                hasLastValidPosition = false;
            }

            if (!hasLastValidPosition)
            {
                if (floor.FitsCircle(currentCenter, radius))
                {
                    lastValidPosition = currentPosition;
                    hasLastValidPosition = true;
                    return;
                }

                Vector2 nearestCenter;
                if (!floor.TryFindNearestCellCenter(
                        currentCenter,
                        radius,
                        out nearestCenter))
                {
                    body.linearVelocity = Vector2.zero;
                    return;
                }

                Vector2 nearestPosition = nearestCenter - centerOffset;
                body.position = nearestPosition;
                body.linearVelocity = Vector2.zero;
                lastValidPosition = nearestPosition;
                hasLastValidPosition = true;
                return;
            }

            Vector2 lastCenter = lastValidPosition + centerOffset;
            if (!floor.FitsCircle(lastCenter, radius))
            {
                hasLastValidPosition = false;
                ApplyMovement();
                return;
            }

            Vector2 requestedDisplacement = currentCenter - lastCenter;
            Vector2 acceptedDisplacement = floor.LimitVelocity(
                lastCenter,
                requestedDisplacement,
                1f,
                radius);
            Vector2 acceptedPosition =
                lastValidPosition + acceptedDisplacement;

            if ((acceptedPosition - currentPosition).sqrMagnitude
                > PositionToleranceSquared)
            {
                body.position = acceptedPosition;
                body.linearVelocity = Vector2.zero;
            }

            lastValidPosition = acceptedPosition;
        }

        private void FixedUpdate()
        {
            ApplyMovement();
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
            float largestScale = Mathf.Max(
                Mathf.Abs(scale.x),
                Mathf.Abs(scale.y));
            return Mathf.Max(
                0.01f,
                bodyCollider.radius * largestScale);
        }
    }
}
