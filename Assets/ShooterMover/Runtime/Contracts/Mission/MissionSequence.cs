using System;

namespace ShooterMover.Contracts.Mission
{
    public enum MissionSequenceRelation
    {
        Stale = -1,
        Current = 0,
        Future = 1,
    }

    /// <summary>
    /// Immutable zero-based mission event position. Commands name the current
    /// position they expect; accepted events advance it by exactly one.
    /// </summary>
    public sealed class MissionSequence :
        IEquatable<MissionSequence>,
        IComparable<MissionSequence>,
        IComparable
    {
        public const long InitialValue = 0L;

        public MissionSequence(long value)
        {
            if (value < InitialValue)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(value),
                    value,
                    "Mission sequence values cannot be negative.");
            }

            Value = value;
        }

        public long Value { get; }

        public static MissionSequence Initial
        {
            get { return new MissionSequence(InitialValue); }
        }

        public MissionSequence Next()
        {
            if (Value == long.MaxValue)
            {
                throw new InvalidOperationException("Mission sequence cannot advance past Int64.MaxValue.");
            }

            return new MissionSequence(Value + 1L);
        }

        public MissionSequenceRelation RelateTo(MissionSequence current)
        {
            if (current == null)
            {
                throw new ArgumentNullException(nameof(current));
            }

            if (Value < current.Value)
            {
                return MissionSequenceRelation.Stale;
            }

            if (Value > current.Value)
            {
                return MissionSequenceRelation.Future;
            }

            return MissionSequenceRelation.Current;
        }

        public int CompareTo(MissionSequence other)
        {
            if (ReferenceEquals(other, null))
            {
                return 1;
            }

            return Value.CompareTo(other.Value);
        }

        int IComparable.CompareTo(object obj)
        {
            if (obj == null)
            {
                return 1;
            }

            MissionSequence other = obj as MissionSequence;
            if (other == null)
            {
                throw new ArgumentException(
                    "Object must be of type MissionSequence.",
                    nameof(obj));
            }

            return CompareTo(other);
        }

        public bool Equals(MissionSequence other)
        {
            return !ReferenceEquals(other, null) && Value == other.Value;
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as MissionSequence);
        }

        public override int GetHashCode()
        {
            return Value.GetHashCode();
        }

        public override string ToString()
        {
            return Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        public static bool operator ==(MissionSequence left, MissionSequence right)
        {
            if (ReferenceEquals(left, right))
            {
                return true;
            }

            if (ReferenceEquals(left, null) || ReferenceEquals(right, null))
            {
                return false;
            }

            return left.Equals(right);
        }

        public static bool operator !=(MissionSequence left, MissionSequence right)
        {
            return !(left == right);
        }

        public static bool operator <(MissionSequence left, MissionSequence right)
        {
            if (ReferenceEquals(left, null))
            {
                return !ReferenceEquals(right, null);
            }

            return left.CompareTo(right) < 0;
        }

        public static bool operator >(MissionSequence left, MissionSequence right)
        {
            if (ReferenceEquals(left, null))
            {
                return false;
            }

            return left.CompareTo(right) > 0;
        }

        public static bool operator <=(MissionSequence left, MissionSequence right)
        {
            return !(left > right);
        }

        public static bool operator >=(MissionSequence left, MissionSequence right)
        {
            return !(left < right);
        }
    }
}
