using System;
using System.Collections.Generic;
using System.Text;
using ShooterMover.Domain.Common;
using ShooterMover.UnityAdapters.Combat;
using UnityEngine;

namespace ShooterMover.ContentPackages.Props.DestructibleProps
{
    /// <summary>
    /// Runtime-owned set that restores all attached props when the host restart generation changes.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DestructiblePropSet2D : MonoBehaviour
    {
        private readonly List<DestructibleProp2D> props = new List<DestructibleProp2D>();
        private Func<long> restartGenerationSource;
        private long observedRestartGeneration;
        private bool configured;

        public int PropCount
        {
            get { return props.Count; }
        }

        public long ObservedRestartGeneration
        {
            get { return observedRestartGeneration; }
        }

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
                throw new ArgumentNullException(nameof(configuredRestartGenerationSource));
            }

            props.Clear();
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
                    "At least one destructible prop is required.",
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
    /// Bounded Stage 1 composition helper. It scans only the supplied presentation and collider
    /// roots, attaches the generic destructible target/relay components, and registers each
    /// collider with the already-owned CombatHit2DAdapter.
    /// </summary>
    public static class Stage1DestructiblePropIntegration
    {
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
                    "The Stage 1 destructible prop integration is already attached.");
            }

            List<DestructibleProp2D> attached = new List<DestructibleProp2D>();
            for (int index = 0; index < presentationRoot.childCount; index++)
            {
                Transform visual = presentationRoot.GetChild(index);
                DestructiblePropAuthoring2D authoring =
                    visual.GetComponent<DestructiblePropAuthoring2D>();
                double maximumHealth;
                if (authoring != null)
                {
                    maximumHealth = authoring.MaximumHealth;
                }
                else if (visual.name.StartsWith("Crate_", StringComparison.Ordinal))
                {
                    maximumHealth = CrateMaximumHealth;
                }
                else if (visual.name.StartsWith("Explosive_", StringComparison.Ordinal))
                {
                    maximumHealth = ExplosiveMaximumHealth;
                }
                else
                {
                    continue;
                }

                Transform colliderTransform =
                    colliderRoot.Find(visual.name + "_Collision");
                if (colliderTransform == null)
                {
                    throw new InvalidOperationException(
                        "Missing grid-aligned prop collider for " + visual.name + ".");
                }

                Collider2D blockingCollider =
                    colliderTransform.GetComponent<Collider2D>();
                if (blockingCollider == null)
                {
                    throw new InvalidOperationException(
                        "Grid-aligned prop collider object has no Collider2D: "
                        + colliderTransform.name + ".");
                }

                StableId propId = CreatePropId(visual.name, index);
                attached.Add(AttachOne(
                    colliderTransform.gameObject,
                    visual.gameObject,
                    blockingCollider,
                    hitAdapter,
                    propId,
                    maximumHealth,
                    confirmedHitDamage,
                    authoring == null ? null : authoring.DestructionAnimation));
            }

            DestructiblePropSet2D set =
                owner.AddComponent<DestructiblePropSet2D>();
            set.Configure(attached, restartGenerationSource);
            return set;
        }

        public static StableId CreatePropId(string visualName, int siblingIndex)
        {
            if (visualName == null)
            {
                throw new ArgumentNullException(nameof(visualName));
            }

            if (siblingIndex < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(siblingIndex));
            }

            string normalized = NormalizeValue(visualName);
            string value = "stage1-" + siblingIndex + "-" + normalized;
            if (value.Length > StableId.MaxValueLength)
            {
                value = value.Substring(0, StableId.MaxValueLength).TrimEnd('-');
            }

            return StableId.Create("prop", value);
        }

        private static DestructibleProp2D AttachOne(
            GameObject colliderObject,
            GameObject presentationRoot,
            Collider2D blockingCollider,
            CombatHit2DAdapter hitAdapter,
            StableId propId,
            double maximumHealth,
            double confirmedHitDamage,
            DestructiblePropDestructionAnimation destructionAnimation)
        {
            DestructibleProp2D prop =
                colliderObject.AddComponent<DestructibleProp2D>();
            prop.Configure(
                propId,
                maximumHealth,
                blockingCollider,
                presentationRoot);

            CombatHit2DTargetRegistrationStatus registration =
                hitAdapter.RegisterTarget(blockingCollider, propId);
            if (registration != CombatHit2DTargetRegistrationStatus.Registered
                && registration
                    != CombatHit2DTargetRegistrationStatus.AlreadyRegistered)
            {
                throw new InvalidOperationException(
                    "Could not register destructible prop target "
                    + propId
                    + ": "
                    + registration);
            }

            DestructiblePropProjectileRelay2D relay =
                colliderObject.AddComponent<DestructiblePropProjectileRelay2D>();
            relay.Configure(prop, confirmedHitDamage);

            DestructiblePropDestructionPlayer2D player =
                colliderObject.AddComponent<DestructiblePropDestructionPlayer2D>();
            player.Configure(
                prop,
                presentationRoot.transform,
                destructionAnimation);
            return prop;
        }

        private static string NormalizeValue(string text)
        {
            StringBuilder builder = new StringBuilder(text.Length);
            bool previousWasSeparator = false;
            for (int index = 0; index < text.Length; index++)
            {
                char current = char.ToLowerInvariant(text[index]);
                bool isLetter = current >= 'a' && current <= 'z';
                bool isDigit = current >= '0' && current <= '9';
                if (isLetter || isDigit)
                {
                    builder.Append(current);
                    previousWasSeparator = false;
                }
                else if (builder.Length > 0 && !previousWasSeparator)
                {
                    builder.Append('-');
                    previousWasSeparator = true;
                }
            }

            while (builder.Length > 0 && builder[builder.Length - 1] == '-')
            {
                builder.Length--;
            }

            return builder.Length == 0 ? "unnamed" : builder.ToString();
        }
    }
}
