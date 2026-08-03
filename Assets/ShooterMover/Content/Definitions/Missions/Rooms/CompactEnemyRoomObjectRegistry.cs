using ShooterMover.Application.Missions.Rooms.Content;
using ShooterMover.Domain.Common;

namespace ShooterMover.Content.Definitions.Missions.Rooms
{
    // Generated from validated Content/Enemies files by tools/enemy-maker/runtime-export.js.
    public static class CompactEnemyRoomObjectRegistry
    {
        public const string PresentationStableId = "presentation.enemy-compact";

        public static RoomContentObjectDefinition[] Create()
        {
            return new[]
            {
                Definition("gunner-droid")
            };
        }

        private static RoomContentObjectDefinition Definition(string enemyId)
        {
            return new RoomContentObjectDefinition(
                StableId.Parse("enemy." + enemyId),
                RoomContentObjectKind.Enemy,
                StableId.Parse("enemy." + enemyId),
                StableId.Parse(PresentationStableId));
        }
    }
}
