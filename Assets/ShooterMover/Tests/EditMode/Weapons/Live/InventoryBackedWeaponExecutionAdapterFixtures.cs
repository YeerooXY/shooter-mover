using System;
using System.Collections.Generic;
using NUnit.Framework;
using ShooterMover.Application.Holdings;
using ShooterMover.Application.Weapons.Execution;
using ShooterMover.Contracts.Equipment;
using ShooterMover.Contracts.Holdings;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Equipment;
using ShooterMover.Domain.Holdings;
using ShooterMover.Domain.Rewards.Model;
using ShooterMover.Domain.Weapons.Catalog;
using ShooterMover.Domain.Weapons.Execution;
using ShooterMover.UnityAdapters.Weapons.Live;

namespace ShooterMover.Tests.EditMode.Weapons.Live
{
    public sealed partial class InventoryBackedWeaponExecutionAdapterTests
    {
        private static Harness CreateHarness(params EquipmentInstance[] equipment)
        {
            EquipmentCatalog equipmentCatalog = EquipmentCatalogFor(equipment);
            WeaponCatalog weaponCatalog = WeaponCatalogFor();
            var lookup = new InMemoryEquipmentLookup(equipment);
            var sink = new RecordingSink();
            var ownership = new FixedActorSource();
            var adapter = new InventoryBackedWeaponExecutionAdapter(
                lookup,
                equipmentCatalog,
                weaponCatalog,
                ownership,
                sink,
                TicksPerSecond);
            return new Harness(adapter, sink);
        }

        private static InventoryWeaponFireRequest Request(
            EquipmentInstance equipment,
            string operation,
            long tick,
            ulong seed = 123UL)
        {
            return new InventoryWeaponFireRequest(
                new WeaponActorInstanceId(ActorId),
                new EquipmentInstanceId(equipment.InstanceId),
                new FireOperationId(StableId.Parse(operation)),
                new LifecycleGeneration(0L),
                tick,
                seed,
                new WeaponVector2(2d, 3d),
                new WeaponVector2(1d, 0d));
        }

        private static InventoryWeaponFireRequest CreateIntent(
            InventoryWeaponFireIntentFactory factory,
            string operation,
            long tick)
        {
            InventoryWeaponFireRequest request;
            string rejection;
            Assert.That(
                factory.TryCreate(
                    new WeaponActorInstanceId(ActorId),
                    new FireOperationId(StableId.Parse(operation)),
                    new LifecycleGeneration(0L),
                    tick,
                    123UL,
                    new WeaponVector2(2d, 3d),
                    new WeaponVector2(1d, 0d),
                    out request,
                    out rejection),
                Is.True,
                rejection);
            return request;
        }

        private static EquipmentCatalog EquipmentCatalogFor(
            IEnumerable<EquipmentInstance> equipment)
        {
            var definitionIds = new HashSet<StableId>();
            var definitions = new List<EquipmentDefinition>();
            foreach (EquipmentInstance instance in equipment)
            {
                if (!definitionIds.Add(instance.DefinitionId))
                {
                    continue;
                }

                definitions.Add(EquipmentDefinition.Create(
                    instance.DefinitionId,
                    EquipmentCategoryIds.Weapon,
                    EquipmentFamilyId,
                    instance.DefinitionId.ToString(),
                    RuntimeWeaponId(instance.DefinitionId),
                    InclusiveIntRange.Create(1, 100),
                    0,
                    new[] { EquipmentQualityTier.Create(QualityId, "Common", 1) },
                    new StableId[0]));
            }

            EquipmentCatalogBuildResult build = EquipmentCatalog.Build(
                definitions,
                new AugmentDefinition[0]);
            Assert.That(build.IsValid, Is.True);
            return build.Catalog;
        }

        private static StableId RuntimeWeaponId(StableId equipmentDefinitionId)
        {
            string value = equipmentDefinitionId.ToString();
            if (value.EndsWith("shotgun", StringComparison.Ordinal))
            {
                return StableId.Parse("weapon.shotgun");
            }

            if (value.EndsWith("rocket", StringComparison.Ordinal))
            {
                return StableId.Parse("weapon.rocket-launcher");
            }

            if (value.EndsWith("flamethrower", StringComparison.Ordinal))
            {
                return StableId.Parse("weapon.flamethrower");
            }

            return StableId.Parse("weapon.blaster-machine-gun");
        }

    }
}
