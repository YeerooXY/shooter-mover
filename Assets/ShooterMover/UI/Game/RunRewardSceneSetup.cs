using System;
using System.Collections.Generic;
using ShooterMover.Application.Enemies.Catalog;
using ShooterMover.Application.Flow.Game;
using ShooterMover.Application.Missions.Rooms.Content;
using ShooterMover.Application.Persistence.Composition;
using ShooterMover.Content.Definitions.Levels.Selection;
using ShooterMover.Contracts.Flow.Session;
using ShooterMover.Contracts.Rewards;
using ShooterMover.Domain.Common;
using ShooterMover.EnemyRuntimeComposition;
using ShooterMover.RunLoot;
using ShooterMover.UI.LevelSelection;
using ShooterMover.UI.StrongboxOpening;
using ShooterMover.UnityAdapters.Enemies;
using ShooterMover.UnityAdapters.Missions.Rooms;
using ShooterMover.UnityAdapters.Rewards.RunLoots;
using UnityEngine;

namespace ShooterMover.UI.Game
{
    /// <summary>
    /// Scene-owned bridge that composes the selected-character Run Session and the existing
    /// physical RunLoot architecture at the accepted room-build boundary.
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    [DisallowMultipleComponent]
    public sealed class RunRewards : MonoBehaviour
    {
        private static readonly StableId SoloPlayModeId =
            StableId.Parse("play-mode.solo");
        private static readonly StableId CampaignRewardGameModeId =
            StableId.Parse("game-mode.campaign");

        private LevelGame controller;
        private RoomLoader roomBootstrap;
        private LevelRooms rooms;
        private RoomEnemies spawner;
        private RunLoot runtime;
        private RunFinish completion;
        private LootBridge pickupBridge;
        private RunLootPositions pickupPositions;
        private RunLootLiveSetup pickupLiveSetup;
        private RunLootSession pickupSession;
        private RunLootViews pickupViews;
        private RunLootView pickupView;
        private GameObject pickupCompositionRoot;
        private GameObject pickupPresentationPrefab;
        private RunLootCollector collector;
        private long observedLifecycleGeneration;
        private string diagnostic = string.Empty;

        public string Diagnostic { get { return diagnostic; } }

        private void Awake()
        {
            controller = GetComponent<LevelGame>();
            roomBootstrap = GetComponent<RoomLoader>();
            rooms = GetComponent<LevelRooms>();
            spawner = GetComponent<RoomEnemies>();
            if (controller == null || roomBootstrap == null || rooms == null || spawner == null)
            {
                diagnostic = "run-reward-scene-composition-references-missing";
                Debug.LogError(diagnostic, this);
                return;
            }

            roomBootstrap.BuildAccepted += HandleRoomBuildAccepted;
            rooms.CurrentRoomPresentationRebuilt +=
                HandleRoomPresentationRebuilt;
            if (roomBootstrap.IsBuilt && roomBootstrap.ImportedBundle != null)
            {
                HandleRoomBuildAccepted(roomBootstrap.ImportedBundle);
            }
        }

        private void Update()
        {
            if (runtime == null
                || pickupBridge == null
                || pickupView == null)
            {
                return;
            }

            TryBindPlayerCollector();

            long generation = runtime.Run.LifecycleGeneration;
            if (generation != observedLifecycleGeneration)
            {
                pickupBridge.RetireOtherLifecycles(
                    runtime.RunStableId,
                    generation);
                observedLifecycleGeneration = generation;
                SynchronizeCurrentRoomPickups();
            }

            if (pickupBridge.PendingCount > 0)
            {
                pickupBridge.ProcessPending();
                SynchronizeCurrentRoomPickups();
            }
        }

        private void HandleRoomBuildAccepted(RoomContentBundle acceptedBundle)
        {
            if (runtime != null)
            {
                throw new InvalidOperationException(
                    "The production run/reward composition is already frozen.");
            }
            Compose(acceptedBundle);
        }

