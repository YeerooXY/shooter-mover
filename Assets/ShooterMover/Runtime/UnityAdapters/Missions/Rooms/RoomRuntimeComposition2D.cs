using System;
using ShooterMover.Application.Missions.Rooms;
using ShooterMover.Contracts.Missions.Rooms;
using ShooterMover.Domain.Common;
using ShooterMover.UnityAdapters.Authoring.LevelDesign;
using UnityEngine;

namespace ShooterMover.UnityAdapters.Missions.Rooms
{
    /// <summary>
    /// Unity composition boundary for the authorable room runtime. All mutations flow
    /// through the coordinated live authority; callers receive only immutable query data.
    /// </summary>
    [DefaultExecutionOrder(200)]
    [DisallowMultipleComponent]
    public sealed class RoomRuntimeComposition2D : MonoBehaviour
    {
        [SerializeField] private AuthorableRoomGraphDefinition2D definitionAsset;
        [SerializeField] private RoomPresentationCatalog2D presentationCatalog;
        [SerializeField] private Transform roomPresentationRoot;
        [SerializeField] private bool buildOnAwake = true;
        [SerializeField] private string runtimeInstanceStableId =
            "room-runtime-instance.unassigned";

        private readonly RoomPresentationScene2D presentation =
            new RoomPresentationScene2D();
        private AuthorableRoomGraphDefinitionV1 configuredDefinition;
        private RoomLiveRuntimeAuthorityV1 authority;

        public event Action FinalExitReached;

        public bool IsBuilt
        {
            get { return authority != null; }
        }

        public AuthorableRoomGraphDefinitionV1 Definition
        {
            get { return configuredDefinition; }
        }

        public IRoomLiveRuntimeQueryV1 Query
        {
            get { return authority; }
        }

        public RoomLiveRuntimeProjectionV1 CurrentProjection
        {
            get { return authority == null ? null : authority.CurrentProjection; }
        }

        public StableId CurrentRoomStableId
        {
            get
            {
                return CurrentProjection == null
                    ? null
                    : CurrentProjection.CurrentRoomStableId;
            }
        }

        public StableId CurrentSpawnPointStableId
        {
            get
            {
                return CurrentProjection == null
                    ? null
                    : CurrentProjection.CurrentSpawnPointStableId;
            }
        }

        public int SpawnedPlacementCount
        {
            get { return presentation.SpawnedPlacementCount; }
        }

        public int SpawnedDoorCount
        {
            get { return presentation.SpawnedDoorCount; }
        }

        public void ConfigureForTests(
            AuthorableRoomGraphDefinitionV1 definition,
            RoomPresentationCatalog2D catalog,
            Transform presentationRoot = null)
        {
            if (IsBuilt)
            {
                throw new InvalidOperationException(
                    "Room runtime composition is already built.");
            }

            configuredDefinition = definition
                ?? throw new ArgumentNullException(nameof(definition));
            presentationCatalog = catalog
                ?? throw new ArgumentNullException(nameof(catalog));
            roomPresentationRoot = presentationRoot;
            buildOnAwake = false;
        }

        public void BuildSession()
        {
            BuildSession(StableId.Parse(runtimeInstanceStableId));
        }

        public void BuildSession(StableId stableRuntimeInstanceId)
        {
            if (IsBuilt)
            {
                throw new InvalidOperationException(
                    "Room runtime composition is already built.");
            }

            if (configuredDefinition == null)
            {
                if (definitionAsset == null)
                {
                    throw new InvalidOperationException(
                        "An authorable room definition is required.");
                }

                configuredDefinition = definitionAsset.BuildDefinition();
            }

            if (presentationCatalog == null)
            {
                throw new InvalidOperationException(
                    "A room presentation catalog is required.");
            }

            EnsurePresentationRoot();
            presentationCatalog.ValidateFor(configuredDefinition);
            authority = new RoomLiveRuntimeAuthorityV1(
                stableRuntimeInstanceId,
                configuredDefinition);
            RebuildCurrentRoomPresentation();
        }

