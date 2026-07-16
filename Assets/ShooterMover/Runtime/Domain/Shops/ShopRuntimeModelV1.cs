using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Equipment;
using ShooterMover.Domain.Progression.Context;

namespace ShooterMover.Domain.Shops
{
    public enum ShopInventoryOpenStatusV1
    {
        Generated = 1,
        ExistingNoChange = 2,
        DefinitionMismatch = 3,
        GenerationRejected = 4,
        SnapshotBindingRejected = 5,
        InvalidRequest = 6,
    }

    public enum ShopStockEntryStateV1
    {
        Available = 1,
        PurchasePending = 2,
        SoldOut = 3,
    }

    public enum ShopPurchaseStatusV1
    {
        Applied = 1,
        ExactDuplicateNoChange = 2,
        ConflictingDuplicate = 3,
        PurchasePending = 4,
        SoldOut = 5,
        InsufficientFunds = 6,
        UnknownShop = 7,
        UnknownStockEntry = 8,
        StaleInventoryFingerprint = 9,
        PriceMismatch = 10,
        RewardApplicationRejected = 11,
        CompensationPending = 12,
        InvalidRequest = 13,
    }

    public enum ShopRefreshStatusV1
    {
        Applied = 1,
        ExactDuplicateNoChange = 2,
        ConflictingDuplicate = 3,
        Disabled = 4,
        LimitReached = 5,
        UnknownShop = 6,
        StaleInventoryFingerprint = 7,
        LockCapacityExceeded = 8,
        UnknownLockedEntry = 9,
        SoldOutEntryCannotBeLocked = 10,
        PendingEntryCannotBeLocked = 11,
        GenerationRejected = 12,
        InvalidRequest = 13,
    }

    public sealed class ShopStockEntryV1 : IComparable<ShopStockEntryV1>
    {
        private readonly string canonicalText;

        public ShopStockEntryV1(
            StableId stockEntryStableId,
            EquipmentInstance equipment,
            long price,
            string generationFingerprint,
            ShopStockEntryStateV1 state,
            StableId purchaseTransactionStableId)
        {
            StockEntryStableId = stockEntryStableId
                ?? throw new ArgumentNullException(nameof(stockEntryStableId));
            Equipment = equipment ?? throw new ArgumentNullException(nameof(equipment));
            if (price < 1L)
            {
                throw new ArgumentOutOfRangeException(nameof(price));
            }

            Price = price;
            GenerationFingerprint = RequireFingerprint(
                generationFingerprint,
                nameof(generationFingerprint));
            if (!Enum.IsDefined(typeof(ShopStockEntryStateV1), state))
            {
                throw new ArgumentOutOfRangeException(nameof(state));
            }

            if (state == ShopStockEntryStateV1.Available && purchaseTransactionStableId != null)
            {
                throw new ArgumentException(
                    "Available stock entries cannot carry a purchase transaction identity.",
                    nameof(purchaseTransactionStableId));
            }

            if (state != ShopStockEntryStateV1.Available && purchaseTransactionStableId == null)
            {
                throw new ArgumentException(
                    "Pending and sold entries require a purchase transaction identity.",
                    nameof(purchaseTransactionStableId));
            }

            State = state;
            PurchaseTransactionStableId = purchaseTransactionStableId;
            canonicalText = BuildCanonicalText();
        }

        public StableId StockEntryStableId { get; }
        public EquipmentInstance Equipment { get; }
        public long Price { get; }
        public string GenerationFingerprint { get; }
        public ShopStockEntryStateV1 State { get; }
        public StableId PurchaseTransactionStableId { get; }

        public ShopStockEntryV1 WithPurchaseState(
            ShopStockEntryStateV1 state,
            StableId purchaseTransactionStableId)
        {
            return new ShopStockEntryV1(
                StockEntryStableId,
                Equipment,
                Price,
                GenerationFingerprint,
                state,
                purchaseTransactionStableId);
        }

