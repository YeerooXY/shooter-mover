using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Missions.Rooms;

namespace ShooterMover.Contracts.Missions.Rooms
{
    public enum RoomAvailabilityState
    {
        Locked = 1,
        Available = 2,
    }

    public enum RoomGraphOperationStatus
    {
        Applied = 1,
        NoChange = 2,
        UnknownExit = 3,
        ExitNotFromCurrentRoom = 4,
        ExitLocked = 5,
        TargetRoomLocked = 6,
    }

    public enum RoomGraphImportStatus
    {
        Imported = 1,
        DuplicateNoChange = 2,
        NullSnapshot = 3,
        UnsupportedSchemaVersion = 4,
        LayoutMismatch = 5,
        DefinitionFingerprintMismatch = 6,
        FingerprintMismatch = 7,
        ValidationRejected = 8,
    }

    public sealed class RoomLiveState
    {
        public RoomLiveState(
            StableId roomStableId,
            RoomAvailabilityState availability,
            bool isCurrent,
            bool isVisited,
            bool isCompleted)
        {
            RoomStableId = roomStableId
                ?? throw new ArgumentNullException(nameof(roomStableId));
            if (!Enum.IsDefined(typeof(RoomAvailabilityState), availability))
            {
                throw new ArgumentOutOfRangeException(nameof(availability));
            }

            Availability = availability;
            IsCurrent = isCurrent;
            IsVisited = isVisited;
            IsCompleted = isCompleted;
        }

        public StableId RoomStableId { get; }

        public RoomAvailabilityState Availability { get; }

        public bool IsCurrent { get; }

        public bool IsVisited { get; }

        public bool IsCompleted { get; }

        public RoomLiveState With(
            RoomAvailabilityState availability,
            bool isCurrent,
            bool isVisited,
            bool isCompleted)
        {
            return new RoomLiveState(
                RoomStableId,
                availability,
                isCurrent,
                isVisited,
                isCompleted);
        }
    }

    public sealed class RoomExitLiveState
    {
        public RoomExitLiveState(
            StableId exitStableId,
            bool isAvailable)
        {
            ExitStableId = exitStableId
                ?? throw new ArgumentNullException(nameof(exitStableId));
            IsAvailable = isAvailable;
        }

        public StableId ExitStableId { get; }

        public bool IsAvailable { get; }

        public RoomExitLiveState WithAvailability(bool isAvailable)
        {
            return new RoomExitLiveState(
                ExitStableId,
                isAvailable);
        }
    }

    /// <summary>
    /// Raw persistence record. String identities are parsed and validated before
    /// application state is changed, so malformed external data fails closed.
    /// </summary>
    public sealed class RoomStateSnapshot
    {
        public RoomStateSnapshot(
            string roomStableId,
            int availability,
            bool isCurrent,
            bool isVisited,
            bool isCompleted)
        {
            RoomStableId = roomStableId;
            Availability = availability;
            IsCurrent = isCurrent;
            IsVisited = isVisited;
            IsCompleted = isCompleted;
        }

        public string RoomStableId { get; }

        public int Availability { get; }

        public bool IsCurrent { get; }

        public bool IsVisited { get; }

        public bool IsCompleted { get; }

        public string ToCanonicalString()
        {
            var builder = new StringBuilder();
            RoomGraphFormat.AppendToken(
                builder,
                "room_stable_id",
                RoomStableId);
            RoomGraphFormat.AppendToken(
                builder,
                "availability",
                Availability.ToString(CultureInfo.InvariantCulture));
            RoomGraphFormat.AppendToken(
                builder,
                "is_current",
                IsCurrent ? "1" : "0");
            RoomGraphFormat.AppendToken(
                builder,
                "is_visited",
                IsVisited ? "1" : "0");
            RoomGraphFormat.AppendToken(
                builder,
                "is_completed",
                IsCompleted ? "1" : "0");
            return builder.ToString();
        }
    }

    public sealed class RoomExitStateSnapshot
    {
        public RoomExitStateSnapshot(
            string exitStableId,
            bool isAvailable)
        {
            ExitStableId = exitStableId;
            IsAvailable = isAvailable;
        }

        public string ExitStableId { get; }

        public bool IsAvailable { get; }

