using System;
using System.Collections.Generic;
using System.Globalization;
using ShooterMover.Application.Economy.Money;
using ShooterMover.Application.Rewards.Application;
using ShooterMover.Application.Rewards.Generation;
using ShooterMover.Contracts.Rewards;
using ShooterMover.Contracts.Rewards.Application;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Economy.Money;
using ShooterMover.Domain.Equipment;
using ShooterMover.Domain.Progression.Context;
using ShooterMover.Domain.Rewards.Generation;
using ShooterMover.Domain.Rewards.Model;
using ShooterMover.Domain.Shops;

namespace ShooterMover.Application.Shops
{
    public sealed partial class ShopRuntimeServiceV1
    {
        private readonly object sync = new object();
        private readonly RewardGenerationServiceV1 generator;
        private readonly MoneyWalletService money;
        private readonly RewardApplicationServiceV1 rewardApplication;
        private readonly StableId scrapAuthorityStableId;
        private readonly StableId holdingsAuthorityStableId;
        private readonly IShopLockCapacityExtensionV1 lockCapacityExtension;
        private readonly Dictionary<string, ShopState> shops = new Dictionary<string, ShopState>();
        private readonly Dictionary<StableId, PurchaseRecord> purchases = new Dictionary<StableId, PurchaseRecord>();
        private readonly Dictionary<StableId, RefreshRecord> refreshes = new Dictionary<StableId, RefreshRecord>();

        public ShopRuntimeServiceV1(
            RewardGenerationServiceV1 generator,
            MoneyWalletService money,
            RewardApplicationServiceV1 rewardApplication,
            StableId scrapAuthorityStableId,
            StableId holdingsAuthorityStableId,
            IShopLockCapacityExtensionV1 lockCapacityExtension = null)
        {
            this.generator = generator ?? throw new ArgumentNullException(nameof(generator));
            this.money = money ?? throw new ArgumentNullException(nameof(money));
            this.rewardApplication = rewardApplication
                ?? throw new ArgumentNullException(nameof(rewardApplication));
            this.scrapAuthorityStableId = scrapAuthorityStableId
                ?? throw new ArgumentNullException(nameof(scrapAuthorityStableId));
            this.holdingsAuthorityStableId = holdingsAuthorityStableId
                ?? throw new ArgumentNullException(nameof(holdingsAuthorityStableId));
            this.lockCapacityExtension = lockCapacityExtension;
        }

        public ShopInventoryOpenResultV1 Open(
            StableId runStableId,
            ShopDefinitionV1 definition,
            EquipmentCatalog catalog,
            ProgressionContext context)
        {
            lock (sync)
            {
                if (runStableId == null || definition == null || catalog == null || context == null)
                {
                    return new ShopInventoryOpenResultV1(
                        ShopInventoryOpenStatusV1.InvalidRequest,
                        null,
                        "shop-open-input-null");
                }

                string key = Key(runStableId, definition.ShopStableId);
                ShopState existing;
                if (shops.TryGetValue(key, out existing))
                {
                    if (!string.Equals(
                        existing.DefinitionFingerprint,
                        definition.Fingerprint,
                        StringComparison.Ordinal))
                    {
                        return new ShopInventoryOpenResultV1(
                            ShopInventoryOpenStatusV1.DefinitionMismatch,
                            existing.ToView(),
                            "shop-definition-fingerprint-mismatch");
                    }

                    if (!string.IsNullOrEmpty(existing.CatalogFingerprint)
                        && !string.Equals(
                            existing.CatalogFingerprint,
                            catalog.Fingerprint,
                            StringComparison.Ordinal))
                    {
                        return new ShopInventoryOpenResultV1(
                            ShopInventoryOpenStatusV1.SnapshotBindingRejected,
                            existing.ToView(),
                            "shop-catalog-fingerprint-mismatch");
                    }

                    if (!existing.CanBind(catalog))
                    {
                        return new ShopInventoryOpenResultV1(
                            ShopInventoryOpenStatusV1.SnapshotBindingRejected,
                            existing.ToView(),
                            "shop-snapshot-equipment-invalid-for-catalog");
                    }

                    existing.Bind(definition, catalog);
                    return new ShopInventoryOpenResultV1(
                        ShopInventoryOpenStatusV1.ExistingNoChange,
                        existing.ToView(),
                        null);
                }

                List<ShopStockEntryV1> entries;
                ulong seed;
                string rejection;
                if (!TryGenerateInventory(
                    runStableId,
                    definition,
                    catalog,
                    context,
                    0,
                    new List<ShopStockEntryV1>(),
                    out seed,
                    out entries,
                    out rejection))
                {
                    return new ShopInventoryOpenResultV1(
                        ShopInventoryOpenStatusV1.GenerationRejected,
                        null,
                        rejection);
                }

                ShopState created = ShopState.Create(
                    runStableId,
                    definition,
                    catalog,
                    context,
                    context,
                    0,
                    seed,
                    entries);
                shops.Add(key, created);
                return new ShopInventoryOpenResultV1(
                    ShopInventoryOpenStatusV1.Generated,
                    created.ToView(),
                    null);
            }
        }

