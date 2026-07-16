using System;
using System.Collections.Generic;
using ShooterMover.Application.Rewards.Generation;
using ShooterMover.Application.Rewards.Strongboxes;
using ShooterMover.Contracts.Rewards;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Common.Random;
using ShooterMover.Domain.Crafting;
using ShooterMover.Domain.Equipment;
using ShooterMover.Domain.Equipment.Upgrades;
using ShooterMover.Domain.Progression.Context;
using ShooterMover.Domain.Progression.Curves;
using ShooterMover.Domain.Rewards.Generation;
using ShooterMover.Domain.Rewards.Model;
using ShooterMover.Domain.Rewards.Strongboxes;
using ShooterMover.Domain.Shops;

namespace ShooterMover.Editor.BalanceSimulator
{
    /// <summary>
    /// Deterministic, asset-free reference composition for the editor product. Every random
    /// decision delegates to the production GEN/strongbox code. Projects may replace this
    /// runtime through IBalanceSimulationRuntimeV1 without changing report aggregation.
    /// </summary>
    public sealed class RuntimeBalanceScenarioV1 : IBalanceSimulationRuntimeV1
    {
        private static readonly StableId DifficultyNormal = Id("difficulty.normal");
        private static readonly StableId QualityCommon = Id("quality.common");
        private static readonly StableId QualityRare = Id("quality.rare");
        private static readonly StableId QualityExceptional = Id("quality.exceptional");
        private static readonly StableId WeaponPulse = Id("equipment.weapon-pulse");
        private static readonly StableId WeaponScatter = Id("equipment.weapon-scatter");
        private static readonly StableId ArmorReactive = Id("equipment.armor-reactive");
        private static readonly StableId AugmentPower = Id("augment.power");
        private static readonly StableId AugmentGuard = Id("augment.guard");
        private static readonly StableId ScrapCurrency = Id("currency.scrap");
        private static readonly StableId TierScaling = Id("scaling.source-tier");
        private static readonly StableId ExceptionalScaling = Id("scaling.exceptional");

        private readonly RewardGenerationServiceV1 generator = new RewardGenerationServiceV1();
        private readonly EquipmentCatalog catalog;
        private readonly EquipmentGenerationPolicyV1 generalPolicy;
        private readonly EquipmentGenerationPolicyV1 strongboxPolicy;
        private readonly CraftingRecipeV1 craftingRecipe;
        private readonly ShopPricingPolicyV1 shopPricing;
        private readonly AugmentUpgradeCostPolicyV1 upgradeCosts;

        public RuntimeBalanceScenarioV1()
        {
            catalog = BuildCatalog();
            generalPolicy = BuildPolicy("balance-policy.general", false);
            strongboxPolicy = BuildStrongboxPolicy();
            craftingRecipe = BuildCraftingRecipe();
            shopPricing = ShopPricingPolicyV1.Create(
                Id("balance-shop-pricing"), 1L, 25L, 3L, 13L, 20L, 7L, 2L);
            upgradeCosts = AugmentUpgradeCostPolicyV1.Create(
                Id("balance-upgrade-costs"),
                1,
                false,
                new[]
                {
                    AugmentTierCostCurveV1.Create(1, 30L, 10L),
                    AugmentTierCostCurveV1.Create(2, 60L, 20L),
                    AugmentTierCostCurveV1.Create(3, 120L, 40L),
                });
        }

        public EquipmentCatalog Catalog { get { return catalog; } }

