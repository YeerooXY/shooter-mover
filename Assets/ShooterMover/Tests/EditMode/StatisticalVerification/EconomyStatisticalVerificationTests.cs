using System;
using System.Collections.Generic;
using System.Globalization;
using NUnit.Framework;
using ShooterMover.Application.Crafting;
using ShooterMover.Application.Economy.Money;
using ShooterMover.Application.Economy.Scrap;
using ShooterMover.Application.Holdings;
using ShooterMover.Application.Rewards.Application;
using ShooterMover.Application.Rewards.Generation;
using ShooterMover.Application.Rewards.Strongboxes;
using ShooterMover.Application.Shops;
using ShooterMover.Contracts.Equipment;
using ShooterMover.Contracts.Holdings;
using ShooterMover.Contracts.Rewards;
using ShooterMover.Contracts.Rewards.Application;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Common.Random;
using ShooterMover.Domain.Crafting;
using ShooterMover.Domain.Economy.Money;
using ShooterMover.Domain.Economy.Scrap;
using ShooterMover.Domain.Equipment;
using ShooterMover.Domain.Equipment.Upgrades;
using ShooterMover.Domain.Holdings;
using ShooterMover.Domain.Progression.Context;
using ShooterMover.Domain.Progression.Curves;
using ShooterMover.Domain.Rewards.Generation;
using ShooterMover.Domain.Rewards.Model;
using ShooterMover.Domain.Rewards.Strongboxes;
using ShooterMover.Domain.Shops;

namespace ShooterMover.Tests.EditMode.StatisticalVerification
{
    public sealed class EconomyStatisticalVerificationTests
    {
        private static readonly StableId ScrapAuthorityId = Id("authority.stat.scrap");
        private static readonly StableId ScrapCurrencyId = Id("currency.scrap");
        private static readonly StableId HoldingsAuthorityId = Id("holdings.stat.player");
        private static readonly StableId RapAuthorityId = Id("authority.stat.rap");
        private static readonly StableId CommonQualityId = Id("quality.common");
        private static readonly StableId RareQualityId = Id("quality.rare");

        [Test]
        public void ShopInventoryRollsAreSeededReproducibleAndStayInsideToleranceBands()
        {
            ShopBatch first = RunShopBatch(1000);
            ShopBatch replay = RunShopBatch(1000);

            Assert.That(replay.Fingerprint, Is.EqualTo(first.Fingerprint));
            Assert.That(replay.EntryCount, Is.EqualTo(first.EntryCount));
            Assert.That(replay.AlphaDefinitionCount, Is.EqualTo(first.AlphaDefinitionCount));
            Assert.That(replay.RareQualityCount, Is.EqualTo(first.RareQualityCount));
            Assert.That(first.RejectionCount, Is.Zero);
            Assert.That(first.NonPositivePriceCount, Is.Zero);
            Assert.That(first.EntryCount, Is.EqualTo(4000));

            StatisticalVerificationAssertions.Proportion(
                "shop alpha-definition selection",
                first.AlphaDefinitionCount,
                first.EntryCount,
                0.35,
                0.65);
            StatisticalVerificationAssertions.Proportion(
                "shop rare-quality selection",
                first.RareQualityCount,
                first.EntryCount,
                0.15,
                0.35);
        }

