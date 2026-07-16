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
        public ShopRefreshFactV1 Refresh(ShopRefreshCommandV1 command)
        {
            lock (sync)
            {
                if (command == null)
                {
                    return new ShopRefreshFactV1(
                        null,
                        null,
                        ShopRefreshStatusV1.InvalidRequest,
                        ShopRefreshStatusV1.InvalidRequest,
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
                    return RecordRefresh(command, null, ShopRefreshStatusV1.UnknownShop,
                        "shop-runtime-unknown-or-unbound");
                }

                if (!string.Equals(
                    state.InventoryFingerprint,
                    command.InventoryFingerprint,
                    StringComparison.Ordinal))
                {
                    return RecordRefresh(command, state, ShopRefreshStatusV1.StaleInventoryFingerprint,
                        "shop-inventory-fingerprint-stale");
                }

                if (state.Definition.RefreshPolicy == ShopRefreshPolicyV1.Disabled)
                {
                    return RecordRefresh(command, state, ShopRefreshStatusV1.Disabled,
                        "shop-refresh-disabled");
                }

                if (state.RefreshOrdinal >= state.Definition.MaximumRunRefreshCount)
                {
                    return RecordRefresh(command, state, ShopRefreshStatusV1.LimitReached,
                        "shop-refresh-limit-reached");
                }

                int capacity = state.Definition.BaseLockCapacity;
                if (lockCapacityExtension != null)
                {
                    int additional = lockCapacityExtension.GetAdditionalCapacity(
                        new ShopLockCapacityQueryV1(
                            state.RunStableId,
                            state.ShopStableId,
                            state.RefreshOrdinal,
                            capacity));
                    if (additional < 0)
                    {
                        return RecordRefresh(command, state, ShopRefreshStatusV1.InvalidRequest,
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
                        return RecordRefresh(command, state, ShopRefreshStatusV1.InvalidRequest,
                            "shop-lock-capacity-extension-overflow");
                    }
                }

                if (command.LockedEntryStableIds.Count > capacity)
                {
                    return RecordRefresh(command, state, ShopRefreshStatusV1.LockCapacityExceeded,
                        "shop-lock-capacity-exceeded");
                }

                List<ShopStockEntryV1> locked = new List<ShopStockEntryV1>();
                for (int index = 0; index < command.LockedEntryStableIds.Count; index++)
                {
                    ShopStockEntryV1 entry = state.FindEntry(command.LockedEntryStableIds[index]);
                    if (entry == null)
                    {
                        return RecordRefresh(command, state, ShopRefreshStatusV1.UnknownLockedEntry,
                            "shop-locked-entry-unknown");
                    }

                    if (entry.State == ShopStockEntryStateV1.SoldOut)
                    {
                        return RecordRefresh(command, state,
                            ShopRefreshStatusV1.SoldOutEntryCannotBeLocked,
                            "shop-sold-entry-cannot-be-locked");
                    }

                    if (entry.State == ShopStockEntryStateV1.PurchasePending)
                    {
                        return RecordRefresh(command, state,
                            ShopRefreshStatusV1.PendingEntryCannotBeLocked,
                            "shop-pending-entry-cannot-be-locked");
                    }

                    locked.Add(entry);
                }

                ProgressionContext selectedContext = state.Definition.SelectRefreshContext(
                    state.FirstOpenContext,
                    command.RequestedContext);
                int nextOrdinal = checked(state.RefreshOrdinal + 1);
                List<ShopStockEntryV1> entries;
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
                    return RecordRefresh(command, state, ShopRefreshStatusV1.GenerationRejected,
                        rejection);
                }

                int previousOrdinal = state.RefreshOrdinal;
                string previousFingerprint = state.InventoryFingerprint;
                state.ReplaceInventory(selectedContext, nextOrdinal, seed, entries);
                ShopRefreshFactV1 fact = new ShopRefreshFactV1(
                    command.TransactionStableId,
                    command.Fingerprint,
                    ShopRefreshStatusV1.Applied,
                    ShopRefreshStatusV1.Applied,
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
            out ShopInventoryViewV1 inventory)
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

        public ShopRuntimeSnapshotV1 ExportSnapshot()
        {
            lock (sync)
            {
                List<ShopRunInventorySnapshotV1> snapshots = new List<ShopRunInventorySnapshotV1>();
                foreach (ShopState state in shops.Values)
                {
                    snapshots.Add(state.ToSnapshot());
                }

                return ShopRuntimeSnapshotV1.CreateCanonical(snapshots);
            }
        }

        public bool TryImportSnapshot(ShopRuntimeSnapshotV1 snapshot, out string rejectionCode)
        {
            lock (sync)
            {
                rejectionCode = null;
                if (snapshot == null)
                {
                    rejectionCode = "shop-snapshot-null";
                    return false;
                }

                if (snapshot.SchemaVersion != ShopRuntimeSnapshotV1.CurrentSchemaVersion)
                {
                    rejectionCode = "shop-snapshot-schema-unsupported";
                    return false;
                }

                if (!string.Equals(
                    snapshot.Fingerprint,
                    ShopRuntimeSnapshotV1.ComputeFingerprint(snapshot),
                    StringComparison.Ordinal))
                {
                    rejectionCode = "shop-snapshot-fingerprint-mismatch";
                    return false;
                }

                Dictionary<string, ShopState> replacement = new Dictionary<string, ShopState>();
                for (int index = 0; index < snapshot.Inventories.Count; index++)
                {
                    ShopRunInventorySnapshotV1 item = snapshot.Inventories[index];
                    string expectedInventoryFingerprint = ShopInventoryViewV1.ComputeInventoryFingerprint(
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
