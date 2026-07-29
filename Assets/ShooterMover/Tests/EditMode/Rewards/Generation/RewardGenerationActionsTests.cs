using System;
using System.Collections.Generic;
using NUnit.Framework;
using ShooterMover.Application.Rewards.Generation;
using ShooterMover.Contracts.Rewards;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Common.Random;
using ShooterMover.Domain.Equipment;
using ShooterMover.Domain.Progression.Context;
using ShooterMover.Domain.Progression.Curves;
using ShooterMover.Domain.Rewards.Generation;
using ShooterMover.Domain.Rewards.Model;

namespace ShooterMover.Tests.EditMode.Rewards.Generation
{
    public sealed class RewardGenerationActionsTests
    {
        private readonly RewardGenerationActions service = new RewardGenerationActions();

        [Test]
        public void RewardGeneration_EqualCanonicalInputsProduceEqualResultsAndTraces()
        {
            RewardGrantSpecification guaranteed = FixedGrant(
                "grant.money",
                RewardGrantKind.Money,
                "currency.money",
                4L);
            RewardGrantSpecification independentGrant = RewardGrantSpecification.Create(
                Id("grant.scrap"),
                RewardGrantKind.Scrap,
                Id("currency.scrap"),
                RewardQuantityRange.Create(1L, 9L),
                new[]
                {
                    RewardScalingInputDescriptor.Create(
                        Id("scaling.character"),
                        RewardScalingInputKind.CharacterLevel)
                });
            IndependentRewardRoll independent = IndependentRewardRoll.Create(
                Id("roll.scrap"),
                650000,
                independentGrant);
            ExclusiveRewardGroup group = ExclusiveRewardGroup.Create(
                Id("group.box"),
                new[]
                {
                    WeightedRewardOutcome.CreateGrant(
                        Id("outcome.box"),
                        3L,
                        FixedGrant("grant.box", RewardGrantKind.Strongbox, "strongbox.tier-1", 1L)),
                    WeightedRewardOutcome.CreateExplicitNoDrop(Id("outcome.none"), 2L)
                });

            RewardProfile leftProfile = RewardProfile.Create(
                Id("reward-profile.equal"),
                new[] { guaranteed },
                new[] { independent },
                new[] { group });
            RewardProfile rightProfile = RewardProfile.Create(
                Id("reward-profile.equal"),
                new List<RewardGrantSpecification> { guaranteed },
                new List<IndependentRewardRoll> { independent },
                new List<ExclusiveRewardGroup> { group });
            Assert.That(rightProfile.Fingerprint, Is.EqualTo(leftProfile.Fingerprint));

            ProgressionContext context = Context(7, 5);
            RewardGenerationResultEnvelope left = service.GenerateReward(
                RewardRequest(leftProfile, context, 0xA55AA55AA55AA55AUL, "equal"));
            RewardGenerationResultEnvelope right = service.GenerateReward(
                RewardRequest(rightProfile, context, 0xA55AA55AA55AA55AUL, "equal"));

            Assert.That(left.Status, Is.EqualTo(right.Status));
            Assert.That(left.Result, Is.EqualTo(right.Result));
            Assert.That(left.RewardTrace, Is.EqualTo(right.RewardTrace));
            Assert.That(
                left.GenerationTrace.ToCanonicalString(),
                Is.EqualTo(right.GenerationTrace.ToCanonicalString()));
            Assert.That(left.ResultFingerprint, Is.EqualTo(right.ResultFingerprint));
        }

        [Test]
        public void RewardGeneration_ProfileInputOrderDoesNotChangeGameplayOrTrace()
        {
            RewardGrantSpecification alpha = FixedGrant(
                "grant.alpha",
                RewardGrantKind.Money,
                "currency.money",
                2L);
            RewardGrantSpecification beta = FixedGrant(
                "grant.beta",
                RewardGrantKind.Scrap,
                "currency.scrap",
                3L);
            RewardProfile forward = RewardProfile.Create(
                Id("reward-profile.order"),
                new[] { alpha, beta },
                Array.Empty<IndependentRewardRoll>(),
                Array.Empty<ExclusiveRewardGroup>());
            RewardProfile reverse = RewardProfile.Create(
                Id("reward-profile.order"),
                new[] { beta, alpha },
                Array.Empty<IndependentRewardRoll>(),
                Array.Empty<ExclusiveRewardGroup>());

            RewardGenerationResultEnvelope left = service.GenerateReward(
                RewardRequest(forward, Context(1, 1), 80UL, "order"));
            RewardGenerationResultEnvelope right = service.GenerateReward(
                RewardRequest(reverse, Context(1, 1), 80UL, "order"));

            Assert.That(forward.Fingerprint, Is.EqualTo(reverse.Fingerprint));
            Assert.That(left.Result, Is.EqualTo(right.Result));
            Assert.That(left.RewardTrace, Is.EqualTo(right.RewardTrace));
            Assert.That(left.GenerationTrace.Fingerprint, Is.EqualTo(right.GenerationTrace.Fingerprint));
        }