        [Test]
        public void CraftingUnlockGatesAreSeededReproducibleAndDistributedAcrossConfiguredBand()
        {
            CraftingRecipeV1 recipe = BuildCraftingRecipe();
            int[] first = ResolveCraftingUnlocks(recipe, 1000, 0xC8AF71UL);
            int[] replay = ResolveCraftingUnlocks(recipe, 1000, 0xC8AF71UL);
            long at55 = 0L;
            long at56 = 0L;
            long at57 = 0L;
            List<string> firstValues = new List<string>(first.Length);
            List<string> replayValues = new List<string>(replay.Length);

            for (int index = 0; index < first.Length; index++)
            {
                Assert.That(first[index], Is.EqualTo(replay[index]));
                Assert.That(first[index], Is.InRange(55, 57));
                Assert.That(first[index], Is.GreaterThan(recipe.OrdinaryDiscoveryActivationLevel));
                firstValues.Add(first[index].ToString(CultureInfo.InvariantCulture));
                replayValues.Add(replay[index].ToString(CultureInfo.InvariantCulture));
                if (first[index] == 55) { at55++; }
                else if (first[index] == 56) { at56++; }
                else if (first[index] == 57) { at57++; }
            }

            Assert.That(
                StatisticalVerificationAssertions.Fingerprint(replayValues),
                Is.EqualTo(StatisticalVerificationAssertions.Fingerprint(firstValues)));
            StatisticalVerificationAssertions.Proportion("craft unlock level 55", at55, first.Length, 0.20, 0.46);
            StatisticalVerificationAssertions.Proportion("craft unlock level 56", at56, first.Length, 0.20, 0.46);
            StatisticalVerificationAssertions.Proportion("craft unlock level 57", at57, first.Length, 0.20, 0.46);

            ulong boundarySeed = StatisticalVerificationAssertions.Seed(0xC8AF71UL, 7);
            int boundary = recipe.ResolveUnlockLevel(boundarySeed);
            CraftingGateFixture belowFixture = new CraftingGateFixture(recipe);
            CraftingResultV1 below = belowFixture.Craft(
                "stat.craft.below",
                boundarySeed,
                boundary - 1);
            CraftingGateFixture unlockedFixture = new CraftingGateFixture(recipe);
            CraftingResultV1 unlocked = unlockedFixture.Craft(
                "stat.craft.unlocked",
                boundarySeed,
                boundary);

            Assert.That(below.Status, Is.EqualTo(CraftingResultStatusV1.ProgressionUnavailable));
            Assert.That(below.UnlockLevel, Is.EqualTo(boundary));
            Assert.That(unlocked.Status, Is.EqualTo(CraftingResultStatusV1.Crafted));
            Assert.That(unlocked.UnlockLevel, Is.EqualTo(boundary));
        }

        [Test]
        public void AugmentUpgradeCostsAreReproducibleMonotonicAndTierOrdered()
        {
            AugmentUpgradeCostPolicyV1 policy = AugmentUpgradeCostPolicyV1.Create(
                Id("stat.augment-upgrade-cost-policy"),
                1,
                false,
                new[]
                {
                    AugmentTierCostCurveV1.Create(1, 100L, 10L),
                    AugmentTierCostCurveV1.Create(2, 250L, 25L),
                    AugmentTierCostCurveV1.Create(3, 500L, 50L)
                });
            List<string> first = CalculateUpgradeCosts(policy);
            List<string> replay = CalculateUpgradeCosts(policy);

            Assert.That(
                StatisticalVerificationAssertions.Fingerprint(replay),
                Is.EqualTo(StatisticalVerificationAssertions.Fingerprint(first)));

            long previousTierOne = 0L;
            long previousTierTwo = 0L;
            long previousTierThree = 0L;
            for (int targetLevel = 2; targetLevel <= 10; targetLevel++)
            {
                long tierOne;
                long tierTwo;
                long tierThree;
                Assert.That(
                    policy.TryCalculateCost(1, targetLevel - 1, targetLevel, out tierOne),
                    Is.EqualTo(AugmentUpgradeCostStatusV1.Calculated));
                Assert.That(
                    policy.TryCalculateCost(2, targetLevel - 1, targetLevel, out tierTwo),
                    Is.EqualTo(AugmentUpgradeCostStatusV1.Calculated));
                Assert.That(
                    policy.TryCalculateCost(3, targetLevel - 1, targetLevel, out tierThree),
                    Is.EqualTo(AugmentUpgradeCostStatusV1.Calculated));

                Assert.That(tierOne, Is.GreaterThan(previousTierOne));
                Assert.That(tierTwo, Is.GreaterThan(previousTierTwo));
                Assert.That(tierThree, Is.GreaterThan(previousTierThree));
                Assert.That(tierTwo, Is.GreaterThan(tierOne));
                Assert.That(tierThree, Is.GreaterThan(tierTwo));
                Assert.That((double)tierThree / tierOne, Is.InRange(4.5, 5.5));

                previousTierOne = tierOne;
                previousTierTwo = tierTwo;
                previousTierThree = tierThree;
            }
        }

