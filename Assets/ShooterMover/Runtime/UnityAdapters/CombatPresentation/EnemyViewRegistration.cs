using System;
using System.Reflection;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Enemies;
using ShooterMover.EnemyRuntimeComposition;
using ShooterMover.GameplayEntities.Enemies;
using ShooterMover.UnityAdapters.Enemies;
using UnityEngine;

namespace ShooterMover.UnityAdapters.CombatPresentation
{
    /// <summary>Optional typed lifecycle seam preferred over reflective package discovery.</summary>
    public interface ICombatPresentationLifecycleSource
    {
        long Generation { get; }
    }

    /// <summary>
    /// Generic registration attached at the enemy creation/registration boundary. It binds one
    /// immutable health source and one accepted-terminal consumer without package-name switches.
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
            IEnemyState authority,
            DeathEffects sharedExplosionPool,
            Vector3 worldOffset,
            EnemyDeathVfxScaleConfiguration scaleConfiguration = null)
        {
            if (presentationRoot == null)
            {
                throw new ArgumentNullException(nameof(presentationRoot));
            }
            if (authority == null)
            {
                throw new ArgumentNullException(nameof(authority));
            }

            EnemyActorState initialState;
            if (!authority.TryReadState(out initialState) || initialState == null)
            {
                throw new InvalidOperationException(
                    "Generic enemy presentation requires an immutable initial actor state.");
            }

            Func<long> lifecycle = ResolveLifecycleSource(presentationRoot, authority);
            EnemyViewRegistration registration =
                presentationRoot.GetComponent<EnemyViewRegistration>();
            if (registration == null)
            {
                registration = presentationRoot
                    .AddComponent<EnemyViewRegistration>();
            }
            registration.Configure(
                initialState.ActorId,
                lifecycle,
                new EnemyActorCombatHealthSnapshotSource(
                    initialState.ActorId,
                    lifecycle,
                    authority.TryReadState,
                    CreateAnchor(initialState.ActorId, worldOffset)),
                null,
                sharedExplosionPool,
                worldOffset,
                scaleConfiguration);
            return registration;
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

        /// <summary>Transitional EN-002 adapter used by current Unity packages.</summary>
        public void Observe(EnemyActorStepResult result)
        {
            if (!configured || result == null)
            {
                return;
            }
            for (int index = 0; index < result.Notifications.Count; index++)
            {
                EnemyDestroyedNotification destruction =
                    result.Notifications[index] as EnemyDestroyedNotification;
                if (destruction == null) continue;
                Present(EnemyTerminalPresentationFactProjector.FromLegacy(
                    destruction,
                    readLifecycleGeneration(),
                    transform.position,
                    EnemyBounds.MeasureLargestDimension(transform)));
                return;
            }
        }

        /// <summary>Canonical factory-runtime terminal path.</summary>
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

        private static Func<long> ResolveLifecycleSource(
            GameObject root,
            IEnemyState authority)
        {
            MonoBehaviour[] components = root.GetComponentsInChildren<MonoBehaviour>(true);
            for (int index = 0; index < components.Length; index++)
            {
                MonoBehaviour component = components[index];
                if (component == null) continue;

                ICombatPresentationLifecycleSource typed =
                    component as ICombatPresentationLifecycleSource;
                if (typed != null && OwnsAuthority(component, authority))
                {
                    return () => typed.Generation;
                }

                PropertyInfo generation = component.GetType().GetProperty(
                    "Generation",
                    BindingFlags.Public | BindingFlags.Instance);
                if (generation == null
                    || generation.PropertyType != typeof(long)
                    || generation.GetIndexParameters().Length != 0
                    || !OwnsAuthority(component, authority))
                {
                    continue;
                }
                return delegate
                {
                    object value = generation.GetValue(component, null);
                    return value is long ? (long)value : -1L;
                };
            }

            throw new InvalidOperationException(
                "The generic enemy registration exposes no lifecycle-generation source.");
        }

        private static bool OwnsAuthority(
            MonoBehaviour component,
            IEnemyState authority)
        {
            if (object.ReferenceEquals(component as IEnemyState, authority))
            {
                return true;
            }
            PropertyInfo property = component.GetType().GetProperty(
                "Authority",
                BindingFlags.Public | BindingFlags.Instance);
            if (property == null
                || property.GetIndexParameters().Length != 0
                || !typeof(IEnemyState).IsAssignableFrom(property.PropertyType))
            {
                return false;
            }
            return object.ReferenceEquals(property.GetValue(component, null), authority);
        }
    }
}
