using System;
using NUnit.Framework;
using ShooterMover.Application.Flow.Game;
using ShooterMover.Application.Persistence.SaveParts;
using ShooterMover.Application.Rewards.Drops;
using ShooterMover.Application.Rewards.Strongboxes;
using ShooterMover.Application.Shops;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Economy.Money;
using ShooterMover.Domain.Rewards.Drops;
using ShooterMover.Domain.Shops;

namespace ShooterMover.Tests.EditMode.Shops
{
    public sealed class ShopRefreshAndReceiptTests
    {
        [Test]
        public void SixHourWindowsUseFixedUtcBoundaries()
        {
            ShopRefreshWindow before = ShopRefreshSchedule.Resolve(
                new DateTime(2026, 8, 1, 5, 59, 59, DateTimeKind.Utc));
            Assert.That(
                before.StartsAtUtc,
                Is.EqualTo(new DateTime(
                    2026, 8, 1, 0, 0, 0, DateTimeKind.Utc)));
            Assert.That(
                before.RefreshesAtUtc,
                Is.EqualTo(new DateTime(
                    2026, 8, 1, 6, 0, 0, DateTimeKind.Utc)));

            ShopRefreshWindow boundary = ShopRefreshSchedule.Resolve(
                new DateTime(2026, 8, 1, 6, 0, 0, DateTimeKind.Utc));
            Assert.That(
                boundary.StartsAtUtc,
                Is.EqualTo(new DateTime(
                    2026, 8, 1, 6, 0, 0, DateTimeKind.Utc)));
            Assert.That(
                boundary.RefreshesAtUtc,
                Is.EqualTo(new DateTime(
                    2026, 8, 1, 12, 0, 0, DateTimeKind.Utc)));
            Assert.That(boundary.Ordinal, Is.EqualTo(before.Ordinal + 1L));
        }

        [Test]
        public void ProductionShopGeneratesExactlySixOffers()
        {
            CharacterLiveGraph graph = CreateGraph(
                "character.shop-six-offers");
            ShopInventoryView stock = Open(graph, Window(7));
            Assert.That(stock.Entries, Has.Count.EqualTo(6));
        }

        [Test]
        public void ReopeningOneWindowReturnsSameEquipmentInstances()
        {
            CharacterLiveGraph graph = CreateGraph(
                "character.shop-reopen");
            ShopRefreshWindow window = Window(7);
            ShopInventoryView first = Open(graph, window);
            ShopInventoryView reopened = Open(graph, window);

            Assert.That(
                reopened.InventoryFingerprint,
                Is.EqualTo(first.InventoryFingerprint));
            for (int index = 0; index < first.Entries.Count; index++)
            {
                Assert.That(
                    reopened.Entries[index].Equipment.InstanceId,
                    Is.EqualTo(first.Entries[index].Equipment.InstanceId));
            }
        }

        [Test]
        public void NextWindowChangesStockIdAndDeterministicStock()
        {
            CharacterLiveGraph graph = CreateGraph(
                "character.shop-next-window");
            ShopRefreshWindow firstWindow = Window(7);
            ShopRefreshWindow nextWindow = Window(13);
            StableId firstId = StockId(graph, firstWindow);
            StableId nextId = StockId(graph, nextWindow);
            ShopInventoryView first = Open(graph, firstWindow);
            ShopInventoryView next = Open(graph, nextWindow);

            Assert.That(nextId, Is.Not.EqualTo(firstId));
            Assert.That(first.RefreshOrdinal, Is.Zero);
            Assert.That(next.RefreshOrdinal, Is.Zero);
            Assert.That(
                next.InventoryFingerprint,
                Is.Not.EqualTo(first.InventoryFingerprint));
            for (int index = 0; index < first.Entries.Count; index++)
            {
                Assert.That(
                    next.Entries[index].Equipment.InstanceId,
                    Is.Not.EqualTo(first.Entries[index].Equipment.InstanceId));
            }
        }

