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
    public sealed partial class ShopLiveActionsTests
    {
        [Test]
        public void DuplicatePurchaseIsNoChangeReplay()
        {
            Fixture fixture = new Fixture(startingMoney: 10000L);
            ShopInventoryView inventory = fixture.Open("run.duplicate");
            ShopPurchaseCommand command = fixture.PurchaseCommand(
                "shop-purchase.duplicate",
                inventory,
                inventory.Entries[0]);
            ShopPurchaseFact original = fixture.Service.Purchase(command);
            long moneyAfter = fixture.Money.Balance;
            long holdingsAfter = fixture.Holdings.Sequence;

            ShopPurchaseFact replay = fixture.Service.Purchase(command);

            Assert.That(replay.Status, Is.EqualTo(ShopPurchaseStatus.ExactDuplicateNoChange));
            Assert.That(replay.OriginalStatus, Is.EqualTo(original.Status));
            Assert.That(fixture.Money.Balance, Is.EqualTo(moneyAfter));
            Assert.That(fixture.Holdings.Sequence, Is.EqualTo(holdingsAfter));
        }

        [Test]
        public void ConflictingDuplicatePurchaseIsRejected()
        {
            Fixture fixture = new Fixture(startingMoney: 10000L);
            ShopInventoryView inventory = fixture.Open("run.conflict");
            ShopStockEntry entry = inventory.Entries[0];
            ShopPurchaseCommand original = fixture.PurchaseCommand(
                "shop-purchase.conflict",
                inventory,
                entry);
            fixture.Service.Purchase(original);
            ShopPurchaseCommand conflict = ShopPurchaseCommand.Create(
                original.TransactionStableId,
                original.RunStableId,
                original.ShopStableId,
                original.StockEntryStableId,
                original.ClaimantStableId,
                original.InventoryFingerprint,
                original.ExpectedPrice + 1L);

            ShopPurchaseFact result = fixture.Service.Purchase(conflict);

            Assert.That(result.Status, Is.EqualTo(ShopPurchaseStatus.ConflictingDuplicate));
        }

        [Test]
        public void InsufficientMoneyChangesNoAuthorityOrStockState()
        {
            Fixture fixture = new Fixture(startingMoney: 0L);
            ShopInventoryView inventory = fixture.Open("run.insufficient");
            ShopStockEntry entry = inventory.Entries[0];

            ShopPurchaseFact result = fixture.Service.Purchase(
                fixture.PurchaseCommand("shop-purchase.insufficient", inventory, entry));
            ShopInventoryView after;
            fixture.Service.TryGetInventory(inventory.RunStableId, inventory.ShopStableId, out after);

            Assert.That(result.Status, Is.EqualTo(ShopPurchaseStatus.InsufficientFunds));
            Assert.That(fixture.Money.Balance, Is.Zero);
            Assert.That(fixture.Money.Sequence, Is.Zero);
            Assert.That(fixture.Holdings.Sequence, Is.Zero);
            Assert.That(after.FindEntry(entry.StockEntryStableId).State,
                Is.EqualTo(ShopStockEntryState.Available));
        }

        [Test]
        public void FailedEquipmentGrantRefundsMoneyAndReleasesStock()
        {
            Fixture fixture = new Fixture(
                startingMoney: 10000L,
                validator: new RejectingEquipmentValidator());
            ShopInventoryView inventory = fixture.Open("run.refund-on-rejection");
            ShopStockEntry entry = inventory.Entries[0];
            long before = fixture.Money.Balance;

            ShopPurchaseFact result = fixture.Service.Purchase(
                fixture.PurchaseCommand("shop-purchase.refund-on-rejection", inventory, entry));
            ShopInventoryView after;
            fixture.Service.TryGetInventory(inventory.RunStableId, inventory.ShopStableId, out after);

            Assert.That(result.Status, Is.EqualTo(ShopPurchaseStatus.RewardApplicationRejected));
            Assert.That(fixture.Money.Balance, Is.EqualTo(before));
            Assert.That(fixture.Holdings.Sequence, Is.Zero);
            Assert.That(after.FindEntry(entry.StockEntryStableId).State,
                Is.EqualTo(ShopStockEntryState.Available));
        }

        [Test]
        public void RapApplicationRejectionRemainsRetrySafe()
        {
            TransientHoldingsState transient = new TransientHoldingsState();
            Fixture fixture = new Fixture(startingMoney: 10000L, holdingsAuthority: transient);
            ShopInventoryView inventory = fixture.Open("run.retry-safe");
            ShopStockEntry entry = inventory.Entries[0];
            ShopPurchaseCommand command = fixture.PurchaseCommand(
                "shop-purchase.retry-safe",
                inventory,
                entry);
            long before = fixture.Money.Balance;

            ShopPurchaseFact pending = fixture.Service.Purchase(command);
            ShopPurchaseFact applied = fixture.Service.Purchase(command);

            Assert.That(pending.Status, Is.EqualTo(ShopPurchaseStatus.PurchasePending));
            Assert.That(applied.Status, Is.EqualTo(ShopPurchaseStatus.Applied));
            Assert.That(fixture.Money.Balance, Is.EqualTo(before - entry.Price));
            Assert.That(transient.ApplyCalls, Is.EqualTo(2));
            Assert.That(transient.ConfirmedApplications, Is.EqualTo(1));
        }

        [Test]
        public void UnknownStockEntryIsRejected()
        {
            Fixture fixture = new Fixture(startingMoney: 10000L);
            ShopInventoryView inventory = fixture.Open("run.unknown-entry");
            ShopPurchaseCommand command = ShopPurchaseCommand.Create(
                Id("shop-purchase.unknown-entry"),
                inventory.RunStableId,
                inventory.ShopStableId,
                Id("shopstock.unknown"),
                Id("player.fixture"),
                inventory.InventoryFingerprint,
                inventory.Entries[0].Price);

            ShopPurchaseFact result = fixture.Service.Purchase(command);

            Assert.That(result.Status, Is.EqualTo(ShopPurchaseStatus.UnknownStockEntry));
        }

        [Test]
        public void StaleInventoryFingerprintIsRejected()
        {
            Fixture fixture = new Fixture(startingMoney: 10000L);
            ShopInventoryView inventory = fixture.Open("run.stale");
            ShopStockEntry entry = inventory.Entries[0];
            ShopPurchaseCommand command = ShopPurchaseCommand.Create(
                Id("shop-purchase.stale"),
                inventory.RunStableId,
                inventory.ShopStableId,
                entry.StockEntryStableId,
                Id("player.fixture"),
                Shop.Fingerprint("stale"),
                entry.Price);

            ShopPurchaseFact result = fixture.Service.Purchase(command);

            Assert.That(result.Status, Is.EqualTo(ShopPurchaseStatus.StaleInventoryFingerprint));
            Assert.That(fixture.Money.Sequence, Is.EqualTo(1L));
            Assert.That(fixture.Holdings.Sequence, Is.Zero);
        }

        [Test]
        public void RefreshLimitIsEnforced()
        {
            Fixture fixture = new Fixture(maximumRefreshes: 1);
            ShopInventoryView first = fixture.Open("run.refresh-limit");
            fixture.Service.Refresh(ShopRefreshCommand.Create(
                Id("shop-refresh.limit-first"),
                first.RunStableId,
                first.ShopStableId,
                first.InventoryFingerprint,
                Context(10)));
            ShopInventoryView current;
            fixture.Service.TryGetInventory(first.RunStableId, first.ShopStableId, out current);

            ShopRefreshFact second = fixture.Service.Refresh(
                ShopRefreshCommand.Create(
                    Id("shop-refresh.limit-second"),
                    current.RunStableId,
                    current.ShopStableId,
                    current.InventoryFingerprint,
                    Context(10)));

            Assert.That(second.Status, Is.EqualTo(ShopRefreshStatus.LimitReached));
            Assert.That(second.CurrentRefreshOrdinal, Is.EqualTo(1));
        }

        [Test]
        public void RejectedRefreshRetainsPreviousInventory()
        {
            Fixture fixture = new Fixture(maximumRefreshes: 2, baseLockCapacity: 1);
            ShopInventoryView before = fixture.Open("run.refresh-rejected");

            ShopRefreshFact rejected = fixture.Service.Refresh(
                ShopRefreshCommand.Create(
                    Id("shop-refresh.rejected"),
                    before.RunStableId,
                    before.ShopStableId,
                    before.InventoryFingerprint,
                    Context(10),
                    new[] { Id("shopstock.unknown-lock") }));
            ShopInventoryView after;
            fixture.Service.TryGetInventory(before.RunStableId, before.ShopStableId, out after);

            Assert.That(rejected.Status, Is.EqualTo(ShopRefreshStatus.UnknownLockedEntry));
            Assert.That(after.RefreshOrdinal, Is.EqualTo(before.RefreshOrdinal));
            Assert.That(after.InventoryFingerprint, Is.EqualTo(before.InventoryFingerprint));
        }

        [Test]
        public void RealGeneratorMoneyHoldingsAndRapIntegrationPasses()
        {
            Fixture fixture = new Fixture(startingMoney: 10000L, inventorySize: 4);
            ShopInventoryView inventory = fixture.Open("run.real-integration");
            ShopStockEntry entry = inventory.Entries[2];
            long before = fixture.Money.Balance;

            ShopPurchaseFact fact = fixture.Service.Purchase(
                fixture.PurchaseCommand("shop-purchase.real-integration", inventory, entry));

            UniqueHoldingSnapshot holding;
            Assert.That(fact.Status, Is.EqualTo(ShopPurchaseStatus.Applied));
            Assert.That(fixture.Money.Balance, Is.EqualTo(before - entry.Price));
            Assert.That(fixture.Holdings.TryGetUnique(entry.Equipment.InstanceId, out holding), Is.True);
            Assert.That(holding.EquipmentInstance.Fingerprint, Is.EqualTo(entry.Equipment.Fingerprint));
        }

    }
}
