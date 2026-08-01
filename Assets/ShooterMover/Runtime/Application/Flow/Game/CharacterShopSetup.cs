using System;
using System.Collections.Generic;
using ShooterMover.Application.Economy.Money;
using ShooterMover.Application.Economy.Scrap;
using ShooterMover.Application.Rewards.Application;
using ShooterMover.Application.Rewards.Generation;
using ShooterMover.Application.Rewards.Strongboxes;
using ShooterMover.Application.Shops;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Common.Random;
using ShooterMover.Domain.Equipment;
using ShooterMover.Domain.Progression.Curves;
using ShooterMover.Domain.Rewards.Generation;
using ShooterMover.Domain.Shops;

namespace ShooterMover.Application.Flow.Game
{
    public sealed class CharacterShopLive
    {
        public CharacterShopLive(
            ShopLiveActions authority,
            ShopDefinition definition,
            ShopReceipts receipts,
            GeneratedEquipmentAugmentSignatureState offerAugments,
            RewardApplicationActions purchaseRewards)
        {
            Authority = authority
                ?? throw new ArgumentNullException(nameof(authority));
            Definition = definition
                ?? throw new ArgumentNullException(nameof(definition));
            Receipts = receipts
                ?? throw new ArgumentNullException(nameof(receipts));
            OfferAugments = offerAugments
                ?? throw new ArgumentNullException(nameof(offerAugments));
            PurchaseRewards = purchaseRewards
                ?? throw new ArgumentNullException(nameof(purchaseRewards));
        }

        public ShopLiveActions Authority { get; }
        public ShopDefinition Definition { get; }
        public ShopReceipts Receipts { get; }
        public GeneratedEquipmentAugmentSignatureState OfferAugments { get; }
        public RewardApplicationActions PurchaseRewards { get; }
    }

    internal static class CharacterShopSetup
    {
        private static readonly StableId ShopId =
            StableId.Parse("shop.hub-weapons");
        private static readonly StableId PurchaseAuthorityId =
            StableId.Parse(
                "authority.production-character-shop-reward-application");

        public static CharacterShopLive Create(
            PlayerLoadoutLive loadout,
            MoneyWalletActions money,
            ScrapWalletActions scrap,
            GeneratedEquipmentAugmentSignatureState augments)
        {
            if (loadout == null) throw new ArgumentNullException(nameof(loadout));
            if (money == null) throw new ArgumentNullException(nameof(money));
            if (scrap == null) throw new ArgumentNullException(nameof(scrap));
            if (augments == null)
            {
                throw new ArgumentNullException(nameof(augments));
            }

            ShopDefinition definition = BuildDefinition(
                loadout.EquipmentCatalog);
            var receipts = new ShopReceipts();
            var roller = new StrongboxOfferRoller(
                loadout.EquipmentCatalog,
                loadout.GunCatalog,
                CharacterStrongboxSetup.GenerationPolicyStableId);
            var purchaseRewards = new RewardApplicationActions(
                PurchaseAuthorityId,
                new MoneyRewardChildState(money),
                new ScrapRewardChildState(scrap),
                new GeneratedAugmentSignaturePlayerHoldingsRewardChildState(
                    loadout.Holdings,
                    loadout.CatalogBridge,
                    augments,
                    roller.OfferAugments,
                    loadout.GunInventory));
            var authority = new ShopLiveActions(
                new RewardGenerationActions(),
                money,
                purchaseRewards,
                scrap.AuthorityStableId,
                loadout.Holdings.AuthorityStableId,
                null,
                roller,
                receipts);
            return new CharacterShopLive(
                authority,
                definition,
                receipts,
                roller.OfferAugments,
                purchaseRewards);
        }

        private static ShopDefinition BuildDefinition(
            EquipmentCatalog catalog)
        {
            if (catalog == null)
            {
                throw new ArgumentNullException(nameof(catalog));
            }

            var candidates = new List<EquipmentGenerationCandidate>();
            var qualityById = new Dictionary<
                StableId,
                EquipmentQualityCandidate>();
            for (int index = 0;
                 index < catalog.EquipmentDefinitions.Count;
                 index++)
            {
                EquipmentDefinition equipment =
                    catalog.EquipmentDefinitions[index];
                if (equipment == null
                    || equipment.CategoryId != EquipmentCategoryIds.Gun)
                {
                    continue;
                }

                candidates.Add(EquipmentGenerationCandidate.Create(
                    equipment.DefinitionId,
                    0,
                    100,
                    0,
                    100,
                    Array.Empty<StableId>(),
                    0L,
                    equipment.ItemLevelRange,
                    1.0,
                    1.0));
                for (int qualityIndex = 0;
                     qualityIndex < equipment.QualityTiers.Count;
                     qualityIndex++)
                {
                    EquipmentQualityTier quality =
                        equipment.QualityTiers[qualityIndex];
                    if (quality != null
                        && !qualityById.ContainsKey(quality.QualityId))
                    {
                        qualityById.Add(
                            quality.QualityId,
                            EquipmentQualityCandidate.Create(
                                quality.QualityId,
                                0L,
                                1UL));
                    }
                }
            }
            if (candidates.Count == 0 || qualityById.Count == 0)
            {
                throw new InvalidOperationException(
                    "Production Shop requires at least one live gun definition.");
            }

            var generation = EquipmentGenerationPolicy.Create(
                StableId.Parse("shop-generation.hub-strongboxes"),
                candidates,
                qualityById.Values,
                Array.Empty<AugmentGenerationCandidate>(),
                0,
                0,
                true,
                new SoftActivationCurveParameters(0.1, 5L, 5L),
                new ObsolescenceCurveParameters(25L, 15.0, 0.2));
            var pricing = ShopPricingPolicy.Create(
                StableId.Parse("shop-pricing.hub-weapons-v1"),
                100L,
                100L,
                25L,
                150L,
                0L,
                0L,
                0L);
            return ShopDefinition.Create(
                ShopId,
                6,
                new[] { EquipmentCategoryIds.Gun },
                Array.Empty<StableId>(),
                Array.Empty<StableId>(),
                generation,
                ShopProgressionContextPolicy.FreezeOnFirstOpen,
                pricing,
                ShopRefreshPolicy.Disabled,
                0,
                0,
                DeterministicRandom.AlgorithmVersion1);
        }
    }
}
