using System;
using System.Collections.Generic;
using ShooterMover.Domain.Common;
using ShooterMover.RunLoot;
using UnityEngine;

namespace ShooterMover.UnityAdapters.Rewards.RunLoots
{
    public sealed class RunLootPresentationSyncResult
    {
        public RunLootPresentationSyncResult(
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
    public sealed class RunLootView : MonoBehaviour
    {
        [SerializeField] private RunLootSession authorityHost;
        [SerializeField] private RunLootViews presentationRegistry;
        [SerializeField] private Transform pickupRoot;

        private readonly Dictionary<StableId, RunLoot> views =
            new Dictionary<StableId, RunLoot>();
        private readonly HashSet<RunLoot> retiringViews =
            new HashSet<RunLoot>();

        public int VisiblePickupCount { get { return views.Count; } }
        public int RetiringPickupCount { get { return retiringViews.Count; } }
        public RunLootPresentationSyncResult LastSyncResult { get; private set; }

        public void Configure(
            RunLootSession authorityHost,
            RunLootViews presentationRegistry,
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

        public RunLootPresentationSyncResult Synchronize(
            StableId currentRoomStableId)
        {
            if (authorityHost == null
                || !authorityHost.IsConfigured
                || presentationRegistry == null)
            {
                LastSyncResult = new RunLootPresentationSyncResult(
                    0, views.Count, 0, 0, 0, 1,
                    "run-pickup-presenter-not-configured");
                return LastSyncResult;
            }

            IReadOnlyList<RunLootSnapshot> available =
                authorityHost.Authority.ExportAvailablePickups();
            var desired = new Dictionary<StableId, RunLootSnapshot>();
            for (int index = 0; index < available.Count; index++)
            {
                RunLootSnapshot pickup = available[index];
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
            foreach (KeyValuePair<StableId, RunLootSnapshot> pair in desired)
            {
                RunLoot existing;
                if (views.TryGetValue(pair.Key, out existing)
                    && existing != null
                    && !existing.IsRetired)
                {
                    retained++;
                    continue;
                }

                RunLootPresentationEntry presentation;
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

                RunLoot view;
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

            LastSyncResult = new RunLootPresentationSyncResult(
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
            out RunLoot view)
        {
            view = null;
            return pickupStableId != null
                && views.TryGetValue(pickupStableId, out view)
                && view != null;
        }

        internal void BeginCollectedRetirement(RunLoot view)
        {
            if (view == null || view.PickupStableId == null)
            {
                return;
            }

            RunLoot existing;
            if (views.TryGetValue(view.PickupStableId, out existing)
                && ReferenceEquals(existing, view))
            {
                views.Remove(view.PickupStableId);
            }
            retiringViews.Add(view);
        }

        internal void CompleteCollectedRetirement(RunLoot view)
        {
            if (view == null)
            {
                return;
            }
            retiringViews.Remove(view);
            Destroy(view.gameObject);
        }

        private int RetireUndesired(
            IDictionary<StableId, RunLootSnapshot> desired)
        {
            var remove = new List<StableId>();
            foreach (KeyValuePair<StableId, RunLoot> pair in views)
            {
                if (pair.Value == null || !desired.ContainsKey(pair.Key))
                    remove.Add(pair.Key);
            }
            for (int index = 0; index < remove.Count; index++)
            {
                RunLoot view;
                if (views.TryGetValue(remove[index], out view) && view != null)
                    Destroy(view.gameObject);
                views.Remove(remove[index]);
            }
            return remove.Count;
        }

        private bool TryCreateView(
            RunLootSnapshot pickup,
            RunLootPresentationEntry presentation,
            out RunLoot view,
            out string diagnostic)
        {
            view = null;
            diagnostic = string.Empty;
            GameObject instance = null;
            try
            {
                instance = presentation.Prefab == null
                    ? new GameObject("RunLootPickup")
                    : Instantiate(presentation.Prefab);
                if (instance == null)
                {
                    diagnostic = "run-pickup-presentation-instantiation-null";
                    return false;
                }
                instance.transform.SetParent(
                    pickupRoot == null ? transform : pickupRoot,
                    false);
                view = instance.GetComponent<RunLoot>();
                if (view == null)
                    view = instance.AddComponent<RunLoot>();
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
            foreach (RunLoot view in views.Values)
            {
                if (view != null)
                    Destroy(view.gameObject);
            }
            views.Clear();

            foreach (RunLoot view in retiringViews)
            {
                if (view != null)
                    Destroy(view.gameObject);
            }
            retiringViews.Clear();
        }
    }
}
