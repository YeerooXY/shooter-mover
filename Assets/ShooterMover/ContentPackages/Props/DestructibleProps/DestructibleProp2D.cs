using System;
using ShooterMover.Contracts.Combat;
using ShooterMover.Domain.Common;
using UnityEngine;

namespace ShooterMover.ContentPackages.Props.DestructibleProps
{
    /// <summary>
    /// Unity-facing destructible target. State mutates only from confirmed combat messages;
    /// collision and presentation references are supplied explicitly by the authoring boundary.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DestructibleProp2D : MonoBehaviour
    {
        private Collider2D blockingCollider;
        private Renderer[] presentationRenderers = Array.Empty<Renderer>();
        private bool[] initialRendererEnabled = Array.Empty<bool>();
        private bool initialColliderEnabled;
        private bool initialColliderIsTrigger;
        private DestructiblePropDestroyedCollisionPolicy destroyedCollisionPolicy;
        private DestructiblePropAuthority authority;
        private DestructiblePropTerminalProvenanceV1 terminalProvenance;
        private bool configured;
        private bool destructionNotificationPublished;
        private int destructionNotificationCount;

        public event Action<DestructiblePropDestructionResult> Destroyed;
        public event Action Restarted;

        public bool IsConfigured => configured;
        public StableId PropId => authority == null ? null : authority.PropId;
        public double MaximumHealth => authority == null ? 0d : authority.MaximumHealth;
        public double CurrentHealth => authority == null || authority.CurrentState == null
            ? 0d
            : authority.CurrentState.CurrentHealth;
        public DestructiblePropState CurrentState => authority == null
            ? null
            : authority.CurrentState;
        public Collider2D BlockingCollider => blockingCollider;
        public DestructiblePropTerminalProvenanceV1 TerminalProvenance =>
            terminalProvenance;
        public int DestructionNotificationCount => destructionNotificationCount;
        public DestructiblePropDestroyedCollisionPolicy DestroyedCollisionPolicy =>
            destroyedCollisionPolicy;

        /// <summary>
        /// Compatibility overload retained for existing package consumers. Production terminal
        /// reward routes must use the provenance overload and fail closed when it is absent.
        /// </summary>
        public void Configure(
            StableId configuredPropId,
            double configuredMaximumHealth,
            Collider2D configuredBlockingCollider,
            GameObject configuredPresentationRoot)
        {
            if (configuredPresentationRoot == null)
                throw new ArgumentNullException(nameof(configuredPresentationRoot));

            Configure(
                configuredPropId,
                configuredMaximumHealth,
                configuredBlockingCollider,
                configuredPresentationRoot.GetComponentsInChildren<Renderer>(true),
                DestructiblePropDestroyedCollisionPolicy.Disable,
                null);
        }

        public void Configure(
            StableId configuredPropId,
            double configuredMaximumHealth,
            Collider2D configuredBlockingCollider,
            Renderer[] configuredPresentationRenderers,
            DestructiblePropDestroyedCollisionPolicy configuredDestroyedCollisionPolicy)
        {
            Configure(
                configuredPropId,
                configuredMaximumHealth,
                configuredBlockingCollider,
                configuredPresentationRenderers,
                configuredDestroyedCollisionPolicy,
                null);
        }

        public void Configure(
            StableId configuredPropId,
            double configuredMaximumHealth,
            Collider2D configuredBlockingCollider,
            Renderer[] configuredPresentationRenderers,
            DestructiblePropDestroyedCollisionPolicy configuredDestroyedCollisionPolicy,
            DestructiblePropTerminalProvenanceV1 configuredTerminalProvenance)
        {
            if (configured)
                throw new InvalidOperationException(
                    "Destructible prop is already configured.");
            if (configuredPropId == null)
                throw new ArgumentNullException(nameof(configuredPropId));
            if (configuredBlockingCollider == null)
                throw new ArgumentNullException(nameof(configuredBlockingCollider));
            if (configuredPresentationRenderers == null
                || configuredPresentationRenderers.Length == 0)
            {
                throw new ArgumentException(
                    "At least one explicit presentation renderer is required.",
                    nameof(configuredPresentationRenderers));
            }
            if (!Enum.IsDefined(
                typeof(DestructiblePropDestroyedCollisionPolicy),
                configuredDestroyedCollisionPolicy))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(configuredDestroyedCollisionPolicy));
            }

