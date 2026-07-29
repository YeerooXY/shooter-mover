using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using UnityEngine;

namespace ShooterMover.UnityAdapters.Authoring.LevelDesign
{
    public enum LevelDoorSide
    {
        North = 1,
        East = 2,
        South = 3,
        West = 4,
    }

    public enum LevelDoorPlacementMode
    {
        EdgeManaged = 1,
        Fixed = 2,
    }

    public enum LevelGridValidationPurpose
    {
        Draft = 1,
        ProductionPublish = 2,
    }

    public enum LevelGridProblemCode
    {
        InvalidDoorIdentity = 1,
        DuplicateDoorIdentity = 2,
        MissingOwningRoom = 3,
        InvalidDoorPlacement = 4,
        InvalidConnectionIdentity = 5,
        DuplicateConnectionIdentity = 6,
        MissingConnectionEndpoint = 7,
        EndpointRoomMismatch = 8,
        SelfConnection = 9,
        DoorUsedByMultipleConnections = 10,
        UnconnectedTraversableDoor = 11,
        InvalidRoomIdentity = 12,
        DuplicateRoomIdentity = 13,
        OverlappingRoomGridFootprint = 14,
        InvalidRoomFolderSlot = 15,
        DuplicateRoomFolderSlot = 16,
        EdgeManagedDoorFacingMismatch = 17,
    }

    public sealed class LevelGridRoomRecord
    {
        public LevelGridRoomRecord(
            string roomId,
            Vector2Int gridCoordinate,
            Vector2Int footprintCells,
            int folderSlot,
            string diagnosticLocation)
        {
            RoomId = roomId;
            GridCoordinate = gridCoordinate;
            FootprintCells = footprintCells;
            FolderSlot = folderSlot;
            DiagnosticLocation = diagnosticLocation ?? string.Empty;
        }

        public string RoomId { get; }

        public Vector2Int GridCoordinate { get; }

        public Vector2Int FootprintCells { get; }

        public int FolderSlot { get; }

        public string DiagnosticLocation { get; }

        public string FolderKey
        {
            get
            {
                return GridCoordinate.x + "," + GridCoordinate.y + ":"
                    + FolderSlot;
            }
        }

        public string FolderName
        {
            get
            {
                return "Room_" + GridCoordinate.x + "_" + GridCoordinate.y
                    + "_" + FolderSlot.ToString("00");
            }
        }
    }

    public sealed class LevelGridDoorRecord
    {
        public LevelGridDoorRecord(
            string doorId,
            string roomId,
            LevelDoorSide side,
            LevelDoorPlacementMode placementMode,
            float edgeOffset,
            Vector2 fixedLocalPosition,
            bool traversable,
            bool visibleOnMap,
            string diagnosticLocation)
            : this(
                doorId,
                roomId,
                side,
                placementMode,
                edgeOffset,
                fixedLocalPosition,
                traversable,
                visibleOnMap,
                true,
                diagnosticLocation)
        {
        }

        public LevelGridDoorRecord(
            string doorId,
            string roomId,
            LevelDoorSide side,
            LevelDoorPlacementMode placementMode,
            float edgeOffset,
            Vector2 fixedLocalPosition,
            bool traversable,
            bool visibleOnMap,
            bool autoFaceConnection,
            string diagnosticLocation)
        {
            DoorId = doorId;
            RoomId = roomId;
            Side = side;
            PlacementMode = placementMode;
            EdgeOffset = edgeOffset;
            FixedLocalPosition = fixedLocalPosition;
            Traversable = traversable;
            VisibleOnMap = visibleOnMap;
            AutoFaceConnection = autoFaceConnection;
            DiagnosticLocation = diagnosticLocation ?? string.Empty;
        }

        public string DoorId { get; }

        public string RoomId { get; }

        public LevelDoorSide Side { get; }

        public LevelDoorPlacementMode PlacementMode { get; }

        public float EdgeOffset { get; }

        public Vector2 FixedLocalPosition { get; }

        public bool Traversable { get; }

        public bool VisibleOnMap { get; }

        public bool AutoFaceConnection { get; }

        public string DiagnosticLocation { get; }

        public string EndpointKey
        {
            get { return BuildEndpointKey(RoomId, DoorId); }
        }

        public static string BuildEndpointKey(string roomId, string doorId)
        {
            return (roomId ?? string.Empty) + "::" + (doorId ?? string.Empty);
        }
    }

