using System;
using ShooterMover.Domain.Common;

namespace ShooterMover.Application.Weapons.Execution
{
    /// <summary>
    /// Deterministic identity for one logical wall contact. Adapters must reuse this value when
    /// the same collision is reported more than once during a simulation step.
    /// </summary>
    public sealed class WeaponWallContactId :
        IEquatable<WeaponWallContactId>,
        IComparable<WeaponWallContactId>
    {
        public WeaponWallContactId(StableId value)
        {
            Value = value ?? throw new ArgumentNullException(nameof(value));
        }

        public StableId Value { get; }

        public int CompareTo(WeaponWallContactId other)
        {
            if (ReferenceEquals(other, null))
            {
                return 1;
            }

            return Value.CompareTo(other.Value);
        }

        public bool Equals(WeaponWallContactId other)
        {
            return !ReferenceEquals(other, null) && Value.Equals(other.Value);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as WeaponWallContactId);
        }

        public override int GetHashCode()
        {
            return Value.GetHashCode();
        }

        public override string ToString()
        {
            return Value.ToString();
        }
    }
}
