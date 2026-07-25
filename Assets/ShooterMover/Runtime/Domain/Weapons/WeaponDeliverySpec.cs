using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Weapons.Execution;

namespace ShooterMover.Domain.Weapons
{
    public enum WeaponDeliveryType
    {
        Normal = 1,
        Orb = 2,
        Rocket = 3,
        Laser = 4,
        Special = 5,
    }

    public sealed class WeaponNormalDeliverySettings
    {
        public WeaponNormalDeliverySettings(double projectileSpeed, double projectileRadius)
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

    public sealed class WeaponOrbDeliverySettings
    {
        public WeaponOrbDeliverySettings(double projectileSpeed, double projectileRadius)
        {
            WeaponNormalDeliverySettings.RequireFinitePositive(
                projectileSpeed,
                nameof(projectileSpeed));
            WeaponNormalDeliverySettings.RequireFinitePositive(
                projectileRadius,
                nameof(projectileRadius));
            ProjectileSpeed = projectileSpeed;
            ProjectileRadius = projectileRadius;
        }

        public double ProjectileSpeed { get; }
        public double ProjectileRadius { get; }
    }

    public sealed class WeaponRocketDeliverySettings
    {
        public WeaponRocketDeliverySettings(double projectileSpeed, double projectileRadius)
        {
            WeaponNormalDeliverySettings.RequireFinitePositive(
                projectileSpeed,
                nameof(projectileSpeed));
            WeaponNormalDeliverySettings.RequireFinitePositive(
                projectileRadius,
                nameof(projectileRadius));
            ProjectileSpeed = projectileSpeed;
            ProjectileRadius = projectileRadius;
        }

        public double ProjectileSpeed { get; }
        public double ProjectileRadius { get; }
    }

    public sealed class WeaponLaserDeliverySettings
    {
        public WeaponLaserDeliverySettings(double width)
        {
            WeaponNormalDeliverySettings.RequireFinitePositive(width, nameof(width));
            Width = width;
        }

        public double Width { get; }
    }

    public enum WeaponSpecialParameterKind
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
    public sealed class WeaponSpecialParameter : IComparable<WeaponSpecialParameter>
    {
        private WeaponSpecialParameter(
            string name,
            WeaponSpecialParameterKind kind,
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
            if (!Enum.IsDefined(typeof(WeaponSpecialParameterKind), kind))
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
        public WeaponSpecialParameterKind Kind { get; }
        public double NumberValue { get; }
        public long IntegerValue { get; }
        public bool BooleanValue { get; }
        public StableId IdentityValue { get; }

        public static WeaponSpecialParameter Number(string name, double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
            {
                throw new ArgumentOutOfRangeException(nameof(value));
            }
            return new WeaponSpecialParameter(
                name,
                WeaponSpecialParameterKind.Number,
                value,
                0L,
                false,
                null);
        }

        public static WeaponSpecialParameter Integer(string name, long value)
        {
            return new WeaponSpecialParameter(
                name,
                WeaponSpecialParameterKind.Integer,
                0d,
                value,
                false,
                null);
        }

        public static WeaponSpecialParameter Boolean(string name, bool value)
        {
            return new WeaponSpecialParameter(
                name,
                WeaponSpecialParameterKind.Boolean,
                0d,
                0L,
                value,
                null);
        }

        public static WeaponSpecialParameter Identity(string name, StableId value)
        {
            return new WeaponSpecialParameter(
                name,
                WeaponSpecialParameterKind.Identity,
                0d,
                0L,
                false,
                value ?? throw new ArgumentNullException(nameof(value)));
        }

        public int CompareTo(WeaponSpecialParameter other)
        {
            return ReferenceEquals(other, null)
                ? 1
                : string.CompareOrdinal(Name, other.Name);
        }
    }

    public sealed class WeaponSpecialParameterSet
    {
        private readonly ReadOnlyCollection<WeaponSpecialParameter> parameters;

        public WeaponSpecialParameterSet(IEnumerable<WeaponSpecialParameter> values)
        {
            var copy = new List<WeaponSpecialParameter>(
                values ?? Array.Empty<WeaponSpecialParameter>());
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
            parameters = new ReadOnlyCollection<WeaponSpecialParameter>(copy);
        }

        public IReadOnlyList<WeaponSpecialParameter> Parameters
        {
            get { return parameters; }
        }

        public static WeaponSpecialParameterSet Empty()
        {
            return new WeaponSpecialParameterSet(
                Array.Empty<WeaponSpecialParameter>());
        }
    }

    public sealed class WeaponSpecialDeliverySettings
    {
        public WeaponSpecialDeliverySettings(
            WeaponBehaviorId behaviorId,
            WeaponSpecialParameterSet parameters)
            : this(behaviorId, parameters, false, false)
        {
        }

        /// <summary>
        /// Approved special schemas opt in explicitly when their adapter consumes the canonical
        /// maximum-distance or ordered-target Pierce values. These flags do not implement a Unity
        /// delivery route and cannot create projectile-speed compatibility.
        /// </summary>
        public WeaponSpecialDeliverySettings(
            WeaponBehaviorId behaviorId,
            WeaponSpecialParameterSet parameters,
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

        public WeaponBehaviorId BehaviorId { get; }
        public WeaponSpecialParameterSet Parameters { get; }
        public bool UsesCanonicalRange { get; }
        public bool UsesCanonicalPierce { get; }
    }

