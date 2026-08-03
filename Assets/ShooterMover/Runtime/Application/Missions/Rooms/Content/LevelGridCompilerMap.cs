using System;
using System.Globalization;

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
                return json.Insert(objectEnd, mapPosition);
            }
        }
    }
}
