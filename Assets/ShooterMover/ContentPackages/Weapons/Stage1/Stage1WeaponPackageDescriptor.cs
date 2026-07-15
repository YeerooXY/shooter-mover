using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using ShooterMover.Contracts.Content;
using ShooterMover.Domain.Combat;
using ShooterMover.Domain.Common;

namespace ShooterMover.ContentPackages.Weapons.Stage1
{
    /// <summary>
    /// Closed Stage 1 behavior families. These values describe package authoring
    /// topology only; runtime execution remains owned by explicitly registered
    /// CB-004 behavior modules.
    /// </summary>
    public enum Stage1WeaponBehaviorKind
    {
        AutomaticProjectile = 1,
        SpreadProjectile = 2,
        RocketAreaDetonation = 3,
        ArcChain = 4,
        RicochetProjectile = 5,
    }

    /// <summary>
    /// Closed numeric vocabulary that an empowered profile may tune without adding
    /// a new behavior, target-selection rule, or shared subsystem.
    /// </summary>
    public enum Stage1WeaponNumericCoefficientKind
    {
        Damage = 1,
        ProjectileSpeed = 2,
        ProjectileLifetimeSeconds = 3,
        SpreadDegrees = 4,
        AreaRadius = 5,
        EffectRange = 6,
        DamageMultiplier = 7,
        ProjectileRadius = 8,
    }

    /// <summary>
    /// Immutable explicit topology for one Stage 1 fire profile. Invalid values are
    /// retained so the separate validator can report deterministic structured errors.
    /// </summary>
    public sealed class Stage1WeaponBehaviorTopology :
        IEquatable<Stage1WeaponBehaviorTopology>
    {
        private readonly string canonicalText;

        private Stage1WeaponBehaviorTopology(
            Stage1WeaponBehaviorKind kind,
            int additionalTargetCount,
            int wallBounceCount,
            int detonationCount,
            bool hasFragmentation)
        {
            Kind = kind;
            AdditionalTargetCount = additionalTargetCount;
            WallBounceCount = wallBounceCount;
            DetonationCount = detonationCount;
            HasFragmentation = hasFragmentation;
            canonicalText = BuildCanonicalText();
        }

        public Stage1WeaponBehaviorKind Kind { get; }

        /// <summary>
        /// Targets after the primary target. Only Arc Chain may use a non-zero value.
        /// </summary>
        public int AdditionalTargetCount { get; }

        /// <summary>
        /// Reflections from valid 2D walls. Only Ricochet Projectile may use a non-zero value.
        /// </summary>
        public int WallBounceCount { get; }

        /// <summary>
        /// Authored area detonations. Rocket Area Detonation must use exactly one.
        /// </summary>
        public int DetonationCount { get; }

        public bool HasFragmentation { get; }

        public static Stage1WeaponBehaviorTopology Create(
            Stage1WeaponBehaviorKind kind,
            int additionalTargetCount,
            int wallBounceCount,
            int detonationCount,
            bool hasFragmentation)
        {
            return new Stage1WeaponBehaviorTopology(
                kind,
                additionalTargetCount,
                wallBounceCount,
                detonationCount,
                hasFragmentation);
        }

        public string ToCanonicalString()
        {
            return canonicalText;
        }

        public bool Equals(Stage1WeaponBehaviorTopology other)
        {
            return !ReferenceEquals(other, null)
                && string.Equals(canonicalText, other.canonicalText, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as Stage1WeaponBehaviorTopology);
        }

        public override int GetHashCode()
        {
            return Stage1WeaponPackageDescriptor.OrdinalHash(canonicalText);
        }

        public override string ToString()
        {
            return canonicalText;
        }

        private string BuildCanonicalText()
        {
            return "behavior_kind="
                + ((int)Kind).ToString(CultureInfo.InvariantCulture)
                + "\nadditional_target_count="
                + AdditionalTargetCount.ToString(CultureInfo.InvariantCulture)
                + "\nwall_bounce_count="
                + WallBounceCount.ToString(CultureInfo.InvariantCulture)
                + "\ndetonation_count="
                + DetonationCount.ToString(CultureInfo.InvariantCulture)
                + "\nhas_fragmentation="
                + (HasFragmentation ? "true" : "false");
        }
    }

