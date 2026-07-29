using System;
using ShooterMover.Contracts.Missions.Rooms;
using ShooterMover.Domain.Common;

namespace ShooterMover.Application.Missions.Rooms
{
    internal sealed class RoomTraversalResult
    {
        public RoomTraversalResult(
            bool applied,
            string rejectionCode,
            StableId targetRoomStableId,
            StableId targetSpawnPointStableId)
        {
            Applied = applied;
            RejectionCode = rejectionCode ?? string.Empty;
            TargetRoomStableId = targetRoomStableId;
            TargetSpawnPointStableId = targetSpawnPointStableId;
        }

        public bool Applied { get; }

        public string RejectionCode { get; }

        public StableId TargetRoomStableId { get; }

        public StableId TargetSpawnPointStableId { get; }
    }

    /// <summary>
    /// Small coordinated mutation boundary for ROOM-001 traversal and ROOM-RUNTIME-001
    /// activation/restart. The contained mutable authorities are intentionally internal.
    /// </summary>
    internal sealed class RoomTraversalFlow
    {
        private readonly StableId runtimeInstanceStableId;
        private readonly RoomOccupancyState occupancyAuthority;
        private readonly RoomMissionLayout missionLayout;

        public RoomTraversalFlow(
            StableId runtimeInstanceStableId,
            AuthorableRoomGraphDefinition definition)
        {
            this.runtimeInstanceStableId = runtimeInstanceStableId
                ?? throw new ArgumentNullException(nameof(runtimeInstanceStableId));
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            occupancyAuthority = new RoomOccupancyState(
                runtimeInstanceStableId,
                definition.RoomGraphDefinition);
            missionLayout = new RoomMissionLayout(definition.RoomGraphDefinition);
        }

        internal RoomOccupancyState OccupancyAuthority
        {
            get { return occupancyAuthority; }
        }

        internal RoomMissionLayout MissionLayout
        {
            get { return missionLayout; }
        }

        public RoomTraversalResult Traverse(
            RoomExitLinkDefinition exit,
            StableId occupancyOperationStableId)
        {
            if (exit == null) throw new ArgumentNullException(nameof(exit));
            if (exit.LinkKind != RoomLiveLinkKind.Room)
            {
                throw new ArgumentException(
                    "Room traversal coordinator accepts only room links.",
                    nameof(exit));
            }

            if (!missionLayout.GetExitState(exit.ExitStableId).IsAvailable)
            {
                return new RoomTraversalResult(
                    false,
                    "room-live-exit-locked",
                    null,
                    null);
            }

            RoomGraphOperationResult traversal = missionLayout.Traverse(
                exit.ExitStableId);
            if (traversal.Status != RoomGraphOperationStatus.Applied)
            {
                return new RoomTraversalResult(
                    false,
                    traversal.RejectionCode,
                    null,
                    null);
            }

            RoomLiveOperationResult activation = occupancyAuthority.ActivateRoom(
                new ActivateRoomCommand(
                    runtimeInstanceStableId,
                    occupancyOperationStableId,
                    occupancyAuthority.CurrentProjection.LifecycleGeneration,
                    exit.TargetRoomStableId));
            if (activation.Status != RoomLiveOperationStatus.Applied
                && activation.Status != RoomLiveOperationStatus.NoChange)
            {
                throw new InvalidOperationException(
                    "Room layout traversal and occupancy activation diverged: "
                    + activation.RejectionCode);
            }

            return new RoomTraversalResult(
                true,
                string.Empty,
                exit.TargetRoomStableId,
                exit.TargetSpawnPointStableId);
        }

        public RoomLiveOperationResult Restart(
            StableId occupancyOperationStableId)
        {
            RoomLiveOperationResult occupancy = occupancyAuthority.Restart(
                new RestartRoomLiveCommand(
                    runtimeInstanceStableId,
                    occupancyOperationStableId,
                    occupancyAuthority.CurrentProjection.LifecycleGeneration));
            if (occupancy.Status != RoomLiveOperationStatus.Rejected)
            {
                missionLayout.Restart();
            }

            return occupancy;
        }

        public bool CompleteCurrentRoom(StableId roomStableId)
        {
            RoomLiveState state = missionLayout.GetRoomState(roomStableId);
            if (!state.IsCurrent || state.IsCompleted)
            {
                return false;
            }

            RoomGraphOperationResult result = missionLayout.CompleteCurrentRoom();
            return result.Status == RoomGraphOperationStatus.Applied;
        }
    }
}
