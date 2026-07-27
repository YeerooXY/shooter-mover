using System;
using System.Collections.Generic;
using System.Linq;
using ShooterMover.Application.Flow.Production;
using ShooterMover.Application.Missions.Rooms.Content;
using ShooterMover.Content.Definitions.Levels.Selection;
using ShooterMover.Contracts.Flow.Session;
using ShooterMover.Domain.Common;
using ShooterMover.UI.LevelSelection;
using ShooterMover.UnityAdapters.Enemies;
using ShooterMover.UnityAdapters.Missions.Rooms;
using UnityEngine;

namespace ShooterMover.UI.ProductionFlow
{
    /// <summary>
    /// Scene-owned bridge that composes the selected-character Run Session synchronously
    /// at the accepted room-build boundary. Enemy downstream ports are therefore frozen
    /// before the production controller performs its first enemy synchronization.
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    [DisallowMultipleComponent]
    public sealed class ProductionRunRewardSceneCompositionV1 : MonoBehaviour
    {
        private static readonly StableId SoloPlayModeId =
            StableId.Parse("play-mode.solo");
        private static readonly StableId CampaignRewardGameModeId =
            StableId.Parse("game-mode.campaign");

        private ProductionPlayableLevelControllerV1 controller;
        private JsonRoomRuntimeBootstrap2D roomBootstrap;
        private RoomRuntimeComposition2D rooms;
        private RoomEnemySpawner2D spawner;
        private ProductionRunRewardRuntimeV1 runtime;
        private string diagnostic = string.Empty;

        public string Diagnostic { get { return diagnostic; } }

        private void Awake()
        {
            controller = GetComponent<ProductionPlayableLevelControllerV1>();
            roomBootstrap = GetComponent<JsonRoomRuntimeBootstrap2D>();
            rooms = GetComponent<RoomRuntimeComposition2D>();
            spawner = GetComponent<RoomEnemySpawner2D>();
            if (controller == null || roomBootstrap == null || rooms == null || spawner == null)
            {
                diagnostic = "run-reward-scene-composition-references-missing";
                Debug.LogError(diagnostic, this);
                return;
            }

            roomBootstrap.BuildAccepted += HandleRoomBuildAccepted;
            if (roomBootstrap.IsBuilt && roomBootstrap.ImportedBundle != null)
            {
                HandleRoomBuildAccepted(roomBootstrap.ImportedBundle);
            }
        }

        private void HandleRoomBuildAccepted(RoomContentBundleV1 acceptedBundle)
        {
            if (runtime != null)
            {
                throw new InvalidOperationException(
                    "The production run/reward composition is already frozen.");
            }
            Compose(acceptedBundle);
        }

        private void Compose(RoomContentBundleV1 acceptedBundle)
        {
            if (acceptedBundle == null
                || !roomBootstrap.IsBuilt
                || !ReferenceEquals(roomBootstrap.ImportedBundle, acceptedBundle)
                || !rooms.IsBuilt)
            {
                throw new InvalidOperationException(
                    "The accepted authored room bundle is unavailable for run/reward composition.");
            }

            PlayerRouteProfilePayloadV1 route;
            StableId modeId;
            StableId levelId;
            if (!LevelSelectionRouteContextV1.TryRead(out route, out modeId, out levelId)
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

            ProductionPlayableLevelDefinitionV1 level;
            if (!ProductionPlayableLevelCatalogV1.TryResolve(levelId, out level)
                || level == null)
            {
                throw new InvalidOperationException(
                    "The selected production level definition is unavailable: " + levelId);
            }

            ProductionCharacterRuntimeGraphV1 graph;
            ProductionFlowProfileRecordV1 profile;
            ShooterMover.Application.Persistence.Composition.CharacterCompositionCoordinatorV1 coordinator;
            if (!ProductionCharacterAccountCompositionV1.TryResolveCurrent(
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

            StableId proofRoomId = StableId.Parse("room.level1-entry");
            List<RoomEnemyPlacementContentV1> proofRows = acceptedBundle.Enemies
                .Where(row => row != null && row.RoomStableId == proofRoomId)
                .ToList();
            if (levelId != ProductionPlayableLevelCatalogV1.FirstLevelStableId
                || proofRows.Count != 1
                || proofRows[0].InstanceStableId == null)
            {
                throw new InvalidOperationException(
                    "RUN-REWARD-COMPOSITION-001 requires exactly one stable proof enemy "
                    + "in the authored Level 1 entry room.");
            }

            EnemyCatalogAsset2D enemyAsset = Resources.Load<EnemyCatalogAsset2D>(
                level.EnemyCatalogResourcePath);
            if (enemyAsset == null)
            {
                throw new InvalidOperationException(
                    "The selected level enemy catalog asset is unavailable.");
            }
            EnemyCatalogImportResultV1 imported = enemyAsset.Import();
            if (imported == null || !imported.IsValid || imported.Catalog == null)
            {
                throw new InvalidOperationException(
                    "The selected level enemy catalog did not import successfully.");
            }

            runtime = ProductionRunRewardRuntimeV1.Create(
                level,
                CampaignRewardGameModeId,
                graph,
                coordinator,
                rooms,
                imported.Catalog,
                proofRoomId,
                proofRows[0].InstanceStableId);
            spawner.ConfigureRunDownstream(
                runtime.RunStableId,
                runtime.ExperienceConsumer,
                runtime.DropConsumer,
                runtime.KillStatisticsConsumer);
            diagnostic = string.Empty;
        }

        private void OnGUI()
        {
            if (runtime == null) return;
            PendingRunRewardProjectionV1 projection = runtime.ExportPendingProjection();
            if (projection.AcceptedAdmissionCount < 1) return;

            GUI.Box(new Rect(16f, 16f, 190f, 108f), string.Empty);
            GUI.Label(new Rect(28f, 24f, 170f, 22f), "Pending rewards:");
            GUI.Label(new Rect(28f, 48f, 170f, 20f), "Cash: " + projection.Cash);
            GUI.Label(new Rect(28f, 68f, 170f, 20f), "Scrap: " + projection.Scrap);
            GUI.Label(new Rect(28f, 88f, 170f, 20f),
                "Strongboxes: " + projection.Strongboxes);
        }

        private void OnDestroy()
        {
            if (roomBootstrap != null)
            {
                roomBootstrap.BuildAccepted -= HandleRoomBuildAccepted;
            }
            runtime = null;
        }
    }
}
