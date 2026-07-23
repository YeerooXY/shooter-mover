
using System;
using System.Collections.Generic;
using ShooterMover.Application.Weapons.Catalog;
using ShooterMover.Domain.Equipment;
using ShooterMover.Domain.Weapons.Catalog;
using ShooterMover.Domain.Weapons.Execution;

namespace ShooterMover.Application.Weapons.Execution
{
    public sealed partial class WeaponCatalogRuntimeProfileResolver
    {
        private readonly EquipmentCatalog equipmentCatalog;
        private readonly WeaponCatalog weaponCatalog;
        private readonly HashSet<string> liveDefinitionIds;
        private readonly IWeaponRuntimePackageRegistryV1 runtimePackageRegistry;
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
                ProductionWeaponRuntimePackageRegistryV1.CreateDefault(),
                new RuntimeReferenceWeaponDefinitionIdResolver(),
                ticks)
        {
            if (selector == null) throw new ArgumentNullException(nameof(selector));
        }

        public WeaponCatalogRuntimeProfileResolver(
            EquipmentCatalog equipment,
            WeaponCatalog weapons,
            IWeaponBehaviorSelector selector,
            IEquipmentWeaponDefinitionIdResolver idResolver,
            int ticksPerSecond)
            : this(
                equipment,
                weapons,
                ProductionWeaponRuntimePackageRegistryV1.CreateDefault(),
                idResolver,
                ticksPerSecond)
        {
            if (selector == null) throw new ArgumentNullException(nameof(selector));
        }

        public WeaponCatalogRuntimeProfileResolver(
            EquipmentCatalog equipment,
            WeaponCatalog weapons,
            IWeaponRuntimePackageRegistryV1 packageRegistry,
            int ticksPerSecond)
            : this(
                equipment,
                weapons,
                packageRegistry,
                new RuntimeReferenceWeaponDefinitionIdResolver(),
                ticksPerSecond)
        {
        }

        public WeaponCatalogRuntimeProfileResolver(
            EquipmentCatalog equipment,
            WeaponCatalog weapons,
            IWeaponRuntimePackageRegistryV1 packageRegistry,
            IEquipmentWeaponDefinitionIdResolver idResolver,
            int ticksPerSecond)
        {
            if (ticksPerSecond < 1) throw new ArgumentOutOfRangeException(nameof(ticksPerSecond));
            equipmentCatalog = equipment ?? throw new ArgumentNullException(nameof(equipment));
            weaponCatalog = weapons ?? throw new ArgumentNullException(nameof(weapons));
            runtimePackageRegistry = packageRegistry ?? throw new ArgumentNullException(nameof(packageRegistry));
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

        public WeaponProfileResolution Resolve(EquipmentInstanceId requested, EquipmentInstance instance)
        {
            if (requested == null
                || instance == null
                || instance.InstanceId == null
                || requested.Value != instance.InstanceId)
            {
                return Reject(WeaponProfileResolutionStatus.InvalidEquipment, "weapon-equipment-instance-mismatch");
            }

            EquipmentValidationResult validation = equipmentCatalog.ValidateInstance(instance);
            if (validation == null || !validation.IsValid)
            {
                return Reject(WeaponProfileResolutionStatus.InvalidEquipment, "weapon-equipment-instance-invalid");
            }

            EquipmentDefinition equipment = equipmentCatalog.FindEquipmentDefinition(instance.DefinitionId);
            if (equipment == null
                || equipment.CategoryId != EquipmentCategoryIds.Weapon
                || equipment.RuntimeWeaponReferenceId == null)
            {
                return Reject(WeaponProfileResolutionStatus.InvalidEquipment, "weapon-equipment-definition-invalid");
            }

            WeaponDefinitionId definitionId;
            if (!definitionIdResolver.TryResolveWeaponDefinitionId(equipment, out definitionId) || definitionId == null)
            {
                return Reject(
                    WeaponProfileResolutionStatus.InvalidEquipment,
                    "weapon-equipment-definition-runtime-link-missing");
            }

            WeaponDefinitionData definition;
            string resolvedDefinitionId = definitionId.Value;
            if (!weaponCatalog.TryGetDefinition(resolvedDefinitionId, out definition) || definition == null)
            {
                if (!CanonicalWeaponCatalogProjectionV1.TryResolveDefinitionId(
                        weaponCatalog,
                        equipment.RuntimeWeaponReferenceId,
                        out resolvedDefinitionId)
                    || !weaponCatalog.TryGetDefinition(resolvedDefinitionId, out definition)
                    || definition == null)
                {
                    return Reject(
                        WeaponProfileResolutionStatus.UnknownWeaponDefinition,
                        "weapon-definition-unknown:" + definitionId.Value);
                }
            }

            if (!liveDefinitionIds.Contains(resolvedDefinitionId))
            {
                return Reject(
                    WeaponProfileResolutionStatus.PreviewOnlyWeaponDefinition,
                    "weapon-definition-preview-only:" + resolvedDefinitionId);
            }

            WeaponBehaviorId behaviorId;
            if (!runtimePackageRegistry.TryResolveBehavior(definition, out behaviorId) || behaviorId == null)
            {
                return Reject(
                    WeaponProfileResolutionStatus.RuntimeBehaviorPending,
                    "weapon-runtime-behavior-pending:" + resolvedDefinitionId);
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