        [Test]
        public void RewardGeneration_FrozenQuantityAndResultFingerprintRemainStable()
        {
            RewardGrantSpecification grant = RewardGrantSpecification.Create(
                Id("grant.money"),
                RewardGrantKind.Money,
                Id("currency.money"),
                RewardQuantityRange.Create(2L, 9L),
                new[]
                {
                    RewardScalingInputDescriptor.Create(
                        Id("scaling.character"),
                        RewardScalingInputKind.CharacterLevel)
                });
            RewardProfile profile = RewardProfile.Create(
                Id("reward-profile.frozen"),
                new[] { grant },
                Array.Empty<IndependentRewardRoll>(),
                Array.Empty<ExclusiveRewardGroup>());
            RewardOperationRequest operation = RewardOperationRequest.Create(
                Id("run.frozen"),
                Id("source.frozen"),
                Id("operation.frozen"),
                Id("commitment.frozen"),
                profile.ProfileStableId,
                profile.Fingerprint);
            RewardGenerationRequest request = RewardGenerationRequest.Create(
                operation,
                profile,
                Context(3, 0),
                0x0123456789ABCDEFUL,
                DeterministicRandom.AlgorithmVersion1);

            RewardGenerationResultEnvelope result = service.GenerateReward(request);

            Assert.That(result.Status, Is.EqualTo(RewardGenerationStatus.Generated));
            Assert.That(result.Result.Grants, Has.Count.EqualTo(1));
            Assert.That(result.Result.Grants[0].Quantity, Is.EqualTo(8L));
            Assert.That(
                result.ResultFingerprint,
                Is.EqualTo("sha256:6b17ac0033ca53ac9ab09de007ba996dbe8ebca06d6b4cfcb4c8b500ae8316cc"));
        }

        [Test]
        public void RewardGeneration_GuaranteedIndependentExclusiveAndQuantityPathsAreRepresented()
        {
            RewardProfile profile = RewardProfile.Create(
                Id("reward-profile.mixed"),
                new[]
                {
                    RewardGrantSpecification.Create(
                        Id("grant.guaranteed"),
                        RewardGrantKind.Money,
                        Id("currency.money"),
                        RewardQuantityRange.Create(2L, 5L),
                        Array.Empty<RewardScalingInputDescriptor>())
                },
                new[]
                {
                    IndependentRewardRoll.Create(
                        Id("roll.always"),
                        IndependentRewardRoll.ProbabilityScale,
                        FixedGrant("grant.independent", RewardGrantKind.Scrap, "currency.scrap", 2L))
                },
                new[]
                {
                    ExclusiveRewardGroup.Create(
                        Id("group.always-grant"),
                        new[]
                        {
                            WeightedRewardOutcome.CreateGrant(
                                Id("outcome.only"),
                                1L,
                                FixedGrant("grant.exclusive", RewardGrantKind.Miscellaneous, "item.component", 1L))
                        })
                });

            RewardGenerationResultEnvelope result = service.GenerateReward(
                RewardRequest(profile, Context(2, 1), 73UL, "mixed"));

            Assert.That(result.Status, Is.EqualTo(RewardGenerationStatus.Generated));
            Assert.That(result.Result.Grants, Has.Count.EqualTo(3));
            Assert.That(ContainsDecision(result.RewardTrace.Entries, RewardTraceDecisionKind.Guaranteed), Is.True);
            Assert.That(ContainsDecision(result.RewardTrace.Entries, RewardTraceDecisionKind.IndependentChance), Is.True);
            Assert.That(ContainsDecision(result.RewardTrace.Entries, RewardTraceDecisionKind.ExclusiveSelection), Is.True);
            Assert.That(ContainsDecision(result.RewardTrace.Entries, RewardTraceDecisionKind.Quantity), Is.True);
        }

