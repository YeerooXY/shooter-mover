using System;
using System.Collections.Generic;
using ShooterMover.Domain.Weapons.Execution;

namespace ShooterMover.Domain.Weapons.Guidance
{
    /// <summary>
    /// Immutable engine-neutral target data captured by the caller for one guidance decision.
    /// </summary>
    public sealed class WeaponGuidanceTargetSnapshot
    {
        public WeaponGuidanceTargetSnapshot(
            WeaponTargetReference target,
            WeaponVector2 position,
            bool isTargetable)
        {
            Target = target ?? throw new ArgumentNullException(nameof(target));
            Position = RequireFinite(position, nameof(position));
            IsTargetable = isTargetable;
        }

        public WeaponTargetReference Target { get; }
        public WeaponVector2 Position { get; }
        public bool IsTargetable { get; }

        private static WeaponVector2 RequireFinite(WeaponVector2 value, string parameterName)
        {
            if (value == null)
            {
                throw new ArgumentNullException(parameterName);
            }
            if (!value.IsFinite)
            {
                throw new ArgumentException(
                    "Weapon guidance target positions must be finite.",
                    parameterName);
            }
            return value;
        }
    }

    /// <summary>
    /// Supplies a read-only target snapshot for one deterministic decision step.
    /// Implementations must not expose Unity objects through this boundary.
    /// </summary>
    public interface IWeaponGuidanceTargetSnapshotSource
    {
        IReadOnlyList<WeaponGuidanceTargetSnapshot> GetTargetSnapshots();
    }
}
