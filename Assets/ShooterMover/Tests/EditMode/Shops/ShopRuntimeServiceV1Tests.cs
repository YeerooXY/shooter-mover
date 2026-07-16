using System;
using System.Collections.Generic;
using NUnit.Framework;
using ShooterMover.Application.Economy.Money;
using ShooterMover.Application.Economy.Scrap;
using ShooterMover.Application.Holdings;
using ShooterMover.Application.Rewards.Application;
using ShooterMover.Application.Rewards.Generation;
using ShooterMover.Application.Shops;
using ShooterMover.Contracts.Equipment;
using ShooterMover.Contracts.Holdings;
using ShooterMover.Contracts.Rewards;
using ShooterMover.Contracts.Rewards.Application;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Common.Random;
using ShooterMover.Domain.Economy.Money;
using ShooterMover.Domain.Equipment;
using ShooterMover.Domain.Holdings;
using ShooterMover.Domain.Progression.Context;
using ShooterMover.Domain.Progression.Curves;
using ShooterMover.Domain.Rewards.Generation;
using ShooterMover.Domain.Rewards.Model;
using ShooterMover.Domain.Shops;

namespace ShooterMover.Tests.EditMode.Shops
{
    public sealed partial class ShopRuntimeServiceV1Tests
    {
        [Test]
        public void SameShopContextProducesIdenticalInventory()
        {
            Fixture left = new Fixture();
            Fixture right = new Fixture();

            ShopInventoryViewV1 a = left.Open("run.same");
            ShopInventoryViewV1 b = right.Open("run.same");

            Assert.That(a.InventoryFingerprint, Is.EqualTo(b.InventoryFingerprint));
            Assert.That(a.Entries, Has.Count.EqualTo(b.Entries.Count));
            for (int index = 0; index < a.Entries.Count; index++)
            {
                Assert.That(a.Entries[index].Equipment.Fingerprint,
                    Is.EqualTo(b.Entries[index].Equipment.Fingerprint));
                Assert.That(a.Entries[index].Price, Is.EqualTo(b.Entries[index].Price));
            }
        }

        [Test]
        public void DifferentRunIdChangesDeterministicStock()
        {
            Fixture fixture = new Fixture();

            ShopInventoryViewV1 left = fixture.Open("run.left");
            ShopInventoryViewV1 right = fixture.Open("run.right");

            Assert.That(left.InventorySeed, Is.Not.EqualTo(right.InventorySeed));
            Assert.That(left.InventoryFingerprint, Is.Not.EqualTo(right.InventoryFingerprint));
            Assert.That(left.Entries[0].StockEntryStableId,
                Is.Not.EqualTo(right.Entries[0].StockEntryStableId));
        }

        [Test]
        public void DifferentRefreshOrdinalChangesDeterministicStock()
        {
            Fixture fixture = new Fixture(maximumRefreshes: 2);
            ShopInventoryViewV1 before = fixture.Open("run.refresh-ordinal");

            ShopRefreshFactV1 refreshed = fixture.Service.Refresh(
                ShopRefreshCommandV1.Create(
                    Id("shop-refresh.ordinal"),
                    before.RunStableId,
                    before.ShopStableId,
                    before.InventoryFingerprint,
                    Context(10)));
            ShopInventoryViewV1 after;
            fixture.Service.TryGetInventory(before.RunStableId, before.ShopStableId, out after);

            Assert.That(refreshed.Status, Is.EqualTo(ShopRefreshStatusV1.Applied));
            Assert.That(after.RefreshOrdinal, Is.EqualTo(1));
            Assert.That(after.InventorySeed, Is.Not.EqualTo(before.InventorySeed));
            Assert.That(after.InventoryFingerprint, Is.Not.EqualTo(before.InventoryFingerprint));
        }

        [Test]
        public void RevisitDoesNotRefresh()
        {
            Fixture fixture = new Fixture();
            ShopInventoryViewV1 first = fixture.Open("run.revisit", Context(4));

            ShopInventoryOpenResultV1 second = fixture.Service.Open(
                first.RunStableId,
                fixture.Definition,
                fixture.Catalog,
                Context(40));

            Assert.That(second.Status, Is.EqualTo(ShopInventoryOpenStatusV1.ExistingNoChange));
            Assert.That(second.Inventory.RefreshOrdinal, Is.Zero);
            Assert.That(second.Inventory.InventoryFingerprint, Is.EqualTo(first.InventoryFingerprint));
            Assert.That(second.Inventory.ProgressionContextFingerprint,
                Is.EqualTo(first.ProgressionContextFingerprint));
        }

