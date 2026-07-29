using System;
using System.Globalization;
using ShooterMover.Contracts.Combat;
using ShooterMover.Domain.Common;
using ShooterMover.UnityAdapters.Combat;
using UnityEngine;

namespace ShooterMover.ContentPackages.Props.Breakables
{
    public sealed class BreakableDestroyed
    {
        public BreakableDestroyed(
            BreakableDestructionResult destruction,
            BreakableTerminalProvenance provenance,
            Vector2 terminalPosition,
            string positionFingerprint)
        {
            Destruction = destruction
                ?? throw new ArgumentNullException(nameof(destruction));
            Provenance = provenance;
            TerminalPosition = terminalPosition;
            if (string.IsNullOrWhiteSpace(positionFingerprint))
            {
                throw new ArgumentException(
                    "A terminal-position fingerprint is required.",
                    nameof(positionFingerprint));
            }
            PositionFingerprint = positionFingerprint.Trim();
        }

        public BreakableDestructionResult Destruction { get; }
        public BreakableTerminalProvenance Provenance { get; }
        public Vector2 TerminalPosition { get; }
        public string PositionFingerprint { get; }
    }

    /// <summary>
    /// Unity-facing destructible target. State mutates only from a shared direct-hit boundary or
    /// confirmed combat messages; collision and presentation references are supplied explicitly by
    /// the authoring boundary.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class Breakable : Damageable
    {
        private Collider2D blockingCollider;
        private Renderer[] presentationRenderers = Array.Empty<Renderer>();
        private bool[] initialRendererEnabled = Array.Empty<bool>();
        private bool initialColliderEnabled;
        private bool initialColliderIsTrigger;
        private BreakableDestroyedCollisionPolicy destroyedCollisionPolicy;
        private BreakableDamage authority;
        private BreakableTerminalProvenance terminalProvenance;
        private bool configured;
        private bool destructionNotificationPublished;
        private int destructionNotificationCount;
        private long lifecycleGeneration;

        public event Action<BreakableDestructionResult> Destroyed;
        public event Action<BreakableDestroyed> TerminalDestroyed;
        public event Action Restarted;

        public bool IsConfigured => configured;
        public StableId PropId => authority == null ? null : authority.PropId;
        public double MaximumHealth => authority == null ? 0d : authority.MaximumHealth;
        public double CurrentHealth => authority == null || authority.CurrentState == null
            ? 0d
            : authority.CurrentState.CurrentHealth;
        public BreakableState CurrentState => authority == null
            ? null
            : authority.CurrentState;
        public Collider2D BlockingCollider => blockingCollider;
        public BreakableTerminalProvenance TerminalProvenance => terminalProvenance;
        public int DestructionNotificationCount => destructionNotificationCount;
        public BreakableDestroyedCollisionPolicy DestroyedCollisionPolicy => destroyedCollisionPolicy;

        public override StableId DamageableStableId => PropId;
        public override long DamageableLifecycleGeneration => lifecycleGeneration;
        public override bool CanTakeDamage => configured
            && authority != null
            && authority.CurrentState != null
            && authority.CurrentState.IsActive;

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
                BreakableDestroyedCollisionPolicy.Disable,
                null);
        }

        public void Configure(
            StableId configuredPropId,
            double configuredMaximumHealth,
            Collider2D configuredBlockingCollider,
            Renderer[] configuredPresentationRenderers,
            BreakableDestroyedCollisionPolicy configuredDestroyedCollisionPolicy)
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
            BreakableDestroyedCollisionPolicy configuredDestroyedCollisionPolicy,
            BreakableTerminalProvenance configuredTerminalProvenance)
        {
            if (configured)
                throw new InvalidOperationException("Destructible prop is already configured.");
            if (configuredPropId == null)
                throw new ArgumentNullException(nameof(configuredPropId));
            if (configuredBlockingCollider == null)
                throw new ArgumentNullException(nameof(configuredBlockingCollider));
            if (configuredPresentationRenderers == null || configuredPresentationRenderers.Length == 0)
                throw new ArgumentException("At least one explicit presentation renderer is required.", nameof(configuredPresentationRenderers));
            if (!Enum.IsDefined(typeof(BreakableDestroyedCollisionPolicy), configuredDestroyedCollisionPolicy))
                throw new ArgumentOutOfRangeException(nameof(configuredDestroyedCollisionPolicy));

            presentationRenderers = new Renderer[configuredPresentationRenderers.Length];
            initialRendererEnabled = new bool[configuredPresentationRenderers.Length];
            for (int index = 0; index < configuredPresentationRenderers.Length; index++)
            {
                Renderer renderer = configuredPresentationRenderers[index];
                if (renderer == null)
                    throw new ArgumentException("Presentation renderer references cannot contain null.", nameof(configuredPresentationRenderers));
                presentationRenderers[index] = renderer;
                initialRendererEnabled[index] = renderer.enabled;
            }

            authority = new BreakableDamage(configuredPropId, configuredMaximumHealth);
            blockingCollider = configuredBlockingCollider;
            initialColliderEnabled = blockingCollider.enabled;
            initialColliderIsTrigger = blockingCollider.isTrigger;
            destroyedCollisionPolicy = configuredDestroyedCollisionPolicy;
            terminalProvenance = configuredTerminalProvenance;
            destructionNotificationPublished = false;
            destructionNotificationCount = 0;
            lifecycleGeneration = 1L;
            configured = true;
            ApplyActivePresentation();
        }

        public override void TakeHit(Hit hit)
        {
            if (hit == null) throw new ArgumentNullException(nameof(hit));
            if (!configured || authority == null)
                throw new InvalidOperationException("The breakable is not configured.");
            if (hit.TargetEntityStableId != PropId || hit.TargetLifecycleGeneration != lifecycleGeneration)
                throw new InvalidOperationException("The direct hit does not match the prop lifecycle.");

            CombatChannel channel = ToCombatChannel(hit.ChannelValue);
            var message = new HitMessage(
                hit.EventStableId,
                hit.SourceEntityStableId,
                hit.TargetEntityStableId,
                channel,
                HitResult.Confirmed);
            BreakableDamageResult result = TryApplyConfirmedHit(message, hit.Amount);
            if (result == null)
                throw new InvalidOperationException("The prop damage authority returned no result.");

            switch (result.Status)
            {
                case BreakableDamageStatus.Applied:
                case BreakableDamageStatus.Destroyed:
                case BreakableDamageStatus.DuplicateEventIgnored:
                case BreakableDamageStatus.TargetAlreadyDestroyed:
                    return;
                default:
                    throw new InvalidOperationException("The prop rejected a direct hit: " + result.Status + ".");
            }
        }

        public BreakableDamageResult TryApplyConfirmedHit(HitMessage hit, double requestedDamage)
        {
            if (!configured || authority == null)
            {
                return new BreakableDamageResult(
                    BreakableDamageStatus.InvalidInput,
                    hit,
                    null,
                    null,
                    null);
            }

            BreakableDamageResult result = authority.ApplyConfirmedHit(hit, requestedDamage);
            if (result.Status != BreakableDamageStatus.Destroyed || result.Destruction == null)
                return result;

            BreakableDestroyed terminal = CaptureTerminalEvent(result.Destruction);
            ApplyDestroyedPresentation();
            PublishDestroyed(result.Destruction, terminal);
            return result;
        }

        public void Restart()
        {
            if (!configured || authority == null) return;
            authority.Restart();
            lifecycleGeneration = checked(lifecycleGeneration + 1L);
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
                    case BreakableDestroyedCollisionPolicy.Disable:
                        blockingCollider.enabled = false;
                        break;
                    case BreakableDestroyedCollisionPolicy.KeepBlocking:
                        blockingCollider.enabled = true;
                        blockingCollider.isTrigger = false;
                        break;
                    case BreakableDestroyedCollisionPolicy.KeepAsTrigger:
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
            int count = Math.Min(presentationRenderers.Length, initialRendererEnabled.Length);
            for (int index = 0; index < count; index++)
            {
                Renderer renderer = presentationRenderers[index];
                if (renderer != null) renderer.enabled = initialRendererEnabled[index];
            }
        }

        private BreakableDestroyed CaptureTerminalEvent(BreakableDestructionResult destruction)
        {
            Vector2 position = blockingCollider.bounds.center;
            string fingerprint = "prop-terminal-position-v1|"
                + destruction.EventId + "|"
                + position.x.ToString("R", CultureInfo.InvariantCulture) + "|"
                + position.y.ToString("R", CultureInfo.InvariantCulture);
            return new BreakableDestroyed(destruction, terminalProvenance, position, fingerprint);
        }

        private void PublishDestroyed(BreakableDestructionResult destruction, BreakableDestroyed terminal)
        {
            if (destructionNotificationPublished || destruction == null) return;
            destructionNotificationPublished = true;
            destructionNotificationCount++;
            PublishTerminalDestroyed(terminal);
            Action<BreakableDestructionResult> handler = Destroyed;
            if (handler == null) return;
            foreach (Delegate subscriber in handler.GetInvocationList())
            {
                try { ((Action<BreakableDestructionResult>)subscriber)(destruction); }
                catch (Exception) { }
            }
        }

        private void PublishTerminalDestroyed(BreakableDestroyed terminal)
        {
            Action<BreakableDestroyed> handler = TerminalDestroyed;
            if (handler == null || terminal == null) return;
            foreach (Delegate subscriber in handler.GetInvocationList())
            {
                try { ((Action<BreakableDestroyed>)subscriber)(terminal); }
                catch (Exception) { }
            }
        }

        private void PublishRestarted()
        {
            Action handler = Restarted;
            if (handler == null) return;
            foreach (Delegate subscriber in handler.GetInvocationList())
            {
                try { ((Action)subscriber)(); }
                catch (Exception) { }
            }
        }

        private static CombatChannel ToCombatChannel(int channelValue)
        {
            if (!Enum.IsDefined(typeof(CombatChannel), channelValue))
                throw new ArgumentOutOfRangeException(nameof(channelValue));
            CombatChannel channel = (CombatChannel)channelValue;
            if (channel == CombatChannel.System)
                throw new ArgumentOutOfRangeException(nameof(channelValue));
            return channel;
        }

        private void OnDestroy()
        {
            Destroyed = null;
            TerminalDestroyed = null;
            Restarted = null;
        }
    }
}
