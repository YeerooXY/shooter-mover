using UnityEngine;

namespace ShooterMover.UnityAdapters.Authoring.LevelDesign
{
    [DisallowMultipleComponent]
    public sealed class LevelDoorLinkAuthoring2D : MonoBehaviour
    {
        [Header("Stable identity")]
        [SerializeField] private string connectionId = "connection.unassigned";

        [Header("Source endpoint")]
        [SerializeField] private LevelRoomAuthoring2D sourceRoom;
        [SerializeField] private LevelDoorEndpointAuthoring2D sourceDoor;

        [Header("Destination endpoint")]
        [SerializeField] private LevelRoomAuthoring2D destinationRoom;
        [SerializeField] private LevelDoorEndpointAuthoring2D destinationDoor;

        [Header("Traversal")]
        [SerializeField] private LevelDoorTravelPolicy travelPolicy =
            LevelDoorTravelPolicy.Bidirectional;

        public string ConnectionIdText
        {
            get { return connectionId; }
        }

        public LevelRoomAuthoring2D SourceRoom
        {
            get { return sourceRoom; }
        }

        public LevelDoorEndpointAuthoring2D SourceDoor
        {
            get { return sourceDoor; }
        }

        public LevelRoomAuthoring2D DestinationRoom
        {
            get { return destinationRoom; }
        }

        public LevelDoorEndpointAuthoring2D DestinationDoor
        {
            get { return destinationDoor; }
        }

        public LevelDoorTravelPolicy TravelPolicy
        {
            get { return travelPolicy; }
        }

        public LevelGridConnectionRecordV2 BuildRecord()
        {
            return new LevelGridConnectionRecordV2(
                connectionId,
                sourceRoom == null ? null : sourceRoom.RoomIdText,
                sourceDoor == null ? null : sourceDoor.DoorIdText,
                destinationRoom == null ? null : destinationRoom.RoomIdText,
                destinationDoor == null ? null : destinationDoor.DoorIdText,
                travelPolicy,
                BuildDiagnosticLocation());
        }

        [ContextMenu("Assign New Stable ID")]
        public void AssignNewStableId()
        {
            connectionId = LevelDesignAuthoringId.New("connection");
        }

        public void ConfigureConnection(
            string configuredConnectionId,
            LevelRoomAuthoring2D configuredSourceRoom,
            LevelDoorEndpointAuthoring2D configuredSourceDoor,
            LevelRoomAuthoring2D configuredDestinationRoom,
            LevelDoorEndpointAuthoring2D configuredDestinationDoor,
            LevelDoorTravelPolicy configuredTravelPolicy =
                LevelDoorTravelPolicy.Bidirectional)
        {
            connectionId = configuredConnectionId;
            sourceRoom = configuredSourceRoom;
            sourceDoor = configuredSourceDoor;
            destinationRoom = configuredDestinationRoom;
            destinationDoor = configuredDestinationDoor;
            travelPolicy = configuredTravelPolicy;
        }

        public void ConfigureForTests(
            string configuredConnectionId,
            LevelRoomAuthoring2D configuredSourceRoom,
            LevelDoorEndpointAuthoring2D configuredSourceDoor,
            LevelRoomAuthoring2D configuredDestinationRoom,
            LevelDoorEndpointAuthoring2D configuredDestinationDoor,
            LevelDoorTravelPolicy configuredTravelPolicy =
                LevelDoorTravelPolicy.Bidirectional)
        {
            ConfigureConnection(
                configuredConnectionId,
                configuredSourceRoom,
                configuredSourceDoor,
                configuredDestinationRoom,
                configuredDestinationDoor,
                configuredTravelPolicy);
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
