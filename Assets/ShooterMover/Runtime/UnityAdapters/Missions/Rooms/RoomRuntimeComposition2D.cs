using System;
using System.Collections.Generic;
using ShooterMover.Application.Missions.Rooms;
using ShooterMover.Contracts.Missions.Rooms;
using ShooterMover.Domain.Common;
using ShooterMover.UnityAdapters.Authoring.LevelDesign;
using UnityEngine;

namespace ShooterMover.UnityAdapters.Missions.Rooms
{
    [DisallowMultipleComponent]
    public sealed class RoomPlacedInstance2D : MonoBehaviour
    {
        private RoomRuntimeComposition2D owner;

        public StableId RoomStableId { get; private set; }

        public StableId InstanceStableId { get; private set; }

        public StableId DefinitionStableId { get; private set; }

        public RoomLivePlacementKindV1 PlacementKind { get; private set; }

        public bool IsConfigured { get; private set; }

        public void Configure(
            RoomRuntimeComposition2D configuredOwner,
            StableId roomStableId,
            RoomPlacedEntityDefinitionV1 definition)
        {
            if (IsConfigured)
            {
                throw new InvalidOperationException(
                    "Room placed instance may only be configured once.");
            }

            owner = configuredOwner
                ?? throw new ArgumentNullException(nameof(configuredOwner));
            RoomStableId = roomStableId
                ?? throw new ArgumentNullException(nameof(roomStableId));
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            InstanceStableId = definition.InstanceStableId;
            DefinitionStableId = definition.DefinitionStableId;
            PlacementKind = definition.PlacementKind;
            IsConfigured = true;
        }

        public RoomLiveOperationResultV1 ReportTerminal(StableId operationStableId)
        {
            if (!IsConfigured || owner == null)
            {
                throw new InvalidOperationException(
                    "Room placed instance is not configured.");
            }

            return owner.ReportOccupantTerminal(
                operationStableId,
                RoomStableId,
                InstanceStableId);
        }
    }

    [DisallowMultipleComponent]
    public sealed class RoomDoorInstance2D : MonoBehaviour
    {
        private RoomRuntimeComposition2D owner;
        private Collider2D[] colliders = Array.Empty<Collider2D>();
        private bool[] authoredColliderEnabled = Array.Empty<bool>();

        public StableId RoomStableId { get; private set; }

        public StableId DoorInstanceStableId { get; private set; }

        public StableId ExitStableId { get; private set; }

        public bool IsOpen { get; private set; }

        public bool IsConfigured { get; private set; }

        public void Configure(
            RoomRuntimeComposition2D configuredOwner,
            StableId roomStableId,
            RoomDoorDefinitionV1 definition)
        {
            if (IsConfigured)
            {
                throw new InvalidOperationException(
                    "Room door instance may only be configured once.");
            }

            owner = configuredOwner
                ?? throw new ArgumentNullException(nameof(configuredOwner));
            RoomStableId = roomStableId
                ?? throw new ArgumentNullException(nameof(roomStableId));
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            DoorInstanceStableId = definition.DoorInstanceStableId;
            ExitStableId = definition.ExitStableId;
            colliders = GetComponentsInChildren<Collider2D>(true);
            authoredColliderEnabled = new bool[colliders.Length];
            for (int index = 0; index < colliders.Length; index++)
            {
                authoredColliderEnabled[index] = colliders[index] != null
                    && colliders[index].enabled;
            }

            IsConfigured = true;
        }

        public void SetOpen(bool open)
        {
            if (!IsConfigured)
            {
                throw new InvalidOperationException("Room door is not configured.");
            }

            IsOpen = open;
            for (int index = 0; index < colliders.Length; index++)
            {
                if (colliders[index] != null)
                {
                    colliders[index].enabled = !open && authoredColliderEnabled[index];
                }
            }
        }

        public RoomLiveOperationResultV1 TryTraverse(StableId operationStableId)
        {
            if (!IsConfigured || owner == null)
            {
                throw new InvalidOperationException("Room door is not configured.");
            }

            return owner.Traverse(operationStableId, ExitStableId);
        }
    }

    [DisallowMultipleComponent]
    public sealed class RoomDropInstance2D : MonoBehaviour
    {
        private RoomRuntimeComposition2D owner;

        public StableId RoomStableId { get; private set; }

        public StableId DropInstanceStableId { get; private set; }