        [Test]
        public void DifferentShopsDoNotShareStockIdentity()
        {
            StableId character = Id("character.shop-id-test");
            ShopRefreshWindow window = Window(7);
            StableId weapons = window.StockId(
                character,
                Id("shop.hub-weapons"));
            StableId armor = window.StockId(
                character,
                Id("shop.hub-armor"));
            Assert.That(weapons, Is.Not.EqualTo(armor));
        }

        [Test]
        public void SuccessfulPurchaseRemainsSoldAfterReceiptImport()
        {
            StableId character = Id("character.shop-receipt-restore");
            ShopRefreshWindow window = Window(7);
            CharacterLiveGraph source = CreateGraph(character);
            ShopInventoryView stock = Open(source, window);
            ShopStockEntry entry = stock.Entries[0];
            GrantMoney(source, "restore", 100000L);

            ShopPurchaseCommand command = Purchase(
                source,
                stock,
                entry,
                "shop-purchase.restore");
            ShopPurchaseFact purchase = source.Shop.Authority.Purchase(command);
            Assert.That(purchase.Status, Is.EqualTo(ShopPurchaseStatus.Applied));

            ShopReceiptSnapshot snapshot =
                source.Shop.Receipts.ExportSnapshot();
            CharacterLiveGraph restored = CreateGraph(character);
            string rejection;
            Assert.That(
                restored.Shop.Receipts.TryImportSnapshot(
                    snapshot,
                    out rejection),
                Is.True,
                rejection);

            ShopInventoryView reopened = Open(restored, window);
            ShopStockEntry restoredEntry = reopened.FindEntry(
                entry.StockEntryStableId);
            Assert.That(restoredEntry, Is.Not.Null);
            Assert.That(
                restoredEntry.State,
                Is.EqualTo(ShopStockEntryState.SoldOut));
            Assert.That(
                restoredEntry.PurchaseTransactionStableId,
                Is.EqualTo(command.TransactionStableId));
        }

        [Test]
        public void FailedPurchaseCreatesNoReceipt()
        {
            CharacterLiveGraph graph = CreateGraph(
                "character.shop-failed-purchase");
            ShopInventoryView stock = Open(graph, Window(7));
            ShopPurchaseFact fact = graph.Shop.Authority.Purchase(
                Purchase(
                    graph,
                    stock,
                    stock.Entries[0],
                    "shop-purchase.no-money"));

            Assert.That(
                fact.Status,
                Is.EqualTo(ShopPurchaseStatus.InsufficientFunds));
            Assert.That(
                graph.Shop.Receipts.ExportSnapshot().Receipts,
                Is.Empty);
        }

        [Test]
        public void ExactPurchaseReplayIsIdempotent()
        {
            CharacterLiveGraph graph = CreateGraph(
                "character.shop-exact-replay");
            ShopInventoryView stock = Open(graph, Window(7));
            ShopStockEntry entry = stock.Entries[0];
            GrantMoney(graph, "replay", 100000L);
            ShopPurchaseCommand command = Purchase(
                graph,
                stock,
                entry,
                "shop-purchase.replay");
            long before = graph.MoneyWallet.Balance;

            ShopPurchaseFact first = graph.Shop.Authority.Purchase(command);
            ShopPurchaseFact replay = graph.Shop.Authority.Purchase(command);

            Assert.That(first.Status, Is.EqualTo(ShopPurchaseStatus.Applied));
            Assert.That(
                replay.Status,
                Is.EqualTo(ShopPurchaseStatus.ExactDuplicateNoChange));
            Assert.That(
                replay.OriginalStatus,
                Is.EqualTo(ShopPurchaseStatus.Applied));
            Assert.That(
                graph.MoneyWallet.Balance,
                Is.EqualTo(before - entry.Price));
            Assert.That(
                graph.Shop.Receipts.ExportSnapshot().Receipts,
                Has.Count.EqualTo(1));
        }

