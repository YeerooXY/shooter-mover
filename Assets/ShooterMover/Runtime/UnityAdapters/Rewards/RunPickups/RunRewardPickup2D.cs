using System;
using ShooterMover.Domain.Common;
using ShooterMover.RunPickups;
using UnityEngine;

namespace ShooterMover.UnityAdapters.Rewards.RunPickups
{
    /// <summary>
    /// Generic physical projection of one exact run-local pickup. Trigger callbacks only
    /// construct and submit a typed collection command. The object hides itself only after
    /// authority acceptance or an exact accepted replay.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RunRewardPickup2D : MonoBehaviour
    {
        private CircleCollider2D collectionTrigger;
        private SpriteRenderer spriteRenderer;
        private RunPickupSnapshotV1 pickup;
        private RunPickupAuthorityHost2D authorityHost;
        private RunPickupPresenter2D presenter;
        private bool collectionInProgress;
        private bool retired;
        private RunPickupCollectionResultV1 lastCollectionResult;

        public RunPickupSnapshotV1 Pickup { get { return pickup; } }
        public StableId PickupStableId
        {
            get { return pickup == null ? null : pickup.PickupStableId; }
        }
        public bool IsRetired { get { return retired; } }
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
            string presentationDiagnostic;
            if (!presentation.IsUsable(out presentationDiagnostic))
                throw new ArgumentException(presentationDiagnostic, nameof(presentation));
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
            ApplyVisibleState(true);
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
                    retired = true;
                    ApplyVisibleState(false);
                    if (presenter != null)
                        presenter.NotifyCollected(this);
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

        private void ApplyVisibleState(bool visible)
        {
            if (collectionTrigger != null) collectionTrigger.enabled = visible;
            if (spriteRenderer != null) spriteRenderer.enabled = visible;
            gameObject.SetActive(visible);
        }
    }
}
