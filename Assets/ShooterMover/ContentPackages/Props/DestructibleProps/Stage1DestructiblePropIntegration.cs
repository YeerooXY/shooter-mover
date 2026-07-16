using System;
using System.Collections.Generic;
using ShooterMover.UnityAdapters.Combat;
using UnityEngine;

namespace ShooterMover.ContentPackages.Props.DestructibleProps
{
    /// <summary>
    /// Runtime-owned set retained as a migration seam for hosts that expose a restart generation.
    /// New authored props also participate in the generic OBJ-001 restart lifecycle.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DestructiblePropSet2D : MonoBehaviour
    {
        private readonly List<DestructibleProp2D> props =
            new List<DestructibleProp2D>();
        private Func<long> restartGenerationSource;
        private long observedRestartGeneration;
        private bool configured;

        public int PropCount => props.Count;
        public long ObservedRestartGeneration => observedRestartGeneration;

        internal void Configure(
            IEnumerable<DestructibleProp2D> configuredProps,
            Func<long> configuredRestartGenerationSource)
        {
            if (configured)
            {
                throw new InvalidOperationException(
                    "Destructible prop set is already configured.");
            }

            if (configuredProps == null)
            {
                throw new ArgumentNullException(nameof(configuredProps));
            }

            if (configuredRestartGenerationSource == null)
            {
                throw new ArgumentNullException(
                    nameof(configuredRestartGenerationSource));
            }

            foreach (DestructibleProp2D prop in configuredProps)
            {
                if (prop != null && !props.Contains(prop))
                {
                    props.Add(prop);
                }
            }

            if (props.Count == 0)
            {
                throw new ArgumentException(
                    "At least one configured destructible prop is required.",
                    nameof(configuredProps));
            }

            restartGenerationSource = configuredRestartGenerationSource;
            observedRestartGeneration = restartGenerationSource();
            configured = true;
        }

        public void RestartAll()
        {
            if (!configured)
            {
                return;
            }

            for (int index = 0; index < props.Count; index++)
            {
                DestructibleProp2D prop = props[index];
                if (prop != null)
                {
                    prop.Restart();
                }
            }
        }

        private void LateUpdate()
        {
            if (!configured || restartGenerationSource == null)
            {
                return;
            }

            long currentGeneration = restartGenerationSource();
            if (currentGeneration == observedRestartGeneration)
            {
                return;
            }

            observedRestartGeneration = currentGeneration;
            RestartAll();
        }

        private void OnDestroy()
        {
            restartGenerationSource = null;
            props.Clear();
        }
    }

    /// <summary>
    /// Legacy host entry point. It no longer derives identity, health, collision, or
    /// presentation from hierarchy names. Every discovered authoring component resolves
    /// explicit OBJ-001 identity and definition data before it is admitted.
    /// </summary>
    public static class Stage1DestructiblePropIntegration
    {
        // Kept only for source compatibility with earlier regression tests and migration tools.
        public const double CrateMaximumHealth = 24d;
        public const double ExplosiveMaximumHealth = 12d;

        public static DestructiblePropSet2D Attach(
            GameObject owner,
            Transform presentationRoot,
            Transform colliderRoot,
            CombatHit2DAdapter hitAdapter,
            double confirmedHitDamage,
            Func<long> restartGenerationSource)
        {
            if (owner == null)
            {
                throw new ArgumentNullException(nameof(owner));
            }

            if (presentationRoot == null)
            {
                throw new ArgumentNullException(nameof(presentationRoot));
            }

            if (colliderRoot == null)
            {
                throw new ArgumentNullException(nameof(colliderRoot));
            }

            if (hitAdapter == null)
            {
                throw new ArgumentNullException(nameof(hitAdapter));
            }

            if (double.IsNaN(confirmedHitDamage)
                || double.IsInfinity(confirmedHitDamage)
                || confirmedHitDamage <= 0d)
            {
                throw new ArgumentOutOfRangeException(nameof(confirmedHitDamage));
            }

            if (restartGenerationSource == null)
            {
                throw new ArgumentNullException(nameof(restartGenerationSource));
            }

            if (owner.GetComponent<DestructiblePropSet2D>() != null)
            {
                throw new InvalidOperationException(
                    "Destructible prop integration is already attached.");
            }

            DestructiblePropAuthoring2D[] authored =
                presentationRoot.GetComponentsInChildren<DestructiblePropAuthoring2D>(true);
            Array.Sort(
                authored,
                (left, right) => left.GetInstanceID().CompareTo(right.GetInstanceID()));

            List<DestructibleProp2D> attached =
                new List<DestructibleProp2D>(authored.Length);
            for (int index = 0; index < authored.Length; index++)
            {
                DestructiblePropAuthoring2D authoring = authored[index];
                if (authoring == null)
                {
                    continue;
                }

                authoring.ApplyLegacyConfirmedHitDamage(confirmedHitDamage);
                DestructiblePropConfigurationResult result =
                    authoring.TryConfigure(hitAdapter);
                if (!result.IsConfigured || result.RuntimeProp == null)
                {
                    throw new InvalidOperationException(
                        "Destructible prop authoring failed closed: " + result.Diagnostic);
                }

                attached.Add(result.RuntimeProp);
            }

            DestructiblePropSet2D set =
                owner.AddComponent<DestructiblePropSet2D>();
            set.Configure(attached, restartGenerationSource);
            return set;
        }
    }
}
