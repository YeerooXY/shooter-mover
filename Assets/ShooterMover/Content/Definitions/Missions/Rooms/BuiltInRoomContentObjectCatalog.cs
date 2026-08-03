using ShooterMover.Application.Missions.Rooms.Content;
using ShooterMover.Domain.Common;

namespace ShooterMover.Content.Definitions.Missions.Rooms
{
    /// <summary>
    /// Maps concise room-authoring object IDs to existing runtime and presentation IDs.
    /// Enemy definitions are intentionally absent until one authoritative enemy-content
    /// workflow replaces the deleted duplicate catalogues.
    /// </summary>
    public static class BuiltInRoomContentObjectCatalog
    {
        public static RoomContentObjectCatalog Create()
        {
            return new RoomContentObjectCatalog(
                new[]
                {
                    Definition(
                        "prop.level1-cover",
                        RoomContentObjectKind.Prop,
                        "prop.level1-cover",
                        "presentation.prop-level1-cover"),
                    Definition(
                        "prop.wall-1x1",
                        RoomContentObjectKind.Prop,
                        "prop.wall-1x1",
                        "presentation.prop-wall-1x1"),
                    Definition(
                        "prop.wall-2x2",
                        RoomContentObjectKind.Prop,
                        "prop.wall-2x2",
                        "presentation.prop-wall-2x2"),
                    Definition(
                        "door.room-standard",
                        RoomContentObjectKind.Door,
                        "environment.room-door",
                        "presentation.environment-room-door"),
                    Definition(
                        "tile.floor-industrial",
                        RoomContentObjectKind.Tile,
                        "tile.floor-industrial",
                        "presentation.environment-floor-industrial"),
                });
        }

        private static RoomContentObjectDefinition Definition(
            string objectId,
            RoomContentObjectKind kind,
            string runtimeDefinitionId,
            string presentationId)
        {
            return new RoomContentObjectDefinition(
                StableId.Parse(objectId),
                kind,
                StableId.Parse(runtimeDefinitionId),
                StableId.Parse(presentationId));
        }
    }
}