        [Test]
        public void ConflictingReceiptIsControlledAndCardRemainsSold()
        {
            CharacterLiveGraph graph = CreateGraph(
                "character.shop-receipt-conflict");
            ShopRefreshWindow window = Window(7);
            ShopInventoryView stock = Open(graph, window);
            ShopStockEntry entry = stock.Entries[0];
            StableId conflictingPurchase =
                Id("shop-purchase.receipt-conflict-existing");
            string rejection;
            Assert.That(
                graph.Shop.Receipts.TryRecord(
                    entry.StockEntryStableId,
                    conflictingPurchase,
                    out rejection),
                Is.True,
                rejection);
            GrantMoney(graph, "conflict", 100000L);

            ShopPurchaseCommand command = Purchase(
                graph,
                stock,
                entry,
                "shop-purchase.receipt-conflict-applied");
            ShopPurchaseFact fact = graph.Shop.Authority.Purchase(command);
            ShopInventoryView reopened = Open(graph, window);
            ShopStockEntry sold = reopened.FindEntry(
                entry.StockEntryStableId);

            Assert.That(fact.Status, Is.EqualTo(ShopPurchaseStatus.Applied));
            Assert.That(
                fact.RejectionCode,
                Is.EqualTo("shop-purchase-receipt-conflict"));
            Assert.That(sold.State, Is.EqualTo(ShopStockEntryState.SoldOut));
            Assert.That(
                sold.PurchaseTransactionStableId,
                Is.EqualTo(command.TransactionStableId));
            StableId recorded;
            Assert.That(
                graph.Shop.Receipts.TryGet(
                    entry.StockEntryStableId,
                    out recorded),
                Is.True);
            Assert.That(recorded, Is.EqualTo(conflictingPurchase));
        }

        [Test]
        public void OfferAugmentsBecomeDurableOnlyAfterPurchase()
        {
            CharacterLiveGraph graph = CreateGraph(
                "character.shop-augment-purchase");
            ShopInventoryView stock = Open(graph, Window(7));
            ShopStockEntry entry = stock.Entries[0];
            GeneratedEquipmentAugmentSignature offer;
            bool committed;

            Assert.That(
                graph.Shop.OfferAugments.TryGetStagedOrCommitted(
                    entry.Equipment.InstanceId,
                    out offer,
                    out committed),
                Is.True);
            Assert.That(committed, Is.False);
            Assert.That(
                graph.Augments.TryGet(
                    entry.Equipment.InstanceId,
                    out offer),
                Is.False);

            GrantMoney(graph, "augment", 100000L);
            ShopPurchaseFact fact = graph.Shop.Authority.Purchase(
                Purchase(
                    graph,
                    stock,
                    entry,
                    "shop-purchase.augment"));

            Assert.That(fact.Status, Is.EqualTo(ShopPurchaseStatus.Applied));
            Assert.That(
                graph.Augments.TryGet(
                    entry.Equipment.InstanceId,
                    out offer),
                Is.True);
        }

        [Test]
        public void PurchasedOfferPassesCharacterSaveValidation()
        {
            CharacterLiveGraph graph = CreateGraph(
                "character.shop-save-validation");
            ShopInventoryView stock = Open(graph, Window(7));
            ShopStockEntry entry = stock.Entries[0];
            GrantMoney(graph, "save-validation", 100000L);

            ShopPurchaseFact purchase = graph.Shop.Authority.Purchase(
                Purchase(
                    graph,
                    stock,
                    entry,
                    "shop-purchase.save-validation"));
            Assert.That(
                purchase.Status,
                Is.EqualTo(ShopPurchaseStatus.Applied));

            GeneratedEquipmentAugmentSignature signature;
            Assert.That(
                graph.Augments.TryGet(
                    entry.Equipment.InstanceId,
                    out signature),
                Is.True);
            Assert.That(
                signature.SourceStrongboxInstanceStableId,
                Is.EqualTo(entry.StockEntryStableId));

            var saved = graph.Character;
            for (int index = 0;
                 index < graph.SaveAdapters.Count;
                 index++)
            {
                saved = saved.WithComponent(
                    graph.SaveAdapters[index].ExportComponent());
            }

            var validation = GameSaveRules.ValidateCharacter(saved);
            Assert.That(
                validation.Succeeded,
                Is.True,
                validation.RejectionCode);
        }

