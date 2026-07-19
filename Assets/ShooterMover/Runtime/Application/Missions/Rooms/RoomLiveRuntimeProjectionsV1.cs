using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using ShooterMover.Contracts.Missions.Rooms;
using ShooterMover.Domain.Common;

namespace ShooterMover.Application.Missions.Rooms
{
    public enum RoomLiveOperationStatusV1
    {
        Applied = 1,
        DuplicateNoChange = 2,
        NoChange = 3,
        Rejected = 4,
        FinalExitReached = 5,
    }

    public interface IRoomLiveRuntimeQueryV1
    {
        StableId RuntimeInstanceStableId { get; }

        AuthorableRoomGraphDefinitionV1 Definition { get; }

        RoomLiveRuntimeProjectionV1 CurrentProjection { get; }

        RoomLiveRoomProjectionV1 GetRoomProjection(StableId roomStableId);
    }

    public sealed class RoomLiveRoomProjectionV1
    {
        private readonly ReadOnlyCollection<RoomOccupantProjectionV1> activeOccupants;
        private readonly ReadOnlyCollection<RoomOccupantProjectionV1> defeatedOccupants;
        private readonly ReadOnlyCollection<StableId> satisfiedConditionStableIds;
        private readonly ReadOnlyCollection<StableId> collectedDropInstanceStableIds;
        private readonly ReadOnlyCollection<StableId> openedDoorInstanceStableIds;

        public RoomLiveRoomProjectionV1(
            StableId roomStableId,
            string displayName,
            bool isActive,
            bool isCurrent,
            bool isVisited,
            bool isCleared,
            bool isCompleted,
            IEnumerable<RoomOccupantProjectionV1> activeOccupants,
            IEnumerable<RoomOccupantProjectionV1> defeatedOccupants,
            IEnumerable<StableId> satisfiedConditionStableIds,
            IEnumerable<StableId> collectedDropInstanceStableIds,
            IEnumerable<StableId> openedDoorInstanceStableIds)
        {
            RoomStableId = roomStableId
                ?? throw new ArgumentNullException(nameof(roomStableId));
            DisplayName = displayName ?? string.Empty;
            IsActive = isActive;
            IsCurrent = isCurrent;
            IsVisited = isVisited;
            IsCleared = isCleared;
            IsCompleted = isCompleted;
            this.activeOccupants = CopyOccupants(activeOccupants);
            this.defeatedOccupants = CopyOccupants(defeatedOccupants);
            this.satisfiedConditionStableIds = CopyIds(
                satisfiedConditionStableIds);
            this.collectedDropInstanceStableIds = CopyIds(
                collectedDropInstanceStableIds);
            this.openedDoorInstanceStableIds = CopyIds(
                openedDoorInstanceStableIds);
        }

        public StableId RoomStableId { get; }

        public string DisplayName { get; }

        public bool IsActive { get; }

        public bool IsCurrent { get; }

        public bool IsVisited { get; }

        public bool IsCleared { get; }

        public bool IsCompleted { get; }

        public IReadOnlyList<RoomOccupantProjectionV1> ActiveOccupants
        {
            get { return activeOccupants; }
        }

        public IReadOnlyList<RoomOccupantProjectionV1> DefeatedOccupants
        {
            get { return defeatedOccupants; }
        }

        public IReadOnlyList<StableId> SatisfiedConditionStableIds
        {
            get { return satisfiedConditionStableIds; }
        }

        public IReadOnlyList<StableId> CollectedDropInstanceStableIds
        {
            get { return collectedDropInstanceStableIds; }
        }

        public IReadOnlyList<StableId> OpenedDoorInstanceStableIds
        {
            get { return openedDoorInstanceStableIds; }
        }

        public bool IsConditionSatisfied(StableId conditionStableId)
        {
            return Contains(satisfiedConditionStableIds, conditionStableId);
        }

        public bool IsDoorOpen(StableId doorInstanceStableId)
        {
            return Contains(openedDoorInstanceStableIds, doorInstanceStableId);
        }

        public bool IsDropCollected(StableId dropInstanceStableId)
        {
            return Contains(collectedDropInstanceStableIds, dropInstanceStableId);
        }