    /// <summary>
    /// One immutable package-owned behavior coefficient. The kind is closed so an
    /// empowered profile cannot smuggle a behavior flag through an arbitrary number.
    /// </summary>
    public sealed class Stage1WeaponNumericCoefficient :
        IEquatable<Stage1WeaponNumericCoefficient>,
        IComparable<Stage1WeaponNumericCoefficient>,
        IComparable
    {
        private readonly string canonicalText;

        private Stage1WeaponNumericCoefficient(
            Stage1WeaponNumericCoefficientKind kind,
            double value)
        {
            Kind = kind;
            Value = value;
            canonicalText = "kind="
                + ((int)Kind).ToString(CultureInfo.InvariantCulture)
                + "\nvalue="
                + Value.ToString("R", CultureInfo.InvariantCulture);
        }

        public Stage1WeaponNumericCoefficientKind Kind { get; }

        public double Value { get; }

        public static Stage1WeaponNumericCoefficient Create(
            Stage1WeaponNumericCoefficientKind kind,
            double value)
        {
            return new Stage1WeaponNumericCoefficient(kind, value);
        }

        public string ToCanonicalString()
        {
            return canonicalText;
        }

        public bool Equals(Stage1WeaponNumericCoefficient other)
        {
            return !ReferenceEquals(other, null)
                && string.Equals(canonicalText, other.canonicalText, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as Stage1WeaponNumericCoefficient);
        }

        public override int GetHashCode()
        {
            return Stage1WeaponPackageDescriptor.OrdinalHash(canonicalText);
        }

        public int CompareTo(Stage1WeaponNumericCoefficient other)
        {
            if (ReferenceEquals(other, null))
            {
                return 1;
            }

            int kindComparison = Kind.CompareTo(other.Kind);
            if (kindComparison != 0)
            {
                return kindComparison;
            }

            return string.CompareOrdinal(canonicalText, other.canonicalText);
        }

        int IComparable.CompareTo(object obj)
        {
            if (obj == null)
            {
                return 1;
            }

            Stage1WeaponNumericCoefficient other =
                obj as Stage1WeaponNumericCoefficient;
            if (other == null)
            {
                throw new ArgumentException(
                    "Object must be a Stage1WeaponNumericCoefficient.",
                    nameof(obj));
            }

            return CompareTo(other);
        }

        public override string ToString()
        {
            return canonicalText;
        }
    }

