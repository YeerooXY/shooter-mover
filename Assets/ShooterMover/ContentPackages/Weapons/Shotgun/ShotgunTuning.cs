using System;
using System.Globalization;
using ShooterMover.ContentPackages.Weapons.Shared.Runtime;

namespace ShooterMover.ContentPackages.Weapons.Shotgun
{
    /// <summary>
    /// Immutable authored numeric limits for one shotgun fire profile. The pellet
    /// topology is fixed; empowerment may only change the existing numeric values
    /// explicitly represented here.
    /// </summary>
    public sealed class ShotgunTuning : IEquatable<ShotgunTuning>
    {
        public const int MinimumPelletCount = 3;
        public const int MaximumPelletCount = 12;
        public const double MinimumSpreadDegrees = 1d;
        public const double MaximumSpreadDegrees = 60d;
        public const double MinimumCadenceSeconds = 0.05d;
        public const double MaximumCadenceSeconds = 10d;
        public const double MaximumRecoverySeconds = 10d;
        public const double MaximumDamage = 1000000d;
        public const double MaximumPelletsPerSecond = 48d;
        public const int MaximumConcurrentPelletCount = 48;

        private readonly string canonicalText;

        public ShotgunTuning(
            int pelletCount,
            double spreadDegrees,
            double damage,
            double cadenceSeconds,
            double projectileSpeed,
            double projectileLifetimeSeconds,
            double projectileRadius,
            double recoverySeconds)
        {
            RequireIntegerInRange(
                nameof(pelletCount),
                pelletCount,
                MinimumPelletCount,
                MaximumPelletCount);
            RequireInRange(
                nameof(spreadDegrees),
                spreadDegrees,
                MinimumSpreadDegrees,
                MaximumSpreadDegrees);
            RequireInRange(nameof(damage), damage, double.Epsilon, MaximumDamage);
            RequireInRange(
                nameof(cadenceSeconds),
                cadenceSeconds,
                MinimumCadenceSeconds,
                MaximumCadenceSeconds);
            RequireInRange(
                nameof(recoverySeconds),
                recoverySeconds,
                0d,
                MaximumRecoverySeconds);

            float speed = ConvertToFiniteFloat(projectileSpeed, nameof(projectileSpeed));
            float lifetime = ConvertToFiniteFloat(
                projectileLifetimeSeconds,
                nameof(projectileLifetimeSeconds));
            float radius = ConvertToFiniteFloat(projectileRadius, nameof(projectileRadius));
            if (!BoundedProjectile2D.IsValidSpeed(speed))
            {
                throw new ArgumentOutOfRangeException(nameof(projectileSpeed));
            }

            if (!BoundedProjectile2D.IsValidLifetime(lifetime))
            {
                throw new ArgumentOutOfRangeException(nameof(projectileLifetimeSeconds));
            }

            if (!BoundedProjectile2D.IsValidRadius(radius))
            {
                throw new ArgumentOutOfRangeException(nameof(projectileRadius));
            }

            double pelletsPerSecond = pelletCount / cadenceSeconds;
            if (!IsFinite(pelletsPerSecond)
                || pelletsPerSecond > MaximumPelletsPerSecond)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(cadenceSeconds),
                    cadenceSeconds,
                    "Shotgun tuning exceeds the bounded pellets-per-second budget.");
            }

