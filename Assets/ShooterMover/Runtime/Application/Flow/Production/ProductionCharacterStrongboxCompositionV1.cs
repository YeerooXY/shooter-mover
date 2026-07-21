using System;
using System.Collections.Generic;
using System.Linq;
using ShooterMover.Application.Economy.Money;
using ShooterMover.Application.Economy.Scrap;
using ShooterMover.Application.Rewards.Application;
using ShooterMover.Application.Rewards.Generation;
using ShooterMover.Application.Rewards.Strongboxes;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Equipment;
using ShooterMover.Domain.Progression.Curves;
using ShooterMover.Domain.Rewards.Generation;
using ShooterMover.Domain.Rewards.Strongboxes;

namespace ShooterMover.Application.Flow.Production
{
    internal sealed class ProductionCharacterStrongboxRuntimeV1
    {
        public ProductionCharacterStrongboxRuntimeV1(
            StrongboxDefinitionCatalogV1 catalog,
            StrongboxOpeningServiceV1 authority)
        {
            Catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            Authority = authority ?? throw new ArgumentNullException(nameof(authority));
        }

        public StrongboxDefinitionCatalogV1 Catalog { get; }

        public StrongboxOpeningServiceV1 Authority { get; }
    }

    /// <summary>
    /// Builds the existing BOX/GEN/RAP authorities over one character graph's real
    /// holdings, money and scrap authorities. The current starter catalog has no augment
    /// definitions, so tier augment budgets are deterministically clamped to the catalog's
    /// actual capacity instead of creating impossible opening policies.
    /// </summary>
    internal static class ProductionCharacterStrongboxCompositionV1
    {
        public static readonly StableId GenerationPolicyStableId =
            StableId.Parse("generation-policy.production-character-strongbox");

        private static readonly StableId RewardApplicationAuthorityStableId =
            StableId.Parse("authority.production-character-reward-application");

        public static ProductionCharacterStrongboxRuntimeV1 Create(
            ProductionPlayerLoadoutRuntimeV1 loadout,
            MoneyWalletService money,
            ScrapWalletServiceV1 scrap)
        {
            if (loadout == null) throw new ArgumentNullException(nameof(loadout));
            if (money == null) throw new ArgumentNullException(nameof(money));
            if (scrap == null) throw new ArgumentNullException(nameof(scrap));

            EquipmentCatalog equipmentCatalog = loadout.EquipmentCatalog;
            EquipmentGenerationPolicyV1 policy = CreateGenerationPolicy(
                equipmentCatalog);
            var definitions = new List<StrongboxDefinitionV1>();
            var bindings = new List<
                StrongboxEquipmentGenerationDefinitionV1>();
            for (int index = 0;
                index < ProductionStrongboxCatalogV1.Tiers.Count;
                index++)
            {
                ProductionStrongboxTierV1 tier =
                    ProductionStrongboxCatalogV1.Tiers[index];
                definitions.Add(tier.CreateDefinition(policy.PolicyId));
                bindings.Add(new StrongboxEquipmentGenerationDefinitionV1(
                    tier.TierStableId,
                    CreateCompatiblePowerBudget(tier, equipmentCatalog),
                    policy,
                    equipmentCatalog));
            }

            var catalog = new StrongboxDefinitionCatalogV1(definitions);
            var generator = new RewardGenerationServiceV1();
            var equipmentResolver =
                new StrongboxEquipmentGenerationResolverV1(
                    generator,
                    new StrongboxEquipmentGenerationDefinitionCatalogV1(
                        bindings));
            var rewardApplication = new RewardApplicationServiceV1(
                RewardApplicationAuthorityStableId,
                new MoneyRewardChildAuthorityV1(money),
                new ScrapRewardChildAuthorityV1(scrap),
                new PlayerHoldingsRewardChildAuthorityV1(
                    loadout.Holdings,
                    loadout.CatalogAdapter));
            var authority = new StrongboxOpeningServiceV1(
                catalog,
                new SharedStrongboxRewardGeneratorV1(generator),
                loadout.Holdings,
                rewardApplication,
                new DeterministicStrongboxGrantPayloadResolverV1(
                    equipmentResolver));
            return new ProductionCharacterStrongboxRuntimeV1(
                catalog,
                authority);
        }

