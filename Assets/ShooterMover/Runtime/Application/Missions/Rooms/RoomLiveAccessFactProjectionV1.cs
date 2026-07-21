using System;
using System.Collections.Generic;
using ShooterMover.Contracts.Missions.Rooms;
using ShooterMover.Domain.Common;

namespace ShooterMover.Application.Missions.Rooms
{
    /// <summary>
    /// Pure projection bridge from ROOM-LIVE immutable state into access facts.
    /// It preserves visited as room-entered, completed as room-complete, accepted
    /// defeated occupant identity as exact-terminal, and retained collected drops.
    /// </summary>
    public static class RoomLiveAccessFactProjectionV1
    {
        public static RoomAccessFactSnapshotV1 Build(
            RoomLiveRuntimeProjectionV1 roomProjection,
            int difficulty,
            IEnumerable<StableId> completedObjectives,
            IEnumerable<StableId> activeSwitches,
            IEnumerable<StableId> consumedHoldings)
        {
            if (roomProjection == null)
            {
                throw new ArgumentNullException(nameof(roomProjection));
            }

            var enteredRooms = new HashSet<StableId>();
            var completedRooms = new HashSet<StableId>();
            var terminalEntities = new HashSet<StableId>();
            var collectedDrops = new HashSet<StableId>();
            for (int roomIndex = 0;
                roomIndex < roomProjection.Rooms.Count;
                roomIndex++)
            {
                RoomLiveRoomProjectionV1 room = roomProjection.Rooms[roomIndex];
                if (room.IsVisited) enteredRooms.Add(room.RoomStableId);
                if (room.IsCompleted) completedRooms.Add(room.RoomStableId);
                for (int occupantIndex = 0;
                    occupantIndex < room.DefeatedOccupants.Count;
                    occupantIndex++)
                {
                    terminalEntities.Add(
                        room.DefeatedOccupants[occupantIndex].EntityStableId);
                }
                for (int dropIndex = 0;
                    dropIndex < room.CollectedDropInstanceStableIds.Count;
                    dropIndex++)
                {
                    collectedDrops.Add(
                        room.CollectedDropInstanceStableIds[dropIndex]);
                }
            }

            return new RoomAccessFactSnapshotV1(
                difficulty,
                enteredRooms,
                completedRooms,
                terminalEntities,
                collectedDrops,
                completedObjectives,
                activeSwitches,
                consumedHoldings);
        }
    }
}
