using System;
using System.Collections.Generic;
using System.Globalization;
using ShooterMover.Domain.Weapons.Execution;

namespace ShooterMover.Application.Weapons.Execution
{
    public enum WeaponEffectLineOfSightPolicy
    {
        Ignore = 1,
        Require = 2,
    }

    public sealed class WeaponEffectSourceContext
    {
        public WeaponEffectSourceContext(WeaponEffectIdentity identity, long impactOrdinal)
        {
            if (impactOrdinal < 0L)
            {
                throw new ArgumentOutOfRangeException(nameof(impactOrdinal));
            }

            Identity = identity ?? throw new ArgumentNullException(nameof(identity));
            ImpactOrdinal = impactOrdinal;
        }

        public WeaponEffectIdentity Identity { get; }
        public long ImpactOrdinal { get; }

        public static WeaponEffectSourceContext FromDescription(
            IWeaponEffectDescription description,
            long impactOrdinal)
        {
            if (description == null)
            {
                throw new ArgumentNullException(nameof(description));
            }

            return new WeaponEffectSourceContext(description.Identity, impactOrdinal);
        }
    }

    public sealed class WeaponEffectTargetSnapshot
    {
        public WeaponEffectTargetSnapshot(
            WeaponTargetReference target,
            WeaponVector2 position,
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

        public WeaponTargetReference Target { get; }
        public WeaponVector2 Position { get; }
        public bool IsEligible { get; }
    }

    public interface IWeaponEffectTargetSource
    {
        IReadOnlyList<WeaponEffectTargetSnapshot> SnapshotTargets();
    }

    public interface IWeaponEffectLineOfSightResolver
    {
        bool HasLineOfSight(WeaponVector2 origin, WeaponEffectTargetSnapshot target);
    }

    public sealed class WeaponEffectApplicationKey : IEquatable<WeaponEffectApplicationKey>
    {
        private readonly string canonicalText;

        private WeaponEffectApplicationKey(string canonicalText)
        {
            this.canonicalText = canonicalText;
        }

        public static WeaponEffectApplicationKey ForDamageOverTime(
            WeaponEffectSourceContext source,
            WeaponTargetReference target)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }
            if (target == null)
            {
                throw new ArgumentNullException(nameof(target));
            }

            WeaponEffectIdentity identity = source.Identity;
            string canonical = string.Join(
                "|",
                new[]
                {
                    "dot",
                    identity.ActorId.ToString(),
                    identity.ParticipantId.ToString(),
                    identity.EquipmentInstanceId.ToString(),
                    identity.WeaponDefinitionId.ToString(),
                    identity.FireOperationId.ToString(),
                    identity.LifecycleGeneration.ToString(),
                    identity.ShotSequence.ToString(CultureInfo.InvariantCulture),
                    target.ToCanonicalString(),
                });
            return new WeaponEffectApplicationKey(canonical);
        }

        public bool Equals(WeaponEffectApplicationKey other)
        {
            return !ReferenceEquals(other, null)
                && string.Equals(canonicalText, other.canonicalText, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as WeaponEffectApplicationKey);
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

    public interface IWeaponEffectApplicationHistory
    {
        bool Contains(WeaponEffectApplicationKey key);
    }

    /// <summary>
    /// Immutable accepted-application snapshot. Use Empty for the first resolution in a sequence.
    /// </summary>
    public sealed class WeaponEffectApplicationHistory : IWeaponEffectApplicationHistory
    {
        private static readonly WeaponEffectApplicationHistory EmptyValue =
            new WeaponEffectApplicationHistory(new WeaponEffectApplicationKey[0]);

        private readonly HashSet<WeaponEffectApplicationKey> acceptedKeys;

        public WeaponEffectApplicationHistory(
            IEnumerable<WeaponEffectApplicationKey> acceptedKeys)
        {
            if (acceptedKeys == null)
            {
                throw new ArgumentNullException(nameof(acceptedKeys));
            }

            this.acceptedKeys = new HashSet<WeaponEffectApplicationKey>();
            foreach (WeaponEffectApplicationKey key in acceptedKeys)
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

        public static WeaponEffectApplicationHistory Empty
        {
            get { return EmptyValue; }
        }

        public bool Contains(WeaponEffectApplicationKey key)
        {
            if (key == null)
            {
                throw new ArgumentNullException(nameof(key));
            }
            return acceptedKeys.Contains(key);
        }
    }

    public sealed class WeaponDamageOverTimeStateSnapshot
    {
        public WeaponDamageOverTimeStateSnapshot(
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

        public static WeaponDamageOverTimeStateSnapshot None()
        {
            return new WeaponDamageOverTimeStateSnapshot(0, 0d);
        }
    }

    internal static class WeaponEffectResolutionMath
    {
        public static double DistanceSquared(WeaponVector2 left, WeaponVector2 right)
        {
            double x = left.X - right.X;
            double y = left.Y - right.Y;
            return (x * x) + (y * y);
        }

        public static int CompareTargets(
            WeaponEffectTargetSnapshot left,
            WeaponEffectTargetSnapshot right,
            WeaponVector2 origin)
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
            WeaponEffectLineOfSightPolicy policy,
            IWeaponEffectLineOfSightResolver resolver)
        {
            if (!Enum.IsDefined(typeof(WeaponEffectLineOfSightPolicy), policy))
            {
                throw new ArgumentOutOfRangeException(nameof(policy));
            }
            if (policy == WeaponEffectLineOfSightPolicy.Require && resolver == null)
            {
                throw new ArgumentNullException(nameof(resolver));
            }
        }
    }
}
