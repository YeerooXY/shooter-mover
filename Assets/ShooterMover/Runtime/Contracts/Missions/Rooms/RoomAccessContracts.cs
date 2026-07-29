using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using ShooterMover.Domain.Common;

namespace ShooterMover.Contracts.Missions.Rooms
{
    public enum RoomAccessConditionKind
    {
        Always = 1,
        RoomEntered = 2,
        RoomComplete = 3,
        ExactEntityTerminal = 4,
        HoldingPresent = 5,
        HoldingConsumed = 6,
        CollectedDrop = 7,
        ObjectiveComplete = 8,
        SwitchActive = 9,
        DifficultyAtLeast = 10,
        All = 11,
        Any = 12,
        Not = 13,
    }

    public enum RoomAccessOperationStatus
    {
        Applied = 1,
        DuplicateNoChange = 2,
        NoChange = 3,
        Rejected = 4,
    }

    public enum RoomHoldingConsumeStatus
    {
        Applied = 1,
        DuplicateAccepted = 2,
        Rejected = 3,
    }

    public sealed class RoomAccessConditionDefinition
    {
        private readonly ReadOnlyCollection<StableId> childConditionStableIds;

        public RoomAccessConditionDefinition(
            StableId conditionStableId,
            RoomAccessConditionKind kind,
            StableId subjectStableId,
            int minimumDifficulty,
            IEnumerable<StableId> childConditionStableIds)
        {
            ConditionStableId = conditionStableId
                ?? throw new ArgumentNullException(nameof(conditionStableId));
            if (!Enum.IsDefined(typeof(RoomAccessConditionKind), kind))
            {
                throw new ArgumentOutOfRangeException(nameof(kind));
            }
            if (minimumDifficulty < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(minimumDifficulty));
            }

            var children = new List<StableId>(
                childConditionStableIds ?? Array.Empty<StableId>());
            var seen = new HashSet<StableId>();
            for (int index = 0; index < children.Count; index++)
            {
                if (children[index] == null)
                {
                    throw new ArgumentException(
                        "Room access child identities cannot contain null values.",
                        nameof(childConditionStableIds));
                }
                if (!seen.Add(children[index]))
                {
                    throw new ArgumentException(
                        "room-access-condition-child-duplicate:"
                        + ConditionStableId
                        + ":"
                        + children[index]);
                }
            }
            children.Sort();

            bool composite = kind == RoomAccessConditionKind.All
                || kind == RoomAccessConditionKind.Any
                || kind == RoomAccessConditionKind.Not;
            if (composite)
            {
                if (subjectStableId != null || minimumDifficulty != 0)
                {
                    throw new ArgumentException(
                        "Composite access conditions cannot carry a subject or difficulty threshold.");
                }
                if (kind == RoomAccessConditionKind.Not && children.Count != 1)
                {
                    throw new ArgumentException(
                        "room-access-not-child-count:" + ConditionStableId);
                }
                if (kind != RoomAccessConditionKind.Not && children.Count == 0)
                {
                    throw new ArgumentException(
                        "room-access-composite-empty:" + ConditionStableId);
                }
            }
            else
            {
                if (children.Count != 0)
                {
                    throw new ArgumentException(
                        "Leaf access conditions cannot reference child conditions.");
                }

                bool requiresSubject = kind == RoomAccessConditionKind.RoomEntered
                    || kind == RoomAccessConditionKind.RoomComplete
                    || kind == RoomAccessConditionKind.ExactEntityTerminal
                    || kind == RoomAccessConditionKind.HoldingPresent
                    || kind == RoomAccessConditionKind.HoldingConsumed
                    || kind == RoomAccessConditionKind.CollectedDrop
                    || kind == RoomAccessConditionKind.ObjectiveComplete
                    || kind == RoomAccessConditionKind.SwitchActive;
                if (requiresSubject && subjectStableId == null)
                {
                    throw new ArgumentNullException(nameof(subjectStableId));
                }
                if (!requiresSubject && subjectStableId != null)
                {
                    throw new ArgumentException(
                        "This access condition kind cannot carry a subject identity.",
                        nameof(subjectStableId));
                }
                if (kind == RoomAccessConditionKind.DifficultyAtLeast)
                {
                    if (minimumDifficulty <= 0)
                    {
                        throw new ArgumentOutOfRangeException(nameof(minimumDifficulty));
                    }
                }
                else if (minimumDifficulty != 0)
                {
                    throw new ArgumentException(
                        "Only difficulty-threshold conditions may carry minimum difficulty.");
                }
            }

