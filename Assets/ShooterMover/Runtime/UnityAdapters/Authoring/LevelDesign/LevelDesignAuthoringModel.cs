using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using ShooterMover.Domain.Common;
using UnityEngine;

namespace ShooterMover.UnityAdapters.Authoring.LevelDesign
{
    public enum LevelRoomAlignment
    {
        GridOrigin = 1,
        Centered = 2,
        Custom = 3,
    }

    public enum LevelCollisionPolicy
    {
        None = 0,
        TriggerOnly = 1,
        Solid = 2,
        DoorControlled = 3,
    }

    public enum LevelRestartPolicy
    {
        Persistent = 1,
        ResetProjection = 2,
        RecreateFromDefinition = 3,
    }

    public enum LevelPlacementKind
    {
        PlayerSpawn = 1,
        EnemySpawn = 2,
        PropPlacement = 3,
        PickupSpawn = 4,
        RewardSocket = 5,
        Entry = 6,
        Exit = 7,
    }

    public enum LevelDoorTravelPolicy
    {
        Bidirectional = 1,
        ForwardOnly = 2,
        ReverseOnly = 3,
    }

    public enum LevelVoidEffect
    {
        InstantDefeat = 1,
        RespawnAtCheckpoint = 2,
        Damage = 3,
        RemoveTransient = 4,
    }

    public enum LevelDesignValidationSeverity
    {
        Info = 1,
        Warning = 2,
        Error = 3,
    }

    public enum LevelDesignValidationCode
    {
        InvalidLevelIdentity = 1,
        InvalidAuthoredIdentity = 2,
        DuplicateAuthoredIdentity = 3,
        MissingRoomReference = 4,
        MissingDefinitionReference = 5,
        MissingPrefabReference = 6,
        MissingPresentation = 7,
        MissingCollider = 8,
        InvalidGridMetadata = 9,
        RoomOverlap = 10,
        PlacementOverlap = 11,
        InvalidRoomConnection = 12,
        MissingDoorPackage = 13,
        MissingDoorPresentation = 14,
        MissingDoorCollision = 15,
        InvalidRewardOverride = 16,
        InvalidSocketIdentity = 17,
        SpawnInsideVoid = 18,
        InvalidVoidRegion = 19,
        MissingSocketReference = 20,
        DuplicateSocketIdentity = 21,
    }

    public interface ILevelDoorPackageAdapter
    {
        bool HasDoorController { get; }

        bool HasClosedPresentation { get; }

        bool HasOpenPresentation { get; }

        bool HasClosedCollider { get; }
    }

    public sealed class LevelRoomRecord
    {
        public LevelRoomRecord(
            string roomId,
            Vector2Int gridCoordinate,
            Vector2 cellSize,
            Vector2Int footprintCells,
            LevelRoomAlignment alignment,
            Rect worldBounds,
            bool hasBoundsCollider,
            int sortingOrder,
            Vector2Int mapCoordinate,
            bool visibleOnMap,
            string diagnosticLocation)
        {
            RoomId = roomId;
            GridCoordinate = gridCoordinate;
            CellSize = cellSize;
            FootprintCells = footprintCells;
            Alignment = alignment;
            WorldBounds = worldBounds;
            HasBoundsCollider = hasBoundsCollider;
            SortingOrder = sortingOrder;
            MapCoordinate = mapCoordinate;
            VisibleOnMap = visibleOnMap;
            DiagnosticLocation = diagnosticLocation ?? string.Empty;
        }

        public string RoomId { get; }

        public Vector2Int GridCoordinate { get; }

        public Vector2 CellSize { get; }

        public Vector2Int FootprintCells { get; }

        public LevelRoomAlignment Alignment { get; }

        public Rect WorldBounds { get; }

        public bool HasBoundsCollider { get; }

        public int SortingOrder { get; }

        public Vector2Int MapCoordinate { get; }

        public bool VisibleOnMap { get; }

        public string DiagnosticLocation { get; }
    }

    public sealed class LevelPlacementRecord
    {
        public LevelPlacementRecord(
            string authoredId,
            string socketId,
            LevelPlacementKind kind,
            string roomId,
            Vector2Int localGridCoordinate,
            Rect worldBounds,
            bool hasDefinition,
            bool hasPrefab,
            bool hasPresentation,
            bool hasCollider,
            LevelCollisionPolicy collisionPolicy,
            LevelRestartPolicy restartPolicy,
            string rewardOverrideId,
            bool visibleOnMap,
            int sortingOrder,
            string diagnosticLocation)
        {
            AuthoredId = authoredId;
            SocketId = socketId;
            Kind = kind;
            RoomId = roomId;
            LocalGridCoordinate = localGridCoordinate;
            WorldBounds = worldBounds;
            HasDefinition = hasDefinition;
            HasPrefab = hasPrefab;
            HasPresentation = hasPresentation;
            HasCollider = hasCollider;
            CollisionPolicy = collisionPolicy;
            RestartPolicy = restartPolicy;
            RewardOverrideId = rewardOverrideId;
            VisibleOnMap = visibleOnMap;
            SortingOrder = sortingOrder;
            DiagnosticLocation = diagnosticLocation ?? string.Empty;
        }

        public string AuthoredId { get; }

        public string SocketId { get; }

        public LevelPlacementKind Kind { get; }

        public string RoomId { get; }

        public Vector2Int LocalGridCoordinate { get; }