        public BalanceSimulationIterationResultV1 Run(BalanceSimulationIterationRequestV1 iteration)
        {
            if (iteration == null) { throw new ArgumentNullException(nameof(iteration)); }
            BalanceSimulationRequestV1 request = iteration.Request;
            List<BalanceRewardObservationV1> rewards = new List<BalanceRewardObservationV1>();
            List<BalanceEquipmentObservationV1> equipment = new List<BalanceEquipmentObservationV1>();
            List<BalanceRejectionV1> rejections = new List<BalanceRejectionV1>();
            long money = request.StartingMoney;
            long scrap = request.StartingScrap;
            long shopRequired = 0L;
            long craftingRequired = craftingRecipe.ScrapCost;
            long upgradeRequired = 0L;

            ProgressionContext characterContext = Context(request.CharacterLevel, request.StrongboxLevel);
            int softCandidates = CountSoftEligibleCandidates(generalPolicy, characterContext);

            RunStrongbox(iteration, characterContext, equipment, rewards, rejections, ref scrap);
            RunShop(iteration, equipment, rewards, rejections, ref money, ref shopRequired);
            int craftingUnlock = RunCrafting(iteration, characterContext, equipment, rewards, rejections, ref scrap);
            RunUpgrade(iteration, characterContext, equipment, rewards, rejections, ref money, ref upgradeRequired);

            return new BalanceSimulationIterationResultV1(
                iteration.IterationIndex,
                iteration.IterationSeed,
                rewards,
                equipment,
                money - request.StartingMoney,
                scrap - request.StartingScrap,
                shopRequired,
                craftingRequired,
                upgradeRequired,
                softCandidates,
                craftingUnlock,
                rejections);
        }

        private void RunStrongbox(
            BalanceSimulationIterationRequestV1 iteration,
            ProgressionContext context,
            ICollection<BalanceEquipmentObservationV1> equipment,
            ICollection<BalanceRewardObservationV1> rewards,
            ICollection<BalanceRejectionV1> rejections,
            ref long scrap)
        {
            BalanceSimulationRequestV1 request = iteration.Request;
            StableId tierId = StableId.Create("balance-strongbox-tier", request.StrongboxTier.ToString());
            StrongboxDefinitionV1 definition = BuildStrongboxDefinition(tierId, request.StrongboxTier);
            StrongboxPowerBudgetPolicyV1 power = StrongboxPowerBudgetPolicyV1.Create(
                request.StrongboxTier,
                4000,
                0,
                2,
                500);
            StrongboxEquipmentGenerationDefinitionCatalogV1 definitions =
                new StrongboxEquipmentGenerationDefinitionCatalogV1(
                    new[]
                    {
                        new StrongboxEquipmentGenerationDefinitionV1(
                            tierId,
                            power,
                            strongboxPolicy,
                            catalog)
                    });
            StrongboxEquipmentGenerationResolverV1 resolver =
                new StrongboxEquipmentGenerationResolverV1(generator, definitions);
            StableId instanceId = DynamicId("strongbox-instance", iteration.IterationSeed, 0);
            StrongboxInstanceContextV1 strongboxContext = StrongboxInstanceContextV1.Create(
                instanceId,
                tierId,
                iteration.IterationSeed,
                DeterministicRandom.AlgorithmVersion1,
                context,
                Id("source.balance-simulator"),
                Id("provenance.balance-simulator"),
                definition.Fingerprint);
            RewardOperationRequestV1 operation = RewardOperationRequestV1.Create(
                DynamicId("run", iteration.IterationSeed, 0),
                instanceId,
                DynamicId("strongbox-operation", iteration.IterationSeed, 0),
                DynamicId("strongbox-commitment", iteration.IterationSeed, 0),
                definition.BaseRewardProfile.ProfileStableId,
                definition.Fingerprint);
            RewardGrantV1 grant = RewardGrantV1.Create(
                Id("grant.balance-strongbox-equipment"),
                RewardGrantKindV1.EquipmentReference,
                WeaponPulse,
                2L);

            IReadOnlyList<EquipmentInstance> generated;
            string rejection;
            if (!resolver.TryResolve(definition, strongboxContext, operation, grant, out generated, out rejection))
            {
                rejections.Add(new BalanceRejectionV1("strongbox", "generation-rejected", rejection));
                return;
            }

            for (int index = 0; index < generated.Count; index++)
            {
                AddEquipment("strongbox", generated[index], equipment);
            }
            rewards.Add(new BalanceRewardObservationV1("strongbox-equipment", generated.Count));

            long scrapGrant = checked(definition.MandatoryScrapPolicy.MinimumQuantity);
            scrap = checked(scrap + scrapGrant);
            rewards.Add(new BalanceRewardObservationV1("scrap", scrapGrant));
        }

