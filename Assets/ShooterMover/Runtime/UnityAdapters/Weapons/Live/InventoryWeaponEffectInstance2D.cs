using System;
using ShooterMover.Domain.Weapons.Execution;
using UnityEngine;

namespace ShooterMover.UnityAdapters.Weapons.Live
{
    /// <summary>
    /// Concrete emitted Unity effect. Projectile effects are configured while their
    /// atomic batch is inactive, then explicitly launched after the whole batch becomes
    /// active. Canonical DoT projectiles create a persistent pool on completion.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class InventoryWeaponEffectInstance2D : MonoBehaviour
    {
        private IWeaponEffectDescription description;
        private Vector2 origin;
        private Vector2 direction;
        private float speed;
        private float remainingSeconds;
        private Rigidbody2D body;
        private CircleCollider2D projectileCollider;
        private bool configured;
        private bool launched;
        private bool instantaneous;
        private bool completed;

        public IWeaponEffectDescription Description
        {
            get { return description; }
        }

        public WeaponEffectKind Kind
        {
            get { return description.Kind; }
        }

        public bool IsConfigured
        {
            get { return configured; }
        }

        public bool IsLaunched
        {
            get { return launched; }
        }

        public bool IsInstantaneous
        {
            get { return instantaneous; }
        }

        public bool IsCompleted
        {
            get { return completed; }
        }

        public Vector2 TravelDirection
        {
            get { return direction; }
        }

        public float TravelSpeed
        {
            get { return speed; }
        }

        public Rigidbody2D Body
        {
            get { return body; }
        }

        public Collider2D ProjectileCollider
        {
            get { return projectileCollider; }
        }

        public bool TryConfigure(IWeaponEffectDescription effect)
        {
            if (configured || effect == null || effect.Identity == null)
            {
                return false;
            }

            ChainArcEffect chain = effect as ChainArcEffect;
            if (chain != null)
            {
                return TryConfigureInstantaneous(chain);
            }

            WeaponVector2 effectOrigin;
            WeaponVector2 effectDirection;
            double effectSpeed;
            double effectRange;
            if (!TryReadProjectileTravel(
                    effect,
                    out effectOrigin,
                    out effectDirection,
                    out effectSpeed,
                    out effectRange))
            {
                return false;
            }

            Vector2 normalizedDirection = new Vector2(
                (float)effectDirection.X,
                (float)effectDirection.Y);
            if (normalizedDirection.sqrMagnitude < 0.000001f
                || effectSpeed <= 0d
                || effectRange <= 0d
                || double.IsNaN(effectSpeed)
                || double.IsInfinity(effectSpeed)
                || double.IsNaN(effectRange)
                || double.IsInfinity(effectRange))
            {
                return false;
            }

            description = effect;
            origin = new Vector2(
                (float)effectOrigin.X,
                (float)effectOrigin.Y);
            direction = normalizedDirection.normalized;
            speed = (float)effectSpeed;
            remainingSeconds = (float)(effectRange / effectSpeed);
            transform.position = new Vector3(origin.x, origin.y, 0f);

            body = gameObject.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Kinematic;
            body.gravityScale = 0f;
            body.collisionDetectionMode =
                CollisionDetectionMode2D.Continuous;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            body.simulated = false;

            projectileCollider = gameObject.AddComponent<CircleCollider2D>();
            projectileCollider.isTrigger = true;
            projectileCollider.radius = 0.05f;
            configured = true;
            return true;
        }

        /// <summary>
        /// Starts the configured effect after its complete batch is active. Exact repeat
        /// calls are accepted without restarting or changing the locked trajectory.
        /// </summary>
        public bool BeginEmission()
        {
            if (!configured || completed)
            {
                return false;
            }
            if (launched)
            {
                return true;
            }
            if (!gameObject.activeInHierarchy)
            {
                return false;
            }

            launched = true;
            if (instantaneous)
            {
                return true;
            }
            if (body == null || projectileCollider == null)
            {
                launched = false;
                return false;
            }

            body.position = origin;
            body.rotation = Mathf.Atan2(direction.y, direction.x)
                * Mathf.Rad2Deg;
            body.simulated = true;
            body.linearVelocity = direction * speed;
            return true;
        }

        private bool TryConfigureInstantaneous(ChainArcEffect chain)
        {
            Vector2 suppliedDirection = new Vector2(
                (float)chain.Direction.X,
                (float)chain.Direction.Y);
            if (suppliedDirection.sqrMagnitude < 0.000001f)
            {
                return false;
            }

            description = chain;
            origin = new Vector2(
                (float)chain.Origin.X,
                (float)chain.Origin.Y);
            direction = suppliedDirection.normalized;
            speed = 0f;
            remainingSeconds = 1f;
            instantaneous = true;
            configured = true;
            transform.position = new Vector3(origin.x, origin.y, 0f);
            return true;
        }

        private void Update()
        {
            if (!configured || !launched || completed)
            {
                return;
            }

            remainingSeconds -= Time.deltaTime;
            if (remainingSeconds <= 0f)
            {
                Complete();
            }
        }

        private void Complete()
        {
            if (completed)
            {
                return;
            }

            completed = true;
            if (body != null)
            {
                body.linearVelocity = Vector2.zero;
                body.simulated = false;
            }

            DamageOverTimeProjectileEffect dot =
                description as DamageOverTimeProjectileEffect;
            if (dot != null
                && dot.PoolRadius > 0d
                && dot.PoolDuration > 0d)
            {
                GameObject poolObject = new GameObject(
                    "WeaponPersistentPool_" + dot.Identity.FireOperationId);
                poolObject.transform.position = transform.position;
                var pool = poolObject.AddComponent<
                    InventoryWeaponPersistentDamageArea2D>();
                pool.Configure(dot);
            }

            Destroy(gameObject);
        }

        private static bool TryReadProjectileTravel(
            IWeaponEffectDescription effect,
            out WeaponVector2 effectOrigin,
            out WeaponVector2 effectDirection,
            out double effectSpeed,
            out double effectRange)
        {
            DirectProjectileEffect direct =
                effect as DirectProjectileEffect;
            if (direct != null)
            {
                effectOrigin = direct.Origin;
                effectDirection = direct.Direction;
                effectSpeed = direct.Speed;
                effectRange = direct.Range;
                return true;
            }

            ExplosiveProjectileEffect explosive =
                effect as ExplosiveProjectileEffect;
            if (explosive != null)
            {
                effectOrigin = explosive.Origin;
                effectDirection = explosive.Direction;
                effectSpeed = explosive.Speed;
                effectRange = explosive.Range;
                return true;
            }

            DamageOverTimeProjectileEffect dot =
                effect as DamageOverTimeProjectileEffect;
            if (dot != null)
            {
                effectOrigin = dot.Origin;
                effectDirection = dot.Direction;
                effectSpeed = dot.Speed;
                effectRange = dot.Range;
                return true;
            }

            effectOrigin = null;
            effectDirection = null;
            effectSpeed = 0d;
            effectRange = 0d;
            return false;
        }
    }
}
