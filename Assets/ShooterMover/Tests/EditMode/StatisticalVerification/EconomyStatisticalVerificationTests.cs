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
            CraftingRecipe recipe = BuildCraftingRecipe();
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
            CraftingResult below = belowFixture.Craft(
                "stat.craft.below",
                boundarySeed,
                boundary - 1);
            CraftingGateFixture unlockedFixture = new CraftingGateFixture(recipe);
            CraftingResult unlocked = unlockedFixture.Craft(
                "stat.craft.unlocked",
                boundarySeed,
                boundary);

            Assert.That(below.Status, Is.EqualTo(CraftingResultStatus.ProgressionUnavailable));
            Assert.That(below.UnlockLevel, Is.EqualTo(boundary));
            Assert.That(unlocked.Status, Is.EqualTo(CraftingResultStatus.Crafted));
            Assert.That(unlocked.UnlockLevel, Is.EqualTo(boundary));
        }

        [Test]
        public void AugmentUpgradeCostsAreReproducibleMonotonicAndTierOrdered()
        {
            AugmentUpgradeCostPolicy policy = AugmentUpgradeCostPolicy.Create(
                Id("stat.augment-upgrade-cost-policy"),
                1,
                false,
                new[]
                {
                    AugmentTierCostCurve.Create(1, 100L, 10L),
                    AugmentTierCostCurve.Create(2, 250L, 25L),
                    AugmentTierCostCurve.Create(3, 500L, 50L)
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
                    Is.EqualTo(AugmentUpgradeCostStatus.Calculated));
                Assert.That(
                    policy.TryCalculateCost(2, targetLevel - 1, targetLevel, out tierTwo),
                    Is.EqualTo(AugmentUpgradeCostStatus.Calculated));
                Assert.That(
                    policy.TryCalculateCost(3, targetLevel - 1, targetLevel, out tierThree),
                    Is.EqualTo(AugmentUpgradeCostStatus.Calculated));

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
            StrongboxOpeningResultLive first = fixture.Service.Open(prepared.Command);
            long moneyAfterFirst = fixture.Money.Balance;
            long moneySequenceAfterFirst = fixture.Money.Sequence;
            long scrapAfterFirst = fixture.Scrap.Balance;
            long scrapSequenceAfterFirst = fixture.Scrap.Sequence;
            long holdingsSequenceAfterFirst = fixture.Holdings.Sequence;
            long openingSequenceAfterFirst = fixture.Service.Sequence;
            long rapSequenceAfterFirst = fixture.Rap.Sequence;

            StrongboxOpeningResultLive replay = fixture.Service.Open(prepared.Command);

            Assert.That(first.Status, Is.EqualTo(StrongboxOpeningLiveStatus.Opened));
            Assert.That(replay.Status, Is.EqualTo(StrongboxOpeningLiveStatus.ExactDuplicateNoChange));
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
                ShopInventoryOpenResult result = fixture.Service.Open(
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
                foreach (ShopStockEntry entry in result.Inventory.Entries)
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
            CraftingRecipe recipe,
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

        private static List<string> CalculateUpgradeCosts(AugmentUpgradeCostPolicy policy)
        {
            List<string> values = new List<string>();
            for (int tier = 1; tier <= 3; tier++)
            {
                for (int targetLevel = 2; targetLevel <= 10; targetLevel++)
                {
                    long cost;
                    AugmentUpgradeCostStatus status = policy.TryCalculateCost(
                        tier,
                        targetLevel - 1,
                        targetLevel,
                        out cost);
                    Assert.That(status, Is.EqualTo(AugmentUpgradeCostStatus.Calculated));
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
                StrongboxOpeningResultLive result = fixture.Service.Open(prepared.Command);
                if (result.Status != StrongboxOpeningLiveStatus.Opened)
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

        private static CraftingRecipe BuildCraftingRecipe()
        {
            return new CraftingRecipe(
                1,
                Id("stat.recipe.gun"),
                Id("stat.craft.gun"),
                Id("stat.discovery.gun"),
                50,
                50,
                5,
                new CraftingDelayVariance(0, 2),
                10L,
                CraftingQualityPolicyKind.Fixed,
                new[] { new CraftingWeightedDefinition(CommonQualityId, 1UL) },
                50,
                60,
                0,
                0,
                1,
                1,
                Array.Empty<CraftingWeightedDefinition>(),
                new CraftingGeneratorPolicy(
                    Id("stat.crafting.generator-policy"),
                    DeterministicRandom.AlgorithmVersion1,
                    new SoftActivationCurveParameters(0.25, 5L, 5L),
                    new ObsolescenceCurveParameters(100L, 50.0, 0.25)));
        }

        private static EquipmentCatalog BuildCraftingCatalog()
        {
            EquipmentQualityTier common = EquipmentQualityTier.Create(CommonQualityId, "Common", 1);
            EquipmentDefinition gun = EquipmentDefinition.Create(
                Id("stat.craft.gun"),
                EquipmentCategoryIds.Gun,
                Id("stat.craft.gun-family"),
                "Stat Craft Gun",
                Id("gun.craft-gun-runtime"),
                InclusiveIntRange.Create(1, 100),
                0,
                new[] { common },
                Array.Empty<StableId>());
            EquipmentCatalogBuildResult build = EquipmentCatalog.Build(
                new[] { gun },
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

        private static ShopDefinition BuildShopDefinition()
        {
            EquipmentGenerationPolicy generation = EquipmentGenerationPolicy.Create(
                Id("stat.shop.generation-policy"),
                new[]
                {
                    ShopCandidate("stat.shop.armor-alpha"),
                    ShopCandidate("stat.shop.armor-beta")
                },
                new[]
                {
                    EquipmentQualityCandidate.Create(CommonQualityId, 0L, 3UL),
                    EquipmentQualityCandidate.Create(RareQualityId, 0L, 1UL)
                },
                Array.Empty<AugmentGenerationCandidate>(),
                0,
                0,
                true,
                new SoftActivationCurveParameters(0.10, 5L, 5L),
                new ObsolescenceCurveParameters(25L, 15.0, 0.20));
            ShopPricingPolicy pricing = ShopPricingPolicy.Create(
                Id("stat.shop.pricing-policy"),
                1L,
                20L,
                3L,
                11L,
                17L,
                5L,
                2L);
            return ShopDefinition.Create(
                Id("stat.shop.definition"),
                4,
                new[] { EquipmentCategoryIds.Armor },
                new[] { Id("stat.shop.tag") },
                Array.Empty<StableId>(),
                generation,
                ShopProgressionContextPolicy.FreezeOnFirstOpen,
                pricing,
                ShopRefreshPolicy.Disabled,
                0,
                0,
                DeterministicRandom.AlgorithmVersion1);
        }

        private static EquipmentGenerationCandidate ShopCandidate(string definitionId)
        {
            return EquipmentGenerationCandidate.Create(
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

        private static void FundScrap(ScrapWalletActions wallet, long amount)
        {
            ScrapTransactionResult result = wallet.Apply(
                new ScrapTransactionCommand(
                    Id("stat.scrap.initial.transaction"),
                    Id("stat.scrap.initial.operation"),
                    wallet.AuthorityStableId,
                    wallet.CurrencyStableId,
                    ScrapMutationKind.Grant,
                    amount,
                    ScrapIdentity.RewardGrantReason,
                    new ScrapProvenance(
                        ScrapIdentity.LootSourceKind,
                        Id("stat.scrap.initial.reward-operation"),
                        Id("stat.player"))));
            Assert.That(result.ChangedState, Is.True);
        }

        private static StableId Id(string value)
        {
            int separatorIndex = value.IndexOf('.');
            if (separatorIndex < 1 || separatorIndex == value.Length - 1)
            {
                return StableId.Create("statistical-verification", value.Replace('.', '-'));
            }

            return StableId.Create(
                value.Substring(0, separatorIndex),
                value.Substring(separatorIndex + 1).Replace('.', '-'));
        }

        private sealed class ShopFixture
        {
            public ShopFixture()
            {
                Catalog = BuildShopCatalog();
                Definition = BuildShopDefinition();
                Money = new MoneyWalletActions();
                ScrapWalletActions scrap = new ScrapWalletActions(
                    ScrapAuthorityId,
                    ScrapCurrencyId);
                CatalogValidator validator = new CatalogValidator(Catalog);
                PlayerHoldingsActions holdings = new PlayerHoldingsActions(
                    HoldingsAuthorityId,
                    10000L,
                    validator);
                RewardApplicationActions rap = new RewardApplicationActions(
                    RapAuthorityId,
                    new MoneyRewardChildState(Money),
                    new ScrapRewardChildState(scrap),
                    new PlayerHoldingsRewardChildState(holdings, validator));
                Service = new ShopLiveActions(
                    new RewardGenerationActions(),
                    Money,
                    rap,
                    ScrapAuthorityId,
                    HoldingsAuthorityId);
            }

            public EquipmentCatalog Catalog { get; }
            public ShopDefinition Definition { get; }
            public MoneyWalletActions Money { get; }
            public ShopLiveActions Service { get; }
        }

        private sealed class CraftingGateFixture
        {
            public CraftingGateFixture(CraftingRecipe recipe)
            {
                EquipmentCatalog catalog = BuildCraftingCatalog();
                CatalogValidator validator = new CatalogValidator(catalog);
                MoneyWalletActions money = new MoneyWalletActions();
                ScrapWalletActions scrap = new ScrapWalletActions(
                    ScrapAuthorityId,
                    ScrapCurrencyId);
                FundScrap(scrap, 100L);
                PlayerHoldingsActions holdings = new PlayerHoldingsActions(
                    HoldingsAuthorityId,
                    100L,
                    validator);
                RewardApplicationActions rap = new RewardApplicationActions(
                    RapAuthorityId,
                    new MoneyRewardChildState(money),
                    new CraftingScrapSpendRewardChildState(scrap),
                    new PlayerHoldingsRewardChildState(holdings, validator));
                Service = new CraftingActions(
                    new CraftingRecipeCatalog(new[] { recipe }),
                    catalog,
                    new RewardGenerationActions(),
                    rap,
                    scrap,
                    MoneyWalletIds.AuthorityStableId,
                    HoldingsAuthorityId);
                Recipe = recipe;
            }

            public CraftingRecipe Recipe { get; }
            public CraftingActions Service { get; }

            public CraftingResult Craft(string transactionId, ulong rootSeed, int characterLevel)
            {
                return Service.Craft(
                    new CraftEquipmentCommand(
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
                RewardGrantSpecification moneyGrant = RewardGrantSpecification.Create(
                    Id("stat.economy-box.money-grant"),
                    RewardGrantKind.Money,
                    MoneyWalletIds.CurrencyStableId,
                    RewardQuantityRange.Create(5L, 15L),
                    Array.Empty<RewardScalingInputDescriptor>());
                RewardProfile profile = RewardProfile.Create(
                    Id("stat.economy-box.profile"),
                    new[] { moneyGrant },
                    Array.Empty<IndependentRewardRoll>(),
                    Array.Empty<ExclusiveRewardGroup>());
                Definition = StrongboxDefinition.Create(
                    TierId,
                    0,
                    1L,
                    1L,
                    0L,
                    StrongboxRewardCountPolicy.Create(2, 2),
                    StrongboxMandatoryScrapPolicy.Create(ScrapCurrencyId, 2L, 8L),
                    Id("stat.economy-box.generation-policy"),
                    profile,
                    Id("stat.scaling.source-tier"),
                    Id("stat.scaling.exceptional"));
                Money = new MoneyWalletActions();
                Scrap = new ScrapWalletActions(ScrapAuthorityId, ScrapCurrencyId);
                AcceptingEquipmentValidator validator = new AcceptingEquipmentValidator();
                Holdings = new PlayerHoldingsActions(HoldingsAuthorityId, 10000L, validator);
                Rap = new RewardApplicationActions(
                    RapAuthorityId,
                    new MoneyRewardChildState(Money),
                    new ScrapRewardChildState(Scrap),
                    new PlayerHoldingsRewardChildState(Holdings, validator));
                Service = new StrongboxOpeningActions(
                    new StrongboxDefinitionCatalog(new[] { Definition }),
                    new SharedStrongboxRewardGenerator(new RewardGenerationActions()),
                    Holdings,
                    Rap,
                    new DeterministicStrongboxGrantPayloadResolver());
            }

            public StrongboxDefinition Definition { get; }
            public MoneyWalletActions Money { get; }
            public ScrapWalletActions Scrap { get; }
            public PlayerHoldingsActions Holdings { get; }
            public RewardApplicationActions Rap { get; }
            public StrongboxOpeningActions Service { get; }

            public PreparedStrongboxOpen Prepare(int index, ulong seed)
            {
                string suffix = index.ToString("D4", CultureInfo.InvariantCulture);
                StableId boxId = Id("stat.economy-box.instance." + suffix);
                PlayerHoldingsMutationResult added = Holdings.Apply(
                    PlayerHoldingsCommand.AddStrongbox(
                        Id("stat.economy-box.add-tx." + suffix),
                        Id("stat.economy-box.add-op." + suffix),
                        HoldingsAuthorityId,
                        Definition.TierStableId,
                        boxId,
                        HoldingProvenance.Create(
                            Id("stat.economy-box.add-grant." + suffix),
                            Id("stat.economy-box.add-source." + suffix))));
                Assert.That(added.Status, Is.EqualTo(PlayerHoldingsMutationStatus.Applied));
                StrongboxInstanceContext context = StrongboxInstanceContext.Create(
                    boxId,
                    Definition.TierStableId,
                    seed,
                    DeterministicRandom.AlgorithmVersion1,
                    Context(20),
                    Id("stat.economy-box.source." + suffix),
                    Id("stat.economy-box.provenance." + suffix),
                    Definition.Fingerprint);
                StrongboxRegistrationResult registered = Service.RegisterInstance(context);
                Assert.That(registered.Status, Is.EqualTo(StrongboxRegistrationStatus.Registered));
                StrongboxOpenCommand command = StrongboxOpenCommand.Create(
                    Id("stat.economy-box.opening." + suffix),
                    Id("stat.economy-box.run"),
                    boxId,
                    PlayerId,
                    MoneyWalletIds.AuthorityStableId,
                    ScrapAuthorityId,
                    HoldingsAuthorityId);
                return new PreparedStrongboxOpen(command);
            }
        }

        private sealed class PreparedStrongboxOpen
        {
            public PreparedStrongboxOpen(StrongboxOpenCommand command)
            {
                Command = command;
            }

            public StrongboxOpenCommand Command { get; }
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
