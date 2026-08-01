using System;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Equipment;
using ShooterMover.Domain.Progression.Context;
using ShooterMover.Domain.Shops;

namespace ShooterMover.Application.Shops
{
    public sealed class ShopOfferRequest
    {
        public ShopOfferRequest(
            StableId stockId,
            ShopDefinition definition,
            EquipmentCatalog catalog,
            ProgressionContext progressionContext,
            ulong inventorySeed,
            int revision,
            int slotIndex)
        {
            StockId = stockId
                ?? throw new ArgumentNullException(nameof(stockId));
            Definition = definition
                ?? throw new ArgumentNullException(nameof(definition));
            Catalog = catalog
                ?? throw new ArgumentNullException(nameof(catalog));
            ProgressionContext = progressionContext
                ?? throw new ArgumentNullException(nameof(progressionContext));
            if (revision < 0 || slotIndex < 0)
            {
                throw new ArgumentOutOfRangeException();
            }
            InventorySeed = inventorySeed;
            Revision = revision;
            SlotIndex = slotIndex;
        }

        public StableId StockId { get; }
        public ShopDefinition Definition { get; }
        public EquipmentCatalog Catalog { get; }
        public ProgressionContext ProgressionContext { get; }
        public ulong InventorySeed { get; }
        public int Revision { get; }
        public int SlotIndex { get; }
    }

    public sealed class ShopOfferRoll
    {
        public ShopOfferRoll(
            EquipmentInstance equipment,
            string fingerprint)
        {
            Equipment = equipment
                ?? throw new ArgumentNullException(nameof(equipment));
            if (string.IsNullOrWhiteSpace(fingerprint))
            {
                throw new ArgumentException(
                    "An offer fingerprint is required.",
                    nameof(fingerprint));
            }
            Fingerprint = fingerprint;
        }

        public EquipmentInstance Equipment { get; }
        public string Fingerprint { get; }
    }

    public interface IShopOfferRoller
    {
        bool TryRoll(
            ShopOfferRequest request,
            out ShopOfferRoll result,
            out string rejectionCode);
    }
}
