using System;
using System.Collections.Generic;
using ShooterMover.Domain.Equipment;
using ShooterMover.Domain.Guns.Catalog;
using ShooterMover.Domain.Guns.Execution;

namespace ShooterMover.Application.Guns.Execution
{
    /// <summary>
    /// Legacy flat-catalog runtime projection retained for GunExecutionCore tooling/tests only.
    /// Live firing resolves Gun and EffectiveGun instead.
    /// </summary>
    [Obsolete(
        "Legacy tooling/test resolver only. Live firing resolves EffectiveGun explicitly.",
        false)]
    public sealed partial class GunCatalogLiveProfileResolver
    {
        private const double Epsilon = 0.000000001d;

        private readonly EquipmentCatalog equipmentCatalog;
        private readonly GunCatalog gunCatalog;
        private readonly HashSet<string> liveDefinitionIds;
        private readonly IGunBehaviorSelector behaviorSelector;
        private readonly IEquipmentGunDefinitionIdResolver definitionIdResolver;
        private readonly int simulationTicksPerSecond;

        public GunCatalogLiveProfileResolver(
            EquipmentCatalog equipment,
            GunCatalog guns,
            IGunBehaviorSelector selector,
            int ticks)
            : this(
                equipment,
                guns,
                selector,
                new LiveReferenceGunDefinitionIdResolver(),
                ticks)
        {
        }

        public GunCatalogLiveProfileResolver(
            EquipmentCatalog equipment,
            GunCatalog guns,
            IGunBehaviorSelector selector,
            IEquipmentGunDefinitionIdResolver idResolver,
            int ticksPerSecond)
        {
            if (ticksPerSecond < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(ticksPerSecond));
            }

            equipmentCatalog = equipment ?? throw new ArgumentNullException(nameof(equipment));
            gunCatalog = guns ?? throw new ArgumentNullException(nameof(guns));
            behaviorSelector = selector ?? throw new ArgumentNullException(nameof(selector));
            definitionIdResolver = idResolver ?? throw new ArgumentNullException(nameof(idResolver));
            simulationTicksPerSecond = ticksPerSecond;

            liveDefinitionIds = new HashSet<string>(StringComparer.Ordinal);
            IReadOnlyList<GunDefinitionData> liveDefinitions =
                gunCatalog.GetDefinitions(GunCatalogContentFilter.LiveOnly);
            for (int index = 0; index < liveDefinitions.Count; index++)
            {
                liveDefinitionIds.Add(liveDefinitions[index].DefinitionId);
            }
        }

        public GunProfileResolution Resolve(
            EquipmentInstanceId requested,
            EquipmentInstance instance)
        {
            if (requested == null
                || instance == null
                || instance.InstanceId == null
                || requested.Value != instance.InstanceId)
            {
                return Reject(
                    GunProfileResolutionStatus.InvalidEquipment,
                    "gun-equipment-instance-mismatch");
            }

            EquipmentValidationResult validation = equipmentCatalog.ValidateInstance(instance);
            if (validation == null || !validation.IsValid)
            {
                return Reject(
                    GunProfileResolutionStatus.InvalidEquipment,
                    "gun-equipment-instance-invalid");
            }

            EquipmentDefinition equipment =
                equipmentCatalog.FindEquipmentDefinition(instance.DefinitionId);
            if (equipment == null
                || equipment.CategoryId != EquipmentCategoryIds.Gun
                || equipment.RuntimeGunReferenceId == null)
            {
                return Reject(
                    GunProfileResolutionStatus.InvalidEquipment,
                    "gun-equipment-definition-invalid");
            }

            GunDefinitionId definitionId;
            if (!definitionIdResolver.TryResolveGunDefinitionId(equipment, out definitionId)
                || definitionId == null)
            {
                return Reject(
                    GunProfileResolutionStatus.InvalidEquipment,
                    "gun-equipment-definition-runtime-link-missing");
            }

            GunDefinitionData definition;
            if (!gunCatalog.TryGetDefinition(definitionId.Value, out definition)
                || definition == null)
            {
                return Reject(
                    GunProfileResolutionStatus.UnknownGunDefinition,
                    "gun-definition-unknown:" + definitionId.Value);
            }

            if (!liveDefinitionIds.Contains(definitionId.Value))
            {
                return Reject(
                    GunProfileResolutionStatus.PreviewOnlyGunDefinition,
                    "gun-definition-preview-only:" + definitionId.Value);
            }

            string invalidCode;
            if (!Validate(definition, out invalidCode))
            {
                GunProfileResolutionStatus status = invalidCode.StartsWith(
                    "gun-effect-unsupported",
                    StringComparison.Ordinal)
                    ? GunProfileResolutionStatus.UnsupportedEffects
                    : GunProfileResolutionStatus.InvalidTuning;
                return Reject(status, invalidCode);
            }

            GunBehaviorId behaviorId;
            if (!behaviorSelector.TrySelect(definition, out behaviorId) || behaviorId == null)
            {
                return Reject(
                    GunProfileResolutionStatus.UnknownBehavior,
                    "gun-behavior-unresolved:" + definitionId.Value);
            }

            int cooldownTicks = Math.Max(
                1,
                (int)Math.Ceiling(simulationTicksPerSecond / definition.FireRate));
            return GunProfileResolution.Resolve(
                new GunLiveFiringProfile(
                    new GunDefinitionId(definition.DefinitionId),
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
