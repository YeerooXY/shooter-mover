using UnityEngine;

namespace ShooterMover.UnityAdapters.Authoring.LevelDesign
{
    [DisallowMultipleComponent]
    public sealed class DoorLink : MonoBehaviour
    {
        [Header("Stable identity")]
        [SerializeField] private string connectionId = "connection.unassigned";

        [Header("Source endpoint")]
        [SerializeField] private LevelRoom sourceRoom;
        [SerializeField] private DoorEndpoint sourceDoor;

        [Header("Destination endpoint")]
        [SerializeField] private LevelRoom destinationRoom;
        [SerializeField] private DoorEndpoint destinationDoor;

        [Header("Traversal")]
        [SerializeField] private LevelDoorTravelPolicy travelPolicy =
            LevelDoorTravelPolicy.Bidirectional;

        public string ConnectionIdText
        {
            get { return connectionId; }
        }

        public LevelRoom SourceRoom
        {
            get { return sourceRoom; }
        }

        public DoorEndpoint SourceDoor
        {
            get { return sourceDoor; }
        }

        public LevelRoom DestinationRoom
        {
            get { return destinationRoom; }
        }

        public DoorEndpoint DestinationDoor
        {
            get { return destinationDoor; }
        }

        public LevelDoorTravelPolicy TravelPolicy
        {
            get { return travelPolicy; }
        }

        public LevelGridConnectionRecord BuildRecord()
        {
            return new LevelGridConnectionRecord(
                connectionId,
                sourceRoom == null ? null : sourceRoom.RoomIdText,
                sourceDoor == null ? null : sourceDoor.DoorIdText,
                destinationRoom == null ? null : destinationRoom.RoomIdText,
                destinationDoor == null ? null : destinationDoor.DoorIdText,
                travelPolicy,
                BuildDiagnosticLocation());
        }

        public void AssignNewStableId()
        {
            connectionId = LevelDesignAuthoringId.New("connection");
        }

        public void ConfigureConnection(
            string configuredConnectionId,
            LevelRoom configuredSourceRoom,
            DoorEndpoint configuredSourceDoor,
            LevelRoom configuredDestinationRoom,
            DoorEndpoint configuredDestinationDoor,
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
            LevelRoom configuredSourceRoom,
            DoorEndpoint configuredSourceDoor,
            LevelRoom configuredDestinationRoom,
            DoorEndpoint configuredDestinationDoor,
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
