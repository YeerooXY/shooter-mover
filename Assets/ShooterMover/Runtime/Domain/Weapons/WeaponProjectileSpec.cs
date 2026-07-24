using System;

namespace ShooterMover.Domain.Weapons
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

    public enum WeaponProjectileKind
    {
        RegularProjectile = 1,
        Rocket = 2,
        Orb = 3,
    }

    public enum WeaponProjectileTerminationBehavior
    {
        StopOnFirstBlockingImpact = 1,
        StopWhenPierceIsSpent = 2,
        ContinueUntilRangeExpiry = 3,
    }

    public sealed class WeaponProjectileSpec
    {
        private WeaponProjectileSpec(
            WeaponProjectileKind kind,
            double speed,
            double range,
            PierceValue pierce,
            WeaponProjectileTerminationBehavior terminationBehavior)
        {
            Kind = kind;
            Speed = speed;
            Range = range;
            Pierce = pierce;
            TerminationBehavior = terminationBehavior;
        }

        public WeaponProjectileKind Kind { get; }
        public double Speed { get; }
        public double Range { get; }
        public PierceValue Pierce { get; }
        public WeaponProjectileTerminationBehavior TerminationBehavior { get; }

        public static WeaponProjectileSpec Create(
            WeaponProjectileKind kind,
            double speed,
            double range,
            PierceValue pierce,
            WeaponProjectileTerminationBehavior terminationBehavior)
        {
            RequireFinitePositive(speed, nameof(speed));
            RequireFinitePositive(range, nameof(range));
            if (!Enum.IsDefined(typeof(WeaponProjectileKind), kind))
            {
                throw new ArgumentOutOfRangeException(nameof(kind));
            }
            if (!Enum.IsDefined(
                    typeof(WeaponProjectileTerminationBehavior),
                    terminationBehavior))
            {
                throw new ArgumentOutOfRangeException(nameof(terminationBehavior));
            }
            return new WeaponProjectileSpec(
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

    public enum WeaponGuidanceMode
    {
        Unguided = 1,
        Homing = 2,
    }

    public enum WeaponTargetPolicy
    {
        ClosestToAim = 1,
        NearestInRange = 2,
        CurrentLockedTarget = 3,
    }

    public enum WeaponReacquisitionMode
    {
        None = 1,
        ReuseTargetPolicy = 2,
    }

    public sealed class WeaponGuidanceSpec
    {
        private WeaponGuidanceSpec(
            WeaponGuidanceMode mode,
            double acquisitionRange,
            double turnRateDegreesPerSecond,
            double activationDelaySeconds,
            WeaponTargetPolicy targetPolicy,
            WeaponReacquisitionMode reacquisition)
        {
            Mode = mode;
            AcquisitionRange = acquisitionRange;
            TurnRateDegreesPerSecond = turnRateDegreesPerSecond;
            ActivationDelaySeconds = activationDelaySeconds;
            TargetPolicy = targetPolicy;
            Reacquisition = reacquisition;
        }

        public WeaponGuidanceMode Mode { get; }
        public double AcquisitionRange { get; }
        public double TurnRateDegreesPerSecond { get; }
        public double ActivationDelaySeconds { get; }
        public WeaponTargetPolicy TargetPolicy { get; }
        public WeaponReacquisitionMode Reacquisition { get; }

        public static WeaponGuidanceSpec Unguided()
        {
            return new WeaponGuidanceSpec(
                WeaponGuidanceMode.Unguided,
                0d,
                0d,
                0d,
                WeaponTargetPolicy.ClosestToAim,
                WeaponReacquisitionMode.None);
        }

        public static WeaponGuidanceSpec Homing(
            double acquisitionRange,
            double turnRateDegreesPerSecond,
            double activationDelaySeconds,
            WeaponTargetPolicy targetPolicy,
            WeaponReacquisitionMode reacquisition)
        {
            RequireFinitePositive(acquisitionRange, nameof(acquisitionRange));
            RequireFinitePositive(
                turnRateDegreesPerSecond,
                nameof(turnRateDegreesPerSecond));
            RequireFiniteNonNegative(
                activationDelaySeconds,
                nameof(activationDelaySeconds));
            if (!Enum.IsDefined(typeof(WeaponTargetPolicy), targetPolicy))
            {
                throw new ArgumentOutOfRangeException(nameof(targetPolicy));
            }
            if (!Enum.IsDefined(typeof(WeaponReacquisitionMode), reacquisition))
            {
                throw new ArgumentOutOfRangeException(nameof(reacquisition));
            }
            return new WeaponGuidanceSpec(
                WeaponGuidanceMode.Homing,
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

    public sealed class WeaponRicochetSpec
    {
        public WeaponRicochetSpec(
            int maximumRicochets,
            double retainedSpeedPerRicochet,
            double randomAngleDegrees)
            : this(
                maximumRicochets,
                retainedSpeedPerRicochet,
                randomAngleDegrees,
                1d,
                0d)
        {
        }

        public WeaponRicochetSpec(
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

            MaximumRicochets = maximumRicochets;
            RetainedSpeedPerRicochet = retainedSpeedPerRicochet;
            RandomAngleDegrees = randomAngleDegrees;
            BounceChance = bounceChance;
            PostBounceHomingPauseSeconds = postBounceHomingPauseSeconds;
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

    public sealed class WeaponExplosionTriggerSpec
    {
        public WeaponExplosionTriggerSpec(
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

    public sealed class WeaponImpactSpec
    {
        private WeaponImpactSpec(
            bool handlesEnemyImpact,
            bool handlesWallImpact,
            bool handlesRangeExpiry,
            bool handlesTermination,
            WeaponRicochetSpec ricochet,
            WeaponExplosionTriggerSpec explosionTrigger)
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
        public WeaponRicochetSpec Ricochet { get; }
        public WeaponExplosionTriggerSpec ExplosionTrigger { get; }

        public static WeaponImpactSpec Create(
            bool handlesEnemyImpact,
            bool handlesWallImpact,
            bool handlesRangeExpiry,
            bool handlesTermination,
            WeaponRicochetSpec ricochet,
            WeaponExplosionTriggerSpec explosionTrigger)
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

            return new WeaponImpactSpec(
                handlesEnemyImpact,
                handlesWallImpact,
                handlesRangeExpiry,
                handlesTermination,
                ricochet,
                explosionTrigger);
        }
    }
}
