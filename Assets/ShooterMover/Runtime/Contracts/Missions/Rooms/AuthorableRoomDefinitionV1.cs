using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Missions.Rooms;

namespace ShooterMover.Contracts.Missions.Rooms
{
    public sealed class AuthorableRoomDefinitionV1
    {
        private readonly ReadOnlyCollection<RoomSpawnPointDefinitionV1> spawnPoints;
        private readonly ReadOnlyCollection<RoomPlacedEntityDefinitionV1> placements;
        private readonly ReadOnlyCollection<RoomDoorDefinitionV1> doors;
        private readonly ReadOnlyCollection<RoomExitLinkDefinitionV1> exits;
        private readonly ReadOnlyCollection<RoomCompletionConditionDefinitionV1>
            completionConditions;
        private readonly Dictionary<StableId, RoomSpawnPointDefinitionV1> spawnPointsById;
        private readonly Dictionary<StableId, RoomPlacedEntityDefinitionV1> placementsById;
        private readonly Dictionary<StableId, RoomDoorDefinitionV1> doorsById;
        private readonly Dictionary<StableId, RoomExitLinkDefinitionV1> exitsById;

        public AuthorableRoomDefinitionV1(
            StableId roomStableId,
            int order,
            string displayName,
            RoomBoundsV1 bounds,
            IEnumerable<RoomSpawnPointDefinitionV1> spawnPoints,
            IEnumerable<RoomPlacedEntityDefinitionV1> placements,
            IEnumerable<RoomDoorDefinitionV1> doors,
            IEnumerable<RoomExitLinkDefinitionV1> exits,
            IEnumerable<RoomCompletionConditionDefinitionV1> completionConditions)
        {
            RoomStableId = roomStableId
                ?? throw new ArgumentNullException(nameof(roomStableId));
            if (order < 0) throw new ArgumentOutOfRangeException(nameof(order));
            if (string.IsNullOrWhiteSpace(displayName))
            {
                throw new ArgumentException(
                    "Room display name is required.",
                    nameof(displayName));
            }

            Order = order;
            DisplayName = displayName.Trim();
            Bounds = bounds ?? throw new ArgumentNullException(nameof(bounds));

            this.spawnPoints = CopyAndSort(
                spawnPoints,
                CompareSpawnPoints,
                nameof(spawnPoints));
            this.placements = CopyAndSort(
                placements,
                ComparePlacements,
                nameof(placements));
            this.doors = CopyAndSort(doors, CompareDoors, nameof(doors));
            this.exits = CopyAndSort(exits, CompareExits, nameof(exits));
            this.completionConditions = CopyAndSort(
                completionConditions,
                CompareCompletionConditions,
                nameof(completionConditions));

            if (this.spawnPoints.Count == 0)
            {
                throw new ArgumentException(
                    "Every authorable room requires at least one spawn point.",
                    nameof(spawnPoints));
            }

            if (this.completionConditions.Count == 0)
            {
                throw new ArgumentException(
                    "Every authorable room requires at least one completion condition.",
                    nameof(completionConditions));
            }

            spawnPointsById = IndexUnique(
                this.spawnPoints,
                item => item.SpawnPointStableId,
                "room-live-spawn-point-duplicate");
            placementsById = IndexUnique(
                this.placements,
                item => item.InstanceStableId,
                "room-live-placement-instance-duplicate");
            doorsById = IndexUnique(
                this.doors,
                item => item.DoorInstanceStableId,
                "room-live-door-instance-duplicate");
            exitsById = IndexUnique(
                this.exits,
                item => item.ExitStableId,
                "room-live-exit-duplicate");

            for (int index = 0; index < this.doors.Count; index++)
            {
                RoomDoorDefinitionV1 door = this.doors[index];
                if (!exitsById.ContainsKey(door.ExitStableId))
                {
                    throw new ArgumentException(
                        "room-live-door-exit-unknown:" + door.ExitStableId);
                }
            }

            for (int index = 0; index < this.exits.Count; index++)
            {
                RoomExitLinkDefinitionV1 exit = this.exits[index];
                RoomDoorDefinitionV1 door;
                if (!doorsById.TryGetValue(exit.DoorInstanceStableId, out door))
                {
                    throw new ArgumentException(
                        "room-live-exit-door-unknown:" + exit.DoorInstanceStableId);
                }

                if (door.ExitStableId != exit.ExitStableId)
                {
                    throw new ArgumentException(
                        "room-live-exit-door-mismatch:" + exit.ExitStableId);
                }
            }
        }

        public StableId RoomStableId { get; }

        public int Order { get; }

        public string DisplayName { get; }

        public RoomBoundsV1 Bounds { get; }

        public IReadOnlyList<RoomSpawnPointDefinitionV1> SpawnPoints
        {
            get { return spawnPoints; }
        }

        public IReadOnlyList<RoomPlacedEntityDefinitionV1> Placements
        {
            get { return placements; }
        }

        public IReadOnlyList<RoomDoorDefinitionV1> Doors
        {
            get { return doors; }
        }

        public IReadOnlyList<RoomExitLinkDefinitionV1> Exits
        {
            get { return exits; }
        }

        public IReadOnlyList<RoomCompletionConditionDefinitionV1> CompletionConditions
        {
            get { return completionConditions; }
        }

