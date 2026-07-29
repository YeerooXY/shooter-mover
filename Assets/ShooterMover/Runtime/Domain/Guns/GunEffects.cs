using System;

namespace ShooterMover.Domain.Guns
{
    public enum GunDamageCategory
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
    public static class GunDamageCategoryConversion
    {
        public static bool TryFromCatalogValue(
            string catalogValue,
            out GunDamageCategory category)
        {
            if (string.Equals(catalogValue, "Physical", StringComparison.Ordinal))
            {
                category = GunDamageCategory.Physical;
                return true;
            }
            if (string.Equals(catalogValue, "Thermal", StringComparison.Ordinal))
            {
                category = GunDamageCategory.Thermal;
                return true;
            }
            if (string.Equals(catalogValue, "Chemical", StringComparison.Ordinal))
            {
                category = GunDamageCategory.Chemical;
                return true;
            }
            if (string.Equals(catalogValue, "Energy", StringComparison.Ordinal))
            {
                category = GunDamageCategory.Energy;
                return true;
            }

            category = default(GunDamageCategory);
            return false;
        }

        public static GunDamageCategory FromCatalogValue(string catalogValue)
        {
            GunDamageCategory category;
            if (!TryFromCatalogValue(catalogValue, out category))
            {
                throw new FormatException(
                    "Unknown gun damage category '"
                    + (catalogValue ?? "<null>")
                    + "'. Expected Physical, Thermal, Chemical, or Energy.");
            }
            return category;
        }

        public static string ToCatalogValue(GunDamageCategory category)
        {
            switch (category)
            {
                case GunDamageCategory.Physical:
                    return "Physical";
                case GunDamageCategory.Thermal:
                    return "Thermal";
                case GunDamageCategory.Chemical:
                    return "Chemical";
                case GunDamageCategory.Energy:
                    return "Energy";
                default:
                    throw new ArgumentOutOfRangeException(nameof(category));
            }
        }
    }

    public sealed class GunDamageSpec
    {
        private GunDamageSpec(
            GunDamageCategory category,
            double directDamage,
            double areaDamage,
            GunDamageOverTimeStats damageOverTime,
            double knockback)
        {
            Category = category;
            DirectDamage = directDamage;
            AreaDamage = areaDamage;
            DamageOverTime = damageOverTime;
            Knockback = knockback;
        }

        public GunDamageCategory Category { get; }
        public double DirectDamage { get; }
        public double AreaDamage { get; }

        /// <summary>
        /// Canonical optional DoT data. Legacy magnitude/duration projections remain below for
        /// the current evaluator and execution adapters.
        /// </summary>
        public GunDamageOverTimeStats DamageOverTime { get; }
        public double DamageOverTimePerSecond
        {
            get { return DamageOverTime == null ? 0d : DamageOverTime.DamagePerSecond; }
        }
        public double DamageOverTimeDurationSeconds
        {
            get { return DamageOverTime == null ? 0d : DamageOverTime.DurationSeconds; }
        }
        public double Knockback { get; }

        public bool HasAreaDamage
        {
            get { return AreaDamage > 0d; }
        }

        public bool HasDamageOverTime
        {
            get { return DamageOverTime != null; }
        }

        /// <summary>
        /// Canonical damaging-gun factory. Area damage remains a delivery/effect concern.
        /// </summary>
        public static GunDamageSpec Create(
            GunDamageCategory category,
            double directDamage,
            GunDamageOverTimeStats damageOverTime,
            double knockback)
        {
            return CreateCore(
                category,
                directDamage,
                0d,
                damageOverTime,
                knockback);
        }

        /// <summary>
        /// Transitional compatibility factory for the flat catalogue and existing effect adapter.
        /// Zero/zero DoT values are converted to an absent typed value rather than retained as
        /// canonical placeholder data.
        /// </summary>
        public static GunDamageSpec Create(
            GunDamageCategory category,
            double directDamage,
            double areaDamage,
            double damageOverTimePerSecond,
            double damageOverTimeDurationSeconds,
            double knockback)
        {
            RequireFiniteNonNegative(
                damageOverTimePerSecond,
                nameof(damageOverTimePerSecond));
            RequireFiniteNonNegative(
                damageOverTimeDurationSeconds,
                nameof(damageOverTimeDurationSeconds));

            bool hasDotDamage = damageOverTimePerSecond > 0d;
            bool hasDotDuration = damageOverTimeDurationSeconds > 0d;
            if (hasDotDamage != hasDotDuration)
            {
                throw new ArgumentException(
                    "Damage-over-time magnitude and duration must both be zero or both be positive.");
            }

            GunDamageOverTimeStats damageOverTime = hasDotDamage
                ? new GunDamageOverTimeStats(
                    damageOverTimePerSecond,
                    damageOverTimeDurationSeconds)
                : null;
            return CreateCore(
                category,
                directDamage,
                areaDamage,
                damageOverTime,
                knockback);
        }

        private static GunDamageSpec CreateCore(
            GunDamageCategory category,
            double directDamage,
            double areaDamage,
            GunDamageOverTimeStats damageOverTime,
            double knockback)
        {
            if (!Enum.IsDefined(typeof(GunDamageCategory), category))
            {
                throw new ArgumentOutOfRangeException(nameof(category));
            }
            RequireFiniteNonNegative(directDamage, nameof(directDamage));
            RequireFiniteNonNegative(areaDamage, nameof(areaDamage));
            RequireFiniteNonNegative(knockback, nameof(knockback));

            return new GunDamageSpec(
                category,
                directDamage,
                areaDamage,
                damageOverTime,
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

    public sealed class GunExplosionEffect
    {
        public GunExplosionEffect(double radius, double minimumDamageMultiplier)
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

    public sealed class GunDamageOverTimeEffect
    {
        public GunDamageOverTimeEffect(
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

    public sealed class GunChainArcEffect
    {
        public GunChainArcEffect(
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
    public sealed class GunEffects
    {
        public GunEffects(
            GunExplosionEffect explosion,
            GunDamageOverTimeEffect damageOverTime,
            GunChainArcEffect chainArc)
        {
            Explosion = explosion;
            DamageOverTime = damageOverTime;
            ChainArc = chainArc;
        }

        public GunExplosionEffect Explosion { get; }
        public GunDamageOverTimeEffect DamageOverTime { get; }
        public GunChainArcEffect ChainArc { get; }

        public static GunEffects None()
        {
            return new GunEffects(null, null, null);
        }
    }
}
