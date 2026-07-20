using System.Collections.Generic;
using NUnit.Framework;
using ShooterMover.Application.Flow.Production;
using ShooterMover.Application.Inventory.LoadoutScreen;
using ShooterMover.Contracts.Flow.Session;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Rewards.Model;

namespace ShooterMover.Tests.EditMode.Flow.Hub
{
    public sealed class ProductionStarterLoadoutMigrationV1Tests
    {
        [Test]
        public void ReorderedPersistedRouteRebuildsAllFiveExactStarterIdentities()
        {
            PlayerRouteProfilePayloadV1 initial =
                PlayerRouteProfilePayloadV1.Create(
                    StableId.Parse("character.starter-migration"),
                    StableId.Parse(
                        "loadout-profile.starter-migration"),
                    new[]
                    {
                        ProductionStarterWeaponCatalogV1
                            .BlasterEquipmentInstanceStableId,
                        ProductionStarterWeaponCatalogV1
                            .ShotgunEquipmentInstanceStableId,
                        ProductionStarterWeaponCatalogV1
                            .RocketEquipmentInstanceStableId,
                        ProductionStarterWeaponCatalogV1
                            .ArcEquipmentInstanceStableId,
                    });
            var first = new ProductionPlayerLoadoutRuntimeV1(initial);
            var service = new InventoryLoadoutScreenServiceV1(
                initial,
                first.Holdings,
                first.CatalogAdapter,
                first.LoadoutAuthority);
            service.TrySelect(
                InventoryLoadoutSlotIdsV1.WeaponOne,
                first.RicochetEquipmentInstanceStableId);
            PlayerRouteProfilePayloadV1 persisted =
                service.Confirm().RoutePayload;

            var restored =
                new ProductionPlayerLoadoutRuntimeV1(persisted);
            var definitions = new HashSet<StableId>();
            var instances = new HashSet<StableId>();
            var holdings = restored.Holdings.ExportSnapshot();
            for (int index = 0;
                index < holdings.UniqueHoldings.Count;
                index++)
            {
                var holding = holdings.UniqueHoldings[index];
                if (holding.RewardKind
                    == RewardGrantKindV1.EquipmentReference)
                {
                    definitions.Add(holding.DefinitionStableId);
                    instances.Add(holding.InstanceStableId);
                }
            }

            Assert.That(definitions.Count, Is.EqualTo(5));
            Assert.That(instances.Count, Is.EqualTo(5));
            for (int index = 0;
                index < ProductionStarterWeaponCatalogV1
                    .AllEquipmentDefinitionStableIds.Count;
                index++)
            {
                Assert.That(
                    definitions.Contains(
                        ProductionStarterWeaponCatalogV1
                            .AllEquipmentDefinitionStableIds[index]),
                    Is.True);
            }
            Assert.That(
                instances.Contains(
                    ProductionStarterWeaponCatalogV1
                        .BlasterEquipmentInstanceStableId),
                Is.True,
                "The displaced Blaster must retain its original concrete identity.");
            Assert.That(
                restored.LoadoutAuthority.ExportSnapshot()
                    .GetBinding(
                        InventoryLoadoutSlotIdsV1.WeaponOne)
                    .EquipmentInstanceStableId,
                Is.EqualTo(
                    first.RicochetEquipmentInstanceStableId));
        }
    }
}
