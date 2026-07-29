using System;
using System.Collections.Generic;
using UnityEngine;

namespace ShooterMover.UnityAdapters.Authoring.LevelDesign
{
    /// <summary>
    /// Combines room identity/folder invariants with endpoint graph validation.
    /// Existing foundation validation remains a separate mandatory production gate.
    /// </summary>
    public static class LevelGridAuthoringCompositeValidator
    {
        public static LevelGridValidationResult Validate(
            IReadOnlyList<LevelRoomRecord> rooms,
            IReadOnlyList<LevelGridRoomRecord> gridRooms,
            IReadOnlyList<LevelGridDoorRecord> doors,
            IReadOnlyList<LevelGridConnectionRecord> connections,
            LevelGridValidationPurpose purpose)
        {
            if (rooms == null) throw new ArgumentNullException(nameof(rooms));
            if (gridRooms == null) throw new ArgumentNullException(nameof(gridRooms));
            if (doors == null) throw new ArgumentNullException(nameof(doors));
            if (connections == null)
            {
                throw new ArgumentNullException(nameof(connections));
            }

            List<LevelGridProblem> problems =
                new List<LevelGridProblem>();
            ValidateRooms(gridRooms, problems);

            LevelGridValidationResult endpointResult =
                LevelGridAuthoringValidator.Validate(
                    rooms,
                    doors,
                    connections,
                    purpose);
            for (int index = 0; index < endpointResult.Problems.Count; index++)
            {
                problems.Add(endpointResult.Problems[index]);
            }

            ValidateConnectionFacing(
                gridRooms,
                doors,
                connections,
                purpose,
                problems);

            return new LevelGridValidationResult(purpose, problems);
        }

        private static void ValidateRooms(
            IReadOnlyList<LevelGridRoomRecord> rooms,
            ICollection<LevelGridProblem> problems)
        {
            Dictionary<string, List<LevelGridRoomRecord>> roomsById =
                new Dictionary<string, List<LevelGridRoomRecord>>(
                    StringComparer.Ordinal);
            Dictionary<string, List<LevelGridRoomRecord>> roomsByFolderKey =
                new Dictionary<string, List<LevelGridRoomRecord>>(
                    StringComparer.Ordinal);

            for (int index = 0; index < rooms.Count; index++)
            {
                LevelGridRoomRecord room = rooms[index];
                if (room == null)
                {
                    Add(
                        problems,
                        LevelDesignValidationSeverity.Error,
                        LevelGridProblemCode.InvalidRoomIdentity,
                        string.Empty,
                        "rooms[" + index + "]",
                        "Room record is missing.");
                    continue;
                }

                if (!LevelDesignAuthoringId.IsCanonical(room.RoomId))
                {
                    Add(
                        problems,
                        LevelDesignValidationSeverity.Error,
                        LevelGridProblemCode.InvalidRoomIdentity,
                        room.RoomId,
                        room.DiagnosticLocation,
                        "Every room requires a canonical stable ID.");
                }

                Register(roomsById, room.RoomId ?? string.Empty, room);

                if (room.FolderSlot <= 0)
                {
                    Add(
                        problems,
                        LevelDesignValidationSeverity.Error,
                        LevelGridProblemCode.InvalidRoomFolderSlot,
                        room.RoomId,
                        room.DiagnosticLocation,
                        "Room folder slot must be at least 1.");
                }
                else
                {
                    Register(roomsByFolderKey, room.FolderKey, room);
                }
            }

            ReportDuplicates(
                roomsById,
                LevelGridProblemCode.DuplicateRoomIdentity,
                "Room stable ID is duplicated.",
                problems);
            ReportDuplicates(
                roomsByFolderKey,
                LevelGridProblemCode.DuplicateRoomFolderSlot,
                "Two rooms use the same grid coordinate and folder slot.",
                problems);

            ValidateGridFootprints(rooms, problems);
        }

