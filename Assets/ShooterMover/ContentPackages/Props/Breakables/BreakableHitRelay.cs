using System;
using System.Collections.Generic;
using ShooterMover.ContentPackages.Guns.Shared.Runtime;
using ShooterMover.Contracts.Combat;
using ShooterMover.UnityAdapters.Combat;
using UnityEngine;

namespace ShooterMover.ContentPackages.Props.Breakables
{
    /// <summary>
    /// Optional bounded bridge for WP-002 projectiles. It waits for the projectile's
    /// existing HitResolver translation and forwards only confirmed HitMessage values.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider2D))]
    public sealed class BreakableHitRelay : MonoBehaviour
    {
        private readonly Dictionary<int, BoundedProjectile> observedProjectiles =
            new Dictionary<int, BoundedProjectile>();
        private Breakable target;
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
            Breakable configuredTarget,
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

            BoundedProjectile projectile =
                other.GetComponentInParent<BoundedProjectile>();
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

        private void OnProjectileCompleted(BoundedProjectile projectile)
        {
            if (projectile != null)
            {
                projectile.Completed -= OnProjectileCompleted;
                observedProjectiles.Remove(projectile.GetInstanceID());
            }

            Consume(projectile);
        }

        private void Consume(BoundedProjectile projectile)
        {
            if (!configured || target == null || projectile == null)
            {
                return;
            }

            HitTranslationResult translation = projectile.LastHitTranslation;
            HitMessage message = translation == null ? null : translation.Message;
            if (translation == null
                || translation.Status != HitTranslationStatus.Confirmed
                || message == null
                || message.Result != HitResult.Confirmed)
            {
                return;
            }

            target.TryApplyConfirmedHit(message, confirmedHitDamage);
        }

        private void OnDisable()
        {
            foreach (BoundedProjectile projectile in observedProjectiles.Values)
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
