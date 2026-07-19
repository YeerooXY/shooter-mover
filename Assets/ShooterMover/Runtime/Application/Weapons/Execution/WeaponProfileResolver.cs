using System;
using System.Collections.Generic;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Equipment;
using ShooterMover.Domain.Weapons.Catalog;
using ShooterMover.Domain.Weapons.Execution;

namespace ShooterMover.Application.Weapons.Execution
{
    public static class BuiltInWeaponBehaviorIds
    {
        public static readonly WeaponBehaviorId Projectile = new WeaponBehaviorId(
            StableId.Parse("weapon-behavior.projectile"));
        public static readonly WeaponBehaviorId Explosive = new WeaponBehaviorId(
            StableId.Parse("weapon-behavior.explosive"));
        public static readonly WeaponBehaviorId Chain = new WeaponBehaviorId(
            StableId.Parse("weapon-behavior.chain"));
    }

    public enum WeaponProfileResolutionStatus
    {
        Resolved = 1,
        InvalidEquipment = 2,
        UnknownWeaponDefinition = 3,
        PreviewOnlyWeaponDefinition = 4,
        InvalidTuning = 5,
        UnsupportedEffects = 6,
        UnknownBehavior = 7,
    }

    public sealed class WeaponProfileResolution
    {
        private WeaponProfileResolution(
            WeaponProfileResolutionStatus status,
            WeaponRuntimeFiringProfile profile,
            string rejectionCode)
        {
            Status = status;
            Profile = profile;
            RejectionCode = rejectionCode ?? string.Empty;
        }

        public WeaponProfileResolutionStatus Status { get; }
        public WeaponRuntimeFiringProfile Profile { get; }
        public string RejectionCode { get; }
        public bool Succeeded { get { return Status == WeaponProfileResolutionStatus.Resolved; } }

        public static WeaponProfileResolution Resolve(WeaponRuntimeFiringProfile profile)
        {
            return new WeaponProfileResolution(
                WeaponProfileResolutionStatus.Resolved,
                profile ?? throw new ArgumentNullException(nameof(profile)),
                string.Empty);
        }

        public static WeaponProfileResolution Reject(
            WeaponProfileResolutionStatus status,
            string code)
        {
            if (status == WeaponProfileResolutionStatus.Resolved)
            {
                throw new ArgumentOutOfRangeException(nameof(status));
            }

            return new WeaponProfileResolution(status, null, code);
        }
    }

    public interface IWeaponBehaviorSelector
    {
        bool TrySelect(
            WeaponDefinitionData definition,
            out WeaponBehaviorId behaviorId);
    }

    public sealed class DefaultWeaponBehaviorSelector : IWeaponBehaviorSelector
    {
        public bool TrySelect(
            WeaponDefinitionData definition,
            out WeaponBehaviorId behaviorId)
        {
            if (definition == null)
            {
                behaviorId = null;
                return false;
            }

            if (definition.ChainTargets > 0)
            {
                behaviorId = BuiltInWeaponBehaviorIds.Chain;
                return true;
            }

            if (definition.AreaDamagePerTrigger > 0d || definition.ExplosionRadius > 0d)
            {
                behaviorId = BuiltInWeaponBehaviorIds.Explosive;
                return true;
            }

            behaviorId = BuiltInWeaponBehaviorIds.Projectile;
            return true;
        }
    }

    public interface IEquipmentWeaponDefinitionIdResolver
    {
        bool TryResolveWeaponDefinitionId(
            EquipmentDefinition equipmentDefinition,
            out WeaponDefinitionId weaponDefinitionId);
    }

    public sealed class RuntimeReferenceWeaponDefinitionIdResolver
        : IEquipmentWeaponDefinitionIdResolver
    {
        public bool TryResolveWeaponDefinitionId(
            EquipmentDefinition definition,
            out WeaponDefinitionId id)
        {
            if (definition == null || definition.RuntimeWeaponReferenceId == null)
            {
                id = null;
                return false;
            }

            id = new WeaponDefinitionId(definition.RuntimeWeaponReferenceId.ToString());
            return true;
        }
    }

    public sealed class WeaponCatalogRuntimeProfileResolver
    {
        private const double Epsilon = 0.000000001d;