        public Rect WorldBounds { get; }

        public bool HasDefinition { get; }

        public bool HasPrefab { get; }

        public bool HasPresentation { get; }

        public bool HasCollider { get; }

        public LevelCollisionPolicy CollisionPolicy { get; }

        public LevelRestartPolicy RestartPolicy { get; }

        public string RewardOverrideId { get; }

        public bool VisibleOnMap { get; }

        public int SortingOrder { get; }

        public string DiagnosticLocation { get; }
    }

    public sealed class LevelDoorRecord
    {
        public LevelDoorRecord(
            string authoredId,
            string sourceRoomId,
            string destinationRoomId,
            string sourceSocketId,
            string destinationSocketId,
            Vector2Int sourceGridEdge,
            Vector2Int destinationGridEdge,
            bool requireAdjacentRooms,
            LevelDoorTravelPolicy travelPolicy,
            bool hasPackageAdapter,
            bool hasDoorController,
            bool hasClosedPresentation,
            bool hasOpenPresentation,
            bool hasClosedCollider,
            string diagnosticLocation)
        {
            AuthoredId = authoredId;
            SourceRoomId = sourceRoomId;
            DestinationRoomId = destinationRoomId;
            SourceSocketId = sourceSocketId;
            DestinationSocketId = destinationSocketId;
            SourceGridEdge = sourceGridEdge;
            DestinationGridEdge = destinationGridEdge;
            RequireAdjacentRooms = requireAdjacentRooms;
            TravelPolicy = travelPolicy;
            HasPackageAdapter = hasPackageAdapter;
            HasDoorController = hasDoorController;
            HasClosedPresentation = hasClosedPresentation;
            HasOpenPresentation = hasOpenPresentation;
            HasClosedCollider = hasClosedCollider;
            DiagnosticLocation = diagnosticLocation ?? string.Empty;
        }

        public string AuthoredId { get; }

        public string SourceRoomId { get; }

        public string DestinationRoomId { get; }

        public string SourceSocketId { get; }

        public string DestinationSocketId { get; }

        public Vector2Int SourceGridEdge { get; }

        public Vector2Int DestinationGridEdge { get; }

        public bool RequireAdjacentRooms { get; }

        public LevelDoorTravelPolicy TravelPolicy { get; }

        public bool HasPackageAdapter { get; }

        public bool HasDoorController { get; }

        public bool HasClosedPresentation { get; }

        public bool HasOpenPresentation { get; }

        public bool HasClosedCollider { get; }

        public string DiagnosticLocation { get; }
    }

    public sealed class LevelVoidRecord
    {
        public LevelVoidRecord(
            string authoredId,
            string roomId,
            Rect worldBounds,
            bool hasCollider,
            bool colliderIsTrigger,
            LevelVoidEffect effect,
            LevelRestartPolicy restartPolicy,
            string diagnosticLocation)
        {
            AuthoredId = authoredId;
            RoomId = roomId;
            WorldBounds = worldBounds;
            HasCollider = hasCollider;
            ColliderIsTrigger = colliderIsTrigger;
            Effect = effect;
            RestartPolicy = restartPolicy;
            DiagnosticLocation = diagnosticLocation ?? string.Empty;
        }

        public string AuthoredId { get; }

        public string RoomId { get; }

        public Rect WorldBounds { get; }

        public bool HasCollider { get; }

        public bool ColliderIsTrigger { get; }

        public LevelVoidEffect Effect { get; }

        public LevelRestartPolicy RestartPolicy { get; }

        public string DiagnosticLocation { get; }
    }

    public sealed class LevelDesignValidationIssue
    {
        public LevelDesignValidationIssue(
            LevelDesignValidationSeverity severity,
            LevelDesignValidationCode code,
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

        public LevelDesignValidationCode Code { get; }

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

    public sealed class LevelDesignValidationResult
    {
        private readonly ReadOnlyCollection<LevelDesignValidationIssue> issues;

        internal LevelDesignValidationResult(
            IEnumerable<LevelDesignValidationIssue> issues)
        {
            this.issues = new ReadOnlyCollection<LevelDesignValidationIssue>(
                new List<LevelDesignValidationIssue>(
                    issues ?? throw new ArgumentNullException(nameof(issues))));
        }

        public IReadOnlyList<LevelDesignValidationIssue> Issues
        {
            get { return issues; }
        }

        public int ErrorCount
        {
            get
            {
                return issues.Count(
                    issue => issue.Severity == LevelDesignValidationSeverity.Error);
            }
        }

        public int WarningCount
        {
            get
            {
                return issues.Count(
                    issue => issue.Severity == LevelDesignValidationSeverity.Warning);
            }
        }

        public bool IsValid
        {
            get { return ErrorCount == 0; }
        }

        public static LevelDesignValidationResult Empty()
        {
            return new LevelDesignValidationResult(
                Array.Empty<LevelDesignValidationIssue>());
        }
    }

    public static class LevelDesignAuthoringId
    {
        public static bool IsCanonical(string value)
        {
            StableId ignored;
            return StableId.TryParse(value, out ignored);
        }

        public static string New(string namespaceName)
        {
            if (string.IsNullOrEmpty(namespaceName))
            {
                throw new ArgumentException(
                    "A StableId namespace is required.",
                    nameof(namespaceName));
            }

            return StableId.Create(
                namespaceName,
                Guid.NewGuid().ToString("N")).ToString();
        }
    }

}
