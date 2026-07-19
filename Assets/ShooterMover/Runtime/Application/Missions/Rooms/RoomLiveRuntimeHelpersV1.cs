using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using ShooterMover.Contracts.Missions.Rooms;
using ShooterMover.Domain.Common;

namespace ShooterMover.Application.Missions.Rooms
{
    internal enum RoomOperationInspectionV1
    {
        New = 1,
        Duplicate = 2,
        Conflict = 3,
    }

    internal sealed class RoomOperationJournalV1
    {
        private readonly Dictionary<StableId, string> payloads =
            new Dictionary<StableId, string>();

        public RoomOperationInspectionV1 Inspect(
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
                return RoomOperationInspectionV1.New;
            }

            return string.Equals(existing, payload, StringComparison.Ordinal)
                ? RoomOperationInspectionV1.Duplicate
                : RoomOperationInspectionV1.Conflict;
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

    internal sealed class RoomRetainedFactStoreV1
    {
        private readonly Dictionary<StableId, HashSet<StableId>> collectedDropsByRoom =
            new Dictionary<StableId, HashSet<StableId>>();
        private readonly Dictionary<StableId, HashSet<StableId>> openedDoorsByRoom =
            new Dictionary<StableId, HashSet<StableId>>();

        public RoomRetainedFactStoreV1(AuthorableRoomGraphDefinitionV1 definition)
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

    internal sealed class RoomCompletionEvaluationV1
    {
        private readonly ReadOnlyCollection<StableId> satisfiedConditionStableIds;

        public RoomCompletionEvaluationV1(
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

    internal sealed class RoomCompletionEvaluatorV1
    {
        public RoomCompletionEvaluationV1 Evaluate(
            AuthorableRoomDefinitionV1 room,
            RoomOccupancyProjectionV1 occupancy,
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
                RoomCompletionConditionDefinitionV1 condition =
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

            return new RoomCompletionEvaluationV1(
                satisfied,
                roomCompletionSatisfied);
        }

        private static bool EvaluateCondition(
            RoomCompletionConditionDefinitionV1 condition,
            RoomOccupancyProjectionV1 occupancy,
            IReadOnlyCollection<StableId> collectedDrops)
        {
            switch (condition.Kind)
            {
                case RoomCompletionConditionKindV1.AlwaysSatisfied:
                    return true;
                case RoomCompletionConditionKindV1.AllBlockingOccupantsTerminal:
                    return occupancy.IsCleared;
                case RoomCompletionConditionKindV1.CollectedDrop:
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

    internal sealed class RoomDoorGatePolicyV1
    {
        public IReadOnlyList<StableId> EvaluateOpenDoors(
            AuthorableRoomDefinitionV1 room,
            RoomCompletionEvaluationV1 completion,
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
                RoomDoorDefinitionV1 door = room.Doors[doorIndex];
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

    internal sealed class RoomLiveProjectionBuilderV1
    {
        public RoomLiveRuntimeProjectionV1 Build(
            StableId runtimeInstanceStableId,
            AuthorableRoomGraphDefinitionV1 definition,
            RoomRuntimeAuthorityV1 occupancyAuthority,
            RoomMissionLayoutV1 missionLayout,
            RoomRetainedFactStoreV1 retainedFacts,
            IReadOnlyDictionary<StableId, RoomCompletionEvaluationV1> evaluations,
            long sequence,
            StableId currentSpawnPointStableId,
            bool finalExitReached)
        {
            var rooms = new List<RoomLiveRoomProjectionV1>();
            for (int roomIndex = 0; roomIndex < definition.Rooms.Count; roomIndex++)
            {
                AuthorableRoomDefinitionV1 room = definition.Rooms[roomIndex];
                RoomOccupancyProjectionV1 occupancy =
                    occupancyAuthority.GetRoomProjection(room.RoomStableId);
                RoomRuntimeStateV1 layout = missionLayout.GetRoomState(
                    room.RoomStableId);
                var active = new List<RoomOccupantProjectionV1>();
                var defeated = new List<RoomOccupantProjectionV1>();
                for (int occupantIndex = 0;
                    occupantIndex < occupancy.Occupants.Count;
                    occupantIndex++)
                {
                    RoomOccupantProjectionV1 occupant = occupancy.Occupants[occupantIndex];
                    if (occupant.IsTerminal) defeated.Add(occupant);
                    else active.Add(occupant);
                }

                RoomCompletionEvaluationV1 evaluation =
                    evaluations[room.RoomStableId];
                rooms.Add(new RoomLiveRoomProjectionV1(
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

            return new RoomLiveRuntimeProjectionV1(
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