        private void RunShop(
            BalanceSimulationIterationRequestV1 iteration,
            ICollection<BalanceEquipmentObservationV1> equipment,
            ICollection<BalanceRewardObservationV1> rewards,
            ICollection<BalanceRejectionV1> rejections,
            ref long money,
            ref long required)
        {
            ProgressionContext shopContext = Context(iteration.Request.ShopLevel, iteration.Request.ShopLevel);
            long firstPrice = 0L;
            for (int index = 0; index < 3; index++)
            {
                EquipmentGenerationResultV1 result = generator.GenerateEquipment(
                    EquipmentGenerationRequestV1.Create(
                        DynamicId("shop-operation", iteration.IterationSeed, index),
                        DynamicId("shop-equipment", iteration.IterationSeed, index),
                        generalPolicy,
                        catalog,
                        shopContext,
                        SubSeed(iteration.IterationSeed, "shop", index),
                        DeterministicRandom.AlgorithmVersion1));
                if (!result.IsSuccess)
                {
                    rejections.Add(new BalanceRejectionV1("shop", result.Status.ToString(), result.FailureReason));
                    continue;
                }

                long price;
                string priceRejection;
                if (!shopPricing.TryCalculatePrice(result.Equipment, catalog, out price, out priceRejection))
                {
                    rejections.Add(new BalanceRejectionV1("shop", "price-rejected", priceRejection));
                    continue;
                }

                required = checked(required + price);
                if (index == 0) { firstPrice = price; }
                AddEquipment("shop", result.Equipment, equipment);
                rewards.Add(new BalanceRewardObservationV1("shop-stock", 1L));
            }

            if (firstPrice > 0L)
            {
                if (money >= firstPrice)
                {
                    money -= firstPrice;
                    rewards.Add(new BalanceRewardObservationV1("money-spent-shop", firstPrice));
                }
                else
                {
                    rejections.Add(new BalanceRejectionV1("shop", "insufficient-funds", firstPrice.ToString()));
                }
            }
        }

        private int RunCrafting(
            BalanceSimulationIterationRequestV1 iteration,
            ProgressionContext context,
            ICollection<BalanceEquipmentObservationV1> equipment,
            ICollection<BalanceRewardObservationV1> rewards,
            ICollection<BalanceRejectionV1> rejections,
            ref long scrap)
        {
            int unlockLevel = craftingRecipe.ResolveUnlockLevel(SubSeed(iteration.IterationSeed, "crafting-unlock", 0));
            if (context.CharacterLevel < unlockLevel)
            {
                rejections.Add(new BalanceRejectionV1(
                    "crafting",
                    "soft-level-requirement",
                    context.CharacterLevel + "<" + unlockLevel));
                return unlockLevel;
            }

            if (scrap < craftingRecipe.ScrapCost)
            {
                rejections.Add(new BalanceRejectionV1("crafting", "insufficient-scrap", craftingRecipe.ScrapCost.ToString()));
                return unlockLevel;
            }

            EquipmentGenerationPolicyV1 policy = BuildCraftingGenerationPolicy();
            EquipmentGenerationResultV1 result = generator.GenerateEquipment(
                EquipmentGenerationRequestV1.Create(
                    DynamicId("crafting-operation", iteration.IterationSeed, 0),
                    DynamicId("crafting-equipment", iteration.IterationSeed, 0),
                    policy,
                    catalog,
                    context,
                    SubSeed(iteration.IterationSeed, "crafting", 0),
                    DeterministicRandom.AlgorithmVersion1));
            if (!result.IsSuccess)
            {
                rejections.Add(new BalanceRejectionV1("crafting", result.Status.ToString(), result.FailureReason));
                return unlockLevel;
            }

            scrap -= craftingRecipe.ScrapCost;
            AddEquipment("crafting", result.Equipment, equipment);
            rewards.Add(new BalanceRewardObservationV1("crafted-equipment", 1L));
            rewards.Add(new BalanceRewardObservationV1("scrap-spent-crafting", craftingRecipe.ScrapCost));
            return unlockLevel;
        }

