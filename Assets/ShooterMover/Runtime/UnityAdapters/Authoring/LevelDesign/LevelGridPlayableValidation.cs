using System;
using System.Collections.Generic;

namespace ShooterMover.UnityAdapters.Authoring.LevelDesign
{
    /// <summary>
    /// Applies the one playable-graph rule that is not expressible by generic Level topology:
    /// the exact configured final-exit endpoint remains traversable at runtime but is intentionally
    /// not required to connect to another authored room. It may not serve both roles.
    /// </summary>
    public static class LevelGridPlayableValidation
    {
        public static LevelGridValidationResult Validate(
            IReadOnlyList<LevelRoomRecord> rooms,
            IReadOnlyList<LevelGridRoomRecord> gridRooms,
            IReadOnlyList<LevelGridDoorRecord> doors,
            IReadOnlyList<LevelGridConnectionRecord> connections,
            LevelGridValidationPurpose purpose,
            string finalExitRoomId,
            string finalExitDoorId)
        {
            if (rooms == null) throw new ArgumentNullException(nameof(rooms));
            if (gridRooms == null) throw new ArgumentNullException(nameof(gridRooms));
            if (doors == null) throw new ArgumentNullException(nameof(doors));
            if (connections == null) throw new ArgumentNullException(nameof(connections));

            bool hasExactFinalExit = !string.IsNullOrWhiteSpace(finalExitRoomId)
                && !string.IsNullOrWhiteSpace(finalExitDoorId);
            var effectiveDoors = new List<LevelGridDoorRecord>(doors.Count);
            for (int index = 0; index < doors.Count; index++)
            {
                LevelGridDoorRecord door = doors[index];
                if (hasExactFinalExit
                    && door != null
                    && string.Equals(
                        door.RoomId,
                        finalExitRoomId,
                        StringComparison.Ordinal)
                    && string.Equals(
                        door.DoorId,
                        finalExitDoorId,
                        StringComparison.Ordinal))
                {
                    effectiveDoors.Add(new LevelGridDoorRecord(
                        door.DoorId,
                        door.RoomId,
                        door.Side,
                        door.PlacementMode,
                        door.EdgeOffset,
                        door.FixedLocalPosition,
                        false,
                        door.VisibleOnMap,
                        door.AutoFaceConnection,
                        door.DiagnosticLocation));
                }
                else
                {
                    effectiveDoors.Add(door);
                }
            }

            LevelGridValidationResult generic =
                LevelGridAuthoringCompositeValidator.Validate(
                    rooms,
                    gridRooms,
                    effectiveDoors,
                    connections,
                    purpose);
            if (!hasExactFinalExit)
            {
                return generic;
            }

            var problems = new List<LevelGridProblem>(generic.Problems);
            for (int index = 0; index < connections.Count; index++)
            {
                LevelGridConnectionRecord connection = connections[index];
                if (connection == null
                    || !TouchesExactFinalExit(
                        connection,
                        finalExitRoomId,
                        finalExitDoorId))
                {
                    continue;
                }

                problems.Add(new LevelGridProblem(
                    LevelDesignValidationSeverity.Error,
                    LevelGridProblemCode.DoorUsedByMultipleConnections,
                    connection.ConnectionId,
                    connection.DiagnosticLocation,
                    "Connection '" + connection.ConnectionId
                        + "' uses configured final-exit endpoint '"
                        + finalExitRoomId + "::" + finalExitDoorId
                        + "'. The exact final-exit door cannot also link two authored rooms."));
            }

            return new LevelGridValidationResult(purpose, problems);
        }

        private static bool TouchesExactFinalExit(
            LevelGridConnectionRecord connection,
            string finalExitRoomId,
            string finalExitDoorId)
        {
            return (string.Equals(
                        connection.SourceRoomId,
                        finalExitRoomId,
                        StringComparison.Ordinal)
                    && string.Equals(
                        connection.SourceDoorId,
                        finalExitDoorId,
                        StringComparison.Ordinal))
                || (string.Equals(
                        connection.DestinationRoomId,
                        finalExitRoomId,
                        StringComparison.Ordinal)
                    && string.Equals(
                        connection.DestinationDoorId,
                        finalExitDoorId,
                        StringComparison.Ordinal));
        }
    }
}
