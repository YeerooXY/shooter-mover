using System;

namespace ShooterMover.Domain.Weapons
{
    public enum WeaponDamageCategory
    {
        Physical = 1,
        Thermal = 2,
        Chemical = 3,
        Energy = 4,
    }

    /// <summary>
    /// Exact, opt-in bridge from the current string catalog. Unknown values never fall back
    /// to another category and must be surfaced to the caller.
    /// </summary>
    public static class WeaponDamageCategoryConversion
    {
        public static bool TryFromCatalogValue(
            string catalogValue,
            out WeaponDamageCategory category)
        {
            if (string.Equals(catalogValue, "Physical", StringComparison.Ordinal))
            {
                category = WeaponDamageCategory.Physical;
                return true;
            }
            if (string.Equals(catalogValue, "Thermal", StringComparison.Ordinal))
            {
                category = WeaponDamageCategory.Thermal;
                return true;
            }
            if (string.Equals(catalogValue, "Chemical", StringComparison.Ordinal))
            {
                category = WeaponDamageCategory.Chemical;
                return true;
            }
            if (string.Equals(catalogValue, "Energy", StringComparison.Ordinal))
            {
                category = WeaponDamageCategory.Energy;
                return true;
            }

            category = default(WeaponDamageCategory);
            return false;
        }

        public static WeaponDamageCategory FromCatalogValue(string catalogValue)
        {
            WeaponDamageCategory category;
            if (!TryFromCatalogValue(catalogValue, out category))
            {
                throw new FormatException(
                    "Unknown weapon damage category '"
                    + (catalogValue ?? "<null>")
                    + "'. Expected Physical, Thermal, Chemical, or Energy.");
            }
            return category;
        }

        public static string ToCatalogValue(WeaponDamageCategory category)
        {
            switch (category)
            {
                case WeaponDamageCategory.Physical:
                    return "Physical";
                case WeaponDamageCategory.Thermal:
                    return "Thermal";
                case WeaponDamageCategory.Chemical:
                    return "Chemical";
                case WeaponDamageCategory.Energy:
                    return "Energy";
                default:
                    throw new ArgumentOutOfRangeException(nameof(category));
            }
        }
    }

    public sealed class WeaponDamageSpec
    {
        private WeaponDamageSpec(
            WeaponDamageCategory category,
            double directDamage,
            double areaDamage,
            double damageOverTimePerSecond,
            double damageOverTimeDurationSeconds,
            double knockback)
        {
            Category = category;
            DirectDamage = directDamage;
            AreaDamage = areaDamage;
            DamageOverTimePerSecond = damageOverTimePerSecond;
            DamageOverTimeDurationSeconds = damageOverTimeDurationSeconds;
            Knockback = knockback;
        }

        public WeaponDamageCategory Category { get; }
        public double DirectDamage { get; }
        public double AreaDamage { get; }
        public double DamageOverTimePerSecond { get; }
        public double DamageOverTimeDurationSeconds { get; }
        public double Knockback { get; }

        public bool HasAreaDamage
        {
            get { return AreaDamage > 0d; }
        }

        public bool HasDamageOverTime
        {
            get { return DamageOverTimePerSecond > 0d; }
        }

        public static WeaponDamageSpec Create(
            WeaponDamageCategory category,
            double directDamage,
            double areaDamage,
            double damageOverTimePerSecond,
            double damageOverTimeDurationSeconds,
            double knockback)
        {
            if (!Enum.IsDefined(typeof(WeaponDamageCategory), category))
            {
                throw new ArgumentOutOfRangeException(nameof(category));
            }
            RequireFiniteNonNegative(directDamage, nameof(directDamage));
            RequireFiniteNonNegative(areaDamage, nameof(areaDamage));
            RequireFiniteNonNegative(
                damageOverTimePerSecond,
                nameof(damageOverTimePerSecond));
            RequireFiniteNonNegative(
                damageOverTimeDurationSeconds,
                nameof(damageOverTimeDurationSeconds));
            RequireFiniteNonNegative(knockback, nameof(knockback));

            bool hasDotDamage = damageOverTimePerSecond > 0d;
            bool hasDotDuration = damageOverTimeDurationSeconds > 0d;
            if (hasDotDamage != hasDotDuration)
            {
                throw new ArgumentException(
                    "Damage-over-time magnitude and duration must both be zero or both be positive.");
            }

            return new WeaponDamageSpec(
                category,
                directDamage,
                areaDamage,
                damageOverTimePerSecond,
                damageOverTimeDurationSeconds,
                knockback);
        }