    public sealed class LevelGridConnectionRecord
    {
        public LevelGridConnectionRecord(
            string connectionId,
            string sourceRoomId,
            string sourceDoorId,
            string destinationRoomId,
            string destinationDoorId,
            LevelDoorTravelPolicy travelPolicy,
            string diagnosticLocation)
        {
            ConnectionId = connectionId;
            SourceRoomId = sourceRoomId;
            SourceDoorId = sourceDoorId;
            DestinationRoomId = destinationRoomId;
            DestinationDoorId = destinationDoorId;
            TravelPolicy = travelPolicy;
            DiagnosticLocation = diagnosticLocation ?? string.Empty;
        }

        public string ConnectionId { get; }

        public string SourceRoomId { get; }

        public string SourceDoorId { get; }

        public string DestinationRoomId { get; }

        public string DestinationDoorId { get; }

        public LevelDoorTravelPolicy TravelPolicy { get; }

        public string DiagnosticLocation { get; }

        public string SourceEndpointKey
        {
            get
            {
                return LevelGridDoorRecord.BuildEndpointKey(
                    SourceRoomId,
                    SourceDoorId);
            }
        }

        public string DestinationEndpointKey
        {
            get
            {
                return LevelGridDoorRecord.BuildEndpointKey(
                    DestinationRoomId,
                    DestinationDoorId);
            }
        }
    }

    public sealed class LevelGridProblem
    {
        public LevelGridProblem(
            LevelDesignValidationSeverity severity,
            LevelGridProblemCode code,
            string authoredId,
            string diagnosticLocation,
            string message)
        {
            Severity = severity;
            Code = code;
            AuthoredId = authoredId ?? string.Empty;
            DiagnosticLocation = diagnosticLocation ?? string.Empty;
            Message = message ?? string.Empty;
        }

        public LevelDesignValidationSeverity Severity { get; }

        public LevelGridProblemCode Code { get; }

        public string AuthoredId { get; }

        public string DiagnosticLocation { get; }

        public string Message { get; }

        public override string ToString()
        {
            string location = string.IsNullOrEmpty(DiagnosticLocation)
                ? "<unknown>"
                : DiagnosticLocation;
            return Severity + " " + Code + " [" + AuthoredId + "] at "
                + location + ": " + Message;
        }
    }

    public sealed class LevelGridValidationResult
    {
        private readonly ReadOnlyCollection<LevelGridProblem> problems;

        internal LevelGridValidationResult(
            LevelGridValidationPurpose purpose,
            IEnumerable<LevelGridProblem> problems)
        {
            Purpose = purpose;
            this.problems = new ReadOnlyCollection<LevelGridProblem>(
                new List<LevelGridProblem>(
                    problems ?? throw new ArgumentNullException(nameof(problems))));
        }

        public LevelGridValidationPurpose Purpose { get; }

        public IReadOnlyList<LevelGridProblem> Problems
        {
            get { return problems; }
        }

        public int ErrorCount
        {
            get
            {
                int count = 0;
                for (int index = 0; index < problems.Count; index++)
                {
                    if (problems[index].Severity == LevelDesignValidationSeverity.Error)
                    {
                        count++;
                    }
                }
                return count;
            }
        }

        public int WarningCount
        {
            get
            {
                int count = 0;
                for (int index = 0; index < problems.Count; index++)
                {
                    if (problems[index].Severity == LevelDesignValidationSeverity.Warning)
                    {
                        count++;
                    }
                }
                return count;
            }
        }

        public int UnconnectedTraversableDoorCount
        {
            get
            {
                int count = 0;
                for (int index = 0; index < problems.Count; index++)
                {
                    if (problems[index].Code
                        == LevelGridProblemCode.UnconnectedTraversableDoor)
                    {
                        count++;
                    }
                }
                return count;
            }
        }

        public bool CanSaveDraft
        {
            get { return true; }
        }

        public bool CanPublish
        {
            get
            {
                return ErrorCount == 0
                    && UnconnectedTraversableDoorCount == 0;
            }
        }

        public static LevelGridValidationResult Empty(
            LevelGridValidationPurpose purpose = LevelGridValidationPurpose.Draft)
        {
            return new LevelGridValidationResult(
                purpose,
                Array.Empty<LevelGridProblem>());
        }
    }
}
