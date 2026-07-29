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
    public sealed partial class ShopLiveActions
    {
        public ShopRefreshFact Refresh(ShopRefreshCommand command)
        {
            lock (sync)
            {
                if (command == null)
                {
                    return new ShopRefreshFact(
                        null,
                        null,
                        ShopRefreshStatus.InvalidRequest,
                        ShopRefreshStatus.InvalidRequest,
                        -1,
                        -1,
                        null,
                        null,
                        "shop-refresh-command-null");
                }

                RefreshRecord prior;
                if (refreshes.TryGetValue(command.TransactionStableId, out prior))
                {
                    return string.Equals(prior.Command.Fingerprint, command.Fingerprint, StringComparison.Ordinal)
                        ? prior.Fact.AsExactDuplicate()
                        : prior.Fact.AsConflict();
                }

                string key = Key(command.RunStableId, command.ShopStableId);
                ShopState state;
                if (!shops.TryGetValue(key, out state) || !state.IsBound)
                {
                    return RecordRefresh(command, null, ShopRefreshStatus.UnknownShop,
                        "shop-runtime-unknown-or-unbound");
                }

                if (!string.Equals(
                    state.InventoryFingerprint,
                    command.InventoryFingerprint,
                    StringComparison.Ordinal))
                {
                    return RecordRefresh(command, state, ShopRefreshStatus.StaleInventoryFingerprint,
                        "shop-inventory-fingerprint-stale");
                }

                if (state.Definition.RefreshPolicy == ShopRefreshPolicy.Disabled)
                {
                    return RecordRefresh(command, state, ShopRefreshStatus.Disabled,
                        "shop-refresh-disabled");
                }

                if (state.RefreshOrdinal >= state.Definition.MaximumRunRefreshCount)
                {
                    return RecordRefresh(command, state, ShopRefreshStatus.LimitReached,
                        "shop-refresh-limit-reached");
                }

                int capacity = state.Definition.BaseLockCapacity;
                if (lockCapacityExtension != null)
                {
                    int additional = lockCapacityExtension.GetAdditionalCapacity(
                        new ShopLockCapacityQuery(
                            state.RunStableId,
                            state.ShopStableId,
                            state.RefreshOrdinal,
                            capacity));
                    if (additional < 0)
                    {
                        return RecordRefresh(command, state, ShopRefreshStatus.InvalidRequest,
                            "shop-lock-capacity-extension-negative");
                    }

                    try
                    {
                        capacity = Math.Min(
                            state.Definition.InventorySize,
                            checked(capacity + additional));
                    }
                    catch (OverflowException)
                    {
                        return RecordRefresh(command, state, ShopRefreshStatus.InvalidRequest,
                            "shop-lock-capacity-extension-overflow");
                    }
                }

                if (command.LockedEntryStableIds.Count > capacity)
                {
                    return RecordRefresh(command, state, ShopRefreshStatus.LockCapacityExceeded,
                        "shop-lock-capacity-exceeded");
                }

                List<ShopStockEntry> locked = new List<ShopStockEntry>();
                for (int index = 0; index < command.LockedEntryStableIds.Count; index++)
                {
                    ShopStockEntry entry = state.FindEntry(command.LockedEntryStableIds[index]);
                    if (entry == null)
                    {
                        return RecordRefresh(command, state, ShopRefreshStatus.UnknownLockedEntry,
                            "shop-locked-entry-unknown");
                    }

                    if (entry.State == ShopStockEntryState.SoldOut)
                    {
                        return RecordRefresh(command, state,
                            ShopRefreshStatus.SoldOutEntryCannotBeLocked,
                            "shop-sold-entry-cannot-be-locked");
                    }

                    if (entry.State == ShopStockEntryState.PurchasePending)
                    {
                        return RecordRefresh(command, state,
                            ShopRefreshStatus.PendingEntryCannotBeLocked,
                            "shop-pending-entry-cannot-be-locked");
                    }

                    locked.Add(entry);
                }

                ProgressionContext selectedContext = state.Definition.SelectRefreshContext(
                    state.FirstOpenContext,
                    command.RequestedContext);
                int nextOrdinal = checked(state.RefreshOrdinal + 1);
                List<ShopStockEntry> entries;
                ulong seed;
                string rejection;
                if (!TryGenerateInventory(
                    state.RunStableId,
                    state.Definition,
                    state.Catalog,
                    selectedContext,
                    nextOrdinal,
                    locked,
                    out seed,
                    out entries,
                    out rejection))
                {
                    return RecordRefresh(command, state, ShopRefreshStatus.GenerationRejected,
                        rejection);
                }

                int previousOrdinal = state.RefreshOrdinal;
                string previousFingerprint = state.InventoryFingerprint;
                state.ReplaceInventory(selectedContext, nextOrdinal, seed, entries);
                ShopRefreshFact fact = new ShopRefreshFact(
                    command.TransactionStableId,
                    command.Fingerprint,
                    ShopRefreshStatus.Applied,
                    ShopRefreshStatus.Applied,
                    previousOrdinal,
                    state.RefreshOrdinal,
                    previousFingerprint,
                    state.InventoryFingerprint,
                    null);
                refreshes.Add(command.TransactionStableId, new RefreshRecord(command, fact));
                return fact;
            }
        }

        public bool TryGetInventory(
            StableId runStableId,
            StableId shopStableId,
            out ShopInventoryView inventory)
        {
            lock (sync)
            {
                ShopState state;
                if (runStableId != null
                    && shopStableId != null
                    && shops.TryGetValue(Key(runStableId, shopStableId), out state))
                {
                    inventory = state.ToView();
                    return true;
                }

                inventory = null;
                return false;
            }
        }

        public ShopLiveSnapshot ExportSnapshot()
        {
            lock (sync)
            {
                List<ShopRunInventorySnapshot> snapshots = new List<ShopRunInventorySnapshot>();
                foreach (ShopState state in shops.Values)
                {
                    snapshots.Add(state.ToSnapshot());
                }

                return ShopLiveSnapshot.CreateCanonical(snapshots);
            }
        }

        public bool TryImportSnapshot(ShopLiveSnapshot snapshot, out string rejectionCode)
        {
            lock (sync)
            {
                rejectionCode = null;
                if (snapshot == null)
                {
                    rejectionCode = "shop-snapshot-null";
                    return false;
                }

                if (snapshot.SchemaVersion != ShopLiveSnapshot.CurrentSchemaVersion)
                {
                    rejectionCode = "shop-snapshot-schema-unsupported";
                    return false;
                }

                if (!string.Equals(
                    snapshot.Fingerprint,
                    ShopLiveSnapshot.ComputeFingerprint(snapshot),
                    StringComparison.Ordinal))
                {
                    rejectionCode = "shop-snapshot-fingerprint-mismatch";
                    return false;
                }

                Dictionary<string, ShopState> replacement = new Dictionary<string, ShopState>();
                for (int index = 0; index < snapshot.Inventories.Count; index++)
                {
                    ShopRunInventorySnapshot item = snapshot.Inventories[index];
                    string expectedInventoryFingerprint = ShopInventoryView.ComputeInventoryFingerprint(
                        item.RunStableId,
                        item.ShopStableId,
                        item.RefreshOrdinal,
                        item.InventorySeed,
                        item.DefinitionFingerprint,
                        item.InventoryContext.Fingerprint,
                        item.Entries);
                    if (!string.Equals(
                        expectedInventoryFingerprint,
                        item.InventoryFingerprint,
                        StringComparison.Ordinal))
                    {
                        rejectionCode = "shop-snapshot-inventory-fingerprint-mismatch";
                        return false;
                    }

                    string key = Key(item.RunStableId, item.ShopStableId);
                    if (replacement.ContainsKey(key))
                    {
                        rejectionCode = "shop-snapshot-duplicate-run-shop";
                        return false;
                    }

                    replacement.Add(key, ShopState.FromSnapshot(item));
                }

                shops.Clear();
                foreach (KeyValuePair<string, ShopState> pair in replacement)
                {
                    shops.Add(pair.Key, pair.Value);
                }

                purchases.Clear();
                refreshes.Clear();
                return true;
            }
        }

    }
}
