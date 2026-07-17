using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ShooterMover.UnityAdapters.Authoring.LevelDesign
{
    public static partial class LevelDesignFoundationValidator
    {
        private static void ValidateDoors(
            IReadOnlyList<LevelDoorRecord> doors,
            IReadOnlyDictionary<string, LevelRoomRecord> roomById,
            IReadOnlyDictionary<string, LevelPlacementRecord> placementBySocketId,
            ICollection<LevelDesignValidationIssue> issues)
        {
            for (int index = 0; index < doors.Count; index++)
            {
                LevelDoorRecord door = doors[index];
                if (door == null)
                {
                    Add(
                        issues,
                        LevelDesignValidationSeverity.Error,
                        LevelDesignValidationCode.InvalidAuthoredIdentity,
                        string.Empty,
                        "doors[" + index + "]",
                        "Door record is missing.");
                    continue;
                }

                if (!LevelDesignAuthoringId.IsCanonical(door.AuthoredId))
                {
                    Add(
                        issues,
                        LevelDesignValidationSeverity.Error,
                        LevelDesignValidationCode.InvalidAuthoredIdentity,
                        door.AuthoredId,
                        door.DiagnosticLocation,
                        "Door identity must be a canonical StableId.");
                }

                bool sourceValid = !string.IsNullOrEmpty(door.SourceRoomId)
                    && roomById.ContainsKey(door.SourceRoomId);
                bool destinationValid =
                    !string.IsNullOrEmpty(door.DestinationRoomId)
                    && roomById.ContainsKey(door.DestinationRoomId);
                if (!sourceValid || !destinationValid)
                {
                    Add(
                        issues,
                        LevelDesignValidationSeverity.Error,
                        LevelDesignValidationCode.MissingRoomReference,
                        door.AuthoredId,
                        door.DiagnosticLocation,
                        "Door must reference two rooms in this authored foundation.");
                }
                else if (string.Equals(
                    door.SourceRoomId,
                    door.DestinationRoomId,
                    StringComparison.Ordinal))
                {
                    Add(
                        issues,
                        LevelDesignValidationSeverity.Error,
                        LevelDesignValidationCode.InvalidRoomConnection,
                        door.AuthoredId,
                        door.DiagnosticLocation,
                        "Door source and destination rooms must be different.");
                }

                bool sourceSocketCanonical =
                    LevelDesignAuthoringId.IsCanonical(door.SourceSocketId);
                bool destinationSocketCanonical =
                    LevelDesignAuthoringId.IsCanonical(door.DestinationSocketId);
                if (!sourceSocketCanonical || !destinationSocketCanonical)
                {
                    Add(
                        issues,
                        LevelDesignValidationSeverity.Error,
                        LevelDesignValidationCode.InvalidSocketIdentity,
                        door.AuthoredId,
                        door.DiagnosticLocation,
                        "Door source and destination socket IDs must be canonical StableIds.");
                }
                else
                {
                    ValidateDoorSocketReference(
                        door,
                        door.SourceSocketId,
                        door.SourceRoomId,
                        "source",
                        placementBySocketId,
                        issues);
                    ValidateDoorSocketReference(
                        door,
                        door.DestinationSocketId,
                        door.DestinationRoomId,
                        "destination",
                        placementBySocketId,
                        issues);
                }

                if (door.RequireAdjacentRooms)
                {
                    int distance = Math.Abs(
                            door.SourceGridEdge.x - door.DestinationGridEdge.x)
                        + Math.Abs(
                            door.SourceGridEdge.y - door.DestinationGridEdge.y);
                    if (distance != 1)
                    {
                        Add(
                            issues,
                            LevelDesignValidationSeverity.Error,
                            LevelDesignValidationCode.InvalidRoomConnection,
                            door.AuthoredId,
                            door.DiagnosticLocation,
                            "Adjacent door grid edges must be exactly one cell apart.");
                    }
                }

                if (!door.HasPackageAdapter || !door.HasDoorController)
                {
                    Add(
                        issues,
                        LevelDesignValidationSeverity.Error,
                        LevelDesignValidationCode.MissingDoorPackage,
                        door.AuthoredId,
                        door.DiagnosticLocation,
                        "Configured door must consume the existing DOOR-001 package.");
                }

                if (!door.HasClosedPresentation || !door.HasOpenPresentation)
                {
                    Add(
                        issues,
                        LevelDesignValidationSeverity.Error,
                        LevelDesignValidationCode.MissingDoorPresentation,
                        door.AuthoredId,
                        door.DiagnosticLocation,
                        "Configured door requires distinct closed and open presentation roots.");
                }

                if (!door.HasClosedCollider)
                {
                    Add(
                        issues,
                        LevelDesignValidationSeverity.Error,
                        LevelDesignValidationCode.MissingDoorCollision,
                        door.AuthoredId,
                        door.DiagnosticLocation,
                        "Configured door requires at least one closed-state collider.");
                }
            }
        }

        private static void ValidateDoorSocketReference(
            LevelDoorRecord door,
            string socketId,
            string expectedRoomId,
            string endpointName,
            IReadOnlyDictionary<string, LevelPlacementRecord> placementBySocketId,
            ICollection<LevelDesignValidationIssue> issues)
        {
            LevelPlacementRecord placement;
            if (!placementBySocketId.TryGetValue(socketId, out placement))
            {
                Add(
                    issues,
                    LevelDesignValidationSeverity.Error,
                    LevelDesignValidationCode.MissingSocketReference,
                    door.AuthoredId,
                    door.DiagnosticLocation,
                    "Door " + endpointName + " socket '" + socketId
                    + "' does not reference an authored entry/exit socket.");
                return;
            }

            if (placement.Kind != LevelPlacementKind.Entry
                && placement.Kind != LevelPlacementKind.Exit)
            {
                Add(
                    issues,
                    LevelDesignValidationSeverity.Error,
                    LevelDesignValidationCode.InvalidRoomConnection,
                    door.AuthoredId,
                    door.DiagnosticLocation,
                    "Door " + endpointName
                    + " must reference an Entry or Exit placement socket.");
            }

            if (!string.Equals(
                placement.RoomId,
                expectedRoomId,
                StringComparison.Ordinal))
            {
                Add(
                    issues,
                    LevelDesignValidationSeverity.Error,
                    LevelDesignValidationCode.InvalidRoomConnection,
                    door.AuthoredId,
                    door.DiagnosticLocation,
                    "Door " + endpointName + " socket belongs to room '"
                    + placement.RoomId + "' instead of '" + expectedRoomId + "'.");
            }
        }

        private static void ValidateVoids(
            IReadOnlyList<LevelVoidRecord> voids,
            IReadOnlyDictionary<string, LevelRoomRecord> roomById,
            ICollection<LevelDesignValidationIssue> issues)
        {
            for (int index = 0; index < voids.Count; index++)
            {
                LevelVoidRecord region = voids[index];
                if (region == null)
                {
                    Add(
                        issues,
                        LevelDesignValidationSeverity.Error,
                        LevelDesignValidationCode.InvalidVoidRegion,
                        string.Empty,
                        "voids[" + index + "]",
                        "Void record is missing.");
                    continue;
                }

                if (!LevelDesignAuthoringId.IsCanonical(region.AuthoredId))
                {
                    Add(
                        issues,
                        LevelDesignValidationSeverity.Error,
                        LevelDesignValidationCode.InvalidAuthoredIdentity,
                        region.AuthoredId,
                        region.DiagnosticLocation,
                        "Void identity must be a canonical StableId.");
                }

                if (string.IsNullOrEmpty(region.RoomId)
                    || !roomById.ContainsKey(region.RoomId))
                {
                    Add(
                        issues,
                        LevelDesignValidationSeverity.Error,
                        LevelDesignValidationCode.MissingRoomReference,
                        region.AuthoredId,
                        region.DiagnosticLocation,
                        "Void region must reference a room in this authored foundation.");
                }

                if (!region.HasCollider
                    || region.WorldBounds.width <= 0f
                    || region.WorldBounds.height <= 0f)
                {
                    Add(
                        issues,
                        LevelDesignValidationSeverity.Error,
                        LevelDesignValidationCode.InvalidVoidRegion,
                        region.AuthoredId,
                        region.DiagnosticLocation,
                        "Void region requires an explicit non-empty Collider2D.");
                }
                else if (!region.ColliderIsTrigger)
                {
                    Add(
                        issues,
                        LevelDesignValidationSeverity.Error,
                        LevelDesignValidationCode.InvalidVoidRegion,
                        region.AuthoredId,
                        region.DiagnosticLocation,
                        "Void region collider must be configured as a trigger.");
                }
            }
        }

        private static void ValidateGlobalIdentityUniqueness(
            IReadOnlyList<LevelRoomRecord> rooms,
            IReadOnlyList<LevelPlacementRecord> placements,
            IReadOnlyList<LevelDoorRecord> doors,
            IReadOnlyList<LevelVoidRecord> voids,
            ICollection<LevelDesignValidationIssue> issues)
        {
            Dictionary<string, List<string>> locationsById =
                new Dictionary<string, List<string>>(StringComparer.Ordinal);

            for (int index = 0; index < rooms.Count; index++)
            {
                LevelRoomRecord room = rooms[index];
                Register(
                    locationsById,
                    room == null ? null : room.RoomId,
                    room == null ? "rooms[" + index + "]" : room.DiagnosticLocation);
            }

            for (int index = 0; index < placements.Count; index++)
            {
                LevelPlacementRecord placement = placements[index];
                Register(
                    locationsById,
                    placement == null ? null : placement.AuthoredId,
                    placement == null
                        ? "placements[" + index + "]"
                        : placement.DiagnosticLocation);
            }

            for (int index = 0; index < doors.Count; index++)
            {
                LevelDoorRecord door = doors[index];
                Register(
                    locationsById,
                    door == null ? null : door.AuthoredId,
                    door == null ? "doors[" + index + "]" : door.DiagnosticLocation);
            }

            for (int index = 0; index < voids.Count; index++)
            {
                LevelVoidRecord region = voids[index];
                Register(
                    locationsById,
                    region == null ? null : region.AuthoredId,
                    region == null ? "voids[" + index + "]" : region.DiagnosticLocation);
            }

            foreach (KeyValuePair<string, List<string>> pair in locationsById)
            {
                if (pair.Value.Count < 2)
                {
                    continue;
                }

                string locations = string.Join(
                    ", ",
                    pair.Value.OrderBy(value => value, StringComparer.Ordinal));
                for (int index = 0; index < pair.Value.Count; index++)
                {
                    Add(
                        issues,
                        LevelDesignValidationSeverity.Error,
                        LevelDesignValidationCode.DuplicateAuthoredIdentity,
                        pair.Key,
                        pair.Value[index],
                        "Authored ID is duplicated at: " + locations + ".");
                }
            }
        }

        private static void ValidateRoomOverlaps(
            IReadOnlyList<LevelRoomRecord> rooms,
            ICollection<LevelDesignValidationIssue> issues)
        {
            for (int leftIndex = 0; leftIndex < rooms.Count; leftIndex++)
            {
                LevelRoomRecord left = rooms[leftIndex];
                if (left == null || !left.HasBoundsCollider)
                {
                    continue;
                }

                for (int rightIndex = leftIndex + 1;
                    rightIndex < rooms.Count;
                    rightIndex++)
                {
                    LevelRoomRecord right = rooms[rightIndex];
                    if (right == null || !right.HasBoundsCollider)
                    {
                        continue;
                    }

                    if (!left.WorldBounds.Overlaps(right.WorldBounds))
                    {
                        continue;
                    }

                    Add(
                        issues,
                        LevelDesignValidationSeverity.Error,
                        LevelDesignValidationCode.RoomOverlap,
                        left.RoomId,
                        left.DiagnosticLocation,
                        "Room bounds overlap room '" + right.RoomId + "'.");
                    Add(
                        issues,
                        LevelDesignValidationSeverity.Error,
                        LevelDesignValidationCode.RoomOverlap,
                        right.RoomId,
                        right.DiagnosticLocation,
                        "Room bounds overlap room '" + left.RoomId + "'.");
                }
            }
        }

        private static void ValidatePlacementOverlaps(
            IReadOnlyList<LevelPlacementRecord> placements,
            ICollection<LevelDesignValidationIssue> issues)
        {
            for (int leftIndex = 0; leftIndex < placements.Count; leftIndex++)
            {
                LevelPlacementRecord left = placements[leftIndex];
                if (left == null
                    || left.CollisionPolicy != LevelCollisionPolicy.Solid
                    || !left.HasCollider)
                {
                    continue;
                }

                for (int rightIndex = leftIndex + 1;
                    rightIndex < placements.Count;
                    rightIndex++)
                {
                    LevelPlacementRecord right = placements[rightIndex];
                    if (right == null
                        || right.CollisionPolicy != LevelCollisionPolicy.Solid
                        || !right.HasCollider
                        || !string.Equals(
                            left.RoomId,
                            right.RoomId,
                            StringComparison.Ordinal)
                        || !left.WorldBounds.Overlaps(right.WorldBounds))
                    {
                        continue;
                    }

                    Add(
                        issues,
                        LevelDesignValidationSeverity.Error,
                        LevelDesignValidationCode.PlacementOverlap,
                        left.AuthoredId,
                        left.DiagnosticLocation,
                        "Solid placement overlaps '" + right.AuthoredId + "'.");
                    Add(
                        issues,
                        LevelDesignValidationSeverity.Error,
                        LevelDesignValidationCode.PlacementOverlap,
                        right.AuthoredId,
                        right.DiagnosticLocation,
                        "Solid placement overlaps '" + left.AuthoredId + "'.");
                }
            }
        }

        private static void Register(
            IDictionary<string, List<string>> locationsById,
            string authoredId,
            string location)
        {
            if (!LevelDesignAuthoringId.IsCanonical(authoredId))
            {
                return;
            }

            List<string> locations;
            if (!locationsById.TryGetValue(authoredId, out locations))
            {
                locations = new List<string>();
                locationsById.Add(authoredId, locations);
            }

            locations.Add(location ?? string.Empty);
        }

        private static void Add(
            ICollection<LevelDesignValidationIssue> issues,
            LevelDesignValidationSeverity severity,
            LevelDesignValidationCode code,
            string authoredId,
            string location,
            string message)
        {
            issues.Add(
                new LevelDesignValidationIssue(
                    severity,
                    code,
                    authoredId,
                    location,
                    message));
        }
    }
}