        public bool IsConfigured { get; private set; }

        public void Configure(
            RoomRuntimeComposition2D configuredOwner,
            StableId roomStableId,
            StableId dropInstanceStableId)
        {
            if (IsConfigured)
            {
                throw new InvalidOperationException(
                    "Room drop instance may only be configured once.");
            }

            owner = configuredOwner
                ?? throw new ArgumentNullException(nameof(configuredOwner));
            RoomStableId = roomStableId
                ?? throw new ArgumentNullException(nameof(roomStableId));
            DropInstanceStableId = dropInstanceStableId
                ?? throw new ArgumentNullException(nameof(dropInstanceStableId));
            IsConfigured = true;
        }

        public RoomLiveOperationResultV1 ReportCollected(StableId operationStableId)
        {
            if (!IsConfigured || owner == null)
            {
                throw new InvalidOperationException(
                    "Room drop instance is not configured.");
            }

            RoomLiveOperationResultV1 result = owner.ReportDropCollected(
                operationStableId,
                RoomStableId,
                DropInstanceStableId);
            if (result.Status != RoomLiveOperationStatusV1.Rejected)
            {
                gameObject.SetActive(false);
            }

            return result;
        }
    }

    /// <summary>
    /// Unity composition and presentation adapter for authorable rooms. It creates room
    /// objects from stable presentation references, delegates all occupancy state to
    /// ROOM-RUNTIME-001 through RoomLiveRuntimeAuthorityV1, and restores retained state
    /// whenever a room is re-entered.
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
            "room-runtime-instance.level1";

        private readonly Dictionary<StableId, RoomPlacedInstance2D> spawnedPlacements =
            new Dictionary<StableId, RoomPlacedInstance2D>();
        private readonly Dictionary<StableId, RoomDoorInstance2D> spawnedDoors =
            new Dictionary<StableId, RoomDoorInstance2D>();
        private readonly List<GameObject> spawnedObjects = new List<GameObject>();
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

