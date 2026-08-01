using System;
using System.Collections.Generic;
using System.Globalization;
using ShooterMover.Application.Rewards.Drops;
using ShooterMover.Application.Rewards.Strongboxes;
using ShooterMover.Contracts.Rewards;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Common.Random;
using ShooterMover.Domain.Equipment;
using ShooterMover.Domain.Rewards.Model;
using ShooterMover.Domain.Rewards.Strongboxes;
using ShooterMover.Domain.Guns.Catalog;

namespace ShooterMover.Application.Shops
{
    /// <summary>
    /// Treats each stock slot as one deterministic virtual strongbox. It reuses the
    /// production tier profile and hybrid equipment resolver, but does not add a box to
    /// holdings or invoke BOX opening. The generated augment signature remains staged
    /// until the existing RAP holdings child commits the purchased equipment.
    /// </summary>
    public sealed class StrongboxShopStockRoller :
        IShopStockRoller
    {
        private static readonly StableId SourceStableId =
            StableId.Parse("source.shop-virtual-strongbox");
        private static readonly StableId TierPurposeId =
            StableId.Parse("shop-rng.virtual-strongbox-tier-v1");

        private readonly StrongboxHybridEquipmentGenerationResolver resolver;
        private readonly GeneratedEquipmentAugmentSignatureState signatures;
        private readonly StableId generationPolicyStableId;

        public StrongboxShopStockRoller(
            EquipmentCatalog equipmentCatalog,
            GunCatalog gunCatalog,
            GeneratedEquipmentAugmentSignatureState signatures,
            StableId generationPolicyStableId)
        {
            resolver = new StrongboxHybridEquipmentGenerationResolver(
                equipmentCatalog
                    ?? throw new ArgumentNullException(nameof(equipmentCatalog)),
                gunCatalog
                    ?? throw new ArgumentNullException(nameof(gunCatalog)),
                signatures
                    ?? throw new ArgumentNullException(nameof(signatures)));
            this.signatures = signatures;
            this.generationPolicyStableId = generationPolicyStableId
                ?? throw new ArgumentNullException(
                    nameof(generationPolicyStableId));
        }

        public bool TryRoll(
            ShopStockRollRequest request,
            out ShopStockRollResult result,
            out string rejectionCode)
        {
            result = null;
            rejectionCode = null;
            if (request == null)
            {
                rejectionCode = "shop-strongbox-roll-request-null";
                return false;
            }

            StableId tierId;
            try
            {
                tierId = StrongboxTierSelectionCatalog.SelectExactTier(
                    StrongboxTierSelectionCatalog.ShopSourceProfileId,
                    Array.Empty<StableId>(),
                    request.InventorySeed,
                    request.Definition.AlgorithmVersion,
                    checked((ulong)request.SlotIndex));
            }
            catch (Exception exception)
            {
                rejectionCode = "shop-strongbox-tier-roll-exception-"
                    + exception.GetType().Name.ToLowerInvariant();
                return false;
            }

            StrongboxTier tier = FindTier(tierId);
            if (tier == null)
            {
                rejectionCode = "shop-strongbox-tier-unavailable";
                return false;
            }
            StrongboxDefinition definition = tier.CreateDefinition(
                generationPolicyStableId);

            string ordinal = request.RefreshOrdinal.ToString(
                CultureInfo.InvariantCulture);
            string slot = request.SlotIndex.ToString(
                CultureInfo.InvariantCulture);
            StableId boxId = Strongbox.DeriveId(
                "shopbox",
                request.RunStableId.ToString(),
                request.Definition.ShopStableId.ToString(),
                ordinal,
                slot);
            StableId sourceOperationId = Strongbox.DeriveId(
                "shopsourceop",
                boxId.ToString());
            StableId commitmentId = Strongbox.DeriveId(
                "shopcommit",
                boxId.ToString());
            StableId operationId = Strongbox.DeriveId(
                "shoprollop",
                boxId.ToString());
            StableId grantId = Strongbox.DeriveId(
                "shoprollgrant",
                boxId.ToString());
            StableId collectionId = Strongbox.DeriveId(
                "shopvirtualcollection",
                boxId.ToString());
            ulong rootSeed = DeriveSeed(
                request.InventorySeed,
                request.SlotIndex);

            StrongboxInstanceContext context =
                StrongboxInstanceContext.Create(
                    boxId,
                    tierId,
                    rootSeed,
                    request.Definition.AlgorithmVersion,
                    request.ProgressionContext,
                    SourceStableId,
                    collectionId,
                    definition.Fingerprint);
            string contentFingerprint =
                ShooterMover.Domain.Shops.Shop.Fingerprint(
                    "schema=shop-virtual-strongbox-roll-v1"
                    + "\nshop_id=" + request.Definition.ShopStableId
                    + "\nrun_id=" + request.RunStableId
                    + "\nslot=" + slot
                    + "\ntier_id=" + tierId
                    + "\ncontext=" + context.Fingerprint);
            RewardOperationRequest operation = RewardOperationRequest.Create(
                operationId,
                request.Definition.ShopStableId,
                sourceOperationId,
                commitmentId,
                TierPurposeId,
                contentFingerprint);
            RewardGrant grant = RewardGrant.Create(
                grantId,
                RewardGrantKind.EquipmentReference,
                tierId,
                1L);

            IReadOnlyList<EquipmentInstance> equipment;
            if (!resolver.TryResolve(
                    definition,
                    context,
                    operation,
                    grant,
                    out equipment,
                    out rejectionCode)
                || equipment == null
                || equipment.Count != 1
                || equipment[0] == null)
            {
                if (string.IsNullOrWhiteSpace(rejectionCode))
                {
                    rejectionCode =
                        "shop-strongbox-equipment-roll-rejected";
                }
                return false;
            }

            GeneratedEquipmentAugmentSignature signature;
            bool committed;
            if (!signatures.TryGetStagedOrCommitted(
                    equipment[0].InstanceId,
                    out signature,
                    out committed)
                || signature == null)
            {
                rejectionCode =
                    "shop-strongbox-augment-signature-missing";
                return false;
            }

            string generationFingerprint =
                ShooterMover.Domain.Shops.Shop.Fingerprint(
                    "schema=shop-strongbox-stock-result-v1"
                    + "\ntier_id=" + tierId
                    + "\nbox_context=" + context.Fingerprint
                    + "\nequipment=" + equipment[0].Fingerprint
                    + "\naugment_signature=" + signature.Fingerprint);
            result = new ShopStockRollResult(
                equipment[0],
                generationFingerprint,
                tierId);
            return true;
        }

        private static StrongboxTier FindTier(StableId tierStableId)
        {
            for (int index = 0;
                 index < StrongboxCatalog.Tiers.Count;
                 index++)
            {
                StrongboxTier tier = StrongboxCatalog.Tiers[index];
                if (tier.TierStableId == tierStableId)
                {
                    return tier;
                }
            }
            return null;
        }

        private static ulong DeriveSeed(
            ulong inventorySeed,
            int slotIndex)
        {
            DeterministicRandom random =
                DeterministicRandom.Create(inventorySeed)
                    .Fork(
                        StableId.Parse("shop.virtual-strongbox"),
                        checked((ulong)slotIndex));
            ulong seed;
            random.NextUInt64(out seed);
            return seed;
        }
    }
}
