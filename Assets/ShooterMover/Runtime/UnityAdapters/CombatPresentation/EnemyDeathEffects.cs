using System;
using System.Collections.Generic;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Enemies;
using ShooterMover.EnemyRuntimeComposition;
using UnityEngine;

namespace ShooterMover.UnityAdapters.CombatPresentation
{
    public sealed class EnemyTerminalPresentationFact
    {
        public EnemyTerminalPresentationFact(
            StableId terminalEventStableId,
            StableId entityInstanceStableId,
            long lifecycleGeneration,
            Vector3 finalWorldPosition,
            float largestPresentationDimension)
        {
            TerminalEventStableId = terminalEventStableId
                ?? throw new ArgumentNullException(nameof(terminalEventStableId));
            EntityInstanceStableId = entityInstanceStableId
                ?? throw new ArgumentNullException(nameof(entityInstanceStableId));
            if (lifecycleGeneration < 0L)
            {
                throw new ArgumentOutOfRangeException(nameof(lifecycleGeneration));
            }
            if (!IsFinite(finalWorldPosition)
                || !IsFinitePositive(largestPresentationDimension))
            {
                throw new ArgumentOutOfRangeException(nameof(largestPresentationDimension));
            }
            LifecycleGeneration = lifecycleGeneration;
            FinalWorldPosition = finalWorldPosition;
            LargestPresentationDimension = largestPresentationDimension;
        }

        public StableId TerminalEventStableId { get; }
        public StableId EntityInstanceStableId { get; }
        public long LifecycleGeneration { get; }
        public Vector3 FinalWorldPosition { get; }
        public float LargestPresentationDimension { get; }

        private static bool IsFinite(Vector3 value)
        {
            return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static bool IsFinitePositive(float value)
        {
            return IsFinite(value) && value > 0f;
        }
    }

    public static class EnemyTerminalPresentationFactProjector
    {
        public static EnemyTerminalPresentationFact FromCanonical(
            EnemyDeathFact fact,
            Vector3 finalWorldPosition,
            float largestPresentationDimension)
        {
            if (fact == null) throw new ArgumentNullException(nameof(fact));
            return new EnemyTerminalPresentationFact(
                fact.DeathEventStableId,
                fact.Identity.EntityInstanceId,
                fact.LifecycleGeneration,
                finalWorldPosition,
                largestPresentationDimension);
        }

        /// <summary>
        /// Transitional adapter for current EN-002 Unity packages. New factory-created enemies
        /// should project the canonical EnemyDeathFact overload instead.
        /// </summary>
        public static EnemyTerminalPresentationFact FromLegacy(
            EnemyDestroyedNotification notification,
            long lifecycleGeneration,
            Vector3 finalWorldPosition,
            float largestPresentationDimension)
        {
            if (notification == null)
            {
                throw new ArgumentNullException(nameof(notification));
            }
            return new EnemyTerminalPresentationFact(
                notification.EventId,
                notification.TargetId,
                lifecycleGeneration,
                finalWorldPosition,
                largestPresentationDimension);
        }
    }

    public sealed class EnemyDeathVfxScaleConfiguration
    {
        public EnemyDeathVfxScaleConfiguration(
            float referenceSize = 1f,
            float minimumScale = 0.75f,
            float maximumScale = 2.25f)
        {
            if (!IsFinitePositive(referenceSize)
                || !IsFinitePositive(minimumScale)
                || !IsFinitePositive(maximumScale)
                || minimumScale > maximumScale)
            {
                throw new ArgumentOutOfRangeException(nameof(referenceSize));
            }
            ReferenceSize = referenceSize;
            MinimumScale = minimumScale;
            MaximumScale = maximumScale;
        }

        public float ReferenceSize { get; }
        public float MinimumScale { get; }
        public float MaximumScale { get; }

        public float Resolve(float largestPresentationDimension)
        {
            if (!IsFinitePositive(largestPresentationDimension))
            {
                throw new ArgumentOutOfRangeException(nameof(largestPresentationDimension));
            }
            return Mathf.Clamp(
                largestPresentationDimension / ReferenceSize,
                MinimumScale,
                MaximumScale);
        }

        private static bool IsFinitePositive(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value) && value > 0f;
        }
    }

    public enum EnemyDeathVfxPresentationStatus
    {
        Spawned = 1,
        ExactReplay = 2,
        RejectedWrongEntity = 3,
        RejectedStaleLifecycle = 4,
        RejectedFutureLifecycle = 5,
        RejectedInvalid = 6,
        NotConfigured = 7,
    }