        private readonly EquipmentCatalog equipmentCatalog;
        private readonly WeaponCatalog weaponCatalog;
        private readonly HashSet<string> liveDefinitionIds;
        private readonly IWeaponBehaviorSelector behaviorSelector;
        private readonly IEquipmentWeaponDefinitionIdResolver definitionIdResolver;
        private readonly int simulationTicksPerSecond;

        public WeaponCatalogRuntimeProfileResolver(
            EquipmentCatalog equipment,
            WeaponCatalog weapons,
            IWeaponBehaviorSelector selector,
            int ticks)
            : this(
                equipment,
                weapons,
                selector,
                new RuntimeReferenceWeaponDefinitionIdResolver(),
                ticks)
        {
        }

        public WeaponCatalogRuntimeProfileResolver(
            EquipmentCatalog equipment,
            WeaponCatalog weapons,
            IWeaponBehaviorSelector selector,
            IEquipmentWeaponDefinitionIdResolver idResolver,
            int ticksPerSecond)
        {
            if (ticksPerSecond < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(ticksPerSecond));
            }

            equipmentCatalog = equipment ?? throw new ArgumentNullException(nameof(equipment));
            weaponCatalog = weapons ?? throw new ArgumentNullException(nameof(weapons));
            behaviorSelector = selector ?? throw new ArgumentNullException(nameof(selector));
            definitionIdResolver = idResolver ?? throw new ArgumentNullException(nameof(idResolver));
            simulationTicksPerSecond = ticksPerSecond;

            liveDefinitionIds = new HashSet<string>(StringComparer.Ordinal);
            IReadOnlyList<WeaponDefinitionData> liveDefinitions =
                weaponCatalog.GetDefinitions(WeaponCatalogContentFilter.LiveOnly);
            for (int index = 0; index < liveDefinitions.Count; index++)
            {
                liveDefinitionIds.Add(liveDefinitions[index].DefinitionId);
            }
        }

        public WeaponProfileResolution Resolve(
            EquipmentInstanceId requested,
            EquipmentInstance instance)
        {
            if (requested == null
                || instance == null
                || instance.InstanceId == null
                || requested.Value != instance.InstanceId)
            {
                return Reject(
                    WeaponProfileResolutionStatus.InvalidEquipment,
                    "weapon-equipment-instance-mismatch");
            }

            EquipmentValidationResult validation = equipmentCatalog.ValidateInstance(instance);
            if (validation == null || !validation.IsValid)
            {
                return Reject(
                    WeaponProfileResolutionStatus.InvalidEquipment,
                    "weapon-equipment-instance-invalid");
            }

            EquipmentDefinition equipment =
                equipmentCatalog.FindEquipmentDefinition(instance.DefinitionId);
            if (equipment == null
                || equipment.CategoryId != EquipmentCategoryIds.Weapon
                || equipment.RuntimeWeaponReferenceId == null)
            {
                return Reject(
                    WeaponProfileResolutionStatus.InvalidEquipment,
                    "weapon-equipment-definition-invalid");
            }

            WeaponDefinitionId definitionId;
            if (!definitionIdResolver.TryResolveWeaponDefinitionId(equipment, out definitionId)
                || definitionId == null)
            {
                return Reject(
                    WeaponProfileResolutionStatus.InvalidEquipment,
                    "weapon-equipment-definition-runtime-link-missing");
            }

            WeaponDefinitionData definition;
            if (!weaponCatalog.TryGetDefinition(definitionId.Value, out definition)
                || definition == null)
            {
                return Reject(
                    WeaponProfileResolutionStatus.UnknownWeaponDefinition,
                    "weapon-definition-unknown:" + definitionId.Value);
            }

            if (!liveDefinitionIds.Contains(definitionId.Value))
            {
                return Reject(
                    WeaponProfileResolutionStatus.PreviewOnlyWeaponDefinition,
                    "weapon-definition-preview-only:" + definitionId.Value);
            }

            string invalidCode;
            if (!Validate(definition, out invalidCode))
            {
                WeaponProfileResolutionStatus status = invalidCode.StartsWith(
                    "weapon-effect-unsupported",
                    StringComparison.Ordinal)
                    ? WeaponProfileResolutionStatus.UnsupportedEffects
                    : WeaponProfileResolutionStatus.InvalidTuning;
                return Reject(status, invalidCode);
            }

            WeaponBehaviorId behaviorId;
            if (!behaviorSelector.TrySelect(definition, out behaviorId) || behaviorId == null)
            {
                return Reject(
                    WeaponProfileResolutionStatus.UnknownBehavior,
                    "weapon-behavior-unresolved:" + definitionId.Value);
            }

            int cooldownTicks = Math.Max(
                1,
                (int)Math.Ceiling(simulationTicksPerSecond / definition.FireRate));
            return WeaponProfileResolution.Resolve(
                new WeaponRuntimeFiringProfile(
                    new WeaponDefinitionId(definition.DefinitionId),
                    behaviorId,
                    cooldownTicks,
                    definition.ProjectilesPerTrigger,
                    definition.SpreadDegrees,
                    definition.ProjectileSpeed,
                    definition.Range,
                    definition.DamagePerProjectile,
                    definition.Pierce,
                    definition.AreaDamagePerTrigger,
                    definition.ExplosionRadius,
                    definition.ChainTargets,
                    definition.ChainRange,
                    definition.Knockback,
                    definition.DamageType));
        }

