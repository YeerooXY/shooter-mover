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
        private void RegisterAuthoredOccupants(AuthorableRoomDefinitionV1 room)
        {
            var occupants = new List<RoomOccupantRegistrationV1>();
            for (int index = 0; index < room.Placements.Count; index++)
            {
                RoomPlacedEntityDefinitionV1 placement = room.Placements[index];
                occupants.Add(new RoomOccupantRegistrationV1(
                    placement.InstanceStableId,
                    placement.DefinitionStableId,
                    placement.ClearRole));
            }

            RoomRuntimeOperationResultV1 result = occupancyAuthority.RegisterOccupants(
                new RegisterRoomOccupantsCommandV1(
                    RuntimeInstanceStableId,
                    CreateInternalOperationStableId(
                        "register|" + room.RoomStableId),
                    occupancyAuthority.CurrentProjection.LifecycleGeneration,
                    room.RoomStableId,
                    occupants));
            if (result.Status != RoomRuntimeOperationStatusV1.Applied)
            {
                throw new InvalidOperationException(
                    "Authored room occupancy registration failed: "
                    + result.RejectionCode);
            }
        }

        private void SynchronizeCompletionAndDoors(StableId roomStableId)
        {
            RoomOccupancyProjectionV1 occupancy =
                occupancyAuthority.GetRoomProjection(roomStableId);
            if (!occupancy.IsCleared)
            {
                return;
            }

            RoomRuntimeStateV1 layoutState = missionLayout.GetRoomState(roomStableId);
            if (occupancy.IsActive
                && layoutState.IsCurrent
                && !layoutState.IsCompleted)
            {
                missionLayout.CompleteCurrentRoom();
                layoutState = missionLayout.GetRoomState(roomStableId);
            }

            if (!layoutState.IsVisited)
            {
                return;
            }

            AuthorableRoomDefinitionV1 room = Definition.GetRoom(roomStableId);
            for (int index = 0; index < room.Doors.Count; index++)
            {
                openedDoorsByRoom[roomStableId].Add(
                    room.Doors[index].DoorInstanceStableId);
            }
        }

        private void RefreshProjection()
        {
            var roomProjections = new List<RoomLiveRoomProjectionV1>();
            for (int roomIndex = 0; roomIndex < Definition.Rooms.Count; roomIndex++)
            {
                AuthorableRoomDefinitionV1 room = Definition.Rooms[roomIndex];
                RoomOccupancyProjectionV1 occupancy =
                    occupancyAuthority.GetRoomProjection(room.RoomStableId);
                var active = new List<RoomOccupantProjectionV1>();
                var defeated = new List<RoomOccupantProjectionV1>();
                for (int occupantIndex = 0;
                    occupantIndex < occupancy.Occupants.Count;
                    occupantIndex++)
                {
                    RoomOccupantProjectionV1 occupant = occupancy.Occupants[occupantIndex];
                    if (occupant.IsTerminal)
                    {
                        defeated.Add(occupant);
                    }
                    else
                    {
                        active.Add(occupant);
                    }
                }

                roomProjections.Add(new RoomLiveRoomProjectionV1(
                    room.RoomStableId,
                    room.DisplayName,
                    occupancy.IsActive,
                    occupancy.IsCleared,
                    active,
                    defeated,
                    collectedDropsByRoom[room.RoomStableId],
                    openedDoorsByRoom[room.RoomStableId]));
            }

            currentProjection = new RoomLiveRuntimeProjectionV1(
                RuntimeInstanceStableId,
                Definition.Fingerprint,
                occupancyAuthority.CurrentProjection.LifecycleGeneration,
                sequence,
                missionLayout.CurrentRoomState.RoomStableId,
                currentSpawnPointStableId,
                finalExitReached,
                roomProjections);
        }

        private RoomLiveOperationResultV1 Result(
            RoomLiveOperationStatusV1 status,
            string rejectionCode,
            RoomLiveRuntimeProjectionV1 previous,
            StableId traversedExitStableId = null,
            StableId targetRoomStableId = null,
            StableId targetSpawnPointStableId = null)
        {
            return new RoomLiveOperationResultV1(
                status,
                rejectionCode,
                previous,
                currentProjection,
                traversedExitStableId,
                targetRoomStableId,
                targetSpawnPointStableId);
        }

        private OperationInspection InspectOperation(
            StableId operationStableId,
            string payload)
        {
            if (operationStableId == null)
            {
                throw new ArgumentNullException(nameof(operationStableId));
            }

            string existing;
            if (!operationPayloads.TryGetValue(operationStableId, out existing))
            {
                return OperationInspection.New;
            }

            return string.Equals(existing, payload, StringComparison.Ordinal)
                ? OperationInspection.Duplicate
                : OperationInspection.Conflict;
        }

        private void RecordOperation(StableId operationStableId, string payload)
        {
            if (!operationPayloads.ContainsKey(operationStableId))
            {
                operationPayloads.Add(operationStableId, payload);
            }
        }

        private static StableId InternalOperation(
            StableId externalOperationStableId,
            string suffix)
        {
            return CreateInternalOperationStableId(
                externalOperationStableId + "|" + suffix);
        }

        private static StableId CreateInternalOperationStableId(string payload)
        {
            using (System.Security.Cryptography.SHA256 sha =
                System.Security.Cryptography.SHA256.Create())
            {
                byte[] bytes = Encoding.UTF8.GetBytes(payload ?? string.Empty);
                byte[] hash = sha.ComputeHash(bytes);
                var token = new StringBuilder(32);
                for (int index = 0; index < 16; index++)
                {
                    token.Append(hash[index].ToString(
                        "x2",
                        CultureInfo.InvariantCulture));
                }

                return StableId.Create(
                    "operation",
                    "room-live-" + token.ToString());
            }
        }

        private static StableId ResolveInitialSpawnPoint(
            AuthorableRoomDefinitionV1 room)
        {
            for (int index = 0; index < room.SpawnPoints.Count; index++)
            {
                if (room.SpawnPoints[index].Kind == RoomSpawnPointKindV1.ForwardEntry)
                {
                    return room.SpawnPoints[index].SpawnPointStableId;
                }
            }

            return room.SpawnPoints[0].SpawnPointStableId;
        }

        private enum OperationInspection
        {
            New = 1,
            Duplicate = 2,
            Conflict = 3,
        }
    }
}
