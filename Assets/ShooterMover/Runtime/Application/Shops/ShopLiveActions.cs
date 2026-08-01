using System;
using System.Collections.Generic;
using ShooterMover.Application.Economy.Money;
using ShooterMover.Application.Rewards.Application;
using ShooterMover.Application.Rewards.Generation;
using ShooterMover.Contracts.Rewards.Application;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Economy.Money;
using ShooterMover.Domain.Equipment;
using ShooterMover.Domain.Progression.Context;
using ShooterMover.Domain.Shops;

namespace ShooterMover.Application.Shops
{
    public sealed partial class ShopLiveActions
    {
        private readonly object sync = new object();
        private readonly RewardGenerationActions generator;
        private readonly MoneyWalletActions money;
        private readonly RewardApplicationActions rewardApplication;
        private readonly StableId scrapAuthorityStableId;
        private readonly StableId holdingsAuthorityStableId;
        private readonly IShopLockCapacityExtension lockCapacityExtension;
        private readonly IShopOfferRoller offerRoller;
        private readonly ShopReceipts receipts;
        private readonly Dictionary<string, ShopState> shops =
            new Dictionary<string, ShopState>();
        private readonly Dictionary<StableId, PurchaseRecord> purchases =
            new Dictionary<StableId, PurchaseRecord>();
        private readonly Dictionary<StableId, RefreshRecord> refreshes =
            new Dictionary<StableId, RefreshRecord>();

        public ShopLiveActions(
            RewardGenerationActions generator,
            MoneyWalletActions money,
            RewardApplicationActions rewardApplication,
            StableId scrapAuthorityStableId,
            StableId holdingsAuthorityStableId,
            IShopLockCapacityExtension lockCapacityExtension = null,
            IShopOfferRoller offerRoller = null,
            ShopReceipts receipts = null)
        {
            this.generator = generator
                ?? throw new ArgumentNullException(nameof(generator));
            this.money = money
                ?? throw new ArgumentNullException(nameof(money));
            this.rewardApplication = rewardApplication
                ?? throw new ArgumentNullException(nameof(rewardApplication));
            this.scrapAuthorityStableId = scrapAuthorityStableId
                ?? throw new ArgumentNullException(nameof(scrapAuthorityStableId));
            this.holdingsAuthorityStableId = holdingsAuthorityStableId
                ?? throw new ArgumentNullException(nameof(holdingsAuthorityStableId));
            this.lockCapacityExtension = lockCapacityExtension;
            this.offerRoller = offerRoller;
            this.receipts = receipts;
        }

