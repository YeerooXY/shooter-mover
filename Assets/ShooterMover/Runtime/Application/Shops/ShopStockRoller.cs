using System;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Equipment;
using ShooterMover.Domain.Progression.Context;
using ShooterMover.Domain.Shops;

namespace ShooterMover.Application.Shops
{
    public sealed class ShopStockRollRequest
    {
        public ShopStockRollRequest(
            StableId runStableId,
            ShopDefinition definition,
            EquipmentCatalog catalog,
            ProgressionContext progressionContext,
            ulong inventorySeed,
            int refreshOrdinal,
            int slotIndex)
        {
            RunStableId = runStableId
                ?? throw new ArgumentNullException(nameof(runStableId));
            Definition = definition
                ?? throw new ArgumentNullException(nameof(definition));
            Catalog = catalog
                ?? throw new ArgumentNullException(nameof(catalog));
            ProgressionContext = progressionContext
                ?? throw new ArgumentNullException(nameof(progressionContext));
            if (refreshOrdinal < 0 || slotIndex < 0)
            {
                throw new ArgumentOutOfRangeException();
            }
            InventorySeed = inventorySeed;
            RefreshOrdinal = refreshOrdinal;
            SlotIndex = slotIndex;
        }

        public StableId RunStableId { get; }
        public ShopDefinition Definition { get; }
        public EquipmentCatalog Catalog { get; }
        public ProgressionContext ProgressionContext { get; }
        public ulong InventorySeed { get; }
        public int RefreshOrdinal { get; }
        public int SlotIndex { get; }
    }

    public sealed class ShopStockRollResult
    {
        public ShopStockRollResult(
            EquipmentInstance equipment,
            string generationFingerprint,
            StableId sourceStrongboxTierStableId)
        {
            Equipment = equipment
                ?? throw new ArgumentNullException(nameof(equipment));
            if (string.IsNullOrWhiteSpace(generationFingerprint))
            {
                throw new ArgumentException(
                    "A generation fingerprint is required.",
                    nameof(generationFingerprint));
            }
            GenerationFingerprint = generationFingerprint;
            SourceStrongboxTierStableId =
                sourceStrongboxTierStableId;
        }

        public EquipmentInstance Equipment { get; }
        public string GenerationFingerprint { get; }
        public StableId SourceStrongboxTierStableId { get; }
    }

    public interface IShopStockRoller
    {
        bool TryRoll(
            ShopStockRollRequest request,
            out ShopStockRollResult result,
            out string rejectionCode);
    }
}
