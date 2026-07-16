using System;
using ShooterMover.Contracts.Combat;
using ShooterMover.Domain.Common;
using UnityEngine;

namespace ShooterMover.ContentPackages.Props.DestructibleProps
{
    /// <summary>
    /// Unity-facing destructible prop target. The component never interprets raw projectile
    /// contact as damage; it mutates state only from a confirmed HitMessage.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DestructibleProp2D : MonoBehaviour
    {
        private Collider2D blockingCollider;
        private GameObject presentationRoot;
        private Renderer[] presentationRenderers = Array.Empty<Renderer>();
        private bool[] initialRendererEnabled = Array.Empty<bool>();
        private bool initialColliderEnabled;
        private DestructiblePropAuthority authority;
        private bool configured;
        private bool destructionNotificationPublished;
        private int destructionNotificationCount;

        public event Action<DestructiblePropDestructionResult> Destroyed;

        public bool IsConfigured
        {
            get { return configured; }
        }

        public StableId PropId
        {
            get { return authority == null ? null : authority.PropId; }
        }

        public double MaximumHealth
        {
            get { return authority == null ? 0d : authority.MaximumHealth; }
        }

        public double CurrentHealth
        {
            get
            {
                return authority == null || authority.CurrentState == null
                    ? 0d
                    : authority.CurrentState.CurrentHealth;
            }
        }

        public DestructiblePropState CurrentState
        {
            get { return authority == null ? null : authority.CurrentState; }
        }

        public Collider2D BlockingCollider
        {
            get { return blockingCollider; }
        }

        public GameObject PresentationRoot
        {
            get { return presentationRoot; }
        }

        public int DestructionNotificationCount
        {
            get { return destructionNotificationCount; }
        }

        public void Configure(
            StableId configuredPropId,
            double configuredMaximumHealth,
            Collider2D configuredBlockingCollider,
            GameObject configuredPresentationRoot)
        {
            if (configured)
            {
                throw new InvalidOperationException(
                    "Destructible prop is already configured.");
            }

            if (configuredPropId == null)
            {
                throw new ArgumentNullException(nameof(configuredPropId));
            }

            if (configuredBlockingCollider == null)
            {
                throw new ArgumentNullException(nameof(configuredBlockingCollider));
            }

            if (configuredPresentationRoot == null)
            {
                throw new ArgumentNullException(nameof(configuredPresentationRoot));
            }

            authority = new DestructiblePropAuthority(
                configuredPropId,
                configuredMaximumHealth);
            blockingCollider = configuredBlockingCollider;
            presentationRoot = configuredPresentationRoot;
            initialColliderEnabled = blockingCollider.enabled;
            presentationRenderers = presentationRoot.GetComponentsInChildren<Renderer>(true);
            initialRendererEnabled = new bool[presentationRenderers.Length];
            for (int index = 0; index < presentationRenderers.Length; index++)
            {
                initialRendererEnabled[index] = presentationRenderers[index] != null
                    && presentationRenderers[index].enabled;
            }

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
            if (!configured || authority == null)
            {
                return;
            }

            authority.Restart();
            destructionNotificationPublished = false;
            destructionNotificationCount = 0;
            ApplyActivePresentation();
        }

        private void ApplyDestroyedPresentation()
        {
            if (blockingCollider != null)
            {
                blockingCollider.enabled = false;
            }

            for (int index = 0; index < presentationRenderers.Length; index++)
            {
                Renderer renderer = presentationRenderers[index];
                if (renderer != null)
                {
                    renderer.enabled = false;
                }
            }
        }

        private void ApplyActivePresentation()
        {
            if (blockingCollider != null)
            {
                blockingCollider.enabled = initialColliderEnabled;
            }

            int count = Math.Min(
                presentationRenderers.Length,
                initialRendererEnabled.Length);
            for (int index = 0; index < count; index++)
            {
                Renderer renderer = presentationRenderers[index];
                if (renderer != null)
                {
                    renderer.enabled = initialRendererEnabled[index];
                }
            }
        }

        private void PublishDestroyed(DestructiblePropDestructionResult destruction)
        {
            if (destructionNotificationPublished || destruction == null)
            {
                return;
            }

            destructionNotificationPublished = true;
            destructionNotificationCount++;
            Action<DestructiblePropDestructionResult> handler = Destroyed;
            if (handler == null)
            {
                return;
            }

            foreach (Delegate subscriber in handler.GetInvocationList())
            {
                try
                {
                    ((Action<DestructiblePropDestructionResult>)subscriber)(destruction);
                }
                catch (Exception)
                {
                    // Presentation observers are optional and cannot replay destruction.
                }
            }
        }

        private void OnDestroy()
        {
            Destroyed = null;
        }
    }
}
