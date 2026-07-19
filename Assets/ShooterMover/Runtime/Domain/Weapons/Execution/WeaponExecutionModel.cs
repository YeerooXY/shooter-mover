using System;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using ShooterMover.Domain.Common;

namespace ShooterMover.Domain.Weapons.Execution
{
    internal static class WeaponExecutionHash
    {
        private const uint Offset = 2166136261u;
        private const uint Prime = 16777619u;

        public static int Of(string text)
        {
            unchecked
            {
                uint hash = Offset;
                string value = text ?? string.Empty;
                for (int index = 0; index < value.Length; index++)
                {
                    hash ^= value[index];
                    hash *= Prime;
                }

                return (int)hash;
            }
        }
    }

    public static class WeaponExecutionFingerprint
    {
        public const string Prefix = "sha256:";

        public static string Compute(string canonicalText)
        {
            if (canonicalText == null)
            {
                throw new ArgumentNullException(nameof(canonicalText));
            }

            byte[] bytes = Encoding.UTF8.GetBytes(canonicalText);
            byte[] hash;
            using (SHA256 algorithm = SHA256.Create())
            {
                hash = algorithm.ComputeHash(bytes);
            }

            StringBuilder builder = new StringBuilder(Prefix, Prefix.Length + (hash.Length * 2));
            for (int index = 0; index < hash.Length; index++)
            {
                builder.Append(hash[index].ToString("x2", CultureInfo.InvariantCulture));
            }

            return builder.ToString();
        }
    }

    public sealed class WeaponActorInstanceId : IEquatable<WeaponActorInstanceId>
    {
        public WeaponActorInstanceId(StableId value)
        {
            Value = value ?? throw new ArgumentNullException(nameof(value));
        }

        public StableId Value { get; }

        public bool Equals(WeaponActorInstanceId other)
        {
            return !ReferenceEquals(other, null) && Value == other.Value;
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as WeaponActorInstanceId);
        }

        public override int GetHashCode()
        {
            return Value.GetHashCode();
        }