        private static void ValidateGridFootprints(
            IReadOnlyList<LevelGridRoomRecord> rooms,
            ICollection<LevelGridProblem> problems)
        {
            for (int leftIndex = 0; leftIndex < rooms.Count; leftIndex++)
            {
                LevelGridRoomRecord left = rooms[leftIndex];
                if (!CanBuildFootprint(left))
                {
                    continue;
                }

                RectInt leftFootprint = BuildFootprint(left);
                for (int rightIndex = leftIndex + 1;
                    rightIndex < rooms.Count;
                    rightIndex++)
                {
                    LevelGridRoomRecord right = rooms[rightIndex];
                    if (!CanBuildFootprint(right))
                    {
                        continue;
                    }

                    RectInt rightFootprint = BuildFootprint(right);
                    if (!leftFootprint.Overlaps(rightFootprint))
                    {
                        continue;
                    }

                    Add(
                        problems,
                        LevelDesignValidationSeverity.Error,
                        LevelGridProblemCode.OverlappingRoomGridFootprint,
                        left.RoomId,
                        left.DiagnosticLocation,
                        "Room grid footprint overlaps room '" + right.RoomId
                            + "'. Move one room or change its footprint.");
                    Add(
                        problems,
                        LevelDesignValidationSeverity.Error,
                        LevelGridProblemCode.OverlappingRoomGridFootprint,
                        right.RoomId,
                        right.DiagnosticLocation,
                        "Room grid footprint overlaps room '" + left.RoomId
                            + "'. Move one room or change its footprint.");
                }
            }
        }

        private static void ValidateConnectionFacing(
            IReadOnlyList<LevelGridRoomRecord> rooms,
            IReadOnlyList<LevelGridDoorRecord> doors,
            IReadOnlyList<LevelGridConnectionRecord> connections,
            LevelGridValidationPurpose purpose,
            ICollection<LevelGridProblem> problems)
        {
            Dictionary<string, LevelGridRoomRecord> roomsById =
                new Dictionary<string, LevelGridRoomRecord>(StringComparer.Ordinal);
            for (int index = 0; index < rooms.Count; index++)
            {
                LevelGridRoomRecord room = rooms[index];
                if (room != null && !string.IsNullOrEmpty(room.RoomId)
                    && !roomsById.ContainsKey(room.RoomId))
                {
                    roomsById.Add(room.RoomId, room);
                }
            }

            Dictionary<string, LevelGridDoorRecord> doorsByEndpoint =
                new Dictionary<string, LevelGridDoorRecord>(StringComparer.Ordinal);
            for (int index = 0; index < doors.Count; index++)
            {
                LevelGridDoorRecord door = doors[index];
                if (door != null && !doorsByEndpoint.ContainsKey(door.EndpointKey))
                {
                    doorsByEndpoint.Add(door.EndpointKey, door);
                }
            }

            LevelDesignValidationSeverity severity =
                purpose == LevelGridValidationPurpose.ProductionPublish
                    ? LevelDesignValidationSeverity.Error
                    : LevelDesignValidationSeverity.Warning;

            for (int index = 0; index < connections.Count; index++)
            {
                LevelGridConnectionRecord connection = connections[index];
                if (connection == null)
                {
                    continue;
                }

                LevelGridRoomRecord sourceRoom;
                LevelGridRoomRecord destinationRoom;
                LevelGridDoorRecord sourceDoor;
                LevelGridDoorRecord destinationDoor;
                if (!roomsById.TryGetValue(connection.SourceRoomId ?? string.Empty, out sourceRoom)
                    || !roomsById.TryGetValue(
                        connection.DestinationRoomId ?? string.Empty,
                        out destinationRoom)
                    || !doorsByEndpoint.TryGetValue(
                        connection.SourceEndpointKey,
                        out sourceDoor)
                    || !doorsByEndpoint.TryGetValue(
                        connection.DestinationEndpointKey,
                        out destinationDoor))
                {
                    continue;
                }

                ValidateDoorFacing(
                    sourceDoor,
                    sourceRoom,
                    destinationRoom,
                    severity,
                    problems);
                ValidateDoorFacing(
                    destinationDoor,
                    destinationRoom,
                    sourceRoom,
                    severity,
                    problems);
            }
        }

