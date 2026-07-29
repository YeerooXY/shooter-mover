using System;
using System.Collections.Generic;
using System.Globalization;
using ShooterMover.Domain.Guns.Execution;

namespace ShooterMover.Application.Guns.Execution
{
    public enum GunEffectLineOfSightPolicy
    {
        Ignore = 1,
        Require = 2,
    }

    public sealed class GunEffectSourceContext
    {
        public GunEffectSourceContext(GunEffectIdentity identity, long impactOrdinal)
        {
            if (impactOrdinal < 0L)
            {
                throw new ArgumentOutOfRangeException(nameof(impactOrdinal));
            }

            Identity = identity ?? throw new ArgumentNullException(nameof(identity));
            ImpactOrdinal = impactOrdinal;
        }

        public GunEffectIdentity Identity { get; }
        public long ImpactOrdinal { get; }

        public static GunEffectSourceContext FromDescription(
            IGunEffectDescription description,
            long impactOrdinal)
        {
            if (description == null)
            {
                throw new ArgumentNullException(nameof(description));
            }

            return new GunEffectSourceContext(description.Identity, impactOrdinal);
        }
    }

    public sealed class GunEffectTargetSnapshot
    {
        public GunEffectTargetSnapshot(
            GunTargetReference target,
            GunVector2 position,
            bool isEligible)
        {
            Target = target ?? throw new ArgumentNullException(nameof(target));
            Position = position ?? throw new ArgumentNullException(nameof(position));
            if (!position.IsFinite)
            {
                throw new ArgumentOutOfRangeException(nameof(position));
            }

            IsEligible = isEligible;
        }

        public GunTargetReference Target { get; }
        public GunVector2 Position { get; }
        public bool IsEligible { get; }
    }

    public interface IGunEffectTargetSource
    {
        IReadOnlyList<GunEffectTargetSnapshot> SnapshotTargets();
    }

    public interface IGunEffectLineOfSightResolver
    {
        bool HasLineOfSight(GunVector2 origin, GunEffectTargetSnapshot target);
    }

    public sealed class GunEffectApplicationKey : IEquatable<GunEffectApplicationKey>
    {
        private readonly string canonicalText;

        private GunEffectApplicationKey(string canonicalText)
        {
            this.canonicalText = canonicalText;
        }

        public static GunEffectApplicationKey ForDamageOverTime(
            GunEffectSourceContext source,
            GunTargetReference target)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }
            if (target == null)
            {
                throw new ArgumentNullException(nameof(target));
            }

            GunEffectIdentity identity = source.Identity;
            string canonical = string.Join(
                "|",
                new[]
                {
                    "dot",
                    identity.ActorId.ToString(),
                    identity.ParticipantId.ToString(),
                    identity.EquipmentInstanceId.ToString(),
                    identity.GunDefinitionId.ToString(),
                    identity.FireOperationId.ToString(),
                    identity.LifecycleGeneration.ToString(),
                    identity.ShotSequence.ToString(CultureInfo.InvariantCulture),
                    target.ToCanonicalString(),
                });
            return new GunEffectApplicationKey(canonical);
        }

        public bool Equals(GunEffectApplicationKey other)
        {
            return !ReferenceEquals(other, null)
                && string.Equals(canonicalText, other.canonicalText, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as GunEffectApplicationKey);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                uint hash = 2166136261u;
                for (int index = 0; index < canonicalText.Length; index++)
                {
                    hash ^= canonicalText[index];
                    hash *= 16777619u;
                }
                return (int)hash;
            }
        }

        public override string ToString()
        {
            return canonicalText;
        }
    }

    public interface IGunEffectApplicationHistory
    {
        bool Contains(GunEffectApplicationKey key);
    }

    /// <summary>
    /// Immutable accepted-application snapshot. Use Empty for the first resolution in a sequence.
    /// </summary>
    public sealed class GunEffectApplicationHistory : IGunEffectApplicationHistory
    {
        private static readonly GunEffectApplicationHistory EmptyValue =
            new GunEffectApplicationHistory(new GunEffectApplicationKey[0]);

        private readonly HashSet<GunEffectApplicationKey> acceptedKeys;

        public GunEffectApplicationHistory(
            IEnumerable<GunEffectApplicationKey> acceptedKeys)
        {
            if (acceptedKeys == null)
            {
                throw new ArgumentNullException(nameof(acceptedKeys));
            }

            this.acceptedKeys = new HashSet<GunEffectApplicationKey>();
            foreach (GunEffectApplicationKey key in acceptedKeys)
            {
                if (key == null)
                {
                    throw new ArgumentException(
                        "Accepted effect application keys cannot contain null values.",
                        nameof(acceptedKeys));
                }
                this.acceptedKeys.Add(key);
            }
        }

        public static GunEffectApplicationHistory Empty
        {
            get { return EmptyValue; }
        }

        public bool Contains(GunEffectApplicationKey key)
        {
            if (key == null)
            {
                throw new ArgumentNullException(nameof(key));
            }
            return acceptedKeys.Contains(key);
        }
    }

    public sealed class GunDamageOverTimeStateSnapshot
    {
        public GunDamageOverTimeStateSnapshot(
            int stackCount,
            double remainingDurationSeconds)
        {
            if (stackCount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(stackCount));
            }
            if (double.IsNaN(remainingDurationSeconds)
                || double.IsInfinity(remainingDurationSeconds)
                || remainingDurationSeconds < 0d)
            {
                throw new ArgumentOutOfRangeException(nameof(remainingDurationSeconds));
            }
            if ((stackCount == 0) != (remainingDurationSeconds == 0d))
            {
                throw new ArgumentException(
                    "Zero stacks require zero remaining duration and active stacks require positive duration.");
            }

            StackCount = stackCount;
            RemainingDurationSeconds = remainingDurationSeconds;
        }

        public int StackCount { get; }
        public double RemainingDurationSeconds { get; }

        public static GunDamageOverTimeStateSnapshot None()
        {
            return new GunDamageOverTimeStateSnapshot(0, 0d);
        }
    }

    internal static class GunEffectResolutionMath
    {
        public static double DistanceSquared(GunVector2 left, GunVector2 right)
        {
            double x = left.X - right.X;
            double y = left.Y - right.Y;
            return (x * x) + (y * y);
        }

        public static int CompareTargets(
            GunEffectTargetSnapshot left,
            GunEffectTargetSnapshot right,
            GunVector2 origin)
        {
            int distance = DistanceSquared(left.Position, origin)
                .CompareTo(DistanceSquared(right.Position, origin));
            if (distance != 0)
            {
                return distance;
            }

            return left.Target.CompareTo(right.Target);
        }

        public static void ValidateLineOfSight(
            GunEffectLineOfSightPolicy policy,
            IGunEffectLineOfSightResolver resolver)
        {
            if (!Enum.IsDefined(typeof(GunEffectLineOfSightPolicy), policy))
            {
                throw new ArgumentOutOfRangeException(nameof(policy));
            }
            if (policy == GunEffectLineOfSightPolicy.Require && resolver == null)
            {
                throw new ArgumentNullException(nameof(resolver));
            }
        }
    }
}
