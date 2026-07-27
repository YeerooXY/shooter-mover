using System;
using System.Collections.Generic;

namespace ShooterMover.UnityAdapters.Authoring.LevelDesign
{
    public static class LevelGridAuthoringV2Validator
    {
        public static LevelGridValidationResultV2 Validate(
            IReadOnlyList<LevelRoomRecord> rooms,
            IReadOnlyList<LevelGridDoorRecordV2> doors,
            IReadOnlyList<LevelGridConnectionRecordV2> connections,
            LevelGridValidationPurposeV2 purpose)
        {
            if (rooms == null) throw new ArgumentNullException(nameof(rooms));
            if (doors == null) throw new ArgumentNullException(nameof(doors));
            if (connections == null)
            {
                throw new ArgumentNullException(nameof(connections));
            }

            List<LevelGridProblemV2> problems = new List<LevelGridProblemV2>();
            Dictionary<string, LevelRoomRecord> roomById = BuildRoomIndex(rooms);
            Dictionary<string, LevelGridDoorRecordV2> doorByEndpoint =
                new Dictionary<string, LevelGridDoorRecordV2>(StringComparer.Ordinal);
            Dictionary<string, List<LevelGridDoorRecordV2>> doorsById =
                new Dictionary<string, List<LevelGridDoorRecordV2>>(
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

            return new LevelGridValidationResultV2(purpose, problems);
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
            IReadOnlyList<LevelGridDoorRecordV2> doors,
            IReadOnlyDictionary<string, LevelRoomRecord> roomById,
            IDictionary<string, LevelGridDoorRecordV2> doorByEndpoint,
            IDictionary<string, List<LevelGridDoorRecordV2>> doorsById,
            ICollection<LevelGridProblemV2> problems)
        {
            for (int index = 0; index < doors.Count; index++)
            {
                LevelGridDoorRecordV2 door = doors[index];
                if (door == null)
                {
                    Add(
                        problems,
                        LevelDesignValidationSeverity.Error,
                        LevelGridProblemCodeV2.InvalidDoorIdentity,
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
                        LevelGridProblemCodeV2.InvalidDoorIdentity,
                        door.DoorId,
                        door.DiagnosticLocation,
                        "Every door endpoint requires a canonical stable ID.");
                }

                List<LevelGridDoorRecordV2> sameId;
                if (!doorsById.TryGetValue(door.DoorId ?? string.Empty, out sameId))
                {
                    sameId = new List<LevelGridDoorRecordV2>();
                    doorsById.Add(door.DoorId ?? string.Empty, sameId);
                }
                sameId.Add(door);

                if (string.IsNullOrEmpty(door.RoomId)
                    || !roomById.ContainsKey(door.RoomId))
                {
                    Add(
                        problems,
                        LevelDesignValidationSeverity.Error,
                        LevelGridProblemCodeV2.MissingOwningRoom,
                        door.DoorId,
                        door.DiagnosticLocation,
                        "Door endpoint must belong to a room in this level.");
                }

                if (door.PlacementMode == LevelDoorPlacementModeV2.EdgeManaged
                    && (door.EdgeOffset < 0f || door.EdgeOffset > 1f
                        || float.IsNaN(door.EdgeOffset)
                        || float.IsInfinity(door.EdgeOffset)))
                {
                    Add(
                        problems,
                        LevelDesignValidationSeverity.Error,
                        LevelGridProblemCodeV2.InvalidDoorPlacement,
                        door.DoorId,
                        door.DiagnosticLocation,
                        "Edge-managed door offset must be between 0 and 1.");
                }

                if (door.PlacementMode == LevelDoorPlacementModeV2.Fixed
                    && (!IsFinite(door.FixedLocalPosition.x)
                        || !IsFinite(door.FixedLocalPosition.y)))
                {
                    Add(
                        problems,
                        LevelDesignValidationSeverity.Error,
                        LevelGridProblemCodeV2.InvalidDoorPlacement,
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
            IReadOnlyDictionary<string, List<LevelGridDoorRecordV2>> doorsById,
            ICollection<LevelGridProblemV2> problems)
        {
            foreach (KeyValuePair<string, List<LevelGridDoorRecordV2>> pair in doorsById)
            {
                if (pair.Value.Count < 2 || string.IsNullOrEmpty(pair.Key))
                {
                    continue;
                }

                for (int index = 0; index < pair.Value.Count; index++)
                {
                    LevelGridDoorRecordV2 door = pair.Value[index];
                    Add(
                        problems,
                        LevelDesignValidationSeverity.Error,
                        LevelGridProblemCodeV2.DuplicateDoorIdentity,
                        pair.Key,
                        door.DiagnosticLocation,
                        "Door stable ID is used by " + pair.Value.Count
                            + " endpoints. Duplicate the object only after assigning a new ID.");
                }
            }
        }

        private static void ValidateConnections(
            IReadOnlyList<LevelGridConnectionRecordV2> connections,
            IReadOnlyDictionary<string, LevelGridDoorRecordV2> doorByEndpoint,
            IDictionary<string, int> connectionUseByEndpoint,
            ICollection<LevelGridProblemV2> problems)
        {
            Dictionary<string, List<LevelGridConnectionRecordV2>> connectionsById =
                new Dictionary<string, List<LevelGridConnectionRecordV2>>(
                    StringComparer.Ordinal);

            for (int index = 0; index < connections.Count; index++)
            {
                LevelGridConnectionRecordV2 connection = connections[index];
                if (connection == null)
                {
                    Add(
                        problems,
                        LevelDesignValidationSeverity.Error,
                        LevelGridProblemCodeV2.InvalidConnectionIdentity,
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
                        LevelGridProblemCodeV2.InvalidConnectionIdentity,
                        connection.ConnectionId,
                        connection.DiagnosticLocation,
                        "Every connection requires a canonical stable ID.");
                }

                RegisterConnectionId(connectionsById, connection);

                LevelGridDoorRecordV2 sourceDoor;
                bool sourceExists = doorByEndpoint.TryGetValue(
                    connection.SourceEndpointKey,
                    out sourceDoor);
                LevelGridDoorRecordV2 destinationDoor;
                bool destinationExists = doorByEndpoint.TryGetValue(
                    connection.DestinationEndpointKey,
                    out destinationDoor);

                if (!sourceExists || !destinationExists)
                {
                    Add(
                        problems,
                        LevelDesignValidationSeverity.Error,
                        LevelGridProblemCodeV2.MissingConnectionEndpoint,
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
                        LevelGridProblemCodeV2.EndpointRoomMismatch,
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
                        LevelGridProblemCodeV2.EndpointRoomMismatch,
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
                        LevelGridProblemCodeV2.SelfConnection,
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

                LevelGridDoorRecordV2 door;
                doorByEndpoint.TryGetValue(pair.Key, out door);
                Add(
                    problems,
                    LevelDesignValidationSeverity.Error,
                    LevelGridProblemCodeV2.DoorUsedByMultipleConnections,
                    door == null ? pair.Key : door.DoorId,
                    door == null ? string.Empty : door.DiagnosticLocation,
                    "A door endpoint may participate in only one connection; found "
                        + pair.Value + ".");
            }

            ValidateDuplicateConnectionIds(connectionsById, problems);
        }

        private static void ValidateDuplicateConnectionIds(
            IReadOnlyDictionary<string, List<LevelGridConnectionRecordV2>> connectionsById,
            ICollection<LevelGridProblemV2> problems)
        {
            foreach (KeyValuePair<string, List<LevelGridConnectionRecordV2>> pair
                in connectionsById)
            {
                if (pair.Value.Count < 2 || string.IsNullOrEmpty(pair.Key))
                {
                    continue;
                }

                for (int index = 0; index < pair.Value.Count; index++)
                {
                    LevelGridConnectionRecordV2 connection = pair.Value[index];
                    Add(
                        problems,
                        LevelDesignValidationSeverity.Error,
                        LevelGridProblemCodeV2.DuplicateConnectionIdentity,
                        pair.Key,
                        connection.DiagnosticLocation,
                        "Connection stable ID is duplicated.");
                }
            }
        }

        private static void ValidateUnconnectedDoors(
            IReadOnlyList<LevelGridDoorRecordV2> doors,
            IReadOnlyDictionary<string, int> connectionUseByEndpoint,
            LevelGridValidationPurposeV2 purpose,
            ICollection<LevelGridProblemV2> problems)
        {
            LevelDesignValidationSeverity severity =
                purpose == LevelGridValidationPurposeV2.ProductionPublish
                    ? LevelDesignValidationSeverity.Error
                    : LevelDesignValidationSeverity.Warning;

            for (int index = 0; index < doors.Count; index++)
            {
                LevelGridDoorRecordV2 door = doors[index];
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
                    LevelGridProblemCodeV2.UnconnectedTraversableDoor,
                    door.DoorId,
                    door.DiagnosticLocation,
                    purpose == LevelGridValidationPurposeV2.ProductionPublish
                        ? "Production publish is blocked until this traversable door is connected or marked non-traversable."
                        : "Traversable door is currently unconnected; draft saving remains allowed.");
            }
        }

        private static void RegisterConnectionId(
            IDictionary<string, List<LevelGridConnectionRecordV2>> connectionsById,
            LevelGridConnectionRecordV2 connection)
        {
            string key = connection.ConnectionId ?? string.Empty;
            List<LevelGridConnectionRecordV2> sameId;
            if (!connectionsById.TryGetValue(key, out sameId))
            {
                sameId = new List<LevelGridConnectionRecordV2>();
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