        public override string ToString()
        {
            return Value.ToString();
        }
    }

    public sealed class RunParticipantId : IEquatable<RunParticipantId>
    {
        public RunParticipantId(StableId value)
        {
            Value = value ?? throw new ArgumentNullException(nameof(value));
        }

        public StableId Value { get; }

        public bool Equals(RunParticipantId other)
        {
            return !ReferenceEquals(other, null) && Value == other.Value;
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as RunParticipantId);
        }

        public override int GetHashCode()
        {
            return Value.GetHashCode();
        }

        public override string ToString()
        {
            return Value.ToString();
        }
    }

    public sealed class EquipmentInstanceId : IEquatable<EquipmentInstanceId>
    {
        public EquipmentInstanceId(StableId value)
        {
            Value = value ?? throw new ArgumentNullException(nameof(value));
        }

        public StableId Value { get; }

        public bool Equals(EquipmentInstanceId other)
        {
            return !ReferenceEquals(other, null) && Value == other.Value;
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as EquipmentInstanceId);
        }

        public override int GetHashCode()
        {
            return Value.GetHashCode();
        }

        public override string ToString()
        {
            return Value.ToString();
        }
    }

    public sealed class WeaponDefinitionId : IEquatable<WeaponDefinitionId>
    {
        public WeaponDefinitionId(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("Weapon definition ID is required.", nameof(value));
            }

            Value = value;
        }

        public string Value { get; }

        public bool Equals(WeaponDefinitionId other)
        {
            return !ReferenceEquals(other, null)
                && string.Equals(Value, other.Value, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as WeaponDefinitionId);
        }

        public override int GetHashCode()
        {
            return WeaponExecutionHash.Of(Value);
        }

        public override string ToString()
        {
            return Value;
        }
    }

    public sealed class WeaponBehaviorId : IEquatable<WeaponBehaviorId>
    {
        public WeaponBehaviorId(StableId value)
        {
            Value = value ?? throw new ArgumentNullException(nameof(value));
        }

        public StableId Value { get; }

        public bool Equals(WeaponBehaviorId other)
        {
            return !ReferenceEquals(other, null) && Value == other.Value;
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as WeaponBehaviorId);
        }

        public override int GetHashCode()
        {
            return Value.GetHashCode();
        }

        public override string ToString()
        {
            return Value.ToString();
        }
    }

    public sealed class FireOperationId : IEquatable<FireOperationId>
    {
        public FireOperationId(StableId value)
        {
            Value = value ?? throw new ArgumentNullException(nameof(value));
        }

        public StableId Value { get; }

        public bool Equals(FireOperationId other)
        {
            return !ReferenceEquals(other, null) && Value == other.Value;
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as FireOperationId);
        }

        public override int GetHashCode()
        {
            return Value.GetHashCode();
        }

        public override string ToString()
        {
            return Value.ToString();
        }
    }

    public sealed class LifecycleGeneration : IEquatable<LifecycleGeneration>
    {
        public LifecycleGeneration(long value)
        {
            if (value < 0L)
            {
                throw new ArgumentOutOfRangeException(nameof(value));
            }

            Value = value;
        }

        public long Value { get; }

        public bool Equals(LifecycleGeneration other)
        {
            return !ReferenceEquals(other, null) && Value == other.Value;
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as LifecycleGeneration);
        }

        public override int GetHashCode()
        {
            return Value.GetHashCode();
        }

        public override string ToString()
        {
            return Value.ToString(CultureInfo.InvariantCulture);
        }
    }

    public sealed class ProjectileOrdinal : IEquatable<ProjectileOrdinal>
    {
        public ProjectileOrdinal(int value)
        {
            if (value < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(value));
            }

            Value = value;
        }

        public int Value { get; }

        public bool Equals(ProjectileOrdinal other)
        {
            return !ReferenceEquals(other, null) && Value == other.Value;
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as ProjectileOrdinal);
        }

        public override int GetHashCode()
        {
            return Value;
        }

        public override string ToString()
        {
            return Value.ToString(CultureInfo.InvariantCulture);
        }
    }

    public sealed class WeaponVector2 : IEquatable<WeaponVector2>
    {
        public WeaponVector2(double x, double y)
        {
            X = x;
            Y = y;
        }

        public double X { get; }
        public double Y { get; }

        public bool IsFinite
        {
            get
            {
                return !double.IsNaN(X)
                    && !double.IsInfinity(X)
                    && !double.IsNaN(Y)
                    && !double.IsInfinity(Y);
            }
        }

        public double LengthSquared
        {
            get { return (X * X) + (Y * Y); }
        }

        public WeaponVector2 Normalized
        {
            get
            {
                if (!IsFinite || LengthSquared <= 0d)
                {
                    return new WeaponVector2(0d, 0d);
                }

                double length = Math.Sqrt(LengthSquared);
                return new WeaponVector2(X / length, Y / length);
            }
        }

        public WeaponVector2 RotateDegrees(double degrees)
        {
            double radians = degrees * Math.PI / 180d;
            double cosine = Math.Cos(radians);
            double sine = Math.Sin(radians);
            return new WeaponVector2(
                (X * cosine) - (Y * sine),
                (X * sine) + (Y * cosine));
        }

        public bool Equals(WeaponVector2 other)
        {
            return !ReferenceEquals(other, null)
                && X.Equals(other.X)
                && Y.Equals(other.Y);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as WeaponVector2);
        }

        public override int GetHashCode()
        {
            return WeaponExecutionHash.Of(ToString());
        }

        public override string ToString()
        {
            return X.ToString("R", CultureInfo.InvariantCulture)
                + ","
                + Y.ToString("R", CultureInfo.InvariantCulture);
        }
    }

    public sealed class WeaponRuntimeFiringProfile
    {
        public const int MaximumEffectsPerFire = 64;

        public WeaponRuntimeFiringProfile(
            WeaponDefinitionId definitionId,
            WeaponBehaviorId behaviorId,
            int cooldownTicks,
            int projectileCount,
            double spreadDegrees,
            double projectileSpeed,
            double projectileRange,
            double directDamage,
            int pierce,
            double areaDamage,
            double explosionRadius,
            int chainTargets,
            double chainRange,
            double knockback,
            string damageType)
            : this(
                definitionId,
                behaviorId,
                cooldownTicks,
                projectileCount,
                spreadDegrees,
                projectileSpeed,
                projectileRange,
                directDamage,
                pierce,
                areaDamage,
                explosionRadius,
                0d,
                0d,
                0d,
                0d,
                chainTargets,
                chainRange,
                knockback,
                damageType)
        {
        }

        public WeaponRuntimeFiringProfile(
            WeaponDefinitionId definitionId,
            WeaponBehaviorId behaviorId,
            int cooldownTicks,
            int projectileCount,
            double spreadDegrees,
            double projectileSpeed,
            double projectileRange,
            double directDamage,
            int pierce,
            double areaDamage,
            double explosionRadius,
            double dotDps,
            double dotDuration,
            double poolRadius,
            double poolDuration,
            int chainTargets,
            double chainRange,
            double knockback,
            string damageType)
        {
            DefinitionId = definitionId ?? throw new ArgumentNullException(nameof(definitionId));
            BehaviorId = behaviorId ?? throw new ArgumentNullException(nameof(behaviorId));
            CooldownTicks = cooldownTicks;
            ProjectileCount = projectileCount;
            SpreadDegrees = spreadDegrees;
            ProjectileSpeed = projectileSpeed;
            ProjectileRange = projectileRange;
            DirectDamage = directDamage;
            Pierce = pierce;
            AreaDamage = areaDamage;
            ExplosionRadius = explosionRadius;
            DotDps = dotDps;
            DotDuration = dotDuration;
            PoolRadius = poolRadius;
            PoolDuration = poolDuration;
            ChainTargets = chainTargets;
            ChainRange = chainRange;
            Knockback = knockback;
            DamageType = damageType ?? string.Empty;
        }

        public WeaponDefinitionId DefinitionId { get; }
        public WeaponBehaviorId BehaviorId { get; }
        public int CooldownTicks { get; }
        public int ProjectileCount { get; }
        public double SpreadDegrees { get; }
        public double ProjectileSpeed { get; }
        public double ProjectileRange { get; }
        public double DirectDamage { get; }
        public int Pierce { get; }
        public double AreaDamage { get; }
        public double ExplosionRadius { get; }
        public double DotDps { get; }
        public double DotDuration { get; }
        public double PoolRadius { get; }
        public double PoolDuration { get; }
        public int ChainTargets { get; }
        public double ChainRange { get; }
        public double Knockback { get; }
        public string DamageType { get; }
    }
}
