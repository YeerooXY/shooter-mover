using System;
using System.Collections.Generic;
using System.Globalization;
using ShooterMover.ContentPackages.Weapons.Shared.Runtime;
using ShooterMover.Domain.Common;
using ShooterMover.UnityAdapters.Combat;
using ShooterMover.UnityAdapters.Players;

namespace ShooterMover.Production.Combat
{
    /// <summary>
    /// Unity/package binding for the reusable enemy-to-player damage router. Source
    /// registration is data-driven; all enemies share the same emission and hit handlers.
    /// </summary>
    public sealed class EnemyProjectileDamageSourceBinderV1 : IDisposable
    {
        private readonly EnemyToPlayerDamageRouterV1 router;
        private readonly HashSet<ProjectileExecutionPlanAdapter> projectileSources =
            new HashSet<ProjectileExecutionPlanAdapter>();
        private readonly Dictionary<BoundedProjectile2D, StableId> trackedProjectiles =
            new Dictionary<BoundedProjectile2D, StableId>();
        private bool disposed;

        public EnemyProjectileDamageSourceBinderV1(
            EnemyToPlayerDamageRouterV1 router)
        {
            this.router = router
                ?? throw new ArgumentNullException(nameof(router));
        }

        public int RegisteredProjectileSourceCount
        {
            get { return projectileSources.Count; }
        }

        public bool RegisterSource(
            CombatHit2DAdapter hitAdapter,
            ProjectileExecutionPlanAdapter projectileAdapter,
            double damage)
        {
            if (disposed || hitAdapter == null || projectileAdapter == null)
            {
                return false;
            }

            EnemyDamageSourceRegistrationStatus registration =
                router.RegisterDamageSource(hitAdapter, damage);
            if (registration != EnemyDamageSourceRegistrationStatus.Registered
                && registration
                    != EnemyDamageSourceRegistrationStatus.AlreadyRegistered)
            {
                return false;
            }

            if (projectileSources.Add(projectileAdapter))
            {
                projectileAdapter.ProjectileSpawned += HandleProjectileSpawned;
            }
            return true;
        }

        public void ClearLifecycle()
        {
            foreach (KeyValuePair<BoundedProjectile2D, StableId> pair
                in trackedProjectiles)
            {
                if (pair.Key != null)
                {
                    pair.Key.Completed -= HandleProjectileCompleted;
                }
            }
            trackedProjectiles.Clear();
            router.ClearLifecycle();
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            foreach (ProjectileExecutionPlanAdapter source in projectileSources)
            {
                if (source != null)
                {
                    source.ProjectileSpawned -= HandleProjectileSpawned;
                }
            }
            projectileSources.Clear();
            ClearLifecycle();
        }

        public static bool TryResolveLifecycleGeneration(
            StableId combatEventId,
            out long generation)
        {
            generation = -1L;
            if (combatEventId == null
                || string.IsNullOrEmpty(combatEventId.Value))
            {
                return false;
            }

            string value = combatEventId.Value;
            string[] tokens =
            {
                "generation-",
                "attempt-",
                "-g",
                ".g",
                "_g",
            };
            for (int index = 0; index < tokens.Length; index++)
            {
                int start = value.IndexOf(tokens[index], StringComparison.Ordinal);
                while (start >= 0)
                {
                    int digitStart = start + tokens[index].Length;
                    int digitEnd = digitStart;
                    while (digitEnd < value.Length
                        && char.IsDigit(value[digitEnd]))
                    {
                        digitEnd++;
                    }
                    if (digitEnd > digitStart
                        && long.TryParse(
                            value.Substring(digitStart, digitEnd - digitStart),
                            NumberStyles.None,
                            CultureInfo.InvariantCulture,
                            out generation)
                        && generation >= 0L)
                    {
                        return true;
                    }
                    start = value.IndexOf(
                        tokens[index],
                        start + 1,
                        StringComparison.Ordinal);
                }
            }

            generation = -1L;
            return false;
        }

        private void HandleProjectileSpawned(
            ProjectileExecutionEmission2D emission)
        {
            if (disposed
                || emission == null
                || emission.Projectile == null
                || emission.HitEventId == null)
            {
                return;
            }

            long generation;
            if (!TryResolveLifecycleGeneration(
                    emission.CombatEventId,
                    out generation))
            {
                return;
            }

            EnemyDamageAdmissionObservationStatus observation =
                router.ObserveEmission(
                    new EnemyProjectileEmissionFactV1(
                        emission.HitEventId,
                        generation));
            if (observation
                    == EnemyDamageAdmissionObservationStatus.InvalidInput
                || observation
                    == EnemyDamageAdmissionObservationStatus.ConflictingDuplicate
                || observation
                    == EnemyDamageAdmissionObservationStatus.Disposed)
            {
                return;
            }

            StableId previousHitEvent;
            if (trackedProjectiles.TryGetValue(
                    emission.Projectile,
                    out previousHitEvent)
                && previousHitEvent != emission.HitEventId)
            {
                router.RetireEmission(previousHitEvent);
            }

            trackedProjectiles[emission.Projectile] = emission.HitEventId;
            emission.Projectile.Completed -= HandleProjectileCompleted;
            emission.Projectile.Completed += HandleProjectileCompleted;
        }

        private void HandleProjectileCompleted(BoundedProjectile2D projectile)
        {
            if (projectile == null)
            {
                return;
            }

            projectile.Completed -= HandleProjectileCompleted;
            StableId hitEventId;
            if (trackedProjectiles.TryGetValue(projectile, out hitEventId))
            {
                trackedProjectiles.Remove(projectile);
                router.RetireEmission(hitEventId);
            }
        }
    }
}
