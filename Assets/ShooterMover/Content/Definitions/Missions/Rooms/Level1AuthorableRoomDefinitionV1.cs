using System.ComponentModel;
using ShooterMover.Contracts.Missions.Rooms;
using ShooterMover.Domain.Common;

namespace ShooterMover.Content.Definitions.Missions.Rooms
{
    /// <summary>
    /// Compatibility alias retained for callers compiled against ROOM-LIVE-001.
    /// New production code should use <see cref="Level1LiveRoomGraphDefinitionV1"/>.
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static class Level1AuthorableRoomDefinitionV1
    {
        public static StableId LayoutStableId =>
            Level1LiveRoomGraphDefinitionV1.LayoutStableId;
        public static StableId EntryRoomStableId =>
            Level1LiveRoomGraphDefinitionV1.EntryRoomStableId;
        public static StableId TerminalRoomStableId =>
            Level1LiveRoomGraphDefinitionV1.TerminalRoomStableId;

        public static StableId EntrySpawnStableId =>
            Level1LiveRoomGraphDefinitionV1.EntrySpawnStableId;
        public static StableId TerminalSpawnStableId =>
            Level1LiveRoomGraphDefinitionV1.TerminalSpawnStableId;

        public static StableId MovingDroidInstanceStableId =>
            Level1LiveRoomGraphDefinitionV1.MovingDroidInstanceStableId;
        public static StableId TurretInstanceStableId =>
            Level1LiveRoomGraphDefinitionV1.TurretInstanceStableId;
        public static StableId CoverPropInstanceStableId =>
            Level1LiveRoomGraphDefinitionV1.CoverPropInstanceStableId;

        public static StableId ForwardExitStableId =>
            Level1LiveRoomGraphDefinitionV1.ForwardExitStableId;
        public static StableId ReturnExitStableId =>
            Level1LiveRoomGraphDefinitionV1.ReturnExitStableId;
        public static StableId FinalExitStableId =>
            Level1LiveRoomGraphDefinitionV1.FinalExitStableId;

        public static StableId ForwardDoorStableId =>
            Level1LiveRoomGraphDefinitionV1.ForwardDoorStableId;
        public static StableId ReturnDoorStableId =>
            Level1LiveRoomGraphDefinitionV1.ReturnDoorStableId;
        public static StableId FinalDoorStableId =>
            Level1LiveRoomGraphDefinitionV1.FinalDoorStableId;

        public static StableId EntryClearConditionStableId =>
            Level1LiveRoomGraphDefinitionV1.EntryClearConditionStableId;
        public static StableId TerminalEnteredConditionStableId =>
            Level1LiveRoomGraphDefinitionV1.TerminalEnteredConditionStableId;
        public static StableId TerminalClearConditionStableId =>
            Level1LiveRoomGraphDefinitionV1.TerminalClearConditionStableId;

        public static StableId MovingDroidPresentationStableId =>
            Level1LiveRoomGraphDefinitionV1.MovingDroidPresentationStableId;
        public static StableId TurretPresentationStableId =>
            Level1LiveRoomGraphDefinitionV1.TurretPresentationStableId;
        public static StableId CoverPresentationStableId =>
            Level1LiveRoomGraphDefinitionV1.CoverPresentationStableId;
        public static StableId DoorPresentationStableId =>
            Level1LiveRoomGraphDefinitionV1.DoorPresentationStableId;

        public static AuthorableRoomGraphDefinitionV1 Create()
        {
            return Level1LiveRoomGraphDefinitionV1.Create();
        }
    }
}