        [TestCase(100)]
        [TestCase(1000)]
        public void StrongboxMoneyAndScrapRewardBatchesRemainExactlyReproducible(int openCount)
        {
            EconomyRewardBatch first = RunEconomyStrongboxBatch(openCount, 0xEC0A0A1UL);
            EconomyRewardBatch replay = RunEconomyStrongboxBatch(openCount, 0xEC0A0A1UL);

            Assert.That(first.RejectionCount, Is.Zero);
            Assert.That(replay.RejectionCount, Is.Zero);
            Assert.That(replay.Fingerprint, Is.EqualTo(first.Fingerprint));
            Assert.That(replay.TotalMoney, Is.EqualTo(first.TotalMoney));
            Assert.That(replay.TotalScrap, Is.EqualTo(first.TotalScrap));
            Assert.That(
                StatisticalVerificationAssertions.Mean(first.TotalMoney, openCount),
                Is.InRange(8.0, 12.0));
            Assert.That(
                StatisticalVerificationAssertions.Mean(first.TotalScrap, openCount),
                Is.InRange(4.0, 6.0));
        }

        [Test]
        public void ExactStrongboxReplayAddsNoMoneyScrapOrHoldingsValue()
        {
            EconomyStrongboxFixture fixture = new EconomyStrongboxFixture();
            PreparedStrongboxOpen prepared = fixture.Prepare(0, 0x1D3A90UL);
            StrongboxOpeningResultRuntimeV1 first = fixture.Service.Open(prepared.Command);
            long moneyAfterFirst = fixture.Money.Balance;
            long moneySequenceAfterFirst = fixture.Money.Sequence;
            long scrapAfterFirst = fixture.Scrap.Balance;
            long scrapSequenceAfterFirst = fixture.Scrap.Sequence;
            long holdingsSequenceAfterFirst = fixture.Holdings.Sequence;
            long openingSequenceAfterFirst = fixture.Service.Sequence;
            long rapSequenceAfterFirst = fixture.Rap.Sequence;

            StrongboxOpeningResultRuntimeV1 replay = fixture.Service.Open(prepared.Command);

            Assert.That(first.Status, Is.EqualTo(StrongboxOpeningRuntimeStatusV1.Opened));
            Assert.That(replay.Status, Is.EqualTo(StrongboxOpeningRuntimeStatusV1.ExactDuplicateNoChange));
            Assert.That(replay.TerminalFact.Fingerprint, Is.EqualTo(first.TerminalFact.Fingerprint));
            Assert.That(fixture.Money.Balance, Is.EqualTo(moneyAfterFirst));
            Assert.That(fixture.Money.Sequence, Is.EqualTo(moneySequenceAfterFirst));
            Assert.That(fixture.Scrap.Balance, Is.EqualTo(scrapAfterFirst));
            Assert.That(fixture.Scrap.Sequence, Is.EqualTo(scrapSequenceAfterFirst));
            Assert.That(fixture.Holdings.Sequence, Is.EqualTo(holdingsSequenceAfterFirst));
            Assert.That(fixture.Service.Sequence, Is.EqualTo(openingSequenceAfterFirst));
            Assert.That(fixture.Rap.Sequence, Is.EqualTo(rapSequenceAfterFirst));
        }