        public string ToCanonicalString() { return canonicalText; }

        public int CompareTo(ShopStockEntryV1 other)
        {
            return ReferenceEquals(other, null)
                ? 1
                : StockEntryStableId.CompareTo(other.StockEntryStableId);
        }

        private string BuildCanonicalText()
        {
            return "stock_entry_id=" + StockEntryStableId
                + "\nequipment_fingerprint=" + Equipment.Fingerprint
                + "\nprice=" + Price.ToString(CultureInfo.InvariantCulture)
                + "\ngeneration_fingerprint=" + GenerationFingerprint
                + "\nstate=" + ((int)State).ToString(CultureInfo.InvariantCulture)
                + "\npurchase_transaction_id="
                + (PurchaseTransactionStableId == null
                    ? "none"
                    : PurchaseTransactionStableId.ToString());
        }

        internal static string RequireFingerprint(string value, string parameterName)
        {
            if (value == null
                || value.Length != 71
                || !value.StartsWith("sha256:", StringComparison.Ordinal))
            {
                throw new ArgumentException("Expected canonical sha256 fingerprint.", parameterName);
            }

            for (int index = 7; index < value.Length; index++)
            {
                char current = value[index];
                if (!((current >= '0' && current <= '9')
                    || (current >= 'a' && current <= 'f')))
                {
                    throw new ArgumentException(
                        "Expected lowercase hexadecimal sha256 fingerprint.",
                        parameterName);
                }
            }

            return value;
        }
    }

    public sealed class ShopInventoryViewV1
    {
        private readonly ReadOnlyCollection<ShopStockEntryV1> entries;

        public ShopInventoryViewV1(
            StableId runStableId,
            StableId shopStableId,
            int refreshOrdinal,
            ulong inventorySeed,
            string definitionFingerprint,
            string progressionContextFingerprint,
            string inventoryFingerprint,
            IEnumerable<ShopStockEntryV1> entries)
        {
            RunStableId = runStableId ?? throw new ArgumentNullException(nameof(runStableId));
            ShopStableId = shopStableId ?? throw new ArgumentNullException(nameof(shopStableId));
            if (refreshOrdinal < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(refreshOrdinal));
            }

            RefreshOrdinal = refreshOrdinal;
            InventorySeed = inventorySeed;
            DefinitionFingerprint = ShopStockEntryV1.RequireFingerprint(
                definitionFingerprint,
                nameof(definitionFingerprint));
            ProgressionContextFingerprint = ShopStockEntryV1.RequireFingerprint(
                progressionContextFingerprint,
                nameof(progressionContextFingerprint));
            InventoryFingerprint = ShopStockEntryV1.RequireFingerprint(
                inventoryFingerprint,
                nameof(inventoryFingerprint));
            this.entries = CopyEntries(entries);
        }

        public StableId RunStableId { get; }
        public StableId ShopStableId { get; }
        public int RefreshOrdinal { get; }
        public ulong InventorySeed { get; }
        public string DefinitionFingerprint { get; }
        public string ProgressionContextFingerprint { get; }
        public string InventoryFingerprint { get; }
        public IReadOnlyList<ShopStockEntryV1> Entries { get { return entries; } }

        public ShopStockEntryV1 FindEntry(StableId entryStableId)
        {
            if (entryStableId == null)
            {
                return null;
            }

            for (int index = 0; index < entries.Count; index++)
            {
                if (entries[index].StockEntryStableId == entryStableId)
                {
                    return entries[index];
                }
            }

            return null;
        }

