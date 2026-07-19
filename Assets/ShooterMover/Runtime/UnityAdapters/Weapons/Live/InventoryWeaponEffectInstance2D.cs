using System;
using System.Collections.Generic;
using ShooterMover.Application.Weapons.Execution;
using ShooterMover.Domain.Weapons.Execution;
using UnityEngine;

namespace ShooterMover.UnityAdapters.Weapons.Live
{
    /// <summary>
    /// Concrete emitted Unity effect. Projectile effects travel for range/speed seconds;
    /// canonical DoT projectiles create a persistent pool on completion when configured.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class InventoryWeaponEffectInstance2D : MonoBehaviour
    {
        private IWeaponEffectDescription description;
        private Vector2 direction;
        private float speed;
        private float remainingSeconds;
        private bool configured;
        private bool completed;

        public IWeaponEffectDescription Description { get { return description; } }
        public WeaponEffectKind Kind { get { return description.Kind; } }
        public bool IsConfigured { get { return configured; } }
        public bool IsCompleted { get { return completed; } }

        public bool TryConfigure(IWeaponEffectDescription effect)
        {
            if (configured || effect == null || effect.Identity == null)
            {
                return false;
            }

            WeaponVector2 origin;
            WeaponVector2 effectDirection;
            double effectSpeed;
            double effectRange;
            if (!TryReadTravel(
                    effect,
                    out origin,
                    out effectDirection,
                    out effectSpeed,
                    out effectRange))
            {
                return false;
            }

            description = effect;
            transform.position = new Vector3((float)origin.X, (float)origin.Y, 0f);
            direction = new Vector2(
                (float)effectDirection.X,
                (float)effectDirection.Y).normalized;
            speed = (float)effectSpeed;
            remainingSeconds = speed <= 0f ? 0f : (float)(effectRange / effectSpeed);
            configured = true;

            var body = gameObject.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Kinematic;
            body.gravityScale = 0f;
            body.linearVelocity = direction * speed;
            var collider = gameObject.AddComponent<CircleCollider2D>();
            collider.isTrigger = true;
            collider.radius = 0.05f;
            return true;
        }

        private void Update()
        {
            if (!configured || completed)
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
            DamageOverTimeProjectileEffect dot =
                description as DamageOverTimeProjectileEffect;
            if (dot != null && dot.PoolRadius > 0d && dot.PoolDuration > 0d)
            {
                GameObject poolObject = new GameObject(
                    "WeaponPersistentPool_" + dot.Identity.FireOperationId);
                poolObject.transform.position = transform.position;
                var pool = poolObject.AddComponent<InventoryWeaponPersistentDamageArea2D>();
                pool.Configure(dot);
            }

            Destroy(gameObject);
        }

        private static bool TryReadTravel(
            IWeaponEffectDescription effect,
            out WeaponVector2 origin,
            out WeaponVector2 direction,
            out double speed,
            out double range)
        {
            DirectProjectileEffect direct = effect as DirectProjectileEffect;
            if (direct != null)
            {
                origin = direct.Origin;
                direction = direct.Direction;
                speed = direct.Speed;
                range = direct.Range;
                return true;
            }

            ExplosiveProjectileEffect explosive = effect as ExplosiveProjectileEffect;
            if (explosive != null)
            {
                origin = explosive.Origin;
                direction = explosive.Direction;
                speed = explosive.Speed;
                range = explosive.Range;
                return true;
            }

            DamageOverTimeProjectileEffect dot =
                effect as DamageOverTimeProjectileEffect;
            if (dot != null)
            {
                origin = dot.Origin;
                direction = dot.Direction;
                speed = dot.Speed;
                range = dot.Range;
                return true;
            }

            ChainArcEffect chain = effect as ChainArcEffect;
            if (chain != null)
            {
                origin = chain.Origin;
                direction = chain.Direction;
                speed = chain.MaximumRange;
                range = chain.MaximumRange;
                return true;
            }

            origin = null;
            direction = null;
            speed = 0d;
            range = 0d;
            return false;
        }
    }

}
