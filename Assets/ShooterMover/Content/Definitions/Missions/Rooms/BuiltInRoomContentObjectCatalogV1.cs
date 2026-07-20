using ShooterMover.Application.Missions.Rooms.Content;
using ShooterMover.Domain.Common;

namespace ShooterMover.Content.Definitions.Missions.Rooms
{
    /// <summary>
    /// Maps concise room-authoring object IDs to existing runtime and presentation IDs.
    /// Combat, XP, drops, kill statistics, and scaling remain owned by their respective
    /// enemy/content authorities rather than by room placement JSON.
    /// </summary>
    public static class BuiltInRoomContentObjectCatalogV1
    {
        public static RoomContentObjectCatalogV1 Create()
        {
            return new RoomContentObjectCatalogV1(
                new[]
                {
                    Definition(
                        "enemy.moving-droid",
                        RoomContentObjectKindV1.Enemy,
                        "enemy.mobile-blaster-droid",
                        "presentation.enemy-mobile-blaster-droid"),
                    Definition(
                        "enemy.blaster-turret",
                        RoomContentObjectKindV1.Enemy,
                        "enemy.blaster-turret",
                        "presentation.enemy-blaster-turret"),
                    Definition(
                        "prop.level1-cover",
                        RoomContentObjectKindV1.Prop,
                        "prop.level1-cover",
                        "presentation.prop-level1-cover"),
                    Definition(
                        "door.room-standard",
                        RoomContentObjectKindV1.Door,
                        "environment.room-door",
                        "presentation.environment-room-door"),
                    Definition(
                        "tile.floor-industrial",
                        RoomContentObjectKindV1.Tile,
                        "tile.floor-industrial",
                        "presentation.environment-floor-industrial"),
                });
        }

        private static RoomContentObjectDefinitionV1 Definition(
            string objectId,
            RoomContentObjectKindV1 kind,
            string runtimeDefinitionId,
            string presentationId)
        {
            return new RoomContentObjectDefinitionV1(
                StableId.Parse(objectId),
                kind,
                StableId.Parse(runtimeDefinitionId),
                StableId.Parse(presentationId));
        }
    }
}
