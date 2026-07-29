using System;
using System.Globalization;

namespace ShooterMover.Domain.Rewards.Strongboxes
{
    public sealed class StrongboxDistanceWeight :
        IComparable<StrongboxDistanceWeight>,
        IEquatable<StrongboxDistanceWeight>
    {
        public StrongboxDistanceWeight(int distance, ulong weightMillionths)
        {
            if (distance < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(distance));
            }
            if (weightMillionths == 0UL)
            {
                throw new ArgumentOutOfRangeException(nameof(weightMillionths));
            }

            Distance = distance;
            WeightMillionths = weightMillionths;
        }

        public int Distance { get; }
        public ulong WeightMillionths { get; }

        public int CompareTo(StrongboxDistanceWeight other)
        {
            return ReferenceEquals(other, null) ? 1 : Distance.CompareTo(other.Distance);
        }

        public bool Equals(StrongboxDistanceWeight other)
        {
            return !ReferenceEquals(other, null)
                && Distance == other.Distance
                && WeightMillionths == other.WeightMillionths;
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as StrongboxDistanceWeight);
        }

        public override int GetHashCode()
        {
            return Strongbox.DeterministicHash(ToCanonicalString());
        }

        public string ToCanonicalString()
        {
            return Distance.ToString(CultureInfo.InvariantCulture)
                + ":"
                + WeightMillionths.ToString(CultureInfo.InvariantCulture);
        }
    }
}
