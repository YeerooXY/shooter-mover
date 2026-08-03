using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.Serialization;

namespace ShooterMover.Application.Missions.Rooms.Content
{
    public static partial class LevelGridCompiler
    {
        private sealed partial class Compiler
        {
            private string Serialize(RoomContentLayoutDto layout)
            {
                if (layout == null)
                {
                    throw new ArgumentNullException(nameof(layout));
                }

                RoomSource room;
                if (!rooms.TryGetValue(layout.Room, out room) || room == null)
                {
                    throw Error(
                        "level-level-1-map-room-missing",
                        "$.level.rooms",
                        "The compiled room layout has no matching map room: "
                            + layout.Room);
                }

                int[] position = RequireVector(
                    room.Entry.GridPosition,
                    room.Root + "room.json.grid_position");
                List<TeleporterDto> teleporters = ReadTeleporters(room);
                string json = Serialize<RoomContentLayoutDto>(layout);
                int objectEnd = json.LastIndexOf('}');
                if (objectEnd < 0)
                {
                    throw Error(
                        "level-level-1-map-layout-invalid",
                        room.Root + "room.json",
                        "The compiled room layout is not a JSON object.");
                }

                string mapPosition = ",\"grid_position\":["
                    + position[0].ToString(CultureInfo.InvariantCulture)
                    + ","
                    + position[1].ToString(CultureInfo.InvariantCulture)
                    + "]";
                string teleporterJson = Serialize(new TeleporterEnvelopeDto
                {
                    Teleporters = teleporters,
                });
                string teleporterField = teleporterJson.Length <= 2
                    ? ",\"teleporters\":[]"
                    : "," + teleporterJson.Substring(1, teleporterJson.Length - 2);
                return json.Insert(objectEnd, mapPosition + teleporterField);
            }

            private List<TeleporterDto> ReadTeleporters(RoomSource room)
            {
                TeleporterRoomDto source = ReadRequired<TeleporterRoomDto>(
                    room.Root + "room.json",
                    "$documents[\"" + room.Root + "room.json\"]");
                List<TeleporterDto> values = source.Teleporters
                    ?? new List<TeleporterDto>();
                var ids = new HashSet<string>(StringComparer.Ordinal);
                for (int index = 0; index < values.Count; index++)
                {
                    string path = room.Root
                        + "room.json.teleporters["
                        + index
                        + "]";
                    TeleporterDto teleporter = Require(values[index], path);
                    teleporter.Id = RequireText(teleporter.Id, path + ".id");
                    if (!ids.Add(teleporter.Id))
                    {
                        throw Error(
                            "level-teleporter-id-duplicate",
                            path + ".id",
                            "Duplicate teleporter ID in room: " + teleporter.Id);
                    }
                    teleporter.Position = RequireFiniteVector(
                        teleporter.Position,
                        path + ".position");
                    RequireFinite(teleporter.Rotation, path + ".rotation");
                    if (!string.Equals(
                        RequireText(
                            teleporter.UnlockWhen,
                            path + ".unlock_when"),
                        "room-complete",
                        StringComparison.Ordinal))
                    {
                        throw Error(
                            "level-teleporter-unlock-unsupported",
                            path + ".unlock_when",
                            "Teleporters currently unlock only when their room is complete.");
                    }
                    ValidateTeleporterPosition(room, teleporter, path);
                }
                return values;
            }

            private static void ValidateTeleporterPosition(
                RoomSource room,
                TeleporterDto teleporter,
                string path)
            {
                double halfWidth = room.Size[0] * 0.5d;
                double halfHeight = room.Size[1] * 0.5d;
                double minX = room.Center[0] - halfWidth;
                double maxX = room.Center[0] + halfWidth;
                double minY = room.Center[1] - halfHeight;
                double maxY = room.Center[1] + halfHeight;
                if (teleporter.Position[0] < minX
                    || teleporter.Position[0] > maxX
                    || teleporter.Position[1] < minY
                    || teleporter.Position[1] > maxY)
                {
                    throw Error(
                        "level-teleporter-outside-room",
                        path + ".position",
                        "A teleporter must be placed inside its room bounds.");
                }
            }
        }

        [DataContract]
        private sealed class TeleporterRoomDto
        {
            [DataMember(Name = "teleporters", EmitDefaultValue = false)]
            public List<TeleporterDto> Teleporters { get; set; }
        }

        [DataContract]
        private sealed class TeleporterDto
        {
            [DataMember(Name = "id", IsRequired = true)]
            public string Id { get; set; }

            [DataMember(Name = "position", IsRequired = true)]
            public double[] Position { get; set; }

            [DataMember(Name = "rotation", IsRequired = true)]
            public double Rotation { get; set; }

            [DataMember(Name = "enabled", IsRequired = true)]
            public bool Enabled { get; set; }

            [DataMember(Name = "unlock_when", IsRequired = true)]
            public string UnlockWhen { get; set; }
        }

        [DataContract]
        private sealed class TeleporterEnvelopeDto
        {
            [DataMember(Name = "teleporters", IsRequired = true)]
            public List<TeleporterDto> Teleporters { get; set; }
        }
    }
}
