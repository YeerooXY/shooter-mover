using System;
using System.Globalization;

namespace ShooterMover.Domain.Rewards.Strongboxes
{
    public sealed class StrongboxWeightedIntOutcome :
        IComparable<StrongboxWeightedIntOutcome>,
        IEquatable<StrongboxWeightedIntOutcome>
    {
        public StrongboxWeightedIntOutcome(int value, ulong weight)
        {
            if (weight == 0UL)
            {
                throw new ArgumentOutOfRangeException(nameof(weight));
            }

            Value = value;
            Weight = weight;
        }

        public int Value { get; }
        public ulong Weight { get; }

        public int CompareTo(StrongboxWeightedIntOutcome other)
        {
            return ReferenceEquals(other, null) ? 1 : Value.CompareTo(other.Value);
        }

        public bool Equals(StrongboxWeightedIntOutcome other)
        {
            return !ReferenceEquals(other, null)
                && Value == other.Value
                && Weight == other.Weight;
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as StrongboxWeightedIntOutcome);
        }

        public override int GetHashCode()
        {
            return Strongbox.DeterministicHash(ToCanonicalString());
        }

        public string ToCanonicalString()
        {
            return Value.ToString(CultureInfo.InvariantCulture)
                + ":"
                + Weight.ToString(CultureInfo.InvariantCulture);
        }
    }
}