        public bool TryGetSpawnedPlacement(
            StableId instanceStableId,
            out RoomPlacedInstance2D instance)
        {
            return presentation.TryGetPlacement(instanceStableId, out instance);
        }

        public bool TryGetSpawnedDoor(
            StableId doorInstanceStableId,
            out RoomDoorInstance2D door)
        {
            return presentation.TryGetDoor(doorInstanceStableId, out door);
        }

        public Vector2 GetCurrentSpawnPosition()
        {
            RequireBuilt();
            AuthorableRoomDefinitionV1 room = configuredDefinition.GetRoom(
                CurrentProjection.CurrentRoomStableId);
            RoomSpawnPointDefinitionV1 spawnPoint;
            if (!room.TryGetSpawnPoint(
                CurrentProjection.CurrentSpawnPointStableId,
                out spawnPoint))
            {
                throw new InvalidOperationException(
                    "Current room spawn point is missing from the authored definition.");
            }

            return new Vector2(
                (float)spawnPoint.LocalPosition.X,
                (float)spawnPoint.LocalPosition.Y);
        }

        public RoomLiveOperationResultV1 ReportOccupantTerminal(
            StableId operationStableId,
            StableId roomStableId,
            StableId occupantInstanceStableId)
        {
            RequireBuilt();
            RoomLiveOperationResultV1 result = authority.ReportOccupantTerminal(
                operationStableId,
                roomStableId,
                occupantInstanceStableId);
            if (roomStableId == CurrentRoomStableId
                && result.Status != RoomLiveOperationStatusV1.Rejected)
            {
                RoomLiveRoomProjectionV1 room = authority.GetRoomProjection(roomStableId);
                presentation.RemoveDefeated(room);
                presentation.SynchronizeDoors(room);
            }

            return result;
        }

        public RoomLiveOperationResultV1 ReportDropCollected(
            StableId operationStableId,
            StableId roomStableId,
            StableId dropInstanceStableId)
        {
            RequireBuilt();
            RoomLiveOperationResultV1 result = authority.ReportDropCollected(
                operationStableId,
                roomStableId,
                dropInstanceStableId);
            if (roomStableId == CurrentRoomStableId
                && result.Status != RoomLiveOperationStatusV1.Rejected)
            {
                presentation.SynchronizeDoors(authority.GetRoomProjection(roomStableId));
            }

            return result;
        }

        public RoomLiveOperationResultV1 Traverse(
            StableId operationStableId,
            StableId exitStableId)
        {
            RequireBuilt();
            RoomLiveOperationResultV1 result = authority.Traverse(
                operationStableId,
                exitStableId);
            if (result.Status == RoomLiveOperationStatusV1.Applied)
            {
                RebuildCurrentRoomPresentation();
            }
            else if (result.Status == RoomLiveOperationStatusV1.FinalExitReached)
            {
                Action handler = FinalExitReached;
                if (handler != null) handler();
            }

            return result;
        }

        public RoomLiveOperationResultV1 Restart(StableId operationStableId)
        {
            RequireBuilt();
            RoomLiveOperationResultV1 result = authority.Restart(operationStableId);
            if (result.Changed)
            {
                RebuildCurrentRoomPresentation();
            }

            return result;
        }

        private void Awake()
        {
            if (buildOnAwake && definitionAsset != null && presentationCatalog != null)
            {
                BuildSession();
            }
        }

        private void OnDestroy()
        {
            presentation.Clear();
            authority = null;
        }

        private void RebuildCurrentRoomPresentation()
        {
            presentation.BuildCurrentRoom(
                this,
                configuredDefinition,
                presentationCatalog,
                roomPresentationRoot,
                authority);
        }

        private void EnsurePresentationRoot()
        {
            if (roomPresentationRoot != null) return;
            var root = new GameObject("RoomRuntimePresentationRoot");
            root.transform.SetParent(transform, false);
            roomPresentationRoot = root.transform;
        }

        private void RequireBuilt()
        {
            if (!IsBuilt)
            {
                throw new InvalidOperationException(
                    "Room runtime composition has not been built.");
            }
        }
    }
}
