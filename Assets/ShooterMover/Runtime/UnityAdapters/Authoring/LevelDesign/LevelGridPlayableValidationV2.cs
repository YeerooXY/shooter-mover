using System;
using System.Collections.Generic;

namespace ShooterMover.UnityAdapters.Authoring.LevelDesign
{
    /// <summary>
    /// Applies the one playable-graph rule that is not expressible by generic Grid V2 topology:
    /// the exact configured final-exit endpoint remains traversable at runtime but is intentionally
    /// not required to connect to another authored room. It may not serve both roles.
    /// </summary>
    public static class LevelGridPlayableValidationV2
    {
        public static LevelGridValidationResultV2 Validate(
            IReadOnlyList<LevelRoomRecord> rooms,
            IReadOnlyList<LevelGridRoomRecordV2> gridRooms,
            IReadOnlyList<LevelGridDoorRecordV2> doors,
            IReadOnlyList<LevelGridConnectionRecordV2> connections,
            LevelGridValidationPurposeV2 purpose,
            string finalExitRoomId,
            string finalExitDoorId)
        {
            if (rooms == null) throw new ArgumentNullException(nameof(rooms));
            if (gridRooms == null) throw new ArgumentNullException(nameof(gridRooms));
            if (doors == null) throw new ArgumentNullException(nameof(doors));
            if (connections == null) throw new ArgumentNullException(nameof(connections));

            bool hasExactFinalExit = !string.IsNullOrWhiteSpace(finalExitRoomId)
                && !string.IsNullOrWhiteSpace(finalExitDoorId);
            var effectiveDoors = new List<LevelGridDoorRecordV2>(doors.Count);
            for (int index = 0; index < doors.Count; index++)
            {
                LevelGridDoorRecordV2 door = doors[index];
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
                    effectiveDoors.Add(new LevelGridDoorRecordV2(
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

            LevelGridValidationResultV2 generic =
                LevelGridAuthoringV2CompositeValidator.Validate(
                    rooms,
                    gridRooms,
                    effectiveDoors,
                    connections,
                    purpose);
            if (!hasExactFinalExit)
            {
                return generic;
            }

            var problems = new List<LevelGridProblemV2>(generic.Problems);
            for (int index = 0; index < connections.Count; index++)
            {
                LevelGridConnectionRecordV2 connection = connections[index];
                if (connection == null
                    || !TouchesExactFinalExit(
                        connection,
                        finalExitRoomId,
                        finalExitDoorId))
                {
                    continue;
                }

                problems.Add(new LevelGridProblemV2(
                    LevelDesignValidationSeverity.Error,
                    LevelGridProblemCodeV2.DoorUsedByMultipleConnections,
                    connection.ConnectionId,
                    connection.DiagnosticLocation,
                    "Connection '" + connection.ConnectionId
                        + "' uses configured final-exit endpoint '"
                        + finalExitRoomId + "::" + finalExitDoorId
                        + "'. The exact final-exit door cannot also link two authored rooms."));
            }

            return new LevelGridValidationResultV2(purpose, problems);
        }

        private static bool TouchesExactFinalExit(
            LevelGridConnectionRecordV2 connection,
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
