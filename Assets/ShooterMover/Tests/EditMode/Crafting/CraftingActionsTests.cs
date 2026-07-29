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
    public sealed class CraftingActionsTests
    {
        private static readonly StableId CommonQuality = Id("quality.common");
        private static readonly StableId RareQuality = Id("quality.rare");
        private static readonly StableId EquipmentAlpha = Id("equipment.alpha");
        private static readonly StableId EquipmentBeta = Id("equipment.beta");
        private static readonly StableId AugmentAlpha = Id("augment.alpha");

        [Test]
        public void RecipeUnlockDerivesFromNaturalLevelPlusPositiveDelayAndVariance()
        {
            CraftingRecipe recipe = CreateRecipe(
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

            CraftingResult result = fixture.Service.Craft(fixture.Command());

            Assert.That(result.Status, Is.EqualTo(CraftingResultStatus.Crafted));
            Assert.That(fixture.Scrap.Balance, Is.EqualTo(90L));
            UniqueHoldingSnapshot holding;
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
            CraftEquipmentCommand command = fixture.Command();

            fixture.Service.Craft(command);
            fixture.Service.Craft(command);

            Assert.That(fixture.Scrap.Balance, Is.EqualTo(90L));
            Assert.That(fixture.Scrap.Sequence, Is.EqualTo(2L));
        }

        [Test]
        public void OneEquipmentInstanceIsGrantedExactlyOnce()
        {
            Fixture fixture = new Fixture();
            CraftEquipmentCommand command = fixture.Command();

            CraftingResult first = fixture.Service.Craft(command);
            CraftingResult replay = fixture.Service.Craft(command);

            Assert.That(
                replay.Status,
                Is.EqualTo(CraftingResultStatus.ExactDuplicateNoChange));
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

            CraftingResult result = fixture.Service.Craft(fixture.Command());

            Assert.That(
                result.Status,
                Is.EqualTo(CraftingResultStatus.InsufficientScrap));
            Assert.That(fixture.Scrap.Balance, Is.EqualTo(9L));
            Assert.That(fixture.Scrap.Sequence, Is.EqualTo(scrapSequence));
            Assert.That(fixture.Holdings.Sequence, Is.Zero);
            Assert.That(fixture.Rap.Sequence, Is.Zero);
        }

        [Test]
        public void ExactDuplicateCraftIsNoChangeReplayAfterSpendingEntireBalance()
        {
            Fixture fixture = new Fixture(initialScrap: 10L);
            CraftEquipmentCommand command = fixture.Command();

            CraftingResult first = fixture.Service.Craft(command);
            long rapSequence = fixture.Rap.Sequence;
            CraftingResult second = fixture.Service.Craft(command);

            Assert.That(
                second.Status,
                Is.EqualTo(CraftingResultStatus.ExactDuplicateNoChange));
            Assert.That(second.EquipmentFingerprint, Is.EqualTo(first.EquipmentFingerprint));
            Assert.That(fixture.Scrap.Balance, Is.Zero);
            Assert.That(fixture.Rap.Sequence, Is.EqualTo(rapSequence));
        }

        [Test]
        public void ConflictingDuplicateIdentityIsRejected()
        {
            Fixture fixture = new Fixture();
            CraftEquipmentCommand first = fixture.Command(rootSeed: 10UL);
            CraftEquipmentCommand conflict = fixture.Command(rootSeed: 11UL);
            fixture.Service.Craft(first);
            long scrap = fixture.Scrap.Balance;
            long holdingsSequence = fixture.Holdings.Sequence;

            CraftingResult result = fixture.Service.Craft(conflict);

            Assert.That(
                result.Status,
                Is.EqualTo(CraftingResultStatus.ConflictingDuplicate));
            Assert.That(fixture.Scrap.Balance, Is.EqualTo(scrap));
            Assert.That(fixture.Holdings.Sequence, Is.EqualTo(holdingsSequence));
        }

        [Test]
        public void UnknownRecipeIsRejectedWithoutMutation()
        {
            Fixture fixture = new Fixture();
            CraftEquipmentCommand command = new CraftEquipmentCommand(
                Id("craft.unknown-recipe"),
                Id("recipe.unknown"),
                Id("run.test"),
                Id("player.test"),
                Context(99),
                1UL);

            CraftingResult result = fixture.Service.Craft(command);

            Assert.That(
                result.Status,
                Is.EqualTo(CraftingResultStatus.UnknownRecipe));
            Assert.That(fixture.Rap.Sequence, Is.Zero);
        }

        [Test]
        public void UnknownTargetEquipmentIsRejectedWithoutMutation()
        {
            CraftingRecipe bad = CreateRecipe(
                target: Id("equipment.missing"));
            Fixture fixture = new Fixture(recipes: new[] { bad });

            CraftingResult result = fixture.Service.Craft(
                fixture.Command(recipeId: bad.RecipeStableId));

            Assert.That(
                result.Status,
                Is.EqualTo(CraftingResultStatus.UnknownTargetEquipment));
            Assert.That(fixture.Rap.Sequence, Is.Zero);
        }

        [Test]
        public void ProgressionBelowCraftingAvailabilityIsRejected()
        {
            Fixture fixture = new Fixture();

            CraftingResult result = fixture.Service.Craft(
                fixture.Command(characterLevel: 54));

            Assert.That(
                result.Status,
                Is.EqualTo(CraftingResultStatus.ProgressionUnavailable));
            Assert.That(fixture.Scrap.Balance, Is.EqualTo(100L));
            Assert.That(fixture.Rap.Sequence, Is.Zero);
        }

        [Test]
        public void FixedQualityCraftingObeysGuarantee()
        {
            Fixture fixture = new Fixture(
                recipe: CreateRecipe(
                    qualityPolicy: CraftingQualityPolicyKind.Fixed,
                    qualities: new[] { Weighted(CommonQuality, 999UL) }));

            CraftingResult result = fixture.Service.Craft(fixture.Command());

            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.Equipment.QualityId, Is.EqualTo(CommonQuality));
        }

        [Test]
        public void RandomQualityCraftingIsDeterministic()
        {
            CraftingRecipe recipe = CreateRecipe(
                qualityPolicy:
                    CraftingQualityPolicyKind.DeterministicWeightedRandom,
                qualities: new[]
                {
                    Weighted(CommonQuality, 1UL),
                    Weighted(RareQuality, 3UL),
                });
            Fixture first = new Fixture(recipe: recipe);
            Fixture second = new Fixture(recipe: recipe);

            CraftingResult left = first.Service.Craft(
                first.Command(rootSeed: 7788UL));
            CraftingResult right = second.Service.Craft(
                second.Command(rootSeed: 7788UL));

            Assert.That(left.Equipment.QualityId, Is.EqualTo(right.Equipment.QualityId));
            Assert.That(left.EquipmentFingerprint, Is.EqualTo(right.EquipmentFingerprint));
        }

        [Test]
        public void SlotTierAndLevelCapsAreEnforced()
        {
            CraftingRecipe recipe = CreateRecipe(
                minimumSlots: 1,
                maximumSlots: 1,
                maximumTier: 1,
                maximumAugmentLevel: 2,
                augments: new[] { Weighted(AugmentAlpha, 1UL) });
            Fixture fixture = new Fixture(recipe: recipe);

            CraftingResult result = fixture.Service.Craft(
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
            CraftEquipmentCommand command = fixture.Command(rootSeed: 123456UL);

            CraftingResult first = fixture.Service.Craft(command);
            CraftingResult retry = fixture.Service.Craft(command);

            Assert.That(
                first.Status,
                Is.EqualTo(CraftingResultStatus.RewardApplicationRetryRequired));
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
            CraftEquipmentCommand command = fixture.Command();

            fixture.Service.Craft(command);
            CraftingResult retry = fixture.Service.Craft(command);

            Assert.That(retry.Succeeded, Is.True);
            Assert.That(fixture.Scrap.Balance, Is.EqualTo(90L));
            Assert.That(fixture.Holdings.Sequence, Is.EqualTo(1L));
            UniqueHoldingSnapshot holding;
            Assert.That(
                fixture.Holdings.TryGetUnique(
                    retry.EquipmentInstanceStableId,
                    out holding),
                Is.True);
        }

        [Test]
        public void RecipeSnapshotsAndFingerprintsAreCanonical()
        {
            CraftingRecipe left = CreateRecipe(
                qualityPolicy:
                    CraftingQualityPolicyKind.DeterministicWeightedRandom,
                qualities: new[]
                {
                    Weighted(RareQuality, 3UL),
                    Weighted(CommonQuality, 1UL),
                });
            CraftingRecipe right = CreateRecipe(
                qualityPolicy:
                    CraftingQualityPolicyKind.DeterministicWeightedRandom,
                qualities: new[]
                {
                    Weighted(CommonQuality, 1UL),
                    Weighted(RareQuality, 3UL),
                });

            Assert.That(left.ToCanonicalString(), Is.EqualTo(right.ToCanonicalString()));
            Assert.That(left.Fingerprint, Is.EqualTo(right.Fingerprint));
            Assert.That(
                new CraftingRecipeCatalog(new[] { left }).Fingerprint,
                Is.EqualTo(
                    new CraftingRecipeCatalog(new[] { right }).Fingerprint));
        }

        [Test]
        public void MultipleRecipesTargetDifferentEquipmentWithoutCodeChanges()
        {
            CraftingRecipe alpha = CreateRecipe(
                recipeId: Id("recipe.alpha"),
                target: EquipmentAlpha);
            CraftingRecipe beta = CreateRecipe(
                recipeId: Id("recipe.beta"),
                target: EquipmentBeta);
            Fixture fixture = new Fixture(recipes: new[] { alpha, beta });

            CraftingResult first = fixture.Service.Craft(
                fixture.Command(
                    craftId: Id("craft.alpha"),
                    recipeId: alpha.RecipeStableId));
            CraftingResult second = fixture.Service.Craft(
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

            CraftingResult result = fixture.Service.Craft(fixture.Command());

            Assert.That(result.Status, Is.EqualTo(CraftingResultStatus.Crafted));
            Assert.That(fixture.Scrap.Sequence, Is.EqualTo(scrapSequence + 1L));
            Assert.That(fixture.Holdings.Sequence, Is.EqualTo(1L));
            Assert.That(fixture.Rap.Sequence, Is.GreaterThan(rapSequence));
            Assert.That(
                result.RewardApplicationResult.Status,
                Is.EqualTo(RewardApplicationResultStatus.Applied));
        }

        private sealed class Fixture
        {
            public Fixture(
                long initialScrap = 100L,
                CraftingRecipe recipe = null,
                IEnumerable<CraftingRecipe> recipes = null,
                bool failHoldingsApplyOnce = false)
            {
                Catalog = BuildEquipmentCatalog();
                Validator = new CatalogValidator(Catalog);
                Scrap = new ScrapWalletActions(
                    Id("authority.scrap"),
                    Id("currency.scrap"));
                Fund(Scrap, initialScrap);
                Holdings = new PlayerHoldingsActions(
                    Id("holdings.player"),
                    1000L,
                    Validator);

                IRewardChildState holdingsAdapter =
                    new PlayerHoldingsRewardChildState(
                        Holdings,
                        Validator);
                if (failHoldingsApplyOnce)
                {
                    holdingsAdapter = new FailOnceApplyState(
                        holdingsAdapter);
                }

                Rap = new RewardApplicationActions(
                    Id("authority.crafting-rap"),
                    new CraftingUnusedMoneyRewardChildState(),
                    new CraftingScrapSpendRewardChildState(Scrap),
                    holdingsAdapter);
                CraftingRecipe selected = recipe
                    ?? CraftingActionsTests.CreateRecipe();
                var catalogRecipes = recipes == null
                    ? new[] { selected }
                    : new List<CraftingRecipe>(recipes).ToArray();
                PrimaryRecipe = catalogRecipes[0];
                Service = new CraftingActions(
                    new CraftingRecipeCatalog(catalogRecipes),
                    Catalog,
                    new RewardGenerationActions(),
                    Rap,
                    Scrap,
                    CraftingUnusedMoneyRewardChildState.StableAuthorityId,
                    Holdings.AuthorityStableId);
            }

            public EquipmentCatalog Catalog { get; }
            public CatalogValidator Validator { get; }
            public ScrapWalletActions Scrap { get; }
            public PlayerHoldingsActions Holdings { get; }
            public RewardApplicationActions Rap { get; }
            public CraftingRecipe PrimaryRecipe { get; }
            public CraftingActions Service { get; }

            public CraftEquipmentCommand Command(
                int characterLevel = 60,
                ulong rootSeed = 44UL,
                StableId craftId = null,
                StableId recipeId = null)
            {
                return new CraftEquipmentCommand(
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

        private sealed class FailOnceApplyState : IRewardChildState
        {
            private readonly IRewardChildState inner;
            private bool failed;

            public FailOnceApplyState(IRewardChildState inner)
            {
                this.inner = inner;
            }

            public StableId AuthorityStableId
            {
                get { return inner.AuthorityStableId; }
            }

            public long Sequence { get { return inner.Sequence; } }

            public RewardStatePreflightResult Preflight(
                IReadOnlyList<RewardChildGrantCommand> commands)
            {
                return inner.Preflight(commands);
            }

            public RewardChildApplyResult Apply(
                RewardChildGrantCommand command)
            {
                if (!failed)
                {
                    failed = true;
                    return new RewardChildApplyResult(
                        command.TransactionStableId,
                        RewardChildApplyStatus.Rejected,
                        false,
                        "test-interruption");
                }
                return inner.Apply(command);
            }
        }

        private static CraftingRecipe CreateRecipe(
            StableId recipeId = null,
            StableId target = null,
            int naturalLevel = 50,
            int ordinaryActivationLevel = 50,
            int delay = 5,
            int minimumVariance = 0,
            int maximumVariance = 2,
            CraftingQualityPolicyKind qualityPolicy =
                CraftingQualityPolicyKind.Fixed,
            IEnumerable<CraftingWeightedDefinition> qualities = null,
            int minimumSlots = 0,
            int maximumSlots = 0,
            int maximumTier = 1,
            int maximumAugmentLevel = 1,
            IEnumerable<CraftingWeightedDefinition> augments = null)
        {
            return new CraftingRecipe(
                1,
                recipeId ?? Id("recipe.alpha"),
                target ?? EquipmentAlpha,
                Id("progression-source.equipment"),
                naturalLevel,
                ordinaryActivationLevel,
                delay,
                new CraftingDelayVariance(
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
                augments ?? Array.Empty<CraftingWeightedDefinition>(),
                new CraftingGeneratorPolicy(
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
            ScrapWalletActions wallet,
            long amount)
        {
            if (amount == 0L)
            {
                return;
            }

            ScrapTransactionResult result = wallet.Apply(
                new ScrapTransactionCommand(
                    Id("scrap-tx.initial"),
                    Id("scrap-op.initial"),
                    wallet.AuthorityStableId,
                    wallet.CurrencyStableId,
                    ScrapMutationKind.Grant,
                    amount,
                    ScrapIdentity.RewardGrantReason,
                    new ScrapProvenance(
                        ScrapIdentity.RewardSourceKind,
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

        private static CraftingWeightedDefinition Weighted(
            StableId id,
            ulong weight)
        {
            return new CraftingWeightedDefinition(id, weight);
        }

        private static StableId Id(string value)
        {
            return StableId.Parse(value);
        }
    }
}
