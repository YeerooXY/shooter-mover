using System;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Enemies.Catalog;
using ShooterMover.EnemyRuntimeComposition;
using ShooterMover.UnityAdapters.Missions.Rooms;
using UnityEngine;

namespace ShooterMover.UnityAdapters.Enemies
{
    /// <summary>
    /// Reusable Unity realization for one supported travelling enemy projectile. Direct payloads
    /// publish one target contact. Instantaneous area payloads complete at target, obstruction,
    /// or maximum range and delegate radius admission to the owning attack driver.
    /// </summary>
    [DisallowMultipleComponent]
    internal sealed class EnemyShot2D : MonoBehaviour
    {
        private EnemyAttack2D owner;
        private RoomEnemyActor2D source;
        private EnemyAttack2D.PlayerBinding target;
        private EnemyAttackEffectEmissionV1 emission;
        private Rigidbody2D body;
        private Vector2 direction;
        private Vector2 origin;
        private double distance;
        private bool ended;

        public void Bind(
            EnemyAttack2D configuredOwner,
            RoomEnemyActor2D configuredSource,
            EnemyAttack2D.PlayerBinding configuredTarget,
            EnemyAttackEffectEmissionV1 configuredEmission,
            Vector2 configuredDirection,
            EnemyProjectilePayloadV1 payload,
            Sprite sprite)
        {
            owner = configuredOwner ?? throw new ArgumentNullException(nameof(configuredOwner));
            source = configuredSource ?? throw new ArgumentNullException(nameof(configuredSource));
            target = configuredTarget ?? throw new ArgumentNullException(nameof(configuredTarget));
            emission = configuredEmission ?? throw new ArgumentNullException(nameof(configuredEmission));
            if (payload == null) throw new ArgumentNullException(nameof(payload));
            if (sprite == null) throw new ArgumentNullException(nameof(sprite));
            if (configuredDirection.sqrMagnitude <= 0.000001f)
            {
                throw new ArgumentOutOfRangeException(nameof(configuredDirection));
            }

            direction = configuredDirection.normalized;
            origin = transform.position;
            distance = payload.MaximumTravelDistance;

            SpriteRenderer renderer = gameObject.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = payload.AreaPayload == null
                ? new Color(1f, 0.72f, 0.12f, 1f)
                : new Color(1f, 0.28f, 0.08f, 1f);
            renderer.sortingOrder = 300;
            float diameter = Mathf.Max(0.12f, (float)payload.CollisionRadius * 2f);
            transform.localScale = new Vector3(diameter, diameter, 1f);

            CircleCollider2D collider = gameObject.AddComponent<CircleCollider2D>();
            collider.radius = 0.5f;
            collider.isTrigger = true;

            body = gameObject.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Kinematic;
            body.gravityScale = 0f;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            body.simulated = true;
            body.rotation = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            enabled = true;
        }

        private void FixedUpdate()
        {
            if (ended) return;
            if (owner == null
                || source == null
                || !source.IsBound
                || !source.IsAlive
                || target == null
                || !target.IsCurrent(gameObject.scene))
            {
                End(CompletionKind.Cancelled, null, body == null ? (Vector2)transform.position : body.position);
                return;
            }

            EnemyProjectilePayloadV1 payload = emission.Projectile.Payload;
            Vector2 next = body.position
                + direction * (float)payload.Speed * Time.fixedDeltaTime;
            body.MovePosition(next);
            if (Vector2.Distance(origin, next) >= distance)
            {
                End(CompletionKind.MaximumRange, null, next);
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (ended || other == null) return;
            Vector2 point = body == null ? (Vector2)transform.position : body.position;
            if (target != null && target.IsTarget(other))
            {
                End(CompletionKind.TargetContact, other, ClosestPoint(other, point));
                return;
            }
            if (source != null
                && (other.transform == source.transform
                    || other.transform.IsChildOf(source.transform)))
            {
                return;
            }
            if (!other.isTrigger)
            {
                End(CompletionKind.Obstruction, null, ClosestPoint(other, point));
            }
        }

        public void Cancel()
        {
            End(
                CompletionKind.Cancelled,
                null,
                body == null ? (Vector2)transform.position : body.position);
        }

        private void End(
            CompletionKind completion,
            Collider2D targetCollider,
            Vector2 completionPoint)
        {
            if (ended) return;
            ended = true;
            StableId id = emission == null ? null : emission.EmissionStableId;
            if (completion != CompletionKind.Cancelled
                && owner != null
                && emission != null)
            {
                EnemyAreaPayloadV1 area = emission.Projectile.Payload.AreaPayload;
                if (area != null)
                {
                    owner.PublishArea(emission, completionPoint, area);
                }
                else if (completion == CompletionKind.TargetContact
                    && targetCollider != null)
                {
                    owner.Publish(emission, targetCollider);
                }
            }
            if (owner != null)
            {
                owner.Ended(id);
            }
            Destroy(gameObject);
        }

        private static Vector2 ClosestPoint(Collider2D collider, Vector2 fallback)
        {
            if (collider == null || !collider.enabled) return fallback;
            Vector2 point = collider.ClosestPoint(fallback);
            return Finite(point) ? point : fallback;
        }

        private static bool Finite(Vector2 value)
        {
            return !float.IsNaN(value.x)
                && !float.IsInfinity(value.x)
                && !float.IsNaN(value.y)
                && !float.IsInfinity(value.y);
        }

        private void OnDestroy()
        {
            if (!ended && owner != null && emission != null)
            {
                owner.Ended(emission.EmissionStableId);
            }
            ended = true;
            owner = null;
            source = null;
            target = null;
            emission = null;
            body = null;
        }

        private enum CompletionKind
        {
            Cancelled = 0,
            TargetContact = 1,
            Obstruction = 2,
            MaximumRange = 3,
        }
    }
}
