using System;
using System.Collections.Generic;
using ShooterMover.ContentPackages.Weapons.Shared.Runtime;
using ShooterMover.Contracts.Combat;
using ShooterMover.UnityAdapters.Combat;
using UnityEngine;

namespace ShooterMover.ContentPackages.Props.DestructibleProps
{
    /// <summary>
    /// Optional bounded bridge for WP-002 projectiles. It waits for the projectile's
    /// existing CombatHit2DAdapter translation and forwards only confirmed HitMessage values.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider2D))]
    public sealed class DestructiblePropProjectileRelay2D : MonoBehaviour
    {
        private readonly Dictionary<int, BoundedProjectile2D> observedProjectiles =
            new Dictionary<int, BoundedProjectile2D>();
        private DestructibleProp2D target;
        private double confirmedHitDamage;
        private bool configured;

        public bool IsConfigured
        {
            get { return configured; }
        }

        public double ConfirmedHitDamage
        {
            get { return confirmedHitDamage; }
        }

        public void Configure(
            DestructibleProp2D configuredTarget,
            double configuredConfirmedHitDamage)
        {
            if (configured)
            {
                throw new InvalidOperationException(
                    "Destructible prop projectile relay is already configured.");
            }

            if (configuredTarget == null)
            {
                throw new ArgumentNullException(nameof(configuredTarget));
            }

            if (double.IsNaN(configuredConfirmedHitDamage)
                || double.IsInfinity(configuredConfirmedHitDamage)
                || configuredConfirmedHitDamage <= 0d)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(configuredConfirmedHitDamage));
            }

            target = configuredTarget;
            confirmedHitDamage = configuredConfirmedHitDamage;
            configured = true;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            Observe(other);
        }

        private void OnCollisionEnter2D(Collision2D collision)
        {
            if (collision != null)
            {
                Observe(collision.collider);
            }
        }

        private void Observe(Collider2D other)
        {
            if (!configured || target == null || other == null)
            {
                return;
            }

            BoundedProjectile2D projectile =
                other.GetComponentInParent<BoundedProjectile2D>();
            if (projectile == null || !projectile.IsInitialized)
            {
                return;
            }

            if (projectile.IsComplete)
            {
                Consume(projectile);
                return;
            }

            int instanceId = projectile.GetInstanceID();
            if (observedProjectiles.ContainsKey(instanceId))
            {
                return;
            }

            observedProjectiles.Add(instanceId, projectile);
            projectile.Completed += OnProjectileCompleted;
        }

        private void OnProjectileCompleted(BoundedProjectile2D projectile)
        {
            if (projectile != null)
            {
                projectile.Completed -= OnProjectileCompleted;
                observedProjectiles.Remove(projectile.GetInstanceID());
            }

            Consume(projectile);
        }

        private void Consume(BoundedProjectile2D projectile)
        {
            if (!configured || target == null || projectile == null)
            {
                return;
            }

            CombatHit2DTranslationResult translation = projectile.LastHitTranslation;
            HitMessage message = translation == null ? null : translation.Message;
            if (translation == null
                || translation.Status != CombatHit2DTranslationStatus.Confirmed
                || message == null
                || message.Result != HitResult.Confirmed)
            {
                return;
            }

            target.TryApplyConfirmedHit(message, confirmedHitDamage);
        }

        private void OnDisable()
        {
            foreach (BoundedProjectile2D projectile in observedProjectiles.Values)
            {
                if (projectile != null)
                {
                    projectile.Completed -= OnProjectileCompleted;
                }
            }

            observedProjectiles.Clear();
        }
    }
}