        [Test]
        public void RewardGeneration_ExplicitNoDropIsNotAnEmptyAccident()
        {
            RewardProfile profile = RewardProfile.CreateExplicitNoDrop(
                Id("reward-profile.none"));

            RewardGenerationResultEnvelope result = service.GenerateReward(
                RewardRequest(profile, Context(0, 0), 9UL, "none"));

            Assert.That(result.Status, Is.EqualTo(RewardGenerationStatus.ExplicitNoDrop));
            Assert.That(result.Result.Disposition, Is.EqualTo(RewardResultDisposition.ExplicitNoDrop));
            Assert.That(result.Result.Grants, Is.Empty);
            Assert.That(result.GenerationTrace.Entries.Count, Is.GreaterThan(0));
        }

        [Test]
        public void RewardGeneration_MissingCustomScalingFailsDeterministicallyWithoutPartialGrant()
        {
            RewardGrantSpecification grant = RewardGrantSpecification.Create(
                Id("grant.custom"),
                RewardGrantKind.Miscellaneous,
                Id("item.custom"),
                RewardQuantityRange.Fixed(1L),
                new[]
                {
                    RewardScalingInputDescriptor.Create(
                        Id("scaling.source-tier"),
                        RewardScalingInputKind.SourceTier)
                });
            RewardProfile profile = RewardProfile.Create(
                Id("reward-profile.custom"),
                new[] { grant },
                Array.Empty<IndependentRewardRoll>(),
                Array.Empty<ExclusiveRewardGroup>());

            RewardGenerationResultEnvelope left = service.GenerateReward(
                RewardRequest(profile, Context(1, 1), 5UL, "missing-scaling"));
            RewardGenerationResultEnvelope right = service.GenerateReward(
                RewardRequest(profile, Context(1, 1), 5UL, "missing-scaling"));

            Assert.That(left.Status, Is.EqualTo(RewardGenerationStatus.ImpossiblePolicy));
            Assert.That(left.Result, Is.Null);
            Assert.That(left.RewardTrace, Is.Null);
            Assert.That(left.ResultFingerprint, Is.EqualTo(right.ResultFingerprint));
            Assert.That(left.FailureReason, Does.Contain("missing-explicit-scaling-value"));
        }

        [Test]
        public void EquipmentGeneration_EqualInputsProduceEqualImmutableEquipmentAndTrace()
        {
            EquipmentCatalog catalog = BuildCatalog();
            EquipmentGenerationPolicy policy = StandardPolicy(
                new[] { EquipmentCandidate("equipment.armor-alpha", 0, 100, 1.0) },
                new[] { QualityCandidate("quality.common", 0L, 1UL) },
                new[] { AugmentCandidate("augment.power", 0, 100, 1UL) },
                1,
                1,
                true);
            EquipmentGenerationRequest request = EquipmentGenerationRequest.Create(
                Id("operation.equipment-equal"),
                Id("equipment-instance.equal"),
                policy,
                catalog,
                Context(10, 2),
                1001UL,
                DeterministicRandom.AlgorithmVersion1);

            EquipmentGenerationResult left = service.GenerateEquipment(request);
            EquipmentGenerationResult right = service.GenerateEquipment(request);

            Assert.That(left.Status, Is.EqualTo(RewardGenerationStatus.Generated));
            Assert.That(left.Equipment, Is.EqualTo(right.Equipment));
            Assert.That(left.ResultFingerprint, Is.EqualTo(right.ResultFingerprint));
            Assert.That(left.Trace.ToCanonicalString(), Is.EqualTo(right.Trace.ToCanonicalString()));
        }