        public string ToCanonicalString()
        {
            var builder = new StringBuilder();
            RoomGraphFormat.AppendToken(
                builder,
                "exit_stable_id",
                ExitStableId);
            RoomGraphFormat.AppendToken(
                builder,
                "is_available",
                IsAvailable ? "1" : "0");
            return builder.ToString();
        }
    }

    public sealed class RoomGraphSnapshot
    {
        private const string SchemaId = "room-graph-snapshot-v1";
        private readonly ReadOnlyCollection<RoomStateSnapshot> rooms;
        private readonly ReadOnlyCollection<RoomExitStateSnapshot> exits;

        public const int CurrentSchemaVersion = 1;

        public RoomGraphSnapshot(
            int schemaVersion,
            string layoutStableId,
            string definitionFingerprint,
            long sequence,
            IEnumerable<RoomStateSnapshot> rooms,
            IEnumerable<RoomExitStateSnapshot> exits,
            string fingerprint)
        {
            SchemaVersion = schemaVersion;
            LayoutStableId = layoutStableId;
            DefinitionFingerprint = definitionFingerprint;
            Sequence = sequence;
            this.rooms = CopyAndOrderRooms(rooms);
            this.exits = CopyAndOrderExits(exits);
            Fingerprint = fingerprint;
        }

        public int SchemaVersion { get; }

        public string LayoutStableId { get; }

        public string DefinitionFingerprint { get; }

        public long Sequence { get; }

        public IReadOnlyList<RoomStateSnapshot> Rooms
        {
            get { return rooms; }
        }

        public IReadOnlyList<RoomExitStateSnapshot> Exits
        {
            get { return exits; }
        }

        public string Fingerprint { get; }

        public static RoomGraphSnapshot CreateCanonical(
            string layoutStableId,
            string definitionFingerprint,
            long sequence,
            IEnumerable<RoomStateSnapshot> rooms,
            IEnumerable<RoomExitStateSnapshot> exits)
        {
            var provisional = new RoomGraphSnapshot(
                CurrentSchemaVersion,
                layoutStableId,
                definitionFingerprint,
                sequence,
                rooms,
                exits,
                string.Empty);
            return new RoomGraphSnapshot(
                provisional.SchemaVersion,
                provisional.LayoutStableId,
                provisional.DefinitionFingerprint,
                provisional.Sequence,
                provisional.Rooms,
                provisional.Exits,
                RoomGraphFormat.ComputeSha256(
                    provisional.ToCanonicalString()));
        }

        public bool HasValidFingerprint()
        {
            return string.Equals(
                Fingerprint,
                RoomGraphFormat.ComputeSha256(ToCanonicalString()),
                StringComparison.Ordinal);
        }

        public string ToCanonicalString()
        {
            var builder = new StringBuilder();
            RoomGraphFormat.AppendToken(builder, "schema", SchemaId);
            RoomGraphFormat.AppendToken(
                builder,
                "schema_version",
                SchemaVersion.ToString(CultureInfo.InvariantCulture));
            RoomGraphFormat.AppendToken(
                builder,
                "layout_stable_id",
                LayoutStableId);
            RoomGraphFormat.AppendToken(
                builder,
                "definition_fingerprint",
                DefinitionFingerprint);
            RoomGraphFormat.AppendToken(
                builder,
                "sequence",
                Sequence.ToString(CultureInfo.InvariantCulture));
            RoomGraphFormat.AppendToken(
                builder,
                "room_count",
                rooms == null
                    ? "-1"
                    : rooms.Count.ToString(CultureInfo.InvariantCulture));
            if (rooms != null)
            {
                for (int index = 0; index < rooms.Count; index++)
                {
                    RoomGraphFormat.AppendToken(
                        builder,
                        "room_" + index.ToString(
                            "D4",
                            CultureInfo.InvariantCulture),
                        rooms[index] == null
                            ? string.Empty
                            : rooms[index].ToCanonicalString());
                }
            }

            RoomGraphFormat.AppendToken(
                builder,
                "exit_count",
                exits == null
                    ? "-1"
                    : exits.Count.ToString(CultureInfo.InvariantCulture));
            if (exits != null)
            {
                for (int index = 0; index < exits.Count; index++)
                {
                    RoomGraphFormat.AppendToken(
                        builder,
                        "exit_" + index.ToString(
                            "D4",
                            CultureInfo.InvariantCulture),
                        exits[index] == null
                            ? string.Empty
                            : exits[index].ToCanonicalString());
                }
            }

            return builder.ToString();
        }

