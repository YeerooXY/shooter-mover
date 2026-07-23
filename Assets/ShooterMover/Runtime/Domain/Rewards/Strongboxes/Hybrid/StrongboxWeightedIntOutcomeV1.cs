using System;
using System.Globalization;

namespace ShooterMover.Domain.Rewards.Strongboxes
{
    public sealed class StrongboxWeightedIntOutcomeV1 :
        IComparable<StrongboxWeightedIntOutcomeV1>,
        IEquatable<StrongboxWeightedIntOutcomeV1>
    {
        public StrongboxWeightedIntOutcomeV1(int value, ulong weight)
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

        public int CompareTo(StrongboxWeightedIntOutcomeV1 other)
        {
            return ReferenceEquals(other, null) ? 1 : Value.CompareTo(other.Value);
        }

        public bool Equals(StrongboxWeightedIntOutcomeV1 other)
        {
            return !ReferenceEquals(other, null)
                && Value == other.Value
                && Weight == other.Weight;
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as StrongboxWeightedIntOutcomeV1);
        }

        public override int GetHashCode()
        {
            return StrongboxCanonicalV1.DeterministicHash(ToCanonicalString());
        }

        public string ToCanonicalString()
        {
            return Value.ToString(CultureInfo.InvariantCulture)
                + ":"
                + Weight.ToString(CultureInfo.InvariantCulture);
        }
    }
}
