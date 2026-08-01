using System;
using ShooterMover.Application.Shops;
using ShooterMover.Domain.Shops;

namespace ShooterMover.Application.Persistence.SaveParts
{
    public static class ShopPurchaseSavePart
    {
        private static readonly ShopReceiptCodec CodecValue =
            new ShopReceiptCodec();

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

        public static ShopReceiptCodec Codec
        {
            get { return CodecValue; }
        }

        public static ISavePart CreateAdapter(
            ShopReceipts receipts)
        {
            if (receipts == null)
            {
                throw new ArgumentNullException(nameof(receipts));
            }

            return new SnapshotSavePart<ShopReceiptSnapshot>(
                Definition(),
                CodecValue,
                receipts.ExportSnapshot,
                CodecValue.Validate,
                snapshot =>
                {
                    string rejectionCode;
                    return receipts.TryImportSnapshot(
                            snapshot,
                            out rejectionCode)
                        ? SavePartApplyResult.Applied()
                        : SavePartApplyResult.Rejected(rejectionCode);
                });
        }
    }

    public sealed class ShopReceiptCodec :
        ExplicitSavePartCodec<ShopReceiptSnapshot>
    {
        public ShopReceiptCodec()
            : base("shop-purchase-receipts-explicit-v1")
        {
        }

        public override SavePartValidationResult Validate(
            ShopReceiptSnapshot snapshot)
        {
            if (snapshot == null)
            {
                return SavePartValidationResult.Reject(
                    "shop-receipt-snapshot-null");
            }
            if (snapshot.SchemaVersion
                != ShopReceiptSnapshot.CurrentSchemaVersion)
            {
                return SavePartValidationResult.Reject(
                    "shop-receipt-schema-unsupported");
            }
            if (!string.Equals(
                    snapshot.Fingerprint,
                    ShooterMover.Domain.Shops.Shop.Fingerprint(
                        snapshot.ToCanonicalString()),
                    StringComparison.Ordinal))
            {
                return SavePartValidationResult.Reject(
                    "shop-receipt-fingerprint-mismatch");
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
                        "shop-receipt-invalid");
                }
            }
            return SavePartValidationResult.Accept();
        }

        protected override Node EncodeNode(
            ShopReceiptSnapshot snapshot)
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

        protected override ShopReceiptSnapshot DecodeNode(
            Node node)
        {
            var reader = new ObjectReader(
                node,
                "schema_version",
                "receipts");
            int schemaVersion = Value.ReadInt32(
                reader.Next("schema_version"));
            if (schemaVersion
                != ShopReceiptSnapshot.CurrentSchemaVersion)
            {
                throw new PayloadException(
                    "shop-receipt-schema-unsupported");
            }
            return new ShopReceiptSnapshot(
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