        [Test]
        public void NewWindowClearsObsoleteOfferAugments()
        {
            CharacterLiveGraph graph = CreateGraph(
                "character.shop-augment-window");
            ShopInventoryView first = Open(graph, Window(7));
            StableId oldEquipmentId = first.Entries[0].Equipment.InstanceId;
            GeneratedEquipmentAugmentSignature signature;
            bool committed;
            Assert.That(
                graph.Shop.OfferAugments.TryGetStagedOrCommitted(
                    oldEquipmentId,
                    out signature,
                    out committed),
                Is.True);

            ShopInventoryView next = Open(graph, Window(13));

            Assert.That(
                graph.Shop.OfferAugments.TryGetStagedOrCommitted(
                    oldEquipmentId,
                    out signature,
                    out committed),
                Is.False);
            Assert.That(
                graph.Shop.OfferAugments.TryGetStagedOrCommitted(
                    next.Entries[0].Equipment.InstanceId,
                    out signature,
                    out committed),
                Is.True);
        }

        [Test]
        public void ShopTierProfileAuthorsFiftyFivePercentSteel()
        {
            StrongboxTierSelectionProfile profile =
                StrongboxTierSelectionCatalog.Get(
                    StrongboxTierSelectionCatalog.ShopSourceProfileId);
            StableId steel = StrongboxCatalog.GetByNumber(1).TierStableId;
            ulong total = 0UL;
            ulong steelWeight = 0UL;
            for (int index = 0;
                 index < profile.BaseWeights.Count;
                 index++)
            {
                StrongboxTierWeight weight = profile.BaseWeights[index];
                total += weight.Weight;
                if (weight.TierStableId == steel)
                {
                    steelWeight = weight.Weight;
                }
            }

            Assert.That(total, Is.EqualTo(1000000UL));
            Assert.That(steelWeight, Is.EqualTo(550000UL));
        }

        private static ShopRefreshWindow Window(int hour)
        {
            return ShopRefreshSchedule.Resolve(
                new DateTime(2026, 8, 1, hour, 0, 0, DateTimeKind.Utc));
        }

        private static CharacterLiveGraph CreateGraph(string characterId)
        {
            return CreateGraph(Id(characterId));
        }

        private static CharacterLiveGraph CreateGraph(StableId characterId)
        {
            return (CharacterLiveGraph)CharacterLiveGraphFactory
                .CreateVerticalSliceDefaults()
                .CreateStarter(
                    0,
                    characterId,
                    Id("character-class.striker"),
                    "Shop Test",
                    null);
        }

        private static StableId StockId(
            CharacterLiveGraph graph,
            ShopRefreshWindow window)
        {
            return window.StockId(
                graph.Character.CharacterInstanceStableId,
                graph.Shop.Definition.ShopStableId);
        }

        private static ShopInventoryView Open(
            CharacterLiveGraph graph,
            ShopRefreshWindow window)
        {
            ShopInventoryOpenResult result = graph.Shop.Authority.Open(
                StockId(graph, window),
                graph.Shop.Definition,
                graph.LoadoutRuntime.EquipmentCatalog,
                graph.ExperienceAuthority.CurrentContext);
            Assert.That(result.Succeeded, Is.True, result.RejectionCode);
            Assert.That(result.Inventory, Is.Not.Null);
            return result.Inventory;
        }

        private static ShopPurchaseCommand Purchase(
            CharacterLiveGraph graph,
            ShopInventoryView stock,
            ShopStockEntry entry,
            string purchaseId)
        {
            return ShopPurchaseCommand.Create(
                Id(purchaseId),
                stock.RunStableId,
                stock.ShopStableId,
                entry.StockEntryStableId,
                graph.Character.CharacterInstanceStableId,
                stock.InventoryFingerprint,
                entry.Price);
        }

        private static void GrantMoney(
            CharacterLiveGraph graph,
            string suffix,
            long amount)
        {
            MoneyWalletChangeFact fact = graph.MoneyWallet.Grant(
                Id("shop-money-grant." + suffix),
                Id("shop-money-operation." + suffix),
                amount);
            Assert.That(
                fact.Status == MoneyWalletTransactionStatus.Applied
                    || fact.Status
                        == MoneyWalletTransactionStatus.DuplicateNoChange,
                Is.True,
                fact.RejectionCode);
        }

        private static StableId Id(string value)
        {
            return StableId.Parse(value);
        }
    }
}
