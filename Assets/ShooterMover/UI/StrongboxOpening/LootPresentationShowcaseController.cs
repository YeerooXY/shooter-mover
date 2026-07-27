using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using ShooterMover.Domain.Common;
using UnityEngine;

namespace ShooterMover.UI.StrongboxOpening
{
    /// <summary>
    /// Development-only composition for LOOT-PRESENTATION-001. The reusable bound views
    /// are production-capable projection surfaces; only the supplied fixture data is disposable.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed partial class LootPresentationShowcaseController : MonoBehaviour
    {
        [SerializeField, Min(0.05f)] private float openingDurationSeconds = 1.1f;
        [SerializeField, Min(0.05f)] private float revealIntervalSeconds = 0.24f;
        [SerializeField, Min(1f)] private float fastForwardMultiplier = 6f;

        private readonly List<LootPickupVisual2D> galleryViews =
            new List<LootPickupVisual2D>();
        private readonly RunLootTotalsPresentationV1 runTotals =
            new RunLootTotalsPresentationV1(1250L, 84L, 0L);
        private IReadOnlyList<LootPickupPresentationV1> gallery;
        private LootRunHudViewV1 runHudView;
        private OwnedStrongboxGroupsViewV1 ownedGroupsView;
        private StrongboxRewardCardsViewV1 rewardCardsView;
        private StrongboxOpeningPresentationViewV1 openingPresentationView;
        private StrongboxOpeningPresentationResultV1 immutableFixtureResult;
        private StrongboxOpeningSceneSessionV1 openingSession;
        private IReadOnlyList<StableId> lastOpeningBatch = Array.Empty<StableId>();
        private string lastOpeningTierId = "strongbox-tier.steel";
        private string lastOpeningTierLabel = "Steel";
        private int openCount = 1;
        private bool fastForward;
        private string diagnostic = string.Empty;
        private DevelopmentPickupAuthorityFixtureV1 pickupFixture;
        private LootPickupVisual2D pickupFixtureView;
        private DevelopmentPickupCollectionResultV1 lastPickupResult;
        private GUIStyle titleStyle;
        private GUIStyle headingStyle;
        private GUIStyle bodyStyle;
        private GUIStyle warningStyle;
        private bool initialized;

        public IReadOnlyList<OwnedStrongboxGroupPresentationV1> Groups
        {
            get
            {
                EnsureInitialized();
                return ownedGroupsView.Groups;
            }
        }

        public ExactStrongboxSelectionV1 Selection
        {
            get
            {
                EnsureInitialized();
                return ownedGroupsView.Selection;
            }
        }

        public StrongboxOpeningPresentationResultV1 ImmutableFixtureResult
        {
            get
            {
                EnsureInitialized();
                return immutableFixtureResult;
            }
        }

        public StrongboxOpeningSceneSessionV1 OpeningSession
        {
            get
            {
                EnsureInitialized();
                return openingSession;
            }
        }

        public IReadOnlyList<StableId> LastOpeningBatch { get { return lastOpeningBatch; } }
        public RunLootTotalsPresentationV1 RunTotals { get { return runTotals; } }
        public LootRunHudViewV1 RunHudView { get { EnsureInitialized(); return runHudView; } }
        public OwnedStrongboxGroupsViewV1 OwnedGroupsView
        {
            get
            {
                EnsureInitialized();
                return ownedGroupsView;
            }
        }

        public StrongboxOpeningPresentationViewV1 OpeningPresentationView
        {
            get
            {
                EnsureInitialized();
                return openingPresentationView;
            }
        }

        public bool IsPickupFixtureViewVisible
        {
            get { return pickupFixtureView != null && pickupFixtureView.IsVisible; }
        }

        public bool IsPickupFixtureCollected
        {
            get { return pickupFixture != null && pickupFixture.IsCollected; }
        }

        private void Awake()
        {
            if (!Application.isEditor && !Debug.isDebugBuild)
            {
                Debug.LogWarning(
                    "LootPresentationShowcase is disabled outside Editor/development builds.");
                enabled = false;
                return;
            }
            EnsureInitialized();
        }

        private void Update()
        {
            if (!initialized || openingSession == null)
            {
                return;
            }

            float multiplier = fastForward
                ? Mathf.Max(1f, fastForwardMultiplier)
                : 1f;
            openingSession.Advance(Time.unscaledDeltaTime * multiplier);
            if (openingPresentationView != null)
            {
                openingPresentationView.Synchronize();
            }
        }

        public bool SelectExactForTests(StableId instanceStableId)
        {
            EnsureInitialized();
            return ownedGroupsView.TrySelectExact(instanceStableId, out diagnostic);
        }

        public void SetOpenCount(int count)
        {
            if (count != 1 && count != 5)
            {
                throw new ArgumentOutOfRangeException(nameof(count));
            }
            openCount = count;
        }

        public bool PlayOpening()
        {
            EnsureInitialized();
            IReadOnlyList<StableId> batch;
            string batchDiagnostic;
            if (!ownedGroupsView.TryResolveBatchExact(
                openCount,
                out batch,
                out batchDiagnostic))
            {
                diagnostic = batchDiagnostic;
                return false;
            }

            OwnedStrongboxGroupPresentationV1 selectedGroup =
                ownedGroupsView.SelectedGroup;
            if (selectedGroup == null)
            {
                diagnostic = "loot-presentation-opening-selected-group-missing";
                return false;
            }

            lastOpeningBatch =
                new ReadOnlyCollection<StableId>(new List<StableId>(batch));
            lastOpeningTierId = selectedGroup.TierStableId.ToString();
            lastOpeningTierLabel = selectedGroup.TierLabel;
            openingSession = CreateOpeningSession(
                lastOpeningTierId,
                lastOpeningTierLabel);
            openingPresentationView.Bind(openingSession, rewardCardsView);
            diagnostic = string.Empty;
            return openingSession.RequestOpen();
        }

        public bool ReplayPresentation()
        {
            EnsureInitialized();
            if (lastOpeningBatch.Count == 0)
            {
                diagnostic = "loot-presentation-replay-missing-frozen-batch";
                return false;
            }

            openingSession = CreateOpeningSession(
                lastOpeningTierId,
                lastOpeningTierLabel);
            openingPresentationView.Bind(openingSession, rewardCardsView);
            diagnostic = string.Empty;
            return openingSession.RequestOpen();
        }

        public bool SkipPresentation()
        {
            EnsureInitialized();
            bool skipped =
                StrongboxPresentationPlaybackV1.SkipToComplete(openingSession);
            if (!skipped)
            {
                diagnostic = "loot-presentation-skip-not-available";
                return false;
            }

            openingPresentationView.Synchronize();
            diagnostic = string.Empty;
            return true;
        }

        public void DestroyPickupFixtureView()
        {
            if (pickupFixtureView != null)
            {
                Destroy(pickupFixtureView.gameObject);
                pickupFixtureView = null;
            }
        }

        public bool ReconstructPickupFixtureView()
        {
            EnsureInitialized();
            DestroyPickupFixtureView();
            LootPickupPresentationV1 available = pickupFixture.ExportAvailable();
            if (available == null)
            {
                diagnostic = "development-pickup-fixture-no-available-pickup";
                return false;
            }

            pickupFixtureView = CreateVisual(
                available,
                new Vector3(0f, -4.1f, 0f),
                "AuthoritativePickupFixtureView");
            diagnostic = string.Empty;
            return true;
        }

        public DevelopmentPickupCollectionResultV1 CollectPickupFixture()
        {
            EnsureInitialized();
            lastPickupResult = pickupFixture.Collect();
            diagnostic = lastPickupResult.Diagnostic;
            if (!lastPickupResult.Accepted)
            {
                return lastPickupResult;
            }

            if (pickupFixtureView != null)
            {
                pickupFixtureView.PlayAcceptedCollectionFeedback(Vector3.zero);
            }
            return lastPickupResult;
        }

        public void RejectNextPickupCollection()
        {
            EnsureInitialized();
            pickupFixture.RejectNextCollection();
        }

        private static T GetOrAddComponent<T>(GameObject owner)
            where T : Component
        {
            T existing = owner.GetComponent<T>();
            return existing == null ? owner.AddComponent<T>() : existing;
        }

        private static void EnsureCamera()
        {
            Camera camera = Camera.main;
            if (camera == null)
            {
                GameObject cameraObject = new GameObject("LootPresentationCamera");
                cameraObject.tag = "MainCamera";
                camera = cameraObject.AddComponent<Camera>();
                camera.transform.position = new Vector3(0f, 0f, -10f);
            }

            camera.orthographic = true;
            camera.orthographicSize = 7.2f;
            camera.backgroundColor = new Color(0.015f, 0.02f, 0.035f, 1f);
        }
    }
}
