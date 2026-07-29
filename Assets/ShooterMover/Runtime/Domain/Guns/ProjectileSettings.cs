using System;

namespace ShooterMover.Domain.Guns
{
    /// <summary>
    /// Fixed-point pierce value in tenths. Legacy integer contracts may convert only when
    /// no fractional additional-hit chance would be discarded.
    /// </summary>
    public struct PierceValue : IEquatable<PierceValue>
    {
        public PierceValue(int tenths)
        {
            if (tenths < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(tenths));
            }
            Tenths = tenths;
        }

        public int Tenths { get; }

        public int GuaranteedHits
        {
            get { return Tenths / 10; }
        }

        public double FractionalAdditionalHitChance
        {
            get { return (Tenths % 10) / 10d; }
        }

        public bool HasFractionalAdditionalHitChance
        {
            get { return Tenths % 10 != 0; }
        }

        public static PierceValue FromLegacyInteger(int legacyPierce)
        {
            if (legacyPierce < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(legacyPierce));
            }
            checked
            {
                return new PierceValue(legacyPierce * 10);
            }
        }

        public bool TryToLegacyInteger(out int legacyPierce)
        {
            if (HasFractionalAdditionalHitChance)
            {
                legacyPierce = 0;
                return false;
            }
            legacyPierce = GuaranteedHits;
            return true;
        }

        public bool Equals(PierceValue other)
        {
            return Tenths == other.Tenths;
        }

        public override bool Equals(object obj)
        {
            return obj is PierceValue && Equals((PierceValue)obj);
        }

        public override int GetHashCode()
        {
            return Tenths;
        }

        public override string ToString()
        {
            return Tenths.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        public static bool operator ==(PierceValue left, PierceValue right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(PierceValue left, PierceValue right)
        {
            return !left.Equals(right);
        }
    }

    public enum GunProjectileKind
    {
        RegularProjectile = 1,
        Rocket = 2,
        Orb = 3,
    }

    public enum GunProjectileTerminationBehavior
    {
        StopOnFirstBlockingImpact = 1,
        StopWhenPierceIsSpent = 2,
        ContinueUntilRangeExpiry = 3,
    }

    public sealed class ProjectileSettings
    {
        private ProjectileSettings(
            GunProjectileKind kind,
            double speed,
            double range,
            PierceValue pierce,
            GunProjectileTerminationBehavior terminationBehavior)
        {
            Kind = kind;
            Speed = speed;
            Range = range;
            Pierce = pierce;
            TerminationBehavior = terminationBehavior;
        }

        public GunProjectileKind Kind { get; }
        public double Speed { get; }
        public double Range { get; }
        public PierceValue Pierce { get; }
        public GunProjectileTerminationBehavior TerminationBehavior { get; }

        public static ProjectileSettings Create(
            GunProjectileKind kind,
            double speed,
            double range,
            PierceValue pierce,
            GunProjectileTerminationBehavior terminationBehavior)
        {
            RequireFinitePositive(speed, nameof(speed));
            RequireFinitePositive(range, nameof(range));
            if (!Enum.IsDefined(typeof(GunProjectileKind), kind))
            {
                throw new ArgumentOutOfRangeException(nameof(kind));
            }
            if (!Enum.IsDefined(
                    typeof(GunProjectileTerminationBehavior),
                    terminationBehavior))
            {
                throw new ArgumentOutOfRangeException(nameof(terminationBehavior));
            }
            return new ProjectileSettings(
                kind,
                speed,
                range,
                pierce,
                terminationBehavior);
        }

        private static void RequireFinitePositive(double value, string parameterName)
        {
            if (double.IsNaN(value) || double.IsInfinity(value) || value <= 0d)
            {
                throw new ArgumentOutOfRangeException(parameterName);
            }
        }
    }

    public enum GunGuidanceMode
    {
        Unguided = 1,
        Homing = 2,
    }

    public enum GunTargetPolicy
    {
        ClosestToAim = 1,
        NearestInRange = 2,
        CurrentLockedTarget = 3,
    }

    public enum GunReacquisitionMode
    {
        None = 1,
        ReuseTargetPolicy = 2,
    }

    public sealed class GunGuidanceSpec
    {
        private GunGuidanceSpec(
            GunGuidanceMode mode,
            double acquisitionRange,
            double turnRateDegreesPerSecond,
            double activationDelaySeconds,
            GunTargetPolicy targetPolicy,
            GunReacquisitionMode reacquisition)
        {
            Mode = mode;
            AcquisitionRange = acquisitionRange;
            TurnRateDegreesPerSecond = turnRateDegreesPerSecond;
            ActivationDelaySeconds = activationDelaySeconds;
            TargetPolicy = targetPolicy;
            Reacquisition = reacquisition;
        }

        public GunGuidanceMode Mode { get; }
        public double AcquisitionRange { get; }
        public double TurnRateDegreesPerSecond { get; }
        public double ActivationDelaySeconds { get; }
        public GunTargetPolicy TargetPolicy { get; }
        public GunReacquisitionMode Reacquisition { get; }

        public static GunGuidanceSpec Unguided()
        {
            return new GunGuidanceSpec(
                GunGuidanceMode.Unguided,
                0d,
                0d,
                0d,
                GunTargetPolicy.ClosestToAim,
                GunReacquisitionMode.None);
        }

        public static GunGuidanceSpec Homing(
            double acquisitionRange,
            double turnRateDegreesPerSecond,
            double activationDelaySeconds,
            GunTargetPolicy targetPolicy,
            GunReacquisitionMode reacquisition)
        {
            RequireFinitePositive(acquisitionRange, nameof(acquisitionRange));
            RequireFinitePositive(
                turnRateDegreesPerSecond,
                nameof(turnRateDegreesPerSecond));
            RequireFiniteNonNegative(
                activationDelaySeconds,
                nameof(activationDelaySeconds));
            if (!Enum.IsDefined(typeof(GunTargetPolicy), targetPolicy))
            {
                throw new ArgumentOutOfRangeException(nameof(targetPolicy));
            }
            if (!Enum.IsDefined(typeof(GunReacquisitionMode), reacquisition))
            {
                throw new ArgumentOutOfRangeException(nameof(reacquisition));
            }
            return new GunGuidanceSpec(
                GunGuidanceMode.Homing,
                acquisitionRange,
                turnRateDegreesPerSecond,
                activationDelaySeconds,
                targetPolicy,
                reacquisition);
        }

        private static void RequireFinitePositive(double value, string parameterName)
        {
            if (double.IsNaN(value) || double.IsInfinity(value) || value <= 0d)
            {
                throw new ArgumentOutOfRangeException(parameterName);
            }
        }

        private static void RequireFiniteNonNegative(double value, string parameterName)
        {
            if (double.IsNaN(value) || double.IsInfinity(value) || value < 0d)
            {
                throw new ArgumentOutOfRangeException(parameterName);
            }
        }
    }

    public sealed class GunRicochetSpec
    {
        public GunRicochetSpec(
            int maximumRicochets,
            double retainedSpeedPerRicochet,
            double randomAngleDegrees)
            : this(
                new RicochetValue(checked(maximumRicochets * 10)),
                maximumRicochets,
                retainedSpeedPerRicochet,
                randomAngleDegrees,
                1d,
                0d)
        {
        }

        /// <summary>
        /// Transitional constructor for the previous independent maximum/chance contract. It is
        /// intentionally not reinterpreted as the canonical integer-plus-one-fraction budget.
        /// </summary>
        public GunRicochetSpec(
            int maximumRicochets,
            double retainedSpeedPerRicochet,
            double randomAngleDegrees,
            double bounceChance,
            double postBounceHomingPauseSeconds)
            : this(
                null,
                maximumRicochets,
                retainedSpeedPerRicochet,
                randomAngleDegrees,
                bounceChance,
                postBounceHomingPauseSeconds)
        {
        }

        /// <summary>
        /// Canonical fixed-point ricochet contract. Guaranteed bounces consume exactly one whole
        /// unit. The fractional remainder is rolled once for one final bounce using the existing
        /// deterministic random authority, then the budget is exhausted.
        /// </summary>
        public GunRicochetSpec(
            RicochetValue budget,
            double retainedSpeedPerRicochet,
            double randomAngleDegrees,
            double postBounceHomingPauseSeconds)
            : this(
                budget,
                checked(
                    budget.GuaranteedBounces
                    + (budget.HasFractionalFinalBounce ? 1 : 0)),
                retainedSpeedPerRicochet,
                randomAngleDegrees,
                budget.HasFractionalFinalBounce
                    ? budget.FractionalFinalBounceChance
                    : 1d,
                postBounceHomingPauseSeconds)
        {
            if (budget.Tenths < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(budget));
            }
        }

        private GunRicochetSpec(
            RicochetValue? fixedPointBudget,
            int maximumRicochets,
            double retainedSpeedPerRicochet,
            double randomAngleDegrees,
            double bounceChance,
            double postBounceHomingPauseSeconds)
        {
            if (maximumRicochets < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(maximumRicochets));
            }
            if (double.IsNaN(retainedSpeedPerRicochet)
                || double.IsInfinity(retainedSpeedPerRicochet)
                || retainedSpeedPerRicochet <= 0d
                || retainedSpeedPerRicochet > 1d)
            {
                throw new ArgumentOutOfRangeException(nameof(retainedSpeedPerRicochet));
            }
            if (double.IsNaN(randomAngleDegrees)
                || double.IsInfinity(randomAngleDegrees)
                || randomAngleDegrees < 0d)
            {
                throw new ArgumentOutOfRangeException(nameof(randomAngleDegrees));
            }
            if (double.IsNaN(bounceChance)
                || double.IsInfinity(bounceChance)
                || bounceChance < 0d
                || bounceChance > 1d)
            {
                throw new ArgumentOutOfRangeException(nameof(bounceChance));
            }
            if (double.IsNaN(postBounceHomingPauseSeconds)
                || double.IsInfinity(postBounceHomingPauseSeconds)
                || postBounceHomingPauseSeconds < 0d)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(postBounceHomingPauseSeconds));
            }

            FixedPointBudget = fixedPointBudget;
            MaximumRicochets = maximumRicochets;
            RetainedSpeedPerRicochet = retainedSpeedPerRicochet;
            RandomAngleDegrees = randomAngleDegrees;
            BounceChance = bounceChance;
            PostBounceHomingPauseSeconds = postBounceHomingPauseSeconds;
        }

        public RicochetValue? FixedPointBudget { get; }
        public bool HasCanonicalFixedPointBudget
        {
            get { return FixedPointBudget.HasValue; }
        }
        public int MaximumRicochets { get; }

        public int MaximumSuccessfulBounces
        {
            get { return MaximumRicochets; }
        }

        public double RetainedSpeedPerRicochet { get; }
        public double RandomAngleDegrees { get; }
        public double BounceChance { get; }
        public double PostBounceHomingPauseSeconds { get; }
    }

    public sealed class GunExplosionTriggerSpec
    {
        public GunExplosionTriggerSpec(
            bool onEnemyImpact,
            bool onWallImpact,
            bool onRangeExpiry,
            bool onTermination)
        {
            if (!onEnemyImpact && !onWallImpact && !onRangeExpiry && !onTermination)
            {
                throw new ArgumentException(
                    "Explosion trigger configuration must enable at least one event.");
            }
            OnEnemyImpact = onEnemyImpact;
            OnWallImpact = onWallImpact;
            OnRangeExpiry = onRangeExpiry;
            OnTermination = onTermination;
        }

        public bool OnEnemyImpact { get; }
        public bool OnWallImpact { get; }
        public bool OnRangeExpiry { get; }
        public bool OnTermination { get; }
    }

    public sealed class GunImpactSpec
    {
        private GunImpactSpec(
            bool handlesEnemyImpact,
            bool handlesWallImpact,
            bool handlesRangeExpiry,
            bool handlesTermination,
            GunRicochetSpec ricochet,
            GunExplosionTriggerSpec explosionTrigger)
        {
            HandlesEnemyImpact = handlesEnemyImpact;
            HandlesWallImpact = handlesWallImpact;
            HandlesRangeExpiry = handlesRangeExpiry;
            HandlesTermination = handlesTermination;
            Ricochet = ricochet;
            ExplosionTrigger = explosionTrigger;
        }

        public bool HandlesEnemyImpact { get; }
        public bool HandlesWallImpact { get; }
        public bool HandlesRangeExpiry { get; }
        public bool HandlesTermination { get; }
        public GunRicochetSpec Ricochet { get; }
        public GunExplosionTriggerSpec ExplosionTrigger { get; }

        public static GunImpactSpec Create(
            bool handlesEnemyImpact,
            bool handlesWallImpact,
            bool handlesRangeExpiry,
            bool handlesTermination,
            GunRicochetSpec ricochet,
            GunExplosionTriggerSpec explosionTrigger)
        {
            if (ricochet != null && !handlesWallImpact)
            {
                throw new ArgumentException(
                    "Ricochet configuration requires wall-impact handling.",
                    nameof(ricochet));
            }
            if (explosionTrigger != null)
            {
                if (explosionTrigger.OnEnemyImpact && !handlesEnemyImpact)
                {
                    throw new ArgumentException(
                        "Enemy-impact explosion trigger requires enemy-impact handling.");
                }
                if (explosionTrigger.OnWallImpact && !handlesWallImpact)
                {
                    throw new ArgumentException(
                        "Wall-impact explosion trigger requires wall-impact handling.");
                }
                if (explosionTrigger.OnRangeExpiry && !handlesRangeExpiry)
                {
                    throw new ArgumentException(
                        "Range-expiry explosion trigger requires range-expiry handling.");
                }
                if (explosionTrigger.OnTermination && !handlesTermination)
                {
                    throw new ArgumentException(
                        "Termination explosion trigger requires termination handling.");
                }
            }

            return new GunImpactSpec(
                handlesEnemyImpact,
                handlesWallImpact,
                handlesRangeExpiry,
                handlesTermination,
                ricochet,
                explosionTrigger);
        }
    }
}
