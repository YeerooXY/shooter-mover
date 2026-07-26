using System;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Enemies.Catalog;
using ShooterMover.EnemyRuntimeComposition;
using ShooterMover.UnityAdapters.Missions.Rooms;
using UnityEngine;

namespace ShooterMover.UnityAdapters.Enemies
{
    /// <summary>
    /// Reusable Unity presentation for one supported simple travelling enemy shot.
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
            renderer.color = new Color(1f, 0.72f, 0.12f, 1f);
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
                End(false, null);
                return;
            }

            EnemyProjectilePayloadV1 payload = emission.Projectile.Payload;
            Vector2 next = body.position
                + direction * (float)payload.Speed * Time.fixedDeltaTime;
            body.MovePosition(next);
            if (Vector2.Distance(origin, next) >= distance)
            {
                End(false, null);
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (ended || other == null) return;
            if (target != null && target.IsTarget(other))
            {
                End(true, other);
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
                End(false, null);
            }
        }

        public void Cancel()
        {
            End(false, null);
        }

        private void End(bool hit, Collider2D targetCollider)
        {
            if (ended) return;
            ended = true;
            StableId id = emission == null ? null : emission.EmissionStableId;
            if (hit && owner != null && emission != null && targetCollider != null)
            {
                owner.Publish(emission, targetCollider);
            }
            if (owner != null)
            {
                owner.Ended(id);
            }
            Destroy(gameObject);
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
    }
}