        private static ReadOnlyCollection<RoomStateSnapshot> CopyAndOrderRooms(
            IEnumerable<RoomStateSnapshot> source)
        {
            if (source == null)
            {
                return null;
            }

            var copy = new List<RoomStateSnapshot>(source);
            copy.Sort(CompareRooms);
            return new ReadOnlyCollection<RoomStateSnapshot>(copy);
        }

        private static ReadOnlyCollection<RoomExitStateSnapshot> CopyAndOrderExits(
            IEnumerable<RoomExitStateSnapshot> source)
        {
            if (source == null)
            {
                return null;
            }

            var copy = new List<RoomExitStateSnapshot>(source);
            copy.Sort(CompareExits);
            return new ReadOnlyCollection<RoomExitStateSnapshot>(copy);
        }

        private static int CompareRooms(
            RoomStateSnapshot left,
            RoomStateSnapshot right)
        {
            if (ReferenceEquals(left, right))
            {
                return 0;
            }

            if (left == null)
            {
                return -1;
            }

            if (right == null)
            {
                return 1;
            }

            return string.CompareOrdinal(
                left.RoomStableId,
                right.RoomStableId);
        }

        private static int CompareExits(
            RoomExitStateSnapshot left,
            RoomExitStateSnapshot right)
        {
            if (ReferenceEquals(left, right))
            {
                return 0;
            }

            if (left == null)
            {
                return -1;
            }

            if (right == null)
            {
                return 1;
            }

            return string.CompareOrdinal(
                left.ExitStableId,
                right.ExitStableId);
        }
    }

    public sealed class RoomGraphOperationResult
    {
        public RoomGraphOperationResult(
            RoomGraphOperationStatus status,
            string rejectionCode,
            StableId exitStableId,
            RoomGraphSnapshot previousSnapshot,
            RoomGraphSnapshot currentSnapshot)
        {
            Status = status;
            RejectionCode = rejectionCode ?? string.Empty;
            ExitStableId = exitStableId;
            PreviousSnapshot = previousSnapshot
                ?? throw new ArgumentNullException(nameof(previousSnapshot));
            CurrentSnapshot = currentSnapshot
                ?? throw new ArgumentNullException(nameof(currentSnapshot));
        }

        public RoomGraphOperationStatus Status { get; }

        public string RejectionCode { get; }

        public StableId ExitStableId { get; }

        public RoomGraphSnapshot PreviousSnapshot { get; }

        public RoomGraphSnapshot CurrentSnapshot { get; }

        public bool Changed
        {
            get { return Status == RoomGraphOperationStatus.Applied; }
        }
    }

    public sealed class RoomGraphImportResult
    {
        public RoomGraphImportResult(
            RoomGraphImportStatus status,
            string rejectionCode,
            RoomGraphSnapshot previousSnapshot,
            RoomGraphSnapshot currentSnapshot)
        {
            Status = status;
            RejectionCode = rejectionCode ?? string.Empty;
            PreviousSnapshot = previousSnapshot
                ?? throw new ArgumentNullException(nameof(previousSnapshot));
            CurrentSnapshot = currentSnapshot
                ?? throw new ArgumentNullException(nameof(currentSnapshot));
        }

        public RoomGraphImportStatus Status { get; }

        public string RejectionCode { get; }

        public RoomGraphSnapshot PreviousSnapshot { get; }

        public RoomGraphSnapshot CurrentSnapshot { get; }
    }

    public interface IRoomMissionLayout
    {
        RoomGraphDefinition Definition { get; }

        RoomLiveState CurrentRoomState { get; }

        IReadOnlyList<RoomLiveState> RoomStates { get; }

        IReadOnlyList<RoomExitLiveState> ExitStates { get; }

        RoomGraphSnapshot CurrentSnapshot { get; }

        RoomGraphOperationResult CompleteCurrentRoom();

        RoomGraphOperationResult Traverse(StableId exitStableId);

        RoomGraphOperationResult Restart();

        RoomGraphImportResult TryImport(RoomGraphSnapshot snapshot);

        string CreateDebugProjection();
    }
}