        private void Compose(RoomContentBundle acceptedBundle)
        {
            if (acceptedBundle == null
                || !roomBootstrap.IsBuilt
                || !ReferenceEquals(roomBootstrap.ImportedBundle, acceptedBundle)
                || !rooms.IsBuilt)
            {
                throw new InvalidOperationException(
                    "The accepted authored room bundle is unavailable for run/reward composition.");
            }

            PlayerRouteProfilePayload route;
            StableId modeId;
            StableId levelId;
            if (!LevelSelectionRouteContext.TryRead(out route, out modeId, out levelId)
                || route == null
                || modeId == null
                || levelId == null
                || controller.LevelStableId != levelId)
            {
                throw new InvalidOperationException(
                    "The selected mode/level route is missing or does not match the composing scene.");
            }
            if (modeId != SoloPlayModeId)
            {
                throw new InvalidOperationException(
                    "No production reward game-mode mapping exists for play mode " + modeId);
            }

            PlayableLevelDefinition level;
            if (!PlayableLevelCatalog.TryResolve(levelId, out level)
                || level == null)
            {
                throw new InvalidOperationException(
                    "The selected production level definition is unavailable: " + levelId);
            }

            CharacterLiveGraph graph;
            FlowProfileRecord profile;
            CharacterSetupFlow coordinator;
            if (!CharacterSave.TryResolveCurrent(
                    out graph,
                    out profile,
                    out coordinator)
                || graph == null
                || profile == null
                || coordinator == null
                || graph.IsDisposed
                || !route.HasValidFingerprint()
                || !graph.RoutePayload.Equals(route)
                || !profile.Payload.Equals(route)
                || controller.CharacterInstanceStableId
                    != graph.Character.CharacterInstanceStableId)
            {
                throw new InvalidOperationException(
                    "The exact selected account-backed character graph is unavailable.");
            }

            EnemyCatalogAsset enemyAsset = Resources.Load<EnemyCatalogAsset>(
                level.EnemyCatalogResourcePath);
            if (enemyAsset == null)
            {
                throw new InvalidOperationException(
                    "The selected level enemy catalog asset is unavailable.");
            }
            EnemyCatalogImportResult imported = enemyAsset.Import();
            if (imported == null || !imported.IsValid || imported.Catalog == null)
            {
                throw new InvalidOperationException(
                    "The selected level enemy catalog did not import successfully.");
            }

            pickupBridge = new LootBridge();
            runtime = RunLoot.Create(
                level,
                CampaignRewardGameModeId,
                graph,
                coordinator,
                rooms,
                imported.Catalog,
                pickupBridge);
            ComposePhysicalPickupRuntime();

            IEnemyDropFactConsumer physicalDropConsumer =
                new LootDropper(
                    spawner,
                    runtime.Run,
                    pickupBridge,
                    runtime.DropConsumer);
            spawner.ConfigureRunDownstream(
                runtime.RunStableId,
                runtime.ExperienceConsumer,
                physicalDropConsumer,
                runtime.KillStatisticsConsumer);

            completion = new RunFinish(
                runtime,
                graph,
                coordinator);
            controller.ConfigureRunCompletion(completion.Complete);
            observedLifecycleGeneration = runtime.Run.LifecycleGeneration;
            pickupBridge.RetireOtherLifecycles(
                runtime.RunStableId,
                observedLifecycleGeneration);
            pickupBridge.ProcessPending();
            SynchronizeCurrentRoomPickups();
            diagnostic = string.Empty;
        }

        private void ComposePhysicalPickupRuntime()
        {
            pickupCompositionRoot = new GameObject(
                "Production Run Loot Composition");
            pickupCompositionRoot.transform.SetParent(transform, false);

            pickupPositions = new RunLootPositions();
            pickupLiveSetup = RunLootLiveSetup.Create(
                runtime.Run,
                pickupPositions);

            pickupSession =
                pickupCompositionRoot.AddComponent<RunLootSession>();
            pickupSession.Configure(pickupLiveSetup.Authority);

            pickupViews =
                pickupCompositionRoot.AddComponent<RunLootViews>();
            pickupPresentationPrefab = CreatePickupPresentationPrefab(
                pickupCompositionRoot.transform);
            pickupViews.Configure(CreatePresentationEntries(
                pickupPresentationPrefab));

            pickupView =
                pickupCompositionRoot.AddComponent<RunLootView>();
            pickupView.Configure(
                pickupSession,
                pickupViews,
                pickupCompositionRoot.transform);

            pickupBridge.ConfigureRuntime(
                pickupPositions,
                pickupLiveSetup.PendingConsumer,
                pickupView);
        }

