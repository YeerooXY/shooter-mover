using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Missions.Rooms;

namespace ShooterMover.Contracts.Missions.Rooms
{
    public enum RoomAvailabilityStateV1
    {
        Locked = 1,
        Available = 2,
    }

    public enum RoomGraphOperationStatusV1
    {
        Applied = 1,
        NoChange = 2,
        UnknownExit = 3,
        ExitNotFromCurrentRoom = 4,
        ExitLocked = 5,
        TargetRoomLocked = 6,
    }

    public enum RoomGraphImportStatusV1
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

    public sealed class RoomRuntimeStateV1
    {
        public RoomRuntimeStateV1(
            StableId roomStableId,
            RoomAvailabilityStateV1 availability,
            bool isCurrent,
            bool isVisited,
            bool isCompleted)
        {
            RoomStableId = roomStableId
                ?? throw new ArgumentNullException(nameof(roomStableId));
            if (!Enum.IsDefined(typeof(RoomAvailabilityStateV1), availability))
            {
                throw new ArgumentOutOfRangeException(nameof(availability));
            }

            Availability = availability;
            IsCurrent = isCurrent;
            IsVisited = isVisited;
            IsCompleted = isCompleted;
        }

        public StableId RoomStableId { get; }

        public RoomAvailabilityStateV1 Availability { get; }

        public bool IsCurrent { get; }

        public bool IsVisited { get; }

        public bool IsCompleted { get; }

        public RoomRuntimeStateV1 With(
            RoomAvailabilityStateV1 availability,
            bool isCurrent,
            bool isVisited,
            bool isCompleted)
        {
            return new RoomRuntimeStateV1(
                RoomStableId,
                availability,
                isCurrent,
                isVisited,
                isCompleted);
        }
    }

    public sealed class RoomExitRuntimeStateV1
    {
        public RoomExitRuntimeStateV1(
            StableId exitStableId,
            bool isAvailable)
        {
            ExitStableId = exitStableId
                ?? throw new ArgumentNullException(nameof(exitStableId));
            IsAvailable = isAvailable;
        }

        public StableId ExitStableId { get; }

        public bool IsAvailable { get; }

        public RoomExitRuntimeStateV1 WithAvailability(bool isAvailable)
        {
            return new RoomExitRuntimeStateV1(
                ExitStableId,
                isAvailable);
        }
    }

    /// <summary>
    /// Raw persistence record. String identities are parsed and validated before
    /// application state is changed, so malformed external data fails closed.
    /// </summary>
    public sealed class RoomStateSnapshotV1
    {
        public RoomStateSnapshotV1(
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
            RoomGraphFormatV1.AppendToken(
                builder,
                "room_stable_id",
                RoomStableId);
            RoomGraphFormatV1.AppendToken(
                builder,
                "availability",
                Availability.ToString(CultureInfo.InvariantCulture));
            RoomGraphFormatV1.AppendToken(
                builder,
                "is_current",
                IsCurrent ? "1" : "0");
            RoomGraphFormatV1.AppendToken(
                builder,
                "is_visited",
                IsVisited ? "1" : "0");
            RoomGraphFormatV1.AppendToken(
                builder,
                "is_completed",
                IsCompleted ? "1" : "0");
            return builder.ToString();
        }
    }

    public sealed class RoomExitStateSnapshotV1
    {
        public RoomExitStateSnapshotV1(
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
            RoomGraphFormatV1.AppendToken(
                builder,
                "exit_stable_id",
                ExitStableId);
            RoomGraphFormatV1.AppendToken(
                builder,
                "is_available",
                IsAvailable ? "1" : "0");
            return builder.ToString();
        }
    }

    public sealed class RoomGraphSnapshotV1
    {
        private const string SchemaId = "room-graph-snapshot-v1";
        private readonly ReadOnlyCollection<RoomStateSnapshotV1> rooms;
        private readonly ReadOnlyCollection<RoomExitStateSnapshotV1> exits;

        public const int CurrentSchemaVersion = 1;

