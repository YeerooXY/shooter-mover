using System;
using ShooterMover.Domain.Common;
using ShooterMover.RunLoot;
using UnityEngine;

namespace ShooterMover.UnityAdapters.Rewards.RunLoots
{
    /// <summary>
    /// Generic physical projection of one exact run-local pickup. Trigger callbacks only
    /// construct and submit a typed collection command. Authority acceptance retires the
    /// exact view immediately from synchronization, while optional visual feedback may
    /// complete before the GameObject is destroyed.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RunLoot : MonoBehaviour
    {
        private Collider2D collectionTrigger;
        private SpriteRenderer spriteRenderer;
        private TextMesh labelText;
        private RunLootSnapshot pickup;
        private RunLootSession authorityHost;
        private RunLootView presenter;
        private IRunLootPickupAcceptedFeedback acceptedFeedback;
        private bool collectionInProgress;
        private bool retired;
        private bool retirementCompleted;
        private RunLootCollectionResult lastCollectionResult;
        private string presentationDiagnostic = string.Empty;

        public RunLootSnapshot Pickup { get { return pickup; } }
        public StableId PickupStableId
        {
            get { return pickup == null ? null : pickup.PickupStableId; }
        }
        public Collider2D CollectionTrigger { get { return collectionTrigger; } }
        public TextMesh LabelText { get { return labelText; } }
        public bool IsRetired { get { return retired; } }
        public bool IsRetirementFeedbackPending
        {
            get { return retired && !retirementCompleted; }
        }
        public string PresentationDiagnostic { get { return presentationDiagnostic; } }
        public RunLootCollectionResult LastCollectionResult
        {
            get { return lastCollectionResult; }
        }

        public void Configure(
            RunLootSnapshot pickup,
            RunLootSession authorityHost,
            RunLootView presenter,
            RunLootPresentationEntry presentation)
        {
            if (pickup == null) throw new ArgumentNullException(nameof(pickup));
            if (pickup.State != RunLootState.Available)
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
            EnsureComponents(presentation);
            if (presentation.Sprite != null)
                spriteRenderer.sprite = presentation.Sprite;
            transform.localScale = presentation.LocalScale;
            EnsureLabel(presentation.Label);
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

        public RunLootCollectionResult TryCollect(
            RunLootCollector collector)
        {
            if (pickup == null || authorityHost == null || !authorityHost.IsConfigured)
            {
                lastCollectionResult = new RunLootCollectionResult(
                    RunLootCollectionStatus.Rejected,
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
                return new RunLootCollectionResult(
                    RunLootCollectionStatus.Rejected,
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
                lastCollectionResult = new RunLootCollectionResult(
                    RunLootCollectionStatus.UnauthorizedCollector,
                    null,
                    pickup,
                    null,
                    collector == null
                        ? "run-pickup-view-collector-missing"
                        : collector.ConfigurationError);
                return lastCollectionResult;
            }

            var command = new RunLootCollectionCommand(
                RunLootIdentity.DeriveCollectionOperationStableId(
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
                try
                {
                    lastCollectionResult = authorityHost.Authority.Collect(command)
                        ?? new RunLootCollectionResult(
                            RunLootCollectionStatus.Rejected,
                            command,
                            pickup,
                            null,
                            "run-pickup-view-authority-result-null");
                }
                catch (Exception exception)
                {
                    lastCollectionResult = new RunLootCollectionResult(
                        RunLootCollectionStatus.Rejected,
                        command,
                        pickup,
                        null,
                        "run-pickup-view-collection-exception:" + exception.Message);
                    return lastCollectionResult;
                }

                if (lastCollectionResult.IsCollected)
                {
                    try
                    {
                        BeginAcceptedRetirement(collector);
                    }
                    catch (Exception exception)
                    {
                        presentationDiagnostic =
                            "run-pickup-retirement-exception:"
                            + exception.GetType().Name
                            + ":"
                            + exception.Message;
                        retired = true;
                        CompleteAcceptedRetirement();
                    }
                }
                return lastCollectionResult;
            }
            finally
            {
                collectionInProgress = false;
            }
        }

        public void HandleTriggerForTests(RunLootCollector collector)
        {
            TryCollect(collector);
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (other == null || retired) return;
            TryCollect(other.GetComponentInParent<RunLootCollector>());
        }

        private void EnsureComponents(RunLootPresentationEntry presentation)
        {
            CircleCollider2D circle = GetComponent<CircleCollider2D>();
            BoxCollider2D rectangle = GetComponent<BoxCollider2D>();
            if (presentation.TriggerShape == RunLootTriggerShape.Rectangle)
            {
                if (rectangle == null)
                    rectangle = gameObject.AddComponent<BoxCollider2D>();
                rectangle.isTrigger = true;
                rectangle.size = presentation.TriggerSize;
                rectangle.enabled = true;
                if (circle != null) circle.enabled = false;
                collectionTrigger = rectangle;
            }
            else
            {
                if (circle == null)
                    circle = gameObject.AddComponent<CircleCollider2D>();
                circle.isTrigger = true;
                circle.radius = presentation.TriggerRadius;
                circle.enabled = true;
                if (rectangle != null) rectangle.enabled = false;
                collectionTrigger = circle;
            }

            spriteRenderer = GetComponent<SpriteRenderer>();
            if (spriteRenderer == null)
                spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
        }

        private void EnsureLabel(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                if (labelText != null) labelText.gameObject.SetActive(false);
                return;
            }

            if (labelText == null)
            {
                Transform existing = transform.Find("Pickup Label");
                GameObject labelObject = existing == null
                    ? new GameObject("Pickup Label")
                    : existing.gameObject;
                labelObject.transform.SetParent(transform, false);
                labelText = labelObject.GetComponent<TextMesh>();
                if (labelText == null)
                    labelText = labelObject.AddComponent<TextMesh>();
            }

            float scaleX = Mathf.Max(0.01f, Mathf.Abs(transform.localScale.x));
            float scaleY = Mathf.Max(0.01f, Mathf.Abs(transform.localScale.y));
            float visualHeight = spriteRenderer != null && spriteRenderer.sprite != null
                ? spriteRenderer.sprite.bounds.size.y * scaleY
                : scaleY;
            labelText.gameObject.SetActive(true);
            labelText.text = text.Trim();
            labelText.anchor = TextAnchor.LowerCenter;
            labelText.alignment = TextAlignment.Center;
            labelText.fontSize = 32;
            labelText.characterSize = 0.075f;
            labelText.color = Color.white;
            labelText.transform.localScale = new Vector3(
                1f / scaleX,
                1f / scaleY,
                1f);
            labelText.transform.localPosition = new Vector3(
                0f,
                (visualHeight * 0.5f + 0.14f) / scaleY,
                -0.01f);
            MeshRenderer labelRenderer = labelText.GetComponent<MeshRenderer>();
            if (labelRenderer != null && spriteRenderer != null)
            {
                labelRenderer.sortingLayerID = spriteRenderer.sortingLayerID;
                labelRenderer.sortingOrder = spriteRenderer.sortingOrder + 1;
            }
        }

        private void BindOptionalPresentation(RunLootSnapshot immutablePickup)
        {
            IRunLootPickupViewBinder binder = null;
            IRunLootPickupAcceptedFeedback feedback = null;
            MonoBehaviour[] behaviours = GetComponents<MonoBehaviour>();
            for (int index = 0; index < behaviours.Length; index++)
            {
                MonoBehaviour behaviour = behaviours[index];
                IRunLootPickupViewBinder candidateBinder =
                    behaviour as IRunLootPickupViewBinder;
                if (candidateBinder != null)
                {
                    if (binder != null && !ReferenceEquals(binder, candidateBinder))
                    {
                        throw new InvalidOperationException(
                            "A pickup view cannot own multiple projection binders.");
                    }
                    binder = candidateBinder;
                }

                IRunLootPickupAcceptedFeedback candidateFeedback =
                    behaviour as IRunLootPickupAcceptedFeedback;
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
                decoratorBound = binder.TryBindRunLoot(
                    immutablePickup,
                    out diagnostic);
                presentationDiagnostic = diagnostic ?? string.Empty;
            }

            if (feedback != null && decoratorBound)
            {
                acceptedFeedback = feedback;
                return;
            }

            RunLootFeedback fallback =
                GetComponent<RunLootFeedback>();
            if (fallback == null)
            {
                fallback = gameObject.AddComponent<RunLootFeedback>();
            }
            acceptedFeedback = fallback;
        }

        private void BeginAcceptedRetirement(RunLootCollector collector)
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
                    "run-pickup-accepted-feedback-exception:"
                    + exception.GetType().Name
                    + ":"
                    + exception.Message;
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
            if (labelText != null) labelText.gameObject.SetActive(visible);
            gameObject.SetActive(visible);
        }
    }
}
