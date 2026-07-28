using System;
using ShooterMover.Domain.Common;
using ShooterMover.RunPickups;
using UnityEngine;

namespace ShooterMover.UnityAdapters.Rewards.RunPickups
{
    /// <summary>
    /// Generic physical projection of one exact run-local pickup. Trigger callbacks only
    /// construct and submit a typed collection command. Authority acceptance retires the
    /// exact view immediately from synchronization, while optional visual feedback may
    /// complete before the GameObject is destroyed.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RunRewardPickup2D : MonoBehaviour
    {
        private CircleCollider2D collectionTrigger;
        private SpriteRenderer spriteRenderer;
        private RunPickupSnapshotV1 pickup;
        private RunPickupAuthorityHost2D authorityHost;
        private RunPickupPresenter2D presenter;
        private IRunRewardPickupAcceptedFeedbackV1 acceptedFeedback;
        private bool collectionInProgress;
        private bool retired;
        private bool retirementCompleted;
        private RunPickupCollectionResultV1 lastCollectionResult;
        private string presentationDiagnostic = string.Empty;

        public RunPickupSnapshotV1 Pickup { get { return pickup; } }
        public StableId PickupStableId
        {
            get { return pickup == null ? null : pickup.PickupStableId; }
        }
        public bool IsRetired { get { return retired; } }
        public bool IsRetirementFeedbackPending
        {
            get { return retired && !retirementCompleted; }
        }
        public string PresentationDiagnostic { get { return presentationDiagnostic; } }
        public RunPickupCollectionResultV1 LastCollectionResult
        {
            get { return lastCollectionResult; }
        }

        public void Configure(
            RunPickupSnapshotV1 pickup,
            RunPickupAuthorityHost2D authorityHost,
            RunPickupPresenter2D presenter,
            RunPickupPresentationEntryV1 presentation)
        {
            if (pickup == null) throw new ArgumentNullException(nameof(pickup));
            if (pickup.State != RunPickupStateV1.Available)
                throw new ArgumentException(
                    "Only an available authoritative pickup can be presented.",
                    nameof(pickup));
            if (authorityHost == null || !authorityHost.IsConfigured)
                throw new ArgumentException(
                    "A configured pickup authority host is required.",
                    nameof(authorityHost));
            if (presentation == null)
                throw new ArgumentNullException(nameof(presentation));
            string presentationValidationDiagnostic;
            if (!presentation.IsUsable(out presentationValidationDiagnostic))
                throw new ArgumentException(
                    presentationValidationDiagnostic,
                    nameof(presentation));
            if (this.pickup != null
                && this.pickup.PickupStableId != pickup.PickupStableId)
            {
                throw new InvalidOperationException(
                    "A physical pickup view cannot be rebound to another identity.");
            }

            this.pickup = pickup;
            this.authorityHost = authorityHost;
            this.presenter = presenter;
            EnsureComponents(presentation.TriggerRadius);
            if (presentation.Sprite != null)
                spriteRenderer.sprite = presentation.Sprite;
            transform.localScale = presentation.LocalScale;
            transform.position = new Vector3(
                (float)pickup.WorldSpawnContext.PositionX,
                (float)pickup.WorldSpawnContext.PositionY,
                transform.position.z);
            retired = false;
            retirementCompleted = false;
            presentationDiagnostic = string.Empty;
            ApplyVisibleState(true);
            BindOptionalPresentation(pickup);
        }

        public RunPickupCollectionResultV1 TryCollect(
            RunPickupCollector2D collector)
        {
            if (pickup == null || authorityHost == null || !authorityHost.IsConfigured)
            {
                lastCollectionResult = new RunPickupCollectionResultV1(
                    RunPickupCollectionStatusV1.Rejected,
                    null,
                    pickup,
                    null,
                    "run-pickup-view-not-configured");
                return lastCollectionResult;
            }
            if (retired && lastCollectionResult != null)
                return lastCollectionResult;
            if (collectionInProgress)
            {
                return new RunPickupCollectionResultV1(
                    RunPickupCollectionStatusV1.Rejected,
                    null,
                    pickup,
                    null,
                    "run-pickup-view-collection-in-progress");
            }

            StableId collectorEntity;
            StableId collectorParticipant;
            if (collector == null
                || !collector.TryGetIdentities(
                    out collectorEntity,
                    out collectorParticipant))
            {
                lastCollectionResult = new RunPickupCollectionResultV1(
                    RunPickupCollectionStatusV1.UnauthorizedCollector,
                    null,
                    pickup,
                    null,
                    collector == null
                        ? "run-pickup-view-collector-missing"
                        : collector.ConfigurationError);
                return lastCollectionResult;
            }

            var command = new RunPickupCollectionCommandV1(
                RunPickupIdentityV1.DeriveCollectionOperationStableId(
                    pickup.PickupStableId,
                    collectorEntity,
                    collectorParticipant),
                pickup.PickupStableId,
                pickup.Reward.RewardInstanceStableId,
                pickup.Batch.RunStableId,
                pickup.Batch.RunLifecycleGeneration,
                collectorEntity,
                collectorParticipant,
                pickup.Fingerprint);

            collectionInProgress = true;
            try
            {
                lastCollectionResult = authorityHost.Authority.Collect(command)
                    ?? new RunPickupCollectionResultV1(
                        RunPickupCollectionStatusV1.Rejected,
                        command,
                        pickup,
                        null,
                        "run-pickup-view-authority-result-null");
                if (lastCollectionResult.IsCollected)
                {
                    BeginAcceptedRetirement(collector);
                }
                return lastCollectionResult;
            }
            catch (Exception exception)
            {
                lastCollectionResult = new RunPickupCollectionResultV1(
                    RunPickupCollectionStatusV1.Rejected,
                    command,
                    pickup,
                    null,
                    "run-pickup-view-collection-exception:" + exception.Message);
                return lastCollectionResult;
            }
            finally
            {
                collectionInProgress = false;
            }
        }

        public void HandleTriggerForTests(RunPickupCollector2D collector)
        {
            TryCollect(collector);
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (other == null || retired) return;
            TryCollect(other.GetComponentInParent<RunPickupCollector2D>());
        }

        private void EnsureComponents(float triggerRadius)
        {
            collectionTrigger = GetComponent<CircleCollider2D>();
            if (collectionTrigger == null)
                collectionTrigger = gameObject.AddComponent<CircleCollider2D>();
            collectionTrigger.isTrigger = true;
            collectionTrigger.radius = triggerRadius;

            spriteRenderer = GetComponent<SpriteRenderer>();
            if (spriteRenderer == null)
                spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
        }

        private void BindOptionalPresentation(RunPickupSnapshotV1 immutablePickup)
        {
            IRunRewardPickupProjectionBinderV1 binder = null;
            IRunRewardPickupAcceptedFeedbackV1 feedback = null;
            MonoBehaviour[] behaviours = GetComponents<MonoBehaviour>();
            for (int index = 0; index < behaviours.Length; index++)
            {
                MonoBehaviour behaviour = behaviours[index];
                IRunRewardPickupProjectionBinderV1 candidateBinder =
                    behaviour as IRunRewardPickupProjectionBinderV1;
                if (candidateBinder != null)
                {
                    if (binder != null && !ReferenceEquals(binder, candidateBinder))
                    {
                        throw new InvalidOperationException(
                            "A pickup view cannot own multiple projection binders.");
                    }
                    binder = candidateBinder;
                }

                IRunRewardPickupAcceptedFeedbackV1 candidateFeedback =
                    behaviour as IRunRewardPickupAcceptedFeedbackV1;
                if (candidateFeedback != null)
                {
                    if (feedback != null && !ReferenceEquals(feedback, candidateFeedback))
                    {
                        throw new InvalidOperationException(
                            "A pickup view cannot own multiple accepted-feedback handlers.");
                    }
                    feedback = candidateFeedback;
                }
            }

            bool decoratorBound = true;
            if (binder != null)
            {
                string diagnostic;
                decoratorBound = binder.TryBindRunPickup(
                    immutablePickup,
                    out diagnostic);
                presentationDiagnostic = diagnostic ?? string.Empty;
            }

            if (feedback != null && decoratorBound)
            {
                acceptedFeedback = feedback;
                return;
            }

            RunPickupTransformAcceptedFeedback2D fallback =
                GetComponent<RunPickupTransformAcceptedFeedback2D>();
            if (fallback == null)
            {
                fallback = gameObject.AddComponent<RunPickupTransformAcceptedFeedback2D>();
            }
            acceptedFeedback = fallback;
        }

        private void BeginAcceptedRetirement(RunPickupCollector2D collector)
        {
            if (retired)
            {
                return;
            }

            retired = true;
            retirementCompleted = false;
            if (collectionTrigger != null)
            {
                collectionTrigger.enabled = false;
            }
            if (presenter != null)
            {
                presenter.BeginCollectedRetirement(this);
            }

            bool started = false;
            try
            {
                started = acceptedFeedback != null
                    && acceptedFeedback.TryPlayAcceptedCollectionFeedback(
                        collector == null ? null : collector.transform,
                        CompleteAcceptedRetirement);
            }
            catch (Exception exception)
            {
                presentationDiagnostic =
                    "run-pickup-accepted-feedback-exception:" + exception.Message;
            }

            if (!started)
            {
                CompleteAcceptedRetirement();
            }
        }

        private void CompleteAcceptedRetirement()
        {
            if (retirementCompleted)
            {
                return;
            }

            retirementCompleted = true;
            if (presenter != null)
            {
                presenter.CompleteCollectedRetirement(this);
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void ApplyVisibleState(bool visible)
        {
            if (collectionTrigger != null) collectionTrigger.enabled = visible;
            if (spriteRenderer != null) spriteRenderer.enabled = visible;
            gameObject.SetActive(visible);
        }
    }
}
