using System;
using System.Collections.Generic;
using NUnit.Framework;
using ShooterMover.Application.Crafting;
using ShooterMover.Application.Economy.Scrap;
using ShooterMover.Application.Holdings;
using ShooterMover.Application.Rewards.Application;
using ShooterMover.Application.Rewards.Generation;
using ShooterMover.Contracts.Equipment;
using ShooterMover.Contracts.Rewards.Application;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Crafting;
using ShooterMover.Domain.Economy.Scrap;
using ShooterMover.Domain.Equipment;
using ShooterMover.Domain.Holdings;
using ShooterMover.Domain.Progression.Context;
using ShooterMover.Domain.Progression.Curves;
using ShooterMover.Domain.Rewards.Application;

namespace ShooterMover.Tests.EditMode.Crafting
{
    public sealed class CraftingServiceV1Tests
    {
        private static readonly StableId CommonQuality = Id("quality.common");
        private static readonly StableId RareQuality = Id("quality.rare");
        private static readonly StableId EquipmentAlpha = Id("equipment.alpha");
        private static readonly StableId EquipmentBeta = Id("equipment.beta");
        private static readonly StableId AugmentAlpha = Id("augment.alpha");

        [Test]
        public void RecipeUnlockDerivesFromNaturalLevelPlusPositiveDelayAndVariance()
        {
            CraftingRecipeV1 recipe = CreateRecipe(
                delay: 5,
                minimumVariance: 0,
                maximumVariance: 2);

            int unlock = recipe.ResolveUnlockLevel(991UL);

            Assert.That(recipe.MinimumUnlockLevel, Is.EqualTo(55));
            Assert.That(recipe.MaximumUnlockLevel, Is.EqualTo(57));
            Assert.That(unlock, Is.InRange(55, 57));
        }

