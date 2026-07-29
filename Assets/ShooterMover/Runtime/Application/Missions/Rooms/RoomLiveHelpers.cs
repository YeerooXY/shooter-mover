using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using ShooterMover.Contracts.Missions.Rooms;
using ShooterMover.Domain.Common;

namespace ShooterMover.Application.Missions.Rooms
{
    internal enum RoomOperationInspection
    {
        New = 1,
        Duplicate = 2,
        Conflict = 3,
    }

    internal sealed class RoomOperationJournal
    {
        private readonly Dictionary<StableId, string> payloads =
            new Dictionary<StableId, string>();

        public RoomOperationInspection Inspect(
            StableId operationStableId,
            string payload)
        {
            if (operationStableId == null)
            {
                throw new ArgumentNullException(nameof(operationStableId));
            }

            string existing;
            if (!payloads.TryGetValue(operationStableId, out existing))
            {
                return RoomOperationInspection.New;
            }

            return string.Equals(existing, payload, StringComparison.Ordinal)
                ? RoomOperationInspection.Duplicate
                : RoomOperationInspection.Conflict;
        }

        public void Record(StableId operationStableId, string payload)
        {
            if (operationStableId == null)
            {
                throw new ArgumentNullException(nameof(operationStableId));
            }

            if (!payloads.ContainsKey(operationStableId))
            {
                payloads.Add(operationStableId, payload ?? string.Empty);
            }
        }
    }

    internal sealed class RoomRetainedFactStore
    {
        private readonly Dictionary<StableId, HashSet<StableId>> collectedDropsByRoom =
            new Dictionary<StableId, HashSet<StableId>>();
        private readonly Dictionary<StableId, HashSet<StableId>> openedDoorsByRoom =
            new Dictionary<StableId, HashSet<StableId>>();

        public RoomRetainedFactStore(AuthorableRoomGraphDefinition definition)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            for (int index = 0; index < definition.Rooms.Count; index++)
            {
                StableId roomId = definition.Rooms[index].RoomStableId;
                collectedDropsByRoom.Add(roomId, new HashSet<StableId>());
                openedDoorsByRoom.Add(roomId, new HashSet<StableId>());
            }
        }

        public bool AddCollectedDrop(StableId roomStableId, StableId dropStableId)
        {
            if (dropStableId == null)
            {
                throw new ArgumentNullException(nameof(dropStableId));
            }

            return Get(collectedDropsByRoom, roomStableId).Add(dropStableId);
        }

        public bool OpenDoor(StableId roomStableId, StableId doorStableId)
        {
            if (doorStableId == null)
            {
                throw new ArgumentNullException(nameof(doorStableId));
            }

            return Get(openedDoorsByRoom, roomStableId).Add(doorStableId);
        }

        public bool IsDoorOpen(StableId roomStableId, StableId doorStableId)
        {
            return doorStableId != null
                && Get(openedDoorsByRoom, roomStableId).Contains(doorStableId);
        }

        public IReadOnlyCollection<StableId> GetCollectedDrops(StableId roomStableId)
        {
            return new ReadOnlyCollection<StableId>(
                Sorted(Get(collectedDropsByRoom, roomStableId)));
        }

        public IReadOnlyCollection<StableId> GetOpenedDoors(StableId roomStableId)
        {
            return new ReadOnlyCollection<StableId>(
                Sorted(Get(openedDoorsByRoom, roomStableId)));
        }

        public void Clear()
        {
            foreach (HashSet<StableId> values in collectedDropsByRoom.Values)
            {
                values.Clear();
            }

            foreach (HashSet<StableId> values in openedDoorsByRoom.Values)
            {
                values.Clear();
            }
        }

        private static HashSet<StableId> Get(
            Dictionary<StableId, HashSet<StableId>> values,
            StableId roomStableId)
        {
            if (roomStableId == null)
            {
                throw new ArgumentNullException(nameof(roomStableId));
            }

            HashSet<StableId> result;
            if (!values.TryGetValue(roomStableId, out result))
            {
                throw new KeyNotFoundException(
                    "Unknown retained room identity: " + roomStableId);
            }

            return result;
        }

