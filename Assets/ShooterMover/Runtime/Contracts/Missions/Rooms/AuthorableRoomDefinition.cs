using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using ShooterMover.Domain.Common;

namespace ShooterMover.Contracts.Missions.Rooms
{
    public sealed class AuthorableRoomDefinition
    {
        private readonly ReadOnlyCollection<RoomSpawnPointDefinition> spawnPoints;
        private readonly ReadOnlyCollection<RoomPlacedEntityDefinition> placements;
        private readonly ReadOnlyCollection<RoomDoorDefinition> doors;
        private readonly ReadOnlyCollection<RoomExitLinkDefinition> exits;
        private readonly ReadOnlyCollection<RoomCompletionConditionDefinition>
            completionConditions;
        private readonly Dictionary<StableId, RoomSpawnPointDefinition> spawnPointsById;
        private readonly Dictionary<StableId, RoomPlacedEntityDefinition> placementsById;
        private readonly Dictionary<StableId, RoomDoorDefinition> doorsById;
        private readonly Dictionary<StableId, RoomExitLinkDefinition> exitsById;
        private readonly Dictionary<StableId, RoomCompletionConditionDefinition>
            completionConditionsById;

        public AuthorableRoomDefinition(
            StableId roomStableId,
            int order,
            string displayName,
            RoomBounds bounds,
            IEnumerable<RoomSpawnPointDefinition> spawnPoints,
            IEnumerable<RoomPlacedEntityDefinition> placements,
            IEnumerable<RoomDoorDefinition> doors,
            IEnumerable<RoomExitLinkDefinition> exits,
            IEnumerable<RoomCompletionConditionDefinition> completionConditions)
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
            completionConditionsById = IndexUnique(
                this.completionConditions,
                item => item.ConditionStableId,
                "room-live-completion-condition-duplicate");

            ValidateDoorAndExitLinks();
            ValidateDoorConditionReferences();
        }

        public StableId RoomStableId { get; }

        public int Order { get; }

        public string DisplayName { get; }

        public RoomBounds Bounds { get; }

        public IReadOnlyList<RoomSpawnPointDefinition> SpawnPoints
        {
            get { return spawnPoints; }
        }

        public IReadOnlyList<RoomPlacedEntityDefinition> Placements
        {
            get { return placements; }
        }

        public IReadOnlyList<RoomDoorDefinition> Doors
        {
            get { return doors; }
        }

        public IReadOnlyList<RoomExitLinkDefinition> Exits
        {
            get { return exits; }
        }

        public IReadOnlyList<RoomCompletionConditionDefinition> CompletionConditions
        {
            get { return completionConditions; }
        }

        public bool HasSpawnPoint(StableId spawnPointStableId)
        {
            return spawnPointStableId != null
                && spawnPointsById.ContainsKey(spawnPointStableId);
        }

        public bool TryGetSpawnPoint(
            StableId spawnPointStableId,
            out RoomSpawnPointDefinition spawnPoint)
        {
            if (spawnPointStableId == null)
            {
                spawnPoint = null;
                return false;
            }

            return spawnPointsById.TryGetValue(spawnPointStableId, out spawnPoint);
        }