        private void RunUpgrade(
            BalanceSimulationIterationRequestV1 iteration,
            ProgressionContext context,
            ICollection<BalanceEquipmentObservationV1> equipment,
            ICollection<BalanceRewardObservationV1> rewards,
            ICollection<BalanceRejectionV1> rejections,
            ref long money,
            ref long required)
        {
            EquipmentGenerationPolicyV1 upgradeTargetPolicy = BuildPolicy("balance-policy.upgrade-target", true);
            EquipmentGenerationResultV1 result = generator.GenerateEquipment(
                EquipmentGenerationRequestV1.Create(
                    DynamicId("upgrade-operation", iteration.IterationSeed, 0),
                    DynamicId("upgrade-equipment", iteration.IterationSeed, 0),
                    upgradeTargetPolicy,
                    catalog,
                    context,
                    SubSeed(iteration.IterationSeed, "upgrade", 0),
                    DeterministicRandom.AlgorithmVersion1));
            if (!result.IsSuccess)
            {
                rejections.Add(new BalanceRejectionV1("augment-upgrade", result.Status.ToString(), result.FailureReason));
                return;
            }

            AddEquipment("augment-upgrade-target", result.Equipment, equipment);
            if (result.Equipment.Augments.Count == 0)
            {
                rejections.Add(new BalanceRejectionV1("augment-upgrade", "missing-augment", string.Empty));
                return;
            }

            AugmentInstance augment = result.Equipment.Augments[0];
            AugmentDefinition definition = catalog.FindAugmentDefinition(augment.DefinitionId);
            if (definition == null || definition.LevelRange == null || augment.Level >= definition.LevelRange.Maximum)
            {
                rejections.Add(new BalanceRejectionV1("augment-upgrade", "maximum-level", augment.Level.ToString()));
                return;
            }

            long cost;
            AugmentUpgradeCostStatusV1 status = upgradeCosts.TryCalculateCost(
                augment.Tier,
                augment.Level,
                augment.Level + 1,
                out cost);
            if (status != AugmentUpgradeCostStatusV1.Calculated)
            {
                rejections.Add(new BalanceRejectionV1("augment-upgrade", status.ToString(), string.Empty));
                return;
            }

            required = cost;
            if (money < cost)
            {
                rejections.Add(new BalanceRejectionV1("augment-upgrade", "insufficient-money", cost.ToString()));
                return;
            }

            money -= cost;
            rewards.Add(new BalanceRewardObservationV1("augment-level-up", 1L));
            rewards.Add(new BalanceRewardObservationV1("money-spent-upgrade", cost));
        }

        private int CountSoftEligibleCandidates(EquipmentGenerationPolicyV1 policy, ProgressionContext context)
        {
            int count = 0;
            for (int index = 0; index < policy.EquipmentCandidates.Count; index++)
            {
                EquipmentGenerationCandidateV1 candidate = policy.EquipmentCandidates[index];
                if (context.CharacterLevel < candidate.NominalActivationLevel
                    && candidate.IsEligible(context, catalog)
                    && candidate.EvaluateWeight(context, policy.Activation, policy.Obsolescence) > 0.0)
                {
                    count++;
                }
            }
            return count;
        }

        private void AddEquipment(
            string source,
            EquipmentInstance instance,
            ICollection<BalanceEquipmentObservationV1> output)
        {
            EquipmentDefinition definition = catalog.FindEquipmentDefinition(instance.DefinitionId);
            output.Add(new BalanceEquipmentObservationV1(
                source,
                instance,
                definition.CategoryId,
                definition.DisplayName));
        }

        private EquipmentGenerationPolicyV1 BuildPolicy(string id, bool requireOneAugment)
        {
            return EquipmentGenerationPolicyV1.Create(
                Id(id),
                new[]
                {
                    Candidate(WeaponPulse, 1L, 1.0),
                    Candidate(ArmorReactive, 8L, 1.0),
                    Candidate(WeaponScatter, 18L, 1.0),
                },
                new[]
                {
                    EquipmentQualityCandidateV1.Create(QualityCommon, 0L, 8UL),
                    EquipmentQualityCandidateV1.Create(QualityRare, 8L, 3UL),
                    EquipmentQualityCandidateV1.Create(QualityExceptional, 20L, 1UL),
                },
                new[]
                {
                    AugmentGenerationCandidateV1.Create(AugmentPower, 0, 1000, 2UL),
                    AugmentGenerationCandidateV1.Create(AugmentGuard, 0, 1000, 2UL),
                },
                requireOneAugment ? 1 : 0,
                requireOneAugment ? 1 : 3,
                requireOneAugment,
                new SoftActivationCurveParameters(0.08, 12L, 8L),
                new ObsolescenceCurveParameters(30L, 20.0, 0.15));
        }