    /// <summary>
    /// Immutable normal or empowered authoring profile. The accepted CB-001 runtime
    /// profile supplies generic mount tuning and ordered CB-004 module IDs; the
    /// package topology and closed coefficient set supply Stage 1 content boundaries.
    /// </summary>
    public sealed class Stage1WeaponFireProfile :
        IEquatable<Stage1WeaponFireProfile>
    {
        private readonly ReadOnlyCollection<Stage1WeaponNumericCoefficient> coefficients;
        private readonly string canonicalText;

        private Stage1WeaponFireProfile(
            WeaponRuntimeProfile runtimeProfile,
            Stage1WeaponBehaviorTopology topology,
            bool consumesConsumableAmmunition,
            IEnumerable<Stage1WeaponNumericCoefficient> numericCoefficients)
        {
            RuntimeProfile = runtimeProfile;
            Topology = topology;
            ConsumesConsumableAmmunition = consumesConsumableAmmunition;
            coefficients = CopyAndOrderCoefficients(numericCoefficients);
            canonicalText = BuildCanonicalText();
        }

        public WeaponRuntimeProfile RuntimeProfile { get; }

        public Stage1WeaponBehaviorTopology Topology { get; }

        public bool ConsumesConsumableAmmunition { get; }

        /// <summary>
        /// Null is retained only for intentionally malformed validation fixtures.
        /// </summary>
        public IReadOnlyList<Stage1WeaponNumericCoefficient> NumericCoefficients
        {
            get { return coefficients; }
        }

        public static Stage1WeaponFireProfile Create(
            WeaponRuntimeProfile runtimeProfile,
            Stage1WeaponBehaviorTopology topology,
            bool consumesConsumableAmmunition,
            IEnumerable<Stage1WeaponNumericCoefficient> numericCoefficients)
        {
            return new Stage1WeaponFireProfile(
                runtimeProfile,
                topology,
                consumesConsumableAmmunition,
                numericCoefficients);
        }

        public string ToCanonicalString()
        {
            return canonicalText;
        }

        public bool Equals(Stage1WeaponFireProfile other)
        {
            return !ReferenceEquals(other, null)
                && string.Equals(canonicalText, other.canonicalText, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as Stage1WeaponFireProfile);
        }

        public override int GetHashCode()
        {
            return Stage1WeaponPackageDescriptor.OrdinalHash(canonicalText);
        }

        public override string ToString()
        {
            return canonicalText;
        }

        private string BuildCanonicalText()
        {
            StringBuilder builder = new StringBuilder();
            builder.Append("consumes_consumable_ammunition=")
                .Append(ConsumesConsumableAmmunition ? "true" : "false")
                .Append("\ntopology:\n")
                .Append(Topology == null ? "null" : Topology.ToCanonicalString())
                .Append("\nruntime_profile:\n")
                .Append(RuntimeProfile == null ? "null" : RuntimeProfile.ToCanonicalString())
                .Append("\ncoefficient_count=");

            if (coefficients == null)
            {
                builder.Append("null");
                return builder.ToString();
            }

            builder.Append(coefficients.Count.ToString(CultureInfo.InvariantCulture));
            for (int index = 0; index < coefficients.Count; index++)
            {
                builder.Append("\ncoefficient_")
                    .Append(index.ToString("D4", CultureInfo.InvariantCulture))
                    .Append(":\n")
                    .Append(coefficients[index] == null
                        ? "null"
                        : coefficients[index].ToCanonicalString());
            }

            return builder.ToString();
        }

        private static ReadOnlyCollection<Stage1WeaponNumericCoefficient>
            CopyAndOrderCoefficients(
                IEnumerable<Stage1WeaponNumericCoefficient> numericCoefficients)
        {
            if (numericCoefficients == null)
            {
                return null;
            }

            List<Stage1WeaponNumericCoefficient> copy =
                new List<Stage1WeaponNumericCoefficient>();
            foreach (Stage1WeaponNumericCoefficient coefficient in numericCoefficients)
            {
                copy.Add(coefficient);
            }

            copy.Sort(CompareCoefficients);
            return new ReadOnlyCollection<Stage1WeaponNumericCoefficient>(copy);
        }

        private static int CompareCoefficients(
            Stage1WeaponNumericCoefficient left,
            Stage1WeaponNumericCoefficient right)
        {
            if (ReferenceEquals(left, right))
            {
                return 0;
            }

            if (ReferenceEquals(left, null))
            {
                return -1;
            }

            if (ReferenceEquals(right, null))
            {
                return 1;
            }

            return left.CompareTo(right);
        }
    }

