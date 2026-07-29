using System;
using System.Collections.Generic;
using System.Linq;
using ShooterMover.Application.Enemies.Catalog;
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
    public sealed class RunRewardSceneSetup : MonoBehaviour
    {
        private const string ProofAuthoredId = "run-reward-proof";
        private static readonly StableId SoloPlayModeId =
            StableId.Parse("play-mode.solo");
        private static readonly StableId CampaignRewardGameModeId =
            StableId.Parse("game-mode.campaign");

        private PlayableLevelController controller;
        private JsonRoomLiveBootstrap2D roomBootstrap;
        private RoomLiveSetup2D rooms;
        private RoomEnemySpawner2D spawner;
        private RunRewardLive runtime;
        private string diagnostic = string.Empty;

        public string Diagnostic { get { return diagnostic; } }

        private void Awake()
        {
            controller = GetComponent<PlayableLevelController>();
            roomBootstrap = GetComponent<JsonRoomLiveBootstrap2D>();
            rooms = GetComponent<RoomLiveSetup2D>();
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
            ShooterMover.Application.Persistence.Composition.CharacterSetupFlow coordinator;
            if (!CharacterAccount.TryResolveCurrent(
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
            List<RoomEnemyPlacementContent> proofRows = acceptedBundle.Enemies
                .Where(row => row != null
                    && row.RoomStableId == proofRoomId
                    && string.Equals(
                        row.AuthoredId,
                        ProofAuthoredId,
                        StringComparison.Ordinal))
                .ToList();
            if (levelId != PlayableLevelCatalog.FirstLevelStableId
                || proofRows.Count != 1
                || proofRows[0].InstanceStableId == null)
            {
                throw new InvalidOperationException(
                    "RUN-REWARD-COMPOSITION-001 requires one exact authored proof enemy "
                    + ProofAuthoredId + " in the Level 1 entry room.");
            }

            EnemyCatalogAsset2D enemyAsset = Resources.Load<EnemyCatalogAsset2D>(
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

            runtime = RunRewardLive.Create(
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
            PendingRunRewardView projection = runtime.ExportPendingProjection();
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
