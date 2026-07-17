using UnityEngine;

namespace ShooterMover.UnityAdapters.Authoring.LevelDesign
{
    [DisallowMultipleComponent]
    public sealed class LevelDoorConnectionAuthoring2D : MonoBehaviour
    {
        [Header("Stable identity")]
        [SerializeField] private string doorId = "door.unassigned";

        [Header("Room connection")]
        [SerializeField] private LevelRoomAuthoring2D sourceRoom;
        [SerializeField] private LevelRoomAuthoring2D destinationRoom;
        [SerializeField] private string sourceSocketId = "socket.unassigned";
        [SerializeField] private string destinationSocketId = "socket.unassigned";
        [SerializeField] private Vector2Int sourceGridEdge = Vector2Int.zero;
        [SerializeField] private Vector2Int destinationGridEdge = Vector2Int.right;
        [SerializeField] private bool requireAdjacentRooms = true;
        [SerializeField] private LevelDoorTravelPolicy travelPolicy =
            LevelDoorTravelPolicy.Bidirectional;

        [Header("Existing DOOR-001 package")]
        [Tooltip("Assign a component implementing ILevelDoorPackageAdapter. The foundation validates the existing door package but does not replace it.")]
        [SerializeField] private MonoBehaviour packageAdapter;

        [Header("Map metadata")]
        [SerializeField] private bool visibleOnMap = true;
        [SerializeField] private Vector2Int mapCoordinate = Vector2Int.zero;

        public string DoorIdText
        {
            get { return doorId; }
        }

        public LevelRoomAuthoring2D SourceRoom
        {
            get { return sourceRoom; }
        }

        public LevelRoomAuthoring2D DestinationRoom
        {
            get { return destinationRoom; }
        }

        public string SourceSocketIdText
        {
            get { return sourceSocketId; }
        }

        public string DestinationSocketIdText
        {
            get { return destinationSocketId; }
        }

        public MonoBehaviour PackageAdapter
        {
            get { return packageAdapter; }
        }

        public LevelDoorRecord BuildRecord()
        {
            ILevelDoorPackageAdapter adapter =
                packageAdapter as ILevelDoorPackageAdapter;
            return new LevelDoorRecord(
                doorId,
                sourceRoom == null ? null : sourceRoom.RoomIdText,
                destinationRoom == null ? null : destinationRoom.RoomIdText,
                sourceSocketId,
                destinationSocketId,
                sourceGridEdge,
                destinationGridEdge,
                requireAdjacentRooms,
                travelPolicy,
                adapter != null,
                adapter != null && adapter.HasDoorController,
                adapter != null && adapter.HasClosedPresentation,
                adapter != null && adapter.HasOpenPresentation,
                adapter != null && adapter.HasClosedCollider,
                BuildDiagnosticLocation());
        }

        [ContextMenu("Assign New Stable ID")]
        public void AssignNewStableId()
        {
            doorId = LevelDesignAuthoringId.New("door");
        }

        [ContextMenu("Assign New Socket IDs")]
        public void AssignNewSocketIds()
        {
            sourceSocketId = LevelDesignAuthoringId.New("socket");
            destinationSocketId = LevelDesignAuthoringId.New("socket");
        }

        public void ConfigureForTests(
            string configuredDoorId,
            LevelRoomAuthoring2D configuredSourceRoom,
            LevelRoomAuthoring2D configuredDestinationRoom,
            string configuredSourceSocketId,
            string configuredDestinationSocketId,
            Vector2Int configuredSourceGridEdge,
            Vector2Int configuredDestinationGridEdge,
            bool configuredRequireAdjacentRooms,
            MonoBehaviour configuredPackageAdapter)
        {
            doorId = configuredDoorId;
            sourceRoom = configuredSourceRoom;
            destinationRoom = configuredDestinationRoom;
            sourceSocketId = configuredSourceSocketId;
            destinationSocketId = configuredDestinationSocketId;
            sourceGridEdge = configuredSourceGridEdge;
            destinationGridEdge = configuredDestinationGridEdge;
            requireAdjacentRooms = configuredRequireAdjacentRooms;
            packageAdapter = configuredPackageAdapter;
        }

        private void Reset()
        {
            packageAdapter = FindPackageAdapterOnSameObject();
        }

        private void OnValidate()
        {
            if (packageAdapter == null)
            {
                packageAdapter = FindPackageAdapterOnSameObject();
            }
        }

        private MonoBehaviour FindPackageAdapterOnSameObject()
        {
            MonoBehaviour[] candidates = GetComponents<MonoBehaviour>();
            for (int index = 0; index < candidates.Length; index++)
            {
                if (candidates[index] is ILevelDoorPackageAdapter)
                {
                    return candidates[index];
                }
            }

            return null;
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
    }
}
