using System;
using System.Collections.Generic;
using ShooterMover.Application.Missions.Rooms;
using ShooterMover.Contracts.Missions.Rooms;
using ShooterMover.Domain.Common;
using ShooterMover.UnityAdapters.Authoring.LevelDesign;
using UnityEngine;

namespace ShooterMover.UnityAdapters.Missions.Rooms
{
    /// <summary>
    /// Unity-only renderer for one active authored room. It owns instantiated presentation
    /// objects and stable-id lookup, but it never owns room state or completion decisions.
    /// </summary>
    internal sealed class RoomPresentationScene2D
    {
        private readonly Dictionary<StableId, RoomPlacedInstance2D> spawnedPlacements =
            new Dictionary<StableId, RoomPlacedInstance2D>();
        private readonly Dictionary<StableId, RoomDoorInstance2D> spawnedDoors =
            new Dictionary<StableId, RoomDoorInstance2D>();
        private readonly List<GameObject> spawnedObjects = new List<GameObject>();

        public int SpawnedPlacementCount
        {
            get { return spawnedPlacements.Count; }
        }

        public int SpawnedDoorCount
        {
            get { return spawnedDoors.Count; }
        }

        public bool TryGetPlacement(
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

        public bool TryGetDoor(
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

        public void BuildCurrentRoom(
            RoomRuntimeComposition2D owner,
            AuthorableRoomGraphDefinitionV1 definition,
            RoomPresentationCatalog2D catalog,
            Transform root,
            IRoomLiveRuntimeQueryV1 query)
        {
            if (owner == null) throw new ArgumentNullException(nameof(owner));
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            if (root == null) throw new ArgumentNullException(nameof(root));
            if (query == null) throw new ArgumentNullException(nameof(query));

            Clear();
            AuthorableRoomDefinitionV1 room = definition.GetRoom(
                query.CurrentProjection.CurrentRoomStableId);
            RoomLiveRoomProjectionV1 projection = query.GetRoomProjection(
                room.RoomStableId);

            for (int index = 0; index < room.Placements.Count; index++)
            {
                RoomPlacedEntityDefinitionV1 placement = room.Placements[index];
                if (IsDefeated(projection, placement.InstanceStableId))
                {
                    continue;
                }

                GameObject instance = InstantiatePresentation(
                    catalog,
                    root,
                    placement.PresentationStableId,
                    placement.LocalPosition,
                    placement.LocalRotationDegrees,
                    placement.InstanceStableId.ToString());
                RoomPlacedInstance2D marker =
                    instance.GetComponent<RoomPlacedInstance2D>()
                    ?? instance.AddComponent<RoomPlacedInstance2D>();
                marker.Configure(owner, room.RoomStableId, placement);
                spawnedPlacements.Add(placement.InstanceStableId, marker);

                if (placement.PlacementKind == RoomLivePlacementKindV1.Enemy)
                {
                    EnemyActorTerminalFactSource2D terminalSource =
                        instance.GetComponent<EnemyActorTerminalFactSource2D>()
                        ?? instance.AddComponent<EnemyActorTerminalFactSource2D>();
                    RoomOccupantTerminalRelay2D relay =
                        instance.GetComponent<RoomOccupantTerminalRelay2D>()
                        ?? instance.AddComponent<RoomOccupantTerminalRelay2D>();
                    relay.Configure(marker, terminalSource);
                }
            }

            for (int index = 0; index < room.Doors.Count; index++)
            {
                RoomDoorDefinitionV1 doorDefinition = room.Doors[index];
                GameObject instance = InstantiatePresentation(
                    catalog,
                    root,
                    doorDefinition.PresentationStableId,
                    doorDefinition.LocalPosition,
                    doorDefinition.LocalRotationDegrees,
                    doorDefinition.DoorInstanceStableId.ToString());
                RoomDoorInstance2D door = instance.GetComponent<RoomDoorInstance2D>()
                    ?? instance.AddComponent<RoomDoorInstance2D>();
                door.Configure(owner, room.RoomStableId, doorDefinition);
                spawnedDoors.Add(doorDefinition.DoorInstanceStableId, door);
            }

            SynchronizeDoors(query.GetRoomProjection(room.RoomStableId));
        }

        public void SynchronizeDoors(RoomLiveRoomProjectionV1 room)
        {
            if (room == null) throw new ArgumentNullException(nameof(room));
            foreach (KeyValuePair<StableId, RoomDoorInstance2D> pair in spawnedDoors)
            {
                if (pair.Value != null)
                {
                    pair.Value.SetOpen(room.IsDoorOpen(pair.Key));
                }
            }
        }

        public void RemoveDefeated(RoomLiveRoomProjectionV1 room)
        {
            if (room == null) throw new ArgumentNullException(nameof(room));
            var remove = new List<StableId>();
            foreach (KeyValuePair<StableId, RoomPlacedInstance2D> pair
                in spawnedPlacements)
            {
                if (IsDefeated(room, pair.Key)) remove.Add(pair.Key);
            }

            for (int index = 0; index < remove.Count; index++)
            {
                StableId id = remove[index];
                RoomPlacedInstance2D instance;
                if (!spawnedPlacements.TryGetValue(id, out instance)) continue;
                spawnedPlacements.Remove(id);
                RemoveSpawnedObject(instance == null ? null : instance.gameObject);
            }
        }

        public void Clear()
        {
            for (int index = spawnedObjects.Count - 1; index >= 0; index--)
            {
                DestroyObject(spawnedObjects[index]);
            }

            spawnedObjects.Clear();
            spawnedPlacements.Clear();
            spawnedDoors.Clear();
        }

        private GameObject InstantiatePresentation(
            RoomPresentationCatalog2D catalog,
            Transform root,
            StableId presentationStableId,
            RoomVector2V1 localPosition,
            double localRotationDegrees,
            string instanceName)
        {
            GameObject prefab;
            if (!catalog.TryResolve(presentationStableId, out prefab))
            {
                throw new InvalidOperationException(
                    "room-live-presentation-missing:" + presentationStableId);
            }

            GameObject instance = UnityEngine.Object.Instantiate(prefab, root);
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
            if (UnityEngine.Application.isPlaying)
            {
                UnityEngine.Object.Destroy(instance);
            }
            else
            {
                UnityEngine.Object.DestroyImmediate(instance);
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
