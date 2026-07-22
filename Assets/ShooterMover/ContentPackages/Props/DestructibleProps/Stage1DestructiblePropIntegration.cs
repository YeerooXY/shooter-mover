using System;
using System.Collections.Generic;
using System.Globalization;
using ShooterMover.Domain.Common;
using ShooterMover.UnityAdapters.Authoring;
using ShooterMover.UnityAdapters.Combat;
using UnityEngine;

namespace ShooterMover.ContentPackages.Props.DestructibleProps
{
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
                throw new InvalidOperationException(
                    "Destructible prop set is already configured.");
            if (configuredProps == null)
                throw new ArgumentNullException(nameof(configuredProps));
            if (configuredRestartGenerationSource == null)
                throw new ArgumentNullException(
                    nameof(configuredRestartGenerationSource));

            foreach (DestructibleProp2D prop in configuredProps)
            {
                if (prop != null && !props.Contains(prop)) props.Add(prop);
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
            if (!configured) return;
            for (int index = 0; index < props.Count; index++)
            {
                DestructibleProp2D prop = props[index];
                if (prop != null) prop.Restart();
            }
        }

        private void LateUpdate()
        {
            if (!configured || restartGenerationSource == null) return;
            long currentGeneration = restartGenerationSource();
            if (currentGeneration == observedRestartGeneration) return;
            observedRestartGeneration = currentGeneration;
            RestartAll();
        }

