using System;
using System.Collections.Generic;
using ShooterMover.Application.Missions.Rooms;
using ShooterMover.Contracts.Missions.Rooms;
using ShooterMover.Domain.Common;
using ShooterMover.UnityAdapters.Authoring.LevelDesign;
using ShooterMover.UnityAdapters.Enemies;
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
        private const string DebugCoverPresentation =
            "presentation.prop-level1-cover";
        private const string DebugWallOnePresentation =
            "presentation.prop-wall-1x1";
        private const string DebugWallTwoPresentation =
            "presentation.prop-wall-2x2";
        private const string DebugDoorPresentation =
            "presentation.environment-room-door";
        private const string DebugFloorPresentation =
            "presentation.environment-floor-industrial";

        private static readonly Color DebugBoundaryColor =
            new Color(0.5f, 0.82f, 1f, 1f);
        private static readonly Color DebugFloorColor =
            new Color(0.025f, 0.09f, 0.24f, 1f);

        private static Sprite debugPixelSprite;

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

            BuildDebugRoomBoundary(root, room.Bounds);

            for (int index = 0; index < room.Placements.Count; index++)
            {
                RoomPlacedEntityDefinition placement = room.Placements[index];
                if (IsDefeated(projection, placement.InstanceStableId))
                {
                    continue;
                }

                bool compactEnemy =
                    placement.PlacementKind == RoomLivePlacementKind.Enemy
                    && CompactEnemyCatalog.IsCompactPresentation(
                        placement.PresentationStableId);
                if (placement.PlacementKind == RoomLivePlacementKind.Enemy
                    && !compactEnemy)
                {
                    throw new InvalidOperationException(
                        "room-live-enemy-presentation-retired:"
                        + placement.PresentationStableId);
                }

                GameObject instance = compactEnemy
                    ? InstantiateCompactPresentation(
                        root,
                        placement.LocalPosition,
                        placement.LocalRotationDegrees,
                        placement.InstanceStableId.ToString())
                    : InstantiatePresentation(
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

                if (compactEnemy)
                {
                    CompactEnemySceneFactory.Configure(
                        instance,
                        owner,
                        room.RoomStableId,
                        placement,
                        1);
                    instance.SetActive(true);
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

        private GameObject InstantiateCompactPresentation(
            Transform root,
            RoomVector2 localPosition,
            double localRotationDegrees,
            string instanceName)
        {
            GameObject instance = new GameObject(instanceName);
            instance.SetActive(false);
            instance.AddComponent<Rigidbody2D>();
            instance.AddComponent<CircleCollider2D>();
            instance.transform.SetParent(root, false);
            instance.transform.localPosition = new Vector3(
                (float)localPosition.X,
                (float)localPosition.Y,
                0f);
            instance.transform.localRotation = Quaternion.Euler(
                0f,
                0f,
                (float)localRotationDegrees);
            spawnedObjects.Add(instance);
            return instance;
        }

        private GameObject InstantiatePresentation(
            RoomArt catalog,
            Transform root,
            StableId presentationStableId,
            RoomVector2 localPosition,
            double localRotationDegrees,
            string instanceName)
        {
            GameObject debugInstance;
            if (TryInstantiateDebugPresentation(
                root,
                presentationStableId,
                localPosition,
                localRotationDegrees,
                instanceName,
                out debugInstance))
            {
                return debugInstance;
            }

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

        private bool TryInstantiateDebugPresentation(
            Transform root,
            StableId presentationStableId,
            RoomVector2 localPosition,
            double localRotationDegrees,
            string instanceName,
            out GameObject instance)
        {
            Vector2 size;
            Color color;
            bool blocksMovement;
            int sortingOrder;
            string presentation = presentationStableId.ToString();
            switch (presentation)
            {
                case DebugFloorPresentation:
                    size = Vector2.one;
                    color = DebugFloorColor;
                    blocksMovement = false;
                    sortingOrder = -20;
                    break;
                case DebugWallOnePresentation:
                    size = Vector2.one;
                    color = new Color(0.2f, 0.55f, 0.85f, 1f);
                    blocksMovement = true;
                    sortingOrder = 2;
                    break;
                case DebugWallTwoPresentation:
                    size = new Vector2(2f, 2f);
                    color = new Color(0.16f, 0.44f, 0.72f, 1f);
                    blocksMovement = true;
                    sortingOrder = 2;
                    break;
                case DebugCoverPresentation:
                    size = Vector2.one;
                    color = new Color(0.95f, 0.6f, 0.15f, 1f);
                    blocksMovement = true;
                    sortingOrder = 3;
                    break;
                case DebugDoorPresentation:
                    size = new Vector2(0.5f, 2f);
                    color = new Color(0.85f, 0.22f, 0.22f, 1f);
                    blocksMovement = true;
                    sortingOrder = 4;
                    break;
                default:
                    instance = null;
                    return false;
            }

            instance = new GameObject(instanceName);
            instance.SetActive(false);
            instance.transform.SetParent(root, false);
            instance.transform.localPosition = new Vector3(
                (float)localPosition.X,
                (float)localPosition.Y,
                0f);
            instance.transform.localRotation = Quaternion.Euler(
                0f,
                0f,
                (float)localRotationDegrees);

            if (presentation == DebugFloorPresentation)
            {
                AddDebugVisual(
                    instance.transform,
                    "Debug Floor Lattice",
                    Vector2.one,
                    Color.white,
                    sortingOrder - 1);
                AddDebugVisual(
                    instance.transform,
                    "Debug Floor",
                    new Vector2(0.9f, 0.9f),
                    color,
                    sortingOrder);
            }
            else
            {
                AddDebugVisual(
                    instance.transform,
                    "Debug Visual",
                    size,
                    color,
                    sortingOrder);
            }

            if (blocksMovement)
            {
                BoxCollider2D collider = instance.AddComponent<BoxCollider2D>();
                collider.size = size;
            }

            instance.SetActive(true);
            spawnedObjects.Add(instance);
            return true;
        }

        private void BuildDebugRoomBoundary(
            Transform root,
            RoomBounds bounds)
        {
            float centerX = (float)bounds.Center.X;
            float centerY = (float)bounds.Center.Y;
            float width = (float)bounds.Size.X;
            float height = (float)bounds.Size.Y;
            const float thickness = 0.5f;

            AddDebugBoundary(
                root,
                "North",
                new Vector2(centerX, centerY + height * 0.5f),
                new Vector2(width + thickness, thickness));
            AddDebugBoundary(
                root,
                "South",
                new Vector2(centerX, centerY - height * 0.5f),
                new Vector2(width + thickness, thickness));
            AddDebugBoundary(
                root,
                "East",
                new Vector2(centerX + width * 0.5f, centerY),
                new Vector2(thickness, height + thickness));
            AddDebugBoundary(
                root,
                "West",
                new Vector2(centerX - width * 0.5f, centerY),
                new Vector2(thickness, height + thickness));
        }

        private void AddDebugBoundary(
            Transform root,
            string name,
            Vector2 position,
            Vector2 size)
        {
            GameObject boundary = new GameObject("Debug Room Boundary " + name);
            boundary.transform.SetParent(root, false);
            boundary.transform.localPosition = new Vector3(
                position.x,
                position.y,
                0f);
            AddDebugVisual(
                boundary.transform,
                "Debug Boundary Visual",
                size,
                DebugBoundaryColor,
                1);
            spawnedObjects.Add(boundary);
        }

        private static SpriteRenderer AddDebugVisual(
            Transform parent,
            string name,
            Vector2 size,
            Color color,
            int sortingOrder)
        {
            GameObject visual = new GameObject(name);
            visual.transform.SetParent(parent, false);
            visual.transform.localScale = new Vector3(size.x, size.y, 1f);
            SpriteRenderer renderer = visual.AddComponent<SpriteRenderer>();
            renderer.sprite = GetDebugPixelSprite();
            renderer.color = color;
            renderer.sortingOrder = sortingOrder;
            return renderer;
        }

        private static Sprite GetDebugPixelSprite()
        {
            if (debugPixelSprite != null)
            {
                return debugPixelSprite;
            }

            Texture2D texture = Texture2D.whiteTexture;
            debugPixelSprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, texture.width, texture.height),
                new Vector2(0.5f, 0.5f),
                texture.width);
            debugPixelSprite.name = "Room Debug Pixel";
            return debugPixelSprite;
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
