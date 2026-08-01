using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Equipment;

namespace ShooterMover.Domain.Guns
{
    /// <summary>
    /// Numeric effective-gun values that an installed augment may modify.
    /// Structural kinds and feature presence are intentionally not modifier targets.
    /// RateOfFire is projectile-only and maps to FireSettings.ShotsPerSecond;
    /// continuous damage ticks and damage-over-time ticks are separate concepts.
    /// RicochetTenths uses the canonical fixed-point budget: one unit equals 0.1 ricochet.
    /// </summary>
    public enum GunEffectiveStat
    {
        DirectDamage = 1,
        AreaDamage = 2,
        RateOfFire = 3,
        SpreadDegrees = 4,
        RandomnessDegrees = 5,
        ProjectileSpeed = 6,
        ProjectileRange = 7,
        PierceTenths = 8,
        ExplosionRadius = 9,
        DamageOverTimePerSecond = 10,
        DamageOverTimeDurationSeconds = 11,
        DamageOverTimeTicksPerSecond = 12,
        DamageOverTimeMaximumStacks = 13,
        HomingAcquisitionRange = 14,
        HomingTurnRateDegreesPerSecond = 15,
        HomingActivationDelaySeconds = 16,
        RicochetMaximumRicochets = 17,
        RicochetRetainedSpeed = 18,
        RicochetRandomAngleDegrees = 19,
        ChainMaximumTargets = 20,
        ChainAcquisitionRange = 21,
        ChainRetainedDamagePerJump = 22,
        RicochetTenths = 23,
    }

    public enum GunModifierOperation
    {
        FlatAddition = 1,
        AdditivePercentage = 2,
        Multiplier = 3,
        Override = 4,
    }

    /// <summary>
    /// One immutable numeric modification. AdditivePercentage uses decimal fractions:
    /// 0.10 means plus ten percent and -0.10 means minus ten percent.
    /// </summary>
    public sealed class GunStatModifier
    {
        private GunStatModifier(
            GunEffectiveStat stat,
            GunModifierOperation operation,
            double value)
        {
            Stat = stat;
            Operation = operation;
            Value = value;
        }

        public GunEffectiveStat Stat { get; }
        public GunModifierOperation Operation { get; }
        public double Value { get; }

        public static GunStatModifier Create(
            GunEffectiveStat stat,
            GunModifierOperation operation,
            double value)
        {
            if (!Enum.IsDefined(typeof(GunEffectiveStat), stat))
            {
                throw new ArgumentOutOfRangeException(nameof(stat));
            }
            if (!Enum.IsDefined(typeof(GunModifierOperation), operation))
            {
                throw new ArgumentOutOfRangeException(nameof(operation));
            }
            if (double.IsNaN(value) || double.IsInfinity(value))
            {
                throw new ArgumentOutOfRangeException(nameof(value));
            }
            if (operation == GunModifierOperation.Multiplier && value < 0d)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(value),
                    "Gun stat multipliers cannot be negative.");
            }

            return new GunStatModifier(stat, operation, value);
        }

        public static GunStatModifier Flat(GunEffectiveStat stat, double value)
        {
            return Create(stat, GunModifierOperation.FlatAddition, value);
        }

        public static GunStatModifier AdditivePercent(GunEffectiveStat stat, double value)
        {
            return Create(stat, GunModifierOperation.AdditivePercentage, value);
        }

        public static GunStatModifier Multiply(GunEffectiveStat stat, double value)
        {
            return Create(stat, GunModifierOperation.Multiplier, value);
        }

        public static GunStatModifier Override(GunEffectiveStat stat, double value)
        {
            return Create(stat, GunModifierOperation.Override, value);
        }
    }

    /// <summary>
    /// Per-installed-augment modifier payload. The existing AugmentDefinition and
    /// AugmentInstance remain authoritative; this value only carries their resolved gun effect.
    /// It is deliberately not a registry.
    /// </summary>
    public sealed class GunAugmentModifierSet
    {
        private readonly ReadOnlyCollection<GunStatModifier> modifiers;

        private GunAugmentModifierSet(
            AugmentDefinition definition,
            AugmentInstance instance,
            IEnumerable<GunStatModifier> modifiers)
        {
            Definition = definition;
            Instance = instance;
            this.modifiers = CopyModifiers(modifiers);
        }

        public AugmentDefinition Definition { get; }
        public AugmentInstance Instance { get; }
        public IReadOnlyList<GunStatModifier> Modifiers { get { return modifiers; } }

        public static GunAugmentModifierSet Create(
            AugmentDefinition definition,
            AugmentInstance instance,
            IEnumerable<GunStatModifier> modifiers)
        {
            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }
            if (instance == null)
            {
                throw new ArgumentNullException(nameof(instance));
            }
            if (definition.DefinitionId == null
                || instance.DefinitionId == null
                || !definition.DefinitionId.Equals(instance.DefinitionId))
            {
                throw new ArgumentException(
                    "The augment definition and instance must have the same existing definition identity.");
            }

            return new GunAugmentModifierSet(definition, instance, modifiers);
        }

        private static ReadOnlyCollection<GunStatModifier> CopyModifiers(
            IEnumerable<GunStatModifier> values)
        {
            if (values == null)
            {
                throw new ArgumentNullException(nameof(values));
            }

            List<GunStatModifier> copy = new List<GunStatModifier>();
            foreach (GunStatModifier value in values)
            {
                if (value == null)
                {
                    throw new ArgumentException(
                        "Gun augment modifier collections cannot contain null values.",
                        nameof(values));
                }
                copy.Add(value);
            }
            return new ReadOnlyCollection<GunStatModifier>(copy);
        }
    }

    /// <summary>
    /// Raised when a numeric modifier would require adding or replacing gun structure,
    /// such as adding homing to an unguided projectile or explosion radius to a non-explosive gun.
    /// </summary>
    public sealed class IncompatibleGunAugmentException : InvalidOperationException
    {
        public IncompatibleGunAugmentException(
            StableId augmentInstanceId,
            StableId augmentDefinitionId,
            GunEffectiveStat stat,
            string reason)
            : base(BuildMessage(augmentInstanceId, augmentDefinitionId, stat, reason))
        {
            AugmentInstanceId = augmentInstanceId;
            AugmentDefinitionId = augmentDefinitionId;
            Stat = stat;
        }

        public StableId AugmentInstanceId { get; }
        public StableId AugmentDefinitionId { get; }
        public GunEffectiveStat Stat { get; }

        private static string BuildMessage(
            StableId augmentInstanceId,
            StableId augmentDefinitionId,
            GunEffectiveStat stat,
            string reason)
        {
            return "Installed augment "
                + (augmentInstanceId == null ? "<null>" : augmentInstanceId.ToString())
                + " (definition "
                + (augmentDefinitionId == null ? "<null>" : augmentDefinitionId.ToString())
                + ") cannot modify "
                + stat
                + ": "
                + (reason ?? "incompatible gun structure")
                + ".";
        }
    }
}
