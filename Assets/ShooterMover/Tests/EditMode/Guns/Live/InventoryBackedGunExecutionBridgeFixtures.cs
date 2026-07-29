using System;
using System.Collections.Generic;
using NUnit.Framework;
using ShooterMover.Application.Holdings;
using ShooterMover.Application.Guns.Execution;
using ShooterMover.Contracts.Equipment;
using ShooterMover.Contracts.Holdings;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Equipment;
using ShooterMover.Domain.Holdings;
using ShooterMover.Domain.Rewards.Model;
using ShooterMover.Domain.Guns.Catalog;
using ShooterMover.Domain.Guns.Execution;
using ShooterMover.UnityAdapters.Guns.Live;

namespace ShooterMover.Tests.EditMode.Guns.Live
{
    public sealed partial class InventoryBackedGunExecutionBridgeTests
    {
        private static Harness CreateHarness(params EquipmentInstance[] equipment)
        {
            EquipmentCatalog equipmentCatalog = EquipmentCatalogFor(equipment);
            GunCatalog gunCatalog = GunCatalogFor();
            var lookup = new InMemoryEquipmentLookup(equipment);
            var sink = new RecordingSink();
            var ownership = new FixedActorSource();
            var adapter = new InventoryBackedGunExecutionBridge(
                lookup,
                equipmentCatalog,
                gunCatalog,
                ownership,
                sink,
                TicksPerSecond);
            return new Harness(adapter, sink);
        }

        private static InventoryGunFireRequest Request(
            EquipmentInstance equipment,
            string operation,
            long tick,
            ulong seed = 123UL)
        {
            return new InventoryGunFireRequest(
                new GunActorInstanceId(ActorId),
                new EquipmentInstanceId(equipment.InstanceId),
                new FireOperationId(StableId.Parse(operation)),
                new LifecycleGeneration(0L),
                tick,
                seed,
                new GunVector2(2d, 3d),
                new GunVector2(1d, 0d));
        }

        private static InventoryGunFireRequest CreateIntent(
            InventoryGunFireIntentFactory factory,
            string operation,
            long tick)
        {
            InventoryGunFireRequest request;
            string rejection;
            Assert.That(
                factory.TryCreate(
                    new GunActorInstanceId(ActorId),
                    new FireOperationId(StableId.Parse(operation)),
                    new LifecycleGeneration(0L),
                    tick,
                    123UL,
                    new GunVector2(2d, 3d),
                    new GunVector2(1d, 0d),
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
                    EquipmentCategoryIds.Gun,
                    EquipmentFamilyId,
                    instance.DefinitionId.ToString(),
                    RuntimeGunId(instance.DefinitionId),
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

        private static StableId RuntimeGunId(StableId equipmentDefinitionId)
        {
            string value = equipmentDefinitionId.ToString();
            if (value.EndsWith("ironwake", StringComparison.Ordinal))
            {
                return StableId.Parse("ironwake.mk1");
            }

            if (value.EndsWith("crownfall", StringComparison.Ordinal))
            {
                return StableId.Parse("crownfall.mk1");
            }

            if (value.EndsWith("nullstar", StringComparison.Ordinal))
            {
                return StableId.Parse("nullstar.mk1");
            }

            return StableId.Parse("rattler.mk1");
        }

    }
}
