using System;
using System.Globalization;
using ShooterMover.Contracts.Mission;
using ShooterMover.Domain.Common;

namespace ShooterMover.Contracts.Rooms
{
    public enum RoomSocketDirection
    {
        Unknown = 0,
        Inbound = 1,
        Outbound = 2,
        Bidirectional = 3,
    }

    public enum RoomProjectionReadStatus
    {
        Found = 1,
        UnknownKey = 2,
    }

    /// <summary>
    /// Identifies one loaded room projection separately from the durable room ID.
    /// Distinct projection IDs allow multiple rooms to coexist additively without
    /// turning either projection into authoritative mission state.
    /// </summary>
    public sealed class RoomProjectionIdentity : IEquatable<RoomProjectionIdentity>
    {
        public RoomProjectionIdentity(StableId roomId, StableId projectionId)
        {
            RoomId = RoomContractFormat.RequireNotNull(roomId, nameof(roomId));
            ProjectionId = RoomContractFormat.RequireNotNull(projectionId, nameof(projectionId));
        }

        public StableId RoomId { get; }

        public StableId ProjectionId { get; }

        public string ToCanonicalString()
        {
            return "room_id=" + RoomId + "\nprojection_id=" + ProjectionId;
        }

        public bool Equals(RoomProjectionIdentity other)
        {
            return !ReferenceEquals(other, null)
                && RoomId.Equals(other.RoomId)
                && ProjectionId.Equals(other.ProjectionId);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as RoomProjectionIdentity);
        }

        public override int GetHashCode()
        {
            return RoomContractFormat.DeterministicHash(ToCanonicalString());
        }