            Kind = kind;
            SubjectStableId = subjectStableId;
            MinimumDifficulty = minimumDifficulty;
            this.childConditionStableIds = new ReadOnlyCollection<StableId>(children);
        }

        public StableId ConditionStableId { get; }

        public RoomAccessConditionKind Kind { get; }

        public StableId SubjectStableId { get; }

        public int MinimumDifficulty { get; }

        public IReadOnlyList<StableId> ChildConditionStableIds =>
            childConditionStableIds;
    }

    public sealed class RoomDoorAccessDefinition
    {
        public RoomDoorAccessDefinition(
            StableId roomStableId,
            StableId doorStableId,
            StableId rootConditionStableId,
            StableId consumeHoldingStableId)
        {
            RoomStableId = roomStableId
                ?? throw new ArgumentNullException(nameof(roomStableId));
            DoorStableId = doorStableId
                ?? throw new ArgumentNullException(nameof(doorStableId));
            RootConditionStableId = rootConditionStableId
                ?? throw new ArgumentNullException(nameof(rootConditionStableId));
            ConsumeHoldingStableId = consumeHoldingStableId;
        }

        public StableId RoomStableId { get; }

        public StableId DoorStableId { get; }

        public StableId RootConditionStableId { get; }

        public StableId ConsumeHoldingStableId { get; }
    }

    public sealed class RoomAccessDefinition
    {
        private readonly ReadOnlyCollection<RoomAccessConditionDefinition> conditions;
        private readonly ReadOnlyCollection<RoomDoorAccessDefinition> doors;
        private readonly Dictionary<StableId, RoomAccessConditionDefinition> conditionsById;
        private readonly Dictionary<StableId, RoomDoorAccessDefinition> doorsById;

        public const int CurrentSchemaVersion = 2;

        public RoomAccessDefinition(
            AuthorableRoomGraphDefinition roomGraph,
            IEnumerable<RoomAccessConditionDefinition> conditions,
            IEnumerable<RoomDoorAccessDefinition> doors)
            : this(
                roomGraph,
                RoomAccessReferenceCatalog.Empty,
                conditions,
                doors)
        {
        }

        public RoomAccessDefinition(
            AuthorableRoomGraphDefinition roomGraph,
            IRoomAccessReferenceRegistry referenceRegistry,
            IEnumerable<RoomAccessConditionDefinition> conditions,
            IEnumerable<RoomDoorAccessDefinition> doors)
        {
            RoomGraph = roomGraph ?? throw new ArgumentNullException(nameof(roomGraph));
            ReferenceRegistry = RoomAccessReferenceCatalog.Snapshot(
                referenceRegistry
                    ?? throw new ArgumentNullException(nameof(referenceRegistry)));
            if (string.IsNullOrWhiteSpace(ReferenceRegistry.Fingerprint))
            {
                throw new ArgumentException(
                    "room-access-reference-registry-fingerprint-missing",
                    nameof(referenceRegistry));
            }

            ReadOnlyCollection<RoomAccessConditionDefinition> conditionCopy =
                CopyAndSort(
                    conditions,
                    (left, right) => left.ConditionStableId.CompareTo(
                        right.ConditionStableId),
                    nameof(conditions));
            ReadOnlyCollection<RoomDoorAccessDefinition> doorCopy = CopyAndSort(
                doors,
                (left, right) => left.DoorStableId.CompareTo(right.DoorStableId),
                nameof(doors));
            if (conditionCopy.Count == 0)
            {
                throw new ArgumentException(
                    "Room access definitions require at least one condition.",
                    nameof(conditions));
            }

            conditionsById = IndexUnique(
                conditionCopy,
                value => value.ConditionStableId,
                "room-access-condition-duplicate");
            doorsById = IndexUnique(
                doorCopy,
                value => value.DoorStableId,
                "room-access-door-duplicate");
            this.conditions = conditionCopy;
            this.doors = doorCopy;

            ValidateConditionReferences();
            ValidateConditionSubjects();
            ValidateDoorReferences();
            ValidateAcyclic();
            CanonicalJson = BuildCanonicalJson();
            Fingerprint = ComputeSha256(CanonicalJson);
        }

        public AuthorableRoomGraphDefinition RoomGraph { get; }

        public RoomAccessReferenceCatalog ReferenceRegistry { get; }

        public StableId LayoutStableId => RoomGraph.LayoutStableId;

        public string ReferenceRegistryFingerprint => ReferenceRegistry.Fingerprint;

        public IReadOnlyList<RoomAccessConditionDefinition> Conditions => conditions;

        public IReadOnlyList<RoomDoorAccessDefinition> Doors => doors;

        public string CanonicalJson { get; }

        public string Fingerprint { get; }

        public bool TryGetCondition(
            StableId conditionStableId,
            out RoomAccessConditionDefinition condition)
        {
            condition = null;
            return conditionStableId != null
                && conditionsById.TryGetValue(conditionStableId, out condition);
        }

        public bool TryGetDoor(
            StableId doorStableId,
            out RoomDoorAccessDefinition door)
        {
            door = null;
            return doorStableId != null
                && doorsById.TryGetValue(doorStableId, out door);
        }

        public string ToCanonicalJson()
        {
            return CanonicalJson;
        }

        private void ValidateConditionReferences()
        {
            for (int index = 0; index < conditions.Count; index++)
            {
                RoomAccessConditionDefinition condition = conditions[index];
                for (int childIndex = 0;
                    childIndex < condition.ChildConditionStableIds.Count;
                    childIndex++)
                {
                    StableId childId = condition.ChildConditionStableIds[childIndex];
                    if (!conditionsById.ContainsKey(childId))
                    {
                        throw new ArgumentException(
                            "room-access-condition-reference-unknown:"
                            + condition.ConditionStableId
                            + ":"
                            + childId);
                    }
                }
            }
        }

        private void ValidateConditionSubjects()
        {
            var placementIds = new HashSet<StableId>();
            for (int roomIndex = 0; roomIndex < RoomGraph.Rooms.Count; roomIndex++)
            {
                AuthorableRoomDefinition room = RoomGraph.Rooms[roomIndex];
                for (int placementIndex = 0;
                    placementIndex < room.Placements.Count;
                    placementIndex++)
                {
                    placementIds.Add(room.Placements[placementIndex].InstanceStableId);
                }
            }

            for (int index = 0; index < conditions.Count; index++)
            {
                RoomAccessConditionDefinition condition = conditions[index];
                AuthorableRoomDefinition ignored;
                if ((condition.Kind == RoomAccessConditionKind.RoomEntered
                        || condition.Kind == RoomAccessConditionKind.RoomComplete)
                    && !RoomGraph.TryGetRoom(condition.SubjectStableId, out ignored))
                {
                    throw new ArgumentException(
                        "room-access-room-reference-unknown:"
                        + condition.ConditionStableId
                        + ":"
                        + condition.SubjectStableId);
                }

                if (condition.Kind == RoomAccessConditionKind.ExactEntityTerminal
                    && !placementIds.Contains(condition.SubjectStableId))
                {
                    throw new ArgumentException(
                        "room-access-terminal-reference-unknown:"
                        + condition.ConditionStableId
                        + ":"
                        + condition.SubjectStableId);
                }

                if (condition.Kind == RoomAccessConditionKind.HoldingPresent
                    && !ReferenceRegistry.ContainsHolding(condition.SubjectStableId))
                {
                    throw new ArgumentException(
                        "room-access-holding-reference-unknown:"
                        + condition.ConditionStableId
                        + ":"
                        + condition.SubjectStableId);
                }

                if (condition.Kind == RoomAccessConditionKind.HoldingConsumed
                    && !ReferenceRegistry.ContainsHolding(condition.SubjectStableId))
                {
                    throw new ArgumentException(
                        "room-access-holding-reference-unknown:"
                        + condition.ConditionStableId
                        + ":"
                        + condition.SubjectStableId);
                }

                if (condition.Kind == RoomAccessConditionKind.ObjectiveComplete
                    && !ReferenceRegistry.ContainsObjective(condition.SubjectStableId))
                {
                    throw new ArgumentException(
                        "room-access-objective-reference-unknown:"
                        + condition.ConditionStableId
                        + ":"
                        + condition.SubjectStableId);
                }

                if (condition.Kind == RoomAccessConditionKind.SwitchActive
                    && !ReferenceRegistry.ContainsSwitch(condition.SubjectStableId))
                {
                    throw new ArgumentException(
                        "room-access-switch-reference-unknown:"
                        + condition.ConditionStableId
                        + ":"
                        + condition.SubjectStableId);
                }

                if (condition.Kind == RoomAccessConditionKind.CollectedDrop
                    && !ReferenceRegistry.ContainsCollectedDrop(condition.SubjectStableId))
                {
                    throw new ArgumentException(
                        "room-access-drop-reference-unknown:"
                        + condition.ConditionStableId
                        + ":"
                        + condition.SubjectStableId);
                }
            }
        }

        private void ValidateDoorReferences()
        {
            for (int index = 0; index < doors.Count; index++)
            {
                RoomDoorAccessDefinition door = doors[index];
                AuthorableRoomDefinition room;
                if (!RoomGraph.TryGetRoom(door.RoomStableId, out room))
                {
                    throw new ArgumentException(
                        "room-access-door-room-unknown:"
                        + door.DoorStableId
                        + ":"
                        + door.RoomStableId);
                }

                RoomDoorDefinition existingDoor;
                if (!room.TryGetDoor(door.DoorStableId, out existingDoor))
                {
                    throw new ArgumentException(
                        "room-access-door-unknown:"
                        + door.RoomStableId
                        + ":"
                        + door.DoorStableId);
                }

                if (!conditionsById.ContainsKey(door.RootConditionStableId))
                {
                    throw new ArgumentException(
                        "room-access-door-condition-unknown:"
                        + door.DoorStableId
                        + ":"
                        + door.RootConditionStableId);
                }

                if (door.ConsumeHoldingStableId != null
                    && !ReferenceRegistry.ContainsHolding(
                        door.ConsumeHoldingStableId))
                {
                    throw new ArgumentException(
                        "room-access-consume-holding-reference-unknown:"
                        + door.DoorStableId
                        + ":"
                        + door.ConsumeHoldingStableId);
                }
            }
        }

        private void ValidateAcyclic()
        {
            var states = new Dictionary<StableId, int>();
            for (int index = 0; index < conditions.Count; index++)
            {
                Visit(conditions[index].ConditionStableId, states);
            }
        }

        private void Visit(
            StableId conditionStableId,
            Dictionary<StableId, int> states)
        {
            int state;
            if (states.TryGetValue(conditionStableId, out state))
            {
                if (state == 1)
                {
                    throw new ArgumentException(
                        "room-access-condition-cycle:" + conditionStableId);
                }
                if (state == 2) return;
            }

            states[conditionStableId] = 1;
            RoomAccessConditionDefinition condition =
                conditionsById[conditionStableId];
            for (int index = 0;
                index < condition.ChildConditionStableIds.Count;
                index++)
            {
                Visit(condition.ChildConditionStableIds[index], states);
            }
            states[conditionStableId] = 2;
        }

        private string BuildCanonicalJson()
        {
            var builder = new StringBuilder();
            builder.Append("{\"version\":")
                .Append(CurrentSchemaVersion.ToString(CultureInfo.InvariantCulture))
                .Append(",\"layout\":");
            AppendString(builder, LayoutStableId.ToString());
            builder.Append(",\"reference_registry_fingerprint\":");
            AppendString(builder, ReferenceRegistryFingerprint);
            builder.Append(",\"conditions\":[");
            for (int index = 0; index < conditions.Count; index++)
            {
                if (index != 0) builder.Append(',');
                RoomAccessConditionDefinition value = conditions[index];
                builder.Append("{\"id\":");
                AppendString(builder, value.ConditionStableId.ToString());
                builder.Append(",\"kind\":");
                AppendString(builder, KindToken(value.Kind));
                builder.Append(",\"subject\":");
                AppendNullableId(builder, value.SubjectStableId);
                builder.Append(",\"minimum_difficulty\":")
                    .Append(value.MinimumDifficulty.ToString(
                        CultureInfo.InvariantCulture))
                    .Append(",\"children\":[");
                for (int childIndex = 0;
                    childIndex < value.ChildConditionStableIds.Count;
                    childIndex++)
                {
                    if (childIndex != 0) builder.Append(',');
                    AppendString(
                        builder,
                        value.ChildConditionStableIds[childIndex].ToString());
                }
                builder.Append("]}");
            }

            builder.Append("],\"doors\":[");
            for (int index = 0; index < doors.Count; index++)
            {
                if (index != 0) builder.Append(',');
                RoomDoorAccessDefinition value = doors[index];
                builder.Append("{\"room\":");
                AppendString(builder, value.RoomStableId.ToString());
                builder.Append(",\"door\":");
                AppendString(builder, value.DoorStableId.ToString());
                builder.Append(",\"condition\":");
                AppendString(builder, value.RootConditionStableId.ToString());
                builder.Append(",\"consume_holding\":");
                AppendNullableId(builder, value.ConsumeHoldingStableId);
                builder.Append('}');
            }
            builder.Append("]}");
            return builder.ToString();
        }

        private static string KindToken(RoomAccessConditionKind kind)
        {
            switch (kind)
            {
                case RoomAccessConditionKind.Always: return "always";
                case RoomAccessConditionKind.RoomEntered: return "room-entered";
                case RoomAccessConditionKind.RoomComplete: return "room-complete";
                case RoomAccessConditionKind.ExactEntityTerminal: return "exact-terminal";
                case RoomAccessConditionKind.HoldingPresent: return "holding-present";
                case RoomAccessConditionKind.HoldingConsumed: return "holding-consumed";
                case RoomAccessConditionKind.CollectedDrop: return "collected-drop";
                case RoomAccessConditionKind.ObjectiveComplete: return "objective-complete";
                case RoomAccessConditionKind.SwitchActive: return "switch-active";
                case RoomAccessConditionKind.DifficultyAtLeast: return "difficulty-at-least";
                case RoomAccessConditionKind.All: return "all";
                case RoomAccessConditionKind.Any: return "any";
                case RoomAccessConditionKind.Not: return "not";
                default:
                    throw new InvalidOperationException(
                        "Unsupported room access kind: " + kind);
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
                        "Room access collections cannot contain null values.",
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

        private static void AppendNullableId(
            StringBuilder builder,
            StableId value)
        {
            if (value == null) builder.Append("null");
            else AppendString(builder, value.ToString());
        }

        private static void AppendString(StringBuilder builder, string value)
        {
            builder.Append('"');
            string source = value ?? string.Empty;
            for (int index = 0; index < source.Length; index++)
            {
                char character = source[index];
                if (character == '"') builder.Append("\\\"");
                else if (character == '\\') builder.Append("\\\\");
                else if (character == '\n') builder.Append("\\n");
                else if (character == '\r') builder.Append("\\r");
                else if (character == '\t') builder.Append("\\t");
                else builder.Append(character);
            }
            builder.Append('"');
        }

        private static string ComputeSha256(string value)
        {
            using (SHA256 sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(
                    Encoding.UTF8.GetBytes(value ?? string.Empty));
                var builder = new StringBuilder(hash.Length * 2);
                for (int index = 0; index < hash.Length; index++)
                {
                    builder.Append(hash[index].ToString(
                        "x2",
                        CultureInfo.InvariantCulture));
                }
                return builder.ToString();
            }
        }
    }

    public sealed class RoomAccessFactSnapshot
    {
        private readonly ReadOnlyCollection<StableId> enteredRooms;
        private readonly ReadOnlyCollection<StableId> completedRooms;
        private readonly ReadOnlyCollection<StableId> terminalEntities;
        private readonly ReadOnlyCollection<StableId> collectedDrops;
        private readonly ReadOnlyCollection<StableId> completedObjectives;
        private readonly ReadOnlyCollection<StableId> activeSwitches;
        private readonly ReadOnlyCollection<StableId> consumedHoldings;

        public RoomAccessFactSnapshot(
            int difficulty,
            IEnumerable<StableId> enteredRooms,
            IEnumerable<StableId> completedRooms,
            IEnumerable<StableId> terminalEntities,
            IEnumerable<StableId> collectedDrops,
            IEnumerable<StableId> completedObjectives,
            IEnumerable<StableId> activeSwitches,
            IEnumerable<StableId> consumedHoldings)
        {
            if (difficulty < 0) throw new ArgumentOutOfRangeException(nameof(difficulty));
            Difficulty = difficulty;
            this.enteredRooms = CopyUnique(enteredRooms, nameof(enteredRooms));
            this.completedRooms = CopyUnique(completedRooms, nameof(completedRooms));
            this.terminalEntities = CopyUnique(terminalEntities, nameof(terminalEntities));
            this.collectedDrops = CopyUnique(collectedDrops, nameof(collectedDrops));
            this.completedObjectives = CopyUnique(
                completedObjectives,
                nameof(completedObjectives));
            this.activeSwitches = CopyUnique(activeSwitches, nameof(activeSwitches));
            this.consumedHoldings = CopyUnique(
                consumedHoldings,
                nameof(consumedHoldings));
            Fingerprint = BuildFingerprint();
        }

        public int Difficulty { get; }

        public IReadOnlyList<StableId> EnteredRooms => enteredRooms;

        public IReadOnlyList<StableId> CompletedRooms => completedRooms;

        public IReadOnlyList<StableId> TerminalEntities => terminalEntities;

        public IReadOnlyList<StableId> CollectedDrops => collectedDrops;

        public IReadOnlyList<StableId> CompletedObjectives => completedObjectives;

        public IReadOnlyList<StableId> ActiveSwitches => activeSwitches;

        public IReadOnlyList<StableId> ConsumedHoldings => consumedHoldings;

        public string Fingerprint { get; }

        public bool Contains(IReadOnlyList<StableId> values, StableId id)
        {
            if (id == null) return false;
            for (int index = 0; index < values.Count; index++)
            {
                if (values[index] == id) return true;
            }
            return false;
        }

        private string BuildFingerprint()
        {
            var builder = new StringBuilder();
            builder.Append(Difficulty.ToString(CultureInfo.InvariantCulture));
            Append(builder, enteredRooms);
            Append(builder, completedRooms);
            Append(builder, terminalEntities);
            Append(builder, collectedDrops);
            Append(builder, completedObjectives);
            Append(builder, activeSwitches);
            Append(builder, consumedHoldings);
            using (SHA256 sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(
                    Encoding.UTF8.GetBytes(builder.ToString()));
                var hex = new StringBuilder(hash.Length * 2);
                for (int index = 0; index < hash.Length; index++)
                {
                    hex.Append(hash[index].ToString(
                        "x2",
                        CultureInfo.InvariantCulture));
                }
                return hex.ToString();
            }
        }

        private static void Append(
            StringBuilder builder,
            IReadOnlyList<StableId> values)
        {
            builder.Append('|');
            for (int index = 0; index < values.Count; index++)
            {
                if (index != 0) builder.Append(',');
                builder.Append(values[index]);
            }
        }

        private static ReadOnlyCollection<StableId> CopyUnique(
            IEnumerable<StableId> source,
            string parameterName)
        {
            var copy = new List<StableId>(source ?? Array.Empty<StableId>());
            var seen = new HashSet<StableId>();
            for (int index = 0; index < copy.Count; index++)
            {
                if (copy[index] == null)
                {
                    throw new ArgumentException(
                        "Room access facts cannot contain null identities.",
                        parameterName);
                }
                if (!seen.Add(copy[index]))
                {
                    throw new ArgumentException(
                        "room-access-fact-duplicate:" + copy[index],
                        parameterName);
                }
            }
            copy.Sort();
            return new ReadOnlyCollection<StableId>(copy);
        }
    }

    public sealed class RoomRunHoldingSnapshot
    {
        private readonly ReadOnlyDictionary<StableId, int> quantities;

        public RoomRunHoldingSnapshot(IDictionary<StableId, int> quantities)
        {
            if (quantities == null) throw new ArgumentNullException(nameof(quantities));
            var copy = new Dictionary<StableId, int>();
            foreach (KeyValuePair<StableId, int> pair in quantities)
            {
                if (pair.Key == null || pair.Value < 0)
                {
                    throw new ArgumentException(
                        "Invalid run holding snapshot.",
                        nameof(quantities));
                }
                copy.Add(pair.Key, pair.Value);
            }
            this.quantities = new ReadOnlyDictionary<StableId, int>(copy);
        }

        public IReadOnlyDictionary<StableId, int> Quantities => quantities;

        public int GetQuantity(StableId holdingStableId)
        {
            if (holdingStableId == null)
            {
                throw new ArgumentNullException(nameof(holdingStableId));
            }
            int quantity;
            return quantities.TryGetValue(holdingStableId, out quantity)
                ? quantity
                : 0;
        }
    }

    public sealed class RoomHoldingConsumeCommand
    {
        public RoomHoldingConsumeCommand(
            StableId runtimeInstanceStableId,
            StableId operationStableId,
            StableId holdingStableId,
            int quantity)
        {
            RuntimeInstanceStableId = runtimeInstanceStableId
                ?? throw new ArgumentNullException(nameof(runtimeInstanceStableId));
            OperationStableId = operationStableId
                ?? throw new ArgumentNullException(nameof(operationStableId));
            HoldingStableId = holdingStableId
                ?? throw new ArgumentNullException(nameof(holdingStableId));
            if (quantity <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(quantity));
            }
            Quantity = quantity;
        }

        public StableId RuntimeInstanceStableId { get; }

        public StableId OperationStableId { get; }

        public StableId HoldingStableId { get; }

        public int Quantity { get; }
    }

    public sealed class RoomHoldingConsumeResult
    {
        public RoomHoldingConsumeResult(
            RoomHoldingConsumeStatus status,
            string rejectionCode)
        {
            if (!Enum.IsDefined(typeof(RoomHoldingConsumeStatus), status))
            {
                throw new ArgumentOutOfRangeException(nameof(status));
            }
            Status = status;
            RejectionCode = rejectionCode ?? string.Empty;
        }

        public RoomHoldingConsumeStatus Status { get; }

        public string RejectionCode { get; }

        public bool IsAccepted => Status != RoomHoldingConsumeStatus.Rejected;
    }

    public interface IRoomRunHoldingPort
    {
        RoomRunHoldingSnapshot CurrentSnapshot { get; }

        RoomHoldingConsumeResult Consume(RoomHoldingConsumeCommand command);
    }

    public interface IRoomAccessFactPort
    {
        RoomAccessFactSnapshot CurrentSnapshot { get; }
    }

    public sealed class UnlockRoomDoorCommand
    {
        public UnlockRoomDoorCommand(
            StableId runtimeInstanceStableId,
            StableId operationStableId,
            long lifecycleGeneration,
            StableId doorStableId)
        {
            RuntimeInstanceStableId = runtimeInstanceStableId
                ?? throw new ArgumentNullException(nameof(runtimeInstanceStableId));
            OperationStableId = operationStableId
                ?? throw new ArgumentNullException(nameof(operationStableId));
            DoorStableId = doorStableId
                ?? throw new ArgumentNullException(nameof(doorStableId));
            if (lifecycleGeneration <= 0L)
            {
                throw new ArgumentOutOfRangeException(nameof(lifecycleGeneration));
            }
            LifecycleGeneration = lifecycleGeneration;
        }

        public StableId RuntimeInstanceStableId { get; }

        public StableId OperationStableId { get; }

        public long LifecycleGeneration { get; }

        public StableId DoorStableId { get; }
    }

    public sealed class RoomDoorAccessView
    {
        public RoomDoorAccessView(
            StableId roomStableId,
            StableId doorStableId,
            bool isConditionSatisfied,
            bool isUnlocked,
            bool isOpen)
        {
            RoomStableId = roomStableId
                ?? throw new ArgumentNullException(nameof(roomStableId));
            DoorStableId = doorStableId
                ?? throw new ArgumentNullException(nameof(doorStableId));
            IsConditionSatisfied = isConditionSatisfied;
            IsUnlocked = isUnlocked;
            IsOpen = isOpen;
        }

        public StableId RoomStableId { get; }

        public StableId DoorStableId { get; }

        public bool IsConditionSatisfied { get; }

        public bool IsUnlocked { get; }

        public bool IsOpen { get; }
    }

    public sealed class RoomAccessSnapshot
    {
        private readonly ReadOnlyCollection<RoomDoorAccessView> doors;

        public RoomAccessSnapshot(
            StableId runtimeInstanceStableId,
            string definitionFingerprint,
            long lifecycleGeneration,
            long sequence,
            string sourceFingerprint,
            IEnumerable<RoomDoorAccessView> doors)
        {
            RuntimeInstanceStableId = runtimeInstanceStableId
                ?? throw new ArgumentNullException(nameof(runtimeInstanceStableId));
            DefinitionFingerprint = definitionFingerprint ?? string.Empty;
            if (lifecycleGeneration <= 0L)
            {
                throw new ArgumentOutOfRangeException(nameof(lifecycleGeneration));
            }
            if (sequence < 0L)
            {
                throw new ArgumentOutOfRangeException(nameof(sequence));
            }
            LifecycleGeneration = lifecycleGeneration;
            Sequence = sequence;
            SourceFingerprint = sourceFingerprint ?? string.Empty;
            var copy = new List<RoomDoorAccessView>(
                doors ?? Array.Empty<RoomDoorAccessView>());
            copy.Sort((left, right) => left.DoorStableId.CompareTo(
                right.DoorStableId));
            this.doors = new ReadOnlyCollection<RoomDoorAccessView>(copy);
        }

        public StableId RuntimeInstanceStableId { get; }

        public string DefinitionFingerprint { get; }

        public long LifecycleGeneration { get; }

        public long Sequence { get; }

        public string SourceFingerprint { get; }

        public IReadOnlyList<RoomDoorAccessView> Doors => doors;

        public RoomDoorAccessView GetDoor(StableId doorStableId)
        {
            if (doorStableId == null)
            {
                throw new ArgumentNullException(nameof(doorStableId));
            }
            for (int index = 0; index < doors.Count; index++)
            {
                if (doors[index].DoorStableId == doorStableId)
                {
                    return doors[index];
                }
            }
            throw new KeyNotFoundException(
                "Unknown access door identity: " + doorStableId);
        }
    }

    public sealed class RoomAccessOperationResult
    {
        public RoomAccessOperationResult(
            RoomAccessOperationStatus status,
            string rejectionCode,
            RoomAccessSnapshot snapshot)
        {
            if (!Enum.IsDefined(typeof(RoomAccessOperationStatus), status))
            {
                throw new ArgumentOutOfRangeException(nameof(status));
            }
            Status = status;
            RejectionCode = rejectionCode ?? string.Empty;
            Snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
        }

        public RoomAccessOperationStatus Status { get; }

        public string RejectionCode { get; }

        public RoomAccessSnapshot Snapshot { get; }
    }
}
