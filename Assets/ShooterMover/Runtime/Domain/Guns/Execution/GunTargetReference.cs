using System;

namespace ShooterMover.Domain.Guns.Execution
{
    /// <summary>
    /// Exact engine-independent gun target identity. Lifecycle generation is part of the
    /// reference so a respawned actor is never mistaken for an earlier lifecycle.
    /// </summary>
    public sealed class GunTargetReference :
        IEquatable<GunTargetReference>,
        IComparable<GunTargetReference>
    {
        public GunTargetReference(
            GunActorInstanceId actorId,
            LifecycleGeneration lifecycleGeneration)
        {
            ActorId = actorId ?? throw new ArgumentNullException(nameof(actorId));
            LifecycleGeneration = lifecycleGeneration
                ?? throw new ArgumentNullException(nameof(lifecycleGeneration));
        }

        public GunActorInstanceId ActorId { get; }
        public LifecycleGeneration LifecycleGeneration { get; }

        public int CompareTo(GunTargetReference other)
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

        public bool Equals(GunTargetReference other)
        {
            return !ReferenceEquals(other, null)
                && ActorId.Equals(other.ActorId)
                && LifecycleGeneration.Equals(other.LifecycleGeneration);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as GunTargetReference);
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