        public bool TryGetPlacement(
            StableId instanceStableId,
            out RoomPlacedEntityDefinition placement)
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
            out RoomDoorDefinition door)
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
            out RoomExitLinkDefinition exit)
        {
            if (exitStableId == null)
            {
                exit = null;
                return false;
            }

            return exitsById.TryGetValue(exitStableId, out exit);
        }

        public bool TryGetCompletionCondition(
            StableId conditionStableId,
            out RoomCompletionConditionDefinition condition)
        {
            if (conditionStableId == null)
            {
                condition = null;
                return false;
            }

            return completionConditionsById.TryGetValue(
                conditionStableId,
                out condition);
        }

        internal void AppendCanonicalJson(StringBuilder builder)
        {
            builder.Append("{\"room_id\":");
            RoomLiveJson.AppendString(builder, RoomStableId.ToString());
            builder.Append(",\"order\":")
                .Append(Order.ToString(CultureInfo.InvariantCulture))
                .Append(",\"display_name\":");
            RoomLiveJson.AppendString(builder, DisplayName);
            builder.Append(",\"bounds\":");
            Bounds.AppendCanonicalJson(builder);
            AppendArray(builder, "spawn_points", spawnPoints, (value, target) =>
                value.AppendCanonicalJson(target));
            AppendArray(builder, "placements", placements, (value, target) =>
                value.AppendCanonicalJson(target));
            AppendArray(builder, "doors", doors, (value, target) =>
                value.AppendCanonicalJson(target));
            AppendArray(builder, "exits", exits, (value, target) =>
                value.AppendCanonicalJson(target));
            AppendArray(
                builder,
                "completion_conditions",
                completionConditions,
                (value, target) => value.AppendCanonicalJson(target));
            builder.Append('}');
        }

        private void ValidateDoorAndExitLinks()
        {
            for (int index = 0; index < doors.Count; index++)
            {
                RoomDoorDefinition door = doors[index];
                RoomExitLinkDefinition exit;
                if (!exitsById.TryGetValue(door.ExitStableId, out exit))
                {
                    throw new ArgumentException(
                        "room-live-door-exit-unknown:" + door.ExitStableId);
                }

                if (exit.DoorInstanceStableId != door.DoorInstanceStableId)
                {
                    throw new ArgumentException(
                        "room-live-door-exit-mismatch:" + door.DoorInstanceStableId);
                }
            }

            for (int index = 0; index < exits.Count; index++)
            {
                RoomExitLinkDefinition exit = exits[index];
                RoomDoorDefinition door;
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

        private void ValidateDoorConditionReferences()
        {
            for (int doorIndex = 0; doorIndex < doors.Count; doorIndex++)
            {
                RoomDoorDefinition door = doors[doorIndex];
                for (int conditionIndex = 0;
                    conditionIndex < door.RequiredConditionStableIds.Count;
                    conditionIndex++)
                {
                    StableId conditionId = door.RequiredConditionStableIds[conditionIndex];
                    if (!completionConditionsById.ContainsKey(conditionId))
                    {
                        throw new ArgumentException(
                            "room-live-door-condition-unknown:"
                            + door.DoorInstanceStableId
                            + ":"
                            + conditionId);
                    }
                }
            }
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
                        "Authorable room collections cannot contain null entries.",
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
            foreach (T value in source)
            {
                StableId id = selectId(value);
                if (result.ContainsKey(id))
                {
                    throw new ArgumentException(rejectionCode + ":" + id);
                }

                result.Add(id, value);
            }

            return result;
        }

        private static void AppendArray<T>(
            StringBuilder builder,
            string name,
            IReadOnlyList<T> values,
            Action<T, StringBuilder> append)
        {
            builder.Append(",\"").Append(name).Append("\":[");
            for (int index = 0; index < values.Count; index++)
            {
                if (index != 0) builder.Append(',');
                append(values[index], builder);
            }

            builder.Append(']');
        }

        private static int CompareSpawnPoints(
            RoomSpawnPointDefinition left,
            RoomSpawnPointDefinition right)
        {
            return left.SpawnPointStableId.CompareTo(right.SpawnPointStableId);
        }

        private static int ComparePlacements(
            RoomPlacedEntityDefinition left,
            RoomPlacedEntityDefinition right)
        {
            return left.InstanceStableId.CompareTo(right.InstanceStableId);
        }

        private static int CompareDoors(
            RoomDoorDefinition left,
            RoomDoorDefinition right)
        {
            return left.DoorInstanceStableId.CompareTo(right.DoorInstanceStableId);
        }

        private static int CompareExits(
            RoomExitLinkDefinition left,
            RoomExitLinkDefinition right)
        {
            return left.ExitStableId.CompareTo(right.ExitStableId);
        }

        private static int CompareCompletionConditions(
            RoomCompletionConditionDefinition left,
            RoomCompletionConditionDefinition right)
        {
            return left.ConditionStableId.CompareTo(right.ConditionStableId);
        }
    }
}
