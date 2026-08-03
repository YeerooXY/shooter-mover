using System;
using ShooterMover.Domain.Common;
using ShooterMover.EnemyRuntimeComposition;
using ShooterMover.GameplayEntities.Enemies;
using ShooterMover.UnityAdapters.Enemies;
using UnityEngine;

namespace ShooterMover.UnityAdapters.CombatPresentation
{
    /// <summary>Optional typed lifecycle seam for enemy presentation packages.</summary>
    public interface ICombatPresentationLifecycleSource
    {
        long Generation { get; }
    }

    /// <summary>
    /// Generic registration attached at the canonical enemy creation boundary. It binds one
    /// immutable runtime projection and presents accepted terminal facts without owning gameplay.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EnemyViewRegistration : MonoBehaviour
    {
        private StableId entityInstanceStableId;
        private Func<long> readLifecycleGeneration;
        private Func<EnemyLiveView> readRuntimeProjection;
        private HealthBar healthBar;
        private EnemyDeathEffects deathVfx;
        private bool configured;

        public StableId EntityInstanceStableId { get { return entityInstanceStableId; } }
        public HealthBar HealthBar { get { return healthBar; } }
        public EnemyDeathEffects DeathVfx { get { return deathVfx; } }
        public bool IsConfigured { get { return configured; } }
        public bool UsesCanonicalRuntimeProjection
        {
            get { return readRuntimeProjection != null; }
        }

        public static EnemyViewRegistration Attach(
            GameObject presentationRoot,
            Func<EnemyLiveView> readRuntime,
            DeathEffects sharedExplosionPool,
            Vector3 worldOffset,
            EnemyDeathVfxScaleConfiguration scaleConfiguration = null)
        {
            if (presentationRoot == null)
            {
                throw new ArgumentNullException(nameof(presentationRoot));
            }
            if (readRuntime == null)
            {
                throw new ArgumentNullException(nameof(readRuntime));
            }

            EnemyLiveView initial = readRuntime();
            if (initial == null)
            {
                throw new InvalidOperationException(
                    "Canonical enemy presentation requires an initial runtime projection.");
            }

            Func<long> lifecycle = delegate
            {
                EnemyLiveView current = readRuntime();
                return current == null ? -1L : current.LifecycleGeneration;
            };
            EnemyViewRegistration registration =
                presentationRoot.GetComponent<EnemyViewRegistration>();
            if (registration == null)
            {
                registration = presentationRoot
                    .AddComponent<EnemyViewRegistration>();
            }
            registration.Configure(
                initial.Identity.EntityInstanceId,
                lifecycle,
                new EnemyLiveCombatHealthSnapshotSource(
                    readRuntime,
                    CreateAnchor(initial.Identity.EntityInstanceId, worldOffset)),
                readRuntime,
                sharedExplosionPool,
                worldOffset,
                scaleConfiguration);
            return registration;
        }

        public CombatHealthBarRefreshStatus Refresh()
        {
            return healthBar == null
                ? CombatHealthBarRefreshStatus.NotConfigured
                : healthBar.Refresh();
        }

        public void SynchronizeLifecycle()
        {
            if (!configured || readLifecycleGeneration == null)
            {
                return;
            }

            long generation = readLifecycleGeneration();
            if (generation > deathVfx.LifecycleGeneration)
            {
                deathVfx.AdvanceLifecycle(generation);
                healthBar.Refresh();
            }
        }

        public void Observe(EnemyDeathFact fact)
        {
            if (!configured || fact == null)
            {
                return;
            }

            Present(EnemyTerminalPresentationFactProjector.FromCanonical(
                fact,
                transform.position,
                EnemyBounds.MeasureLargestDimension(transform)));
        }

        private void Configure(
            StableId actorId,
            Func<long> lifecycleSource,
            ICombatHealthBarSnapshotSource healthSource,
            Func<EnemyLiveView> runtimeSource,
            DeathEffects sharedExplosionPool,
            Vector3 worldOffset,
            EnemyDeathVfxScaleConfiguration scaleConfiguration)
        {
            if (actorId == null) throw new ArgumentNullException(nameof(actorId));
            if (lifecycleSource == null)
                throw new ArgumentNullException(nameof(lifecycleSource));
            if (healthSource == null) throw new ArgumentNullException(nameof(healthSource));
            if (sharedExplosionPool == null)
                throw new ArgumentNullException(nameof(sharedExplosionPool));
            if (configured)
            {
                if (entityInstanceStableId != actorId)
                {
                    throw new InvalidOperationException(
                        "An enemy presentation registration cannot change entity identity.");
                }
                return;
            }

            long generation = lifecycleSource();
            if (generation < 0L)
            {
                throw new InvalidOperationException(
                    "Enemy presentation lifecycle generation is unavailable.");
            }

            entityInstanceStableId = actorId;
            readLifecycleGeneration = lifecycleSource;
            readRuntimeProjection = runtimeSource;
            healthBar = GetComponent<HealthBar>();
            if (healthBar == null)
            {
                healthBar = gameObject.AddComponent<HealthBar>();
            }
            healthBar.Configure(actorId, healthSource, worldOffset);

            deathVfx = GetComponent<EnemyDeathEffects>();
            if (deathVfx == null)
            {
                deathVfx = gameObject.AddComponent<EnemyDeathEffects>();
            }
            deathVfx.Configure(
                actorId,
                generation,
                healthBar,
                sharedExplosionPool,
                scaleConfiguration ?? new EnemyDeathVfxScaleConfiguration());
            configured = true;
        }

        private void Present(EnemyTerminalPresentationFact fact)
        {
            if (fact == null || fact.EntityInstanceStableId != entityInstanceStableId)
            {
                return;
            }
            SynchronizeLifecycle();
            deathVfx.TryPresent(fact);
        }

        private void LateUpdate()
        {
            if (configured)
            {
                SynchronizeLifecycle();
            }
        }

        private static CombatPresentationAnchorFacts CreateAnchor(
            StableId actorId,
            Vector3 worldOffset)
        {
            return new CombatPresentationAnchorFacts(
                actorId,
                worldOffset.x,
                worldOffset.y,
                worldOffset.z);
        }
    }
}