        private static bool Contains(
            IReadOnlyList<StableId> values,
            StableId stableId)
        {
            if (stableId == null) return false;
            for (int index = 0; index < values.Count; index++)
            {
                if (values[index] == stableId) return true;
            }

            return false;
        }

        private static ReadOnlyCollection<RoomOccupantProjectionV1> CopyOccupants(
            IEnumerable<RoomOccupantProjectionV1> source)
        {
            var copy = new List<RoomOccupantProjectionV1>(
                source ?? Array.Empty<RoomOccupantProjectionV1>());
            copy.Sort((left, right) => left.EntityStableId.CompareTo(
                right.EntityStableId));
            return new ReadOnlyCollection<RoomOccupantProjectionV1>(copy);
        }

        private static ReadOnlyCollection<StableId> CopyIds(IEnumerable<StableId> source)
        {
            var copy = new List<StableId>(source ?? Array.Empty<StableId>());
            copy.Sort();
            return new ReadOnlyCollection<StableId>(copy);
        }
    }

    public sealed class RoomLiveRuntimeProjectionV1
    {
        private readonly ReadOnlyCollection<RoomLiveRoomProjectionV1> rooms;

        public RoomLiveRuntimeProjectionV1(
            StableId runtimeInstanceStableId,
            string definitionFingerprint,
            long lifecycleGeneration,
            long sequence,
            StableId currentRoomStableId,
            StableId currentSpawnPointStableId,
            bool finalExitReached,
            IEnumerable<RoomLiveRoomProjectionV1> rooms)
        {
            RuntimeInstanceStableId = runtimeInstanceStableId
                ?? throw new ArgumentNullException(nameof(runtimeInstanceStableId));
            DefinitionFingerprint = definitionFingerprint ?? string.Empty;
            if (lifecycleGeneration <= 0L)
            {
                throw new ArgumentOutOfRangeException(nameof(lifecycleGeneration));
            }

            if (sequence < 0L) throw new ArgumentOutOfRangeException(nameof(sequence));
            CurrentRoomStableId = currentRoomStableId
                ?? throw new ArgumentNullException(nameof(currentRoomStableId));
            CurrentSpawnPointStableId = currentSpawnPointStableId
                ?? throw new ArgumentNullException(nameof(currentSpawnPointStableId));
            LifecycleGeneration = lifecycleGeneration;
            Sequence = sequence;
            FinalExitReached = finalExitReached;
            var copy = new List<RoomLiveRoomProjectionV1>(
                rooms ?? throw new ArgumentNullException(nameof(rooms)));
            copy.Sort((left, right) => left.RoomStableId.CompareTo(
                right.RoomStableId));
            this.rooms = new ReadOnlyCollection<RoomLiveRoomProjectionV1>(copy);
            Fingerprint = BuildFingerprint();
        }

        public StableId RuntimeInstanceStableId { get; }

        public string DefinitionFingerprint { get; }

        public long LifecycleGeneration { get; }

        public long Sequence { get; }

        public StableId CurrentRoomStableId { get; }

        public StableId CurrentSpawnPointStableId { get; }

        public bool FinalExitReached { get; }

        public IReadOnlyList<RoomLiveRoomProjectionV1> Rooms
        {
            get { return rooms; }
        }

        public string Fingerprint { get; }

        public RoomLiveRoomProjectionV1 GetRoom(StableId roomStableId)
        {
            if (roomStableId == null)
            {
                throw new ArgumentNullException(nameof(roomStableId));
            }

            for (int index = 0; index < rooms.Count; index++)
            {
                if (rooms[index].RoomStableId == roomStableId)
                {
                    return rooms[index];
                }
            }

            throw new KeyNotFoundException(
                "Unknown live room projection: " + roomStableId);
        }

