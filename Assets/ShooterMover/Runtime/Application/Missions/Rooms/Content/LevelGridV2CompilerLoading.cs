using System;
using System.Collections.Generic;

namespace ShooterMover.Application.Missions.Rooms.Content
{
    public static partial class LevelGridV2Compiler
    {
        private sealed partial class Compiler
        {
            private void LoadDoors(RoomSource room, DoorsDto roomDoors)
            {
                List<DoorDto> values = RequireList(roomDoors.Doors, room.Root + "doors.json.doors");
                for (int i = 0; i < values.Count; i++)
                {
                    string path = room.Root + "doors.json.doors[" + i + "]";
                    DoorDto value = Require(values[i], path);
                    string doorId = RequireText(value.DoorId, path + ".door_id");
                    if (doors.ContainsKey(doorId))
                    {
                        throw Error("level-grid-v2-door-id-duplicate", path + ".door_id", "Door stable IDs must be unique across the level: " + doorId);
                    }
                    string side = RequireSide(value.Side, path + ".side");
                    double[] local = RequireFiniteVector(value.CurrentLocalPosition, path + ".current_local_position");
                    if (value.Traversable)
                    {
                        RequireText(value.RuntimeObject, path + ".runtime_object");
                    }
                    var door = new DoorSource(room, value, doorId, side, local);
                    doors.Add(doorId, door);
                    room.Doors.Add(door);
                }
            }

            private EncounterDto ReadEncounterOrDefault(string path, string roomId)
            {
                string json;
                if (!source.TryGet(path, out json) || string.IsNullOrWhiteSpace(json))
                {
                    return DefaultEncounter(roomId);
                }
                EncounterDto encounter = Deserialize<EncounterDto>(json, "$documents[\"" + path + "\"]");
                bool empty = string.IsNullOrWhiteSpace(encounter.Room)
                    && string.IsNullOrWhiteSpace(encounter.Completion)
                    && encounter.OptionalEnemyIds == null
                    && encounter.DoorRules == null;
                if (empty) return DefaultEncounter(roomId);
                if (encounter.SchemaVersion != 0)
                {
                    RequireVersion(encounter.SchemaVersion, path + ".schema_version");
                }
                ValidateSidecarRoom(roomId, encounter.Room, path + ".room");
                encounter.Completion = RequireText(encounter.Completion, path + ".completion");
                encounter.OptionalEnemyIds = encounter.OptionalEnemyIds ?? new List<string>();
                encounter.DoorRules = encounter.DoorRules ?? new List<DoorRuleDto>();
                return encounter;
            }

            private static EncounterDto DefaultEncounter(string roomId)
            {
                return new EncounterDto
                {
                    Room = roomId,
                    Completion = "all-enemies",
                    OptionalEnemyIds = new List<string>(),
                    DoorRules = new List<DoorRuleDto>(),
                };
            }

        }
    }
}
