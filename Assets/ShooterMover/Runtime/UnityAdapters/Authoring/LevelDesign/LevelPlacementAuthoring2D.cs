using UnityEngine;

namespace ShooterMover.UnityAdapters.Authoring.LevelDesign
{
    [DisallowMultipleComponent]
    public sealed class LevelPlacementAuthoring2D : MonoBehaviour
    {
        [Header("Stable identity")]
        [SerializeField] private string authoredId = "spawn.unassigned";
        [SerializeField] private string socketId = "socket.unassigned";
        [SerializeField] private LevelPlacementKind placementKind =
            LevelPlacementKind.EnemySpawn;

        [Header("Room and grid")]
        [SerializeField] private LevelRoomAuthoring2D room;
        [SerializeField] private Vector2Int localGridCoordinate = Vector2Int.zero;
        [SerializeField] private bool snapToRoomGrid = true;

        [Header("Existing package references")]
        [Tooltip("Assign the existing enemy, prop, reward, pickup, or spawn definition/profile. This component never duplicates package behavior.")]
        [SerializeField] private Object definitionReference;
        [SerializeField] private GameObject prefabReference;
        [SerializeField] private Transform presentationRoot;

        [Header("Collision and sorting")]
        [SerializeField] private LevelCollisionPolicy collisionPolicy =
            LevelCollisionPolicy.TriggerOnly;
        [SerializeField] private Collider2D authoredCollider;
        [SerializeField] private int sortingOrder;

        [Header("Reward and lifecycle")]
        [SerializeField] private string rewardOverrideId = string.Empty;
        [SerializeField] private LevelRestartPolicy restartPolicy =
            LevelRestartPolicy.ResetProjection;
        [SerializeField] private bool visibleOnMap;

        public string AuthoredIdText
        {
            get { return authoredId; }
        }

        public string SocketIdText
        {
            get { return socketId; }
        }

        public LevelPlacementKind PlacementKind
        {
            get { return placementKind; }
        }

        public LevelRoomAuthoring2D Room
        {
            get { return room; }
        }

        public Object DefinitionReference
        {
            get { return definitionReference; }
        }

        public GameObject PrefabReference
        {
            get { return prefabReference; }
        }

        public LevelCollisionPolicy CollisionPolicy
        {
            get { return collisionPolicy; }
        }

        public LevelRestartPolicy RestartPolicy
        {
            get { return restartPolicy; }
        }

        public Vector3 GridAlignedWorldPosition
        {
            get
            {
                if (room == null)
                {
                    return transform.position;
                }

                Vector3 roomPosition = room.transform.position;
                Vector2 cell = room.CellSize;
                return new Vector3(
                    roomPosition.x + localGridCoordinate.x * cell.x,
                    roomPosition.y + localGridCoordinate.y * cell.y,
                    transform.position.z);
            }
        }

        public LevelPlacementRecord BuildRecord()
        {
            Rect bounds = authoredCollider == null
                ? new Rect((Vector2)transform.position, Vector2.zero)
                : RectFromBounds(authoredCollider.bounds);
            return new LevelPlacementRecord(
                authoredId,
                socketId,
                placementKind,
                room == null ? null : room.RoomIdText,
                localGridCoordinate,
                bounds,
                definitionReference != null,
                prefabReference != null,
                presentationRoot != null,
                authoredCollider != null,
                collisionPolicy,
                restartPolicy,
                rewardOverrideId,
                visibleOnMap,
                sortingOrder,
                BuildDiagnosticLocation());
        }

        [ContextMenu("Assign New Stable ID")]
        public void AssignNewStableId()
        {
            authoredId = LevelDesignAuthoringId.New(GetIdentityNamespace());
        }

        [ContextMenu("Assign New Socket ID")]
        public void AssignNewSocketId()
        {
            socketId = LevelDesignAuthoringId.New("socket");
        }

        [ContextMenu("Snap Placement To Room Grid")]
        public void SnapToGrid()
        {
            if (room != null)
            {
                transform.position = GridAlignedWorldPosition;
            }
        }

        public void ConfigureForTests(
            string configuredAuthoredId,
            string configuredSocketId,
            LevelPlacementKind configuredKind,
            LevelRoomAuthoring2D configuredRoom,
            Object configuredDefinition,
            GameObject configuredPrefab,
            Transform configuredPresentation,
            Collider2D configuredCollider,
            LevelCollisionPolicy configuredCollisionPolicy,
            string configuredRewardOverrideId)
        {
            authoredId = configuredAuthoredId;
            socketId = configuredSocketId;
            placementKind = configuredKind;
            room = configuredRoom;
            definitionReference = configuredDefinition;
            prefabReference = configuredPrefab;
            presentationRoot = configuredPresentation;
            authoredCollider = configuredCollider;
            collisionPolicy = configuredCollisionPolicy;
            rewardOverrideId = configuredRewardOverrideId;
        }

        private void Reset()
        {
            room = GetComponentInParent<LevelRoomAuthoring2D>();
            authoredCollider = GetComponent<Collider2D>();
            presentationRoot = transform;
        }

        private void OnValidate()
        {
            if (snapToRoomGrid && room != null)
            {
                transform.position = GridAlignedWorldPosition;
            }
        }

        private string GetIdentityNamespace()
        {
            switch (placementKind)
            {
                case LevelPlacementKind.PlayerSpawn:
                case LevelPlacementKind.EnemySpawn:
                    return "spawn";
                case LevelPlacementKind.PropPlacement:
                    return "prop";
                case LevelPlacementKind.PickupSpawn:
                case LevelPlacementKind.RewardSocket:
                    return "reward";
                case LevelPlacementKind.Entry:
                case LevelPlacementKind.Exit:
                    return "transition";
                default:
                    return "placement";
            }
        }

        private string BuildDiagnosticLocation()
        {
            return gameObject.scene.name + ":" + GetHierarchyPath(transform);
        }

        private static string GetHierarchyPath(Transform current)
        {
            string path = current.name;
            while (current.parent != null)
            {
                current = current.parent;
                path = current.name + "/" + path;
            }

            return path;
        }

        private static Rect RectFromBounds(Bounds bounds)
        {
            return new Rect(
                bounds.min.x,
                bounds.min.y,
                bounds.size.x,
                bounds.size.y);
        }
    }
}
