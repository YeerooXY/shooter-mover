using System;

namespace ShooterMover.Domain.Weapons
{
    public enum WeaponFireMode
    {
        SemiAutomatic = 1,
        Automatic = 2,
        Burst = 3,

        // Transitional compatibility for pre-WEAPON-DATA-002 contracts. Canonical authored
        // weapons use semi-automatic, automatic, or burst even when their delivery is a laser.
        Continuous = 4,
    }

    public sealed class WeaponBurstSettings
    {
        public WeaponBurstSettings(
            int shotsPerBurst,
            double intervalBetweenShotsSeconds)
        {
            if (shotsPerBurst < 2)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(shotsPerBurst),
                    "Burst fire requires at least two sequential shots.");
            }
            if (double.IsNaN(intervalBetweenShotsSeconds)
                || double.IsInfinity(intervalBetweenShotsSeconds)
                || intervalBetweenShotsSeconds <= 0d)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(intervalBetweenShotsSeconds));
            }

            ShotsPerBurst = shotsPerBurst;
            IntervalBetweenShotsSeconds = intervalBetweenShotsSeconds;
        }

        public int ShotsPerBurst { get; }
        public double IntervalBetweenShotsSeconds { get; }
    }

    /// <summary>
    /// Immutable trigger and firing-cycle cadence. RateOfFire means how frequently a new firing
    /// cycle may begin. Sequential burst shots remain separate from simultaneous projectiles per
    /// shot. Continuous fields remain only for the explicit legacy migration boundary.
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
            BurstSettings = mode == WeaponFireMode.Burst
                ? new WeaponBurstSettings(
                    shotsPerBurst,
                    intervalBetweenBurstShotsSeconds)
                : null;
        }

        public WeaponFireMode Mode { get; }

        /// <summary>
        /// Canonical firing cycles per second. The legacy property name is retained because the
        /// current scheduler already consumes this exact value.
        /// </summary>
        public double RateOfFire { get { return ShotsPerSecond; } }
        public double ShotsPerSecond { get; }

        /// <summary>
        /// Transitional trigger-level grouping. Canonical authored definitions always use one.
        /// It is never the projectile count emitted by a shot.
        /// </summary>
        public int ShotsPerTrigger { get; }

        /// <summary>
        /// Sequential shots inside one burst. This is not projectiles per shot.
        /// </summary>
        public int ShotsPerBurst { get; }

        public double IntervalBetweenBurstShotsSeconds { get; }
        public double IntervalAfterBurstSeconds { get; }
        public double DamageTicksPerSecond { get; }
        public WeaponBurstSettings BurstSettings { get; }

        public bool IsContinuous
        {
            get { return Mode == WeaponFireMode.Continuous; }
        }

        public bool IsCanonicalAuthoredMode
        {
            get
            {
                return Mode == WeaponFireMode.SemiAutomatic
                    || Mode == WeaponFireMode.Automatic
                    || Mode == WeaponFireMode.Burst;
            }
        }

        public static WeaponFireSettings SemiAutomatic(double rateOfFire)
        {
            return Create(
                WeaponFireMode.SemiAutomatic,
                rateOfFire,
                1,
                1,
                0d,
                0d,
                0d);
        }

        public static WeaponFireSettings Automatic(double rateOfFire)
        {
            return Create(
                WeaponFireMode.Automatic,
                rateOfFire,
                1,
                1,
                0d,
                0d,
                0d);
        }

        public static WeaponFireSettings Burst(
            double rateOfFire,
            WeaponBurstSettings burst)
        {
            if (burst == null)
            {
                throw new ArgumentNullException(nameof(burst));
            }

            return Create(
                WeaponFireMode.Burst,
                rateOfFire,
                1,
                burst.ShotsPerBurst,
                burst.IntervalBetweenShotsSeconds,
                0d,
                0d);
        }

        /// <summary>
        /// Compatibility factory retained for the current catalogue mapper and scheduler.
        /// Canonical content should use SemiAutomatic, Automatic, or Burst.
        /// </summary>
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
                            "Non-burst fire requires exactly one sequential shot per cycle.",
                            nameof(shotsPerBurst));
                    }
                    if (intervalBetweenBurstShotsSeconds != 0d
                        || intervalAfterBurstSeconds != 0d)
                    {
                        throw new ArgumentException(
                            "Non-burst fire cannot carry burst-only settings.");
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
                    if (intervalBetweenBurstShotsSeconds <= 0d)
                    {
                        throw new ArgumentException(
                            "Burst fire requires a positive interval between sequential shots.");
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
                            "Transitional continuous fire must leave projectile firing fields at zero.");
                    }
                    if (damageTicksPerSecond <= 0d)
                    {
                        throw new ArgumentOutOfRangeException(
                            nameof(damageTicksPerSecond),
                            "Transitional continuous fire requires a positive damage tick rate.");
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
                    "Fire rate must be positive.");
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

        /// <summary>
        /// Canonical designer-facing shot settings. A zero-spread multi-emission shot is a volley;
        /// a positive-spread multi-emission shot is a spread. Neither implies burst fire.
        /// </summary>
        public static WeaponShotPattern Canonical(
            int projectilesPerShot,
            double spreadDegrees)
        {
            if (projectilesPerShot < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(projectilesPerShot));
            }
            if (double.IsNaN(spreadDegrees)
                || double.IsInfinity(spreadDegrees)
                || spreadDegrees < 0d
                || spreadDegrees > 360d)
            {
                throw new ArgumentOutOfRangeException(nameof(spreadDegrees));
            }
            if (projectilesPerShot == 1)
            {
                if (spreadDegrees != 0d)
                {
                    throw new ArgumentException(
                        "One emitted attack cannot author a multi-emission spread arc.",
                        nameof(spreadDegrees));
                }
                return Create(
                    WeaponShotPatternKind.Single,
                    1,
                    0d,
                    0d,
                    1,
                    0d);
            }

            return Create(
                spreadDegrees > 0d
                    ? WeaponShotPatternKind.Spread
                    : WeaponShotPatternKind.Volley,
                projectilesPerShot,
                spreadDegrees,
                0d,
                1,
                0d);
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