        private static ShopBatch RunShopBatch(int inventoryCount)
        {
            ShopFixture fixture = new ShopFixture();
            List<string> fingerprints = new List<string>(inventoryCount);
            long entryCount = 0L;
            long alphaDefinitionCount = 0L;
            long rareQualityCount = 0L;
            long rejectionCount = 0L;
            long nonPositivePriceCount = 0L;

            for (int index = 0; index < inventoryCount; index++)
            {
                string suffix = index.ToString("D4", CultureInfo.InvariantCulture);
                ShopInventoryOpenResultV1 result = fixture.Service.Open(
                    Id("stat.shop.run." + suffix),
                    fixture.Definition,
                    fixture.Catalog,
                    Context(10));
                if (!result.Succeeded || result.Inventory == null)
                {
                    rejectionCount++;
                    fingerprints.Add("rejected:" + result.RejectionCode);
                    continue;
                }

                fingerprints.Add(result.Inventory.InventoryFingerprint);
                foreach (ShopStockEntryV1 entry in result.Inventory.Entries)
                {
                    entryCount++;
                    if (entry.Price <= 0L) { nonPositivePriceCount++; }
                    if (entry.Equipment.DefinitionId == Id("stat.shop.armor-alpha"))
                    {
                        alphaDefinitionCount++;
                    }
                    else
                    {
                        Assert.That(entry.Equipment.DefinitionId, Is.EqualTo(Id("stat.shop.armor-beta")));
                    }

                    if (entry.Equipment.QualityId == RareQualityId)
                    {
                        rareQualityCount++;
                    }
                    else
                    {
                        Assert.That(entry.Equipment.QualityId, Is.EqualTo(CommonQualityId));
                    }
                }
            }

            return new ShopBatch(
                entryCount,
                alphaDefinitionCount,
                rareQualityCount,
                rejectionCount,
                nonPositivePriceCount,
                StatisticalVerificationAssertions.Fingerprint(fingerprints));
        }

        private static int[] ResolveCraftingUnlocks(
            CraftingRecipeV1 recipe,
            int sampleCount,
            ulong rootSeed)
        {
            int[] values = new int[sampleCount];
            for (int index = 0; index < sampleCount; index++)
            {
                values[index] = recipe.ResolveUnlockLevel(
                    StatisticalVerificationAssertions.Seed(rootSeed, index));
            }

            return values;
        }

        private static List<string> CalculateUpgradeCosts(AugmentUpgradeCostPolicyV1 policy)
        {
            List<string> values = new List<string>();
            for (int tier = 1; tier <= 3; tier++)
            {
                for (int targetLevel = 2; targetLevel <= 10; targetLevel++)
                {
                    long cost;
                    AugmentUpgradeCostStatusV1 status = policy.TryCalculateCost(
                        tier,
                        targetLevel - 1,
                        targetLevel,
                        out cost);
                    Assert.That(status, Is.EqualTo(AugmentUpgradeCostStatusV1.Calculated));
                    values.Add(tier.ToString(CultureInfo.InvariantCulture)
                        + ":" + targetLevel.ToString(CultureInfo.InvariantCulture)
                        + ":" + cost.ToString(CultureInfo.InvariantCulture));
                }
            }

            return values;
        }

        private static EconomyRewardBatch RunEconomyStrongboxBatch(
            int openCount,
            ulong rootSeed)
        {
            EconomyStrongboxFixture fixture = new EconomyStrongboxFixture();
            List<string> fingerprints = new List<string>(openCount);
            long totalMoney = 0L;
            long totalScrap = 0L;
            long rejectionCount = 0L;

            for (int index = 0; index < openCount; index++)
            {
                PreparedStrongboxOpen prepared = fixture.Prepare(
                    index,
                    StatisticalVerificationAssertions.Seed(rootSeed, index));
                long moneyBefore = fixture.Money.Balance;
                long scrapBefore = fixture.Scrap.Balance;
                StrongboxOpeningResultRuntimeV1 result = fixture.Service.Open(prepared.Command);
                if (result.Status != StrongboxOpeningRuntimeStatusV1.Opened)
                {
                    rejectionCount++;
                    fingerprints.Add("rejected:" + result.Status + ":" + result.RejectionCode);
                    continue;
                }

                long moneyDelta = fixture.Money.Balance - moneyBefore;
                long scrapDelta = fixture.Scrap.Balance - scrapBefore;
                Assert.That(moneyDelta, Is.InRange(5L, 15L));
                Assert.That(scrapDelta, Is.InRange(2L, 8L));
                totalMoney += moneyDelta;
                totalScrap += scrapDelta;
                fingerprints.Add(result.GeneratedOutcome.Fingerprint
                    + "|money=" + moneyDelta.ToString(CultureInfo.InvariantCulture)
                    + "|scrap=" + scrapDelta.ToString(CultureInfo.InvariantCulture));
            }

            return new EconomyRewardBatch(
                totalMoney,
                totalScrap,
                rejectionCount,
                StatisticalVerificationAssertions.Fingerprint(fingerprints));
        }