        private EquipmentGenerationPolicyV1 BuildStrongboxPolicy()
        {
            return EquipmentGenerationPolicyV1.Create(
                Id("balance-policy.strongbox"),
                new[] { Candidate(WeaponPulse, 0L, 1.0) },
                new[]
                {
                    EquipmentQualityCandidateV1.Create(QualityCommon, 0L, 8UL),
                    EquipmentQualityCandidateV1.Create(QualityRare, 8L, 3UL),
                    EquipmentQualityCandidateV1.Create(QualityExceptional, 20L, 1UL),
                },
                new[]
                {
                    AugmentGenerationCandidateV1.Create(AugmentPower, 0, 1000, 2UL),
                    AugmentGenerationCandidateV1.Create(AugmentGuard, 0, 1000, 2UL),
                },
                0,
                2,
                false,
                new SoftActivationCurveParameters(0.08, 12L, 8L),
                new ObsolescenceCurveParameters(30L, 20.0, 0.15));
        }

        private EquipmentGenerationPolicyV1 BuildCraftingGenerationPolicy()
        {
            return EquipmentGenerationPolicyV1.Create(
                Id("balance-policy.crafting"),
                new[] { Candidate(craftingRecipe.TargetEquipmentDefinitionStableId, 0L, 1.0) },
                new[]
                {
                    EquipmentQualityCandidateV1.Create(QualityCommon, 0L, 3UL),
                    EquipmentQualityCandidateV1.Create(QualityRare, 0L, 1UL),
                },
                new[] { AugmentGenerationCandidateV1.Create(AugmentPower, 0, 1000, 1UL) },
                craftingRecipe.MinimumAugmentSlots,
                craftingRecipe.MaximumAugmentSlots,
                true,
                craftingRecipe.GeneratorPolicy.Activation,
                craftingRecipe.GeneratorPolicy.Obsolescence);
        }

        private CraftingRecipeV1 BuildCraftingRecipe()
        {
            return new CraftingRecipeV1(
                1,
                Id("recipe.weapon-scatter"),
                WeaponScatter,
                Id("source.natural.weapon-scatter"),
                18,
                18,
                4,
                new CraftingDelayVarianceV1(0, 2),
                50L,
                CraftingQualityPolicyKindV1.DeterministicWeightedRandom,
                new[]
                {
                    new CraftingWeightedDefinitionV1(QualityCommon, 3UL),
                    new CraftingWeightedDefinitionV1(QualityRare, 1UL),
                },
                1,
                100,
                1,
                1,
                3,
                10,
                new[] { new CraftingWeightedDefinitionV1(AugmentPower, 1UL) },
                new CraftingGeneratorPolicyV1(
                    Id("crafting-generator.weapon-scatter"),
                    DeterministicRandom.AlgorithmVersion1,
                    new SoftActivationCurveParameters(0.08, 12L, 8L),
                    new ObsolescenceCurveParameters(30L, 20.0, 0.15)));
        }

        private StrongboxDefinitionV1 BuildStrongboxDefinition(StableId tierId, int tier)
        {
            RewardGrantSpecificationV1 equipment = RewardGrantSpecificationV1.CreateFixed(
                Id("grant.balance-strongbox-equipment-spec"),
                RewardGrantKindV1.EquipmentReference,
                WeaponPulse,
                2L);
            RewardProfileV1 profile = RewardProfileV1.Create(
                Id("profile.balance-strongbox"),
                new[] { equipment },
                Array.Empty<IndependentRewardRollV1>(),
                Array.Empty<ExclusiveRewardGroupV1>());
            return StrongboxDefinitionV1.Create(
                tierId,
                tier,
                Math.Max(1, tier + 1),
                Math.Max(1, tier + 1),
                tier,
                StrongboxRewardCountPolicyV1.Create(2, 2),
                StrongboxMandatoryScrapPolicyV1.Create(ScrapCurrency, Math.Max(1, tier + 1), Math.Max(1, tier + 1)),
                strongboxPolicy.PolicyId,
                profile,
                TierScaling,
                ExceptionalScaling);
        }

