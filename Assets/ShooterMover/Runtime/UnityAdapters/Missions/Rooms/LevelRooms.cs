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
    public sealed class LevelRooms : MonoBehaviour
    {
        [SerializeField] private RoomGraphDraft definitionAsset;
        [SerializeField] private RoomArt presentationCatalog;
        [SerializeField] private Transform roomPresentationRoot;
        [SerializeField] private bool buildOnAwake = true;
        [SerializeField] private string runtimeInstanceStableId =
            "room-runtime-instance.unassigned";

        private readonly RoomObjects presentation =
            new RoomObjects();
        private AuthorableRoomGraphDefinition configuredDefinition;
        private RoomFlowState authority;
        private long presentationRevision;

        public event Action CurrentRoomPresentationRebuilt;
        public event Action FinalExitReached;

        public bool IsBuilt
        {
            get { return authority != null; }
        }

        public long PresentationRevision
        {
            get { return presentationRevision; }
        }

        public AuthorableRoomGraphDefinition Definition
        {
            get { return configuredDefinition; }
        }

        public IRoomLiveQuery Query
        {
            get { return authority; }
        }

        public RoomLiveView CurrentProjection
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

        public void ConfigureDefinition(
            AuthorableRoomGraphDefinition definition,
            RoomArt catalog,
            Transform presentationRoot = null)
        {
            if (IsBuilt)
            {
                throw new InvalidOperationException(
                    "Room runtime composition is already built.");
            }
            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }
            if (catalog == null)
            {
                throw new ArgumentNullException(nameof(catalog));
            }

            configuredDefinition = definition;
            presentationCatalog = catalog;
            roomPresentationRoot = presentationRoot;
            buildOnAwake = false;
        }

        public void ConfigureForTests(
            AuthorableRoomGraphDefinition definition,
            RoomArt catalog,
            Transform presentationRoot = null)
        {
            ConfigureDefinition(definition, catalog, presentationRoot);
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
            var candidateAuthority = new RoomFlowState(
                stableRuntimeInstanceId,
                configuredDefinition);
            try
            {
                BuildCurrentRoomPresentation(candidateAuthority);
            }
            catch (Exception)
            {
                ClearFailedInitialPresentation();
                throw;
            }

            authority = candidateAuthority;
            CommitCurrentRoomPresentationRebuild();
        }

        public bool TryGetSpawnedPlacement(
            StableId instanceStableId,
            out RoomObjectInstance instance)
        {
            return presentation.TryGetPlacement(instanceStableId, out instance);
        }

        public bool TryGetSpawnedDoor(
            StableId doorInstanceStableId,
            out RoomDoor door)
        {
            return presentation.TryGetDoor(doorInstanceStableId, out door);
        }

        public Vector2 GetCurrentSpawnPosition()
        {
            RequireBuilt();
            AuthorableRoomDefinition room = configuredDefinition.GetRoom(
                CurrentProjection.CurrentRoomStableId);
            RoomSpawnPointDefinition spawnPoint;
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

        public RoomLiveOperationResult ReportOccupantTerminal(
            StableId operationStableId,
            StableId roomStableId,
            StableId occupantInstanceStableId)
        {
            RequireBuilt();
            RoomLiveOperationResult result = authority.ReportOccupantTerminal(
                operationStableId,
                roomStableId,
                occupantInstanceStableId);
            if (roomStableId == CurrentRoomStableId
                && result.Status != RoomLiveOperationStatus.Rejected)
            {
                RoomLiveRoomView room = authority.GetRoomProjection(roomStableId);
                presentation.RemoveDefeated(room);
                presentation.SynchronizeDoors(room);
            }

            return result;
        }

        public RoomLiveOperationResult ReportDropCollected(
            StableId operationStableId,
            StableId roomStableId,
            StableId dropInstanceStableId)
        {
            RequireBuilt();
            RoomLiveOperationResult result = authority.ReportDropCollected(
                operationStableId,
                roomStableId,
                dropInstanceStableId);
            if (roomStableId == CurrentRoomStableId
                && result.Status != RoomLiveOperationStatus.Rejected)
            {
                presentation.SynchronizeDoors(authority.GetRoomProjection(roomStableId));
            }

            return result;
        }

        public RoomLiveOperationResult Traverse(
            StableId operationStableId,
            StableId exitStableId)
        {
            RequireBuilt();
            RoomLiveOperationResult result = authority.Traverse(
                operationStableId,
                exitStableId);
            if (result.Status == RoomLiveOperationStatus.Applied)
            {
                RebuildCurrentRoomPresentation();
            }
            else if (result.Status == RoomLiveOperationStatus.FinalExitReached)
            {
                Action handler = FinalExitReached;
                if (handler != null) handler();
            }

            return result;
        }

        public RoomLiveOperationResult Restart(StableId operationStableId)
        {
            RequireBuilt();
            RoomLiveOperationResult result = authority.Restart(operationStableId);
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
            BuildCurrentRoomPresentation(authority);
            CommitCurrentRoomPresentationRebuild();
        }

        private void BuildCurrentRoomPresentation(IRoomLiveQuery query)
        {
            presentation.BuildCurrentRoom(
                this,
                configuredDefinition,
                presentationCatalog,
                roomPresentationRoot,
                query);
        }

        private void CommitCurrentRoomPresentationRebuild()
        {
            presentationRevision++;
            PublishCurrentRoomPresentationRebuilt();
        }

        private void PublishCurrentRoomPresentationRebuilt()
        {
            Action handlers = CurrentRoomPresentationRebuilt;
            if (handlers == null) return;

            Delegate[] subscribers = handlers.GetInvocationList();
            for (int index = 0; index < subscribers.Length; index++)
            {
                try
                {
                    ((Action)subscribers[index])();
                }
                catch (Exception exception)
                {
                    if (IsFatalException(exception)) throw;
                    Debug.LogException(exception, this);
                }
            }
        }

        private void ClearFailedInitialPresentation()
        {
            try
            {
                presentation.Clear();
            }
            catch (Exception exception)
            {
                if (IsFatalException(exception)) throw;
                Debug.LogException(exception, this);
            }
        }

        private static bool IsFatalException(Exception exception)
        {
            return exception is OutOfMemoryException
                || exception is StackOverflowException
                || exception is AccessViolationException;
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