        public bool TryGetPlacement(
            StableId instanceStableId,
            out RoomPlacedEntityDefinitionV1 placement)
        {
            if (instanceStableId == null)
            {
                placement = null;
                return false;
            }

            return placementsById.TryGetValue(instanceStableId, out placement);
        }

        public bool TryGetDoor(
            StableId doorInstanceStableId,
            out RoomDoorDefinitionV1 door)
        {
            if (doorInstanceStableId == null)
            {
                door = null;
                return false;
            }

            return doorsById.TryGetValue(doorInstanceStableId, out door);
        }

        public bool TryGetExit(
            StableId exitStableId,
            out RoomExitLinkDefinitionV1 exit)
        {
            if (exitStableId == null)
            {
                exit = null;
                return false;
            }

            return exitsById.TryGetValue(exitStableId, out exit);
        }

        public bool TryGetSpawnPoint(
            StableId spawnPointStableId,
            out RoomSpawnPointDefinitionV1 spawnPoint)
        {
            if (spawnPointStableId == null)
            {
                spawnPoint = null;
                return false;
            }

            return spawnPointsById.TryGetValue(spawnPointStableId, out spawnPoint);
        }

        public bool HasSpawnPoint(StableId spawnPointStableId)
        {
            RoomSpawnPointDefinitionV1 ignored;
            return TryGetSpawnPoint(spawnPointStableId, out ignored);
        }

        internal void AppendCanonicalJson(StringBuilder builder)
        {
            builder.Append("{\"room_id\":");
            RoomLiveJsonV1.AppendString(builder, RoomStableId.ToString());
            builder.Append(",\"order\":")
                .Append(Order.ToString(CultureInfo.InvariantCulture))
                .Append(",\"display_name\":");
            RoomLiveJsonV1.AppendString(builder, DisplayName);
            builder.Append(",\"bounds\":");
            Bounds.AppendCanonicalJson(builder);
            AppendArray(builder, "spawn_points", spawnPoints, (item, output) =>
                item.AppendCanonicalJson(output));
            AppendArray(builder, "placements", placements, (item, output) =>
                item.AppendCanonicalJson(output));
            AppendArray(builder, "doors", doors, (item, output) =>
                item.AppendCanonicalJson(output));
            AppendArray(builder, "exits", exits, (item, output) =>
                item.AppendCanonicalJson(output));
            AppendArray(
                builder,
                "completion_conditions",
                completionConditions,
                (item, output) => item.AppendCanonicalJson(output));
            builder.Append('}');
        }

        private static void AppendArray<T>(
            StringBuilder builder,
            string propertyName,
            IReadOnlyList<T> values,
            Action<T, StringBuilder> append)
        {
            builder.Append(',');
            RoomLiveJsonV1.AppendString(builder, propertyName);
            builder.Append(":[");
            for (int index = 0; index < values.Count; index++)
            {
                if (index != 0) builder.Append(',');
                append(values[index], builder);
            }

            builder.Append(']');
        }

        private static ReadOnlyCollection<T> CopyAndSort<T>(
            IEnumerable<T> source,
            Comparison<T> comparison,
            string parameterName)
            where T : class
        {
            if (source == null) throw new ArgumentNullException(parameterName);
            var copy = new List<T>(source);
            for (int index = 0; index < copy.Count; index++)
            {
                if (copy[index] == null)
                {
                    throw new ArgumentException(
                        "Room definitions cannot contain null elements.",
                        parameterName);
                }
            }

            copy.Sort(comparison);
            return new ReadOnlyCollection<T>(copy);
        }

        private static Dictionary<StableId, T> IndexUnique<T>(
            IEnumerable<T> source,
            Func<T, StableId> selectId,
            string rejectionCode)
        {
            var result = new Dictionary<StableId, T>();
            foreach (T item in source)
            {
                StableId id = selectId(item);
                if (result.ContainsKey(id))
                {
                    throw new ArgumentException(rejectionCode + ":" + id);
                }

                result.Add(id, item);
            }

            return result;
        }

        private static int CompareSpawnPoints(
            RoomSpawnPointDefinitionV1 left,
            RoomSpawnPointDefinitionV1 right)
        {
            return left.SpawnPointStableId.CompareTo(right.SpawnPointStableId);
        }

        private static int ComparePlacements(
            RoomPlacedEntityDefinitionV1 left,
            RoomPlacedEntityDefinitionV1 right)
        {
            return left.InstanceStableId.CompareTo(right.InstanceStableId);
        }

        private static int CompareDoors(
            RoomDoorDefinitionV1 left,
            RoomDoorDefinitionV1 right)
        {
            return left.DoorInstanceStableId.CompareTo(right.DoorInstanceStableId);
        }

        private static int CompareExits(
            RoomExitLinkDefinitionV1 left,
            RoomExitLinkDefinitionV1 right)
        {
            return left.ExitStableId.CompareTo(right.ExitStableId);
        }

        private static int CompareCompletionConditions(
            RoomCompletionConditionDefinitionV1 left,
            RoomCompletionConditionDefinitionV1 right)
        {
            return left.ConditionStableId.CompareTo(right.ConditionStableId);
        }
    }

}