        public RoomLiveRuntimeAuthorityV1 Authority
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
            get { return spawnedPlacements.Count; }
        }

        public int SpawnedDoorCount
        {
            get { return spawnedDoors.Count; }
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
                    "Room runtime composition may only be built once.");
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

            presentationCatalog.ValidateFor(configuredDefinition);
            EnsurePresentationRoot();
            authority = new RoomLiveRuntimeAuthorityV1(
                stableRuntimeInstanceId,
                configuredDefinition);
            RebuildCurrentRoomPresentation();
        }

        public bool TryGetSpawnedPlacement(
            StableId instanceStableId,
            out RoomPlacedInstance2D instance)
        {
            if (instanceStableId == null)
            {
                instance = null;
                return false;
            }

            return spawnedPlacements.TryGetValue(instanceStableId, out instance)
                && instance != null;
        }

        public bool TryGetSpawnedDoor(
            StableId doorInstanceStableId,
            out RoomDoorInstance2D door)
        {
            if (doorInstanceStableId == null)
            {
                door = null;
                return false;
            }

            return spawnedDoors.TryGetValue(doorInstanceStableId, out door)
                && door != null;
        }

        public Vector2 GetCurrentSpawnPosition()
        {
            RequireBuilt();
            AuthorableRoomDefinitionV1 room = configuredDefinition.GetRoom(
                authority.CurrentProjection.CurrentRoomStableId);
            RoomSpawnPointDefinitionV1 spawnPoint;
            if (!room.TryGetSpawnPoint(
                authority.CurrentProjection.CurrentSpawnPointStableId,
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
            if (roomStableId == CurrentRoomStableId)
            {
                RoomPlacedInstance2D spawned;
                RoomLiveRoomProjectionV1 room = authority.GetRoomProjection(roomStableId);
                if (IsDefeated(room, occupantInstanceStableId)
                    && spawnedPlacements.TryGetValue(
                        occupantInstanceStableId,
                        out spawned))
                {
                    spawnedPlacements.Remove(occupantInstanceStableId);
                    RemoveSpawnedObject(spawned == null ? null : spawned.gameObject);
                }

                SynchronizeDoorPresentation();
            }

            return result;
        }

        public RoomLiveOperationResultV1 ReportDropCollected(
            StableId operationStableId,
            StableId roomStableId,
            StableId dropInstanceStableId)
        {
            RequireBuilt();
            return authority.ReportDropCollected(
                operationStableId,
                roomStableId,
                dropInstanceStableId);
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
            ClearPresentation();
            authority = null;
        }

        private void RebuildCurrentRoomPresentation()
        {
            ClearPresentation();
            AuthorableRoomDefinitionV1 room = configuredDefinition.GetRoom(
                authority.CurrentProjection.CurrentRoomStableId);
            RoomLiveRoomProjectionV1 roomProjection = authority.GetRoomProjection(
                room.RoomStableId);

            for (int index = 0; index < room.Placements.Count; index++)
            {
                RoomPlacedEntityDefinitionV1 placement = room.Placements[index];
                if (IsDefeated(roomProjection, placement.InstanceStableId))
                {
                    continue;
                }

                GameObject instance = InstantiatePresentation(
                    placement.PresentationStableId,
                    placement.LocalPosition,
                    placement.LocalRotationDegrees,
                    placement.InstanceStableId.ToString());
                RoomPlacedInstance2D marker =
                    instance.GetComponent<RoomPlacedInstance2D>()
                    ?? instance.AddComponent<RoomPlacedInstance2D>();
                marker.Configure(this, room.RoomStableId, placement);
                spawnedPlacements.Add(placement.InstanceStableId, marker);
            }

            for (int index = 0; index < room.Doors.Count; index++)
            {
                RoomDoorDefinitionV1 doorDefinition = room.Doors[index];
                GameObject instance = InstantiatePresentation(
                    doorDefinition.PresentationStableId,
                    doorDefinition.LocalPosition,
                    doorDefinition.LocalRotationDegrees,
                    doorDefinition.DoorInstanceStableId.ToString());
                RoomDoorInstance2D door = instance.GetComponent<RoomDoorInstance2D>()
                    ?? instance.AddComponent<RoomDoorInstance2D>();
                door.Configure(this, room.RoomStableId, doorDefinition);
                spawnedDoors.Add(doorDefinition.DoorInstanceStableId, door);
            }

            SynchronizeDoorPresentation();
        }

        private GameObject InstantiatePresentation(
            StableId presentationStableId,
            RoomVector2V1 localPosition,
            double localRotationDegrees,
            string instanceName)
        {
            GameObject prefab;
            if (!presentationCatalog.TryResolve(presentationStableId, out prefab))
            {
                throw new InvalidOperationException(
                    "room-live-presentation-missing:" + presentationStableId);
            }

            GameObject instance = Instantiate(prefab, roomPresentationRoot);
            instance.name = instanceName;
            instance.transform.localPosition = new Vector3(
                (float)localPosition.X,
                (float)localPosition.Y,
                0f);
            instance.transform.localRotation = Quaternion.Euler(
                0f,
                0f,
                (float)localRotationDegrees);
            instance.SetActive(true);
            spawnedObjects.Add(instance);
            return instance;
        }

        private void SynchronizeDoorPresentation()
        {
            RoomLiveRoomProjectionV1 room = authority.GetRoomProjection(
                authority.CurrentProjection.CurrentRoomStableId);
            foreach (KeyValuePair<StableId, RoomDoorInstance2D> pair in spawnedDoors)
            {
                if (pair.Value != null)
                {
                    pair.Value.SetOpen(room.IsDoorOpen(pair.Key));
                }
            }
        }

        private void ClearPresentation()
        {
            for (int index = spawnedObjects.Count - 1; index >= 0; index--)
            {
                DestroyObject(spawnedObjects[index]);
            }

            spawnedObjects.Clear();
            spawnedPlacements.Clear();
            spawnedDoors.Clear();
        }

        private void RemoveSpawnedObject(GameObject instance)
        {
            if (instance == null) return;
            spawnedObjects.Remove(instance);
            DestroyObject(instance);
        }

        private static void DestroyObject(GameObject instance)
        {
            if (instance == null) return;
            instance.SetActive(false);
            if (Application.isPlaying)
            {
                Destroy(instance);
            }
            else
            {
                DestroyImmediate(instance);
            }
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

        private static bool IsDefeated(
            RoomLiveRoomProjectionV1 room,
            StableId instanceStableId)
        {
            for (int index = 0; index < room.DefeatedOccupants.Count; index++)
            {
                if (room.DefeatedOccupants[index].EntityStableId == instanceStableId)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