        public RoomGraphSnapshotV1(
            int schemaVersion,
            string layoutStableId,
            string definitionFingerprint,
            long sequence,
            IEnumerable<RoomStateSnapshotV1> rooms,
            IEnumerable<RoomExitStateSnapshotV1> exits,
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

        public IReadOnlyList<RoomStateSnapshotV1> Rooms
        {
            get { return rooms; }
        }

        public IReadOnlyList<RoomExitStateSnapshotV1> Exits
        {
            get { return exits; }
        }

        public string Fingerprint { get; }

        public static RoomGraphSnapshotV1 CreateCanonical(
            string layoutStableId,
            string definitionFingerprint,
            long sequence,
            IEnumerable<RoomStateSnapshotV1> rooms,
            IEnumerable<RoomExitStateSnapshotV1> exits)
        {
            var provisional = new RoomGraphSnapshotV1(
                CurrentSchemaVersion,
                layoutStableId,
                definitionFingerprint,
                sequence,
                rooms,
                exits,
                string.Empty);
            return new RoomGraphSnapshotV1(
                provisional.SchemaVersion,
                provisional.LayoutStableId,
                provisional.DefinitionFingerprint,
                provisional.Sequence,
                provisional.Rooms,
                provisional.Exits,
                RoomGraphFormatV1.ComputeSha256(
                    provisional.ToCanonicalString()));
        }

        public bool HasValidFingerprint()
        {
            return string.Equals(
                Fingerprint,
                RoomGraphFormatV1.ComputeSha256(ToCanonicalString()),
                StringComparison.Ordinal);
        }

        public string ToCanonicalString()
        {
            var builder = new StringBuilder();
            RoomGraphFormatV1.AppendToken(builder, "schema", SchemaId);
            RoomGraphFormatV1.AppendToken(
                builder,
                "schema_version",
                SchemaVersion.ToString(CultureInfo.InvariantCulture));
            RoomGraphFormatV1.AppendToken(
                builder,
                "layout_stable_id",
                LayoutStableId);
            RoomGraphFormatV1.AppendToken(
                builder,
                "definition_fingerprint",
                DefinitionFingerprint);
            RoomGraphFormatV1.AppendToken(
                builder,
                "sequence",
                Sequence.ToString(CultureInfo.InvariantCulture));
            RoomGraphFormatV1.AppendToken(
                builder,
                "room_count",
                rooms == null
                    ? "-1"
                    : rooms.Count.ToString(CultureInfo.InvariantCulture));
            if (rooms != null)
            {
                for (int index = 0; index < rooms.Count; index++)
                {
                    RoomGraphFormatV1.AppendToken(
                        builder,
                        "room_" + index.ToString(
                            "D4",
                            CultureInfo.InvariantCulture),
                        rooms[index] == null
                            ? string.Empty
                            : rooms[index].ToCanonicalString());
                }
            }

            RoomGraphFormatV1.AppendToken(
                builder,
                "exit_count",
                exits == null
                    ? "-1"
                    : exits.Count.ToString(CultureInfo.InvariantCulture));
            if (exits != null)
            {
                for (int index = 0; index < exits.Count; index++)
                {
                    RoomGraphFormatV1.AppendToken(
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

        private static ReadOnlyCollection<RoomStateSnapshotV1> CopyAndOrderRooms(
            IEnumerable<RoomStateSnapshotV1> source)
        {
            if (source == null)
            {
                return null;
            }

            var copy = new List<RoomStateSnapshotV1>(source);
            copy.Sort(CompareRooms);
            return new ReadOnlyCollection<RoomStateSnapshotV1>(copy);
        }

        private static ReadOnlyCollection<RoomExitStateSnapshotV1> CopyAndOrderExits(
            IEnumerable<RoomExitStateSnapshotV1> source)
        {
            if (source == null)
            {
                return null;
            }

            var copy = new List<RoomExitStateSnapshotV1>(source);
            copy.Sort(CompareExits);
            return new ReadOnlyCollection<RoomExitStateSnapshotV1>(copy);
        }

        private static int CompareRooms(
            RoomStateSnapshotV1 left,
            RoomStateSnapshotV1 right)
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
            RoomExitStateSnapshotV1 left,
            RoomExitStateSnapshotV1 right)
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

    public sealed class RoomGraphOperationResultV1
    {
        public RoomGraphOperationResultV1(
            RoomGraphOperationStatusV1 status,
            string rejectionCode,
            StableId exitStableId,
            RoomGraphSnapshotV1 previousSnapshot,
            RoomGraphSnapshotV1 currentSnapshot)
        {
            Status = status;
            RejectionCode = rejectionCode ?? string.Empty;
            ExitStableId = exitStableId;
            PreviousSnapshot = previousSnapshot
                ?? throw new ArgumentNullException(nameof(previousSnapshot));
            CurrentSnapshot = currentSnapshot
                ?? throw new ArgumentNullException(nameof(currentSnapshot));
        }

        public RoomGraphOperationStatusV1 Status { get; }

        public string RejectionCode { get; }

        public StableId ExitStableId { get; }

        public RoomGraphSnapshotV1 PreviousSnapshot { get; }

        public RoomGraphSnapshotV1 CurrentSnapshot { get; }

        public bool Changed
        {
            get { return Status == RoomGraphOperationStatusV1.Applied; }
        }
    }

    public sealed class RoomGraphImportResultV1
    {
        public RoomGraphImportResultV1(
            RoomGraphImportStatusV1 status,
            string rejectionCode,
            RoomGraphSnapshotV1 previousSnapshot,
            RoomGraphSnapshotV1 currentSnapshot)
        {
            Status = status;
            RejectionCode = rejectionCode ?? string.Empty;
            PreviousSnapshot = previousSnapshot
                ?? throw new ArgumentNullException(nameof(previousSnapshot));
            CurrentSnapshot = currentSnapshot
                ?? throw new ArgumentNullException(nameof(currentSnapshot));
        }

        public RoomGraphImportStatusV1 Status { get; }

        public string RejectionCode { get; }

        public RoomGraphSnapshotV1 PreviousSnapshot { get; }

        public RoomGraphSnapshotV1 CurrentSnapshot { get; }
    }

    public interface IRoomMissionLayoutV1
    {
        RoomGraphDefinitionV1 Definition { get; }

        RoomRuntimeStateV1 CurrentRoomState { get; }

        IReadOnlyList<RoomRuntimeStateV1> RoomStates { get; }

        IReadOnlyList<RoomExitRuntimeStateV1> ExitStates { get; }

        RoomGraphSnapshotV1 CurrentSnapshot { get; }

        RoomGraphOperationResultV1 CompleteCurrentRoom();

        RoomGraphOperationResultV1 Traverse(StableId exitStableId);

        RoomGraphOperationResultV1 Restart();

        RoomGraphImportResultV1 TryImport(RoomGraphSnapshotV1 snapshot);

        string CreateDebugProjection();
    }
}
