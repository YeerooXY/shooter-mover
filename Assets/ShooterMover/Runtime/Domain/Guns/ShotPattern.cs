using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Guns.Execution;

namespace ShooterMover.Domain.Guns
{
    public enum GunDeliveryType
    {
        Normal = 1,
        Orb = 2,
        Rocket = 3,
        Laser = 4,
        Special = 5,
    }

    public sealed class GunNormalDeliverySettings
    {
        public GunNormalDeliverySettings(double projectileSpeed, double projectileRadius)
        {
            RequireFinitePositive(projectileSpeed, nameof(projectileSpeed));
            RequireFinitePositive(projectileRadius, nameof(projectileRadius));
            ProjectileSpeed = projectileSpeed;
            ProjectileRadius = projectileRadius;
        }

        public double ProjectileSpeed { get; }
        public double ProjectileRadius { get; }

        internal static void RequireFinitePositive(double value, string parameterName)
        {
            if (double.IsNaN(value) || double.IsInfinity(value) || value <= 0d)
            {
                throw new ArgumentOutOfRangeException(parameterName);
            }
        }
    }

    public sealed class GunOrbDeliverySettings
    {
        public GunOrbDeliverySettings(double projectileSpeed, double projectileRadius)
        {
            GunNormalDeliverySettings.RequireFinitePositive(
                projectileSpeed,
                nameof(projectileSpeed));
            GunNormalDeliverySettings.RequireFinitePositive(
                projectileRadius,
                nameof(projectileRadius));
            ProjectileSpeed = projectileSpeed;
            ProjectileRadius = projectileRadius;
        }

        public double ProjectileSpeed { get; }
        public double ProjectileRadius { get; }
    }

    public sealed class GunRocketDeliverySettings
    {
        public GunRocketDeliverySettings(double projectileSpeed, double projectileRadius)
        {
            GunNormalDeliverySettings.RequireFinitePositive(
                projectileSpeed,
                nameof(projectileSpeed));
            GunNormalDeliverySettings.RequireFinitePositive(
                projectileRadius,
                nameof(projectileRadius));
            ProjectileSpeed = projectileSpeed;
            ProjectileRadius = projectileRadius;
        }

        public double ProjectileSpeed { get; }
        public double ProjectileRadius { get; }
    }

    public sealed class GunLaserDeliverySettings
    {
        public GunLaserDeliverySettings(double width)
        {
            GunNormalDeliverySettings.RequireFinitePositive(width, nameof(width));
            Width = width;
        }

        public double Width { get; }
    }

    public enum GunSpecialParameterKind
    {
        Number = 1,
        Integer = 2,
        Boolean = 3,
        Identity = 4,
    }

    /// <summary>
    /// One validated typed parameter for an approved special behaviour. Canonical content never
    /// carries executable code, reflection targets, Unity references, or an untyped dictionary.
    /// </summary>
    public sealed class GunSpecialParameter : IComparable<GunSpecialParameter>
    {
        private GunSpecialParameter(
            string name,
            GunSpecialParameterKind kind,
            double numberValue,
            long integerValue,
            bool booleanValue,
            StableId identityValue)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException(
                    "A special parameter name is required.",
                    nameof(name));
            }
            if (!Enum.IsDefined(typeof(GunSpecialParameterKind), kind))
            {
                throw new ArgumentOutOfRangeException(nameof(kind));
            }

