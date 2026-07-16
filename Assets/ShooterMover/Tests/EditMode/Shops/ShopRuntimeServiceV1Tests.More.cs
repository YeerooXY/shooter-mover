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
        public void DuplicatePurchaseIsNoChangeReplay()
        {
            Fixture fixture = new Fixture(startingMoney: 10000L);
            ShopInventoryViewV1 inventory = fixture.Open("run.duplicate");
            ShopPurchaseCommandV1 command = fixture.PurchaseCommand(
                "shop-purchase.duplicate",
                inventory,
                inventory.Entries[0]);
            ShopPurchaseFactV1 original = fixture.Service.Purchase(command);
            long moneyAfter = fixture.Money.Balance;
            long holdingsAfter = fixture.Holdings.Sequence;

            ShopPurchaseFactV1 replay = fixture.Service.Purchase(command);

            Assert.That(replay.Status, Is.EqualTo(ShopPurchaseStatusV1.ExactDuplicateNoChange));
            Assert.That(replay.OriginalStatus, Is.EqualTo(original.Status));
            Assert.That(fixture.Money.Balance, Is.EqualTo(moneyAfter));
            Assert.That(fixture.Holdings.Sequence, Is.EqualTo(holdingsAfter));
        }

        [Test]
        public void ConflictingDuplicatePurchaseIsRejected()
        {
            Fixture fixture = new Fixture(startingMoney: 10000L);
            ShopInventoryViewV1 inventory = fixture.Open("run.conflict");
            ShopStockEntryV1 entry = inventory.Entries[0];
            ShopPurchaseCommandV1 original = fixture.PurchaseCommand(
                "shop-purchase.conflict",
                inventory,
                entry);
            fixture.Service.Purchase(original);
            ShopPurchaseCommandV1 conflict = ShopPurchaseCommandV1.Create(
                original.TransactionStableId,
                original.RunStableId,
                original.ShopStableId,
                original.StockEntryStableId,
                original.ClaimantStableId,
                original.InventoryFingerprint,
                original.ExpectedPrice + 1L);

            ShopPurchaseFactV1 result = fixture.Service.Purchase(conflict);

            Assert.That(result.Status, Is.EqualTo(ShopPurchaseStatusV1.ConflictingDuplicate));
        }

        [Test]
        public void InsufficientMoneyChangesNoAuthorityOrStockState()
        {
            Fixture fixture = new Fixture(startingMoney: 0L);
            ShopInventoryViewV1 inventory = fixture.Open("run.insufficient");
            ShopStockEntryV1 entry = inventory.Entries[0];

            ShopPurchaseFactV1 result = fixture.Service.Purchase(
                fixture.PurchaseCommand("shop-purchase.insufficient", inventory, entry));
            ShopInventoryViewV1 after;
            fixture.Service.TryGetInventory(inventory.RunStableId, inventory.ShopStableId, out after);

            Assert.That(result.Status, Is.EqualTo(ShopPurchaseStatusV1.InsufficientFunds));
            Assert.That(fixture.Money.Balance, Is.Zero);
            Assert.That(fixture.Money.Sequence, Is.Zero);
            Assert.That(fixture.Holdings.Sequence, Is.Zero);
            Assert.That(after.FindEntry(entry.StockEntryStableId).State,
                Is.EqualTo(ShopStockEntryStateV1.Available));
        }

        [Test]
        public void FailedEquipmentGrantRefundsMoneyAndReleasesStock()
        {
            Fixture fixture = new Fixture(
                startingMoney: 10000L,
                validator: new RejectingEquipmentValidator());
            ShopInventoryViewV1 inventory = fixture.Open("run.refund-on-rejection");
            ShopStockEntryV1 entry = inventory.Entries[0];
            long before = fixture.Money.Balance;

            ShopPurchaseFactV1 result = fixture.Service.Purchase(
                fixture.PurchaseCommand("shop-purchase.refund-on-rejection", inventory, entry));
            ShopInventoryViewV1 after;
            fixture.Service.TryGetInventory(inventory.RunStableId, inventory.ShopStableId, out after);

            Assert.That(result.Status, Is.EqualTo(ShopPurchaseStatusV1.RewardApplicationRejected));
            Assert.That(fixture.Money.Balance, Is.EqualTo(before));
            Assert.That(fixture.Holdings.Sequence, Is.Zero);
            Assert.That(after.FindEntry(entry.StockEntryStableId).State,
                Is.EqualTo(ShopStockEntryStateV1.Available));
        }

        [Test]
        public void RapApplicationRejectionRemainsRetrySafe()
        {
            TransientHoldingsAuthority transient = new TransientHoldingsAuthority();
            Fixture fixture = new Fixture(startingMoney: 10000L, holdingsAuthority: transient);
            ShopInventoryViewV1 inventory = fixture.Open("run.retry-safe");
            ShopStockEntryV1 entry = inventory.Entries[0];
            ShopPurchaseCommandV1 command = fixture.PurchaseCommand(
                "shop-purchase.retry-safe",
                inventory,
                entry);
            long before = fixture.Money.Balance;

            ShopPurchaseFactV1 pending = fixture.Service.Purchase(command);
            ShopPurchaseFactV1 applied = fixture.Service.Purchase(command);

            Assert.That(pending.Status, Is.EqualTo(ShopPurchaseStatusV1.PurchasePending));
            Assert.That(applied.Status, Is.EqualTo(ShopPurchaseStatusV1.Applied));
            Assert.That(fixture.Money.Balance, Is.EqualTo(before - entry.Price));
            Assert.That(transient.ApplyCalls, Is.EqualTo(2));
            Assert.That(transient.ConfirmedApplications, Is.EqualTo(1));
        }

        [Test]
        public void UnknownStockEntryIsRejected()
        {
            Fixture fixture = new Fixture(startingMoney: 10000L);
            ShopInventoryViewV1 inventory = fixture.Open("run.unknown-entry");
            ShopPurchaseCommandV1 command = ShopPurchaseCommandV1.Create(
                Id("shop-purchase.unknown-entry"),
                inventory.RunStableId,
                inventory.ShopStableId,
                Id("shopstock.unknown"),
                Id("player.fixture"),
                inventory.InventoryFingerprint,
                inventory.Entries[0].Price);

            ShopPurchaseFactV1 result = fixture.Service.Purchase(command);

            Assert.That(result.Status, Is.EqualTo(ShopPurchaseStatusV1.UnknownStockEntry));
        }

        [Test]
        public void StaleInventoryFingerprintIsRejected()
        {
            Fixture fixture = new Fixture(startingMoney: 10000L);
            ShopInventoryViewV1 inventory = fixture.Open("run.stale");
            ShopStockEntryV1 entry = inventory.Entries[0];
            ShopPurchaseCommandV1 command = ShopPurchaseCommandV1.Create(
                Id("shop-purchase.stale"),
                inventory.RunStableId,
                inventory.ShopStableId,
                entry.StockEntryStableId,
                Id("player.fixture"),
                ShopCanonicalV1.Fingerprint("stale"),
                entry.Price);

            ShopPurchaseFactV1 result = fixture.Service.Purchase(command);

            Assert.That(result.Status, Is.EqualTo(ShopPurchaseStatusV1.StaleInventoryFingerprint));
            Assert.That(fixture.Money.Sequence, Is.EqualTo(1L));
            Assert.That(fixture.Holdings.Sequence, Is.Zero);
        }

        [Test]
        public void RefreshLimitIsEnforced()
        {
            Fixture fixture = new Fixture(maximumRefreshes: 1);
            ShopInventoryViewV1 first = fixture.Open("run.refresh-limit");
            fixture.Service.Refresh(ShopRefreshCommandV1.Create(
                Id("shop-refresh.limit-first"),
                first.RunStableId,
                first.ShopStableId,
                first.InventoryFingerprint,
                Context(10)));
            ShopInventoryViewV1 current;
            fixture.Service.TryGetInventory(first.RunStableId, first.ShopStableId, out current);

            ShopRefreshFactV1 second = fixture.Service.Refresh(
                ShopRefreshCommandV1.Create(
                    Id("shop-refresh.limit-second"),
                    current.RunStableId,
                    current.ShopStableId,
                    current.InventoryFingerprint,
                    Context(10)));

            Assert.That(second.Status, Is.EqualTo(ShopRefreshStatusV1.LimitReached));
            Assert.That(second.CurrentRefreshOrdinal, Is.EqualTo(1));
        }

        [Test]
        public void RejectedRefreshRetainsPreviousInventory()
        {
            Fixture fixture = new Fixture(maximumRefreshes: 2, baseLockCapacity: 1);
            ShopInventoryViewV1 before = fixture.Open("run.refresh-rejected");

            ShopRefreshFactV1 rejected = fixture.Service.Refresh(
                ShopRefreshCommandV1.Create(
                    Id("shop-refresh.rejected"),
                    before.RunStableId,
                    before.ShopStableId,
                    before.InventoryFingerprint,
                    Context(10),
                    new[] { Id("shopstock.unknown-lock") }));
            ShopInventoryViewV1 after;
            fixture.Service.TryGetInventory(before.RunStableId, before.ShopStableId, out after);

            Assert.That(rejected.Status, Is.EqualTo(ShopRefreshStatusV1.UnknownLockedEntry));
            Assert.That(after.RefreshOrdinal, Is.EqualTo(before.RefreshOrdinal));
            Assert.That(after.InventoryFingerprint, Is.EqualTo(before.InventoryFingerprint));
        }

        [Test]
        public void RealGeneratorMoneyHoldingsAndRapIntegrationPasses()
        {
            Fixture fixture = new Fixture(startingMoney: 10000L, inventorySize: 4);
            ShopInventoryViewV1 inventory = fixture.Open("run.real-integration");
            ShopStockEntryV1 entry = inventory.Entries[2];
            long before = fixture.Money.Balance;

            ShopPurchaseFactV1 fact = fixture.Service.Purchase(
                fixture.PurchaseCommand("shop-purchase.real-integration", inventory, entry));

            UniqueHoldingSnapshotV1 holding;
            Assert.That(fact.Status, Is.EqualTo(ShopPurchaseStatusV1.Applied));
            Assert.That(fixture.Money.Balance, Is.EqualTo(before - entry.Price));
            Assert.That(fixture.Holdings.TryGetUnique(entry.Equipment.InstanceId, out holding), Is.True);
            Assert.That(holding.EquipmentInstance.Fingerprint, Is.EqualTo(entry.Equipment.Fingerprint));
        }

    }
}