        public ShopPurchaseFactV1 Purchase(ShopPurchaseCommandV1 command)
        {
            lock (sync)
            {
                if (command == null)
                {
                    return new ShopPurchaseFactV1(
                        null,
                        null,
                        ShopPurchaseStatusV1.InvalidRequest,
                        ShopPurchaseStatusV1.InvalidRequest,
                        null,
                        0L,
                        money.Balance,
                        money.Balance,
                        false,
                        "shop-purchase-command-null");
                }

                PurchaseRecord prior;
                if (purchases.TryGetValue(command.TransactionStableId, out prior))
                {
                    if (!string.Equals(prior.Command.Fingerprint, command.Fingerprint, StringComparison.Ordinal))
                    {
                        return prior.Fact.AsConflict();
                    }

                    if (prior.Fact.OriginalStatus == ShopPurchaseStatusV1.PurchasePending
                        || prior.Fact.OriginalStatus == ShopPurchaseStatusV1.CompensationPending)
                    {
                        return ResumePendingPurchase(prior);
                    }

                    return prior.Fact.AsExactDuplicate();
                }

                string key = Key(command.RunStableId, command.ShopStableId);
                ShopState state;
                if (!shops.TryGetValue(key, out state) || !state.IsBound)
                {
                    return RecordTerminal(command, null, ShopPurchaseStatusV1.UnknownShop, 0L,
                        money.Balance, money.Balance, false, "shop-runtime-unknown-or-unbound");
                }

                if (!string.Equals(
                    state.InventoryFingerprint,
                    command.InventoryFingerprint,
                    StringComparison.Ordinal))
                {
                    return RecordTerminal(command, null, ShopPurchaseStatusV1.StaleInventoryFingerprint, 0L,
                        money.Balance, money.Balance, false, "shop-inventory-fingerprint-stale");
                }

                ShopStockEntryV1 entry = state.FindEntry(command.StockEntryStableId);
                if (entry == null)
                {
                    return RecordTerminal(command, null, ShopPurchaseStatusV1.UnknownStockEntry, 0L,
                        money.Balance, money.Balance, false, "shop-stock-entry-unknown");
                }

                if (entry.State != ShopStockEntryStateV1.Available)
                {
                    return RecordTerminal(command, entry, ShopPurchaseStatusV1.SoldOut, entry.Price,
                        money.Balance, money.Balance, false,
                        entry.State == ShopStockEntryStateV1.SoldOut
                            ? "shop-stock-entry-sold-out"
                            : "shop-stock-entry-purchase-pending");
                }

                if (entry.Price != command.ExpectedPrice)
                {
                    return RecordTerminal(command, entry, ShopPurchaseStatusV1.PriceMismatch, entry.Price,
                        money.Balance, money.Balance, false, "shop-price-mismatch");
                }

                long balanceBefore = money.Balance;
                long moneySequence = money.Sequence;
                if (balanceBefore < entry.Price)
                {
                    return RecordTerminal(command, entry, ShopPurchaseStatusV1.InsufficientFunds,
                        entry.Price, balanceBefore, balanceBefore, false, "shop-insufficient-money");
                }

                state.SetEntry(entry.WithPurchaseState(
                    ShopStockEntryStateV1.PurchasePending,
                    command.TransactionStableId));

                RewardCommitCommandV1 commit = BuildCommit(command, state, entry);
                RewardApplicationResultV1 committed = rewardApplication.Commit(commit);
                if (!IsCommitAccepted(committed.Status))
                {
                    state.SetEntry(entry);
                    return RecordTerminal(command, entry, ShopPurchaseStatusV1.RewardApplicationRejected,
                        entry.Price, balanceBefore, money.Balance, false,
                        committed.RejectionCode ?? "shop-rap-commit-rejected");
                }

                MoneyWalletChangeFact spend = money.Spend(
                    SpendTransaction(command.TransactionStableId),
                    SpendOperation(command.TransactionStableId),
                    entry.Price,
                    moneySequence);
                if (!IsMoneyApplied(spend))
                {
                    state.SetEntry(entry);
                    ShopPurchaseStatusV1 status = spend.Status == MoneyWalletTransactionStatus.InsufficientFunds
                        ? ShopPurchaseStatusV1.InsufficientFunds
                        : ShopPurchaseStatusV1.InvalidRequest;
                    return RecordTerminal(command, entry, status, entry.Price,
                        balanceBefore, money.Balance, false,
                        spend.RejectionCode ?? "shop-money-spend-rejected");
                }

                RewardClaimCommandV1 claim = BuildClaim(command, commit);
                RewardApplicationResultV1 claimed = rewardApplication.Claim(claim);
                if (IsRewardApplied(claimed.Status))
                {
                    state.SetEntry(entry.WithPurchaseState(
                        ShopStockEntryStateV1.SoldOut,
                        command.TransactionStableId));
                    return RecordTerminal(command, entry, ShopPurchaseStatusV1.Applied,
                        entry.Price, balanceBefore, money.Balance, true, null);
                }

                if (claimed.Status == RewardApplicationResultStatusV1.ClaimedPendingApplication)
                {
                    ShopPurchaseFactV1 pending = new ShopPurchaseFactV1(
                        command.TransactionStableId,
                        command.Fingerprint,
                        ShopPurchaseStatusV1.PurchasePending,
                        ShopPurchaseStatusV1.PurchasePending,
                        entry.StockEntryStableId,
                        entry.Price,
                        balanceBefore,
                        money.Balance,
                        false,
                        claimed.RejectionCode ?? "shop-rap-application-pending");
                    PurchaseRecord record = new PurchaseRecord(command, pending, state, entry, commit);
                    purchases.Add(command.TransactionStableId, record);
                    return pending;
                }

                MoneyWalletChangeFact refund = money.Grant(
                    RefundTransaction(command.TransactionStableId),
                    RefundOperation(command.TransactionStableId),
                    entry.Price);
                if (IsMoneyApplied(refund))
                {
                    state.SetEntry(entry);
                    return RecordTerminal(command, entry,
                        ShopPurchaseStatusV1.RewardApplicationRejected,
                        entry.Price,
                        balanceBefore,
                        money.Balance,
                        false,
                        claimed.RejectionCode ?? "shop-rap-claim-rejected");
                }

                ShopPurchaseFactV1 compensationPending = new ShopPurchaseFactV1(
                    command.TransactionStableId,
                    command.Fingerprint,
                    ShopPurchaseStatusV1.CompensationPending,
                    ShopPurchaseStatusV1.CompensationPending,
                    entry.StockEntryStableId,
                    entry.Price,
                    balanceBefore,
                    money.Balance,
                    false,
                    refund.RejectionCode ?? "shop-refund-pending");
                purchases.Add(command.TransactionStableId,
                    new PurchaseRecord(command, compensationPending, state, entry, commit));
                return compensationPending;
            }
        }

    }
}
