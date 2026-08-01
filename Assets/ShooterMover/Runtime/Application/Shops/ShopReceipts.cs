using System;
using System.Collections.Generic;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Shops;

namespace ShooterMover.Application.Shops
{
    public sealed class ShopReceipts
    {
        private readonly object sync = new object();
        private readonly Dictionary<StableId, StableId> receipts =
            new Dictionary<StableId, StableId>();

        public bool TryGet(
            StableId stockEntryStableId,
            out StableId purchaseTransactionStableId)
        {
            lock (sync)
            {
                if (stockEntryStableId != null
                    && receipts.TryGetValue(
                        stockEntryStableId,
                        out purchaseTransactionStableId))
                {
                    return true;
                }
                purchaseTransactionStableId = null;
                return false;
            }
        }

        public bool TryRecord(
            StableId stockEntryStableId,
            StableId purchaseTransactionStableId,
            out string rejectionCode)
        {
            lock (sync)
            {
                rejectionCode = null;
                if (stockEntryStableId == null
                    || purchaseTransactionStableId == null)
                {
                    rejectionCode = "shop-purchase-receipt-input-null";
                    return false;
                }

                StableId existing;
                if (receipts.TryGetValue(
                        stockEntryStableId,
                        out existing))
                {
                    if (existing == purchaseTransactionStableId)
                    {
                        return true;
                    }
                    rejectionCode = "shop-purchase-receipt-conflict";
                    return false;
                }

                receipts.Add(
                    stockEntryStableId,
                    purchaseTransactionStableId);
                return true;
            }
        }

        public ShopReceiptSnapshot ExportSnapshot()
        {
            lock (sync)
            {
                var snapshot = new List<ShopPurchaseReceipt>(
                    receipts.Count);
                foreach (KeyValuePair<StableId, StableId> pair
                    in receipts)
                {
                    snapshot.Add(new ShopPurchaseReceipt(
                        pair.Key,
                        pair.Value));
                }
                return new ShopReceiptSnapshot(snapshot);
            }
        }

        public bool TryImportSnapshot(
            ShopReceiptSnapshot snapshot,
            out string rejectionCode)
        {
            lock (sync)
            {
                rejectionCode = null;
                if (snapshot == null)
                {
                    rejectionCode = "shop-receipt-snapshot-null";
                    return false;
                }
                if (snapshot.SchemaVersion
                    != ShopReceiptSnapshot.CurrentSchemaVersion)
                {
                    rejectionCode =
                        "shop-receipt-schema-unsupported";
                    return false;
                }
                if (!string.Equals(
                        snapshot.Fingerprint,
                        ShooterMover.Domain.Shops.Shop.Fingerprint(
                            snapshot.ToCanonicalString()),
                        StringComparison.Ordinal))
                {
                    rejectionCode =
                        "shop-receipt-fingerprint-mismatch";
                    return false;
                }

                var replacement = new Dictionary<StableId, StableId>();
                for (int index = 0;
                     index < snapshot.Receipts.Count;
                     index++)
                {
                    ShopPurchaseReceipt receipt =
                        snapshot.Receipts[index];
                    if (receipt == null
                        || replacement.ContainsKey(
                            receipt.StockEntryStableId))
                    {
                        rejectionCode =
                            "shop-receipt-invalid";
                        return false;
                    }
                    replacement.Add(
                        receipt.StockEntryStableId,
                        receipt.PurchaseTransactionStableId);
                }

                receipts.Clear();
                foreach (KeyValuePair<StableId, StableId> pair
                    in replacement)
                {
                    receipts.Add(pair.Key, pair.Value);
                }
                return true;
            }
        }
    }
}
