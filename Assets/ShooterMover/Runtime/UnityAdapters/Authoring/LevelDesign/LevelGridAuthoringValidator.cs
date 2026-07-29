using System;
using System.Collections.Generic;

namespace ShooterMover.UnityAdapters.Authoring.LevelDesign
{
    public static class LevelGridAuthoringValidator
    {
        public static LevelGridValidationResult Validate(
            IReadOnlyList<LevelRoomRecord> rooms,
            IReadOnlyList<LevelGridDoorRecord> doors,
            IReadOnlyList<LevelGridConnectionRecord> connections,
            LevelGridValidationPurpose purpose)
        {
            if (rooms == null) throw new ArgumentNullException(nameof(rooms));
            if (doors == null) throw new ArgumentNullException(nameof(doors));
            if (connections == null)
            {
                throw new ArgumentNullException(nameof(connections));
            }

            List<LevelGridProblem> problems = new List<LevelGridProblem>();
            Dictionary<string, LevelRoomRecord> roomById = BuildRoomIndex(rooms);
            Dictionary<string, LevelGridDoorRecord> doorByEndpoint =
                new Dictionary<string, LevelGridDoorRecord>(StringComparer.Ordinal);
            Dictionary<string, List<LevelGridDoorRecord>> doorsById =
                new Dictionary<string, List<LevelGridDoorRecord>>(
                    StringComparer.Ordinal);
            Dictionary<string, int> connectionUseByEndpoint =
                new Dictionary<string, int>(StringComparer.Ordinal);

            ValidateDoors(
                doors,
                roomById,
                doorByEndpoint,
                doorsById,
                problems);
            ValidateDuplicateDoorIds(doorsById, problems);
            ValidateConnections(
                connections,
                doorByEndpoint,
                connectionUseByEndpoint,
                problems);
            ValidateUnconnectedDoors(
                doors,
                connectionUseByEndpoint,
                purpose,
                problems);

            return new LevelGridValidationResult(purpose, problems);
        }

        private static Dictionary<string, LevelRoomRecord> BuildRoomIndex(
            IReadOnlyList<LevelRoomRecord> rooms)
        {
            Dictionary<string, LevelRoomRecord> roomById =
                new Dictionary<string, LevelRoomRecord>(StringComparer.Ordinal);
            for (int index = 0; index < rooms.Count; index++)
            {
                LevelRoomRecord room = rooms[index];
                if (room == null || string.IsNullOrEmpty(room.RoomId))
                {
                    continue;
                }

                if (!roomById.ContainsKey(room.RoomId))
                {
                    roomById.Add(room.RoomId, room);
                }
            }

            return roomById;
        }

        private static void ValidateDoors(
            IReadOnlyList<LevelGridDoorRecord> doors,
            IReadOnlyDictionary<string, LevelRoomRecord> roomById,
            IDictionary<string, LevelGridDoorRecord> doorByEndpoint,
            IDictionary<string, List<LevelGridDoorRecord>> doorsById,
            ICollection<LevelGridProblem> problems)
        {
            for (int index = 0; index < doors.Count; index++)
            {
                LevelGridDoorRecord door = doors[index];
                if (door == null)
                {
                    Add(
                        problems,
                        LevelDesignValidationSeverity.Error,
                        LevelGridProblemCode.InvalidDoorIdentity,
                        string.Empty,
                        "doors[" + index + "]",
                        "Door endpoint record is missing.");
                    continue;
                }

                if (!LevelDesignAuthoringId.IsCanonical(door.DoorId))
                {
                    Add(
                        problems,
                        LevelDesignValidationSeverity.Error,
                        LevelGridProblemCode.InvalidDoorIdentity,
                        door.DoorId,
                        door.DiagnosticLocation,
                        "Every door endpoint requires a canonical stable ID.");
                }

                List<LevelGridDoorRecord> sameId;
                if (!doorsById.TryGetValue(door.DoorId ?? string.Empty, out sameId))
                {
                    sameId = new List<LevelGridDoorRecord>();
                    doorsById.Add(door.DoorId ?? string.Empty, sameId);
                }
                sameId.Add(door);

                if (string.IsNullOrEmpty(door.RoomId)
                    || !roomById.ContainsKey(door.RoomId))
                {
                    Add(
                        problems,
                        LevelDesignValidationSeverity.Error,
                        LevelGridProblemCode.MissingOwningRoom,
                        door.DoorId,
                        door.DiagnosticLocation,
                        "Door endpoint must belong to a room in this level.");
                }

                if (door.PlacementMode == LevelDoorPlacementMode.EdgeManaged
                    && (door.EdgeOffset < 0f || door.EdgeOffset > 1f
                        || float.IsNaN(door.EdgeOffset)
                        || float.IsInfinity(door.EdgeOffset)))
                {
                    Add(
                        problems,
                        LevelDesignValidationSeverity.Error,
                        LevelGridProblemCode.InvalidDoorPlacement,
                        door.DoorId,
                        door.DiagnosticLocation,
                        "Edge-managed door offset must be between 0 and 1.");
                }

                if (door.PlacementMode == LevelDoorPlacementMode.Fixed
                    && (!IsFinite(door.FixedLocalPosition.x)
                        || !IsFinite(door.FixedLocalPosition.y)))
                {
                    Add(
                        problems,
                        LevelDesignValidationSeverity.Error,
                        LevelGridProblemCode.InvalidDoorPlacement,
                        door.DoorId,
                        door.DiagnosticLocation,
                        "Fixed door position must contain finite coordinates.");
                }

                if (!doorByEndpoint.ContainsKey(door.EndpointKey))
                {
                    doorByEndpoint.Add(door.EndpointKey, door);
                }
            }
        }

