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
        private sealed class ShopState
        {
            private readonly List<ShopStockEntryV1> entries;

            private ShopState(
                StableId runStableId,
                StableId shopStableId,
                string definitionFingerprint,
                string catalogFingerprint,
                ProgressionContext firstOpenContext,
                ProgressionContext inventoryContext,
                int refreshOrdinal,
                ulong inventorySeed,
                IEnumerable<ShopStockEntryV1> entries,
                ShopDefinitionV1 definition,
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
                this.entries = new List<ShopStockEntryV1>(entries);
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
            public ShopDefinitionV1 Definition { get; private set; }
            public EquipmentCatalog Catalog { get; private set; }
            public bool IsBound { get { return Definition != null && Catalog != null; } }

            public static ShopState Create(
                StableId runStableId,
                ShopDefinitionV1 definition,
                EquipmentCatalog catalog,
                ProgressionContext firstOpenContext,
                ProgressionContext inventoryContext,
                int refreshOrdinal,
                ulong inventorySeed,
                IEnumerable<ShopStockEntryV1> entries)
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

            public static ShopState FromSnapshot(ShopRunInventorySnapshotV1 snapshot)
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

            public void Bind(ShopDefinitionV1 definition, EquipmentCatalog catalog)
            {
                Definition = definition;
                Catalog = catalog;
                CatalogFingerprint = catalog.Fingerprint;
            }

            public ShopStockEntryV1 FindEntry(StableId entryId)
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

            public void SetEntry(ShopStockEntryV1 replacement)
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
                IEnumerable<ShopStockEntryV1> replacements)
            {
                InventoryContext = context;
                RefreshOrdinal = refreshOrdinal;
                InventorySeed = inventorySeed;
                entries.Clear();
                entries.AddRange(replacements);
                entries.Sort();
                RecomputeFingerprint();
            }

            public ShopInventoryViewV1 ToView()
            {
                return new ShopInventoryViewV1(
                    RunStableId,
                    ShopStableId,
                    RefreshOrdinal,
                    InventorySeed,
                    DefinitionFingerprint,
                    InventoryContext.Fingerprint,
                    InventoryFingerprint,
                    entries);
            }

            public ShopRunInventorySnapshotV1 ToSnapshot()
            {
                return new ShopRunInventorySnapshotV1(
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
                InventoryFingerprint = ShopInventoryViewV1.ComputeInventoryFingerprint(
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
                ShopPurchaseCommandV1 command,
                ShopPurchaseFactV1 fact,
                ShopState state,
                ShopStockEntryV1 entry,
                RewardCommitCommandV1 commit)
            {
                Command = command;
                Fact = fact;
                State = state;
                Entry = entry;
                Commit = commit;
            }

            public ShopPurchaseCommandV1 Command { get; }
            public ShopPurchaseFactV1 Fact { get; set; }
            public ShopState State { get; }
            public ShopStockEntryV1 Entry { get; }
            public RewardCommitCommandV1 Commit { get; }
        }

        private sealed class RefreshRecord
        {
            public RefreshRecord(ShopRefreshCommandV1 command, ShopRefreshFactV1 fact)
            {
                Command = command;
                Fact = fact;
            }

            public ShopRefreshCommandV1 Command { get; }
            public ShopRefreshFactV1 Fact { get; }
        }
    }
}
