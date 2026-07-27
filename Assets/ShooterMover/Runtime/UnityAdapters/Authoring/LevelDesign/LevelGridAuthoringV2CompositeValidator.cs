using System;
using System.Collections.Generic;
using UnityEngine;

namespace ShooterMover.UnityAdapters.Authoring.LevelDesign
{
    /// <summary>
    /// Production-facing validation entry point. The original endpoint validator remains
    /// focused on doors and links; this facade adds room identity and grid-footprint
    /// invariants before returning one combined problem set.
    /// </summary>
    public static class LevelGridAuthoringV2CompositeValidator
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

            List<LevelGridProblemV2> problems =
                new List<LevelGridProblemV2>();
            ValidateRooms(rooms, problems);

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

            return new LevelGridValidationResultV2(purpose, problems);
        }

        private static void ValidateRooms(
            IReadOnlyList<LevelRoomRecord> rooms,
            ICollection<LevelGridProblemV2> problems)
        {
            Dictionary<string, List<LevelRoomRecord>> roomsById =
                new Dictionary<string, List<LevelRoomRecord>>(
                    StringComparer.Ordinal);

            for (int index = 0; index < rooms.Count; index++)
            {
                LevelRoomRecord room = rooms[index];
                if (room == null)
                {
                    Add(
                        problems,
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
                        LevelGridProblemCodeV2.InvalidRoomIdentity,
                        room.RoomId,
                        room.DiagnosticLocation,
                        "Every room requires a canonical stable ID.");
                }

                string key = room.RoomId ?? string.Empty;
                List<LevelRoomRecord> sameId;
                if (!roomsById.TryGetValue(key, out sameId))
                {
                    sameId = new List<LevelRoomRecord>();
                    roomsById.Add(key, sameId);
                }
                sameId.Add(room);
            }

            foreach (KeyValuePair<string, List<LevelRoomRecord>> pair in roomsById)
            {
                if (pair.Value.Count < 2 || string.IsNullOrEmpty(pair.Key))
                {
                    continue;
                }

                for (int index = 0; index < pair.Value.Count; index++)
                {
                    LevelRoomRecord room = pair.Value[index];
                    Add(
                        problems,
                        LevelGridProblemCodeV2.DuplicateRoomIdentity,
                        pair.Key,
                        room.DiagnosticLocation,
                        "Room stable ID is used by " + pair.Value.Count
                            + " rooms. Moving a room must not create a new identity, "
                            + "but duplicated rooms require a new stable ID.");
                }
            }

            ValidateGridFootprints(rooms, problems);
        }

        private static void ValidateGridFootprints(
            IReadOnlyList<LevelRoomRecord> rooms,
            ICollection<LevelGridProblemV2> problems)
        {
            for (int leftIndex = 0; leftIndex < rooms.Count; leftIndex++)
            {
                LevelRoomRecord left = rooms[leftIndex];
                if (!CanBuildFootprint(left))
                {
                    continue;
                }

                RectInt leftFootprint = BuildFootprint(left);
                for (int rightIndex = leftIndex + 1;
                    rightIndex < rooms.Count;
                    rightIndex++)
                {
                    LevelRoomRecord right = rooms[rightIndex];
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
                        LevelGridProblemCodeV2.OverlappingRoomGridFootprint,
                        left.RoomId,
                        left.DiagnosticLocation,
                        "Room grid footprint overlaps room '" + right.RoomId
                            + "'. Move one room or change its footprint.");
                    Add(
                        problems,
                        LevelGridProblemCodeV2.OverlappingRoomGridFootprint,
                        right.RoomId,
                        right.DiagnosticLocation,
                        "Room grid footprint overlaps room '" + left.RoomId
                            + "'. Move one room or change its footprint.");
                }
            }
        }

        private static bool CanBuildFootprint(LevelRoomRecord room)
        {
            return room != null
                && room.FootprintCells.x > 0
                && room.FootprintCells.y > 0;
        }

        private static RectInt BuildFootprint(LevelRoomRecord room)
        {
            return new RectInt(
                room.GridCoordinate,
                room.FootprintCells);
        }

        private static void Add(
            ICollection<LevelGridProblemV2> problems,
            LevelGridProblemCodeV2 code,
            string authoredId,
            string diagnosticLocation,
            string message)
        {
            problems.Add(new LevelGridProblemV2(
                LevelDesignValidationSeverity.Error,
                code,
                authoredId,
                diagnosticLocation,
                message));
        }
    }
}
