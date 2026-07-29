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
    public static class Level1LiveRoomGraphDefinition
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

        public static AuthorableRoomGraphDefinition Create()
        {
            var entryRoom = new AuthorableRoomDefinition(
                EntryRoomStableId,
                0,
                "DROID APPROACH",
                new RoomBounds(
                    new RoomVector2(0d, 0d),
                    new RoomVector2(24d, 14d)),
                new[]
                {
                    new RoomSpawnPointDefinition(
                        EntrySpawnStableId,
                        RoomSpawnPointKind.ForwardEntry,
                        new RoomVector2(-10d, 0d),
                        0d),
                },
                new[]
                {
                    new RoomPlacedEntityDefinition(
                        MovingDroidInstanceStableId,
                        RoomLivePlacementKind.Enemy,
                        StableId.Parse("enemy.mobile-blaster-droid"),
                        MovingDroidPresentationStableId,
                        RoomOccupantClearRole.RequiredEnemy,
                        new RoomVector2(4d, 0d),
                        180d),
                    new RoomPlacedEntityDefinition(
                        CoverPropInstanceStableId,
                        RoomLivePlacementKind.Prop,
                        StableId.Parse("prop.level1-cover"),
                        CoverPresentationStableId,
                        RoomOccupantClearRole.NonParticipant,
                        new RoomVector2(0d, -3d),
                        0d),
                },
                new[]
                {
                    new RoomDoorDefinition(
                        ForwardDoorStableId,
                        DoorPresentationStableId,
                        ForwardExitStableId,
                        new[] { EntryClearConditionStableId },
                        new RoomVector2(11d, 0d),
                        0d),
                },
                new[]
                {
                    new RoomExitLinkDefinition(
                        ForwardExitStableId,
                        ForwardDoorStableId,
                        RoomLiveLinkKind.Room,
                        RoomExitType.Progression,
                        TerminalRoomStableId,
                        TerminalSpawnStableId),
                },
                new[]
                {
                    new RoomCompletionConditionDefinition(
                        EntryClearConditionStableId,
                        RoomCompletionConditionKind.AllBlockingOccupantsTerminal,
                        null,
                        true),
                });

            var terminalRoom = new AuthorableRoomDefinition(
                TerminalRoomStableId,
                1,
                "TURRET TERMINAL",
                new RoomBounds(
                    new RoomVector2(0d, 0d),
                    new RoomVector2(24d, 14d)),
                new[]
                {
                    new RoomSpawnPointDefinition(
                        TerminalSpawnStableId,
                        RoomSpawnPointKind.ForwardEntry,
                        new RoomVector2(-10d, 0d),
                        0d),
                },
                new[]
                {
                    new RoomPlacedEntityDefinition(
                        TurretInstanceStableId,
                        RoomLivePlacementKind.Enemy,
                        StableId.Parse("enemy.blaster-turret"),
                        TurretPresentationStableId,
                        RoomOccupantClearRole.RequiredEnemy,
                        new RoomVector2(4d, 0d),
                        180d),
                },
                new[]
                {
                    new RoomDoorDefinition(
                        ReturnDoorStableId,
                        DoorPresentationStableId,
                        ReturnExitStableId,
                        new[] { TerminalEnteredConditionStableId },
                        new RoomVector2(-11d, -3d),
                        180d),
                    new RoomDoorDefinition(
                        FinalDoorStableId,
                        DoorPresentationStableId,
                        FinalExitStableId,
                        new[] { TerminalClearConditionStableId },
                        new RoomVector2(11d, 3d),
                        0d),
                },
                new[]
                {
                    new RoomExitLinkDefinition(
                        ReturnExitStableId,
                        ReturnDoorStableId,
                        RoomLiveLinkKind.Room,
                        RoomExitType.Return,
                        EntryRoomStableId,
                        EntrySpawnStableId),
                    new RoomExitLinkDefinition(
                        FinalExitStableId,
                        FinalDoorStableId,
                        RoomLiveLinkKind.FinalExit,
                        RoomExitType.Progression,
                        null,
                        null),
                },
                new[]
                {
                    new RoomCompletionConditionDefinition(
                        TerminalEnteredConditionStableId,
                        RoomCompletionConditionKind.AlwaysSatisfied,
                        null,
                        false),
                    new RoomCompletionConditionDefinition(
                        TerminalClearConditionStableId,
                        RoomCompletionConditionKind.AllBlockingOccupantsTerminal,
                        null,
                        true),
                });

            return new AuthorableRoomGraphDefinition(
                LayoutStableId,
                EntryRoomStableId,
                TerminalRoomStableId,
                new[] { entryRoom, terminalRoom });
        }
    }
}