        private static WeaponProfileResolution Reject(
            WeaponProfileResolutionStatus status,
            string code)
        {
            return WeaponProfileResolution.Reject(status, code);
        }

        private static bool Validate(WeaponDefinitionData definition, out string code)
        {
            if (definition.BurstCount != 1
                || definition.DotShare > Epsilon
                || definition.DotDps > Epsilon
                || definition.DotDuration > Epsilon
                || definition.PoolRadius > Epsilon
                || definition.PoolDuration > Epsilon
                || definition.HealingPerSecond > Epsilon)
            {
                code = "weapon-effect-unsupported:" + definition.DefinitionId;
                return false;
            }

            if (!IsPositive(definition.FireRate)
                || definition.ProjectilesPerTrigger < 1
                || definition.ProjectilesPerTrigger > WeaponRuntimeFiringProfile.MaximumEffectsPerFire
                || !IsInRange(definition.SpreadDegrees, 0d, 360d)
                || !IsPositive(definition.ProjectileSpeed)
                || !IsPositive(definition.Range)
                || !IsNonNegative(definition.DamagePerProjectile)
                || definition.Pierce < 0
                || !IsNonNegative(definition.AreaDamagePerTrigger)
                || !IsNonNegative(definition.ExplosionRadius)
                || definition.ChainTargets < 0
                || !IsNonNegative(definition.ChainRange)
                || !IsNonNegative(definition.Knockback)
                || string.IsNullOrWhiteSpace(definition.DamageType))
            {
                code = "weapon-tuning-invalid:" + definition.DefinitionId;
                return false;
            }

            bool explosive = definition.AreaDamagePerTrigger > Epsilon
                || definition.ExplosionRadius > Epsilon;
            bool chain = definition.ChainTargets > 0 || definition.ChainRange > Epsilon;

            if (explosive && chain)
            {
                code = "weapon-effect-unsupported-combination:" + definition.DefinitionId;
                return false;
            }

            if (explosive
                && (!IsPositive(definition.AreaDamagePerTrigger)
                    || !IsPositive(definition.ExplosionRadius)))
            {
                code = "weapon-tuning-invalid-explosion:" + definition.DefinitionId;
                return false;
            }

            if (chain
                && (definition.ChainTargets < 1
                    || !IsPositive(definition.ChainRange)
                    || definition.ProjectilesPerTrigger != 1
                    || !IsPositive(definition.DamagePerProjectile)))
            {
                code = "weapon-tuning-invalid-chain:" + definition.DefinitionId;
                return false;
            }

            if (!explosive && !chain && !IsPositive(definition.DamagePerProjectile))
            {
                code = "weapon-tuning-invalid-direct:" + definition.DefinitionId;
                return false;
            }

            code = string.Empty;
            return true;
        }

        private static bool IsPositive(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value) && value > 0d;
        }

        private static bool IsNonNegative(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value) && value >= 0d;
        }

        private static bool IsInRange(double value, double minimum, double maximum)
        {
            return !double.IsNaN(value)
                && !double.IsInfinity(value)
                && value >= minimum
                && value <= maximum;
        }
    }
}
