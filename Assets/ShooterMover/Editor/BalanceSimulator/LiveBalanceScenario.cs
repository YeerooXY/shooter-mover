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
    /// runtime through IBalanceSimulationLive without changing report aggregation.
    /// </summary>
    public sealed class LiveBalanceScenario : IBalanceSimulationLive
    {
        private static readonly StableId DifficultyNormal = Id("difficulty.normal");
        private static readonly StableId QualityCommon = Id("quality.common");
        private static readonly StableId QualityRare = Id("quality.rare");
        private static readonly StableId QualityExceptional = Id("quality.exceptional");
        private static readonly StableId GunPulse = Id("equipment.gun-pulse");
        private static readonly StableId GunScatter = Id("equipment.gun-scatter");
        private static readonly StableId ArmorReactive = Id("equipment.armor-reactive");
        private static readonly StableId AugmentPower = Id("augment.power");
        private static readonly StableId AugmentGuard = Id("augment.guard");
        private static readonly StableId ScrapCurrency = Id("currency.scrap");
        private static readonly StableId TierScaling = Id("scaling.source-tier");
        private static readonly StableId ExceptionalScaling = Id("scaling.exceptional");

        private readonly RewardGenerationActions generator = new RewardGenerationActions();
        private readonly EquipmentCatalog catalog;
        private readonly EquipmentGenerationPolicy generalPolicy;
        private readonly EquipmentGenerationPolicy strongboxPolicy;
        private readonly CraftingRecipe craftingRecipe;
        private readonly ShopPricingPolicy shopPricing;
        private readonly AugmentUpgradeCostPolicy upgradeCosts;

        public LiveBalanceScenario()
        {
            catalog = BuildCatalog();
            generalPolicy = BuildPolicy("balance-policy.general", false);
            strongboxPolicy = BuildStrongboxPolicy();
            craftingRecipe = BuildCraftingRecipe();
            shopPricing = ShopPricingPolicy.Create(
                Id("balance-shop-pricing"), 1L, 25L, 3L, 13L, 20L, 7L, 2L);
            upgradeCosts = AugmentUpgradeCostPolicy.Create(
                Id("balance-upgrade-costs"),
                1,
                false,
                new[]
                {
                    AugmentTierCostCurve.Create(1, 30L, 10L),
                    AugmentTierCostCurve.Create(2, 60L, 20L),
                    AugmentTierCostCurve.Create(3, 120L, 40L),
                });
        }

        public EquipmentCatalog Catalog { get { return catalog; } }

        public BalanceSimulationIterationResult Run(BalanceSimulationIterationRequest iteration)
        {
            if (iteration == null) { throw new ArgumentNullException(nameof(iteration)); }
            BalanceSimulationRequest request = iteration.Request;
            List<BalanceRewardObservation> rewards = new List<BalanceRewardObservation>();
            List<BalanceEquipmentObservation> equipment = new List<BalanceEquipmentObservation>();
            List<BalanceRejection> rejections = new List<BalanceRejection>();
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

            return new BalanceSimulationIterationResult(
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
            BalanceSimulationIterationRequest iteration,
            ProgressionContext context,
            ICollection<BalanceEquipmentObservation> equipment,
            ICollection<BalanceRewardObservation> rewards,
            ICollection<BalanceRejection> rejections,
            ref long scrap)
        {
            BalanceSimulationRequest request = iteration.Request;
            StableId tierId = StableId.Create("balance-strongbox-tier", request.StrongboxTier.ToString());
            StrongboxDefinition definition = BuildStrongboxDefinition(tierId, request.StrongboxTier);
            StrongboxPowerBudgetPolicy power = StrongboxPowerBudgetPolicy.Create(
                request.StrongboxTier,
                4000,
                0,
                2,
                500);
            StrongboxEquipmentGenerationDefinitionCatalog definitions =
                new StrongboxEquipmentGenerationDefinitionCatalog(
                    new[]
                    {
                        new StrongboxEquipmentGenerationDefinition(
                            tierId,
                            power,
                            strongboxPolicy,
                            catalog)
                    });
            StrongboxEquipmentGenerationResolver resolver =
                new StrongboxEquipmentGenerationResolver(generator, definitions);
            StableId instanceId = DynamicId("strongbox-instance", iteration.IterationSeed, 0);
            StrongboxInstanceContext strongboxContext = StrongboxInstanceContext.Create(
                instanceId,
                tierId,
                iteration.IterationSeed,
                DeterministicRandom.AlgorithmVersion1,
                context,
                Id("source.balance-simulator"),
                Id("provenance.balance-simulator"),
                definition.Fingerprint);
            RewardOperationRequest operation = RewardOperationRequest.Create(
                DynamicId("run", iteration.IterationSeed, 0),
                instanceId,
                DynamicId("strongbox-operation", iteration.IterationSeed, 0),
                DynamicId("strongbox-commitment", iteration.IterationSeed, 0),
                definition.BaseRewardProfile.ProfileStableId,
                definition.Fingerprint);
            RewardGrant grant = RewardGrant.Create(
                Id("grant.balance-strongbox-equipment"),
                RewardGrantKind.EquipmentReference,
                GunPulse,
                2L);

            IReadOnlyList<EquipmentInstance> generated;
            string rejection;
            if (!resolver.TryResolve(definition, strongboxContext, operation, grant, out generated, out rejection))
            {
                rejections.Add(new BalanceRejection("strongbox", "generation-rejected", rejection));
                return;
            }

            for (int index = 0; index < generated.Count; index++)
            {
                AddEquipment("strongbox", generated[index], equipment);
            }
            rewards.Add(new BalanceRewardObservation("strongbox-equipment", generated.Count));

            long scrapGrant = checked(definition.MandatoryScrapPolicy.MinimumQuantity);
            scrap = checked(scrap + scrapGrant);
            rewards.Add(new BalanceRewardObservation("scrap", scrapGrant));
        }

        private void RunShop(
            BalanceSimulationIterationRequest iteration,
            ICollection<BalanceEquipmentObservation> equipment,
            ICollection<BalanceRewardObservation> rewards,
            ICollection<BalanceRejection> rejections,
            ref long money,
            ref long required)
        {
            ProgressionContext shopContext = Context(iteration.Request.ShopLevel, iteration.Request.ShopLevel);
            long firstPrice = 0L;
            for (int index = 0; index < 3; index++)
            {
                EquipmentGenerationResult result = generator.GenerateEquipment(
                    EquipmentGenerationRequest.Create(
                        DynamicId("shop-operation", iteration.IterationSeed, index),
                        DynamicId("shop-equipment", iteration.IterationSeed, index),
                        generalPolicy,
                        catalog,
                        shopContext,
                        SubSeed(iteration.IterationSeed, "shop", index),
                        DeterministicRandom.AlgorithmVersion1));
                if (!result.IsSuccess)
                {
                    rejections.Add(new BalanceRejection("shop", result.Status.ToString(), result.FailureReason));
                    continue;
                }

                long price;
                string priceRejection;
                if (!shopPricing.TryCalculatePrice(result.Equipment, catalog, out price, out priceRejection))
                {
                    rejections.Add(new BalanceRejection("shop", "price-rejected", priceRejection));
                    continue;
                }

                required = checked(required + price);
                if (index == 0) { firstPrice = price; }
                AddEquipment("shop", result.Equipment, equipment);
                rewards.Add(new BalanceRewardObservation("shop-stock", 1L));
            }

            if (firstPrice > 0L)
            {
                if (money >= firstPrice)
                {
                    money -= firstPrice;
                    rewards.Add(new BalanceRewardObservation("money-spent-shop", firstPrice));
                }
                else
                {
                    rejections.Add(new BalanceRejection("shop", "insufficient-funds", firstPrice.ToString()));
                }
            }
        }

        private int RunCrafting(
            BalanceSimulationIterationRequest iteration,
            ProgressionContext context,
            ICollection<BalanceEquipmentObservation> equipment,
            ICollection<BalanceRewardObservation> rewards,
            ICollection<BalanceRejection> rejections,
            ref long scrap)
        {
            int unlockLevel = craftingRecipe.ResolveUnlockLevel(SubSeed(iteration.IterationSeed, "crafting-unlock", 0));
            if (context.CharacterLevel < unlockLevel)
            {
                rejections.Add(new BalanceRejection(
                    "crafting",
                    "soft-level-requirement",
                    context.CharacterLevel + "<" + unlockLevel));
                return unlockLevel;
            }

            if (scrap < craftingRecipe.ScrapCost)
            {
                rejections.Add(new BalanceRejection("crafting", "insufficient-scrap", craftingRecipe.ScrapCost.ToString()));
                return unlockLevel;
            }

            EquipmentGenerationPolicy policy = BuildCraftingGenerationPolicy();
            EquipmentGenerationResult result = generator.GenerateEquipment(
                EquipmentGenerationRequest.Create(
                    DynamicId("crafting-operation", iteration.IterationSeed, 0),
                    DynamicId("crafting-equipment", iteration.IterationSeed, 0),
                    policy,
                    catalog,
                    context,
                    SubSeed(iteration.IterationSeed, "crafting", 0),
                    DeterministicRandom.AlgorithmVersion1));
            if (!result.IsSuccess)
            {
                rejections.Add(new BalanceRejection("crafting", result.Status.ToString(), result.FailureReason));
                return unlockLevel;
            }

            scrap -= craftingRecipe.ScrapCost;
            AddEquipment("crafting", result.Equipment, equipment);
            rewards.Add(new BalanceRewardObservation("crafted-equipment", 1L));
            rewards.Add(new BalanceRewardObservation("scrap-spent-crafting", craftingRecipe.ScrapCost));
            return unlockLevel;
        }

        private void RunUpgrade(
            BalanceSimulationIterationRequest iteration,
            ProgressionContext context,
            ICollection<BalanceEquipmentObservation> equipment,
            ICollection<BalanceRewardObservation> rewards,
            ICollection<BalanceRejection> rejections,
            ref long money,
            ref long required)
        {
            EquipmentGenerationPolicy upgradeTargetPolicy = BuildPolicy("balance-policy.upgrade-target", true);
            EquipmentGenerationResult result = generator.GenerateEquipment(
                EquipmentGenerationRequest.Create(
                    DynamicId("upgrade-operation", iteration.IterationSeed, 0),
                    DynamicId("upgrade-equipment", iteration.IterationSeed, 0),
                    upgradeTargetPolicy,
                    catalog,
                    context,
                    SubSeed(iteration.IterationSeed, "upgrade", 0),
                    DeterministicRandom.AlgorithmVersion1));
            if (!result.IsSuccess)
            {
                rejections.Add(new BalanceRejection("augment-upgrade", result.Status.ToString(), result.FailureReason));
                return;
            }

            AddEquipment("augment-upgrade-target", result.Equipment, equipment);
            if (result.Equipment.Augments.Count == 0)
            {
                rejections.Add(new BalanceRejection("augment-upgrade", "missing-augment", string.Empty));
                return;
            }

            AugmentInstance augment = result.Equipment.Augments[0];
            AugmentDefinition definition = catalog.FindAugmentDefinition(augment.DefinitionId);
            if (definition == null || definition.LevelRange == null || augment.Level >= definition.LevelRange.Maximum)
            {
                rejections.Add(new BalanceRejection("augment-upgrade", "maximum-level", augment.Level.ToString()));
                return;
            }

            long cost;
            AugmentUpgradeCostStatus status = upgradeCosts.TryCalculateCost(
                augment.Tier,
                augment.Level,
                augment.Level + 1,
                out cost);
            if (status != AugmentUpgradeCostStatus.Calculated)
            {
                rejections.Add(new BalanceRejection("augment-upgrade", status.ToString(), string.Empty));
                return;
            }

            required = cost;
            if (money < cost)
            {
                rejections.Add(new BalanceRejection("augment-upgrade", "insufficient-money", cost.ToString()));
                return;
            }

            money -= cost;
            rewards.Add(new BalanceRewardObservation("augment-level-up", 1L));
            rewards.Add(new BalanceRewardObservation("money-spent-upgrade", cost));
        }

        private int CountSoftEligibleCandidates(EquipmentGenerationPolicy policy, ProgressionContext context)
        {
            int count = 0;
            for (int index = 0; index < policy.EquipmentCandidates.Count; index++)
            {
                EquipmentGenerationCandidate candidate = policy.EquipmentCandidates[index];
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
            ICollection<BalanceEquipmentObservation> output)
        {
            EquipmentDefinition definition = catalog.FindEquipmentDefinition(instance.DefinitionId);
            output.Add(new BalanceEquipmentObservation(
                source,
                instance,
                definition.CategoryId,
                definition.DisplayName));
        }

        private EquipmentGenerationPolicy BuildPolicy(string id, bool requireOneAugment)
        {
            return EquipmentGenerationPolicy.Create(
                Id(id),
                new[]
                {
                    Candidate(GunPulse, 1L, 1.0),
                    Candidate(ArmorReactive, 8L, 1.0),
                    Candidate(GunScatter, 18L, 1.0),
                },
                new[]
                {
                    EquipmentQualityCandidate.Create(QualityCommon, 0L, 8UL),
                    EquipmentQualityCandidate.Create(QualityRare, 8L, 3UL),
                    EquipmentQualityCandidate.Create(QualityExceptional, 20L, 1UL),
                },
                new[]
                {
                    AugmentGenerationCandidate.Create(AugmentPower, 0, 1000, 2UL),
                    AugmentGenerationCandidate.Create(AugmentGuard, 0, 1000, 2UL),
                },
                requireOneAugment ? 1 : 0,
                requireOneAugment ? 1 : 3,
                requireOneAugment,
                new SoftActivationCurveParameters(0.08, 12L, 8L),
                new ObsolescenceCurveParameters(30L, 20.0, 0.15));
        }

        private EquipmentGenerationPolicy BuildStrongboxPolicy()
        {
            return EquipmentGenerationPolicy.Create(
                Id("balance-policy.strongbox"),
                new[] { Candidate(GunPulse, 0L, 1.0) },
                new[]
                {
                    EquipmentQualityCandidate.Create(QualityCommon, 0L, 8UL),
                    EquipmentQualityCandidate.Create(QualityRare, 8L, 3UL),
                    EquipmentQualityCandidate.Create(QualityExceptional, 20L, 1UL),
                },
                new[]
                {
                    AugmentGenerationCandidate.Create(AugmentPower, 0, 1000, 2UL),
                    AugmentGenerationCandidate.Create(AugmentGuard, 0, 1000, 2UL),
                },
                0,
                2,
                false,
                new SoftActivationCurveParameters(0.08, 12L, 8L),
                new ObsolescenceCurveParameters(30L, 20.0, 0.15));
        }

        private EquipmentGenerationPolicy BuildCraftingGenerationPolicy()
        {
            return EquipmentGenerationPolicy.Create(
                Id("balance-policy.crafting"),
                new[] { Candidate(craftingRecipe.TargetEquipmentDefinitionStableId, 0L, 1.0) },
                new[]
                {
                    EquipmentQualityCandidate.Create(QualityCommon, 0L, 3UL),
                    EquipmentQualityCandidate.Create(QualityRare, 0L, 1UL),
                },
                new[] { AugmentGenerationCandidate.Create(AugmentPower, 0, 1000, 1UL) },
                craftingRecipe.MinimumAugmentSlots,
                craftingRecipe.MaximumAugmentSlots,
                true,
                craftingRecipe.GeneratorPolicy.Activation,
                craftingRecipe.GeneratorPolicy.Obsolescence);
        }

        private CraftingRecipe BuildCraftingRecipe()
        {
            return new CraftingRecipe(
                1,
                Id("recipe.gun-scatter"),
                GunScatter,
                Id("source.natural.gun-scatter"),
                18,
                18,
                4,
                new CraftingDelayVariance(0, 2),
                50L,
                CraftingQualityPolicyKind.DeterministicWeightedRandom,
                new[]
                {
                    new CraftingWeightedDefinition(QualityCommon, 3UL),
                    new CraftingWeightedDefinition(QualityRare, 1UL),
                },
                1,
                100,
                1,
                1,
                3,
                10,
                new[] { new CraftingWeightedDefinition(AugmentPower, 1UL) },
                new CraftingGeneratorPolicy(
                    Id("crafting-generator.gun-scatter"),
                    DeterministicRandom.AlgorithmVersion1,
                    new SoftActivationCurveParameters(0.08, 12L, 8L),
                    new ObsolescenceCurveParameters(30L, 20.0, 0.15)));
        }

        private StrongboxDefinition BuildStrongboxDefinition(StableId tierId, int tier)
        {
            RewardGrantSpecification equipment = RewardGrantSpecification.CreateFixed(
                Id("grant.balance-strongbox-equipment-spec"),
                RewardGrantKind.EquipmentReference,
                GunPulse,
                2L);
            RewardProfile profile = RewardProfile.Create(
                Id("profile.balance-strongbox"),
                new[] { equipment },
                Array.Empty<IndependentRewardRoll>(),
                Array.Empty<ExclusiveRewardGroup>());
            return StrongboxDefinition.Create(
                tierId,
                tier,
                Math.Max(1, tier + 1),
                Math.Max(1, tier + 1),
                tier,
                StrongboxRewardCountPolicy.Create(2, 2),
                StrongboxMandatoryScrapPolicy.Create(ScrapCurrency, Math.Max(1, tier + 1), Math.Max(1, tier + 1)),
                strongboxPolicy.PolicyId,
                profile,
                TierScaling,
                ExceptionalScaling);
        }

        private EquipmentGenerationCandidate Candidate(StableId definitionId, long nominalLevel, double weight)
        {
            return EquipmentGenerationCandidate.Create(
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
                GunPulse,
                EquipmentCategoryIds.Gun,
                Id("equipment-family.pulse"),
                "Pulse Gun",
                Id("gun.runtime.pulse"),
                InclusiveIntRange.Create(1, 100),
                3,
                new[] { common, rare, exceptional },
                Array.Empty<StableId>());
            EquipmentDefinition scatter = EquipmentDefinition.Create(
                GunScatter,
                EquipmentCategoryIds.Gun,
                Id("equipment-family.scatter"),
                "Scatter Gun",
                Id("gun.runtime.scatter"),
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

        private static StableId Id(string value)
        {
            int separatorIndex = value.IndexOf('.');
            if (separatorIndex < 1 || separatorIndex == value.Length - 1)
            {
                return StableId.Create("balance-simulator", value.Replace('.', '-'));
            }

            return StableId.Create(
                value.Substring(0, separatorIndex),
                value.Substring(separatorIndex + 1).Replace('.', '-'));
        }
    }
}
