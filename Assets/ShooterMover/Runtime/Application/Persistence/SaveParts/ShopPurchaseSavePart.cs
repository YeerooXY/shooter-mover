using System;
using ShooterMover.Application.Shops;
using ShooterMover.Domain.Shops;

namespace ShooterMover.Application.Persistence.SaveParts
{
    public static class ShopPurchaseSavePart
    {
        private static readonly ShopPurchaseCodec CodecValue =
            new ShopPurchaseCodec();

        public static SavePartDefinition Definition()
        {
            return new SavePartDefinition(
                ShooterMover.Domain.Common.StableId.Create(
                    "save-part",
                    "shop-purchase-receipts"),
                1,
                "shop-purchase-receipts-explicit-v1",
                false,
                660);
        }

        public static ShopPurchaseCodec Codec
        {
            get { return CodecValue; }
        }

        public static ISavePart CreateAdapter(
            ShopPurchaseLedger authority)
        {
            if (authority == null)
            {
                throw new ArgumentNullException(nameof(authority));
            }

            return new SnapshotSavePart<ShopPurchaseLedgerSnapshot>(
                Definition(),
                CodecValue,
                authority.ExportSnapshot,
                CodecValue.Validate,
                snapshot =>
                {
                    string rejectionCode;
                    return authority.TryImportSnapshot(
                            snapshot,
                            out rejectionCode)
                        ? SavePartApplyResult.Applied()
                        : SavePartApplyResult.Rejected(rejectionCode);
                });
        }
    }

    public sealed class ShopPurchaseCodec :
        ExplicitSavePartCodec<ShopPurchaseLedgerSnapshot>
    {
        public ShopPurchaseCodec()
            : base("shop-purchase-receipts-explicit-v1")
        {
        }

        public override SavePartValidationResult Validate(
            ShopPurchaseLedgerSnapshot snapshot)
        {
            if (snapshot == null)
            {
                return SavePartValidationResult.Reject(
                    "shop-purchase-ledger-snapshot-null");
            }
            if (snapshot.SchemaVersion
                != ShopPurchaseLedgerSnapshot.CurrentSchemaVersion)
            {
                return SavePartValidationResult.Reject(
                    "shop-purchase-ledger-schema-unsupported");
            }
            if (!string.Equals(
                    snapshot.Fingerprint,
                    ShooterMover.Domain.Shops.Shop.Fingerprint(
                        snapshot.ToCanonicalString()),
                    StringComparison.Ordinal))
            {
                return SavePartValidationResult.Reject(
                    "shop-purchase-ledger-fingerprint-mismatch");
            }
            for (int index = 0;
                 index < snapshot.Receipts.Count;
                 index++)
            {
                ShopPurchaseReceipt receipt = snapshot.Receipts[index];
                if (receipt == null
                    || !string.Equals(
                        receipt.Fingerprint,
                        ShooterMover.Domain.Shops.Shop.Fingerprint(
                            receipt.ToCanonicalString()),
                        StringComparison.Ordinal))
                {
                    return SavePartValidationResult.Reject(
                        "shop-purchase-ledger-receipt-invalid");
                }
            }
            return SavePartValidationResult.Accept();
        }

        protected override Node EncodeNode(
            ShopPurchaseLedgerSnapshot snapshot)
        {
            return Node.Object(
                Value.Field(
                    "schema_version",
                    Value.Int32(snapshot.SchemaVersion)),
                Value.Field(
                    "receipts",
                    ExplicitCodecValues.EncodeList(
                        snapshot.Receipts,
                        EncodeReceipt)));
        }

        protected override ShopPurchaseLedgerSnapshot DecodeNode(
            Node node)
        {
            var reader = new ObjectReader(
                node,
                "schema_version",
                "receipts");
            int schemaVersion = Value.ReadInt32(
                reader.Next("schema_version"));
            if (schemaVersion
                != ShopPurchaseLedgerSnapshot.CurrentSchemaVersion)
            {
                throw new PayloadException(
                    "shop-purchase-ledger-schema-unsupported");
            }
            return new ShopPurchaseLedgerSnapshot(
                ExplicitCodecValues.DecodeList(
                    reader.Next("receipts"),
                    DecodeReceipt),
                schemaVersion);
        }

        private static Node EncodeReceipt(
            ShopPurchaseReceipt receipt)
        {
            return Node.Object(
                Value.Field(
                    "stock_entry_id",
                    ExplicitCodecValues.RequiredIdNode(
                        receipt.StockEntryStableId)),
                Value.Field(
                    "purchase_transaction_id",
                    ExplicitCodecValues.RequiredIdNode(
                        receipt.PurchaseTransactionStableId)));
        }

        private static ShopPurchaseReceipt DecodeReceipt(Node node)
        {
            var reader = new ObjectReader(
                node,
                "stock_entry_id",
                "purchase_transaction_id");
            return new ShopPurchaseReceipt(
                ExplicitCodecValues.RequiredId(
                    reader.Next("stock_entry_id")),
                ExplicitCodecValues.RequiredId(
                    reader.Next("purchase_transaction_id")));
        }
    }
}
