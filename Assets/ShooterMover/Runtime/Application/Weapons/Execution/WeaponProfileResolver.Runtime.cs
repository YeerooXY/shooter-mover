using System;
using System.Collections.Generic;
using ShooterMover.Domain.Equipment;
using ShooterMover.Domain.Weapons.Catalog;
using ShooterMover.Domain.Weapons.Execution;

namespace ShooterMover.Application.Weapons.Execution
{
    public sealed partial class WeaponCatalogRuntimeProfileResolver
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
                    definition.DotDps,
                    definition.DotDuration,
                    definition.PoolRadius,
                    definition.PoolDuration,
                    definition.ChainTargets,
                    definition.ChainRange,
                    definition.Knockback,
                    definition.DamageType));
        }

    }
}