        private void OnDestroy()
        {
            restartGenerationSource = null;
            props.Clear();
        }
    }

    public static class Stage1DestructiblePropIntegration
    {
        public const double CrateMaximumHealth = 24d;
        public const double ExplosiveMaximumHealth = 12d;

        private const float LegacyPositionToleranceSquared = 0.000001f;
        private const float LegacyRotationToleranceDegrees = 0.01f;

        public static DestructiblePropSet2D Attach(
            GameObject owner,
            Transform presentationRoot,
            Transform colliderRoot,
            CombatHit2DAdapter hitAdapter,
            double confirmedHitDamage,
            Func<long> restartGenerationSource)
        {
            ValidateArguments(
                owner,
                presentationRoot,
                colliderRoot,
                hitAdapter,
                confirmedHitDamage,
                restartGenerationSource);

            if (owner.GetComponent<DestructiblePropSet2D>() != null)
                throw new InvalidOperationException(
                    "Destructible prop integration is already attached.");

            DestructiblePropAuthoring2D[] authored =
                presentationRoot.GetComponentsInChildren<DestructiblePropAuthoring2D>(true);
            Array.Sort(
                authored,
                (left, right) => left.GetInstanceID().CompareTo(right.GetInstanceID()));

            var attached = new List<DestructibleProp2D>(authored.Length);
            var legacy = new List<DestructiblePropAuthoring2D>();
            var consumedColliders = new HashSet<Collider2D>();
            var consumedLegacyIds = new HashSet<StableId>();

            for (int index = 0; index < authored.Length; index++)
            {
                DestructiblePropAuthoring2D authoring = authored[index];
                if (authoring == null) continue;

                authoring.ApplyLegacyConfirmedHitDamage(confirmedHitDamage);
                DestructiblePropConfigurationResult result =
                    authoring.TryConfigure(hitAdapter);
                if (result.IsConfigured && result.RuntimeProp != null)
                {
                    attached.Add(result.RuntimeProp);
                    if (result.RuntimeProp.BlockingCollider != null)
                        consumedColliders.Add(result.RuntimeProp.BlockingCollider);
                    continue;
                }

                if (result.Status == DestructiblePropConfigurationStatus.MissingPlacedObject
                    && authoring.GetComponent<PlacedObjectAuthoring2D>() == null)
                {
                    legacy.Add(authoring);
                    continue;
                }

                throw new InvalidOperationException(
                    "Destructible prop authoring failed closed: " + result.Diagnostic);
            }

            Collider2D[] candidateColliders =
                colliderRoot.GetComponentsInChildren<Collider2D>(true);
            for (int index = 0; index < legacy.Count; index++)
            {
                DestructiblePropAuthoring2D authoring = legacy[index];
                Collider2D collider = ResolveLegacyCollider(
                    authoring,
                    presentationRoot,
                    candidateColliders,
                    consumedColliders);
                SpriteRenderer renderer = authoring.GetComponent<SpriteRenderer>();
                if (renderer == null)
                    throw new InvalidOperationException(
                        "Legacy destructible prop marker requires a co-located SpriteRenderer.");

                StableId propId = CreateLegacyPropId(authoring);
                if (!consumedLegacyIds.Add(propId))
                {
                    throw new InvalidOperationException(
                        "Legacy destructible prop identity collision for '" + propId
                        + "'. Give the placements distinct authored positions or migrate them "
                        + "to explicit PlacedObjectAuthoring2D identities.");
                }

                DestructibleProp2D prop = AttachLegacyOne(
                    collider,
                    renderer,
                    authoring,
                    hitAdapter,
                    propId,
                    confirmedHitDamage);
                consumedColliders.Add(collider);
                attached.Add(prop);
            }

            DestructiblePropSet2D set = owner.AddComponent<DestructiblePropSet2D>();
            set.Configure(attached, restartGenerationSource);
            return set;
        }

        public static StableId CreateLegacyPropId(DestructiblePropAuthoring2D authoring)
        {
            if (authoring == null)
                throw new ArgumentNullException(nameof(authoring));
            Vector3 position = authoring.transform.position;
            Vector2 size = authoring.ColliderSize;
            Vector2 offset = authoring.ColliderOffset;
            string canonical = "position=" + Vector(position)
                + "|health=" + authoring.MaximumHealth.ToString(
                    "R",
                    CultureInfo.InvariantCulture)
                + "|size=" + Vector(size)
                + "|offset=" + Vector(offset);
            return StableId.Create("prop", "legacy-" + Fingerprint64(canonical));
        }

        private static DestructibleProp2D AttachLegacyOne(
            Collider2D blockingCollider,
            SpriteRenderer renderer,
            DestructiblePropAuthoring2D authoring,
            CombatHit2DAdapter hitAdapter,
            StableId propId,
            double confirmedHitDamage)
        {
            DestructiblePropTerminalProvenanceV1 provenance =
                authoring.GeneratedTerminalProvenance;
            if (provenance == null)
            {
                throw new InvalidOperationException(
                    "Legacy destructible prop terminal provenance is missing.");
            }

            DestructibleProp2D prop =
                blockingCollider.GetComponent<DestructibleProp2D>()
                ?? blockingCollider.gameObject.AddComponent<DestructibleProp2D>();
            prop.Configure(
                propId,
                authoring.MaximumHealth,
                blockingCollider,
                new Renderer[] { renderer },
                DestructiblePropDestroyedCollisionPolicy.Disable,
                provenance);

            CombatHit2DTargetRegistrationStatus registration =
                hitAdapter.RegisterTarget(blockingCollider, propId);
            if (registration != CombatHit2DTargetRegistrationStatus.Registered
                && registration != CombatHit2DTargetRegistrationStatus.AlreadyRegistered)
            {
                throw new InvalidOperationException(
                    "Could not register legacy destructible prop target "
                    + propId + ": " + registration + ".");
            }

            DestructiblePropProjectileRelay2D relay =
                blockingCollider.GetComponent<DestructiblePropProjectileRelay2D>()
                ?? blockingCollider.gameObject
                    .AddComponent<DestructiblePropProjectileRelay2D>();
            if (!relay.IsConfigured)
                relay.Configure(prop, confirmedHitDamage);

            DestructiblePropDestructionPlayer2D player =
                blockingCollider.GetComponent<DestructiblePropDestructionPlayer2D>()
                ?? blockingCollider.gameObject
                    .AddComponent<DestructiblePropDestructionPlayer2D>();
            player.Configure(prop, renderer.transform, authoring.DestructionAnimation);
            return prop;
        }

        private static Collider2D ResolveLegacyCollider(
            DestructiblePropAuthoring2D authoring,
            Transform presentationRoot,
            Collider2D[] candidates,
            HashSet<Collider2D> consumed)
        {
            Vector3 expectedPosition = authoring.transform.position
                + (Vector3)authoring.ColliderOffset;
            Collider2D resolved = null;
            for (int index = 0; index < candidates.Length; index++)
            {
                Collider2D candidate = candidates[index];
                if (candidate == null
                    || consumed.Contains(candidate)
                    || candidate.transform.IsChildOf(presentationRoot)
                    || (candidate.transform.position - expectedPosition).sqrMagnitude
                        > LegacyPositionToleranceSquared
                    || Quaternion.Angle(
                        candidate.transform.rotation,
                        authoring.transform.rotation) > LegacyRotationToleranceDegrees)
                {
                    continue;
                }
                if (resolved != null)
                {
                    throw new InvalidOperationException(
                        "Legacy destructible prop marker has multiple colliders at its "
                        + "authored position. Migrate it to an explicit collider reference.");
                }
                resolved = candidate;
            }
            if (resolved == null)
            {
                throw new InvalidOperationException(
                    "Legacy destructible prop marker has no unique collider at its authored "
                    + "position. Migrate it to explicit definition-driven authoring.");
            }
            return resolved;
        }

        private static void ValidateArguments(
            GameObject owner,
            Transform presentationRoot,
            Transform colliderRoot,
            CombatHit2DAdapter hitAdapter,
            double confirmedHitDamage,
            Func<long> restartGenerationSource)
        {
            if (owner == null) throw new ArgumentNullException(nameof(owner));
            if (presentationRoot == null)
                throw new ArgumentNullException(nameof(presentationRoot));
            if (colliderRoot == null)
                throw new ArgumentNullException(nameof(colliderRoot));
            if (hitAdapter == null)
                throw new ArgumentNullException(nameof(hitAdapter));
            if (double.IsNaN(confirmedHitDamage)
                || double.IsInfinity(confirmedHitDamage)
                || confirmedHitDamage <= 0d)
            {
                throw new ArgumentOutOfRangeException(nameof(confirmedHitDamage));
            }
            if (restartGenerationSource == null)
                throw new ArgumentNullException(nameof(restartGenerationSource));
        }

        private static string Vector(Vector2 value)
        {
            return value.x.ToString("R", CultureInfo.InvariantCulture)
                + "," + value.y.ToString("R", CultureInfo.InvariantCulture);
        }

        private static string Vector(Vector3 value)
        {
            return value.x.ToString("R", CultureInfo.InvariantCulture)
                + "," + value.y.ToString("R", CultureInfo.InvariantCulture)
                + "," + value.z.ToString("R", CultureInfo.InvariantCulture);
        }

        private static string Fingerprint64(string input)
        {
            unchecked
            {
                const ulong offset = 14695981039346656037UL;
                const ulong prime = 1099511628211UL;
                ulong hash = offset;
                for (int index = 0; index < input.Length; index++)
                {
                    char value = input[index];
                    hash ^= (byte)(value & 0xff);
                    hash *= prime;
                    hash ^= (byte)(value >> 8);
                    hash *= prime;
                }
                return hash.ToString("x16", CultureInfo.InvariantCulture);
            }
        }
    }
}