        private static CraftingRecipeV1 BuildCraftingRecipe()
        {
            return new CraftingRecipeV1(
                1,
                Id("stat.recipe.weapon"),
                Id("stat.craft.weapon"),
                Id("stat.discovery.weapon"),
                50,
                50,
                5,
                new CraftingDelayVarianceV1(0, 2),
                10L,
                CraftingQualityPolicyKindV1.Fixed,
                new[] { new CraftingWeightedDefinitionV1(CommonQualityId, 1UL) },
                50,
                60,
                0,
                0,
                1,
                1,
                Array.Empty<CraftingWeightedDefinitionV1>(),
                new CraftingGeneratorPolicyV1(
                    Id("stat.crafting.generator-policy"),
                    DeterministicRandom.AlgorithmVersion1,
                    new SoftActivationCurveParameters(0.25, 5L, 5L),
                    new ObsolescenceCurveParameters(100L, 50.0, 0.25)));
        }

        private static EquipmentCatalog BuildCraftingCatalog()
        {
            EquipmentQualityTier common = EquipmentQualityTier.Create(CommonQualityId, "Common", 1);
            EquipmentDefinition weapon = EquipmentDefinition.Create(
                Id("stat.craft.weapon"),
                EquipmentCategoryIds.Weapon,
                Id("stat.craft.weapon-family"),
                "Stat Craft Weapon",
                Id("stat.craft.weapon-runtime"),
                InclusiveIntRange.Create(1, 100),
                0,
                new[] { common },
                Array.Empty<StableId>());
            EquipmentCatalogBuildResult build = EquipmentCatalog.Build(
                new[] { weapon },
                Array.Empty<AugmentDefinition>());
            Assert.That(build.IsValid, Is.True);
            return build.Catalog;
        }

        private static EquipmentCatalog BuildShopCatalog()
        {
            EquipmentQualityTier common = EquipmentQualityTier.Create(CommonQualityId, "Common", 1);
            EquipmentQualityTier rare = EquipmentQualityTier.Create(RareQualityId, "Rare", 2);
            StableId shopTag = Id("stat.shop.tag");
            EquipmentDefinition alpha = EquipmentDefinition.Create(
                Id("stat.shop.armor-alpha"),
                EquipmentCategoryIds.Armor,
                Id("stat.shop.family-alpha"),
                "Stat Armor Alpha",
                null,
                InclusiveIntRange.Create(1, 20),
                0,
                new[] { common, rare },
                new[] { shopTag });
            EquipmentDefinition beta = EquipmentDefinition.Create(
                Id("stat.shop.armor-beta"),
                EquipmentCategoryIds.Armor,
                Id("stat.shop.family-beta"),
                "Stat Armor Beta",
                null,
                InclusiveIntRange.Create(1, 20),
                0,
                new[] { common, rare },
                new[] { shopTag });
            EquipmentCatalogBuildResult build = EquipmentCatalog.Build(
                new[] { alpha, beta },
                Array.Empty<AugmentDefinition>());
            Assert.That(build.IsValid, Is.True);
            return build.Catalog;
        }

        private static ShopDefinitionV1 BuildShopDefinition()
        {
            EquipmentGenerationPolicyV1 generation = EquipmentGenerationPolicyV1.Create(
                Id("stat.shop.generation-policy"),
                new[]
                {
                    ShopCandidate("stat.shop.armor-alpha"),
                    ShopCandidate("stat.shop.armor-beta")
                },
                new[]
                {
                    EquipmentQualityCandidateV1.Create(CommonQualityId, 0L, 3UL),
                    EquipmentQualityCandidateV1.Create(RareQualityId, 0L, 1UL)
                },
                Array.Empty<AugmentGenerationCandidateV1>(),
                0,
                0,
                true,
                new SoftActivationCurveParameters(0.10, 5L, 5L),
                new ObsolescenceCurveParameters(25L, 15.0, 0.20));
            ShopPricingPolicyV1 pricing = ShopPricingPolicyV1.Create(
                Id("stat.shop.pricing-policy"),
                1L,
                20L,
                3L,
                11L,
                17L,
                5L,
                2L);
            return ShopDefinitionV1.Create(
                Id("stat.shop.definition"),
                4,
                new[] { EquipmentCategoryIds.Armor },
                new[] { Id("stat.shop.tag") },
                Array.Empty<StableId>(),
                generation,
                ShopProgressionContextPolicyV1.FreezeOnFirstOpen,
                pricing,
                ShopRefreshPolicyV1.Disabled,
                0,
                0,
                DeterministicRandom.AlgorithmVersion1);
        }

