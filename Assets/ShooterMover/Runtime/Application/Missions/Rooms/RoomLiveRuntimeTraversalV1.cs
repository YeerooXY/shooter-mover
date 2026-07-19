using System;
using ShooterMover.Contracts.Missions.Rooms;
using ShooterMover.Domain.Common;

namespace ShooterMover.Application.Missions.Rooms
{
    internal sealed class RoomTraversalResultV1
    {
        public RoomTraversalResultV1(
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
    internal sealed class RoomTraversalCoordinatorV1
    {
        private readonly StableId runtimeInstanceStableId;
        private readonly RoomRuntimeAuthorityV1 occupancyAuthority;
        private readonly RoomMissionLayoutV1 missionLayout;

        public RoomTraversalCoordinatorV1(
            StableId runtimeInstanceStableId,
            AuthorableRoomGraphDefinitionV1 definition)
        {
            this.runtimeInstanceStableId = runtimeInstanceStableId
                ?? throw new ArgumentNullException(nameof(runtimeInstanceStableId));
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            occupancyAuthority = new RoomRuntimeAuthorityV1(
                runtimeInstanceStableId,
                definition.RoomGraphDefinition);
            missionLayout = new RoomMissionLayoutV1(definition.RoomGraphDefinition);
        }

        internal RoomRuntimeAuthorityV1 OccupancyAuthority
        {
            get { return occupancyAuthority; }
        }

        internal RoomMissionLayoutV1 MissionLayout
        {
            get { return missionLayout; }
        }

        public RoomTraversalResultV1 Traverse(
            RoomExitLinkDefinitionV1 exit,
            StableId occupancyOperationStableId)
        {
            if (exit == null) throw new ArgumentNullException(nameof(exit));
            if (exit.LinkKind != RoomLiveLinkKindV1.Room)
            {
                throw new ArgumentException(
                    "Room traversal coordinator accepts only room links.",
                    nameof(exit));
            }

            if (!missionLayout.GetExitState(exit.ExitStableId).IsAvailable)
            {
                return new RoomTraversalResultV1(
                    false,
                    "room-live-exit-locked",
                    null,
                    null);
            }

            RoomGraphOperationResultV1 traversal = missionLayout.Traverse(
                exit.ExitStableId);
            if (traversal.Status != RoomGraphOperationStatusV1.Applied)
            {
                return new RoomTraversalResultV1(
                    false,
                    traversal.RejectionCode,
                    null,
                    null);
            }

            RoomRuntimeOperationResultV1 activation = occupancyAuthority.ActivateRoom(
                new ActivateRoomCommandV1(
                    runtimeInstanceStableId,
                    occupancyOperationStableId,
                    occupancyAuthority.CurrentProjection.LifecycleGeneration,
                    exit.TargetRoomStableId));
            if (activation.Status != RoomRuntimeOperationStatusV1.Applied
                && activation.Status != RoomRuntimeOperationStatusV1.NoChange)
            {
                throw new InvalidOperationException(
                    "Room layout traversal and occupancy activation diverged: "
                    + activation.RejectionCode);
            }

            return new RoomTraversalResultV1(
                true,
                string.Empty,
                exit.TargetRoomStableId,
                exit.TargetSpawnPointStableId);
        }

        public RoomRuntimeOperationResultV1 Restart(
            StableId occupancyOperationStableId)
        {
            RoomRuntimeOperationResultV1 occupancy = occupancyAuthority.Restart(
                new RestartRoomRuntimeCommandV1(
                    runtimeInstanceStableId,
                    occupancyOperationStableId,
                    occupancyAuthority.CurrentProjection.LifecycleGeneration));
            if (occupancy.Status != RoomRuntimeOperationStatusV1.Rejected)
            {
                missionLayout.Restart();
            }

            return occupancy;
        }

        public bool CompleteCurrentRoom(StableId roomStableId)
        {
            RoomRuntimeStateV1 state = missionLayout.GetRoomState(roomStableId);
            if (!state.IsCurrent || state.IsCompleted)
            {
                return false;
            }

            RoomGraphOperationResultV1 result = missionLayout.CompleteCurrentRoom();
            return result.Status == RoomGraphOperationStatusV1.Applied;
        }
    }
}