    /// <summary>
    /// Canonical discriminated delivery contract. Exactly one typed settings group is present.
    /// Guidance, impact, and effects reuse the existing generic authorities.
    /// </summary>
    public sealed class WeaponDeliverySpec
    {
        private WeaponDeliverySpec(
            WeaponDeliveryType type,
            WeaponNormalDeliverySettings normal,
            WeaponOrbDeliverySettings orb,
            WeaponRocketDeliverySettings rocket,
            WeaponLaserDeliverySettings laser,
            WeaponSpecialDeliverySettings special,
            WeaponGuidanceSpec guidance,
            WeaponImpactSpec impact,
            WeaponEffects effects)
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

        public WeaponDeliveryType Type { get; }
        public WeaponNormalDeliverySettings Normal { get; }
        public WeaponOrbDeliverySettings Orb { get; }
        public WeaponRocketDeliverySettings Rocket { get; }
        public WeaponLaserDeliverySettings Laser { get; }
        public WeaponSpecialDeliverySettings Special { get; }
        public WeaponGuidanceSpec Guidance { get; }
        public WeaponImpactSpec Impact { get; }
        public WeaponEffects Effects { get; }

        public bool IsTravelling
        {
            get
            {
                return Type == WeaponDeliveryType.Normal
                    || Type == WeaponDeliveryType.Orb
                    || Type == WeaponDeliveryType.Rocket;
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
                return Type != WeaponDeliveryType.Special
                    || (Special != null && Special.UsesCanonicalRange);
            }
        }

        public bool SupportsCanonicalPierceModifiers
        {
            get
            {
                return Type != WeaponDeliveryType.Special
                    || (Special != null && Special.UsesCanonicalPierce);
            }
        }

        public static WeaponDeliverySpec Create(
            WeaponDeliveryType type,
            WeaponNormalDeliverySettings normal,
            WeaponOrbDeliverySettings orb,
            WeaponRocketDeliverySettings rocket,
            WeaponLaserDeliverySettings laser,
            WeaponSpecialDeliverySettings special,
            WeaponGuidanceSpec guidance,
            WeaponImpactSpec impact,
            WeaponEffects effects)
        {
            if (!Enum.IsDefined(typeof(WeaponDeliveryType), type))
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
                case WeaponDeliveryType.Normal:
                    RequireSelected(normal, orb, rocket, laser, special);
                    break;
                case WeaponDeliveryType.Orb:
                    RequireSelected(orb, normal, rocket, laser, special);
                    if (impact.ExplosionTrigger != null
                        && impact.ExplosionTrigger.OnWallImpact)
                    {
                        throw new ArgumentException(
                            "Orb delivery cannot inherit rocket-style wall-contact detonation.",
                            nameof(impact));
                    }
                    break;
                case WeaponDeliveryType.Rocket:
                    RequireSelected(rocket, normal, orb, laser, special);
                    ValidateRocket(impact, effects);
                    break;
                case WeaponDeliveryType.Laser:
                    RequireSelected(laser, normal, orb, rocket, special);
                    RequireUnguided(guidance, "Laser delivery cannot carry projectile guidance data.");
                    break;
                case WeaponDeliveryType.Special:
                    RequireSelected(special, normal, orb, rocket, laser);
                    RequireUnguided(
                        guidance,
                        "Special delivery must express approved exceptional targeting through its validated behaviour schema.");
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(type));
            }

            return new WeaponDeliverySpec(
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

        public WeaponProjectileSpec CreateTravellingProjectileSpec(
            WeaponBaseStats baseStats)
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

            WeaponProjectileKind kind;
            double speed;
            WeaponProjectileTerminationBehavior termination;
            switch (Type)
            {
                case WeaponDeliveryType.Normal:
                    kind = WeaponProjectileKind.RegularProjectile;
                    speed = Normal.ProjectileSpeed;
                    termination = WeaponProjectileTerminationBehavior.StopWhenPierceIsSpent;
                    break;
                case WeaponDeliveryType.Orb:
                    kind = WeaponProjectileKind.Orb;
                    speed = Orb.ProjectileSpeed;
                    termination = WeaponProjectileTerminationBehavior.StopWhenPierceIsSpent;
                    break;
                case WeaponDeliveryType.Rocket:
                    kind = WeaponProjectileKind.Rocket;
                    speed = Rocket.ProjectileSpeed;
                    termination = WeaponProjectileTerminationBehavior.StopOnFirstBlockingImpact;
                    break;
                default:
                    throw new InvalidOperationException(
                        "Only travelling deliveries project into WeaponProjectileSpec.");
            }

            return WeaponProjectileSpec.Create(
                kind,
                speed,
                baseStats.MaximumAttackDistance.Distance,
                baseStats.Pierce,
                termination);
        }

        private static void ValidateRocket(
            WeaponImpactSpec impact,
            WeaponEffects effects)
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
            WeaponGuidanceSpec guidance,
            string detail)
        {
            if (guidance.Mode != WeaponGuidanceMode.Unguided)
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
