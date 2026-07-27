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
using UnityEngine.SceneManagement;

namespace ShooterMover.UI.ProductionFlow
{
    [DefaultExecutionOrder(20000)]
    [DisallowMultipleComponent]
    public sealed class ProductionRunRewardSceneCompositionV1 : MonoBehaviour
    {
        private ProductionRunRewardRuntimeV1 runtime;
        private string diagnostic = string.Empty;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetHook()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void InstallHook()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.sceneLoaded += HandleSceneLoaded;
            InstallForScene(SceneManager.GetActiveScene());
        }

        private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            InstallForScene(scene);
        }

        private static void InstallForScene(Scene scene)
        {
            if (!scene.IsValid()
                || !string.Equals(
                    scene.path,
                    ProductionPlayableLevelCatalogV1.PlayableLevelScenePath,
                    StringComparison.Ordinal))
            {
                return;
            }

            ProductionPlayableLevelControllerV1 controller =
                FindInScene<ProductionPlayableLevelControllerV1>(scene);
            if (controller != null
                && controller.GetComponent<ProductionRunRewardSceneCompositionV1>() == null)
            {
                controller.gameObject.AddComponent<ProductionRunRewardSceneCompositionV1>();
            }
        }

        private void Start()
        {
            try
            {
                Compose();
            }
            catch (Exception exception)
            {
                diagnostic = exception.GetType().Name + ": " + exception.Message;
                Debug.LogException(exception, this);
            }
        }

        private void Compose()
        {
            ProductionPlayableLevelControllerV1 controller =
                GetComponent<ProductionPlayableLevelControllerV1>();
            if (controller == null || !controller.IsConfigured)
            {
                throw new InvalidOperationException(
                    "The production playable level must be configured before run rewards compose.");
            }

            PlayerRouteProfilePayloadV1 route;
            StableId modeId;
            StableId levelId;
            if (!LevelSelectionRouteContextV1.TryRead(out route, out modeId, out levelId)
                || levelId == null
                || controller.LevelStableId != levelId)
            {
                throw new InvalidOperationException(
                    "The selected level route is missing or does not match the configured scene.");
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
                || route == null
                || !graph.RoutePayload.Equals(route))
            {
                throw new InvalidOperationException(
                    "The exact selected account-backed character graph is unavailable.");
            }

            JsonRoomRuntimeBootstrap2D roomBootstrap =
                FindInScene<JsonRoomRuntimeBootstrap2D>(gameObject.scene);
            RoomRuntimeComposition2D rooms =
                FindInScene<RoomRuntimeComposition2D>(gameObject.scene);
            RoomEnemySpawner2D spawner =
                FindInScene<RoomEnemySpawner2D>(gameObject.scene);
            if (roomBootstrap == null
                || !roomBootstrap.IsBuilt
                || roomBootstrap.ImportedBundle == null
                || rooms == null
                || !rooms.IsBuilt
                || spawner == null)
            {
                throw new InvalidOperationException(
                    "The authored room runtime is not ready for run/reward composition.");
            }

            StableId proofRoomId = StableId.Parse("room.level1-entry");
            List<RoomEnemyPlacementContentV1> proofRows = roomBootstrap.ImportedBundle
                .Enemies
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
            if (!spawner.Synchronize())
            {
                throw new InvalidOperationException(
                    "The authored room enemy runtime rejected the selected-character run composition: "
                    + spawner.LastBuildError);
            }
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
            runtime = null;
        }

        private static T FindInScene<T>(Scene scene) where T : Component
        {
            GameObject[] roots = scene.GetRootGameObjects();
            for (int index = 0; index < roots.Length; index++)
            {
                T value = roots[index].GetComponentInChildren<T>(true);
                if (value != null) return value;
            }
            return null;
        }
    }
}