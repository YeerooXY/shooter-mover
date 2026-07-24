using System;

namespace ShooterMover.Domain.Weapons
{
    public enum WeaponFireMode
    {
        SemiAutomatic = 1,
        Automatic = 2,
        Burst = 3,
        Continuous = 4,
    }

    /// <summary>
    /// Immutable trigger and cadence configuration. Projectile cadence and continuous
    /// damage cadence are intentionally separate so one cannot silently stand in for the other.
    /// </summary>
    public sealed class WeaponFireSettings
    {
        private WeaponFireSettings(
            WeaponFireMode mode,
            double shotsPerSecond,
            int shotsPerTrigger,
            int shotsPerBurst,
            double intervalBetweenBurstShotsSeconds,
            double intervalAfterBurstSeconds,
            double damageTicksPerSecond)
        {
            Mode = mode;
            ShotsPerSecond = shotsPerSecond;
            ShotsPerTrigger = shotsPerTrigger;
            ShotsPerBurst = shotsPerBurst;
            IntervalBetweenBurstShotsSeconds = intervalBetweenBurstShotsSeconds;
            IntervalAfterBurstSeconds = intervalAfterBurstSeconds;
            DamageTicksPerSecond = damageTicksPerSecond;
        }

        public WeaponFireMode Mode { get; }

        /// <summary>
        /// Projectile shots authored per second. Always zero for continuous weapons.
        /// </summary>
        public double ShotsPerSecond { get; }

        /// <summary>
        /// Trigger-level shot groups. This is not the projectile count emitted by one shot.
        /// </summary>
        public int ShotsPerTrigger { get; }

        /// <summary>
        /// Sequential shots inside one burst. This is not projectiles per shot.
        /// </summary>
        public int ShotsPerBurst { get; }

        public double IntervalBetweenBurstShotsSeconds { get; }
        public double IntervalAfterBurstSeconds { get; }

        /// <summary>
        /// Explicit continuous-damage evaluation rate. Always zero for projectile modes.
        /// </summary>
        public double DamageTicksPerSecond { get; }

        public bool IsContinuous
        {
            get { return Mode == WeaponFireMode.Continuous; }
        }

        public static WeaponFireSettings Create(
            WeaponFireMode mode,
            double shotsPerSecond,
            int shotsPerTrigger,
            int shotsPerBurst,
            double intervalBetweenBurstShotsSeconds,
            double intervalAfterBurstSeconds,
            double damageTicksPerSecond)
        {
            ValidateFiniteNonNegative(shotsPerSecond, nameof(shotsPerSecond));
            ValidateFiniteNonNegative(
                intervalBetweenBurstShotsSeconds,
                nameof(intervalBetweenBurstShotsSeconds));
            ValidateFiniteNonNegative(
                intervalAfterBurstSeconds,
                nameof(intervalAfterBurstSeconds));
            ValidateFiniteNonNegative(
                damageTicksPerSecond,
                nameof(damageTicksPerSecond));

            switch (mode)
            {
                case WeaponFireMode.SemiAutomatic:
                case WeaponFireMode.Automatic:
                    RequireProjectileCadence(shotsPerSecond, shotsPerTrigger);
                    if (shotsPerBurst != 1)
                    {
                        throw new ArgumentException(
                            "Non-burst projectile fire requires exactly one shot per burst.",
                            nameof(shotsPerBurst));
                    }
                    if (intervalBetweenBurstShotsSeconds != 0d
                        || intervalAfterBurstSeconds != 0d)
                    {
                        throw new ArgumentException(
                            "Non-burst projectile fire cannot author burst intervals.");
                    }
                    if (damageTicksPerSecond != 0d)
                    {
                        throw new ArgumentException(
                            "Projectile fire cannot reuse continuous damage tick rate.",
                            nameof(damageTicksPerSecond));
                    }
                    break;

                case WeaponFireMode.Burst:
                    RequireProjectileCadence(shotsPerSecond, shotsPerTrigger);
                    if (shotsPerBurst < 2)
                    {
                        throw new ArgumentOutOfRangeException(
                            nameof(shotsPerBurst),
                            "Burst fire requires at least two sequential shots.");
                    }
                    if (intervalBetweenBurstShotsSeconds <= 0d
                        || intervalAfterBurstSeconds <= 0d)
                    {
                        throw new ArgumentException(
                            "Burst fire requires explicit in-burst and post-burst intervals.");
                    }
                    if (damageTicksPerSecond != 0d)
                    {
                        throw new ArgumentException(
                            "Burst fire cannot reuse continuous damage tick rate.",
                            nameof(damageTicksPerSecond));
                    }
                    break;

                case WeaponFireMode.Continuous:
                    if (shotsPerSecond != 0d
                        || shotsPerTrigger != 0
                        || shotsPerBurst != 0
                        || intervalBetweenBurstShotsSeconds != 0d
                        || intervalAfterBurstSeconds != 0d)
                    {
                        throw new ArgumentException(
                            "Continuous fire must leave every projectile firing field at zero.");
                    }
                    if (damageTicksPerSecond <= 0d)
                    {
                        throw new ArgumentOutOfRangeException(
                            nameof(damageTicksPerSecond),
                            "Continuous fire requires an explicit positive damage tick rate.");
                    }
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(mode));
            }

