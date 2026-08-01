using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
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
            ShopPurchaseLedger purchases,
            GeneratedEquipmentAugmentSignatureState previewAugmentSignatures)
        {
            Authority = authority
                ?? throw new ArgumentNullException(nameof(authority));
            Definition = definition
                ?? throw new ArgumentNullException(nameof(definition));
            Purchases = purchases
                ?? throw new ArgumentNullException(nameof(purchases));
            PreviewAugmentSignatures = previewAugmentSignatures
                ?? throw new ArgumentNullException(
                    nameof(previewAugmentSignatures));
        }

        public ShopLiveActions Authority { get; }
        public ShopDefinition Definition { get; }
        public ShopPurchaseLedger Purchases { get; }
        public GeneratedEquipmentAugmentSignatureState PreviewAugmentSignatures
        {
            get;
        }
    }

    public static class CharacterShopRegistry
    {
        private static readonly object Gate = new object();
        private static readonly ConditionalWeakTable<
            StrongboxOpeningActions,
            CharacterShopLive> Shops =
                new ConditionalWeakTable<
                    StrongboxOpeningActions,
                    CharacterShopLive>();

        public static void Bind(
            StrongboxOpeningActions strongboxes,
            CharacterShopLive shop)
        {
            if (strongboxes == null)
            {
                throw new ArgumentNullException(nameof(strongboxes));
            }
            if (shop == null)
            {
                throw new ArgumentNullException(nameof(shop));
            }
            lock (Gate)
            {
                Shops.Remove(strongboxes);
                Shops.Add(strongboxes, shop);
            }
        }

        public static bool TryResolve(
            StrongboxOpeningActions strongboxes,
            out CharacterShopLive shop)
        {
            lock (Gate)
            {
                return strongboxes != null
                    && Shops.TryGetValue(strongboxes, out shop)
                    && shop != null;
            }
        }
    }

    internal static class CharacterShopSetup
    {
        private static readonly StableId ShopStableId =
            StableId.Parse("shop.hub-weapons");

        public static CharacterShopLive Create(
            PlayerLoadoutLive loadout,
            MoneyWalletActions money,
            ScrapWalletActions scrap,
            RewardGenerationActions generator,
            RewardApplicationActions rewardApplication,
            GeneratedEquipmentAugmentSignatureState augmentSignatures)
        {
            if (loadout == null) throw new ArgumentNullException(nameof(loadout));
            if (money == null) throw new ArgumentNullException(nameof(money));
            if (scrap == null) throw new ArgumentNullException(nameof(scrap));
            if (generator == null) throw new ArgumentNullException(nameof(generator));
            if (rewardApplication == null)
            {
                throw new ArgumentNullException(nameof(rewardApplication));
            }
            if (augmentSignatures == null)
            {
                throw new ArgumentNullException(nameof(augmentSignatures));
            }

            ShopDefinition definition = BuildDefinition(
                loadout.EquipmentCatalog);
            var purchases = new ShopPurchaseLedger();
            var roller = new StrongboxShopStockRoller(
                loadout.EquipmentCatalog,
                loadout.GunCatalog,
                augmentSignatures,
                CharacterStrongboxSetup.GenerationPolicyStableId);
            var authority = new ShopLiveActions(
                generator,
                money,
                rewardApplication,
                scrap.AuthorityStableId,
                loadout.LegacyHoldings.AuthorityStableId,
                null,
                roller,
                purchases);
            return new CharacterShopLive(
                authority,
                definition,
                purchases,
                roller.PreviewSignatures);
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
                ShopStableId,
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