        private static void ValidateDoorFacing(
            LevelGridDoorRecord door,
            LevelGridRoomRecord owningRoom,
            LevelGridRoomRecord otherRoom,
            LevelDesignValidationSeverity severity,
            ICollection<LevelGridProblem> problems)
        {
            if (door.PlacementMode != LevelDoorPlacementMode.EdgeManaged
                || !door.AutoFaceConnection)
            {
                return;
            }

            LevelDoorSide expected;
            if (!TryResolveFacingSide(
                owningRoom.GridCoordinate,
                otherRoom.GridCoordinate,
                out expected)
                || door.Side == expected)
            {
                return;
            }

            Add(
                problems,
                severity,
                LevelGridProblemCode.EdgeManagedDoorFacingMismatch,
                door.DoorId,
                door.DiagnosticLocation,
                "Edge-managed door faces " + door.Side + " but its connected room is "
                    + expected + ". Reflow it or disable automatic facing to keep placement.");
        }

        public static bool TryResolveFacingSide(
            Vector2Int from,
            Vector2Int to,
            out LevelDoorSide side)
        {
            int deltaX = to.x - from.x;
            int deltaY = to.y - from.y;
            if (deltaX == 0 && deltaY == 0)
            {
                side = LevelDoorSide.North;
                return false;
            }

            if (Mathf.Abs(deltaX) >= Mathf.Abs(deltaY))
            {
                side = deltaX >= 0
                    ? LevelDoorSide.East
                    : LevelDoorSide.West;
            }
            else
            {
                side = deltaY >= 0
                    ? LevelDoorSide.North
                    : LevelDoorSide.South;
            }
            return true;
        }

        private static bool CanBuildFootprint(LevelGridRoomRecord room)
        {
            return room != null
                && room.FootprintCells.x > 0
                && room.FootprintCells.y > 0;
        }

        private static RectInt BuildFootprint(LevelGridRoomRecord room)
        {
            return new RectInt(
                room.GridCoordinate,
                room.FootprintCells);
        }

        private static void Register(
            IDictionary<string, List<LevelGridRoomRecord>> index,
            string key,
            LevelGridRoomRecord room)
        {
            List<LevelGridRoomRecord> values;
            if (!index.TryGetValue(key, out values))
            {
                values = new List<LevelGridRoomRecord>();
                index.Add(key, values);
            }
            values.Add(room);
        }

        private static void ReportDuplicates(
            IReadOnlyDictionary<string, List<LevelGridRoomRecord>> index,
            LevelGridProblemCode code,
            string message,
            ICollection<LevelGridProblem> problems)
        {
            foreach (KeyValuePair<string, List<LevelGridRoomRecord>> pair in index)
            {
                if (pair.Value.Count < 2 || string.IsNullOrEmpty(pair.Key))
                {
                    continue;
                }

                for (int valueIndex = 0; valueIndex < pair.Value.Count; valueIndex++)
                {
                    LevelGridRoomRecord room = pair.Value[valueIndex];
                    Add(
                        problems,
                        LevelDesignValidationSeverity.Error,
                        code,
                        room.RoomId,
                        room.DiagnosticLocation,
                        message + " Conflict count: " + pair.Value.Count + ".");
                }
            }
        }

        private static void Add(
            ICollection<LevelGridProblem> problems,
            LevelDesignValidationSeverity severity,
            LevelGridProblemCode code,
            string authoredId,
            string diagnosticLocation,
            string message)
        {
            problems.Add(new LevelGridProblem(
                severity,
                code,
                authoredId,
                diagnosticLocation,
                message));
        }
    }
}