        public static string ComputeInventoryFingerprint(
            StableId runStableId,
            StableId shopStableId,
            int refreshOrdinal,
            ulong inventorySeed,
            string definitionFingerprint,
            string progressionContextFingerprint,
            IEnumerable<ShopStockEntryV1> entries)
        {
            List<ShopStockEntryV1> copy = new List<ShopStockEntryV1>(
                entries ?? throw new ArgumentNullException(nameof(entries)));
            copy.Sort();
            StringBuilder builder = new StringBuilder();
            builder.Append("schema=shop-inventory-v1")
                .Append("\nrun_id=").Append(runStableId)
                .Append("\nshop_id=").Append(shopStableId)
                .Append("\nrefresh_ordinal=").Append(refreshOrdinal.ToString(CultureInfo.InvariantCulture))
                .Append("\ninventory_seed=").Append(inventorySeed.ToString(CultureInfo.InvariantCulture))
                .Append("\ndefinition_fingerprint=").Append(definitionFingerprint)
                .Append("\nprogression_context_fingerprint=").Append(progressionContextFingerprint)
                .Append("\nentry_count=").Append(copy.Count.ToString(CultureInfo.InvariantCulture));
            for (int index = 0; index < copy.Count; index++)
            {
                ShopStockEntryV1 entry = copy[index];
                builder.Append("\nentry_")
                    .Append(index.ToString("D4", CultureInfo.InvariantCulture))
                    .Append("=")
                    .Append(entry.StockEntryStableId)
                    .Append('|').Append(entry.Equipment.Fingerprint)
                    .Append('|').Append(entry.Price.ToString(CultureInfo.InvariantCulture))
                    .Append('|').Append(entry.GenerationFingerprint);
            }

            return ShopCanonicalV1.Fingerprint(builder.ToString());
        }

        private static ReadOnlyCollection<ShopStockEntryV1> CopyEntries(
            IEnumerable<ShopStockEntryV1> source)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            List<ShopStockEntryV1> copy = new List<ShopStockEntryV1>();
            HashSet<StableId> ids = new HashSet<StableId>();
            foreach (ShopStockEntryV1 entry in source)
            {
                if (entry == null || !ids.Add(entry.StockEntryStableId))
                {
                    throw new ArgumentException(
                        "Stock entries must be non-null and have unique identities.",
                        nameof(source));
                }

                copy.Add(entry);
            }