        [Test]
        public void EquipmentGeneration_LowAndHighContextsUseExplicitEligibilityRanges()
        {
            EquipmentCatalog catalog = BuildCatalog();
            EquipmentGenerationPolicy policy = StandardPolicy(
                new[]
                {
                    EquipmentCandidate("equipment.armor-alpha", 0, 10, 1.0),
                    EquipmentCandidate("equipment.armor-beta", 11, 100, 1.0)
                },
                new[] { QualityCandidate("quality.common", 0L, 1UL) },
                Array.Empty<AugmentGenerationCandidate>(),
                0,
                0,
                true);

            EquipmentGenerationResult low = service.GenerateEquipment(
                EquipmentRequest("low", policy, catalog, Context(5, 1), 77UL));
            EquipmentGenerationResult high = service.GenerateEquipment(
                EquipmentRequest("high", policy, catalog, Context(25, 1), 77UL));

            Assert.That(low.Status, Is.EqualTo(RewardGenerationStatus.Generated));
            Assert.That(high.Status, Is.EqualTo(RewardGenerationStatus.Generated));
            Assert.That(low.Equipment.DefinitionId, Is.EqualTo(Id("equipment.armor-alpha")));
            Assert.That(high.Equipment.DefinitionId, Is.EqualTo(Id("equipment.armor-beta")));
        }

        [Test]
        public void EquipmentGeneration_NoEligibleCandidateReturnsDeterministicStatus()
        {
            EquipmentCatalog catalog = BuildCatalog();
            EquipmentGenerationPolicy policy = StandardPolicy(
                new[] { EquipmentCandidate("equipment.armor-alpha", 20, 30, 1.0) },
                new[] { QualityCandidate("quality.common", 0L, 1UL) },
                Array.Empty<AugmentGenerationCandidate>(),
                0,
                0,
                true);

            EquipmentGenerationResult left = service.GenerateEquipment(
                EquipmentRequest("none", policy, catalog, Context(2, 0), 44UL));
            EquipmentGenerationResult right = service.GenerateEquipment(
                EquipmentRequest("none", policy, catalog, Context(2, 0), 44UL));

            Assert.That(left.Status, Is.EqualTo(RewardGenerationStatus.NoEligibleCandidate));
            Assert.That(left.Equipment, Is.Null);
            Assert.That(left.ResultFingerprint, Is.EqualTo(right.ResultFingerprint));
            Assert.That(left.FailureReason, Is.EqualTo("no-eligible-equipment-candidate"));
        }

        [Test]
        public void EquipmentGeneration_CatalogCompatibilityFiltersImpossibleAugmentsBeforeSelection()
        {
            EquipmentCatalog catalog = BuildCatalog();
            EquipmentGenerationPolicy policy = StandardPolicy(
                new[] { EquipmentCandidate("equipment.armor-alpha", 0, 100, 1.0) },
                new[] { QualityCandidate("quality.common", 0L, 1UL) },
                new[]
                {
                    AugmentCandidate("augment.power", 0, 100, 1UL),
                    AugmentCandidate("augment.gun-only", 0, 100, 1000UL)
                },
                1,
                1,
                true);

            EquipmentGenerationResult result = service.GenerateEquipment(
                EquipmentRequest("compatibility", policy, catalog, Context(10, 0), 6UL));

            Assert.That(result.Status, Is.EqualTo(RewardGenerationStatus.Generated));
            Assert.That(result.Equipment.Augments, Has.Count.EqualTo(1));
            Assert.That(result.Equipment.Augments[0].DefinitionId, Is.EqualTo(Id("augment.power")));
            Assert.That(catalog.ValidateInstance(result.Equipment).IsValid, Is.True);
        }

        [Test]
        public void EquipmentGeneration_ImpossibleDuplicateAugmentRequirementFailsWithoutRetryLoop()
        {
            EquipmentCatalog catalog = BuildCatalog();
            EquipmentGenerationPolicy policy = StandardPolicy(
                new[] { EquipmentCandidate("equipment.armor-alpha", 0, 100, 1.0) },
                new[] { QualityCandidate("quality.common", 0L, 1UL) },
                new[] { AugmentCandidate("augment.power", 0, 100, 1UL) },
                2,
                2,
                true);

            EquipmentGenerationResult result = service.GenerateEquipment(
                EquipmentRequest("impossible-duplicate", policy, catalog, Context(10, 0), 99UL));

            Assert.That(result.Status, Is.EqualTo(RewardGenerationStatus.ImpossiblePolicy));
            Assert.That(result.Equipment, Is.Null);
            Assert.That(result.FailureReason, Does.Contain("no-compatible-augment-for-required-slot-1"));
        }

