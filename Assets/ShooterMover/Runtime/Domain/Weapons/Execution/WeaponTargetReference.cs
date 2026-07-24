using System;

namespace ShooterMover.Domain.Weapons.Execution
{
    /// <summary>
    /// Exact engine-independent weapon target identity. Lifecycle generation is part of the
    /// reference so a respawned actor is never mistaken for an earlier lifecycle.
    /// </summary>
    public sealed class WeaponTargetReference :
        IEquatable<WeaponTargetReference>,
        IComparable<WeaponTargetReference>
    {
        public WeaponTargetReference(
            WeaponActorInstanceId actorId,
            LifecycleGeneration lifecycleGeneration)
        {
            ActorId = actorId ?? throw new ArgumentNullException(nameof(actorId));
            LifecycleGeneration = lifecycleGeneration
                ?? throw new ArgumentNullException(nameof(lifecycleGeneration));
        }

        public WeaponActorInstanceId ActorId { get; }
        public LifecycleGeneration LifecycleGeneration { get; }

        public int CompareTo(WeaponTargetReference other)
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

        public bool Equals(WeaponTargetReference other)
        {
            return !ReferenceEquals(other, null)
                && ActorId.Equals(other.ActorId)
                && LifecycleGeneration.Equals(other.LifecycleGeneration);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as WeaponTargetReference);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (ActorId.GetHashCode() * 397) ^ LifecycleGeneration.GetHashCode();
            }
        }

        public string ToCanonicalString()
        {
            return ActorId + "|" + LifecycleGeneration;
        }

        public override string ToString()
        {
            return ToCanonicalString();
        }
    }
}