        private static EquipmentGenerationCandidateV1 ShopCandidate(string definitionId)
        {
            return EquipmentGenerationCandidateV1.Create(
                Id(definitionId),
                0,
                100,
                0,
                100,
                Array.Empty<StableId>(),
                0L,
                InclusiveIntRange.Create(1, 20),
                1.0,
                1.0);
        }

        private static ProgressionContext Context(int characterLevel)
        {
            return ProgressionContext.Create(
                characterLevel,
                1,
                Id("difficulty.normal"),
                1,
                Array.Empty<StableId>());
        }

        private static void FundScrap(ScrapWalletServiceV1 wallet, long amount)
        {
            ScrapTransactionResultV1 result = wallet.Apply(
                new ScrapTransactionCommandV1(
                    Id("stat.scrap.initial.transaction"),
                    Id("stat.scrap.initial.operation"),
                    wallet.AuthorityStableId,
                    wallet.CurrencyStableId,
                    ScrapMutationKindV1.Grant,
                    amount,
                    ScrapIdentityV1.RewardGrantReason,
                    new ScrapProvenanceV1(
                        ScrapIdentityV1.RewardSourceKind,
                        Id("stat.scrap.initial.reward-operation"),
                        Id("stat.player"))));
            Assert.That(result.ChangedState, Is.True);
        }

        private static StableId Id(string value)
        {
            return StableId.Parse(value);
        }

        private sealed class ShopFixture
        {
            public ShopFixture()
            {
                Catalog = BuildShopCatalog();
                Definition = BuildShopDefinition();
                Money = new MoneyWalletService();
                ScrapWalletServiceV1 scrap = new ScrapWalletServiceV1(
                    ScrapAuthorityId,
                    ScrapCurrencyId);
                CatalogValidator validator = new CatalogValidator(Catalog);
                PlayerHoldingsService holdings = new PlayerHoldingsService(
                    HoldingsAuthorityId,
                    10000L,
                    validator);
                RewardApplicationServiceV1 rap = new RewardApplicationServiceV1(
                    RapAuthorityId,
                    new MoneyRewardChildAuthorityV1(Money),
                    new ScrapRewardChildAuthorityV1(scrap),
                    new PlayerHoldingsRewardChildAuthorityV1(holdings, validator));
                Service = new ShopRuntimeServiceV1(
                    new RewardGenerationServiceV1(),
                    Money,
                    rap,
                    ScrapAuthorityId,
                    HoldingsAuthorityId);
            }

            public EquipmentCatalog Catalog { get; }
            public ShopDefinitionV1 Definition { get; }
            public MoneyWalletService Money { get; }
            public ShopRuntimeServiceV1 Service { get; }
        }

        private sealed class CraftingGateFixture
        {
            public CraftingGateFixture(CraftingRecipeV1 recipe)
            {
                EquipmentCatalog catalog = BuildCraftingCatalog();
                CatalogValidator validator = new CatalogValidator(catalog);
                MoneyWalletService money = new MoneyWalletService();
                ScrapWalletServiceV1 scrap = new ScrapWalletServiceV1(
                    ScrapAuthorityId,
                    ScrapCurrencyId);
                FundScrap(scrap, 100L);
                PlayerHoldingsService holdings = new PlayerHoldingsService(
                    HoldingsAuthorityId,
                    100L,
                    validator);
                RewardApplicationServiceV1 rap = new RewardApplicationServiceV1(
                    RapAuthorityId,
                    new MoneyRewardChildAuthorityV1(money),
                    new CraftingScrapSpendRewardChildAuthorityV1(scrap),
                    new PlayerHoldingsRewardChildAuthorityV1(holdings, validator));
                Service = new CraftingServiceV1(
                    new CraftingRecipeCatalogV1(new[] { recipe }),
                    catalog,
                    new RewardGenerationServiceV1(),
                    rap,
                    scrap,
                    MoneyWalletIdsV1.AuthorityStableId,
                    HoldingsAuthorityId);
                Recipe = recipe;
            }

