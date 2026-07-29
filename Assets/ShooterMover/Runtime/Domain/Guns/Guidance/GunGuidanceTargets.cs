using System;
using System.Collections.Generic;
using ShooterMover.Domain.Guns.Execution;

namespace ShooterMover.Domain.Guns.Guidance
{
    /// <summary>
    /// Immutable engine-neutral target data captured by the caller for one guidance decision.
    /// </summary>
    public sealed class GunGuidanceTargetSnapshot
    {
        public GunGuidanceTargetSnapshot(
            GunTargetReference target,
            GunVector2 position,
            bool isTargetable)
        {
            Target = target ?? throw new ArgumentNullException(nameof(target));
            Position = RequireFinite(position, nameof(position));
            IsTargetable = isTargetable;
        }

        public GunTargetReference Target { get; }
        public GunVector2 Position { get; }
        public bool IsTargetable { get; }

        private static GunVector2 RequireFinite(GunVector2 value, string parameterName)
        {
            if (value == null)
            {
                throw new ArgumentNullException(parameterName);
            }
            if (!value.IsFinite)
            {
                throw new ArgumentException(
                    "Gun guidance target positions must be finite.",
                    parameterName);
            }
            return value;
        }
    }

    /// <summary>
    /// Supplies a read-only target snapshot for one deterministic decision step.
    /// Implementations must not expose Unity objects through this boundary.
    /// </summary>
    public interface IGunGuidanceTargetSnapshotSource
    {
        IReadOnlyList<GunGuidanceTargetSnapshot> GetTargetSnapshots();
    }
}