            presentationRenderers = new Renderer[configuredPresentationRenderers.Length];
            initialRendererEnabled = new bool[configuredPresentationRenderers.Length];
            for (int index = 0; index < configuredPresentationRenderers.Length; index++)
            {
                Renderer renderer = configuredPresentationRenderers[index];
                if (renderer == null)
                {
                    throw new ArgumentException(
                        "Presentation renderer references cannot contain null.",
                        nameof(configuredPresentationRenderers));
                }
                presentationRenderers[index] = renderer;
                initialRendererEnabled[index] = renderer.enabled;
            }

            authority = new DestructiblePropAuthority(
                configuredPropId,
                configuredMaximumHealth);
            blockingCollider = configuredBlockingCollider;
            initialColliderEnabled = blockingCollider.enabled;
            initialColliderIsTrigger = blockingCollider.isTrigger;
            destroyedCollisionPolicy = configuredDestroyedCollisionPolicy;
            terminalProvenance = configuredTerminalProvenance;
            destructionNotificationPublished = false;
            destructionNotificationCount = 0;
            configured = true;
            ApplyActivePresentation();
        }

        public DestructiblePropDamageResult TryApplyConfirmedHit(
            HitMessage hit,
            double requestedDamage)
        {
            if (!configured || authority == null)
            {
                return new DestructiblePropDamageResult(
                    DestructiblePropDamageStatus.InvalidInput,
                    hit,
                    null,
                    null,
                    null);
            }

            DestructiblePropDamageResult result =
                authority.ApplyConfirmedHit(hit, requestedDamage);
            if (result.Status != DestructiblePropDamageStatus.Destroyed
                || result.Destruction == null)
            {
                return result;
            }

            ApplyDestroyedPresentation();
            PublishDestroyed(result.Destruction);
            return result;
        }

        public void Restart()
        {
            if (!configured || authority == null) return;
            authority.Restart();
            destructionNotificationPublished = false;
            destructionNotificationCount = 0;
            ApplyActivePresentation();
            PublishRestarted();
        }

        private void ApplyDestroyedPresentation()
        {
            if (blockingCollider != null)
            {
                switch (destroyedCollisionPolicy)
                {
                    case DestructiblePropDestroyedCollisionPolicy.Disable:
                        blockingCollider.enabled = false;
                        break;
                    case DestructiblePropDestroyedCollisionPolicy.KeepBlocking:
                        blockingCollider.enabled = true;
                        blockingCollider.isTrigger = false;
                        break;
                    case DestructiblePropDestroyedCollisionPolicy.KeepAsTrigger:
                        blockingCollider.enabled = true;
                        blockingCollider.isTrigger = true;
                        break;
                }
            }

            for (int index = 0; index < presentationRenderers.Length; index++)
            {
                Renderer renderer = presentationRenderers[index];
                if (renderer != null) renderer.enabled = false;
            }
        }

        private void ApplyActivePresentation()
        {
            if (blockingCollider != null)
            {
                blockingCollider.enabled = initialColliderEnabled;
                blockingCollider.isTrigger = initialColliderIsTrigger;
            }

            int count = Math.Min(
                presentationRenderers.Length,
                initialRendererEnabled.Length);
            for (int index = 0; index < count; index++)
            {
                Renderer renderer = presentationRenderers[index];
                if (renderer != null)
                    renderer.enabled = initialRendererEnabled[index];
            }
        }

        private void PublishDestroyed(DestructiblePropDestructionResult destruction)
        {
            if (destructionNotificationPublished || destruction == null) return;
            destructionNotificationPublished = true;
            destructionNotificationCount++;
            Action<DestructiblePropDestructionResult> handler = Destroyed;
            if (handler == null) return;

            foreach (Delegate subscriber in handler.GetInvocationList())
            {
                try
                {
                    ((Action<DestructiblePropDestructionResult>)subscriber)(destruction);
                }
                catch (Exception)
                {
                    // Optional observers cannot replay or block authoritative destruction.
                }
            }
        }

        private void PublishRestarted()
        {
            Action handler = Restarted;
            if (handler == null) return;
            foreach (Delegate subscriber in handler.GetInvocationList())
            {
                try
                {
                    ((Action)subscriber)();
                }
                catch (Exception)
                {
                    // Optional observers cannot block authoritative restart.
                }
            }
        }

        private void OnDestroy()
        {
            Destroyed = null;
            Restarted = null;
        }
    }
}