        private string BuildFingerprint()
        {
            var builder = new StringBuilder();
            builder.Append(RuntimeInstanceStableId)
                .Append('|')
                .Append(DefinitionFingerprint)
                .Append('|')
                .Append(LifecycleGeneration.ToString(CultureInfo.InvariantCulture))
                .Append('|')
                .Append(Sequence.ToString(CultureInfo.InvariantCulture))
                .Append('|')
                .Append(CurrentRoomStableId)
                .Append('|')
                .Append(CurrentSpawnPointStableId)
                .Append('|')
                .Append(FinalExitReached ? '1' : '0');
            for (int roomIndex = 0; roomIndex < rooms.Count; roomIndex++)
            {
                RoomLiveRoomProjectionV1 room = rooms[roomIndex];
                builder.Append("|room:")
                    .Append(room.RoomStableId)
                    .Append(':')
                    .Append(room.IsActive ? '1' : '0')
                    .Append(':')
                    .Append(room.IsCurrent ? '1' : '0')
                    .Append(':')
                    .Append(room.IsVisited ? '1' : '0')
                    .Append(':')
                    .Append(room.IsCleared ? '1' : '0')
                    .Append(':')
                    .Append(room.IsCompleted ? '1' : '0');
                AppendOccupants(builder, "active", room.ActiveOccupants);
                AppendOccupants(builder, "defeated", room.DefeatedOccupants);
                AppendIds(builder, "condition", room.SatisfiedConditionStableIds);
                AppendIds(builder, "drop", room.CollectedDropInstanceStableIds);
                AppendIds(builder, "door", room.OpenedDoorInstanceStableIds);
            }

            return ComputeSha256(builder.ToString());
        }

        private static void AppendOccupants(
            StringBuilder builder,
            string prefix,
            IReadOnlyList<RoomOccupantProjectionV1> occupants)
        {
            for (int index = 0; index < occupants.Count; index++)
            {
                builder.Append('|')
                    .Append(prefix)
                    .Append(':')
                    .Append(occupants[index].EntityStableId);
            }
        }

        private static void AppendIds(
            StringBuilder builder,
            string prefix,
            IReadOnlyList<StableId> ids)
        {
            for (int index = 0; index < ids.Count; index++)
            {
                builder.Append('|')
                    .Append(prefix)
                    .Append(':')
                    .Append(ids[index]);
            }
        }

        private static string ComputeSha256(string value)
        {
            using (System.Security.Cryptography.SHA256 sha =
                System.Security.Cryptography.SHA256.Create())
            {
                byte[] bytes = System.Text.Encoding.UTF8.GetBytes(value);
                byte[] hash = sha.ComputeHash(bytes);
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

    public sealed class RoomLiveOperationResultV1
    {
        public RoomLiveOperationResultV1(
            RoomLiveOperationStatusV1 status,
            string rejectionCode,
            RoomLiveRuntimeProjectionV1 previousProjection,
            RoomLiveRuntimeProjectionV1 currentProjection,
            StableId traversedExitStableId,
            StableId targetRoomStableId,
            StableId targetSpawnPointStableId)
        {
            if (!Enum.IsDefined(typeof(RoomLiveOperationStatusV1), status))
            {
                throw new ArgumentOutOfRangeException(nameof(status));
            }

            Status = status;
            RejectionCode = rejectionCode ?? string.Empty;
            PreviousProjection = previousProjection
                ?? throw new ArgumentNullException(nameof(previousProjection));
            CurrentProjection = currentProjection
                ?? throw new ArgumentNullException(nameof(currentProjection));
            TraversedExitStableId = traversedExitStableId;
            TargetRoomStableId = targetRoomStableId;
            TargetSpawnPointStableId = targetSpawnPointStableId;
        }

        public RoomLiveOperationStatusV1 Status { get; }

        public string RejectionCode { get; }

        public RoomLiveRuntimeProjectionV1 PreviousProjection { get; }

        public RoomLiveRuntimeProjectionV1 CurrentProjection { get; }

        public StableId TraversedExitStableId { get; }

        public StableId TargetRoomStableId { get; }

        public StableId TargetSpawnPointStableId { get; }

        public bool Changed
        {
            get
            {
                return Status == RoomLiveOperationStatusV1.Applied
                    || Status == RoomLiveOperationStatusV1.FinalExitReached;
            }
        }
    }
}