        public ShopInventoryOpenResult Open(
            StableId runStableId,
            ShopDefinition definition,
            EquipmentCatalog catalog,
            ProgressionContext context)
        {
            lock (sync)
            {
                if (runStableId == null
                    || definition == null
                    || catalog == null
                    || context == null)
                {
                    return new ShopInventoryOpenResult(
                        ShopInventoryOpenStatus.InvalidRequest,
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
                        return new ShopInventoryOpenResult(
                            ShopInventoryOpenStatus.DefinitionMismatch,
                            existing.ToView(),
                            "shop-definition-fingerprint-mismatch");
                    }

                    if (!string.IsNullOrEmpty(existing.CatalogFingerprint)
                        && !string.Equals(
                            existing.CatalogFingerprint,
                            catalog.Fingerprint,
                            StringComparison.Ordinal))
                    {
                        return new ShopInventoryOpenResult(
                            ShopInventoryOpenStatus.SnapshotBindingRejected,
                            existing.ToView(),
                            "shop-catalog-fingerprint-mismatch");
                    }

                    if (!existing.CanBind(catalog))
                    {
                        return new ShopInventoryOpenResult(
                            ShopInventoryOpenStatus.SnapshotBindingRejected,
                            existing.ToView(),
                            "shop-snapshot-equipment-invalid-for-catalog");
                    }

                    existing.Bind(definition, catalog);
                    ApplyReceipts(existing);
                    return new ShopInventoryOpenResult(
                        ShopInventoryOpenStatus.ExistingNoChange,
                        existing.ToView(),
                        null);
                }

                List<ShopStockEntry> entries;
                ulong seed;
                string rejection;
                if (!TryGenerateInventory(
                    runStableId,
                    definition,
                    catalog,
                    context,
                    0,
                    new List<ShopStockEntry>(),
                    out seed,
                    out entries,
                    out rejection))
                {
                    return new ShopInventoryOpenResult(
                        ShopInventoryOpenStatus.GenerationRejected,
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
                ApplyReceipts(created);
                shops.Add(key, created);
                return new ShopInventoryOpenResult(
                    ShopInventoryOpenStatus.Generated,
                    created.ToView(),
                    null);
            }
        }

        public ShopPurchaseFact Purchase(ShopPurchaseCommand command)
        {
            lock (sync)
            {
                if (command == null)
                {
                    return new ShopPurchaseFact(
                        null,
                        null,
                        ShopPurchaseStatus.InvalidRequest,
                        ShopPurchaseStatus.InvalidRequest,
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
                    if (!string.Equals(
                        prior.Command.Fingerprint,
                        command.Fingerprint,
                        StringComparison.Ordinal))
                    {
                        return prior.Fact.AsConflict();
                    }

                    if (prior.Fact.OriginalStatus
                            == ShopPurchaseStatus.PurchasePending
                        || prior.Fact.OriginalStatus
                            == ShopPurchaseStatus.CompensationPending)
                    {
                        return ResumePendingPurchase(prior);
                    }

                    return prior.Fact.AsExactDuplicate();
                }

                string key = Key(command.RunStableId, command.ShopStableId);
                ShopState state;
                if (!shops.TryGetValue(key, out state) || !state.IsBound)
                {
                    return RecordTerminal(
                        command,
                        null,
                        ShopPurchaseStatus.UnknownShop,
                        0L,
                        money.Balance,
                        money.Balance,
                        false,
                        "shop-runtime-unknown-or-unbound");
                }

                if (!string.Equals(
                    state.InventoryFingerprint,
                    command.InventoryFingerprint,
                    StringComparison.Ordinal))
                {
                    return RecordTerminal(
                        command,
                        null,
                        ShopPurchaseStatus.StaleInventoryFingerprint,
                        0L,
                        money.Balance,
                        money.Balance,
                        false,
                        "shop-inventory-fingerprint-stale");
                }

                ShopStockEntry entry = state.FindEntry(
                    command.StockEntryStableId);
                if (entry == null)
                {
                    return RecordTerminal(
                        command,
                        null,
                        ShopPurchaseStatus.UnknownStockEntry,
                        0L,
                        money.Balance,
                        money.Balance,
                        false,
                        "shop-stock-entry-unknown");
                }

                if (entry.State != ShopStockEntryState.Available)
                {
                    return RecordTerminal(
                        command,
                        entry,
                        ShopPurchaseStatus.SoldOut,
                        entry.Price,
                        money.Balance,
                        money.Balance,
                        false,
                        entry.State == ShopStockEntryState.SoldOut
                            ? "shop-stock-entry-sold-out"
                            : "shop-stock-entry-purchase-pending");
                }

                if (entry.Price != command.ExpectedPrice)
                {
                    return RecordTerminal(
                        command,
                        entry,
                        ShopPurchaseStatus.PriceMismatch,
                        entry.Price,
                        money.Balance,
                        money.Balance,
                        false,
                        "shop-price-mismatch");
                }

                long balanceBefore = money.Balance;
                long moneySequence = money.Sequence;
                if (balanceBefore < entry.Price)
                {
                    return RecordTerminal(
                        command,
                        entry,
                        ShopPurchaseStatus.InsufficientFunds,
                        entry.Price,
                        balanceBefore,
                        balanceBefore,
                        false,
                        "shop-insufficient-money");
                }

                state.SetEntry(entry.WithPurchaseState(
                    ShopStockEntryState.PurchasePending,
                    command.TransactionStableId));

                RewardCommitCommand commit = BuildCommit(command, state, entry);
                RewardApplicationResult committed = rewardApplication.Commit(commit);
                if (!IsCommitAccepted(committed.Status))
                {
                    state.SetEntry(entry);
                    return RecordTerminal(
                        command,
                        entry,
                        ShopPurchaseStatus.RewardApplicationRejected,
                        entry.Price,
                        balanceBefore,
                        money.Balance,
                        false,
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
                    ShopPurchaseStatus status = spend.Status
                            == MoneyWalletTransactionStatus.InsufficientFunds
                        ? ShopPurchaseStatus.InsufficientFunds
                        : ShopPurchaseStatus.InvalidRequest;
                    return RecordTerminal(
                        command,
                        entry,
                        status,
                        entry.Price,
                        balanceBefore,
                        money.Balance,
                        false,
                        spend.RejectionCode ?? "shop-money-spend-rejected");
                }

                RewardClaimCommand claim = BuildClaim(command, commit);
                RewardApplicationResult claimed = rewardApplication.Claim(claim);
                if (IsRewardApplied(claimed.Status))
                {
                    state.SetEntry(entry.WithPurchaseState(
                        ShopStockEntryState.SoldOut,
                        command.TransactionStableId));
                    string receiptDiagnostic;
                    RecordReceipt(
                        entry.StockEntryStableId,
                        command.TransactionStableId,
                        out receiptDiagnostic);
                    return RecordTerminal(
                        command,
                        entry,
                        ShopPurchaseStatus.Applied,
                        entry.Price,
                        balanceBefore,
                        money.Balance,
                        true,
                        receiptDiagnostic);
                }

                if (claimed.Status
                    == RewardApplicationResultStatus.ClaimedPendingApplication)
                {
                    ShopPurchaseFact pending = new ShopPurchaseFact(
                        command.TransactionStableId,
                        command.Fingerprint,
                        ShopPurchaseStatus.PurchasePending,
                        ShopPurchaseStatus.PurchasePending,
                        entry.StockEntryStableId,
                        entry.Price,
                        balanceBefore,
                        money.Balance,
                        false,
                        claimed.RejectionCode
                            ?? "shop-rap-application-pending");
                    PurchaseRecord record = new PurchaseRecord(
                        command,
                        pending,
                        state,
                        entry,
                        commit);
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
                    return RecordTerminal(
                        command,
                        entry,
                        ShopPurchaseStatus.RewardApplicationRejected,
                        entry.Price,
                        balanceBefore,
                        money.Balance,
                        false,
                        claimed.RejectionCode ?? "shop-rap-claim-rejected");
                }

                ShopPurchaseFact compensationPending = new ShopPurchaseFact(
                    command.TransactionStableId,
                    command.Fingerprint,
                    ShopPurchaseStatus.CompensationPending,
                    ShopPurchaseStatus.CompensationPending,
                    entry.StockEntryStableId,
                    entry.Price,
                    balanceBefore,
                    money.Balance,
                    false,
                    refund.RejectionCode ?? "shop-refund-pending");
                purchases.Add(
                    command.TransactionStableId,
                    new PurchaseRecord(
                        command,
                        compensationPending,
                        state,
                        entry,
                        commit));
                return compensationPending;
            }
        }
    }
}
