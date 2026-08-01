using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using ShooterMover.Domain.Common;

namespace ShooterMover.Domain.Shops
{
    public sealed class ShopPurchaseReceipt :
        IComparable<ShopPurchaseReceipt>
    {
        private readonly string canonicalText;

        public ShopPurchaseReceipt(
            StableId stockEntryStableId,
            StableId purchaseTransactionStableId)
        {
            StockEntryStableId = stockEntryStableId
                ?? throw new ArgumentNullException(nameof(stockEntryStableId));
            PurchaseTransactionStableId = purchaseTransactionStableId
                ?? throw new ArgumentNullException(
                    nameof(purchaseTransactionStableId));
            canonicalText = "stock_entry_id=" + StockEntryStableId
                + "\npurchase_transaction_id="
                + PurchaseTransactionStableId;
            Fingerprint = Shop.Fingerprint(canonicalText);
        }

        public StableId StockEntryStableId { get; }
        public StableId PurchaseTransactionStableId { get; }
        public string Fingerprint { get; }

        public string ToCanonicalString()
        {
            return canonicalText;
        }

        public int CompareTo(ShopPurchaseReceipt other)
        {
            return ReferenceEquals(other, null)
                ? 1
                : StockEntryStableId.CompareTo(
                    other.StockEntryStableId);
        }
    }

    public sealed class ShopPurchaseLedgerSnapshot
    {
        public const int CurrentSchemaVersion = 1;
        private readonly ReadOnlyCollection<ShopPurchaseReceipt> receipts;

        public ShopPurchaseLedgerSnapshot(
            IEnumerable<ShopPurchaseReceipt> receipts,
            int schemaVersion = CurrentSchemaVersion)
        {
            if (schemaVersion != CurrentSchemaVersion)
            {
                throw new ArgumentOutOfRangeException(nameof(schemaVersion));
            }

            SchemaVersion = schemaVersion;
            var copy = new List<ShopPurchaseReceipt>(
                receipts ?? throw new ArgumentNullException(nameof(receipts)));
            copy.Sort();
            var ids = new HashSet<StableId>();
            for (int index = 0; index < copy.Count; index++)
            {
                if (copy[index] == null
                    || !ids.Add(copy[index].StockEntryStableId))
                {
                    throw new ArgumentException(
                        "Shop purchase receipts must be non-null and unique.",
                        nameof(receipts));
                }
            }
            this.receipts = new ReadOnlyCollection<ShopPurchaseReceipt>(copy);
            Fingerprint = Shop.Fingerprint(ToCanonicalString());
        }

        public int SchemaVersion { get; }
        public IReadOnlyList<ShopPurchaseReceipt> Receipts
        {
            get { return receipts; }
        }
        public string Fingerprint { get; }

        public string ToCanonicalString()
        {
            var builder = new StringBuilder();
            builder.Append("schema=shop-purchase-ledger-v1")
                .Append("\nschema_version=")
                .Append(SchemaVersion.ToString(CultureInfo.InvariantCulture))
                .Append("\nreceipt_count=")
                .Append(receipts.Count.ToString(CultureInfo.InvariantCulture));
            for (int index = 0; index < receipts.Count; index++)
            {
                builder.Append("\nreceipt_")
                    .Append(index.ToString("D4", CultureInfo.InvariantCulture))
                    .Append('=')
                    .Append(receipts[index].Fingerprint);
            }
            return builder.ToString();
        }
    }
}
