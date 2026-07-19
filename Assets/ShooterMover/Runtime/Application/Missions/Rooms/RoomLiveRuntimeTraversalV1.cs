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
        public RoomLiveOperationResultV1 Traverse(
            StableId operationStableId,
            StableId exitStableId)
        {
            RoomLiveRuntimeProjectionV1 previous = currentProjection;
            string payload = "traverse|" + exitStableId;
            OperationInspection inspection = InspectOperation(operationStableId, payload);
            if (inspection == OperationInspection.Duplicate)
            {
                return Result(
                    RoomLiveOperationStatusV1.DuplicateNoChange,
                    string.Empty,
                    previous,
                    exitStableId);
            }

            if (inspection == OperationInspection.Conflict)
            {
                return Result(
                    RoomLiveOperationStatusV1.Rejected,
                    "room-live-operation-id-conflict",
                    previous,
                    exitStableId);
            }

            AuthorableRoomDefinitionV1 owner;
            RoomExitLinkDefinitionV1 exit;
            if (!Definition.TryGetExitOwner(exitStableId, out owner)
                || !owner.TryGetExit(exitStableId, out exit))
            {
                RecordOperation(operationStableId, payload);
                return Result(
                    RoomLiveOperationStatusV1.Rejected,
                    "room-live-exit-unknown",
                    previous,
                    exitStableId);
            }

            if (owner.RoomStableId != currentProjection.CurrentRoomStableId)
            {
                RecordOperation(operationStableId, payload);
                return Result(
                    RoomLiveOperationStatusV1.Rejected,
                    "room-live-exit-not-from-current-room",
                    previous,
                    exitStableId);
            }

            if (!openedDoorsByRoom[owner.RoomStableId].Contains(
                exit.DoorInstanceStableId))
            {
                RecordOperation(operationStableId, payload);
                return Result(
                    RoomLiveOperationStatusV1.Rejected,
                    "room-live-door-closed",
                    previous,
                    exitStableId);
            }

            RecordOperation(operationStableId, payload);
            if (exit.LinkKind == RoomLiveLinkKindV1.FinalExit)
            {
                if (finalExitReached)
                {
                    return Result(
                        RoomLiveOperationStatusV1.NoChange,
                        string.Empty,
                        previous,
                        exitStableId);
                }

                finalExitReached = true;
                sequence = checked(sequence + 1L);
                RefreshProjection();
                return Result(
                    RoomLiveOperationStatusV1.FinalExitReached,
                    string.Empty,
                    previous,
                    exitStableId);
            }

            if (!missionLayout.GetExitState(exitStableId).IsAvailable)
            {
                return Result(
                    RoomLiveOperationStatusV1.Rejected,
                    "room-live-exit-locked",
                    previous,
                    exitStableId);
            }

            RoomGraphOperationResultV1 traversal = missionLayout.Traverse(exitStableId);
            if (traversal.Status != RoomGraphOperationStatusV1.Applied)
            {
                return Result(
                    RoomLiveOperationStatusV1.Rejected,
                    traversal.RejectionCode,
                    previous,
                    exitStableId);
            }

            RoomRuntimeOperationResultV1 activation = occupancyAuthority.ActivateRoom(
                new ActivateRoomCommandV1(
                    RuntimeInstanceStableId,
                    InternalOperation(operationStableId, "occupancy-activate"),
                    occupancyAuthority.CurrentProjection.LifecycleGeneration,
                    exit.TargetRoomStableId));
            if (activation.Status != RoomRuntimeOperationStatusV1.Applied
                && activation.Status != RoomRuntimeOperationStatusV1.NoChange)
            {
                throw new InvalidOperationException(
                    "Room layout traversal and occupancy activation diverged: "
                    + activation.RejectionCode);
            }

            currentSpawnPointStableId = exit.TargetSpawnPointStableId;
            SynchronizeCompletionAndDoors(exit.TargetRoomStableId);
            sequence = checked(sequence + 1L);
            RefreshProjection();
            return Result(
                RoomLiveOperationStatusV1.Applied,
                string.Empty,
                previous,
                exitStableId,
                exit.TargetRoomStableId,
                exit.TargetSpawnPointStableId);
        }

        public RoomLiveOperationResultV1 Restart(StableId operationStableId)
        {
            RoomLiveRuntimeProjectionV1 previous = currentProjection;
            string payload = "restart|" + currentProjection.LifecycleGeneration;
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

            RoomRuntimeOperationResultV1 occupancy = occupancyAuthority.Restart(
                new RestartRoomRuntimeCommandV1(
                    RuntimeInstanceStableId,
                    InternalOperation(operationStableId, "occupancy-restart"),
                    occupancyAuthority.CurrentProjection.LifecycleGeneration));
            RecordOperation(operationStableId, payload);
            if (occupancy.Status == RoomRuntimeOperationStatusV1.Rejected)
            {
                return Result(
                    RoomLiveOperationStatusV1.Rejected,
                    occupancy.RejectionCode,
                    previous);
            }

            missionLayout.Restart();
            foreach (HashSet<StableId> drops in collectedDropsByRoom.Values)
            {
                drops.Clear();
            }

            foreach (HashSet<StableId> doors in openedDoorsByRoom.Values)
            {
                doors.Clear();
            }

            finalExitReached = false;
            currentSpawnPointStableId = ResolveInitialSpawnPoint(
                Definition.GetRoom(Definition.StartRoomStableId));
            SynchronizeCompletionAndDoors(Definition.StartRoomStableId);
            sequence = checked(sequence + 1L);
            RefreshProjection();
            return Result(RoomLiveOperationStatusV1.Applied, string.Empty, previous);
        }
    }
}
