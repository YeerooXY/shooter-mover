using System;
using System.Collections.Generic;
using ShooterMover.Contracts.Authoring;
using ShooterMover.Domain.Common;
using ShooterMover.UnityAdapters.Authoring;
using UnityEngine;

namespace ShooterMover.UnityAdapters.Rewards.Pickups
{
    /// <summary>
    /// Reusable physical reward projection. The component owns collider/presentation
    /// state only. Collection is delegated to an injected RAP lifecycle authority.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RewardPickup2D : MonoBehaviour, IRestartParticipant
    {
        [SerializeField] private CircleCollider2D collectionTrigger;
        [SerializeField] private SpriteRenderer spriteRenderer;
        [SerializeField, Min(0.01f)] private float collectionRadius = 0.75f;
        [SerializeField] private RewardPickupPresentationStyleV1[] presentationStyles =
            new RewardPickupPresentationStyleV1[0];
        [SerializeField] private MonoBehaviour lifecycleAuthority;
        [SerializeField] private GameplaySceneScope2D restartScope;
        [SerializeField] private bool registerForRestart = true;

        private RewardPickupPayloadV1 payload;
        private bool collectInProgress;
        private bool collected;
        private RewardPickupCollectResultV1 lastCollectResult;
        private RestartParticipantRegistrationResult lastRestartRegistration;

        public RewardPickupPayloadV1 Payload { get { return payload; } }
        public bool IsCollected { get { return collected; } }
        public float CollectionRadius { get { return collectionRadius; } }
        public RewardPickupCollectResultV1 LastCollectResult { get { return lastCollectResult; } }
        public RestartParticipantRegistrationResult LastRestartRegistration
        {
            get { return lastRestartRegistration; }
        }

        private void OnEnable()
        {
            if (registerForRestart && payload != null)
            {
                RegisterForRestart();
            }
        }

        public StableId RestartParticipantId
        {
            get
            {
                if (payload == null)
                {
                    throw new InvalidOperationException(
                        "Reward pickup must be configured before its restart identity is read.");
                }

                return payload.RestartParticipantStableId;
            }
        }

        public void Configure(
            RewardPickupPayloadV1 payload,
            MonoBehaviour lifecycleAuthority,
            GameplaySceneScope2D restartScope)
        {
            if (payload == null)
            {
                throw new ArgumentNullException(nameof(payload));
            }

            if (!(lifecycleAuthority is IRewardPickupLifecycleAuthorityV1))
            {
                throw new ArgumentException(
                    "Lifecycle authority must implement IRewardPickupLifecycleAuthorityV1.",
                    nameof(lifecycleAuthority));
            }

            if (restartScope != null
                && restartScope.RunId != payload.CommitCommand.Operation.RunStableId)
            {
                throw new ArgumentException(
                    "Pickup restart scope run must match the reward operation run.",
                    nameof(restartScope));
            }

            if (this.payload != null && !this.payload.Equals(payload))
            {
                throw new InvalidOperationException(
                    "A configured pickup cannot be rebound to a different immutable payload.");
            }

            this.payload = payload;
            this.lifecycleAuthority = lifecycleAuthority;
            this.restartScope = restartScope;
            EnsurePresentationComponents();
            ApplyConfiguredPresentation();
            ApplyProjectionState();
            if (registerForRestart)
            {
                RegisterForRestart();
            }
        }

        public void ConfigureForTests(
            RewardPickupPayloadV1 payload,
            MonoBehaviour lifecycleAuthority,
            GameplaySceneScope2D restartScope,
            float collectionRadius,
            IEnumerable<RewardPickupPresentationStyleV1> styles,
            bool registerForRestart = true)
        {
            if (collectionRadius <= 0f || float.IsNaN(collectionRadius) || float.IsInfinity(collectionRadius))
            {
                throw new ArgumentOutOfRangeException(nameof(collectionRadius));
            }

            this.collectionRadius = collectionRadius;
            presentationStyles = styles == null
                ? new RewardPickupPresentationStyleV1[0]
                : new List<RewardPickupPresentationStyleV1>(styles).ToArray();
            this.registerForRestart = registerForRestart;
            Configure(payload, lifecycleAuthority, restartScope);
        }

        public RewardPickupCollectResultV1 TryCollect(StableId claimantStableId)
        {
            if (claimantStableId == null)
            {
                lastCollectResult = new RewardPickupCollectResultV1(
                    RewardPickupCollectStatusV1.Invalid,
                    null,
                    "Claimant identity is required.");
                return lastCollectResult;
            }

            if (payload == null)
            {
                lastCollectResult = new RewardPickupCollectResultV1(
                    RewardPickupCollectStatusV1.Invalid,
                    null,
                    "Pickup payload has not been configured.");
                return lastCollectResult;
            }

            if (collected)
            {
                lastCollectResult = new RewardPickupCollectResultV1(
                    RewardPickupCollectStatusV1.AlreadyCollectedNoChange,
                    lastCollectResult == null ? null : lastCollectResult.AuthorityResult,
                    "Repeated collection callback produced no additional reward.");
                return lastCollectResult;
            }

            if (collectInProgress)
            {
                return new RewardPickupCollectResultV1(
                    RewardPickupCollectStatusV1.PendingRetry,
                    lastCollectResult == null ? null : lastCollectResult.AuthorityResult,
                    "A collection attempt is already in progress.");
            }

            IRewardPickupLifecycleAuthorityV1 authority =
                lifecycleAuthority as IRewardPickupLifecycleAuthorityV1;
            if (authority == null)
            {
                lastCollectResult = new RewardPickupCollectResultV1(
                    RewardPickupCollectStatusV1.Invalid,
                    null,
                    "Pickup lifecycle authority is missing or incompatible.");
                return lastCollectResult;
            }

            collectInProgress = true;
            try
            {
                lastCollectResult = authority.Collect(payload, claimantStableId)
                    ?? new RewardPickupCollectResultV1(
                        RewardPickupCollectStatusV1.Rejected,
                        null,
                        "Pickup lifecycle authority returned no result.");
                if (lastCollectResult.IsCollected)
                {
                    collected = true;
                    ApplyProjectionState();
                }

                return lastCollectResult;
            }
            catch (Exception exception)
            {
                lastCollectResult = new RewardPickupCollectResultV1(
                    RewardPickupCollectStatusV1.Rejected,
                    null,
                    "Pickup collection threw: " + exception.Message);
                return lastCollectResult;
            }
            finally
            {
                collectInProgress = false;
            }
        }

        public RestartParticipantRegistrationResult RegisterForRestart()
        {
            if (payload == null)
            {
                lastRestartRegistration = RestartParticipantRegistrationResult.Invalid(
                    "Pickup payload must be configured before restart registration.");
                return lastRestartRegistration;
            }

            if (restartScope == null)
            {
                lastRestartRegistration = RestartParticipantRegistrationResult.Invalid(
                    "Pickup restart scope is missing.");
                return lastRestartRegistration;
            }

            lastRestartRegistration = restartScope.RegisterRestartParticipant(
                new RestartParticipantRegistrationRequest(
                    this,
                    this,
                    BuildDiagnosticLocation()));
            return lastRestartRegistration;
        }

        public void OnRestartPhase(RestartContext context, RestartLifecyclePhase phase)
        {
            if (payload == null || context == null)
            {
                return;
            }

            if (context.RunId != payload.CommitCommand.Operation.RunStableId)
            {
                throw new InvalidOperationException(
                    "Reward pickup received restart context for a different run.");
            }

            switch (phase)
            {
                case RestartLifecyclePhase.RetireAttempt:
                    collectInProgress = false;
                    break;
                case RestartLifecyclePhase.ApplyResetProjection:
                    // RAP claim truth survives quick restart. Applied pickups remain
                    // retired; unclaimed pickups are projected again for the new attempt.
                    ApplyProjectionState();
                    break;
            }
        }

        public void HandleTriggerForTests(RewardPickupClaimant2D claimant)
        {
            StableId claimantId;
            if (claimant != null && claimant.TryGetClaimantStableId(out claimantId))
            {
                TryCollect(claimantId);
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (other == null)
            {
                return;
            }

            RewardPickupClaimant2D claimant = other.GetComponentInParent<RewardPickupClaimant2D>();
            HandleTriggerForTests(claimant);
        }

        private void OnDisable()
        {
            UnregisterFromRestart();
        }

        private void OnDestroy()
        {
            UnregisterFromRestart();
        }

        private void EnsurePresentationComponents()
        {
            if (collectionTrigger == null)
            {
                collectionTrigger = GetComponent<CircleCollider2D>();
                if (collectionTrigger == null)
                {
                    collectionTrigger = gameObject.AddComponent<CircleCollider2D>();
                }
            }

            collectionTrigger.isTrigger = true;
            collectionTrigger.radius = collectionRadius;

            if (spriteRenderer == null)
            {
                spriteRenderer = GetComponent<SpriteRenderer>();
                if (spriteRenderer == null)
                {
                    spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
                }
            }
        }

        private void ApplyConfiguredPresentation()
        {
            if (payload == null || spriteRenderer == null)
            {
                return;
            }

            for (int index = 0; index < presentationStyles.Length; index++)
            {
                RewardPickupPresentationStyleV1 style = presentationStyles[index];
                if (style == null || style.Category != payload.Category)
                {
                    continue;
                }

                spriteRenderer.sprite = style.Sprite;
                spriteRenderer.color = style.Tint;
                transform.localScale = style.LocalScale;
                return;
            }
        }

        private void ApplyProjectionState()
        {
            if (collectionTrigger != null)
            {
                collectionTrigger.enabled = !collected;
            }

            if (spriteRenderer != null)
            {
                spriteRenderer.enabled = !collected;
            }
        }

        private void UnregisterFromRestart()
        {
            if (restartScope != null && payload != null)
            {
                restartScope.UnregisterRestartParticipant(
                    payload.RestartParticipantStableId,
                    this);
            }
        }

        private string BuildDiagnosticLocation()
        {
            List<string> names = new List<string>();
            Transform current = transform;
            while (current != null)
            {
                names.Add(current.name);
                current = current.parent;
            }

            names.Reverse();
            return gameObject.scene.name + ":" + string.Join("/", names.ToArray());
        }

        private void OnValidate()
        {
            if (collectionRadius <= 0f || float.IsNaN(collectionRadius) || float.IsInfinity(collectionRadius))
            {
                collectionRadius = 0.75f;
            }

            if (collectionTrigger != null)
            {
                collectionTrigger.isTrigger = true;
                collectionTrigger.radius = collectionRadius;
            }
        }
    }
}
