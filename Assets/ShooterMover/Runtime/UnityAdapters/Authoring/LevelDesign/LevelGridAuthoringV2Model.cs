using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using UnityEngine;

namespace ShooterMover.UnityAdapters.Authoring.LevelDesign
{
    public enum LevelDoorSideV2
    {
        North = 1,
        East = 2,
        South = 3,
        West = 4,
    }

    public enum LevelDoorPlacementModeV2
    {
        EdgeManaged = 1,
        Fixed = 2,
    }

    public enum LevelGridValidationPurposeV2
    {
        Draft = 1,
        ProductionPublish = 2,
    }

    public enum LevelGridProblemCodeV2
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
    }

    public sealed class LevelGridDoorRecordV2
    {
        public LevelGridDoorRecordV2(
            string doorId,
            string roomId,
            LevelDoorSideV2 side,
            LevelDoorPlacementModeV2 placementMode,
            float edgeOffset,
            Vector2 fixedLocalPosition,
            bool traversable,
            bool visibleOnMap,
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
            DiagnosticLocation = diagnosticLocation ?? string.Empty;
        }

        public string DoorId { get; }

        public string RoomId { get; }

        public LevelDoorSideV2 Side { get; }

        public LevelDoorPlacementModeV2 PlacementMode { get; }

        public float EdgeOffset { get; }

        public Vector2 FixedLocalPosition { get; }

        public bool Traversable { get; }

        public bool VisibleOnMap { get; }

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

    public sealed class LevelGridConnectionRecordV2
    {
        public LevelGridConnectionRecordV2(
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
                return LevelGridDoorRecordV2.BuildEndpointKey(
                    SourceRoomId,
                    SourceDoorId);
            }
        }

        public string DestinationEndpointKey
        {
            get
            {
                return LevelGridDoorRecordV2.BuildEndpointKey(
                    DestinationRoomId,
                    DestinationDoorId);
            }
        }
    }

    public sealed class LevelGridProblemV2
    {
        public LevelGridProblemV2(
            LevelDesignValidationSeverity severity,
            LevelGridProblemCodeV2 code,
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

        public LevelGridProblemCodeV2 Code { get; }

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

    public sealed class LevelGridValidationResultV2
    {
        private readonly ReadOnlyCollection<LevelGridProblemV2> problems;

        internal LevelGridValidationResultV2(
            LevelGridValidationPurposeV2 purpose,
            IEnumerable<LevelGridProblemV2> problems)
        {
            Purpose = purpose;
            this.problems = new ReadOnlyCollection<LevelGridProblemV2>(
                new List<LevelGridProblemV2>(
                    problems ?? throw new ArgumentNullException(nameof(problems))));
        }

        public LevelGridValidationPurposeV2 Purpose { get; }

        public IReadOnlyList<LevelGridProblemV2> Problems
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
                        == LevelGridProblemCodeV2.UnconnectedTraversableDoor)
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

        public static LevelGridValidationResultV2 Empty(
            LevelGridValidationPurposeV2 purpose = LevelGridValidationPurposeV2.Draft)
        {
            return new LevelGridValidationResultV2(
                purpose,
                Array.Empty<LevelGridProblemV2>());
        }
    }
}