        public override string ToString()
        {
            return ToCanonicalString();
        }
    }

    /// <summary>
    /// Identifies the authoritative mission projection slice a room asks to read.
    /// Mission sequence comes from Mission Messages v1 and prevents refreshes from
    /// silently regressing to an older committed position.
    /// </summary>
    public sealed class RoomProjectionKey : IEquatable<RoomProjectionKey>
    {
        public RoomProjectionKey(
            StableId runId,
            StableId roomId,
            MissionSequence sequence)
        {
            RunId = RoomContractFormat.RequireNotNull(runId, nameof(runId));
            RoomId = RoomContractFormat.RequireNotNull(roomId, nameof(roomId));
            Sequence = RoomContractFormat.RequireNotNull(sequence, nameof(sequence));
        }

        public StableId RunId { get; }

        public StableId RoomId { get; }

        public MissionSequence Sequence { get; }

        public string ToCanonicalString()
        {
            return "run_id="
                + RunId
                + "\nroom_id="
                + RoomId
                + "\nmission_sequence="
                + Sequence;
        }

        public bool Equals(RoomProjectionKey other)
        {
            return !ReferenceEquals(other, null)
                && RunId.Equals(other.RunId)
                && RoomId.Equals(other.RoomId)
                && Sequence.Equals(other.Sequence);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as RoomProjectionKey);
        }

        public override int GetHashCode()
        {
            return RoomContractFormat.DeterministicHash(ToCanonicalString());
        }

        public override string ToString()
        {
            return ToCanonicalString();
        }
    }

    /// <summary>
    /// One explicitly addressed socket on one loaded room projection.
    /// </summary>
    public sealed class RoomSocket : IEquatable<RoomSocket>
    {
        public RoomSocket(
            RoomProjectionIdentity room,
            StableId socketId,
            RoomSocketDirection direction)
        {
            Room = RoomContractFormat.RequireNotNull(room, nameof(room));
            SocketId = RoomContractFormat.RequireNotNull(socketId, nameof(socketId));

            if (!RoomContractFormat.IsKnownDirection(direction))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(direction),
                    direction,
                    "Room sockets require an explicit supported direction.");
            }

            Direction = direction;
        }

        public RoomProjectionIdentity Room { get; }

        public StableId SocketId { get; }

        public RoomSocketDirection Direction { get; }

        public bool CanConnectTo(RoomSocket other)
        {
            if (ReferenceEquals(other, null)
                || Room.Equals(other.Room)
                || Equals(other))
            {
                return false;
            }

            return (RoomContractFormat.AllowsOutbound(Direction)
                    && RoomContractFormat.AllowsInbound(other.Direction))
                || (RoomContractFormat.AllowsInbound(Direction)
                    && RoomContractFormat.AllowsOutbound(other.Direction));
        }

        public string ToCanonicalString()
        {
            return Room.ToCanonicalString()
                + "\nsocket_id="
                + SocketId
                + "\ndirection="
                + RoomContractFormat.DirectionToken(Direction);
        }

        public bool Equals(RoomSocket other)
        {
            return !ReferenceEquals(other, null)
                && Room.Equals(other.Room)
                && SocketId.Equals(other.SocketId)
                && Direction == other.Direction;
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as RoomSocket);
        }

        public override int GetHashCode()
        {
            return RoomContractFormat.DeterministicHash(ToCanonicalString());
        }

        public override string ToString()
        {
            return ToCanonicalString();
        }
    }

    /// <summary>
    /// Immutable cross-room connection. Endpoint order is canonical so the same
    /// connection compares equally regardless of construction order.
    /// </summary>
    public sealed class RoomConnection : IEquatable<RoomConnection>
    {
        public RoomConnection(RoomSocket first, RoomSocket second)
        {
            RoomSocket validatedFirst = RoomContractFormat.RequireNotNull(first, nameof(first));
            RoomSocket validatedSecond = RoomContractFormat.RequireNotNull(second, nameof(second));

            if (!validatedFirst.CanConnectTo(validatedSecond))
            {
                throw new ArgumentException(
                    "Room sockets must belong to distinct projections and have compatible directions.",
                    nameof(second));
            }

            if (string.CompareOrdinal(
                    validatedFirst.ToCanonicalString(),
                    validatedSecond.ToCanonicalString()) <= 0)
            {
                First = validatedFirst;
                Second = validatedSecond;
            }
            else
            {
                First = validatedSecond;
                Second = validatedFirst;
            }
        }

        public RoomSocket First { get; }

        public RoomSocket Second { get; }

        public bool Connects(RoomProjectionIdentity room)
        {
            RoomProjectionIdentity validated = RoomContractFormat.RequireNotNull(room, nameof(room));
            return First.Room.Equals(validated) || Second.Room.Equals(validated);
        }

        public RoomProjectionIdentity GetOther(RoomProjectionIdentity room)
        {
            RoomProjectionIdentity validated = RoomContractFormat.RequireNotNull(room, nameof(room));

            if (First.Room.Equals(validated))
            {
                return Second.Room;
            }

            if (Second.Room.Equals(validated))
            {
                return First.Room;
            }

            throw new ArgumentException(
                "The supplied room projection is not an endpoint of this connection.",
                nameof(room));
        }

        public string ToCanonicalString()
        {
            return "first:\n"
                + First.ToCanonicalString()
                + "\nsecond:\n"
                + Second.ToCanonicalString();
        }

        public bool Equals(RoomConnection other)
        {
            return !ReferenceEquals(other, null)
                && First.Equals(other.First)
                && Second.Equals(other.Second);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as RoomConnection);
        }

        public override int GetHashCode()
        {
            return RoomContractFormat.DeterministicHash(ToCanonicalString());
        }

        public override string ToString()
        {
            return ToCanonicalString();
        }
    }

    /// <summary>
    /// Explicit result for a projection read. Unknown keys are data, not null or
    /// an exception, so room presentation can fail closed without inventing state.
    /// </summary>
    public sealed class RoomProjectionReadResult<TProjection>
    {
        private RoomProjectionReadResult(
            RoomProjectionKey key,
            RoomProjectionReadStatus status,
            TProjection value)
        {
            Key = RoomContractFormat.RequireNotNull(key, nameof(key));
            Status = status;
            Value = value;
        }

        public RoomProjectionKey Key { get; }

        public RoomProjectionReadStatus Status { get; }

        public TProjection Value { get; }

        public bool HasValue
        {
            get { return Status == RoomProjectionReadStatus.Found; }
        }

        public static RoomProjectionReadResult<TProjection> Found(
            RoomProjectionKey key,
            TProjection value)
        {
            if (ReferenceEquals(value, null))
            {
                throw new ArgumentNullException(nameof(value));
            }

            return new RoomProjectionReadResult<TProjection>(
                key,
                RoomProjectionReadStatus.Found,
                value);
        }

        public static RoomProjectionReadResult<TProjection> Unknown(RoomProjectionKey key)
        {
            return new RoomProjectionReadResult<TProjection>(
                key,
                RoomProjectionReadStatus.UnknownKey,
                default(TProjection));
        }
    }

    /// <summary>
    /// Read-only port to authoritative mission projection data. Implementations
    /// return immutable projection DTOs owned by their defining contract.
    /// </summary>
    public interface IRoomProjectionStateReader
    {
        RoomProjectionReadResult<TProjection> Read<TProjection>(RoomProjectionKey key);
    }

    /// <summary>
    /// The only room-to-mission write path: submit a typed Mission Messages v1
    /// command for authoritative validation. Protocol admission is not durable
    /// mission acceptance and does not let the room set permanent truth directly.
    /// </summary>
    public interface IRoomMissionCommandSubmitter
    {
        MissionCommandEvaluation Submit(MissionCommandEnvelope command);
    }

    /// <summary>
    /// Explicit immutable service bundle supplied to a room projection.
    /// </summary>
    public sealed class RoomProjectionServices
    {
        public RoomProjectionServices(
            IRoomProjectionStateReader stateReader,
            IRoomMissionCommandSubmitter missionCommands)
        {
            StateReader = RoomContractFormat.RequireNotNull(
                stateReader,
                nameof(stateReader));
            MissionCommands = RoomContractFormat.RequireNotNull(
                missionCommands,
                nameof(missionCommands));
        }

        public IRoomProjectionStateReader StateReader { get; }

        public IRoomMissionCommandSubmitter MissionCommands { get; }
    }

    internal static class RoomContractFormat
    {
        private const uint FnvOffsetBasis = 2166136261u;
        private const uint FnvPrime = 16777619u;

        public static T RequireNotNull<T>(T value, string parameterName)
            where T : class
        {
            if (value == null)
            {
                throw new ArgumentNullException(parameterName);
            }

            return value;
        }

        public static bool IsKnownDirection(RoomSocketDirection direction)
        {
            return direction == RoomSocketDirection.Inbound
                || direction == RoomSocketDirection.Outbound
                || direction == RoomSocketDirection.Bidirectional;
        }

        public static bool AllowsInbound(RoomSocketDirection direction)
        {
            return direction == RoomSocketDirection.Inbound
                || direction == RoomSocketDirection.Bidirectional;
        }

        public static bool AllowsOutbound(RoomSocketDirection direction)
        {
            return direction == RoomSocketDirection.Outbound
                || direction == RoomSocketDirection.Bidirectional;
        }

        public static string DirectionToken(RoomSocketDirection direction)
        {
            switch (direction)
            {
                case RoomSocketDirection.Inbound:
                    return "inbound";
                case RoomSocketDirection.Outbound:
                    return "outbound";
                case RoomSocketDirection.Bidirectional:
                    return "bidirectional";
                default:
                    return "unknown-" + ((int)direction).ToString(CultureInfo.InvariantCulture);
            }
        }

        public static int DeterministicHash(string canonicalText)
        {
            if (canonicalText == null)
            {
                throw new ArgumentNullException(nameof(canonicalText));
            }

            unchecked
            {
                uint hash = FnvOffsetBasis;
                for (int index = 0; index < canonicalText.Length; index++)
                {
                    hash ^= canonicalText[index];
                    hash *= FnvPrime;
                }

                return (int)hash;
            }
        }
    }
}