        [Test]
        public void EquipmentGeneration_IneligibleCandidateAndInputOrderDoNotShiftGameplayResult()
        {
            EquipmentCatalog catalog = BuildCatalog();
            EquipmentGenerationCandidate eligible = EquipmentCandidate(
                "equipment.armor-alpha",
                0,
                100,
                1.0);
            EquipmentGenerationCandidate ineligible = EquipmentCandidate(
                "equipment.armor-beta",
                50,
                100,
                999.0);
            EquipmentGenerationPolicy basePolicy = StandardPolicy(
                new[] { eligible },
                new[] { QualityCandidate("quality.common", 0L, 1UL) },
                new[] { AugmentCandidate("augment.power", 0, 100, 1UL) },
                1,
                1,
                true);
            EquipmentGenerationPolicy extendedPolicy = StandardPolicy(
                new[] { ineligible, eligible },
                new[] { QualityCandidate("quality.common", 0L, 1UL) },
                new[]
                {
                    AugmentCandidate("augment.gun-only", 0, 100, 999UL),
                    AugmentCandidate("augment.power", 0, 100, 1UL)
                },
                1,
                1,
                true,
                "generation-policy.extended");

            EquipmentGenerationResult baseResult = service.GenerateEquipment(
                EquipmentRequest("isolation", basePolicy, catalog, Context(10, 0), 0xBEEFUL));
            EquipmentGenerationResult extendedResult = service.GenerateEquipment(
                EquipmentRequest("isolation", extendedPolicy, catalog, Context(10, 0), 0xBEEFUL));

            Assert.That(baseResult.Status, Is.EqualTo(RewardGenerationStatus.Generated));
            Assert.That(extendedResult.Status, Is.EqualTo(RewardGenerationStatus.Generated));
            Assert.That(
                extendedResult.Equipment.ToCanonicalString(),
                Is.EqualTo(baseResult.Equipment.ToCanonicalString()));
            Assert.That(extendedResult.ContentFingerprint, Is.Not.EqualTo(baseResult.ContentFingerprint));
        }

        [Test]
        public void EquipmentGeneration_HasNoThreeSlotTierOrTenLevelCaps()
        {
            EquipmentCatalog catalog = BuildCatalog();
            EquipmentGenerationPolicy policy = StandardPolicy(
                new[] { EquipmentCandidate("equipment.armor-alpha", 0, 100, 1.0) },
                new[] { QualityCandidate("quality.legendary", 0L, 1UL) },
                new[]
                {
                    AugmentCandidate("augment.power", 0, 100, 1UL),
                    AugmentCandidate("augment.guard", 0, 100, 1UL),
                    AugmentCandidate("augment.mobility", 0, 100, 1UL),
                    AugmentCandidate("augment.utility", 0, 100, 1UL)
                },
                4,
                4,
                true);

            EquipmentGenerationResult result = service.GenerateEquipment(
                EquipmentRequest("uncapped", policy, catalog, Context(40, 4), 1234UL));

            Assert.That(result.Status, Is.EqualTo(RewardGenerationStatus.Generated));
            Assert.That(result.Equipment.QualityId, Is.EqualTo(Id("quality.legendary")));
            Assert.That(result.Equipment.Augments, Has.Count.EqualTo(4));
            for (int index = 0; index < result.Equipment.Augments.Count; index++)
            {
                Assert.That(result.Equipment.Augments[index].Tier, Is.GreaterThanOrEqualTo(4));
                Assert.That(result.Equipment.Augments[index].Level, Is.GreaterThan(10));
            }

            Assert.That(catalog.ValidateInstance(result.Equipment).IsValid, Is.True);
        }

        [Test]
        public void EquipmentCandidate_SourceBiasUsesAcceptedProgressionCurve()
        {
            EquipmentGenerationCandidate neutral = EquipmentCandidate(
                "equipment.armor-alpha",
                0,
                100,
                1.0);
            EquipmentGenerationCandidate biased = EquipmentGenerationCandidate.Create(
                Id("equipment.armor-alpha"),
                0,
                100,
                0,
                100,
                Array.Empty<StableId>(),
                0L,
                InclusiveIntRange.Create(1, 50),
                1.0,
                2.0);
            ProgressionContext context = Context(10, 0);
            SoftActivationCurveParameters activation = new SoftActivationCurveParameters(0.1, 5L, 5L);
            ObsolescenceCurveParameters obsolescence = new ObsolescenceCurveParameters(10L, 10.0, 0.2);

            Assert.That(
                biased.EvaluateWeight(context, activation, obsolescence),
                Is.EqualTo(neutral.EvaluateWeight(context, activation, obsolescence) * 2.0));
        }

