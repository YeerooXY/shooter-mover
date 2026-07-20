using ShooterMover.Contracts.Missions.Rooms;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Missions.Rooms;

namespace ShooterMover.Content.Definitions.Missions.Rooms
{
    /// <summary>
    /// Level 1 live room graph: Room 1 has a moving droid, Room 2 has a turret,
    /// the Room 2 return door is available on entry, and the final exit is independently
    /// gated by the Room 2 clear condition.
    /// </summary>
    public static class Level1LiveRoomGraphDefinitionV1
    {
        public static readonly StableId LayoutStableId =
            StableId.Parse("layout.level1-authorable-two-room");
        public static readonly StableId EntryRoomStableId =
            StableId.Parse("room.level1-entry");
        public static readonly StableId TerminalRoomStableId =
            StableId.Parse("room.level1-terminal");

        public static readonly StableId EntrySpawnStableId =
            StableId.Parse("entry.level1-entry-main");
        public static readonly StableId TerminalSpawnStableId =
            StableId.Parse("entry.level1-terminal-main");

        public static readonly StableId MovingDroidInstanceStableId =
            StableId.Parse("enemy-instance.level1-room1-moving-droid");
        public static readonly StableId TurretInstanceStableId =
            StableId.Parse("enemy-instance.level1-room2-blaster-turret");
        public static readonly StableId CoverPropInstanceStableId =
            StableId.Parse("prop-instance.level1-room1-cover");

        public static readonly StableId ForwardExitStableId =
            StableId.Parse("exit.level1-entry-to-terminal");
        public static readonly StableId ReturnExitStableId =
            StableId.Parse("exit.level1-terminal-to-entry");
        public static readonly StableId FinalExitStableId =
            StableId.Parse("exit.level1-terminal-final");

        public static readonly StableId ForwardDoorStableId =
            StableId.Parse("door-instance.level1-entry-forward");
        public static readonly StableId ReturnDoorStableId =
            StableId.Parse("door-instance.level1-terminal-return");
        public static readonly StableId FinalDoorStableId =
            StableId.Parse("door-instance.level1-terminal-final");

        public static readonly StableId EntryClearConditionStableId =
            StableId.Parse("completion.level1-entry-clear");
        public static readonly StableId TerminalEnteredConditionStableId =
            StableId.Parse("completion.level1-terminal-entered");
        public static readonly StableId TerminalClearConditionStableId =
            StableId.Parse("completion.level1-terminal-clear");

        public static readonly StableId MovingDroidPresentationStableId =
            StableId.Parse("presentation.enemy-mobile-blaster-droid");
        public static readonly StableId TurretPresentationStableId =
            StableId.Parse("presentation.enemy-blaster-turret");
        public static readonly StableId CoverPresentationStableId =
            StableId.Parse("presentation.prop-level1-cover");
        public static readonly StableId DoorPresentationStableId =
            StableId.Parse("presentation.environment-room-door");

