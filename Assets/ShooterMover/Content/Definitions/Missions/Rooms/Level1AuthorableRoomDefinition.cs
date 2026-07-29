using System.ComponentModel;
using ShooterMover.Contracts.Missions.Rooms;
using ShooterMover.Domain.Common;

namespace ShooterMover.Content.Definitions.Missions.Rooms
{
    /// <summary>
    /// Compatibility alias retained for callers compiled against ROOM-LIVE-001.
    /// New production code should use <see cref="Level1LiveRoomGraphDefinition"/>.
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static class Level1AuthorableRoomDefinition
    {
        public static StableId LayoutStableId =>
            Level1LiveRoomGraphDefinition.LayoutStableId;
        public static StableId EntryRoomStableId =>
            Level1LiveRoomGraphDefinition.EntryRoomStableId;
        public static StableId TerminalRoomStableId =>
            Level1LiveRoomGraphDefinition.TerminalRoomStableId;

        public static StableId EntrySpawnStableId =>
            Level1LiveRoomGraphDefinition.EntrySpawnStableId;
        public static StableId TerminalSpawnStableId =>
            Level1LiveRoomGraphDefinition.TerminalSpawnStableId;

        public static StableId MovingDroidInstanceStableId =>
            Level1LiveRoomGraphDefinition.MovingDroidInstanceStableId;
        public static StableId TurretInstanceStableId =>
            Level1LiveRoomGraphDefinition.TurretInstanceStableId;
        public static StableId CoverPropInstanceStableId =>
            Level1LiveRoomGraphDefinition.CoverPropInstanceStableId;

        public static StableId ForwardExitStableId =>
            Level1LiveRoomGraphDefinition.ForwardExitStableId;
        public static StableId ReturnExitStableId =>
            Level1LiveRoomGraphDefinition.ReturnExitStableId;
        public static StableId FinalExitStableId =>
            Level1LiveRoomGraphDefinition.FinalExitStableId;

        public static StableId ForwardDoorStableId =>
            Level1LiveRoomGraphDefinition.ForwardDoorStableId;
        public static StableId ReturnDoorStableId =>
            Level1LiveRoomGraphDefinition.ReturnDoorStableId;
        public static StableId FinalDoorStableId =>
            Level1LiveRoomGraphDefinition.FinalDoorStableId;

        public static StableId EntryClearConditionStableId =>
            Level1LiveRoomGraphDefinition.EntryClearConditionStableId;
        public static StableId TerminalEnteredConditionStableId =>
            Level1LiveRoomGraphDefinition.TerminalEnteredConditionStableId;
        public static StableId TerminalClearConditionStableId =>
            Level1LiveRoomGraphDefinition.TerminalClearConditionStableId;

        public static StableId MovingDroidPresentationStableId =>
            Level1LiveRoomGraphDefinition.MovingDroidPresentationStableId;
        public static StableId TurretPresentationStableId =>
            Level1LiveRoomGraphDefinition.TurretPresentationStableId;
        public static StableId CoverPresentationStableId =>
            Level1LiveRoomGraphDefinition.CoverPresentationStableId;
        public static StableId DoorPresentationStableId =>
            Level1LiveRoomGraphDefinition.DoorPresentationStableId;

        public static AuthorableRoomGraphDefinition Create()
        {
            return Level1LiveRoomGraphDefinition.Create();
        }
    }
}