        private static void ValidateDuplicateDoorIds(
            IReadOnlyDictionary<string, List<LevelGridDoorRecord>> doorsById,
            ICollection<LevelGridProblem> problems)
        {
            foreach (KeyValuePair<string, List<LevelGridDoorRecord>> pair in doorsById)
            {
                if (pair.Value.Count < 2 || string.IsNullOrEmpty(pair.Key))
                {
                    continue;
                }

                for (int index = 0; index < pair.Value.Count; index++)
                {
                    LevelGridDoorRecord door = pair.Value[index];
                    Add(
                        problems,
                        LevelDesignValidationSeverity.Error,
                        LevelGridProblemCode.DuplicateDoorIdentity,
                        pair.Key,
                        door.DiagnosticLocation,
                        "Door stable ID is used by " + pair.Value.Count
                            + " endpoints. Duplicate the object only after assigning a new ID.");
                }
            }
        }

        private static void ValidateConnections(
            IReadOnlyList<LevelGridConnectionRecord> connections,
            IReadOnlyDictionary<string, LevelGridDoorRecord> doorByEndpoint,
            IDictionary<string, int> connectionUseByEndpoint,
            ICollection<LevelGridProblem> problems)
        {
            Dictionary<string, List<LevelGridConnectionRecord>> connectionsById =
                new Dictionary<string, List<LevelGridConnectionRecord>>(
                    StringComparer.Ordinal);

            for (int index = 0; index < connections.Count; index++)
            {
                LevelGridConnectionRecord connection = connections[index];
                if (connection == null)
                {
                    Add(
                        problems,
                        LevelDesignValidationSeverity.Error,
                        LevelGridProblemCode.InvalidConnectionIdentity,
                        string.Empty,
                        "connections[" + index + "]",
                        "Connection record is missing.");
                    continue;
                }

                if (!LevelDesignAuthoringId.IsCanonical(connection.ConnectionId))
                {
                    Add(
                        problems,
                        LevelDesignValidationSeverity.Error,
                        LevelGridProblemCode.InvalidConnectionIdentity,
                        connection.ConnectionId,
                        connection.DiagnosticLocation,
                        "Every connection requires a canonical stable ID.");
                }

                RegisterConnectionId(connectionsById, connection);

                LevelGridDoorRecord sourceDoor;
                bool sourceExists = doorByEndpoint.TryGetValue(
                    connection.SourceEndpointKey,
                    out sourceDoor);
                LevelGridDoorRecord destinationDoor;
                bool destinationExists = doorByEndpoint.TryGetValue(
                    connection.DestinationEndpointKey,
                    out destinationDoor);

                if (!sourceExists || !destinationExists)
                {
                    Add(
                        problems,
                        LevelDesignValidationSeverity.Error,
                        LevelGridProblemCode.MissingConnectionEndpoint,
                        connection.ConnectionId,
                        connection.DiagnosticLocation,
                        "Connection must reference two existing room ID + door ID endpoints.");
                }

                if (sourceExists
                    && !string.Equals(
                        sourceDoor.RoomId,
                        connection.SourceRoomId,
                        StringComparison.Ordinal))
                {
                    Add(
                        problems,
                        LevelDesignValidationSeverity.Error,
                        LevelGridProblemCode.EndpointRoomMismatch,
                        connection.ConnectionId,
                        connection.DiagnosticLocation,
                        "Source door does not belong to the referenced source room.");
                }

                if (destinationExists
                    && !string.Equals(
                        destinationDoor.RoomId,
                        connection.DestinationRoomId,
                        StringComparison.Ordinal))
                {
                    Add(
                        problems,
                        LevelDesignValidationSeverity.Error,
                        LevelGridProblemCode.EndpointRoomMismatch,
                        connection.ConnectionId,
                        connection.DiagnosticLocation,
                        "Destination door does not belong to the referenced destination room.");
                }

                if (string.Equals(
                        connection.SourceRoomId,
                        connection.DestinationRoomId,
                        StringComparison.Ordinal)
                    || string.Equals(
                        connection.SourceEndpointKey,
                        connection.DestinationEndpointKey,
                        StringComparison.Ordinal))
                {
                    Add(
                        problems,
                        LevelDesignValidationSeverity.Error,
                        LevelGridProblemCode.SelfConnection,
                        connection.ConnectionId,
                        connection.DiagnosticLocation,
                        "A room connection must join two different room endpoints.");
                }

                if (sourceExists)
                {
                    IncrementUse(connectionUseByEndpoint, connection.SourceEndpointKey);
                }
                if (destinationExists)
                {
                    IncrementUse(connectionUseByEndpoint, connection.DestinationEndpointKey);
                }
            }

            foreach (KeyValuePair<string, int> pair in connectionUseByEndpoint)
            {
                if (pair.Value < 2)
                {
                    continue;
                }

                LevelGridDoorRecord door;
                doorByEndpoint.TryGetValue(pair.Key, out door);
                Add(
                    problems,
                    LevelDesignValidationSeverity.Error,
                    LevelGridProblemCode.DoorUsedByMultipleConnections,
                    door == null ? pair.Key : door.DoorId,
                    door == null ? string.Empty : door.DiagnosticLocation,
                    "A door endpoint may participate in only one connection; found "
                        + pair.Value + ".");
            }

            ValidateDuplicateConnectionIds(connectionsById, problems);
        }