        public static AuthorableRoomGraphDefinitionV1 Create()
        {
            var entryRoom = new AuthorableRoomDefinitionV1(
                EntryRoomStableId,
                0,
                "DROID APPROACH",
                new RoomBoundsV1(
                    new RoomVector2V1(0d, 0d),
                    new RoomVector2V1(24d, 14d)),
                new[]
                {
                    new RoomSpawnPointDefinitionV1(
                        EntrySpawnStableId,
                        RoomSpawnPointKindV1.ForwardEntry,
                        new RoomVector2V1(-10d, 0d),
                        0d),
                },
                new[]
                {
                    new RoomPlacedEntityDefinitionV1(
                        MovingDroidInstanceStableId,
                        RoomLivePlacementKindV1.Enemy,
                        StableId.Parse("enemy.mobile-blaster-droid"),
                        MovingDroidPresentationStableId,
                        RoomOccupantClearRoleV1.RequiredEnemy,
                        new RoomVector2V1(4d, 0d),
                        180d),
                    new RoomPlacedEntityDefinitionV1(
                        CoverPropInstanceStableId,
                        RoomLivePlacementKindV1.Prop,
                        StableId.Parse("prop.level1-cover"),
                        CoverPresentationStableId,
                        RoomOccupantClearRoleV1.NonParticipant,
                        new RoomVector2V1(0d, -3d),
                        0d),
                },
                new[]
                {
                    new RoomDoorDefinitionV1(
                        ForwardDoorStableId,
                        DoorPresentationStableId,
                        ForwardExitStableId,
                        new[] { EntryClearConditionStableId },
                        new RoomVector2V1(11d, 0d),
                        0d),
                },
                new[]
                {
                    new RoomExitLinkDefinitionV1(
                        ForwardExitStableId,
                        ForwardDoorStableId,
                        RoomLiveLinkKindV1.Room,
                        RoomExitTypeV1.Progression,
                        TerminalRoomStableId,
                        TerminalSpawnStableId),
                },
                new[]
                {
                    new RoomCompletionConditionDefinitionV1(
                        EntryClearConditionStableId,
                        RoomCompletionConditionKindV1.AllBlockingOccupantsTerminal,
                        null,
                        true),
                });

            var terminalRoom = new AuthorableRoomDefinitionV1(
                TerminalRoomStableId,
                1,
                "TURRET TERMINAL",
                new RoomBoundsV1(
                    new RoomVector2V1(0d, 0d),
                    new RoomVector2V1(24d, 14d)),
                new[]
                {
                    new RoomSpawnPointDefinitionV1(
                        TerminalSpawnStableId,
                        RoomSpawnPointKindV1.ForwardEntry,
                        new RoomVector2V1(-10d, 0d),
                        0d),
                },
                new[]
                {
                    new RoomPlacedEntityDefinitionV1(
                        TurretInstanceStableId,
                        RoomLivePlacementKindV1.Enemy,
                        StableId.Parse("enemy.blaster-turret"),
                        TurretPresentationStableId,
                        RoomOccupantClearRoleV1.RequiredEnemy,
                        new RoomVector2V1(4d, 0d),
                        180d),
                },
                new[]
                {
                    new RoomDoorDefinitionV1(
                        ReturnDoorStableId,
                        DoorPresentationStableId,
                        ReturnExitStableId,
                        new[] { TerminalEnteredConditionStableId },
                        new RoomVector2V1(-11d, -3d),
                        180d),
                    new RoomDoorDefinitionV1(
                        FinalDoorStableId,
                        DoorPresentationStableId,
                        FinalExitStableId,
                        new[] { TerminalClearConditionStableId },
                        new RoomVector2V1(11d, 3d),
                        0d),
                },
                new[]
                {
                    new RoomExitLinkDefinitionV1(
                        ReturnExitStableId,
                        ReturnDoorStableId,
                        RoomLiveLinkKindV1.Room,
                        RoomExitTypeV1.Return,
                        EntryRoomStableId,
                        EntrySpawnStableId),
                    new RoomExitLinkDefinitionV1(
                        FinalExitStableId,
                        FinalDoorStableId,
                        RoomLiveLinkKindV1.FinalExit,
                        RoomExitTypeV1.Progression,
                        null,
                        null),
                },
                new[]
                {
                    new RoomCompletionConditionDefinitionV1(
                        TerminalEnteredConditionStableId,
                        RoomCompletionConditionKindV1.AlwaysSatisfied,
                        null,
                        false),
                    new RoomCompletionConditionDefinitionV1(
                        TerminalClearConditionStableId,
                        RoomCompletionConditionKindV1.AllBlockingOccupantsTerminal,
                        null,
                        true),
                });

            return new AuthorableRoomGraphDefinitionV1(
                LayoutStableId,
                EntryRoomStableId,
                TerminalRoomStableId,
                new[] { entryRoom, terminalRoom });
        }
    }
}