        [Test]
        public void DeathRestartAndReloadDoNotRefresh()
        {
            Fixture original = new Fixture();
            ShopInventoryViewV1 before = original.Open("run.reload");
            ShopRuntimeSnapshotV1 snapshot = original.Service.ExportSnapshot();

            Fixture restored = new Fixture();
            string rejection;
            Assert.That(restored.Service.TryImportSnapshot(snapshot, out rejection), Is.True, rejection);
            ShopInventoryOpenResultV1 reopened = restored.Service.Open(
                before.RunStableId,
                restored.Definition,
                restored.Catalog,
                Context(99));

            Assert.That(reopened.Status, Is.EqualTo(ShopInventoryOpenStatusV1.ExistingNoChange));
            Assert.That(reopened.Inventory.RefreshOrdinal, Is.Zero);
            Assert.That(reopened.Inventory.InventoryFingerprint, Is.EqualTo(before.InventoryFingerprint));
        }

        [Test]
        public void InventorySizeIsConfigurable()
        {
            Fixture fixture = new Fixture(inventorySize: 5);

            ShopInventoryViewV1 inventory = fixture.Open("run.size");

            Assert.That(inventory.Entries, Has.Count.EqualTo(5));
        }

        [Test]
        public void CategoryAndTagRestrictionsAreRespected()
        {
            Fixture fixture = new Fixture(inventorySize: 8);

            ShopInventoryViewV1 inventory = fixture.Open("run.restrictions");

            foreach (ShopStockEntryV1 entry in inventory.Entries)
            {
                EquipmentDefinition definition = fixture.Catalog.FindEquipmentDefinition(
                    entry.Equipment.DefinitionId);
                Assert.That(definition.CategoryId, Is.EqualTo(EquipmentCategoryIds.Armor));
                Assert.That(definition.HasTag(Id("equipment-tag.energy")), Is.True);
                Assert.That(definition.HasTag(Id("equipment-tag.forbidden")), Is.False);
            }
        }

        [Test]
        public void PricesAreDeterministic()
        {
            Fixture left = new Fixture();
            Fixture right = new Fixture();

            ShopInventoryViewV1 a = left.Open("run.prices");
            ShopInventoryViewV1 b = right.Open("run.prices");

            for (int index = 0; index < a.Entries.Count; index++)
            {
                Assert.That(a.Entries[index].Price, Is.EqualTo(b.Entries[index].Price));
                Assert.That(a.Entries[index].Price, Is.GreaterThan(0L));
            }
        }

        [Test]
        public void PurchaseSpendsExactMoneyOnce()
        {
            Fixture fixture = new Fixture(startingMoney: 10000L);
            ShopInventoryViewV1 inventory = fixture.Open("run.spend-once");
            ShopPurchaseCommandV1 command = fixture.PurchaseCommand(
                "shop-purchase.spend-once",
                inventory,
                inventory.Entries[0]);
            long before = fixture.Money.Balance;

            ShopPurchaseFactV1 first = fixture.Service.Purchase(command);
            ShopPurchaseFactV1 duplicate = fixture.Service.Purchase(command);

            Assert.That(first.Status, Is.EqualTo(ShopPurchaseStatusV1.Applied));
            Assert.That(duplicate.Status, Is.EqualTo(ShopPurchaseStatusV1.ExactDuplicateNoChange));
            Assert.That(duplicate.OriginalStatus, Is.EqualTo(ShopPurchaseStatusV1.Applied));
            Assert.That(fixture.Money.Balance, Is.EqualTo(before - inventory.Entries[0].Price));
            Assert.That(fixture.Money.Sequence, Is.EqualTo(2L));
        }

        [Test]
        public void PurchaseGrantsExactEquipmentOnce()
        {
            Fixture fixture = new Fixture(startingMoney: 10000L);
            ShopInventoryViewV1 inventory = fixture.Open("run.grant-once");
            ShopStockEntryV1 entry = inventory.Entries[0];
            ShopPurchaseCommandV1 command = fixture.PurchaseCommand(
                "shop-purchase.grant-once",
                inventory,
                entry);

            fixture.Service.Purchase(command);
            fixture.Service.Purchase(command);

            UniqueHoldingSnapshotV1 holding;
            Assert.That(fixture.Holdings.TryGetUnique(entry.Equipment.InstanceId, out holding), Is.True);
            Assert.That(holding.EquipmentInstance.Fingerprint, Is.EqualTo(entry.Equipment.Fingerprint));
            Assert.That(fixture.Holdings.Sequence, Is.EqualTo(1L));
        }

        [Test]
        public void SoldOutEntryCannotBePurchasedAgain()
        {
            Fixture fixture = new Fixture(startingMoney: 10000L);
            ShopInventoryViewV1 inventory = fixture.Open("run.sold-out");
            ShopStockEntryV1 entry = inventory.Entries[0];
            fixture.Service.Purchase(fixture.PurchaseCommand("shop-purchase.first", inventory, entry));

            ShopPurchaseFactV1 second = fixture.Service.Purchase(
                fixture.PurchaseCommand("shop-purchase.second", inventory, entry));

            Assert.That(second.Status, Is.EqualTo(ShopPurchaseStatusV1.SoldOut));
        }

    }
}
