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
        private sealed class ShopState
        {
            private readonly List<ShopStockEntry> entries;

            private ShopState(
                StableId runStableId,
                StableId shopStableId,
                string definitionFingerprint,
                string catalogFingerprint,
                ProgressionContext firstOpenContext,
                ProgressionContext inventoryContext,
                int refreshOrdinal,
                ulong inventorySeed,
                IEnumerable<ShopStockEntry> entries,
                ShopDefinition definition,
                EquipmentCatalog catalog)
            {
                RunStableId = runStableId;
                ShopStableId = shopStableId;
                DefinitionFingerprint = definitionFingerprint;
                CatalogFingerprint = catalogFingerprint;
                FirstOpenContext = firstOpenContext;
                InventoryContext = inventoryContext;
                RefreshOrdinal = refreshOrdinal;
                InventorySeed = inventorySeed;
                this.entries = new List<ShopStockEntry>(entries);
                this.entries.Sort();
                Definition = definition;
                Catalog = catalog;
                RecomputeFingerprint();
            }

            public StableId RunStableId { get; }
            public StableId ShopStableId { get; }
            public string DefinitionFingerprint { get; }
            public string CatalogFingerprint { get; private set; }
            public ProgressionContext FirstOpenContext { get; }
            public ProgressionContext InventoryContext { get; private set; }
            public int RefreshOrdinal { get; private set; }
            public ulong InventorySeed { get; private set; }
            public string InventoryFingerprint { get; private set; }
            public ShopDefinition Definition { get; private set; }
            public EquipmentCatalog Catalog { get; private set; }
            public bool IsBound { get { return Definition != null && Catalog != null; } }

            public static ShopState Create(
                StableId runStableId,
                ShopDefinition definition,
                EquipmentCatalog catalog,
                ProgressionContext firstOpenContext,
                ProgressionContext inventoryContext,
                int refreshOrdinal,
                ulong inventorySeed,
                IEnumerable<ShopStockEntry> entries)
            {
                return new ShopState(
                    runStableId,
                    definition.ShopStableId,
                    definition.Fingerprint,
                    catalog.Fingerprint,
                    firstOpenContext,
                    inventoryContext,
                    refreshOrdinal,
                    inventorySeed,
                    entries,
                    definition,
                    catalog);
            }

            public static ShopState FromSnapshot(ShopRunInventorySnapshot snapshot)
            {
                ShopState state = new ShopState(
                    snapshot.RunStableId,
                    snapshot.ShopStableId,
                    snapshot.DefinitionFingerprint,
                    string.Empty,
                    snapshot.FirstOpenContext,
                    snapshot.InventoryContext,
                    snapshot.RefreshOrdinal,
                    snapshot.InventorySeed,
                    snapshot.Entries,
                    null,
                    null);
                state.InventoryFingerprint = snapshot.InventoryFingerprint;
                return state;
            }

            public bool CanBind(EquipmentCatalog catalog)
            {
                if (catalog == null)
                {
                    return false;
                }

                for (int index = 0; index < entries.Count; index++)
                {
                    if (!catalog.ValidateInstance(entries[index].Equipment).IsValid)
                    {
                        return false;
                    }
                }

                return true;
            }

            public void Bind(ShopDefinition definition, EquipmentCatalog catalog)
            {
                Definition = definition;
                Catalog = catalog;
                CatalogFingerprint = catalog.Fingerprint;
            }

            public ShopStockEntry FindEntry(StableId entryId)
            {
                for (int index = 0; index < entries.Count; index++)
                {
                    if (entries[index].StockEntryStableId == entryId)
                    {
                        return entries[index];
                    }
                }

                return null;
            }

            public void SetEntry(ShopStockEntry replacement)
            {
                for (int index = 0; index < entries.Count; index++)
                {
                    if (entries[index].StockEntryStableId == replacement.StockEntryStableId)
                    {
                        entries[index] = replacement;
                        return;
                    }
                }

                throw new InvalidOperationException("Shop stock entry was not found.");
            }

            public void ReplaceInventory(
                ProgressionContext context,
                int refreshOrdinal,
                ulong inventorySeed,
                IEnumerable<ShopStockEntry> replacements)
            {
                InventoryContext = context;
                RefreshOrdinal = refreshOrdinal;
                InventorySeed = inventorySeed;
                entries.Clear();
                entries.AddRange(replacements);
                entries.Sort();
                RecomputeFingerprint();
            }

            public ShopInventoryView ToView()
            {
                return new ShopInventoryView(
                    RunStableId,
                    ShopStableId,
                    RefreshOrdinal,
                    InventorySeed,
                    DefinitionFingerprint,
                    InventoryContext.Fingerprint,
                    InventoryFingerprint,
                    entries);
            }

            public ShopRunInventorySnapshot ToSnapshot()
            {
                return new ShopRunInventorySnapshot(
                    RunStableId,
                    ShopStableId,
                    RefreshOrdinal,
                    InventorySeed,
                    DefinitionFingerprint,
                    FirstOpenContext,
                    InventoryContext,
                    InventoryFingerprint,
                    entries);
            }

            private void RecomputeFingerprint()
            {
                InventoryFingerprint = ShopInventoryView.ComputeInventoryFingerprint(
                    RunStableId,
                    ShopStableId,
                    RefreshOrdinal,
                    InventorySeed,
                    DefinitionFingerprint,
                    InventoryContext.Fingerprint,
                    entries);
            }
        }

        private sealed class PurchaseRecord
        {
            public PurchaseRecord(
                ShopPurchaseCommand command,
                ShopPurchaseFact fact,
                ShopState state,
                ShopStockEntry entry,
                RewardCommitCommand commit)
            {
                Command = command;
                Fact = fact;
                State = state;
                Entry = entry;
                Commit = commit;
            }

            public ShopPurchaseCommand Command { get; }
            public ShopPurchaseFact Fact { get; set; }
            public ShopState State { get; }
            public ShopStockEntry Entry { get; }
            public RewardCommitCommand Commit { get; }
        }

        private sealed class RefreshRecord
        {
            public RefreshRecord(ShopRefreshCommand command, ShopRefreshFact fact)
            {
                Command = command;
                Fact = fact;
            }

            public ShopRefreshCommand Command { get; }
            public ShopRefreshFact Fact { get; }
        }
    }
}
