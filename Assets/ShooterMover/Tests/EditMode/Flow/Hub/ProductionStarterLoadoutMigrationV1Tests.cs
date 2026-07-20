using System.Collections.Generic;
using NUnit.Framework;
using ShooterMover.Application.Flow.Production;
using ShooterMover.Application.Inventory.LoadoutScreen;
using ShooterMover.Contracts.Flow.Session;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Equipment;
using ShooterMover.Domain.Rewards.Model;

namespace ShooterMover.Tests.EditMode.Flow.Hub
{
    public sealed class ProductionStarterLoadoutMigrationV1Tests
    {
        [Test]
        public void ReorderedPersistedRouteRebuildsAllFiveDefinitionIdentities()
        {
            PlayerRouteProfilePayloadV1 initial =
                PlayerRouteProfilePayloadV1.Create(
                    StableId.Parse("character.starter-migration"),
                    StableId.Parse(
                        "loadout-profile.starter-migration"),
                    new[]
                    {
                        StableId.Parse(
                            "equipment-instance.flow-draft-slot-1"),
                        StableId.Parse(
                            "equipment-instance.flow-draft-slot-2"),
                        StableId.Parse(
                            "equipment-instance.flow-draft-slot-3"),
                        StableId.Parse(
                            "equipment-instance.flow-draft-slot-4"),
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
            for (int index = 0;
                index < restored.Holdings.ExportSnapshot()
                    .UniqueHoldings.Count;
                index++)
            {
                var holding = restored.Holdings.ExportSnapshot()
                    .UniqueHoldings[index];
                if (holding.RewardKind
                    == RewardGrantKindV1.EquipmentReference)
                {
                    definitions.Add(holding.DefinitionStableId);
                }
            }

            Assert.That(definitions.Count, Is.EqualTo(5));
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
                restored.LoadoutAuthority.ExportSnapshot()
                    .GetBinding(
                        InventoryLoadoutSlotIdsV1.WeaponOne)
                    .EquipmentInstanceStableId,
                Is.EqualTo(
                    first.RicochetEquipmentInstanceStableId));
        }
    }
}