            public CraftingRecipeV1 Recipe { get; }
            public CraftingServiceV1 Service { get; }

            public CraftingResultV1 Craft(string transactionId, ulong rootSeed, int characterLevel)
            {
                return Service.Craft(
                    new CraftEquipmentCommandV1(
                        Id(transactionId),
                        Recipe.RecipeStableId,
                        Id("stat.craft.run"),
                        Id("stat.player"),
                        Context(characterLevel),
                        rootSeed));
            }
        }

        private sealed class EconomyStrongboxFixture
        {
            private static readonly StableId TierId = Id("stat.economy-box.tier");
            private static readonly StableId PlayerId = Id("stat.player");

            public EconomyStrongboxFixture()
            {
                RewardGrantSpecificationV1 moneyGrant = RewardGrantSpecificationV1.Create(
                    Id("stat.economy-box.money-grant"),
                    RewardGrantKindV1.Money,
                    MoneyWalletIdsV1.CurrencyStableId,
                    RewardQuantityRangeV1.Create(5L, 15L),
                    Array.Empty<RewardScalingInputDescriptorV1>());
                RewardProfileV1 profile = RewardProfileV1.Create(
                    Id("stat.economy-box.profile"),
                    new[] { moneyGrant },
                    Array.Empty<IndependentRewardRollV1>(),
                    Array.Empty<ExclusiveRewardGroupV1>());
                Definition = StrongboxDefinitionV1.Create(
                    TierId,
                    0,
                    1L,
                    1L,
                    0L,
                    StrongboxRewardCountPolicyV1.Create(2, 2),
                    StrongboxMandatoryScrapPolicyV1.Create(ScrapCurrencyId, 2L, 8L),
                    Id("stat.economy-box.generation-policy"),
                    profile,
                    Id("stat.scaling.source-tier"),
                    Id("stat.scaling.exceptional"));
                Money = new MoneyWalletService();
                Scrap = new ScrapWalletServiceV1(ScrapAuthorityId, ScrapCurrencyId);
                AcceptingEquipmentValidator validator = new AcceptingEquipmentValidator();
                Holdings = new PlayerHoldingsService(HoldingsAuthorityId, 10000L, validator);
                Rap = new RewardApplicationServiceV1(
                    RapAuthorityId,
                    new MoneyRewardChildAuthorityV1(Money),
                    new ScrapRewardChildAuthorityV1(Scrap),
                    new PlayerHoldingsRewardChildAuthorityV1(Holdings, validator));
                Service = new StrongboxOpeningServiceV1(
                    new StrongboxDefinitionCatalogV1(new[] { Definition }),
                    new SharedStrongboxRewardGeneratorV1(new RewardGenerationServiceV1()),
                    Holdings,
                    Rap,
                    new DeterministicStrongboxGrantPayloadResolverV1());
            }

            public StrongboxDefinitionV1 Definition { get; }
            public MoneyWalletService Money { get; }
            public ScrapWalletServiceV1 Scrap { get; }
            public PlayerHoldingsService Holdings { get; }
            public RewardApplicationServiceV1 Rap { get; }
            public StrongboxOpeningServiceV1 Service { get; }

