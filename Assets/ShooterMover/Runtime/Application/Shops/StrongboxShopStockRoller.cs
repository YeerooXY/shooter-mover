using System;
using System.Collections.Generic;
using System.Globalization;
using ShooterMover.Application.Rewards.Drops;
using ShooterMover.Application.Rewards.Strongboxes;
using ShooterMover.Contracts.Rewards;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Common.Random;
using ShooterMover.Domain.Equipment;
using ShooterMover.Domain.Guns.Catalog;
using ShooterMover.Domain.Rewards.Model;
using ShooterMover.Domain.Rewards.Strongboxes;

namespace ShooterMover.Application.Shops
{
    /// <summary>
    /// Treats each stock slot as one deterministic virtual strongbox. Preview signatures
    /// remain transient and window-scoped; the Shop RAP holdings child commits the exact
    /// purchased preview alongside equipment ownership.
    /// </summary>
    public sealed class StrongboxShopStockRoller :
        IShopStockRoller
    {
        private static readonly StableId SourceStableId =
            StableId.Parse("source.shop-virtual-strongbox");
        private static readonly StableId TierPurposeId =
            StableId.Parse("shop-rng.virtual-strongbox-tier-v1");

        private readonly object gate = new object();
        private readonly GeneratedEquipmentAugmentSignatureState
            previewSignatures =
                new GeneratedEquipmentAugmentSignatureState();
        private readonly StrongboxHybridEquipmentGenerationResolver resolver;
        private readonly StableId generationPolicyStableId;
        private StableId currentStockIdentity;

        public StrongboxShopStockRoller(
            EquipmentCatalog equipmentCatalog,
            GunCatalog gunCatalog,
            StableId generationPolicyStableId)
        {
            resolver = new StrongboxHybridEquipmentGenerationResolver(
                equipmentCatalog
                    ?? throw new ArgumentNullException(nameof(equipmentCatalog)),
                gunCatalog
                    ?? throw new ArgumentNullException(nameof(gunCatalog)),
                previewSignatures);
            this.generationPolicyStableId = generationPolicyStableId
                ?? throw new ArgumentNullException(
                    nameof(generationPolicyStableId));
        }

        public GeneratedEquipmentAugmentSignatureState PreviewSignatures
        {
            get { return previewSignatures; }
        }

        public bool TryRoll(
            ShopStockRollRequest request,
            out ShopStockRollResult result,
            out string rejectionCode)
        {
            lock (gate)
            {
                result = null;
                rejectionCode = null;
                if (request == null)
                {
                    rejectionCode = "shop-strongbox-roll-request-null";
                    return false;
                }

                ResetPreviewWindow(request.RunStableId);

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
                string contextFingerprint = Strongbox.Fingerprint(
                    context.ToCanonicalString());
                string contentFingerprint =
                    ShooterMover.Domain.Shops.Shop.Fingerprint(
                        "schema=shop-virtual-strongbox-roll-v1"
                        + "\nshop_id=" + request.Definition.ShopStableId
                        + "\nrun_id=" + request.RunStableId
                        + "\nslot=" + slot
                        + "\ntier_id=" + tierId
                        + "\ncontext=" + contextFingerprint);
                RewardOperationRequest operation = RewardOperationRequest.Create(
                    request.RunStableId,
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
                if (!previewSignatures.TryGetStagedOrCommitted(
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
                        + "\nbox_context=" + contextFingerprint
                        + "\nequipment=" + equipment[0].Fingerprint
                        + "\naugment_signature=" + signature.Fingerprint);
                result = new ShopStockRollResult(
                    equipment[0],
                    generationFingerprint,
                    tierId);
                return true;
            }
        }

        private void ResetPreviewWindow(StableId stockIdentity)
        {
            if (currentStockIdentity == stockIdentity)
            {
                return;
            }
            previewSignatures.RestoreDurableSnapshot(
                new GeneratedEquipmentAugmentSignatureSnapshot(
                    Array.Empty<GeneratedEquipmentAugmentSignature>(),
                    Array.Empty<GeneratedEquipmentAugmentSignature>()));
            currentStockIdentity = stockIdentity;
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
