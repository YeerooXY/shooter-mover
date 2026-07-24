using System;
using System.Collections.Generic;
using ShooterMover.Domain.Weapons.Execution;

namespace ShooterMover.Domain.Weapons.Guidance
{
    /// <summary>
    /// Exact target identity. Lifecycle generation is part of the reference so a respawned actor
    /// can never be mistaken for the previously locked target.
    /// </summary>
    public sealed class WeaponGuidanceTargetReference :
        IEquatable<WeaponGuidanceTargetReference>,
        IComparable<WeaponGuidanceTargetReference>
    {
        public WeaponGuidanceTargetReference(
            WeaponActorInstanceId actorId,
            LifecycleGeneration lifecycleGeneration)
        {
            ActorId = actorId ?? throw new ArgumentNullException(nameof(actorId));
            LifecycleGeneration = lifecycleGeneration
                ?? throw new ArgumentNullException(nameof(lifecycleGeneration));
        }

        public WeaponActorInstanceId ActorId { get; }
        public LifecycleGeneration LifecycleGeneration { get; }

        public int CompareTo(WeaponGuidanceTargetReference other)
        {
            if (ReferenceEquals(other, null))
            {
                return 1;
            }

            int actorComparison = ActorId.Value.CompareTo(other.ActorId.Value);
            if (actorComparison != 0)
            {
                return actorComparison;
            }

            return LifecycleGeneration.Value.CompareTo(other.LifecycleGeneration.Value);
        }

        public bool Equals(WeaponGuidanceTargetReference other)
        {
            return !ReferenceEquals(other, null)
                && ActorId.Equals(other.ActorId)
                && LifecycleGeneration.Equals(other.LifecycleGeneration);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as WeaponGuidanceTargetReference);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (ActorId.GetHashCode() * 397) ^ LifecycleGeneration.GetHashCode();
            }
        }

        public override string ToString()
        {
            return ActorId + "@" + LifecycleGeneration;
        }
    }

    /// <summary>
    /// Immutable engine-neutral target data captured by the caller for one guidance decision.
    /// </summary>
    public sealed class WeaponGuidanceTargetSnapshot
    {
        public WeaponGuidanceTargetSnapshot(
            WeaponGuidanceTargetReference target,
            WeaponVector2 position,
            bool isTargetable)
        {
            Target = target ?? throw new ArgumentNullException(nameof(target));
            Position = RequireFinite(position, nameof(position));
            IsTargetable = isTargetable;
        }

        public WeaponGuidanceTargetReference Target { get; }
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