    /// <summary>
    /// Consumes immutable accepted enemy terminal facts and projects one shared visual.
    /// It creates no hit, damage, room, kill, experience, drop, or persistence fact.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EnemyDeathEffects : MonoBehaviour
    {
        private readonly HashSet<string> acceptedFacts =
            new HashSet<string>(StringComparer.Ordinal);
        private StableId boundEntityStableId;
        private long lifecycleGeneration = -1L;
        private HealthBar healthBar;
        private DeathEffects explosionPool;
        private EnemyDeathVfxScaleConfiguration scaleConfiguration;
        private bool configured;

        public StableId BoundEntityStableId { get { return boundEntityStableId; } }
        public long LifecycleGeneration { get { return lifecycleGeneration; } }
        public int SpawnedCount { get; private set; }
        public float LastResolvedScale { get; private set; }

        public void Configure(
            StableId entityInstanceStableId,
            long initialLifecycleGeneration,
            HealthBar boundHealthBar,
            DeathEffects sharedExplosionPool,
            EnemyDeathVfxScaleConfiguration configuredScale = null)
        {
            if (entityInstanceStableId == null)
            {
                throw new ArgumentNullException(nameof(entityInstanceStableId));
            }
            if (initialLifecycleGeneration < 0L)
            {
                throw new ArgumentOutOfRangeException(nameof(initialLifecycleGeneration));
            }
            if (boundHealthBar == null)
            {
                throw new ArgumentNullException(nameof(boundHealthBar));
            }
            if (sharedExplosionPool == null)
            {
                throw new ArgumentNullException(nameof(sharedExplosionPool));
            }
            if (configured
                && (boundEntityStableId != entityInstanceStableId
                    || !object.ReferenceEquals(healthBar, boundHealthBar)
                    || !object.ReferenceEquals(explosionPool, sharedExplosionPool)))
            {
                throw new InvalidOperationException(
                    "An enemy death VFX presenter cannot be rebound to another entity or source.");
            }

            boundEntityStableId = entityInstanceStableId;
            lifecycleGeneration = initialLifecycleGeneration;
            healthBar = boundHealthBar;
            explosionPool = sharedExplosionPool;
            scaleConfiguration = configuredScale ?? new EnemyDeathVfxScaleConfiguration();
            configured = true;
        }

        public bool AdvanceLifecycle(long replacementLifecycleGeneration)
        {
            if (!configured
                || replacementLifecycleGeneration <= lifecycleGeneration)
            {
                return false;
            }
            lifecycleGeneration = replacementLifecycleGeneration;
            acceptedFacts.Clear();
            return true;
        }

        public EnemyDeathVfxPresentationStatus TryPresent(
            EnemyTerminalPresentationFact fact)
        {
            if (!configured)
            {
                return EnemyDeathVfxPresentationStatus.NotConfigured;
            }
            if (fact == null)
            {
                return EnemyDeathVfxPresentationStatus.RejectedInvalid;
            }
            if (fact.EntityInstanceStableId != boundEntityStableId)
            {
                return EnemyDeathVfxPresentationStatus.RejectedWrongEntity;
            }
            if (fact.LifecycleGeneration < lifecycleGeneration)
            {
                return EnemyDeathVfxPresentationStatus.RejectedStaleLifecycle;
            }
            if (fact.LifecycleGeneration > lifecycleGeneration)
            {
                return EnemyDeathVfxPresentationStatus.RejectedFutureLifecycle;
            }

            string ledgerKey = fact.LifecycleGeneration + "|" + fact.TerminalEventStableId;
            if (acceptedFacts.Contains(ledgerKey))
            {
                return EnemyDeathVfxPresentationStatus.ExactReplay;
            }

            float scale = scaleConfiguration.Resolve(fact.LargestPresentationDimension);
            healthBar.Clear();
            explosionPool.Spawn(fact.FinalWorldPosition, scale);
            acceptedFacts.Add(ledgerKey);
            SpawnedCount++;
            LastResolvedScale = scale;
            return EnemyDeathVfxPresentationStatus.Spawned;
        }
    }

    public static class EnemyBounds
    {
        public static float MeasureLargestDimension(Transform presentationRoot)
        {
            if (presentationRoot == null)
            {
                throw new ArgumentNullException(nameof(presentationRoot));
            }

            bool found = false;
            Bounds combined = new Bounds(presentationRoot.position, Vector3.zero);
            Renderer[] renderers = presentationRoot.GetComponentsInChildren<Renderer>(true);
            for (int index = 0; index < renderers.Length; index++)
            {
                Renderer renderer = renderers[index];
                if (renderer == null
                    || renderer is LineRenderer
                    || renderer.GetComponentInParent<GeneratedCombatVisual>() != null)
                {
                    continue;
                }
                if (!found)
                {
                    combined = renderer.bounds;
                    found = true;
                }
                else
                {
                    combined.Encapsulate(renderer.bounds);
                }
            }

            if (!found)
            {
                Collider2D[] colliders = presentationRoot.GetComponentsInChildren<Collider2D>(true);
                for (int index = 0; index < colliders.Length; index++)
                {
                    Collider2D collider = colliders[index];
                    if (collider == null) continue;
                    if (!found)
                    {
                        combined = collider.bounds;
                        found = true;
                    }
                    else
                    {
                        combined.Encapsulate(collider.bounds);
                    }
                }
            }

            if (!found)
            {
                Vector3 scale = presentationRoot.lossyScale;
                return Mathf.Max(0.01f, Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y)));
            }

            return Mathf.Max(0.01f, Mathf.Max(combined.size.x, combined.size.y));
        }
    }
}