            public PreparedStrongboxOpen Prepare(int index, ulong seed)
            {
                string suffix = index.ToString("D4", CultureInfo.InvariantCulture);
                StableId boxId = Id("stat.economy-box.instance." + suffix);
                PlayerHoldingsMutationResultV1 added = Holdings.Apply(
                    PlayerHoldingsCommandV1.AddStrongbox(
                        Id("stat.economy-box.add-tx." + suffix),
                        Id("stat.economy-box.add-op." + suffix),
                        HoldingsAuthorityId,
                        Definition.TierStableId,
                        boxId,
                        HoldingProvenanceV1.Create(
                            Id("stat.economy-box.add-grant." + suffix),
                            Id("stat.economy-box.add-source." + suffix))));
                Assert.That(added.Status, Is.EqualTo(PlayerHoldingsMutationStatusV1.Applied));
                StrongboxInstanceContextV1 context = StrongboxInstanceContextV1.Create(
                    boxId,
                    Definition.TierStableId,
                    seed,
                    DeterministicRandom.AlgorithmVersion1,
                    Context(20),
                    Id("stat.economy-box.source." + suffix),
                    Id("stat.economy-box.provenance." + suffix),
                    Definition.Fingerprint);
                StrongboxRegistrationResultV1 registered = Service.RegisterInstance(context);
                Assert.That(registered.Status, Is.EqualTo(StrongboxRegistrationStatusV1.Registered));
                StrongboxOpenCommandV1 command = StrongboxOpenCommandV1.Create(
                    Id("stat.economy-box.opening." + suffix),
                    Id("stat.economy-box.run"),
                    boxId,
                    PlayerId,
                    MoneyWalletIdsV1.AuthorityStableId,
                    ScrapAuthorityId,
                    HoldingsAuthorityId);
                return new PreparedStrongboxOpen(command);
            }
        }

        private sealed class PreparedStrongboxOpen
        {
            public PreparedStrongboxOpen(StrongboxOpenCommandV1 command)
            {
                Command = command;
            }

            public StrongboxOpenCommandV1 Command { get; }
        }

        private sealed class CatalogValidator : IEquipmentInstanceValidator
        {
            private readonly EquipmentCatalog catalog;

            public CatalogValidator(EquipmentCatalog catalog)
            {
                this.catalog = catalog;
            }

            public EquipmentInstanceValidationResponse Validate(
                EquipmentInstanceValidationRequest request)
            {
                EquipmentInstance instance = request == null ? null : request.Instance;
                return EquipmentInstanceValidationResponse.From(
                    catalog,
                    instance,
                    catalog.ValidateInstance(instance));
            }
        }

        private sealed class AcceptingEquipmentValidator : IEquipmentInstanceValidator
        {
            public EquipmentInstanceValidationResponse Validate(
                EquipmentInstanceValidationRequest request)
            {
                return new EquipmentInstanceValidationResponse(
                    request != null && request.Instance != null,
                    "stat-accepting-validator",
                    request == null || request.Instance == null
                        ? null
                        : request.Instance.Fingerprint,
                    Array.Empty<EquipmentModelIssue>());
            }
        }

        private sealed class ShopBatch
        {
            public ShopBatch(
                long entryCount,
                long alphaDefinitionCount,
                long rareQualityCount,
                long rejectionCount,
                long nonPositivePriceCount,
                string fingerprint)
            {
                EntryCount = entryCount;
                AlphaDefinitionCount = alphaDefinitionCount;
                RareQualityCount = rareQualityCount;
                RejectionCount = rejectionCount;
                NonPositivePriceCount = nonPositivePriceCount;
                Fingerprint = fingerprint;
            }

            public long EntryCount { get; }
            public long AlphaDefinitionCount { get; }
            public long RareQualityCount { get; }
            public long RejectionCount { get; }
            public long NonPositivePriceCount { get; }
            public string Fingerprint { get; }
        }

        private sealed class EconomyRewardBatch
        {
            public EconomyRewardBatch(
                long totalMoney,
                long totalScrap,
                long rejectionCount,
                string fingerprint)
            {
                TotalMoney = totalMoney;
                TotalScrap = totalScrap;
                RejectionCount = rejectionCount;
                Fingerprint = fingerprint;
            }

            public long TotalMoney { get; }
            public long TotalScrap { get; }
            public long RejectionCount { get; }
            public string Fingerprint { get; }
        }
    }
}
