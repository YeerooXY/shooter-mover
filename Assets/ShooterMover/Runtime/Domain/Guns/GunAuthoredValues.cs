using System;
using ShooterMover.Domain.Guns.Execution;

namespace ShooterMover.Domain.Guns
{
    /// <summary>
    /// Stable, display-facing gun identity. Content names never select runtime behaviour.
    /// </summary>
    public sealed class GunIdentity
    {
        public GunIdentity(
            GunDefinitionId definitionId,
            string displayName,
            string familyId)
        {
            DefinitionId = definitionId
                ?? throw new ArgumentNullException(nameof(definitionId));
            if (string.IsNullOrWhiteSpace(displayName))
            {
                throw new ArgumentException(
                    "A gun display name is required.",
                    nameof(displayName));
            }
            if (string.IsNullOrWhiteSpace(familyId))
            {
                throw new ArgumentException(
                    "A gun family identity is required by the current architecture.",
                    nameof(familyId));
            }

            DisplayName = displayName;
            FamilyId = familyId;
        }

        public GunDefinitionId DefinitionId { get; }
        public string DisplayName { get; }
        public string FamilyId { get; }
    }

    /// <summary>
    /// Explicit optional damage-over-time magnitude and duration. Absence is represented by null.
    /// Tick cadence, stacking, and refresh policy remain in the existing reusable effect contract.
    /// </summary>
    public sealed class GunDamageOverTimeStats
    {
        public GunDamageOverTimeStats(
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

    public enum GunAttackDistanceMode
    {
        Limited = 1,
        Unlimited = 2,
    }

    /// <summary>
    /// Typed maximum attack distance. Unlimited range is never encoded as a magic number.
    /// </summary>
    public sealed class GunAttackDistance
    {
        private GunAttackDistance(GunAttackDistanceMode mode, double distance)
        {
            Mode = mode;
            Distance = distance;
        }

        public GunAttackDistanceMode Mode { get; }
        public double Distance { get; }
        public bool IsLimited { get { return Mode == GunAttackDistanceMode.Limited; } }

        public static GunAttackDistance Limited(double distance)
        {
            if (double.IsNaN(distance)
                || double.IsInfinity(distance)
                || distance <= 0d)
            {
                throw new ArgumentOutOfRangeException(nameof(distance));
            }

            return new GunAttackDistance(
                GunAttackDistanceMode.Limited,
                distance);
        }

        public static GunAttackDistance Unlimited()
        {
            return new GunAttackDistance(
                GunAttackDistanceMode.Unlimited,
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
    public sealed class GunBaseStats
    {
        public GunBaseStats(
            double directDamage,
            GunDamageCategory damageCategory,
            GunDamageOverTimeStats damageOverTime,
            PierceValue pierce,
            RicochetValue ricochet,
            double movementPenaltyPercent,
            GunAttackDistance maximumAttackDistance)
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

        public GunBaseStats(
            double directDamage,
            GunDamageCategory damageCategory,
            GunDamageOverTimeStats damageOverTime,
            PierceValue pierce,
            RicochetValue ricochet,
            double movementPenaltyPercent,
            GunAttackDistance maximumAttackDistance,
            double knockback)
        {
            if (!Enum.IsDefined(typeof(GunDamageCategory), damageCategory))
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
                    "A damaging gun requires positive direct damage or explicit damage-over-time data.",
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
        public GunDamageCategory DamageCategory { get; }
        public GunDamageOverTimeStats DamageOverTime { get; }
        public PierceValue Pierce { get; }
        public RicochetValue Ricochet { get; }
        public double MovementPenaltyPercent { get; }
        public GunAttackDistance MaximumAttackDistance { get; }
        public double Knockback { get; }
    }
}
