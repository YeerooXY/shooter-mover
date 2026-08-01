using System;
using System.Collections.Generic;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Shops;

namespace ShooterMover.Application.Shops
{
    public sealed class ShopPurchaseLedger
    {
        private readonly object sync = new object();
        private readonly Dictionary<StableId, StableId> purchases =
            new Dictionary<StableId, StableId>();

        public bool TryGet(
            StableId stockEntryStableId,
            out StableId purchaseTransactionStableId)
        {
            lock (sync)
            {
                if (stockEntryStableId != null
                    && purchases.TryGetValue(
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
                if (purchases.TryGetValue(
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

                purchases.Add(
                    stockEntryStableId,
                    purchaseTransactionStableId);
                return true;
            }
        }

        public ShopPurchaseLedgerSnapshot ExportSnapshot()
        {
            lock (sync)
            {
                var receipts = new List<ShopPurchaseReceipt>(
                    purchases.Count);
                foreach (KeyValuePair<StableId, StableId> pair
                    in purchases)
                {
                    receipts.Add(new ShopPurchaseReceipt(
                        pair.Key,
                        pair.Value));
                }
                return new ShopPurchaseLedgerSnapshot(receipts);
            }
        }

        public bool TryImportSnapshot(
            ShopPurchaseLedgerSnapshot snapshot,
            out string rejectionCode)
        {
            lock (sync)
            {
                rejectionCode = null;
                if (snapshot == null)
                {
                    rejectionCode = "shop-purchase-ledger-snapshot-null";
                    return false;
                }
                if (snapshot.SchemaVersion
                    != ShopPurchaseLedgerSnapshot.CurrentSchemaVersion)
                {
                    rejectionCode =
                        "shop-purchase-ledger-schema-unsupported";
                    return false;
                }
                if (!string.Equals(
                        snapshot.Fingerprint,
                        ShooterMover.Domain.Shops.Shop.Fingerprint(
                            snapshot.ToCanonicalString()),
                        StringComparison.Ordinal))
                {
                    rejectionCode =
                        "shop-purchase-ledger-fingerprint-mismatch";
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
                            "shop-purchase-ledger-receipt-invalid";
                        return false;
                    }
                    replacement.Add(
                        receipt.StockEntryStableId,
                        receipt.PurchaseTransactionStableId);
                }

                purchases.Clear();
                foreach (KeyValuePair<StableId, StableId> pair
                    in replacement)
                {
                    purchases.Add(pair.Key, pair.Value);
                }
                return true;
            }
        }
    }
}