        private static bool ContainsDecision(
            IReadOnlyList<RewardTraceEntry> entries,
            RewardTraceDecisionKind decision)
        {
            for (int index = 0; index < entries.Count; index++)
            {
                if (entries[index].DecisionKind == decision)
                {
                    return true;
                }
            }

            return false;
        }

        private static RewardGenerationRequest RewardRequest(
            RewardProfile profile,
            ProgressionContext context,
            ulong seed,
            string suffix)
        {
            RewardOperationRequest operation = RewardOperationRequest.Create(
                Id("run." + suffix),
                Id("source." + suffix),
                Id("operation." + suffix),
                Id("commitment." + suffix),
                profile.ProfileStableId,
                profile.Fingerprint);
            return RewardGenerationRequest.Create(
                operation,
                profile,
                context,
                seed,
                DeterministicRandom.AlgorithmVersion1);
        }

        private static EquipmentGenerationRequest EquipmentRequest(
            string suffix,
            EquipmentGenerationPolicy policy,
            EquipmentCatalog catalog,
            ProgressionContext context,
            ulong seed)
        {
            return EquipmentGenerationRequest.Create(
                Id("operation." + suffix),
                Id("equipment-instance." + suffix),
                policy,
                catalog,
                context,
                seed,
                DeterministicRandom.AlgorithmVersion1);
        }

        private static RewardGrantSpecification FixedGrant(
            string grantId,
            RewardGrantKind kind,
            string contentId,
            long quantity)
        {
            return RewardGrantSpecification.CreateFixed(
                Id(grantId),
                kind,
                Id(contentId),
                quantity);
        }

        private static EquipmentGenerationCandidate EquipmentCandidate(
            string definitionId,
            int minimumCharacterLevel,
            int maximumCharacterLevel,
            double sourceBias)
        {
            return EquipmentGenerationCandidate.Create(
                Id(definitionId),
                minimumCharacterLevel,
                maximumCharacterLevel,
                0,
                100,
                Array.Empty<StableId>(),
                0L,
                InclusiveIntRange.Create(1, 200),
                1.0,
                sourceBias);
        }

        private static EquipmentQualityCandidate QualityCandidate(
            string qualityId,
            long nominalLevel,
            ulong weight)
        {
            return EquipmentQualityCandidate.Create(Id(qualityId), nominalLevel, weight);
        }

        private static AugmentGenerationCandidate AugmentCandidate(
            string definitionId,
            int minimumCharacterLevel,
            int maximumCharacterLevel,
            ulong weight)
        {
            return AugmentGenerationCandidate.Create(
                Id(definitionId),
                minimumCharacterLevel,
                maximumCharacterLevel,
                weight);
        }

        private static EquipmentGenerationPolicy StandardPolicy(
            IEnumerable<EquipmentGenerationCandidate> equipment,
            IEnumerable<EquipmentQualityCandidate> qualities,
            IEnumerable<AugmentGenerationCandidate> augments,
            int minimumSlots,
            int maximumSlots,
            bool exactSlots,
            string policyId = "generation-policy.standard")
        {
            return EquipmentGenerationPolicy.Create(
                Id(policyId),
                equipment,
                qualities,
                augments,
                minimumSlots,
                maximumSlots,
                exactSlots,
                new SoftActivationCurveParameters(0.1, 5L, 5L),
                new ObsolescenceCurveParameters(25L, 15.0, 0.2));
        }

        private static ProgressionContext Context(int characterLevel, int regionLevel)
        {
            return ProgressionContext.Create(
                characterLevel,
                regionLevel,
                Id("difficulty.normal"),
                1,
                Array.Empty<StableId>());
        }