            return new WeaponFireSettings(
                mode,
                shotsPerSecond,
                shotsPerTrigger,
                shotsPerBurst,
                intervalBetweenBurstShotsSeconds,
                intervalAfterBurstSeconds,
                damageTicksPerSecond);
        }

        private static void RequireProjectileCadence(
            double shotsPerSecond,
            int shotsPerTrigger)
        {
            if (shotsPerSecond <= 0d)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(shotsPerSecond),
                    "Projectile fire requires a positive shots-per-second value.");
            }
            if (shotsPerTrigger < 1)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(shotsPerTrigger),
                    "Projectile fire requires at least one shot group per trigger.");
            }
        }

        private static void ValidateFiniteNonNegative(double value, string parameterName)
        {
            if (double.IsNaN(value) || double.IsInfinity(value) || value < 0d)
            {
                throw new ArgumentOutOfRangeException(parameterName);
            }
        }
    }

    public enum WeaponShotPatternKind
    {
        Single = 1,
        Spread = 2,
        PulseSpread = 3,
        TwinBarrel = 4,
        Volley = 5,
        Beam = 6,
        Spray = 7,
    }

    /// <summary>
    /// Immutable spatial emission description. ProjectilesPerShot is deliberately independent
    /// from WeaponFireSettings.ShotsPerBurst.
    /// </summary>
    public sealed class WeaponShotPattern
    {
        private WeaponShotPattern(
            WeaponShotPatternKind kind,
            int projectilesPerShot,
            double spreadDegrees,
            double randomnessDegrees,
            int pulsesPerShot,
            double intervalBetweenPulsesSeconds)
        {
            Kind = kind;
            ProjectilesPerShot = projectilesPerShot;
            SpreadDegrees = spreadDegrees;
            RandomnessDegrees = randomnessDegrees;
            PulsesPerShot = pulsesPerShot;
            IntervalBetweenPulsesSeconds = intervalBetweenPulsesSeconds;
        }

        public WeaponShotPatternKind Kind { get; }
        public int ProjectilesPerShot { get; }
        public double SpreadDegrees { get; }
        public double RandomnessDegrees { get; }
        public int PulsesPerShot { get; }
        public double IntervalBetweenPulsesSeconds { get; }

        public bool UsesProjectiles
        {
            get { return ProjectilesPerShot > 0; }
        }

        public static WeaponShotPattern Create(
            WeaponShotPatternKind kind,
            int projectilesPerShot,
            double spreadDegrees,
            double randomnessDegrees,
            int pulsesPerShot,
            double intervalBetweenPulsesSeconds)
        {
            if (projectilesPerShot < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(projectilesPerShot));
            }
            ValidateFiniteNonNegative(spreadDegrees, nameof(spreadDegrees));
            ValidateFiniteNonNegative(randomnessDegrees, nameof(randomnessDegrees));
            ValidateFiniteNonNegative(
                intervalBetweenPulsesSeconds,
                nameof(intervalBetweenPulsesSeconds));
            if (pulsesPerShot < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(pulsesPerShot));
            }

            switch (kind)
            {
                case WeaponShotPatternKind.Single:
                    RequireExact(projectilesPerShot, 1, nameof(projectilesPerShot));
                    RequireZero(spreadDegrees, randomnessDegrees);
                    RequireSinglePulse(pulsesPerShot, intervalBetweenPulsesSeconds);
                    break;

                case WeaponShotPatternKind.Spread:
                    RequireProjectileCount(projectilesPerShot, 2, "Spread");
                    if (spreadDegrees <= 0d)
                    {
                        throw new ArgumentOutOfRangeException(
                            nameof(spreadDegrees),
                            "Spread patterns require a positive authored spread.");
                    }
                    RequireSinglePulse(pulsesPerShot, intervalBetweenPulsesSeconds);
                    break;

                case WeaponShotPatternKind.PulseSpread:
                    RequireProjectileCount(projectilesPerShot, 2, "Pulse spread");
                    if (spreadDegrees <= 0d)
                    {
                        throw new ArgumentOutOfRangeException(nameof(spreadDegrees));
                    }
                    if (pulsesPerShot < 2 || intervalBetweenPulsesSeconds <= 0d)
                    {
                        throw new ArgumentException(
                            "Pulse spread requires at least two pulses and a positive pulse interval.");
                    }
                    break;

                case WeaponShotPatternKind.TwinBarrel:
                    RequireExact(projectilesPerShot, 2, nameof(projectilesPerShot));
                    RequireSinglePulse(pulsesPerShot, intervalBetweenPulsesSeconds);
                    break;

                case WeaponShotPatternKind.Volley:
                    RequireProjectileCount(projectilesPerShot, 2, "Volley");
                    RequireSinglePulse(pulsesPerShot, intervalBetweenPulsesSeconds);
                    break;

                case WeaponShotPatternKind.Beam:
                    RequireExact(projectilesPerShot, 0, nameof(projectilesPerShot));
                    RequireZero(spreadDegrees, randomnessDegrees);
                    RequireSinglePulse(pulsesPerShot, intervalBetweenPulsesSeconds);
                    break;

                case WeaponShotPatternKind.Spray:
                    if (randomnessDegrees <= 0d)
                    {
                        throw new ArgumentOutOfRangeException(
                            nameof(randomnessDegrees),
                            "Spray patterns require positive randomness.");
                    }
                    RequireSinglePulse(pulsesPerShot, intervalBetweenPulsesSeconds);
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(kind));
            }

            return new WeaponShotPattern(
                kind,
                projectilesPerShot,
                spreadDegrees,
                randomnessDegrees,
                pulsesPerShot,
                intervalBetweenPulsesSeconds);
        }

        private static void RequireProjectileCount(int value, int minimum, string label)
        {
            if (value < minimum)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(value),
                    label + " requires an explicit projectile count of at least " + minimum + ".");
            }
        }

        private static void RequireExact(int value, int expected, string parameterName)
        {
            if (value != expected)
            {
                throw new ArgumentOutOfRangeException(parameterName);
            }
        }

        private static void RequireZero(double spreadDegrees, double randomnessDegrees)
        {
            if (spreadDegrees != 0d || randomnessDegrees != 0d)
            {
                throw new ArgumentException(
                    "This pattern does not support authored spread or randomness.");
            }
        }

        private static void RequireSinglePulse(int pulsesPerShot, double intervalSeconds)
        {
            if (pulsesPerShot != 1 || intervalSeconds != 0d)
            {
                throw new ArgumentException(
                    "This pattern requires one pulse and no pulse interval.");
            }
        }

        private static void ValidateFiniteNonNegative(double value, string parameterName)
        {
            if (double.IsNaN(value) || double.IsInfinity(value) || value < 0d)
            {
                throw new ArgumentOutOfRangeException(parameterName);
            }
        }
    }
}
