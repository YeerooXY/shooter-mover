using System;
using System.Collections.Generic;
using ShooterMover.Domain.Common;
using ShooterMover.RunPickups;
using UnityEngine;

namespace ShooterMover.UnityAdapters.Rewards.RunPickups
{
    public sealed class RunPickupPresentationSyncResultV1
    {
        public RunPickupPresentationSyncResultV1(
            int availableCount,
            int visibleCount,
            int createdCount,
            int retainedCount,
            int retiredCount,
            int failedCount,
            string diagnostic)
        {
            AvailableCount = availableCount;
            VisibleCount = visibleCount;
            CreatedCount = createdCount;
            RetainedCount = retainedCount;
            RetiredCount = retiredCount;
            FailedCount = failedCount;
            Diagnostic = diagnostic ?? string.Empty;
        }

        public int AvailableCount { get; }
        public int VisibleCount { get; }
        public int CreatedCount { get; }
        public int RetainedCount { get; }
        public int RetiredCount { get; }
        public int FailedCount { get; }
        public string Diagnostic { get; }
        public bool Succeeded { get { return FailedCount == 0; } }
    }

    /// <summary>
    /// Reconstructable projection coordinator. It queries immutable available snapshots,
    /// creates at most one view per exact pickup identity, and can be destroyed/recreated
    /// without changing authoritative pickup state.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RunPickupPresenter2D : MonoBehaviour
    {
        [SerializeField] private RunPickupAuthorityHost2D authorityHost;
        [SerializeField] private RunPickupPresentationRegistry2D presentationRegistry;
        [SerializeField] private Transform pickupRoot;

        private readonly Dictionary<StableId, RunRewardPickup2D> views =
            new Dictionary<StableId, RunRewardPickup2D>();

        public int VisiblePickupCount { get { return views.Count; } }
        public RunPickupPresentationSyncResultV1 LastSyncResult { get; private set; }

        public void Configure(
            RunPickupAuthorityHost2D authorityHost,
            RunPickupPresentationRegistry2D presentationRegistry,
            Transform pickupRoot = null)
        {
            if (authorityHost == null || !authorityHost.IsConfigured)
                throw new ArgumentException(
                    "A configured run pickup authority host is required.",
                    nameof(authorityHost));
            this.authorityHost = authorityHost;
            this.presentationRegistry = presentationRegistry
                ?? throw new ArgumentNullException(nameof(presentationRegistry));
            this.pickupRoot = pickupRoot == null ? transform : pickupRoot;
        }

        public RunPickupPresentationSyncResultV1 Synchronize(
            StableId currentRoomStableId)
        {
            if (authorityHost == null
                || !authorityHost.IsConfigured
                || presentationRegistry == null)
            {
                LastSyncResult = new RunPickupPresentationSyncResultV1(
                    0, views.Count, 0, 0, 0, 1,
                    "run-pickup-presenter-not-configured");
                return LastSyncResult;
            }

            IReadOnlyList<RunPickupSnapshotV1> available =
                authorityHost.Authority.ExportAvailablePickups();
            var desired = new Dictionary<StableId, RunPickupSnapshotV1>();
            for (int index = 0; index < available.Count; index++)
            {
                RunPickupSnapshotV1 pickup = available[index];
                if (currentRoomStableId == null
                    || pickup.WorldSpawnContext.RoomStableId == currentRoomStableId)
                {
                    desired[pickup.PickupStableId] = pickup;
                }
            }

            int retired = RetireUndesired(desired);
            int created = 0;
            int retained = 0;
            int failed = 0;
            string firstDiagnostic = string.Empty;
            foreach (KeyValuePair<StableId, RunPickupSnapshotV1> pair in desired)
            {
                RunRewardPickup2D existing;
                if (views.TryGetValue(pair.Key, out existing)
                    && existing != null
                    && !existing.IsRetired)
                {
                    retained++;
                    continue;
                }

                RunPickupPresentationEntryV1 presentation;
                string diagnostic;
                if (!presentationRegistry.TryResolve(
                    pair.Value,
                    out presentation,
                    out diagnostic))
                {
                    failed++;
                    if (string.IsNullOrEmpty(firstDiagnostic))
                        firstDiagnostic = diagnostic;
                    continue;
                }

                RunRewardPickup2D view;
                if (!TryCreateView(pair.Value, presentation, out view, out diagnostic))
                {
                    failed++;
                    if (string.IsNullOrEmpty(firstDiagnostic))
                        firstDiagnostic = diagnostic;
                    continue;
                }
                views[pair.Key] = view;
                created++;
            }

            LastSyncResult = new RunPickupPresentationSyncResultV1(
                desired.Count,
                views.Count,
                created,
                retained,
                retired,
                failed,
                firstDiagnostic);
            return LastSyncResult;
        }

        public bool TryGetView(
            StableId pickupStableId,
            out RunRewardPickup2D view)
        {
            view = null;
            return pickupStableId != null
                && views.TryGetValue(pickupStableId, out view)
                && view != null;
        }

        internal void NotifyCollected(RunRewardPickup2D view)
        {
            if (view == null || view.PickupStableId == null) return;
            RunRewardPickup2D existing;
            if (views.TryGetValue(view.PickupStableId, out existing)
                && ReferenceEquals(existing, view))
            {
                views.Remove(view.PickupStableId);
            }
            Destroy(view.gameObject);
        }

        private int RetireUndesired(
            IDictionary<StableId, RunPickupSnapshotV1> desired)
        {
            var remove = new List<StableId>();
            foreach (KeyValuePair<StableId, RunRewardPickup2D> pair in views)
            {
                if (pair.Value == null || !desired.ContainsKey(pair.Key))
                    remove.Add(pair.Key);
            }
            for (int index = 0; index < remove.Count; index++)
            {
                RunRewardPickup2D view;
                if (views.TryGetValue(remove[index], out view) && view != null)
                    Destroy(view.gameObject);
                views.Remove(remove[index]);
            }
            return remove.Count;
        }

        private bool TryCreateView(
            RunPickupSnapshotV1 pickup,
            RunPickupPresentationEntryV1 presentation,
            out RunRewardPickup2D view,
            out string diagnostic)
        {
            view = null;
            diagnostic = string.Empty;
            GameObject instance = null;
            try
            {
                instance = presentation.Prefab == null
                    ? new GameObject("RunRewardPickup")
                    : Instantiate(presentation.Prefab);
                if (instance == null)
                {
                    diagnostic = "run-pickup-presentation-instantiation-null";
                    return false;
                }
                instance.transform.SetParent(
                    pickupRoot == null ? transform : pickupRoot,
                    false);
                view = instance.GetComponent<RunRewardPickup2D>();
                if (view == null)
                    view = instance.AddComponent<RunRewardPickup2D>();
                view.Configure(pickup, authorityHost, this, presentation);
                return true;
            }
            catch (Exception exception)
            {
                if (instance != null) Destroy(instance);
                view = null;
                diagnostic = "run-pickup-presentation-instantiation-failed:"
                    + exception.Message;
                return false;
            }
        }

        private void OnDestroy()
        {
            foreach (RunRewardPickup2D view in views.Values)
            {
                if (view != null)
                    Destroy(view.gameObject);
            }
            views.Clear();
        }
    }
}