            Name = name;
            Kind = kind;
            NumberValue = numberValue;
            IntegerValue = integerValue;
            BooleanValue = booleanValue;
            IdentityValue = identityValue;
        }

        public string Name { get; }
        public GunSpecialParameterKind Kind { get; }
        public double NumberValue { get; }
        public long IntegerValue { get; }
        public bool BooleanValue { get; }
        public StableId IdentityValue { get; }

        public static GunSpecialParameter Number(string name, double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
            {
                throw new ArgumentOutOfRangeException(nameof(value));
            }
            return new GunSpecialParameter(
                name,
                GunSpecialParameterKind.Number,
                value,
                0L,
                false,
                null);
        }

        public static GunSpecialParameter Integer(string name, long value)
        {
            return new GunSpecialParameter(
                name,
                GunSpecialParameterKind.Integer,
                0d,
                value,
                false,
                null);
        }

        public static GunSpecialParameter Boolean(string name, bool value)
        {
            return new GunSpecialParameter(
                name,
                GunSpecialParameterKind.Boolean,
                0d,
                0L,
                value,
                null);
        }

        public static GunSpecialParameter Identity(string name, StableId value)
        {
            return new GunSpecialParameter(
                name,
                GunSpecialParameterKind.Identity,
                0d,
                0L,
                false,
                value ?? throw new ArgumentNullException(nameof(value)));
        }

        public int CompareTo(GunSpecialParameter other)
        {
            return ReferenceEquals(other, null)
                ? 1
                : string.CompareOrdinal(Name, other.Name);
        }
    }

    public sealed class GunSpecialParameterSet
    {
        private readonly ReadOnlyCollection<GunSpecialParameter> parameters;

        public GunSpecialParameterSet(IEnumerable<GunSpecialParameter> values)
        {
            var copy = new List<GunSpecialParameter>(
                values ?? Array.Empty<GunSpecialParameter>());
            copy.Sort();
            for (int index = 0; index < copy.Count; index++)
            {
                if (copy[index] == null)
                {
                    throw new ArgumentException(
                        "Special parameter sets cannot contain null values.",
                        nameof(values));
                }
                if (index > 0
                    && string.Equals(
                        copy[index - 1].Name,
                        copy[index].Name,
                        StringComparison.Ordinal))
                {
                    throw new ArgumentException(
                        "Special parameter names must be unique: " + copy[index].Name,
                        nameof(values));
                }
            }
            parameters = new ReadOnlyCollection<GunSpecialParameter>(copy);
        }

        public IReadOnlyList<GunSpecialParameter> Parameters
        {
            get { return parameters; }
        }

        public static GunSpecialParameterSet Empty()
        {
            return new GunSpecialParameterSet(
                Array.Empty<GunSpecialParameter>());
        }
    }

    public sealed class GunSpecialDeliverySettings
    {
        public GunSpecialDeliverySettings(
            GunBehaviorId behaviorId,
            GunSpecialParameterSet parameters)
            : this(behaviorId, parameters, false, false)
        {
        }

        /// <summary>
        /// Approved special schemas opt in explicitly when their adapter consumes the canonical
        /// maximum-distance or ordered-target Pierce values. These flags do not implement a Unity
        /// delivery route and cannot create projectile-speed compatibility.
        /// </summary>
        public GunSpecialDeliverySettings(
            GunBehaviorId behaviorId,
            GunSpecialParameterSet parameters,
            bool usesCanonicalRange,
            bool usesCanonicalPierce)
        {
            BehaviorId = behaviorId
                ?? throw new ArgumentNullException(nameof(behaviorId));
            Parameters = parameters
                ?? throw new ArgumentNullException(nameof(parameters));
            UsesCanonicalRange = usesCanonicalRange;
            UsesCanonicalPierce = usesCanonicalPierce;
        }

        public GunBehaviorId BehaviorId { get; }
        public GunSpecialParameterSet Parameters { get; }
        public bool UsesCanonicalRange { get; }
        public bool UsesCanonicalPierce { get; }
    }

    /// <summary>
    /// Canonical discriminated delivery contract. Exactly one typed settings group is present.
    /// Guidance, impact, and effects reuse the existing generic authorities.
    /// </summary>
    public sealed class ShotPattern
    {
        private ShotPattern(
            GunDeliveryType type,
            GunNormalDeliverySettings normal,
            GunOrbDeliverySettings orb,
            GunRocketDeliverySettings rocket,
            GunLaserDeliverySettings laser,
            GunSpecialDeliverySettings special,
            GunGuidanceSpec guidance,
            GunImpactSpec impact,
            GunEffects effects)
        {
            Type = type;
            Normal = normal;
            Orb = orb;
            Rocket = rocket;
            Laser = laser;
            Special = special;
            Guidance = guidance;
            Impact = impact;
            Effects = effects;
        }

        public GunDeliveryType Type { get; }
        public GunNormalDeliverySettings Normal { get; }
        public GunOrbDeliverySettings Orb { get; }
        public GunRocketDeliverySettings Rocket { get; }
        public GunLaserDeliverySettings Laser { get; }
        public GunSpecialDeliverySettings Special { get; }
        public GunGuidanceSpec Guidance { get; }
        public GunImpactSpec Impact { get; }
        public GunEffects Effects { get; }

        public bool IsTravelling
        {
            get
            {
                return Type == GunDeliveryType.Normal
                    || Type == GunDeliveryType.Orb
                    || Type == GunDeliveryType.Rocket;
            }
        }

        public bool SupportsProjectileSpeedModifiers
        {
            get { return IsTravelling; }
        }

        public bool SupportsCanonicalRangeModifiers
        {
            get
            {
                return Type != GunDeliveryType.Special
                    || (Special != null && Special.UsesCanonicalRange);
            }
        }

        public bool SupportsCanonicalPierceModifiers
        {
            get
            {
                return Type != GunDeliveryType.Special
                    || (Special != null && Special.UsesCanonicalPierce);
            }
        }

        public static ShotPattern Create(
            GunDeliveryType type,
            GunNormalDeliverySettings normal,
            GunOrbDeliverySettings orb,
            GunRocketDeliverySettings rocket,
            GunLaserDeliverySettings laser,
            GunSpecialDeliverySettings special,
            GunGuidanceSpec guidance,
            GunImpactSpec impact,
            GunEffects effects)
        {
            if (!Enum.IsDefined(typeof(GunDeliveryType), type))
            {
                throw new ArgumentOutOfRangeException(nameof(type));
            }
            guidance = guidance
                ?? throw new ArgumentNullException(nameof(guidance));
            impact = impact
                ?? throw new ArgumentNullException(nameof(impact));
            effects = effects
                ?? throw new ArgumentNullException(nameof(effects));

            int populated = Count(normal, orb, rocket, laser, special);
            if (populated != 1)
            {
                throw new ArgumentException(
                    "Exactly one delivery-specific settings group is required.");
            }

            switch (type)
            {
                case GunDeliveryType.Normal:
                    RequireSelected(normal, orb, rocket, laser, special);
                    break;
                case GunDeliveryType.Orb:
                    RequireSelected(orb, normal, rocket, laser, special);
                    if (impact.ExplosionTrigger != null
                        && impact.ExplosionTrigger.OnWallImpact)
                    {
                        throw new ArgumentException(
                            "Orb delivery cannot inherit rocket-style wall-contact detonation.",
                            nameof(impact));
                    }
                    break;
                case GunDeliveryType.Rocket:
                    RequireSelected(rocket, normal, orb, laser, special);
                    ValidateRocket(impact, effects);
                    break;
                case GunDeliveryType.Laser:
                    RequireSelected(laser, normal, orb, rocket, special);
                    RequireUnguided(guidance, "Laser delivery cannot carry projectile guidance data.");
                    break;
                case GunDeliveryType.Special:
                    RequireSelected(special, normal, orb, rocket, laser);
                    RequireUnguided(
                        guidance,
                        "Special delivery must express approved exceptional targeting through its validated behaviour schema.");
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(type));
            }

            return new ShotPattern(
                type,
                normal,
                orb,
                rocket,
                laser,
                special,
                guidance,
                impact,
                effects);
        }

        public ProjectileSettings CreateTravellingProjectileSpec(
            GunBaseStats baseStats)
        {
            if (!IsTravelling)
            {
                return null;
            }
            if (baseStats == null)
            {
                throw new ArgumentNullException(nameof(baseStats));
            }
            if (!baseStats.MaximumAttackDistance.IsLimited)
            {
                throw new InvalidOperationException(
                    "The current travelling-projectile runtime requires a finite range. Unlimited authored delivery remains behind the migration boundary.");
            }

            GunProjectileKind kind;
            double speed;
            GunProjectileTerminationBehavior termination;
            switch (Type)
            {
                case GunDeliveryType.Normal:
                    kind = GunProjectileKind.RegularProjectile;
                    speed = Normal.ProjectileSpeed;
                    termination = GunProjectileTerminationBehavior.StopWhenPierceIsSpent;
                    break;
                case GunDeliveryType.Orb:
                    kind = GunProjectileKind.Orb;
                    speed = Orb.ProjectileSpeed;
                    termination = GunProjectileTerminationBehavior.StopWhenPierceIsSpent;
                    break;
                case GunDeliveryType.Rocket:
                    kind = GunProjectileKind.Rocket;
                    speed = Rocket.ProjectileSpeed;
                    termination = GunProjectileTerminationBehavior.StopOnFirstBlockingImpact;
                    break;
                default:
                    throw new InvalidOperationException(
                        "Only travelling deliveries project into ProjectileSettings.");
            }

            return ProjectileSettings.Create(
                kind,
                speed,
                baseStats.MaximumAttackDistance.Distance,
                baseStats.Pierce,
                termination);
        }

        private static void ValidateRocket(
            GunImpactSpec impact,
            GunEffects effects)
        {
            if (!impact.HandlesEnemyImpact
                || !impact.HandlesWallImpact
                || impact.ExplosionTrigger == null
                || !impact.ExplosionTrigger.OnEnemyImpact
                || !impact.ExplosionTrigger.OnWallImpact)
            {
                throw new ArgumentException(
                    "Rocket delivery requires immediate enemy- and wall-contact explosion behaviour.",
                    nameof(impact));
            }
            if (effects.Explosion == null)
            {
                throw new ArgumentException(
                    "Rocket delivery requires a valid explosion effect.",
                    nameof(effects));
            }
        }

        private static void RequireUnguided(
            GunGuidanceSpec guidance,
            string detail)
        {
            if (guidance.Mode != GunGuidanceMode.Unguided)
            {
                throw new ArgumentException(detail, nameof(guidance));
            }
        }

        private static int Count(params object[] values)
        {
            int count = 0;
            for (int index = 0; index < values.Length; index++)
            {
                if (values[index] != null)
                {
                    count++;
                }
            }
            return count;
        }

        private static void RequireSelected(object selected, params object[] others)
        {
            if (selected == null)
            {
                throw new ArgumentException(
                    "The selected delivery type is missing its required settings group.");
            }
            for (int index = 0; index < others.Length; index++)
            {
                if (others[index] != null)
                {
                    throw new ArgumentException(
                        "The selected delivery type carries incompatible delivery data.");
                }
            }
        }
    }
}
