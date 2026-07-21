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
    public interface ICombatPresentationLifecycleSourceV1
    {
        long Generation { get; }
    }

    /// <summary>
    /// Generic registration attached at the enemy creation/registration boundary. It binds one
    /// immutable health source and one accepted-terminal consumer without package-name switches.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CombatEnemyPresentationRegistration2D : MonoBehaviour
    {
        private StableId entityInstanceStableId;
        private Func<long> readLifecycleGeneration;
        private Func<EnemyRuntimeProjection> readRuntimeProjection;
        private CombatHealthBarPresenter2D healthBar;
        private EnemyDeathVfxPresenter2D deathVfx;
        private bool configured;

        public StableId EntityInstanceStableId { get { return entityInstanceStableId; } }
        public CombatHealthBarPresenter2D HealthBar { get { return healthBar; } }
        public EnemyDeathVfxPresenter2D DeathVfx { get { return deathVfx; } }
        public bool IsConfigured { get { return configured; } }
        public bool UsesCanonicalRuntimeProjection
        {
            get { return readRuntimeProjection != null; }
        }

        public static CombatEnemyPresentationRegistration2D Attach(
            GameObject presentationRoot,
            IEnemyActor2DAuthority authority,
            CombatDeathVfxPool2D sharedExplosionPool,
            Vector3 worldOffset,
            EnemyDeathVfxScaleConfigurationV1 scaleConfiguration = null)
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
            CombatEnemyPresentationRegistration2D registration =
                presentationRoot.GetComponent<CombatEnemyPresentationRegistration2D>();
            if (registration == null)
            {
                registration = presentationRoot
                    .AddComponent<CombatEnemyPresentationRegistration2D>();
            }
            registration.Configure(
                initialState.ActorId,
                lifecycle,
                new EnemyActorCombatHealthSnapshotSourceV1(
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

        public static CombatEnemyPresentationRegistration2D Attach(
            GameObject presentationRoot,
            Func<EnemyRuntimeProjection> readRuntime,
            CombatDeathVfxPool2D sharedExplosionPool,
            Vector3 worldOffset,
            EnemyDeathVfxScaleConfigurationV1 scaleConfiguration = null)
        {
            if (presentationRoot == null)
            {
                throw new ArgumentNullException(nameof(presentationRoot));
            }
            if (readRuntime == null)
            {
                throw new ArgumentNullException(nameof(readRuntime));
            }
            EnemyRuntimeProjection initial = readRuntime();
            if (initial == null)
            {
                throw new InvalidOperationException(
                    "Canonical enemy presentation requires an initial runtime projection.");
            }

            Func<long> lifecycle = delegate
            {
                EnemyRuntimeProjection current = readRuntime();
                return current == null ? -1L : current.LifecycleGeneration;
            };
            CombatEnemyPresentationRegistration2D registration =
                presentationRoot.GetComponent<CombatEnemyPresentationRegistration2D>();
            if (registration == null)
            {
                registration = presentationRoot
                    .AddComponent<CombatEnemyPresentationRegistration2D>();
            }
            registration.Configure(
                initial.Identity.EntityInstanceId,
                lifecycle,
                new EnemyRuntimeCombatHealthSnapshotSourceV1(
                    readRuntime,
                    CreateAnchor(initial.Identity.EntityInstanceId, worldOffset)),
                readRuntime,
                sharedExplosionPool,
                worldOffset,
                scaleConfiguration);
            return registration;
        }

        public CombatHealthBarRefreshStatusV1 Refresh()
        {
            return healthBar == null
                ? CombatHealthBarRefreshStatusV1.NotConfigured
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
                Present(EnemyTerminalPresentationFactProjectorV1.FromLegacy(
                    destruction,
                    readLifecycleGeneration(),
                    transform.position,
                    EnemyPresentationBounds2D.MeasureLargestDimension(transform)));
                return;
            }
        }

        /// <summary>Canonical factory-runtime terminal path.</summary>
        public void Observe(EnemyDeathFactV1 fact)
        {
            if (!configured || fact == null)
            {
                return;
            }
            Present(EnemyTerminalPresentationFactProjectorV1.FromCanonical(
                fact,
                transform.position,
                EnemyPresentationBounds2D.MeasureLargestDimension(transform)));
        }

        private void Configure(
            StableId actorId,
            Func<long> lifecycleSource,
            ICombatHealthBarSnapshotSourceV1 healthSource,
            Func<EnemyRuntimeProjection> runtimeSource,
            CombatDeathVfxPool2D sharedExplosionPool,
            Vector3 worldOffset,
            EnemyDeathVfxScaleConfigurationV1 scaleConfiguration)
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
            healthBar = GetComponent<CombatHealthBarPresenter2D>();
            if (healthBar == null)
            {
                healthBar = gameObject.AddComponent<CombatHealthBarPresenter2D>();
            }
            healthBar.Configure(actorId, healthSource, worldOffset);

            deathVfx = GetComponent<EnemyDeathVfxPresenter2D>();
            if (deathVfx == null)
            {
                deathVfx = gameObject.AddComponent<EnemyDeathVfxPresenter2D>();
            }
            deathVfx.Configure(
                actorId,
                generation,
                healthBar,
                sharedExplosionPool,
                scaleConfiguration ?? new EnemyDeathVfxScaleConfigurationV1());
            configured = true;
        }

        private void Present(EnemyTerminalPresentationFactV1 fact)
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

        private static CombatPresentationAnchorFactsV1 CreateAnchor(
            StableId actorId,
            Vector3 worldOffset)
        {
            return new CombatPresentationAnchorFactsV1(
                actorId,
                worldOffset.x,
                worldOffset.y,
                worldOffset.z);
        }

        private static Func<long> ResolveLifecycleSource(
            GameObject root,
            IEnemyActor2DAuthority authority)
        {
            MonoBehaviour[] components = root.GetComponentsInChildren<MonoBehaviour>(true);
            for (int index = 0; index < components.Length; index++)
            {
                MonoBehaviour component = components[index];
                if (component == null) continue;

                ICombatPresentationLifecycleSourceV1 typed =
                    component as ICombatPresentationLifecycleSourceV1;
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
            IEnemyActor2DAuthority authority)
        {
            if (object.ReferenceEquals(component as IEnemyActor2DAuthority, authority))
            {
                return true;
            }
            PropertyInfo property = component.GetType().GetProperty(
                "Authority",
                BindingFlags.Public | BindingFlags.Instance);
            if (property == null
                || property.GetIndexParameters().Length != 0
                || !typeof(IEnemyActor2DAuthority).IsAssignableFrom(property.PropertyType))
            {
                return false;
            }
            return object.ReferenceEquals(property.GetValue(component, null), authority);
        }
    }
}
