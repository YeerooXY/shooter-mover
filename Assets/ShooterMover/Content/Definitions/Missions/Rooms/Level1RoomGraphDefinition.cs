using System;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Missions.Rooms;

namespace ShooterMover.Content.Definitions.Missions.Rooms
{
    /// <summary>
    /// Deterministic two-room vertical-slice layout. This is authored data only;
    /// it does not load scenes, place doors, or own runtime mission state.
    /// </summary>
    public static class Level1RoomGraphDefinition
    {
        public static readonly StableId LayoutStableId =
            StableId.Parse("layout.level1-two-room");

        public static readonly StableId EntryRoomStableId =
            StableId.Parse("room.level1-entry");

        public static readonly StableId TerminalRoomStableId =
            StableId.Parse("room.level1-terminal");

        public static readonly StableId EntryRoomEntryStableId =
            StableId.Parse("entry.level1-entry-main");

        public static readonly StableId TerminalRoomEntryStableId =
            StableId.Parse("entry.level1-terminal-main");

        public static readonly StableId ForwardExitStableId =
            StableId.Parse("exit.level1-entry-to-terminal");

        public static readonly StableId ReturnExitStableId =
            StableId.Parse("exit.level1-terminal-to-entry");

        public static readonly StableId ConnectionStableId =
            StableId.Parse("connection.level1-entry-terminal");

        public static readonly StableId DoorLinkStableId =
            StableId.Parse("door-link.level1-entry-terminal");

        public static RoomGraphDefinition Create()
        {
            var rooms = new[]
            {
                new RoomDefinition(
                    EntryRoomStableId,
                    0,
                    RoomInitialAvailability.Available,
                    true),
                new RoomDefinition(
                    TerminalRoomStableId,
                    1,
                    RoomInitialAvailability.Locked,
                    true),
            };

            var entries = new[]
            {
                new RoomEntryDefinition(
                    EntryRoomEntryStableId,
                    EntryRoomStableId,
                    0),
                new RoomEntryDefinition(
                    TerminalRoomEntryStableId,
                    TerminalRoomStableId,
                    0),
            };

            var forward = new RoomExitDefinition(
                ForwardExitStableId,
                EntryRoomStableId,
                TerminalRoomEntryStableId,
                0,
                RoomExitType.Progression,
                true,
                EntryRoomStableId);
            var reverse = new RoomExitDefinition(
                ReturnExitStableId,
                TerminalRoomStableId,
                EntryRoomEntryStableId,
                0,
                RoomExitType.Return,
                true,
                EntryRoomStableId);

            var connections = new[]
            {
                new RoomConnectionDefinition(
                    ConnectionStableId,
                    RoomConnectionDirectionality.Bidirectional,
                    DoorLinkStableId,
                    new[] { reverse, forward }),
            };

            var doorLinks = new[]
            {
                new RoomDoorLinkDefinition(DoorLinkStableId),
            };

            RoomGraphValidationResult result =
                RoomGraphDefinition.ValidateAndCreate(
                    LayoutStableId,
                    EntryRoomStableId,
                    TerminalRoomStableId,
                    rooms,
                    entries,
                    connections,
                    doorLinks);
            if (!result.IsValid)
            {
                throw new InvalidOperationException(
                    "The built-in Level 1 room graph is invalid: "
                    + DescribeIssues(result));
            }

            return result.Definition;
        }

        private static string DescribeIssues(RoomGraphValidationResult result)
        {
            if (result == null || result.Issues.Count == 0)
            {
                return "unknown validation failure";
            }

            var messages = new string[result.Issues.Count];
            for (int index = 0; index < result.Issues.Count; index++)
            {
                RoomGraphValidationIssue issue = result.Issues[index];
                messages[index] = issue.Code + "[" + issue.Subject + "]: " + issue.Message;
            }

            return string.Join("; ", messages);
        }
    }
}
