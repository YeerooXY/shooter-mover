using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Equipment;

namespace ShooterMover.Domain.Weapons
{
    /// <summary>
    /// Numeric effective-weapon values that an installed augment may modify.
    /// Structural kinds and feature presence are intentionally not modifier targets.
    /// RateOfFire is projectile-only and maps to WeaponFireSettings.ShotsPerSecond;
    /// continuous damage ticks and damage-over-time ticks are separate concepts.
    /// </summary>
    public enum WeaponEffectiveStat
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
    }

    public enum WeaponModifierOperation
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
    public sealed class WeaponStatModifier
    {
        private WeaponStatModifier(
            WeaponEffectiveStat stat,
            WeaponModifierOperation operation,
            double value)
        {
            Stat = stat;
            Operation = operation;
            Value = value;
        }

        public WeaponEffectiveStat Stat { get; }
        public WeaponModifierOperation Operation { get; }
        public double Value { get; }

        public static WeaponStatModifier Create(
            WeaponEffectiveStat stat,
            WeaponModifierOperation operation,
            double value)
        {
            if (!Enum.IsDefined(typeof(WeaponEffectiveStat), stat))
            {
                throw new ArgumentOutOfRangeException(nameof(stat));
            }
            if (!Enum.IsDefined(typeof(WeaponModifierOperation), operation))
            {
                throw new ArgumentOutOfRangeException(nameof(operation));
            }
            if (double.IsNaN(value) || double.IsInfinity(value))
            {
                throw new ArgumentOutOfRangeException(nameof(value));
            }
            if (operation == WeaponModifierOperation.Multiplier && value < 0d)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(value),
                    "Weapon stat multipliers cannot be negative.");
            }

            return new WeaponStatModifier(stat, operation, value);
        }

        public static WeaponStatModifier Flat(WeaponEffectiveStat stat, double value)
        {
            return Create(stat, WeaponModifierOperation.FlatAddition, value);
        }

        public static WeaponStatModifier AdditivePercent(WeaponEffectiveStat stat, double value)
        {
            return Create(stat, WeaponModifierOperation.AdditivePercentage, value);
        }

        public static WeaponStatModifier Multiply(WeaponEffectiveStat stat, double value)
        {
            return Create(stat, WeaponModifierOperation.Multiplier, value);
        }

        public static WeaponStatModifier Override(WeaponEffectiveStat stat, double value)
        {
            return Create(stat, WeaponModifierOperation.Override, value);
        }
    }

    /// <summary>
    /// Per-installed-augment modifier payload. The existing AugmentDefinition and
    /// AugmentInstance remain authoritative; this value only carries their resolved weapon effect.
    /// It is deliberately not a registry.
    /// </summary>
    public sealed class WeaponAugmentModifierSet
    {
        private readonly ReadOnlyCollection<WeaponStatModifier> modifiers;

        private WeaponAugmentModifierSet(
            AugmentDefinition definition,
            AugmentInstance instance,
            IEnumerable<WeaponStatModifier> modifiers)
        {
            Definition = definition;
            Instance = instance;
            this.modifiers = CopyModifiers(modifiers);
        }

        public AugmentDefinition Definition { get; }
        public AugmentInstance Instance { get; }
        public IReadOnlyList<WeaponStatModifier> Modifiers { get { return modifiers; } }

        public static WeaponAugmentModifierSet Create(
            AugmentDefinition definition,
            AugmentInstance instance,
            IEnumerable<WeaponStatModifier> modifiers)
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

            return new WeaponAugmentModifierSet(definition, instance, modifiers);
        }

        private static ReadOnlyCollection<WeaponStatModifier> CopyModifiers(
            IEnumerable<WeaponStatModifier> values)
        {
            if (values == null)
            {
                throw new ArgumentNullException(nameof(values));
            }

            List<WeaponStatModifier> copy = new List<WeaponStatModifier>();
            foreach (WeaponStatModifier value in values)
            {
                if (value == null)
                {
                    throw new ArgumentException(
                        "Weapon augment modifier collections cannot contain null values.",
                        nameof(values));
                }
                copy.Add(value);
            }
            return new ReadOnlyCollection<WeaponStatModifier>(copy);
        }
    }

    /// <summary>
    /// Raised when a numeric modifier would require adding or replacing weapon structure,
    /// such as adding homing to an unguided projectile or explosion radius to a non-explosive weapon.
    /// </summary>
    public sealed class IncompatibleWeaponAugmentException : InvalidOperationException
    {
        public IncompatibleWeaponAugmentException(
            StableId augmentInstanceId,
            StableId augmentDefinitionId,
            WeaponEffectiveStat stat,
            string reason)
            : base(BuildMessage(augmentInstanceId, augmentDefinitionId, stat, reason))
        {
            AugmentInstanceId = augmentInstanceId;
            AugmentDefinitionId = augmentDefinitionId;
            Stat = stat;
        }

        public StableId AugmentInstanceId { get; }
        public StableId AugmentDefinitionId { get; }
        public WeaponEffectiveStat Stat { get; }

        private static string BuildMessage(
            StableId augmentInstanceId,
            StableId augmentDefinitionId,
            WeaponEffectiveStat stat,
            string reason)
        {
            return "Installed augment "
                + (augmentInstanceId == null ? "<null>" : augmentInstanceId.ToString())
                + " (definition "
                + (augmentDefinitionId == null ? "<null>" : augmentDefinitionId.ToString())
                + ") cannot modify "
                + stat
                + ": "
                + (reason ?? "incompatible weapon structure")
                + ".";
        }
    }
}