            copy.Sort();
            return new ReadOnlyCollection<ShopStockEntryV1>(copy);
        }
    }

    public sealed class ShopInventoryOpenResultV1
    {
        public ShopInventoryOpenResultV1(
            ShopInventoryOpenStatusV1 status,
            ShopInventoryViewV1 inventory,
            string rejectionCode)
        {
            if (!Enum.IsDefined(typeof(ShopInventoryOpenStatusV1), status))
            {
                throw new ArgumentOutOfRangeException(nameof(status));
            }

            Status = status;
            Inventory = inventory;
            RejectionCode = rejectionCode;
        }

        public ShopInventoryOpenStatusV1 Status { get; }
        public ShopInventoryViewV1 Inventory { get; }
        public string RejectionCode { get; }
        public bool Succeeded
        {
            get
            {
                return Status == ShopInventoryOpenStatusV1.Generated
                    || Status == ShopInventoryOpenStatusV1.ExistingNoChange;
            }
        }
    }

    public sealed class ShopPurchaseCommandV1
    {
        private readonly string canonicalText;

        private ShopPurchaseCommandV1(
            StableId transactionStableId,
            StableId runStableId,
            StableId shopStableId,
            StableId stockEntryStableId,
            StableId claimantStableId,
            string inventoryFingerprint,
            long expectedPrice)
        {
            TransactionStableId = transactionStableId
                ?? throw new ArgumentNullException(nameof(transactionStableId));
            RunStableId = runStableId ?? throw new ArgumentNullException(nameof(runStableId));
            ShopStableId = shopStableId ?? throw new ArgumentNullException(nameof(shopStableId));
            StockEntryStableId = stockEntryStableId
                ?? throw new ArgumentNullException(nameof(stockEntryStableId));
            ClaimantStableId = claimantStableId
                ?? throw new ArgumentNullException(nameof(claimantStableId));
            InventoryFingerprint = ShopStockEntryV1.RequireFingerprint(
                inventoryFingerprint,
                nameof(inventoryFingerprint));
            if (expectedPrice < 1L)
            {
                throw new ArgumentOutOfRangeException(nameof(expectedPrice));
            }

            ExpectedPrice = expectedPrice;
            canonicalText = "schema=shop-purchase-command-v1"
                + "\ntransaction_id=" + TransactionStableId
                + "\nrun_id=" + RunStableId
                + "\nshop_id=" + ShopStableId
                + "\nstock_entry_id=" + StockEntryStableId
                + "\nclaimant_id=" + ClaimantStableId
                + "\ninventory_fingerprint=" + InventoryFingerprint
                + "\nexpected_price=" + ExpectedPrice.ToString(CultureInfo.InvariantCulture);
            Fingerprint = ShopCanonicalV1.Fingerprint(canonicalText);
        }

        public StableId TransactionStableId { get; }
        public StableId RunStableId { get; }
        public StableId ShopStableId { get; }
        public StableId StockEntryStableId { get; }
        public StableId ClaimantStableId { get; }
        public string InventoryFingerprint { get; }
        public long ExpectedPrice { get; }
        public string Fingerprint { get; }

        public static ShopPurchaseCommandV1 Create(
            StableId transactionStableId,
            StableId runStableId,
            StableId shopStableId,
            StableId stockEntryStableId,
            StableId claimantStableId,
            string inventoryFingerprint,
            long expectedPrice)
        {
            return new ShopPurchaseCommandV1(
                transactionStableId,
                runStableId,
                shopStableId,
                stockEntryStableId,
                claimantStableId,
                inventoryFingerprint,
                expectedPrice);
        }

        public string ToCanonicalString() { return canonicalText; }
    }

    public sealed class ShopPurchaseFactV1
    {
        public ShopPurchaseFactV1(
            StableId transactionStableId,
            string commandFingerprint,
            ShopPurchaseStatusV1 status,
            ShopPurchaseStatusV1 originalStatus,
            StableId stockEntryStableId,
            long price,
            long moneyBalanceBefore,
            long moneyBalanceAfter,
            bool equipmentConfirmed,
            string rejectionCode)
        {
            TransactionStableId = transactionStableId;
            CommandFingerprint = commandFingerprint;
            if (!Enum.IsDefined(typeof(ShopPurchaseStatusV1), status)
                || !Enum.IsDefined(typeof(ShopPurchaseStatusV1), originalStatus))
            {
                throw new ArgumentOutOfRangeException();
            }

            Status = status;
            OriginalStatus = originalStatus;
            StockEntryStableId = stockEntryStableId;
            Price = price;
            MoneyBalanceBefore = moneyBalanceBefore;
            MoneyBalanceAfter = moneyBalanceAfter;
            EquipmentConfirmed = equipmentConfirmed;
            RejectionCode = rejectionCode;
        }

        public StableId TransactionStableId { get; }
        public string CommandFingerprint { get; }
        public ShopPurchaseStatusV1 Status { get; }
        public ShopPurchaseStatusV1 OriginalStatus { get; }
        public StableId StockEntryStableId { get; }
        public long Price { get; }
        public long MoneyBalanceBefore { get; }
        public long MoneyBalanceAfter { get; }
        public bool EquipmentConfirmed { get; }
        public string RejectionCode { get; }
        public bool IsTerminal
        {
            get
            {
                return OriginalStatus != ShopPurchaseStatusV1.PurchasePending
                    && OriginalStatus != ShopPurchaseStatusV1.CompensationPending;
            }
        }

        public ShopPurchaseFactV1 AsExactDuplicate()
        {
            return new ShopPurchaseFactV1(
                TransactionStableId,
                CommandFingerprint,
                ShopPurchaseStatusV1.ExactDuplicateNoChange,
                OriginalStatus,
                StockEntryStableId,
                Price,
                MoneyBalanceBefore,
                MoneyBalanceAfter,
                EquipmentConfirmed,
                RejectionCode);
        }

        public ShopPurchaseFactV1 AsConflict()
        {
            return new ShopPurchaseFactV1(
                TransactionStableId,
                CommandFingerprint,
                ShopPurchaseStatusV1.ConflictingDuplicate,
                OriginalStatus,
                StockEntryStableId,
                Price,
                MoneyBalanceBefore,
                MoneyBalanceAfter,
                EquipmentConfirmed,
                "shop-purchase-conflicting-duplicate");
        }
    }

    public sealed class ShopRefreshCommandV1
    {
        private readonly ReadOnlyCollection<StableId> lockedEntryStableIds;
        private readonly string canonicalText;

        private ShopRefreshCommandV1(
            StableId transactionStableId,
            StableId runStableId,
            StableId shopStableId,
            string inventoryFingerprint,
            ProgressionContext requestedContext,
            IEnumerable<StableId> lockedEntryStableIds)
        {
            TransactionStableId = transactionStableId
                ?? throw new ArgumentNullException(nameof(transactionStableId));
            RunStableId = runStableId ?? throw new ArgumentNullException(nameof(runStableId));
            ShopStableId = shopStableId ?? throw new ArgumentNullException(nameof(shopStableId));
            InventoryFingerprint = ShopStockEntryV1.RequireFingerprint(
                inventoryFingerprint,
                nameof(inventoryFingerprint));
            RequestedContext = requestedContext ?? throw new ArgumentNullException(nameof(requestedContext));
            this.lockedEntryStableIds = CopyIds(lockedEntryStableIds);
            StringBuilder builder = new StringBuilder();
            builder.Append("schema=shop-refresh-command-v1")
                .Append("\ntransaction_id=").Append(TransactionStableId)
                .Append("\nrun_id=").Append(RunStableId)
                .Append("\nshop_id=").Append(ShopStableId)
                .Append("\ninventory_fingerprint=").Append(InventoryFingerprint)
                .Append("\nrequested_context_fingerprint=").Append(RequestedContext.Fingerprint)
                .Append("\nlocked_count=").Append(this.lockedEntryStableIds.Count.ToString(CultureInfo.InvariantCulture));
            for (int index = 0; index < this.lockedEntryStableIds.Count; index++)
            {
                builder.Append("\nlocked_").Append(index.ToString("D4", CultureInfo.InvariantCulture))
                    .Append('=').Append(this.lockedEntryStableIds[index]);
            }

            canonicalText = builder.ToString();
            Fingerprint = ShopCanonicalV1.Fingerprint(canonicalText);
        }

        public StableId TransactionStableId { get; }
        public StableId RunStableId { get; }
        public StableId ShopStableId { get; }
        public string InventoryFingerprint { get; }
        public ProgressionContext RequestedContext { get; }
        public IReadOnlyList<StableId> LockedEntryStableIds { get { return lockedEntryStableIds; } }
        public string Fingerprint { get; }

        public static ShopRefreshCommandV1 Create(
            StableId transactionStableId,
            StableId runStableId,
            StableId shopStableId,
            string inventoryFingerprint,
            ProgressionContext requestedContext,
            IEnumerable<StableId> lockedEntryStableIds = null)
        {
            return new ShopRefreshCommandV1(
                transactionStableId,
                runStableId,
                shopStableId,
                inventoryFingerprint,
                requestedContext,
                lockedEntryStableIds);
        }

        public string ToCanonicalString() { return canonicalText; }

        private static ReadOnlyCollection<StableId> CopyIds(IEnumerable<StableId> source)
        {
            SortedSet<StableId> ids = new SortedSet<StableId>();
            if (source != null)
            {
                foreach (StableId id in source)
                {
                    if (id == null)
                    {
                        throw new ArgumentException(
                            "Locked entry identities must not contain null entries.",
                            nameof(source));
                    }

                    ids.Add(id);
                }
            }

            return new ReadOnlyCollection<StableId>(new List<StableId>(ids));
        }
    }

    public sealed class ShopRefreshFactV1
    {
        public ShopRefreshFactV1(
            StableId transactionStableId,
            string commandFingerprint,
            ShopRefreshStatusV1 status,
            ShopRefreshStatusV1 originalStatus,
            int previousRefreshOrdinal,
            int currentRefreshOrdinal,
            string previousInventoryFingerprint,
            string currentInventoryFingerprint,
            string rejectionCode)
        {
            TransactionStableId = transactionStableId;
            CommandFingerprint = commandFingerprint;
            if (!Enum.IsDefined(typeof(ShopRefreshStatusV1), status)
                || !Enum.IsDefined(typeof(ShopRefreshStatusV1), originalStatus))
            {
                throw new ArgumentOutOfRangeException();
            }

            Status = status;
            OriginalStatus = originalStatus;
            PreviousRefreshOrdinal = previousRefreshOrdinal;
            CurrentRefreshOrdinal = currentRefreshOrdinal;
            PreviousInventoryFingerprint = previousInventoryFingerprint;
            CurrentInventoryFingerprint = currentInventoryFingerprint;
            RejectionCode = rejectionCode;
        }

        public StableId TransactionStableId { get; }
        public string CommandFingerprint { get; }
        public ShopRefreshStatusV1 Status { get; }
        public ShopRefreshStatusV1 OriginalStatus { get; }
        public int PreviousRefreshOrdinal { get; }
        public int CurrentRefreshOrdinal { get; }
        public string PreviousInventoryFingerprint { get; }
        public string CurrentInventoryFingerprint { get; }
        public string RejectionCode { get; }

        public ShopRefreshFactV1 AsExactDuplicate()
        {
            return new ShopRefreshFactV1(
                TransactionStableId,
                CommandFingerprint,
                ShopRefreshStatusV1.ExactDuplicateNoChange,
                OriginalStatus,
                PreviousRefreshOrdinal,
                CurrentRefreshOrdinal,
                PreviousInventoryFingerprint,
                CurrentInventoryFingerprint,
                RejectionCode);
        }

        public ShopRefreshFactV1 AsConflict()
        {
            return new ShopRefreshFactV1(
                TransactionStableId,
                CommandFingerprint,
                ShopRefreshStatusV1.ConflictingDuplicate,
                OriginalStatus,
                PreviousRefreshOrdinal,
                CurrentRefreshOrdinal,
                PreviousInventoryFingerprint,
                CurrentInventoryFingerprint,
                "shop-refresh-conflicting-duplicate");
        }
    }

    public sealed class ShopRunInventorySnapshotV1 : IComparable<ShopRunInventorySnapshotV1>
    {
        private readonly ReadOnlyCollection<ShopStockEntryV1> entries;

        public ShopRunInventorySnapshotV1(
            StableId runStableId,
            StableId shopStableId,
            int refreshOrdinal,
            ulong inventorySeed,
            string definitionFingerprint,
            ProgressionContext firstOpenContext,
            ProgressionContext inventoryContext,
            string inventoryFingerprint,
            IEnumerable<ShopStockEntryV1> entries)
        {
            RunStableId = runStableId ?? throw new ArgumentNullException(nameof(runStableId));
            ShopStableId = shopStableId ?? throw new ArgumentNullException(nameof(shopStableId));
            RefreshOrdinal = refreshOrdinal;
            InventorySeed = inventorySeed;
            DefinitionFingerprint = definitionFingerprint;
            FirstOpenContext = firstOpenContext ?? throw new ArgumentNullException(nameof(firstOpenContext));
            InventoryContext = inventoryContext ?? throw new ArgumentNullException(nameof(inventoryContext));
            InventoryFingerprint = inventoryFingerprint;
            this.entries = new ReadOnlyCollection<ShopStockEntryV1>(
                new List<ShopStockEntryV1>(entries ?? throw new ArgumentNullException(nameof(entries))));
        }

        public StableId RunStableId { get; }
        public StableId ShopStableId { get; }
        public int RefreshOrdinal { get; }
        public ulong InventorySeed { get; }
        public string DefinitionFingerprint { get; }
        public ProgressionContext FirstOpenContext { get; }
        public ProgressionContext InventoryContext { get; }
        public string InventoryFingerprint { get; }
        public IReadOnlyList<ShopStockEntryV1> Entries { get { return entries; } }

        public int CompareTo(ShopRunInventorySnapshotV1 other)
        {
            if (ReferenceEquals(other, null)) { return 1; }
            int run = RunStableId.CompareTo(other.RunStableId);
            return run != 0 ? run : ShopStableId.CompareTo(other.ShopStableId);
        }
    }

    public sealed class ShopRuntimeSnapshotV1
    {
        public const int CurrentSchemaVersion = 1;
        private readonly ReadOnlyCollection<ShopRunInventorySnapshotV1> inventories;

        public ShopRuntimeSnapshotV1(
            int schemaVersion,
            IEnumerable<ShopRunInventorySnapshotV1> inventories,
            string fingerprint)
        {
            SchemaVersion = schemaVersion;
            List<ShopRunInventorySnapshotV1> copy = new List<ShopRunInventorySnapshotV1>(
                inventories ?? throw new ArgumentNullException(nameof(inventories)));
            copy.Sort();
            this.inventories = new ReadOnlyCollection<ShopRunInventorySnapshotV1>(copy);
            Fingerprint = fingerprint;
        }

        public int SchemaVersion { get; }
        public IReadOnlyList<ShopRunInventorySnapshotV1> Inventories { get { return inventories; } }
        public string Fingerprint { get; }

        public static ShopRuntimeSnapshotV1 CreateCanonical(
            IEnumerable<ShopRunInventorySnapshotV1> inventories)
        {
            ShopRuntimeSnapshotV1 provisional = new ShopRuntimeSnapshotV1(
                CurrentSchemaVersion,
                inventories,
                string.Empty);
            return new ShopRuntimeSnapshotV1(
                CurrentSchemaVersion,
                provisional.Inventories,
                ComputeFingerprint(provisional));
        }

        public static string ComputeFingerprint(ShopRuntimeSnapshotV1 snapshot)
        {
            StringBuilder builder = new StringBuilder();
            builder.Append("schema=shop-runtime-snapshot-v1")
                .Append("\nschema_version=").Append(snapshot.SchemaVersion.ToString(CultureInfo.InvariantCulture))
                .Append("\ninventory_count=").Append(snapshot.Inventories.Count.ToString(CultureInfo.InvariantCulture));
            for (int index = 0; index < snapshot.Inventories.Count; index++)
            {
                ShopRunInventorySnapshotV1 item = snapshot.Inventories[index];
                builder.Append("\ninventory_").Append(index.ToString("D4", CultureInfo.InvariantCulture))
                    .Append('=').Append(item.RunStableId)
                    .Append('|').Append(item.ShopStableId)
                    .Append('|').Append(item.RefreshOrdinal.ToString(CultureInfo.InvariantCulture))
                    .Append('|').Append(item.InventorySeed.ToString(CultureInfo.InvariantCulture))
                    .Append('|').Append(item.DefinitionFingerprint)
                    .Append('|').Append(item.FirstOpenContext.Fingerprint)
                    .Append('|').Append(item.InventoryContext.Fingerprint)
                    .Append('|').Append(item.InventoryFingerprint)
                    .Append('|').Append(item.Entries.Count.ToString(CultureInfo.InvariantCulture));
                for (int entryIndex = 0; entryIndex < item.Entries.Count; entryIndex++)
                {
                    builder.Append('|').Append(item.Entries[entryIndex].ToCanonicalString().Replace("\n", "\\n"));
                }
            }

            return ShopCanonicalV1.Fingerprint(builder.ToString());
        }
    }
}