        private static EquipmentCatalog BuildCatalog()
        {
            EquipmentQualityTier common = EquipmentQualityTier.Create(
                Id("quality.common"),
                "Common",
                1);
            EquipmentQualityTier legendary = EquipmentQualityTier.Create(
                Id("quality.legendary"),
                "Legendary",
                7);
            StableId energyTag = Id("equipment-tag.energy");
            EquipmentDefinition armorAlpha = EquipmentDefinition.Create(
                Id("equipment.armor-alpha"),
                EquipmentCategoryIds.Armor,
                Id("equipment-family.alpha"),
                "Armor Alpha",
                null,
                InclusiveIntRange.Create(1, 200),
                5,
                new[] { legendary, common },
                new[] { energyTag });
            EquipmentDefinition armorBeta = EquipmentDefinition.Create(
                Id("equipment.armor-beta"),
                EquipmentCategoryIds.Armor,
                Id("equipment-family.beta"),
                "Armor Beta",
                null,
                InclusiveIntRange.Create(1, 400),
                6,
                new[] { common, legendary },
                new[] { energyTag });
            EquipmentDefinition gun = EquipmentDefinition.Create(
                Id("equipment.gun-fixture"),
                EquipmentCategoryIds.Gun,
                Id("equipment-family.gun-fixture"),
                "Gun Fixture",
                Id("gun.blaster-machine-gun"),
                InclusiveIntRange.Create(1, 100),
                2,
                new[] { common },
                Array.Empty<StableId>());

            AugmentDefinition power = AugmentDefinition.Create(
                Id("augment.power"),
                Id("augment-family.power"),
                "Power",
                Compatibility(EquipmentCategoryIds.Armor, energyTag),
                Array.Empty<StableId>(),
                AugmentDuplicatePolicy.DisallowSameDefinition,
                InclusiveIntRange.Create(4, 7),
                InclusiveIntRange.Create(11, 20));
            AugmentDefinition guard = AugmentDefinition.Create(
                Id("augment.guard"),
                Id("augment-family.guard"),
                "Guard",
                Compatibility(EquipmentCategoryIds.Armor, energyTag),
                Array.Empty<StableId>(),
                AugmentDuplicatePolicy.DisallowSameDefinition,
                InclusiveIntRange.Create(4, 8),
                InclusiveIntRange.Create(12, 25));
            AugmentDefinition mobility = AugmentDefinition.Create(
                Id("augment.mobility"),
                Id("augment-family.mobility"),
                "Mobility",
                Compatibility(EquipmentCategoryIds.Armor, energyTag),
                Array.Empty<StableId>(),
                AugmentDuplicatePolicy.DisallowSameDefinition,
                InclusiveIntRange.Create(5, 9),
                InclusiveIntRange.Create(13, 30));
            AugmentDefinition utility = AugmentDefinition.Create(
                Id("augment.utility"),
                Id("augment-family.utility"),
                "Utility",
                Compatibility(EquipmentCategoryIds.Armor, energyTag),
                Array.Empty<StableId>(),
                AugmentDuplicatePolicy.DisallowSameDefinition,
                InclusiveIntRange.Create(6, 10),
                InclusiveIntRange.Create(14, 35));
            AugmentDefinition gunOnly = AugmentDefinition.Create(
                Id("augment.gun-only"),
                Id("augment-family.gun"),
                "Gun Only",
                AugmentCompatibility.Create(
                    new[] { EquipmentCategoryIds.Gun },
                    Array.Empty<StableId>(),
                    Array.Empty<StableId>(),
                    Array.Empty<StableId>()),
                Array.Empty<StableId>(),
                AugmentDuplicatePolicy.DisallowSameDefinition,
                InclusiveIntRange.Create(1, 2),
                InclusiveIntRange.Create(1, 3));

            EquipmentCatalogBuildResult build = EquipmentCatalog.Build(
                new[] { gun, armorBeta, armorAlpha },
                new[] { utility, gunOnly, power, mobility, guard });
            if (!build.IsValid)
            {
                throw new InvalidOperationException("Fixture catalog is invalid: " + JoinIssues(build.Issues));
            }

            return build.Catalog;
        }

        private static AugmentCompatibility Compatibility(StableId categoryId, StableId requiredTag)
        {
            return AugmentCompatibility.Create(
                new[] { categoryId },
                Array.Empty<StableId>(),
                new[] { requiredTag },
                Array.Empty<StableId>());
        }

        private static string JoinIssues(IReadOnlyList<EquipmentModelIssue> issues)
        {
            string result = string.Empty;
            for (int index = 0; index < issues.Count; index++)
            {
                result += (index == 0 ? string.Empty : ";") + issues[index];
            }

            return result;
        }

        private static StableId Id(string value)
        {
            return StableId.Parse(value);
        }
    }
}