    /// <summary>
    /// Immutable, engine-independent authoring boundary for one amended Stage 1
    /// weapon package. It contributes validated registry inputs but never writes
    /// CS-011 generated outputs or dispatches runtime behavior by weapon ID.
    /// </summary>
    public sealed class Stage1WeaponPackageDescriptor :
        IEquatable<Stage1WeaponPackageDescriptor>,
        IComparable<Stage1WeaponPackageDescriptor>,
        IComparable
    {
        public const int CurrentDescriptorVersion = 1;

        private static readonly StableId BlasterMachineGunIdValue =
            StableId.Parse("weapon.blaster-machine-gun");
        private static readonly StableId ShotgunIdValue =
            StableId.Parse("weapon.shotgun");
        private static readonly StableId RocketLauncherIdValue =
            StableId.Parse("weapon.rocket-launcher");
        private static readonly StableId ArcGunIdValue =
            StableId.Parse("weapon.arc-gun");
        private static readonly StableId RicochetGunIdValue =
            StableId.Parse("weapon.ricochet-gun");

        private static readonly ReadOnlyCollection<StableId> AcceptedWeaponIdsValue =
            Array.AsReadOnly(
                new[]
                {
                    BlasterMachineGunIdValue,
                    ShotgunIdValue,
                    RocketLauncherIdValue,
                    ArcGunIdValue,
                    RicochetGunIdValue,
                });

        private readonly string canonicalText;

        private Stage1WeaponPackageDescriptor(
            int descriptorVersion,
            ContentDefinitionDescriptor contentDefinition,
            bool isDefaultStartingWeapon,
            Stage1WeaponFireProfile normalFire,
            Stage1WeaponFireProfile empoweredFire)
        {
            DescriptorVersion = descriptorVersion;
            ContentDefinition = contentDefinition;
            IsDefaultStartingWeapon = isDefaultStartingWeapon;
            NormalFire = normalFire;
            EmpoweredFire = empoweredFire;
            canonicalText = BuildCanonicalText();
        }

        public int DescriptorVersion { get; }

        public ContentDefinitionDescriptor ContentDefinition { get; }

        public StableId DefinitionId
        {
            get { return ContentDefinition == null ? null : ContentDefinition.DefinitionId; }
        }

        public bool IsDefaultStartingWeapon { get; }

        public Stage1WeaponFireProfile NormalFire { get; }

        public Stage1WeaponFireProfile EmpoweredFire { get; }

        public static StableId BlasterMachineGunId
        {
            get { return BlasterMachineGunIdValue; }
        }

        public static StableId ShotgunId
        {
            get { return ShotgunIdValue; }
        }

        public static StableId RocketLauncherId
        {
            get { return RocketLauncherIdValue; }
        }

        public static StableId ArcGunId
        {
            get { return ArcGunIdValue; }
        }

        public static StableId RicochetGunId
        {
            get { return RicochetGunIdValue; }
        }

        public static IReadOnlyList<StableId> AcceptedWeaponIds
        {
            get { return AcceptedWeaponIdsValue; }
        }

        public static Stage1WeaponPackageDescriptor Create(
            int descriptorVersion,
            ContentDefinitionDescriptor contentDefinition,
            bool isDefaultStartingWeapon,
            Stage1WeaponFireProfile normalFire,
            Stage1WeaponFireProfile empoweredFire)
        {
            return new Stage1WeaponPackageDescriptor(
                descriptorVersion,
                contentDefinition,
                isDefaultStartingWeapon,
                normalFire,
                empoweredFire);
        }

        public ContentReference CreateWeaponReference()
        {
            if (ContentDefinition == null)
            {
                throw new InvalidOperationException(
                    "A package without a content definition cannot create a weapon reference.");
            }

            return ContentReference.Create(
                ContentDefinition.DefinitionId,
                ContentDefinitionKind.Weapon,
                ContentDefinition.DefinitionVersion);
        }

        public string ToCanonicalString()
        {
            return canonicalText;
        }

        public bool Equals(Stage1WeaponPackageDescriptor other)
        {
            return !ReferenceEquals(other, null)
                && string.Equals(canonicalText, other.canonicalText, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as Stage1WeaponPackageDescriptor);
        }

        public override int GetHashCode()
        {
            return OrdinalHash(canonicalText);
        }

        public int CompareTo(Stage1WeaponPackageDescriptor other)
        {
            if (ReferenceEquals(other, null))
            {
                return 1;
            }

            if (DefinitionId == null)
            {
                if (other.DefinitionId != null)
                {
                    return 1;
                }
            }
            else
            {
                if (other.DefinitionId == null)
                {
                    return -1;
                }

                int idComparison = DefinitionId.CompareTo(other.DefinitionId);
                if (idComparison != 0)
                {
                    return idComparison;
                }
            }

            return string.CompareOrdinal(canonicalText, other.canonicalText);
        }

        int IComparable.CompareTo(object obj)
        {
            if (obj == null)
            {
                return 1;
            }

            Stage1WeaponPackageDescriptor other =
                obj as Stage1WeaponPackageDescriptor;
            if (other == null)
            {
                throw new ArgumentException(
                    "Object must be a Stage1WeaponPackageDescriptor.",
                    nameof(obj));
            }

            return CompareTo(other);
        }

        public override string ToString()
        {
            return canonicalText;
        }

        internal static int OrdinalHash(string value)
        {
            unchecked
            {
                uint hash = 2166136261u;
                for (int index = 0; index < value.Length; index++)
                {
                    hash ^= value[index];
                    hash *= 16777619u;
                }

                return (int)hash;
            }
        }

        private string BuildCanonicalText()
        {
            StringBuilder builder = new StringBuilder();
            builder.Append("descriptor_version=")
                .Append(DescriptorVersion.ToString(CultureInfo.InvariantCulture))
                .Append("\ndefault_starting_weapon=")
                .Append(IsDefaultStartingWeapon ? "true" : "false")
                .Append("\nnormal_fire:\n")
                .Append(NormalFire == null ? "null" : NormalFire.ToCanonicalString())
                .Append("\nempowered_fire:\n")
                .Append(EmpoweredFire == null ? "null" : EmpoweredFire.ToCanonicalString())
                .Append("\ncontent_definition:\n")
                .Append(ContentDefinition == null
                    ? "null"
                    : ContentDefinition.ToCanonicalString());
            return builder.ToString();
        }
    }
}
