using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using ShooterMover.Contracts.Missions.Rooms;
using ShooterMover.Domain.Common;

namespace ShooterMover.Application.Missions.Rooms
{
    public sealed partial class RoomLiveRuntimeAuthorityV1
    {
        private readonly Dictionary<StableId, string> operationPayloads =
            new Dictionary<StableId, string>();
        private readonly Dictionary<StableId, HashSet<StableId>> collectedDropsByRoom =
            new Dictionary<StableId, HashSet<StableId>>();
        private readonly Dictionary<StableId, HashSet<StableId>> openedDoorsByRoom =
            new Dictionary<StableId, HashSet<StableId>>();
        private readonly RoomRuntimeAuthorityV1 occupancyAuthority;
        private readonly RoomMissionLayoutV1 missionLayout;
        private long sequence;
        private StableId currentSpawnPointStableId;
        private bool finalExitReached;
        private RoomLiveRuntimeProjectionV1 currentProjection;

        public RoomLiveRuntimeAuthorityV1(
            StableId runtimeInstanceStableId,
            AuthorableRoomGraphDefinitionV1 definition)
        {
            RuntimeInstanceStableId = runtimeInstanceStableId
                ?? throw new ArgumentNullException(nameof(runtimeInstanceStableId));
            Definition = definition
                ?? throw new ArgumentNullException(nameof(definition));
            occupancyAuthority = new RoomRuntimeAuthorityV1(
                RuntimeInstanceStableId,
                Definition.RoomGraphDefinition);
            missionLayout = new RoomMissionLayoutV1(Definition.RoomGraphDefinition);
            for (int index = 0; index < Definition.Rooms.Count; index++)
            {
                AuthorableRoomDefinitionV1 room = Definition.Rooms[index];
                collectedDropsByRoom.Add(room.RoomStableId, new HashSet<StableId>());
                openedDoorsByRoom.Add(room.RoomStableId, new HashSet<StableId>());
                RegisterAuthoredOccupants(room);
            }

            currentSpawnPointStableId = ResolveInitialSpawnPoint(
                Definition.GetRoom(Definition.StartRoomStableId));
            SynchronizeCompletionAndDoors(Definition.StartRoomStableId);
            RefreshProjection();
        }

        public StableId RuntimeInstanceStableId { get; }

        public AuthorableRoomGraphDefinitionV1 Definition { get; }

        public IRoomRuntimeAuthorityV1 OccupancyAuthority
        {
            get { return occupancyAuthority; }
        }

        public RoomMissionLayoutV1 MissionLayout
        {
            get { return missionLayout; }
        }

        public RoomLiveRuntimeProjectionV1 CurrentProjection
        {
            get { return currentProjection; }
        }

        public RoomLiveRoomProjectionV1 GetRoomProjection(StableId roomStableId)
        {
            return currentProjection.GetRoom(roomStableId);
        }

        public RoomLiveOperationResultV1 ReportOccupantTerminal(
            StableId operationStableId,
            StableId roomStableId,
            StableId occupantInstanceStableId)
        {
            RoomLiveRuntimeProjectionV1 previous = currentProjection;
            string payload = "terminal|" + roomStableId + "|" + occupantInstanceStableId;
            OperationInspection inspection = InspectOperation(operationStableId, payload);
            if (inspection == OperationInspection.Duplicate)
            {
                return Result(
                    RoomLiveOperationStatusV1.DuplicateNoChange,
                    string.Empty,
                    previous);
            }

            if (inspection == OperationInspection.Conflict)
            {
                return Result(
                    RoomLiveOperationStatusV1.Rejected,
                    "room-live-operation-id-conflict",
                    previous);
            }

            AuthorableRoomDefinitionV1 room;
            RoomPlacedEntityDefinitionV1 placement;
            if (!Definition.TryGetRoom(roomStableId, out room))
            {
                RecordOperation(operationStableId, payload);
                return Result(
                    RoomLiveOperationStatusV1.Rejected,
                    "room-live-room-unknown",
                    previous);
            }

            if (!room.TryGetPlacement(occupantInstanceStableId, out placement))
            {
                RecordOperation(operationStableId, payload);
                return Result(
                    RoomLiveOperationStatusV1.Rejected,
                    "room-live-occupant-unknown",
                    previous);
            }

            RoomRuntimeOperationResultV1 occupancy = occupancyAuthority.ReportTerminal(
                new ReportRoomOccupantTerminalCommandV1(
                    RuntimeInstanceStableId,
                    InternalOperation(operationStableId, "occupancy-terminal"),
                    occupancyAuthority.CurrentProjection.LifecycleGeneration,
                    roomStableId,
                    occupantInstanceStableId));
            RecordOperation(operationStableId, payload);
            if (occupancy.Status == RoomRuntimeOperationStatusV1.Rejected)
            {
                return Result(
                    RoomLiveOperationStatusV1.Rejected,
                    occupancy.RejectionCode,
                    previous);
            }

            if (occupancy.Status == RoomRuntimeOperationStatusV1.Applied)
            {
                SynchronizeCompletionAndDoors(roomStableId);
                sequence = checked(sequence + 1L);
                RefreshProjection();
                return Result(RoomLiveOperationStatusV1.Applied, string.Empty, previous);
            }

            RefreshProjection();
            return Result(RoomLiveOperationStatusV1.NoChange, string.Empty, previous);
        }

        public RoomLiveOperationResultV1 ReportDropCollected(
            StableId operationStableId,
            StableId roomStableId,
            StableId dropInstanceStableId)
        {
            RoomLiveRuntimeProjectionV1 previous = currentProjection;
            string payload = "drop|" + roomStableId + "|" + dropInstanceStableId;
            OperationInspection inspection = InspectOperation(operationStableId, payload);
            if (inspection == OperationInspection.Duplicate)
            {
                return Result(
                    RoomLiveOperationStatusV1.DuplicateNoChange,
                    string.Empty,
                    previous);
            }

            if (inspection == OperationInspection.Conflict)
            {
                return Result(
                    RoomLiveOperationStatusV1.Rejected,
                    "room-live-operation-id-conflict",
                    previous);
            }

            if (dropInstanceStableId == null)
            {
                throw new ArgumentNullException(nameof(dropInstanceStableId));
            }

            AuthorableRoomDefinitionV1 ignored;
            if (!Definition.TryGetRoom(roomStableId, out ignored))
            {
                RecordOperation(operationStableId, payload);
                return Result(
                    RoomLiveOperationStatusV1.Rejected,
                    "room-live-room-unknown",
                    previous);
            }

            RecordOperation(operationStableId, payload);
            if (!collectedDropsByRoom[roomStableId].Add(dropInstanceStableId))
            {
                return Result(RoomLiveOperationStatusV1.NoChange, string.Empty, previous);
            }

            sequence = checked(sequence + 1L);
            RefreshProjection();
            return Result(RoomLiveOperationStatusV1.Applied, string.Empty, previous);
        }

    }
}