        private static EquipmentGenerationPolicyV1 CreateGenerationPolicy(
            EquipmentCatalog catalog)
        {
            var equipment = new List<EquipmentGenerationCandidateV1>();
            var qualityById = new SortedDictionary<
                string,
                EquipmentQualityTier>(StringComparer.Ordinal);
            for (int index = 0;
                index < catalog.EquipmentDefinitions.Count;
                index++)
            {
                EquipmentDefinition definition =
                    catalog.EquipmentDefinitions[index];
                if (definition.CategoryId != EquipmentCategoryIds.Weapon)
                {
                    continue;
                }
                equipment.Add(EquipmentGenerationCandidateV1.Create(
                    definition.DefinitionId,
                    0,
                    100,
                    0,
                    100,
                    Array.Empty<StableId>(),
                    1L,
                    definition.ItemLevelRange,
                    1d,
                    1d));
                for (int qualityIndex = 0;
                    qualityIndex < definition.QualityTiers.Count;
                    qualityIndex++)
                {
                    EquipmentQualityTier quality =
                        definition.QualityTiers[qualityIndex];
                    qualityById[quality.QualityId.ToString()] = quality;
                }
            }
            if (equipment.Count == 0)
            {
                throw new InvalidOperationException(
                    "Character BOX requires at least one weapon equipment definition.");
            }

            var qualities = qualityById.Values
                .Select(item => EquipmentQualityCandidateV1.Create(
                    item.QualityId,
                    0L,
                    1UL))
                .ToList();
            var augments = catalog.AugmentDefinitions
                .Select(item => AugmentGenerationCandidateV1.Create(
                    item.DefinitionId,
                    0,
                    100,
                    1UL))
                .ToList();
            int maximumAugmentSlots = ResolveMaximumAugmentCapacity(catalog);
            return EquipmentGenerationPolicyV1.Create(
                GenerationPolicyStableId,
                equipment,
                qualities,
                augments,
                0,
                maximumAugmentSlots,
                false,
                new SoftActivationCurveParameters(0.1, 10L, 10L),
                new ObsolescenceCurveParameters(100L, 100d, 1d));
        }

        private static StrongboxPowerBudgetPolicyV1
            CreateCompatiblePowerBudget(
                ProductionStrongboxTierV1 tier,
                EquipmentCatalog catalog)
        {
            int available = ResolveMaximumAugmentCapacity(catalog);
            int maximum = Math.Min(tier.MaximumAugmentSlots, available);
            int minimum = Math.Min(tier.MinimumAugmentSlots, maximum);
            int deviation = minimum == maximum
                ? 0
                : tier.AugmentSlotStandardDeviationMilli;
            return StrongboxPowerBudgetPolicyV1.Create(
                Math.Max(0, tier.LevelOffset),
                tier.ItemLevelStandardDeviationMilli,
                minimum,
                maximum,
                deviation);
        }

        private static int ResolveMaximumAugmentCapacity(
            EquipmentCatalog catalog)
        {
            if (catalog.AugmentDefinitions.Count == 0)
            {
                return 0;
            }
            int maximum = 0;
            for (int index = 0;
                index < catalog.EquipmentDefinitions.Count;
                index++)
            {
                EquipmentDefinition definition =
                    catalog.EquipmentDefinitions[index];
                if (definition.CategoryId == EquipmentCategoryIds.Weapon)
                {
                    maximum = Math.Max(
                        maximum,
                        definition.MaximumAugmentSlots);
                }
            }
            return maximum;
        }
    }
}