        [TestCase(0)]
        [TestCase(-1)]
        public void ZeroOrNegativeDelayIsRejected(int delay)
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => CreateRecipe(delay: delay));
        }

        [Test]
        public void RecipeCannotUnlockAtOrBeforeOrdinaryDiscoveryActivation()
        {
            Assert.Throws<ArgumentException>(
                () => CreateRecipe(
                    naturalLevel: 50,
                    ordinaryActivationLevel: 55,
                    delay: 5));
        }

        [Test]
        public void EligibleRecipeCraftsSuccessfullyThroughRealAuthorities()
        {
            Fixture fixture = new Fixture();

            CraftingResultV1 result = fixture.Service.Craft(fixture.Command());

            Assert.That(result.Status, Is.EqualTo(CraftingResultStatusV1.Crafted));
            Assert.That(fixture.Scrap.Balance, Is.EqualTo(90L));
            UniqueHoldingSnapshotV1 holding;
            Assert.That(
                fixture.Holdings.TryGetUnique(
                    result.EquipmentInstanceStableId,
                    out holding),
                Is.True);
            Assert.That(
                holding.EquipmentInstance.Fingerprint,
                Is.EqualTo(result.EquipmentFingerprint));
        }

        [Test]
        public void ScrapIsSpentExactlyOnce()
        {
            Fixture fixture = new Fixture();
            CraftEquipmentCommandV1 command = fixture.Command();

            fixture.Service.Craft(command);
            fixture.Service.Craft(command);

            Assert.That(fixture.Scrap.Balance, Is.EqualTo(90L));
            Assert.That(fixture.Scrap.Sequence, Is.EqualTo(2L));
        }

        [Test]
        public void OneEquipmentInstanceIsGrantedExactlyOnce()
        {
            Fixture fixture = new Fixture();
            CraftEquipmentCommandV1 command = fixture.Command();

            CraftingResultV1 first = fixture.Service.Craft(command);
            CraftingResultV1 replay = fixture.Service.Craft(command);

            Assert.That(
                replay.Status,
                Is.EqualTo(CraftingResultStatusV1.ExactDuplicateNoChange));
            Assert.That(fixture.Holdings.Sequence, Is.EqualTo(1L));
            Assert.That(
                replay.EquipmentInstanceStableId,
                Is.EqualTo(first.EquipmentInstanceStableId));
        }

        [Test]
        public void InsufficientScrapLeavesEverythingUnchanged()
        {
            Fixture fixture = new Fixture(initialScrap: 9L);
            long scrapSequence = fixture.Scrap.Sequence;

            CraftingResultV1 result = fixture.Service.Craft(fixture.Command());

            Assert.That(
                result.Status,
                Is.EqualTo(CraftingResultStatusV1.InsufficientScrap));
            Assert.That(fixture.Scrap.Balance, Is.EqualTo(9L));
            Assert.That(fixture.Scrap.Sequence, Is.EqualTo(scrapSequence));
            Assert.That(fixture.Holdings.Sequence, Is.Zero);
            Assert.That(fixture.Rap.Sequence, Is.Zero);
        }

        [Test]
        public void ExactDuplicateCraftIsNoChangeReplayAfterSpendingEntireBalance()
        {
            Fixture fixture = new Fixture(initialScrap: 10L);
            CraftEquipmentCommandV1 command = fixture.Command();

            CraftingResultV1 first = fixture.Service.Craft(command);
            long rapSequence = fixture.Rap.Sequence;
            CraftingResultV1 second = fixture.Service.Craft(command);

            Assert.That(
                second.Status,
                Is.EqualTo(CraftingResultStatusV1.ExactDuplicateNoChange));
            Assert.That(second.EquipmentFingerprint, Is.EqualTo(first.EquipmentFingerprint));
            Assert.That(fixture.Scrap.Balance, Is.Zero);
            Assert.That(fixture.Rap.Sequence, Is.EqualTo(rapSequence));
        }

        [Test]
        public void ConflictingDuplicateIdentityIsRejected()
        {
            Fixture fixture = new Fixture();
            CraftEquipmentCommandV1 first = fixture.Command(rootSeed: 10UL);
            CraftEquipmentCommandV1 conflict = fixture.Command(rootSeed: 11UL);
            fixture.Service.Craft(first);
            long scrap = fixture.Scrap.Balance;
            long holdingsSequence = fixture.Holdings.Sequence;

            CraftingResultV1 result = fixture.Service.Craft(conflict);

            Assert.That(
                result.Status,
                Is.EqualTo(CraftingResultStatusV1.ConflictingDuplicate));
            Assert.That(fixture.Scrap.Balance, Is.EqualTo(scrap));
            Assert.That(fixture.Holdings.Sequence, Is.EqualTo(holdingsSequence));
        }

        [Test]
        public void UnknownRecipeIsRejectedWithoutMutation()
        {
            Fixture fixture = new Fixture();
            CraftEquipmentCommandV1 command = new CraftEquipmentCommandV1(
                Id("craft.unknown-recipe"),
                Id("recipe.unknown"),
                Id("run.test"),
                Id("player.test"),
                Context(99),
                1UL);

            CraftingResultV1 result = fixture.Service.Craft(command);

            Assert.That(
                result.Status,
                Is.EqualTo(CraftingResultStatusV1.UnknownRecipe));
            Assert.That(fixture.Rap.Sequence, Is.Zero);
        }

        [Test]
        public void UnknownTargetEquipmentIsRejectedWithoutMutation()
        {
            CraftingRecipeV1 bad = CreateRecipe(
                target: Id("equipment.missing"));
            Fixture fixture = new Fixture(recipes: new[] { bad });

            CraftingResultV1 result = fixture.Service.Craft(
                fixture.Command(recipeId: bad.RecipeStableId));

            Assert.That(
                result.Status,
                Is.EqualTo(CraftingResultStatusV1.UnknownTargetEquipment));
            Assert.That(fixture.Rap.Sequence, Is.Zero);
        }

        [Test]
        public void ProgressionBelowCraftingAvailabilityIsRejected()
        {
            Fixture fixture = new Fixture();

            CraftingResultV1 result = fixture.Service.Craft(
                fixture.Command(characterLevel: 54));

            Assert.That(
                result.Status,
                Is.EqualTo(CraftingResultStatusV1.ProgressionUnavailable));
            Assert.That(fixture.Scrap.Balance, Is.EqualTo(100L));
            Assert.That(fixture.Rap.Sequence, Is.Zero);
        }

        [Test]
        public void FixedQualityCraftingObeysGuarantee()
        {
            Fixture fixture = new Fixture(
                recipe: CreateRecipe(
                    qualityPolicy: CraftingQualityPolicyKindV1.Fixed,
                    qualities: new[] { Weighted(CommonQuality, 999UL) }));

            CraftingResultV1 result = fixture.Service.Craft(fixture.Command());

            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.Equipment.QualityId, Is.EqualTo(CommonQuality));
        }

        [Test]
        public void RandomQualityCraftingIsDeterministic()
        {
            CraftingRecipeV1 recipe = CreateRecipe(
                qualityPolicy:
                    CraftingQualityPolicyKindV1.DeterministicWeightedRandom,
                qualities: new[]
                {
                    Weighted(CommonQuality, 1UL),
                    Weighted(RareQuality, 3UL),
                });
            Fixture first = new Fixture(recipe: recipe);
            Fixture second = new Fixture(recipe: recipe);

            CraftingResultV1 left = first.Service.Craft(
                first.Command(rootSeed: 7788UL));
            CraftingResultV1 right = second.Service.Craft(
                second.Command(rootSeed: 7788UL));

            Assert.That(left.Equipment.QualityId, Is.EqualTo(right.Equipment.QualityId));
            Assert.That(left.EquipmentFingerprint, Is.EqualTo(right.EquipmentFingerprint));
        }

        [Test]
        public void SlotTierAndLevelCapsAreEnforced()
        {
            CraftingRecipeV1 recipe = CreateRecipe(
                minimumSlots: 1,
                maximumSlots: 1,
                maximumTier: 1,
                maximumAugmentLevel: 2,
                augments: new[] { Weighted(AugmentAlpha, 1UL) });
            Fixture fixture = new Fixture(recipe: recipe);

            CraftingResultV1 result = fixture.Service.Craft(
                fixture.Command(rootSeed: 812UL));

            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.Equipment.Augments.Count, Is.EqualTo(1));
            Assert.That(result.Equipment.Augments[0].Tier, Is.LessThanOrEqualTo(1));
            Assert.That(result.Equipment.Augments[0].Level, Is.LessThanOrEqualTo(2));
        }

        [Test]
        public void RetryUsesSameGeneratedInstanceIdentityAndFingerprint()
        {
            Fixture fixture = new Fixture(failHoldingsApplyOnce: true);
            CraftEquipmentCommandV1 command = fixture.Command(rootSeed: 123456UL);

            CraftingResultV1 first = fixture.Service.Craft(command);
            CraftingResultV1 retry = fixture.Service.Craft(command);

            Assert.That(
                first.Status,
                Is.EqualTo(CraftingResultStatusV1.RewardApplicationRetryRequired));
            Assert.That(retry.Succeeded, Is.True);
            Assert.That(
                retry.EquipmentInstanceStableId,
                Is.EqualTo(first.EquipmentInstanceStableId));
            Assert.That(
                retry.EquipmentFingerprint,
                Is.EqualTo(first.EquipmentFingerprint));
        }

        [Test]
        public void RapFailureRemainsRetrySafeAndEventuallyAppliesExactlyOnce()
        {
            Fixture fixture = new Fixture(failHoldingsApplyOnce: true);
            CraftEquipmentCommandV1 command = fixture.Command();

            fixture.Service.Craft(command);
            CraftingResultV1 retry = fixture.Service.Craft(command);

            Assert.That(retry.Succeeded, Is.True);
            Assert.That(fixture.Scrap.Balance, Is.EqualTo(90L));
            Assert.That(fixture.Holdings.Sequence, Is.EqualTo(1L));
            UniqueHoldingSnapshotV1 holding;
            Assert.That(
                fixture.Holdings.TryGetUnique(
                    retry.EquipmentInstanceStableId,
                    out holding),
                Is.True);
        }

        [Test]
        public void RecipeSnapshotsAndFingerprintsAreCanonical()
        {
            CraftingRecipeV1 left = CreateRecipe(
                qualityPolicy:
                    CraftingQualityPolicyKindV1.DeterministicWeightedRandom,
                qualities: new[]
                {
                    Weighted(RareQuality, 3UL),
                    Weighted(CommonQuality, 1UL),
                });
            CraftingRecipeV1 right = CreateRecipe(
                qualityPolicy:
                    CraftingQualityPolicyKindV1.DeterministicWeightedRandom,
                qualities: new[]
                {
                    Weighted(CommonQuality, 1UL),
                    Weighted(RareQuality, 3UL),
                });

            Assert.That(left.ToCanonicalString(), Is.EqualTo(right.ToCanonicalString()));
            Assert.That(left.Fingerprint, Is.EqualTo(right.Fingerprint));
            Assert.That(
                new CraftingRecipeCatalogV1(new[] { left }).Fingerprint,
                Is.EqualTo(
                    new CraftingRecipeCatalogV1(new[] { right }).Fingerprint));
        }

        [Test]
        public void MultipleRecipesTargetDifferentEquipmentWithoutCodeChanges()
        {
            CraftingRecipeV1 alpha = CreateRecipe(
                recipeId: Id("recipe.alpha"),
                target: EquipmentAlpha);
            CraftingRecipeV1 beta = CreateRecipe(
                recipeId: Id("recipe.beta"),
                target: EquipmentBeta);
            Fixture fixture = new Fixture(recipes: new[] { alpha, beta });

            CraftingResultV1 first = fixture.Service.Craft(
                fixture.Command(
                    craftId: Id("craft.alpha"),
                    recipeId: alpha.RecipeStableId));
            CraftingResultV1 second = fixture.Service.Craft(
                fixture.Command(
                    craftId: Id("craft.beta"),
                    recipeId: beta.RecipeStableId));

            Assert.That(first.Equipment.DefinitionId, Is.EqualTo(EquipmentAlpha));
            Assert.That(second.Equipment.DefinitionId, Is.EqualTo(EquipmentBeta));
        }

        [Test]
        public void RealIntegrationExercisesScrapHoldingsAndRap()
        {
            Fixture fixture = new Fixture();
            long scrapSequence = fixture.Scrap.Sequence;
            long rapSequence = fixture.Rap.Sequence;

            CraftingResultV1 result = fixture.Service.Craft(fixture.Command());

            Assert.That(result.Status, Is.EqualTo(CraftingResultStatusV1.Crafted));
            Assert.That(fixture.Scrap.Sequence, Is.EqualTo(scrapSequence + 1L));
            Assert.That(fixture.Holdings.Sequence, Is.EqualTo(1L));
            Assert.That(fixture.Rap.Sequence, Is.GreaterThan(rapSequence));
            Assert.That(
                result.RewardApplicationResult.Status,
                Is.EqualTo(RewardApplicationResultStatusV1.Applied));
        }

        private sealed class Fixture
        {
            public Fixture(
                long initialScrap = 100L,
                CraftingRecipeV1 recipe = null,
                IEnumerable<CraftingRecipeV1> recipes = null,
                bool failHoldingsApplyOnce = false)
            {
                Catalog = BuildEquipmentCatalog();
                Validator = new CatalogValidator(Catalog);
                Scrap = new ScrapWalletServiceV1(
                    Id("authority.scrap"),
                    Id("currency.scrap"));
                Fund(Scrap, initialScrap);
                Holdings = new PlayerHoldingsService(
                    Id("holdings.player"),
                    1000L,
                    Validator);

                IRewardChildAuthorityV1 holdingsAdapter =
                    new PlayerHoldingsRewardChildAuthorityV1(
                        Holdings,
                        Validator);
                if (failHoldingsApplyOnce)
                {
                    holdingsAdapter = new FailOnceApplyAuthority(
                        holdingsAdapter);
                }

                Rap = new RewardApplicationServiceV1(
                    Id("authority.crafting-rap"),
                    new CraftingUnusedMoneyRewardChildAuthorityV1(),
                    new CraftingScrapSpendRewardChildAuthorityV1(Scrap),
                    holdingsAdapter);
                CraftingRecipeV1 selected = recipe
                    ?? CraftingServiceV1Tests.CreateRecipe();
                var catalogRecipes = recipes == null
                    ? new[] { selected }
                    : new List<CraftingRecipeV1>(recipes).ToArray();
                PrimaryRecipe = catalogRecipes[0];
                Service = new CraftingServiceV1(
                    new CraftingRecipeCatalogV1(catalogRecipes),
                    Catalog,
                    new RewardGenerationServiceV1(),
                    Rap,
                    Scrap,
                    CraftingUnusedMoneyRewardChildAuthorityV1.StableAuthorityId,
                    Holdings.AuthorityStableId);
            }

            public EquipmentCatalog Catalog { get; }
            public CatalogValidator Validator { get; }
            public ScrapWalletServiceV1 Scrap { get; }
            public PlayerHoldingsService Holdings { get; }
            public RewardApplicationServiceV1 Rap { get; }
            public CraftingRecipeV1 PrimaryRecipe { get; }
            public CraftingServiceV1 Service { get; }

            public CraftEquipmentCommandV1 Command(
                int characterLevel = 60,
                ulong rootSeed = 44UL,
                StableId craftId = null,
                StableId recipeId = null)
            {
                return new CraftEquipmentCommandV1(
                    craftId ?? Id("craft.transaction"),
                    recipeId ?? PrimaryRecipe.RecipeStableId,
                    Id("run.test"),
                    Id("player.test"),
                    Context(characterLevel),
                    rootSeed);
            }
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
                EquipmentInstance instance = request == null
                    ? null
                    : request.Instance;
                return EquipmentInstanceValidationResponse.From(
                    catalog,
                    instance,
                    catalog.ValidateInstance(instance));
            }
        }

        private sealed class FailOnceApplyAuthority : IRewardChildAuthorityV1
        {
            private readonly IRewardChildAuthorityV1 inner;
            private bool failed;

            public FailOnceApplyAuthority(IRewardChildAuthorityV1 inner)
            {
                this.inner = inner;
            }

            public StableId AuthorityStableId
            {
                get { return inner.AuthorityStableId; }
            }

            public long Sequence { get { return inner.Sequence; } }

            public RewardAuthorityPreflightResultV1 Preflight(
                IReadOnlyList<RewardChildGrantCommandV1> commands)
            {
                return inner.Preflight(commands);
            }

            public RewardChildApplyResultV1 Apply(
                RewardChildGrantCommandV1 command)
            {
                if (!failed)
                {
                    failed = true;
                    return new RewardChildApplyResultV1(
                        command.TransactionStableId,
                        RewardChildApplyStatusV1.Rejected,
                        false,
                        "test-interruption");
                }
                return inner.Apply(command);
            }
        }

        private static CraftingRecipeV1 CreateRecipe(
            StableId recipeId = null,
            StableId target = null,
            int naturalLevel = 50,
            int ordinaryActivationLevel = 50,
            int delay = 5,
            int minimumVariance = 0,
            int maximumVariance = 2,
            CraftingQualityPolicyKindV1 qualityPolicy =
                CraftingQualityPolicyKindV1.Fixed,
            IEnumerable<CraftingWeightedDefinitionV1> qualities = null,
            int minimumSlots = 0,
            int maximumSlots = 0,
            int maximumTier = 1,
            int maximumAugmentLevel = 1,
            IEnumerable<CraftingWeightedDefinitionV1> augments = null)
        {
            return new CraftingRecipeV1(
                1,
                recipeId ?? Id("recipe.alpha"),
                target ?? EquipmentAlpha,
                Id("progression-source.equipment"),
                naturalLevel,
                ordinaryActivationLevel,
                delay,
                new CraftingDelayVarianceV1(
                    minimumVariance,
                    maximumVariance),
                10L,
                qualityPolicy,
                qualities ?? new[] { Weighted(CommonQuality, 1UL) },
                50,
                60,
                minimumSlots,
                maximumSlots,
                maximumTier,
                maximumAugmentLevel,
                augments ?? Array.Empty<CraftingWeightedDefinitionV1>(),
                new CraftingGeneratorPolicyV1(
                    Id("generator-policy.crafting"),
                    1,
                    new SoftActivationCurveParameters(0.25, 2L, 2L),
                    new ObsolescenceCurveParameters(
                        1000L,
                        1000.0,
                        1.0)));
        }

        private static EquipmentCatalog BuildEquipmentCatalog()
        {
            EquipmentQualityTier[] qualities =
            {
                EquipmentQualityTier.Create(CommonQuality, "Common", 1),
                EquipmentQualityTier.Create(RareQuality, "Rare", 2),
            };
            EquipmentDefinition alpha = EquipmentDefinition.Create(
                EquipmentAlpha,
                EquipmentCategoryIds.Weapon,
                Id("equipment-family.alpha"),
                "Alpha",
                Id("weapon.alpha"),
                InclusiveIntRange.Create(1, 100),
                2,
                qualities,
                Array.Empty<StableId>());
            EquipmentDefinition beta = EquipmentDefinition.Create(
                EquipmentBeta,
                EquipmentCategoryIds.Weapon,
                Id("equipment-family.beta"),
                "Beta",
                Id("weapon.beta"),
                InclusiveIntRange.Create(1, 100),
                2,
                qualities,
                Array.Empty<StableId>());
            AugmentDefinition augment = AugmentDefinition.Create(
                AugmentAlpha,
                Id("augment-family.alpha"),
                "Augment Alpha",
                AugmentCompatibility.Create(
                    Array.Empty<StableId>(),
                    Array.Empty<StableId>(),
                    Array.Empty<StableId>(),
                    Array.Empty<StableId>()),
                Array.Empty<StableId>(),
                AugmentDuplicatePolicy.DisallowSameDefinition,
                InclusiveIntRange.Create(1, 3),
                InclusiveIntRange.Create(1, 10));
            EquipmentCatalogBuildResult build = EquipmentCatalog.Build(
                new[] { alpha, beta },
                new[] { augment });
            Assert.That(build.IsValid, Is.True);
            return build.Catalog;
        }

        private static void Fund(
            ScrapWalletServiceV1 wallet,
            long amount)
        {
            if (amount == 0L)
            {
                return;
            }

            ScrapTransactionResultV1 result = wallet.Apply(
                new ScrapTransactionCommandV1(
                    Id("scrap-tx.initial"),
                    Id("scrap-op.initial"),
                    wallet.AuthorityStableId,
                    wallet.CurrencyStableId,
                    ScrapMutationKindV1.Grant,
                    amount,
                    ScrapIdentityV1.RewardGrantReason,
                    new ScrapProvenanceV1(
                        ScrapIdentityV1.RewardSourceKind,
                        Id("reward-op.initial"),
                        Id("player.test"))));
            Assert.That(result.ChangedState, Is.True);
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

        private static CraftingWeightedDefinitionV1 Weighted(
            StableId id,
            ulong weight)
        {
            return new CraftingWeightedDefinitionV1(id, weight);
        }

        private static StableId Id(string value)
        {
            return StableId.Parse(value);
        }
    }
}
