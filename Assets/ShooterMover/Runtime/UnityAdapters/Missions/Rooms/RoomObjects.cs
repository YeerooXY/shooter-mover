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
    /// Optional presentation-only handoff used after authoritative room defeat has committed.
    /// Implementations may keep the already-terminal object visible briefly, then invoke release.
    /// They never delay room completion, door synchronization, collision shutdown, or rewards.
    /// </summary>
    public interface IEnemyDeathView
    {
        bool TryBeginRetirement(Action release);
    }

    /// <summary>
    /// Unity-only renderer for one active authored room. It owns instantiated presentation
    /// objects and stable-id lookup, but it never owns room state or completion decisions.
    /// </summary>
    internal sealed class RoomObjects
    {
        private readonly Dictionary<StableId, RoomObjectInstance> spawnedPlacements =
            new Dictionary<StableId, RoomObjectInstance>();
        private readonly Dictionary<StableId, RoomDoor> spawnedDoors =
            new Dictionary<StableId, RoomDoor>();
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
            out RoomObjectInstance instance)
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
            out RoomDoor door)
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
            LevelRooms owner,
            AuthorableRoomGraphDefinition definition,
            RoomArt catalog,
            Transform root,
            IRoomLiveQuery query)
        {
            if (owner == null) throw new ArgumentNullException(nameof(owner));
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            if (root == null) throw new ArgumentNullException(nameof(root));
            if (query == null) throw new ArgumentNullException(nameof(query));

            Clear();
            AuthorableRoomDefinition room = definition.GetRoom(
                query.CurrentProjection.CurrentRoomStableId);
            RoomLiveRoomView projection = query.GetRoomProjection(
                room.RoomStableId);

            for (int index = 0; index < room.Placements.Count; index++)
            {
                RoomPlacedEntityDefinition placement = room.Placements[index];
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
                RoomObjectInstance marker =
                    instance.GetComponent<RoomObjectInstance>()
                    ?? instance.AddComponent<RoomObjectInstance>();
                marker.Configure(owner, room.RoomStableId, placement);
                spawnedPlacements.Add(placement.InstanceStableId, marker);

                if (placement.PlacementKind == RoomLivePlacementKind.Enemy)
                {
                    EnemyDeathSource terminalSource =
                        instance.GetComponent<EnemyDeathSource>()
                        ?? instance.AddComponent<EnemyDeathSource>();
                    RoomEnemyDeathRelay relay =
                        instance.GetComponent<RoomEnemyDeathRelay>()
                        ?? instance.AddComponent<RoomEnemyDeathRelay>();
                    relay.Configure(marker, terminalSource);
                }
            }

            for (int index = 0; index < room.Doors.Count; index++)
            {
                RoomDoorDefinition doorDefinition = room.Doors[index];
                GameObject instance = InstantiatePresentation(
                    catalog,
                    root,
                    doorDefinition.PresentationStableId,
                    doorDefinition.LocalPosition,
                    doorDefinition.LocalRotationDegrees,
                    doorDefinition.DoorInstanceStableId.ToString());
                RoomDoor door = instance.GetComponent<RoomDoor>()
                    ?? instance.AddComponent<RoomDoor>();
                door.Configure(owner, room.RoomStableId, doorDefinition);
                spawnedDoors.Add(doorDefinition.DoorInstanceStableId, door);
            }

            SynchronizeDoors(query.GetRoomProjection(room.RoomStableId));
        }

        public void SynchronizeDoors(RoomLiveRoomView room)
        {
            if (room == null) throw new ArgumentNullException(nameof(room));
            foreach (KeyValuePair<StableId, RoomDoor> pair in spawnedDoors)
            {
                if (pair.Value != null)
                {
                    pair.Value.SetOpen(room.IsDoorOpen(pair.Key));
                }
            }
        }

        public void RemoveDefeated(RoomLiveRoomView room)
        {
            if (room == null) throw new ArgumentNullException(nameof(room));
            var remove = new List<StableId>();
            foreach (KeyValuePair<StableId, RoomObjectInstance> pair
                in spawnedPlacements)
            {
                if (IsDefeated(room, pair.Key)) remove.Add(pair.Key);
            }

            for (int index = 0; index < remove.Count; index++)
            {
                StableId id = remove[index];
                RoomObjectInstance instance;
                if (!spawnedPlacements.TryGetValue(id, out instance)) continue;

                // Remove lookup ownership immediately so the defeated placement cannot be rebound,
                // targeted, or counted as live. Only visual destruction may be deferred.
                spawnedPlacements.Remove(id);
                RetireSpawnedObject(instance == null ? null : instance.gameObject);
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
            RoomArt catalog,
            Transform root,
            StableId presentationStableId,
            RoomVector2 localPosition,
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

        private void RetireSpawnedObject(GameObject instance)
        {
            if (instance == null) return;

            IEnemyDeathView retirement = null;
            int providerCount = 0;
            MonoBehaviour[] behaviours = instance.GetComponentsInChildren<MonoBehaviour>(true);
            for (int index = 0; index < behaviours.Length; index++)
            {
                IEnemyDeathView candidate =
                    behaviours[index] as IEnemyDeathView;
                if (candidate == null) continue;
                retirement = candidate;
                providerCount++;
            }

            if (providerCount > 1)
            {
                Debug.LogError(
                    "room-defeated-presentation-retirement-ambiguous",
                    instance);
                RemoveSpawnedObject(instance);
                return;
            }
            if (retirement == null)
            {
                RemoveSpawnedObject(instance);
                return;
            }

            try
            {
                if (retirement.TryBeginRetirement(
                    () => RemoveSpawnedObject(instance)))
                {
                    return;
                }
            }
            catch (Exception exception)
            {
                if (IsFatalException(exception)) throw;
                Debug.LogException(exception, instance);
            }

            // The authoritative defeat is already committed. A failed optional visual handoff
            // must clean up immediately rather than leave a stale room-owned object behind.
            RemoveSpawnedObject(instance);
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
            RoomLiveRoomView room,
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

        private static bool IsFatalException(Exception exception)
        {
            return exception is OutOfMemoryException
                || exception is StackOverflowException
                || exception is AccessViolationException;
        }
    }
}
