using System;
using System.Collections.Generic;
using UnityEngine;

namespace ShooterMover.UnityAdapters.Authoring.LevelDesign
{
    /// <summary>
    /// Combines room identity/folder invariants with endpoint graph validation.
    /// Existing foundation validation remains a separate mandatory production gate.
    /// </summary>
    public static class LevelGridAuthoringV2CompositeValidator
    {
        public static LevelGridValidationResultV2 Validate(
            IReadOnlyList<LevelRoomRecord> rooms,
            IReadOnlyList<LevelGridRoomRecordV2> gridRooms,
            IReadOnlyList<LevelGridDoorRecordV2> doors,
            IReadOnlyList<LevelGridConnectionRecordV2> connections,
            LevelGridValidationPurposeV2 purpose)
        {
            if (rooms == null) throw new ArgumentNullException(nameof(rooms));
            if (gridRooms == null) throw new ArgumentNullException(nameof(gridRooms));
            if (doors == null) throw new ArgumentNullException(nameof(doors));
            if (connections == null)
            {
                throw new ArgumentNullException(nameof(connections));
            }

            List<LevelGridProblemV2> problems =
                new List<LevelGridProblemV2>();
            ValidateRooms(gridRooms, problems);

            LevelGridValidationResultV2 endpointResult =
                LevelGridAuthoringV2Validator.Validate(
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

            return new LevelGridValidationResultV2(purpose, problems);
        }

        private static void ValidateRooms(
            IReadOnlyList<LevelGridRoomRecordV2> rooms,
            ICollection<LevelGridProblemV2> problems)
        {
            Dictionary<string, List<LevelGridRoomRecordV2>> roomsById =
                new Dictionary<string, List<LevelGridRoomRecordV2>>(
                    StringComparer.Ordinal);
            Dictionary<string, List<LevelGridRoomRecordV2>> roomsByFolderKey =
                new Dictionary<string, List<LevelGridRoomRecordV2>>(
                    StringComparer.Ordinal);

            for (int index = 0; index < rooms.Count; index++)
            {
                LevelGridRoomRecordV2 room = rooms[index];
                if (room == null)
                {
                    Add(
                        problems,
                        LevelDesignValidationSeverity.Error,
                        LevelGridProblemCodeV2.InvalidRoomIdentity,
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
                        LevelGridProblemCodeV2.InvalidRoomIdentity,
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
                        LevelGridProblemCodeV2.InvalidRoomFolderSlot,
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
                LevelGridProblemCodeV2.DuplicateRoomIdentity,
                "Room stable ID is duplicated.",
                problems);
            ReportDuplicates(
                roomsByFolderKey,
                LevelGridProblemCodeV2.DuplicateRoomFolderSlot,
                "Two rooms use the same grid coordinate and folder slot.",
                problems);

            ValidateGridFootprints(rooms, problems);
        }

        private static void ValidateGridFootprints(
            IReadOnlyList<LevelGridRoomRecordV2> rooms,
            ICollection<LevelGridProblemV2> problems)
        {
            for (int leftIndex = 0; leftIndex < rooms.Count; leftIndex++)
            {
                LevelGridRoomRecordV2 left = rooms[leftIndex];
                if (!CanBuildFootprint(left))
                {
                    continue;
                }

                RectInt leftFootprint = BuildFootprint(left);
                for (int rightIndex = leftIndex + 1;
                    rightIndex < rooms.Count;
                    rightIndex++)
                {
                    LevelGridRoomRecordV2 right = rooms[rightIndex];
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
                        LevelGridProblemCodeV2.OverlappingRoomGridFootprint,
                        left.RoomId,
                        left.DiagnosticLocation,
                        "Room grid footprint overlaps room '" + right.RoomId
                            + "'. Move one room or change its footprint.");
                    Add(
                        problems,
                        LevelDesignValidationSeverity.Error,
                        LevelGridProblemCodeV2.OverlappingRoomGridFootprint,
                        right.RoomId,
                        right.DiagnosticLocation,
                        "Room grid footprint overlaps room '" + left.RoomId
                            + "'. Move one room or change its footprint.");
                }
            }
        }

        private static void ValidateConnectionFacing(
            IReadOnlyList<LevelGridRoomRecordV2> rooms,
            IReadOnlyList<LevelGridDoorRecordV2> doors,
            IReadOnlyList<LevelGridConnectionRecordV2> connections,
            LevelGridValidationPurposeV2 purpose,
            ICollection<LevelGridProblemV2> problems)
        {
            Dictionary<string, LevelGridRoomRecordV2> roomsById =
                new Dictionary<string, LevelGridRoomRecordV2>(StringComparer.Ordinal);
            for (int index = 0; index < rooms.Count; index++)
            {
                LevelGridRoomRecordV2 room = rooms[index];
                if (room != null && !string.IsNullOrEmpty(room.RoomId)
                    && !roomsById.ContainsKey(room.RoomId))
                {
                    roomsById.Add(room.RoomId, room);
                }
            }

            Dictionary<string, LevelGridDoorRecordV2> doorsByEndpoint =
                new Dictionary<string, LevelGridDoorRecordV2>(StringComparer.Ordinal);
            for (int index = 0; index < doors.Count; index++)
            {
                LevelGridDoorRecordV2 door = doors[index];
                if (door != null && !doorsByEndpoint.ContainsKey(door.EndpointKey))
                {
                    doorsByEndpoint.Add(door.EndpointKey, door);
                }
            }

            LevelDesignValidationSeverity severity =
                purpose == LevelGridValidationPurposeV2.ProductionPublish
                    ? LevelDesignValidationSeverity.Error
                    : LevelDesignValidationSeverity.Warning;

            for (int index = 0; index < connections.Count; index++)
            {
                LevelGridConnectionRecordV2 connection = connections[index];
                if (connection == null)
                {
                    continue;
                }

                LevelGridRoomRecordV2 sourceRoom;
                LevelGridRoomRecordV2 destinationRoom;
                LevelGridDoorRecordV2 sourceDoor;
                LevelGridDoorRecordV2 destinationDoor;
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
            LevelGridDoorRecordV2 door,
            LevelGridRoomRecordV2 owningRoom,
            LevelGridRoomRecordV2 otherRoom,
            LevelDesignValidationSeverity severity,
            ICollection<LevelGridProblemV2> problems)
        {
            if (door.PlacementMode != LevelDoorPlacementModeV2.EdgeManaged
                || !door.AutoFaceConnection)
            {
                return;
            }

            LevelDoorSideV2 expected;
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
                LevelGridProblemCodeV2.EdgeManagedDoorFacingMismatch,
                door.DoorId,
                door.DiagnosticLocation,
                "Edge-managed door faces " + door.Side + " but its connected room is "
                    + expected + ". Reflow it or disable automatic facing to keep placement.");
        }

        public static bool TryResolveFacingSide(
            Vector2Int from,
            Vector2Int to,
            out LevelDoorSideV2 side)
        {
            int deltaX = to.x - from.x;
            int deltaY = to.y - from.y;
            if (deltaX == 0 && deltaY == 0)
            {
                side = LevelDoorSideV2.North;
                return false;
            }

            if (Mathf.Abs(deltaX) >= Mathf.Abs(deltaY))
            {
                side = deltaX >= 0
                    ? LevelDoorSideV2.East
                    : LevelDoorSideV2.West;
            }
            else
            {
                side = deltaY >= 0
                    ? LevelDoorSideV2.North
                    : LevelDoorSideV2.South;
            }
            return true;
        }

        private static bool CanBuildFootprint(LevelGridRoomRecordV2 room)
        {
            return room != null
                && room.FootprintCells.x > 0
                && room.FootprintCells.y > 0;
        }

        private static RectInt BuildFootprint(LevelGridRoomRecordV2 room)
        {
            return new RectInt(
                room.GridCoordinate,
                room.FootprintCells);
        }

        private static void Register(
            IDictionary<string, List<LevelGridRoomRecordV2>> index,
            string key,
            LevelGridRoomRecordV2 room)
        {
            List<LevelGridRoomRecordV2> values;
            if (!index.TryGetValue(key, out values))
            {
                values = new List<LevelGridRoomRecordV2>();
                index.Add(key, values);
            }
            values.Add(room);
        }

        private static void ReportDuplicates(
            IReadOnlyDictionary<string, List<LevelGridRoomRecordV2>> index,
            LevelGridProblemCodeV2 code,
            string message,
            ICollection<LevelGridProblemV2> problems)
        {
            foreach (KeyValuePair<string, List<LevelGridRoomRecordV2>> pair in index)
            {
                if (pair.Value.Count < 2 || string.IsNullOrEmpty(pair.Key))
                {
                    continue;
                }

                for (int valueIndex = 0; valueIndex < pair.Value.Count; valueIndex++)
                {
                    LevelGridRoomRecordV2 room = pair.Value[valueIndex];
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
            ICollection<LevelGridProblemV2> problems,
            LevelDesignValidationSeverity severity,
            LevelGridProblemCodeV2 code,
            string authoredId,
            string diagnosticLocation,
            string message)
        {
            problems.Add(new LevelGridProblemV2(
                severity,
                code,
                authoredId,
                diagnosticLocation,
                message));
        }
    }
}