        private static void RequireFiniteNonNegative(double value, string parameterName)
        {
            if (double.IsNaN(value) || double.IsInfinity(value) || value < 0d)
            {
                throw new ArgumentOutOfRangeException(parameterName);
            }
        }
    }

    public sealed class WeaponExplosionEffect
    {
        public WeaponExplosionEffect(double radius, double minimumDamageMultiplier)
        {
            if (double.IsNaN(radius) || double.IsInfinity(radius) || radius <= 0d)
            {
                throw new ArgumentOutOfRangeException(nameof(radius));
            }
            if (double.IsNaN(minimumDamageMultiplier)
                || double.IsInfinity(minimumDamageMultiplier)
                || minimumDamageMultiplier < 0d
                || minimumDamageMultiplier > 1d)
            {
                throw new ArgumentOutOfRangeException(nameof(minimumDamageMultiplier));
            }
            Radius = radius;
            MinimumDamageMultiplier = minimumDamageMultiplier;
        }

        public double Radius { get; }
        public double MinimumDamageMultiplier { get; }
    }

    public sealed class WeaponDamageOverTimeEffect
    {
        public WeaponDamageOverTimeEffect(
            double ticksPerSecond,
            int maximumStacks,
            bool refreshesDuration)
        {
            if (double.IsNaN(ticksPerSecond)
                || double.IsInfinity(ticksPerSecond)
                || ticksPerSecond <= 0d)
            {
                throw new ArgumentOutOfRangeException(nameof(ticksPerSecond));
            }
            if (maximumStacks < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(maximumStacks));
            }
            TicksPerSecond = ticksPerSecond;
            MaximumStacks = maximumStacks;
            RefreshesDuration = refreshesDuration;
        }

        public double TicksPerSecond { get; }
        public int MaximumStacks { get; }
        public bool RefreshesDuration { get; }
    }

    public sealed class WeaponChainArcEffect
    {
        public WeaponChainArcEffect(
            int maximumTargets,
            double acquisitionRange,
            double retainedDamagePerJump)
        {
            if (maximumTargets < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(maximumTargets));
            }
            if (double.IsNaN(acquisitionRange)
                || double.IsInfinity(acquisitionRange)
                || acquisitionRange <= 0d)
            {
                throw new ArgumentOutOfRangeException(nameof(acquisitionRange));
            }
            if (double.IsNaN(retainedDamagePerJump)
                || double.IsInfinity(retainedDamagePerJump)
                || retainedDamagePerJump < 0d
                || retainedDamagePerJump > 1d)
            {
                throw new ArgumentOutOfRangeException(nameof(retainedDamagePerJump));
            }
            MaximumTargets = maximumTargets;
            AcquisitionRange = acquisitionRange;
            RetainedDamagePerJump = retainedDamagePerJump;
        }

        public int MaximumTargets { get; }
        public double AcquisitionRange { get; }
        public double RetainedDamagePerJump { get; }
    }

    /// <summary>
    /// Optional reusable effect descriptions. They contain no Unity behavior or runtime state.
    /// </summary>
    public sealed class WeaponEffects
    {
        public WeaponEffects(
            WeaponExplosionEffect explosion,
            WeaponDamageOverTimeEffect damageOverTime,
            WeaponChainArcEffect chainArc)
        {
            Explosion = explosion;
            DamageOverTime = damageOverTime;
            ChainArc = chainArc;
        }

        public WeaponExplosionEffect Explosion { get; }
        public WeaponDamageOverTimeEffect DamageOverTime { get; }
        public WeaponChainArcEffect ChainArc { get; }

        public static WeaponEffects None()
        {
            return new WeaponEffects(null, null, null);
        }
    }
}
