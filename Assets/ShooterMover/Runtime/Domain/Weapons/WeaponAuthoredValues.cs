using System;
using ShooterMover.Domain.Weapons.Execution;

namespace ShooterMover.Domain.Weapons
{
    /// <summary>
    /// Stable, display-facing weapon identity. Content names never select runtime behaviour.
    /// </summary>
    public sealed class WeaponIdentity
    {
        public WeaponIdentity(
            WeaponDefinitionId definitionId,
            string displayName,
            string familyId)
        {
            DefinitionId = definitionId
                ?? throw new ArgumentNullException(nameof(definitionId));
            if (string.IsNullOrWhiteSpace(displayName))
            {
                throw new ArgumentException(
                    "A weapon display name is required.",
                    nameof(displayName));
            }

            DisplayName = displayName;
            FamilyId = familyId ?? string.Empty;
        }

        public WeaponDefinitionId DefinitionId { get; }
        public string DisplayName { get; }
        public string FamilyId { get; }
    }

    /// <summary>
    /// Explicit optional damage-over-time magnitude and duration. Absence is represented by null.
    /// Tick cadence, stacking, and refresh policy remain in the existing reusable effect contract.
    /// </summary>
    public sealed class WeaponDamageOverTimeStats
    {
        public WeaponDamageOverTimeStats(
            double damagePerSecond,
            double durationSeconds)
        {
            RequireFinitePositive(damagePerSecond, nameof(damagePerSecond));
            RequireFinitePositive(durationSeconds, nameof(durationSeconds));
            DamagePerSecond = damagePerSecond;
            DurationSeconds = durationSeconds;
        }

        public double DamagePerSecond { get; }
        public double DurationSeconds { get; }

        private static void RequireFinitePositive(double value, string parameterName)
        {
            if (double.IsNaN(value) || double.IsInfinity(value) || value <= 0d)
            {
                throw new ArgumentOutOfRangeException(parameterName);
            }
        }
    }

    public enum WeaponAttackDistanceMode
    {
        Limited = 1,
        Unlimited = 2,
    }

    /// <summary>
    /// Typed maximum attack distance. Unlimited range is never encoded as a magic number.
    /// </summary>
    public sealed class WeaponAttackDistance
    {
        private WeaponAttackDistance(WeaponAttackDistanceMode mode, double distance)
        {
            Mode = mode;
            Distance = distance;
        }

        public WeaponAttackDistanceMode Mode { get; }
        public double Distance { get; }
        public bool IsLimited { get { return Mode == WeaponAttackDistanceMode.Limited; } }

        public static WeaponAttackDistance Limited(double distance)
        {
            if (double.IsNaN(distance)
                || double.IsInfinity(distance)
                || distance <= 0d)
            {
                throw new ArgumentOutOfRangeException(nameof(distance));
            }

            return new WeaponAttackDistance(
                WeaponAttackDistanceMode.Limited,
                distance);
        }

        public static WeaponAttackDistance Unlimited()
        {
            return new WeaponAttackDistance(
                WeaponAttackDistanceMode.Unlimited,
                0d);
        }
    }

    /// <summary>
    /// Fixed-point ricochet budget in tenths. The integer part is guaranteed bounces and the
    /// fractional part is one chance for one final bounce. Runtime consumes the fractional
    /// remainder once through the existing deterministic random authority.
    /// </summary>
    public struct RicochetValue : IEquatable<RicochetValue>
    {
        public RicochetValue(int tenths)
        {
            if (tenths < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(tenths));
            }
            Tenths = tenths;
        }

        public int Tenths { get; }
        public int GuaranteedBounces { get { return Tenths / 10; } }
        public int FractionalTenths { get { return Tenths % 10; } }
        public bool HasFractionalFinalBounce { get { return FractionalTenths != 0; } }
        public double FractionalFinalBounceChance
        {
            get { return FractionalTenths / 10d; }
        }

        public bool Equals(RicochetValue other)
        {
            return Tenths == other.Tenths;
        }

        public override bool Equals(object obj)
        {
            return obj is RicochetValue && Equals((RicochetValue)obj);
        }

        public override int GetHashCode()
        {
            return Tenths;
        }

        public override string ToString()
        {
            return Tenths.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        public static bool operator ==(RicochetValue left, RicochetValue right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(RicochetValue left, RicochetValue right)
        {
            return !left.Equals(right);
        }
    }

    /// <summary>
    /// Universal authored combat values. Delivery-only values such as projectile speed,
    /// projectile radius, explosion radius, and beam width deliberately live elsewhere.
    /// </summary>
    public sealed class WeaponBaseStats
    {
        public WeaponBaseStats(
            double directDamage,
            WeaponDamageCategory damageCategory,
            WeaponDamageOverTimeStats damageOverTime,
            PierceValue pierce,
            RicochetValue ricochet,
            double movementPenaltyPercent,
            WeaponAttackDistance maximumAttackDistance)
            : this(
                directDamage,
                damageCategory,
                damageOverTime,
                pierce,
                ricochet,
                movementPenaltyPercent,
                maximumAttackDistance,
                0d)
        {
        }

        public WeaponBaseStats(
            double directDamage,
            WeaponDamageCategory damageCategory,
            WeaponDamageOverTimeStats damageOverTime,
            PierceValue pierce,
            RicochetValue ricochet,
            double movementPenaltyPercent,
            WeaponAttackDistance maximumAttackDistance,
            double knockback)
        {
            if (!Enum.IsDefined(typeof(WeaponDamageCategory), damageCategory))
            {
                throw new ArgumentOutOfRangeException(nameof(damageCategory));
            }
            if (double.IsNaN(directDamage)
                || double.IsInfinity(directDamage)
                || directDamage < 0d)
            {
                throw new ArgumentOutOfRangeException(nameof(directDamage));
            }
            if (directDamage <= 0d && damageOverTime == null)
            {
                throw new ArgumentException(
                    "A damaging weapon requires positive direct damage or explicit damage-over-time data.",
                    nameof(directDamage));
            }
            if (double.IsNaN(movementPenaltyPercent)
                || double.IsInfinity(movementPenaltyPercent)
                || movementPenaltyPercent < 0d
                || movementPenaltyPercent > 100d)
            {
                throw new ArgumentOutOfRangeException(nameof(movementPenaltyPercent));
            }
            if (double.IsNaN(knockback)
                || double.IsInfinity(knockback)
                || knockback < 0d)
            {
                throw new ArgumentOutOfRangeException(nameof(knockback));
            }

            DirectDamage = directDamage;
            DamageCategory = damageCategory;
            DamageOverTime = damageOverTime;
            Pierce = pierce;
            Ricochet = ricochet;
            MovementPenaltyPercent = movementPenaltyPercent;
            MaximumAttackDistance = maximumAttackDistance
                ?? throw new ArgumentNullException(nameof(maximumAttackDistance));
            Knockback = knockback;
        }

        public double DirectDamage { get; }
        public WeaponDamageCategory DamageCategory { get; }
        public WeaponDamageOverTimeStats DamageOverTime { get; }
        public PierceValue Pierce { get; }
        public RicochetValue Ricochet { get; }
        public double MovementPenaltyPercent { get; }
        public WeaponAttackDistance MaximumAttackDistance { get; }
        public double Knockback { get; }
    }
}