        private EquipmentGenerationCandidateV1 Candidate(StableId definitionId, long nominalLevel, double weight)
        {
            return EquipmentGenerationCandidateV1.Create(
                definitionId,
                0,
                1000,
                0,
                1000,
                Array.Empty<StableId>(),
                nominalLevel,
                InclusiveIntRange.Create(1, 100),
                weight,
                1.0);
        }

        private static EquipmentCatalog BuildCatalog()
        {
            EquipmentQualityTier common = EquipmentQualityTier.Create(QualityCommon, "Common", 1);
            EquipmentQualityTier rare = EquipmentQualityTier.Create(QualityRare, "Rare", 2);
            EquipmentQualityTier exceptional = EquipmentQualityTier.Create(QualityExceptional, "Exceptional", 3);
            EquipmentDefinition pulse = EquipmentDefinition.Create(
                WeaponPulse,
                EquipmentCategoryIds.Weapon,
                Id("equipment-family.pulse"),
                "Pulse Weapon",
                Id("weapon.runtime.pulse"),
                InclusiveIntRange.Create(1, 100),
                3,
                new[] { common, rare, exceptional },
                Array.Empty<StableId>());
            EquipmentDefinition scatter = EquipmentDefinition.Create(
                WeaponScatter,
                EquipmentCategoryIds.Weapon,
                Id("equipment-family.scatter"),
                "Scatter Weapon",
                Id("weapon.runtime.scatter"),
                InclusiveIntRange.Create(1, 100),
                2,
                new[] { common, rare, exceptional },
                Array.Empty<StableId>());
            EquipmentDefinition armor = EquipmentDefinition.Create(
                ArmorReactive,
                EquipmentCategoryIds.Armor,
                Id("equipment-family.reactive-armor"),
                "Reactive Armor",
                null,
                InclusiveIntRange.Create(1, 100),
                2,
                new[] { common, rare, exceptional },
                Array.Empty<StableId>());
            AugmentCompatibility any = AugmentCompatibility.Create(
                Array.Empty<StableId>(),
                Array.Empty<StableId>(),
                Array.Empty<StableId>(),
                Array.Empty<StableId>());
            AugmentDefinition power = AugmentDefinition.Create(
                AugmentPower,
                Id("augment-family.power"),
                "Power",
                any,
                Array.Empty<StableId>(),
                AugmentDuplicatePolicy.DisallowSameDefinition,
                InclusiveIntRange.Create(1, 3),
                InclusiveIntRange.Create(1, 10));
            AugmentDefinition guard = AugmentDefinition.Create(
                AugmentGuard,
                Id("augment-family.guard"),
                "Guard",
                any,
                Array.Empty<StableId>(),
                AugmentDuplicatePolicy.DisallowSameDefinition,
                InclusiveIntRange.Create(1, 3),
                InclusiveIntRange.Create(1, 10));
            EquipmentCatalogBuildResult build = EquipmentCatalog.Build(
                new[] { pulse, scatter, armor },
                new[] { power, guard });
            if (!build.IsValid) { throw new InvalidOperationException("Balance simulator catalog is invalid."); }
            return build.Catalog;
        }

        private static ProgressionContext Context(int characterLevel, int regionLevel)
        {
            return ProgressionContext.Create(
                characterLevel,
                regionLevel,
                DifficultyNormal,
                1,
                Array.Empty<StableId>());
        }

        private static ulong SubSeed(ulong rootSeed, string purpose, int ordinal)
        {
            DeterministicRandom random = DeterministicRandom.Create(rootSeed)
                .Fork(StableId.Create("balance-simulator", purpose), checked((ulong)ordinal));
            random.NextUInt64(out ulong value);
            return value;
        }

        private static StableId DynamicId(string purpose, ulong seed, int ordinal)
        {
            return StableId.Create("balance-simulator", purpose + "-" + seed.ToString("x16") + "-" + ordinal.ToString("D4"));
        }

        private static StableId Id(string value) { return StableId.Parse(value); }
    }
}