        private static List<StableId> Sorted(IEnumerable<StableId> values)
        {
            var result = new List<StableId>(values);
            result.Sort();
            return result;
        }
    }

    internal sealed class RoomCompletionEvaluation
    {
        private readonly ReadOnlyCollection<StableId> satisfiedConditionStableIds;

        public RoomCompletionEvaluation(
            IEnumerable<StableId> satisfiedConditionStableIds,
            bool isRoomCompletionSatisfied)
        {
            var copy = new List<StableId>(
                satisfiedConditionStableIds ?? Array.Empty<StableId>());
            copy.Sort();
            this.satisfiedConditionStableIds =
                new ReadOnlyCollection<StableId>(copy);
            IsRoomCompletionSatisfied = isRoomCompletionSatisfied;
        }

        public IReadOnlyList<StableId> SatisfiedConditionStableIds
        {
            get { return satisfiedConditionStableIds; }
        }

        public bool IsRoomCompletionSatisfied { get; }

        public bool IsSatisfied(StableId conditionStableId)
        {
            if (conditionStableId == null) return false;
            for (int index = 0; index < satisfiedConditionStableIds.Count; index++)
            {
                if (satisfiedConditionStableIds[index] == conditionStableId)
                {
                    return true;
                }
            }

            return false;
        }
    }

    internal sealed class RoomCompletionEvaluator
    {
        public RoomCompletionEvaluation Evaluate(
            AuthorableRoomDefinition room,
            RoomOccupancyView occupancy,
            IReadOnlyCollection<StableId> collectedDrops)
        {
            if (room == null) throw new ArgumentNullException(nameof(room));
            if (occupancy == null) throw new ArgumentNullException(nameof(occupancy));
            if (collectedDrops == null)
            {
                throw new ArgumentNullException(nameof(collectedDrops));
            }

            var satisfied = new List<StableId>();
            bool roomCompletionSatisfied = true;
            for (int index = 0; index < room.CompletionConditions.Count; index++)
            {
                RoomCompletionConditionDefinition condition =
                    room.CompletionConditions[index];
                bool isSatisfied = EvaluateCondition(
                    condition,
                    occupancy,
                    collectedDrops);
                if (isSatisfied)
                {
                    satisfied.Add(condition.ConditionStableId);
                }
                else if (condition.IsRequiredForRoomCompletion)
                {
                    roomCompletionSatisfied = false;
                }
            }

            return new RoomCompletionEvaluation(
                satisfied,
                roomCompletionSatisfied);
        }

        private static bool EvaluateCondition(
            RoomCompletionConditionDefinition condition,
            RoomOccupancyView occupancy,
            IReadOnlyCollection<StableId> collectedDrops)
        {
            switch (condition.Kind)
            {
                case RoomCompletionConditionKind.AlwaysSatisfied:
                    return true;
                case RoomCompletionConditionKind.AllBlockingOccupantsTerminal:
                    return occupancy.IsCleared;
                case RoomCompletionConditionKind.CollectedDrop:
                    foreach (StableId drop in collectedDrops)
                    {
                        if (drop == condition.SubjectStableId) return true;
                    }

                    return false;
                default:
                    throw new InvalidOperationException(
                        "room-live-completion-kind-unsupported:" + condition.Kind);
            }
        }
    }

    internal sealed class RoomDoorGatePolicy
    {
        public IReadOnlyList<StableId> EvaluateOpenDoors(
            AuthorableRoomDefinition room,
            RoomCompletionEvaluation completion,
            bool isVisited)
        {
            if (room == null) throw new ArgumentNullException(nameof(room));
            if (completion == null)
            {
                throw new ArgumentNullException(nameof(completion));
            }

            var result = new List<StableId>();
            if (!isVisited) return new ReadOnlyCollection<StableId>(result);

            for (int doorIndex = 0; doorIndex < room.Doors.Count; doorIndex++)
            {
                RoomDoorDefinition door = room.Doors[doorIndex];
                bool allSatisfied = true;
                for (int conditionIndex = 0;
                    conditionIndex < door.RequiredConditionStableIds.Count;
                    conditionIndex++)
                {
                    if (!completion.IsSatisfied(
                        door.RequiredConditionStableIds[conditionIndex]))
                    {
                        allSatisfied = false;
                        break;
                    }
                }

                if (allSatisfied)
                {
                    result.Add(door.DoorInstanceStableId);
                }
            }

            result.Sort();
            return new ReadOnlyCollection<StableId>(result);
        }
    }

    internal sealed class RoomLiveViewBuilder
    {
        public RoomLiveView Build(
            StableId runtimeInstanceStableId,
            AuthorableRoomGraphDefinition definition,
            RoomOccupancy occupancyAuthority,
            RoomMissionLayout missionLayout,
            RoomRetainedFactStore retainedFacts,
            IReadOnlyDictionary<StableId, RoomCompletionEvaluation> evaluations,
            long sequence,
            StableId currentSpawnPointStableId,
            bool finalExitReached)
        {
            var rooms = new List<RoomLiveRoomView>();
            for (int roomIndex = 0; roomIndex < definition.Rooms.Count; roomIndex++)
            {
                AuthorableRoomDefinition room = definition.Rooms[roomIndex];
                RoomOccupancyView occupancy =
                    occupancyAuthority.GetRoomProjection(room.RoomStableId);
                RoomLiveState layout = missionLayout.GetRoomState(
                    room.RoomStableId);
                var active = new List<RoomOccupantView>();
                var defeated = new List<RoomOccupantView>();
                for (int occupantIndex = 0;
                    occupantIndex < occupancy.Occupants.Count;
                    occupantIndex++)
                {
                    RoomOccupantView occupant = occupancy.Occupants[occupantIndex];
                    if (occupant.IsTerminal) defeated.Add(occupant);
                    else active.Add(occupant);
                }

                RoomCompletionEvaluation evaluation =
                    evaluations[room.RoomStableId];
                rooms.Add(new RoomLiveRoomView(
                    room.RoomStableId,
                    room.DisplayName,
                    occupancy.IsActive,
                    layout.IsCurrent,
                    layout.IsVisited,
                    occupancy.IsCleared,
                    layout.IsCompleted,
                    active,
                    defeated,
                    evaluation.SatisfiedConditionStableIds,
                    retainedFacts.GetCollectedDrops(room.RoomStableId),
                    retainedFacts.GetOpenedDoors(room.RoomStableId)));
            }

            return new RoomLiveView(
                runtimeInstanceStableId,
                definition.Fingerprint,
                occupancyAuthority.CurrentProjection.LifecycleGeneration,
                sequence,
                missionLayout.CurrentRoomState.RoomStableId,
                currentSpawnPointStableId,
                finalExitReached,
                rooms);
        }
    }
}