        private static GameObject CreatePickupPresentationPrefab(
            Transform owner)
        {
            var prefab = new GameObject(
                "Runtime Loot Pickup Presentation");
            prefab.transform.SetParent(owner, false);
            prefab.AddComponent<LootVisual>();
            prefab.AddComponent<LootRunView>();
            prefab.SetActive(false);
            return prefab;
        }

        private static IEnumerable<RunLootPresentationEntry>
            CreatePresentationEntries(GameObject prefab)
        {
            return new[]
            {
                CreatePresentationEntry(
                    RewardGrantKind.Money,
                    prefab,
                    "CREDITS"),
                CreatePresentationEntry(
                    RewardGrantKind.Scrap,
                    prefab,
                    "SCRAP"),
                CreatePresentationEntry(
                    RewardGrantKind.Strongbox,
                    prefab,
                    "STRONGBOX"),
            };
        }

        private static RunLootPresentationEntry CreatePresentationEntry(
            RewardGrantKind kind,
            GameObject prefab,
            string label)
        {
            var entry = new RunLootPresentationEntry();
            entry.Configure(
                kind,
                null,
                prefab,
                null,
                Vector3.one,
                0.75f,
                label);
            return entry;
        }

        private void TryBindPlayerCollector()
        {
            if (collector != null
                || !controller.IsConfigured
                || pickupLiveSetup == null)
            {
                return;
            }

            PlayerMarker player =
                controller.GetComponentInChildren<PlayerMarker>(true);
            if (player == null
                || player.CharacterInstanceStableId
                    != controller.CharacterInstanceStableId)
            {
                return;
            }

            collector = player.GetComponent<RunLootCollector>()
                ?? player.gameObject.AddComponent<RunLootCollector>();
            collector.Configure(
                pickupLiveSetup.RunSessionPort.PlayerActorStableId,
                pickupLiveSetup.RunSessionPort.PlayerParticipantStableId);
        }

        private void HandleRoomPresentationRebuilt()
        {
            if (runtime == null) return;
            pickupBridge.ProcessPending();
            SynchronizeCurrentRoomPickups();
        }

        private void SynchronizeCurrentRoomPickups()
        {
            if (pickupView == null
                || rooms == null
                || !rooms.IsBuilt)
            {
                return;
            }

            RunLootPresentationSyncResult result =
                pickupView.Synchronize(rooms.CurrentRoomStableId);
            if (result != null && !result.Succeeded)
            {
                diagnostic = string.IsNullOrWhiteSpace(result.Diagnostic)
                    ? "run-pickup-presentation-sync-rejected"
                    : result.Diagnostic;
            }
        }

        private void OnDestroy()
        {
            if (roomBootstrap != null)
            {
                roomBootstrap.BuildAccepted -= HandleRoomBuildAccepted;
            }
            if (rooms != null)
            {
                rooms.CurrentRoomPresentationRebuilt -=
                    HandleRoomPresentationRebuilt;
            }

            if (pickupBridge != null)
            {
                pickupBridge.ProcessPending();
                if (pickupView != null && rooms != null && rooms.IsBuilt)
                {
                    pickupView.Synchronize(rooms.CurrentRoomStableId);
                }
                pickupBridge.ReleaseRuntime();
                pickupBridge.ClearAll();
            }
            if (pickupCompositionRoot != null)
            {
                Destroy(pickupCompositionRoot);
            }

            collector = null;
            pickupPresentationPrefab = null;
            pickupView = null;
            pickupViews = null;
            pickupSession = null;
            pickupLiveSetup = null;
            pickupPositions = null;
            pickupBridge = null;
            runtime = null;
            completion = null;
        }
    }
}