        private static void ValidateDuplicateConnectionIds(
            IReadOnlyDictionary<string, List<LevelGridConnectionRecord>> connectionsById,
            ICollection<LevelGridProblem> problems)
        {
            foreach (KeyValuePair<string, List<LevelGridConnectionRecord>> pair
                in connectionsById)
            {
                if (pair.Value.Count < 2 || string.IsNullOrEmpty(pair.Key))
                {
                    continue;
                }

                for (int index = 0; index < pair.Value.Count; index++)
                {
                    LevelGridConnectionRecord connection = pair.Value[index];
                    Add(
                        problems,
                        LevelDesignValidationSeverity.Error,
                        LevelGridProblemCode.DuplicateConnectionIdentity,
                        pair.Key,
                        connection.DiagnosticLocation,
                        "Connection stable ID is duplicated.");
                }
            }
        }

        private static void ValidateUnconnectedDoors(
            IReadOnlyList<LevelGridDoorRecord> doors,
            IReadOnlyDictionary<string, int> connectionUseByEndpoint,
            LevelGridValidationPurpose purpose,
            ICollection<LevelGridProblem> problems)
        {
            LevelDesignValidationSeverity severity =
                purpose == LevelGridValidationPurpose.ProductionPublish
                    ? LevelDesignValidationSeverity.Error
                    : LevelDesignValidationSeverity.Warning;

            for (int index = 0; index < doors.Count; index++)
            {
                LevelGridDoorRecord door = doors[index];
                if (door == null || !door.Traversable)
                {
                    continue;
                }

                int useCount;
                if (connectionUseByEndpoint.TryGetValue(door.EndpointKey, out useCount)
                    && useCount > 0)
                {
                    continue;
                }

                Add(
                    problems,
                    severity,
                    LevelGridProblemCode.UnconnectedTraversableDoor,
                    door.DoorId,
                    door.DiagnosticLocation,
                    purpose == LevelGridValidationPurpose.ProductionPublish
                        ? "Production publish is blocked until this traversable door is connected or marked non-traversable."
                        : "Traversable door is currently unconnected; draft saving remains allowed.");
            }
        }

        private static void RegisterConnectionId(
            IDictionary<string, List<LevelGridConnectionRecord>> connectionsById,
            LevelGridConnectionRecord connection)
        {
            string key = connection.ConnectionId ?? string.Empty;
            List<LevelGridConnectionRecord> sameId;
            if (!connectionsById.TryGetValue(key, out sameId))
            {
                sameId = new List<LevelGridConnectionRecord>();
                connectionsById.Add(key, sameId);
            }
            sameId.Add(connection);
        }

        private static void IncrementUse(
            IDictionary<string, int> connectionUseByEndpoint,
            string endpointKey)
        {
            int count;
            connectionUseByEndpoint.TryGetValue(endpointKey, out count);
            connectionUseByEndpoint[endpointKey] = count + 1;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
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