            int overlappingCycles = Math.Max(
                1,
                (int)Math.Ceiling(projectileLifetimeSeconds / cadenceSeconds));
            int estimatedConcurrentPelletCount = checked(pelletCount * overlappingCycles);
            if (estimatedConcurrentPelletCount > MaximumConcurrentPelletCount)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(projectileLifetimeSeconds),
                    projectileLifetimeSeconds,
                    "Shotgun tuning exceeds the bounded concurrent-pellet budget.");
            }

            PelletCount = pelletCount;
            SpreadDegrees = spreadDegrees;
            Damage = damage;
            CadenceSeconds = cadenceSeconds;
            ProjectileSpeed = projectileSpeed;
            ProjectileLifetimeSeconds = projectileLifetimeSeconds;
            ProjectileRadius = projectileRadius;
            RecoverySeconds = recoverySeconds;
            PelletsPerSecond = pelletsPerSecond;
            EstimatedConcurrentPelletCount = estimatedConcurrentPelletCount;
            canonicalText = BuildCanonicalText();
        }

        public int PelletCount { get; }

        public double SpreadDegrees { get; }

        public double Damage { get; }

        public double CadenceSeconds { get; }

        public double ProjectileSpeed { get; }

        public double ProjectileLifetimeSeconds { get; }

        public double ProjectileRadius { get; }

        public double RecoverySeconds { get; }

        public double PelletsPerSecond { get; }

        public int EstimatedConcurrentPelletCount { get; }

        public static void ValidateEmpowermentBoundary(
            ShotgunTuning normal,
            ShotgunTuning empowered)
        {
            if (normal == null)
            {
                throw new ArgumentNullException(nameof(normal));
            }

            if (empowered == null)
            {
                throw new ArgumentNullException(nameof(empowered));
            }

            if (normal.PelletCount != empowered.PelletCount)
            {
                throw new ArgumentException(
                    "Empowerment cannot change shotgun pellet topology.",
                    nameof(empowered));
            }

            if (normal.ProjectileLifetimeSeconds
                    != empowered.ProjectileLifetimeSeconds
                || normal.ProjectileRadius != empowered.ProjectileRadius)
            {
                throw new ArgumentException(
                    "Empowerment may tune only spread, damage, cadence, speed, or recovery.",
                    nameof(empowered));
            }
        }

        public string ToCanonicalString()
        {
            return canonicalText;
        }

        public bool Equals(ShotgunTuning other)
        {
            return !ReferenceEquals(other, null)
                && string.Equals(canonicalText, other.canonicalText, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as ShotgunTuning);
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

        private string BuildCanonicalText()
        {
            return string.Join(
                "\n",
                new[]
                {
                    "pellet_count=" + PelletCount.ToString(CultureInfo.InvariantCulture),
                    "spread_degrees=" + SpreadDegrees.ToString("R", CultureInfo.InvariantCulture),
                    "damage=" + Damage.ToString("R", CultureInfo.InvariantCulture),
                    "cadence_seconds=" + CadenceSeconds.ToString("R", CultureInfo.InvariantCulture),
                    "projectile_speed=" + ProjectileSpeed.ToString("R", CultureInfo.InvariantCulture),
                    "projectile_lifetime_seconds="
                        + ProjectileLifetimeSeconds.ToString("R", CultureInfo.InvariantCulture),
                    "projectile_radius="
                        + ProjectileRadius.ToString("R", CultureInfo.InvariantCulture),
                    "recovery_seconds=" + RecoverySeconds.ToString("R", CultureInfo.InvariantCulture),
                    "pellets_per_second=" + PelletsPerSecond.ToString("R", CultureInfo.InvariantCulture),
                    "estimated_concurrent_pellets="
                        + EstimatedConcurrentPelletCount.ToString(CultureInfo.InvariantCulture),
                });
        }

        private static float ConvertToFiniteFloat(double value, string parameterName)
        {
            if (!IsFinite(value))
            {
                throw new ArgumentOutOfRangeException(parameterName);
            }

            float converted = (float)value;
            if (float.IsNaN(converted) || float.IsInfinity(converted))
            {
                throw new ArgumentOutOfRangeException(parameterName);
            }

            return converted;
        }

        private static void RequireIntegerInRange(
            string parameterName,
            int value,
            int minimum,
            int maximum)
        {
            if (value < minimum || value > maximum)
            {
                throw new ArgumentOutOfRangeException(parameterName);
            }
        }

        private static void RequireInRange(
            string parameterName,
            double value,
            double minimum,
            double maximum)
        {
            if (!IsFinite(value) || value < minimum || value > maximum)
            {
                throw new ArgumentOutOfRangeException(parameterName);
            }
        }

        private static bool IsFinite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }
    }
}
