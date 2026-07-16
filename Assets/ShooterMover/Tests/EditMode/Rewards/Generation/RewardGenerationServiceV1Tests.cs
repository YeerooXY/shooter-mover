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
    public sealed class RewardGenerationServiceV1Tests
    {
        private readonly RewardGenerationServiceV1 service = new RewardGenerationServiceV1();

        [Test]
        public void RewardGeneration_EqualCanonicalInputsProduceEqualResultsAndTraces()
        {
            RewardGrantSpecificationV1 guaranteed = FixedGrant(
                "grant.money",
                RewardGrantKindV1.Money,
                "currency.money",
                4L);
            RewardGrantSpecificationV1 independentGrant = RewardGrantSpecificationV1.Create(
                Id("grant.scrap"),
                RewardGrantKindV1.Scrap,
                Id("currency.scrap"),
                RewardQuantityRangeV1.Create(1L, 9L),
                new[]
                {
                    RewardScalingInputDescriptorV1.Create(
                        Id("scaling.character"),
                        RewardScalingInputKindV1.CharacterLevel)
                });
            IndependentRewardRollV1 independent = IndependentRewardRollV1.Create(
                Id("roll.scrap"),
                650000,
                independentGrant);
            ExclusiveRewardGroupV1 group = ExclusiveRewardGroupV1.Create(
                Id("group.box"),
                new[]
                {
                    WeightedRewardOutcomeV1.CreateGrant(
                        Id("outcome.box"),
                        3L,
                        FixedGrant("grant.box", RewardGrantKindV1.Strongbox, "strongbox.tier-1", 1L)),
                    WeightedRewardOutcomeV1.CreateExplicitNoDrop(Id("outcome.none"), 2L)
                });

            RewardProfileV1 leftProfile = RewardProfileV1.Create(
                Id("reward-profile.equal"),
                new[] { guaranteed },
                new[] { independent },
                new[] { group });
            RewardProfileV1 rightProfile = RewardProfileV1.Create(
                Id("reward-profile.equal"),
                new List<RewardGrantSpecificationV1> { guaranteed },
                new List<IndependentRewardRollV1> { independent },
                new List<ExclusiveRewardGroupV1> { group });
            Assert.That(rightProfile.Fingerprint, Is.EqualTo(leftProfile.Fingerprint));

            ProgressionContext context = Context(7, 5);
            RewardGenerationResultEnvelopeV1 left = service.GenerateReward(
                RewardRequest(leftProfile, context, 0xA55AA55AA55AA55AUL, "equal"));
            RewardGenerationResultEnvelopeV1 right = service.GenerateReward(
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
            RewardGrantSpecificationV1 alpha = FixedGrant(
                "grant.alpha",
                RewardGrantKindV1.Money,
                "currency.money",
                2L);
            RewardGrantSpecificationV1 beta = FixedGrant(
                "grant.beta",
                RewardGrantKindV1.Scrap,
                "currency.scrap",
                3L);
            RewardProfileV1 forward = RewardProfileV1.Create(
                Id("reward-profile.order"),
                new[] { alpha, beta },
                Array.Empty<IndependentRewardRollV1>(),
                Array.Empty<ExclusiveRewardGroupV1>());
            RewardProfileV1 reverse = RewardProfileV1.Create(
                Id("reward-profile.order"),
                new[] { beta, alpha },
                Array.Empty<IndependentRewardRollV1>(),
                Array.Empty<ExclusiveRewardGroupV1>());

            RewardGenerationResultEnvelopeV1 left = service.GenerateReward(
                RewardRequest(forward, Context(1, 1), 80UL, "order"));
            RewardGenerationResultEnvelopeV1 right = service.GenerateReward(
                RewardRequest(reverse, Context(1, 1), 80UL, "order"));

            Assert.That(forward.Fingerprint, Is.EqualTo(reverse.Fingerprint));
            Assert.That(left.Result, Is.EqualTo(right.Result));
            Assert.That(left.RewardTrace, Is.EqualTo(right.RewardTrace));
            Assert.That(left.GenerationTrace.Fingerprint, Is.EqualTo(right.GenerationTrace.Fingerprint));
        }

        [Test]
        public void RewardGeneration_FrozenQuantityAndResultFingerprintRemainStable()
        {
            RewardGrantSpecificationV1 grant = RewardGrantSpecificationV1.Create(
                Id("grant.money"),
                RewardGrantKindV1.Money,
                Id("currency.money"),
                RewardQuantityRangeV1.Create(2L, 9L),
                new[]
                {
                    RewardScalingInputDescriptorV1.Create(
                        Id("scaling.character"),
                        RewardScalingInputKindV1.CharacterLevel)
                });
            RewardProfileV1 profile = RewardProfileV1.Create(
                Id("reward-profile.frozen"),
                new[] { grant },
                Array.Empty<IndependentRewardRollV1>(),
                Array.Empty<ExclusiveRewardGroupV1>());
            RewardOperationRequestV1 operation = RewardOperationRequestV1.Create(
                Id("run.frozen"),
                Id("source.frozen"),
                Id("operation.frozen"),
                Id("commitment.frozen"),
                profile.ProfileStableId,
                profile.Fingerprint);
            RewardGenerationRequestV1 request = RewardGenerationRequestV1.Create(
                operation,
                profile,
                Context(3, 0),
                0x0123456789ABCDEFUL,
                DeterministicRandom.AlgorithmVersion1);

            RewardGenerationResultEnvelopeV1 result = service.GenerateReward(request);

            Assert.That(result.Status, Is.EqualTo(RewardGenerationStatusV1.Generated));
            Assert.That(result.Result.Grants, Has.Count.EqualTo(1));
            Assert.That(result.Result.Grants[0].Quantity, Is.EqualTo(8L));
            Assert.That(
                result.ResultFingerprint,
                Is.EqualTo("sha256:6b17ac0033ca53ac9ab09de007ba996dbe8ebca06d6b4cfcb4c8b500ae8316cc"));
        }

        [Test]
        public void RewardGeneration_GuaranteedIndependentExclusiveAndQuantityPathsAreRepresented()
        {
            RewardProfileV1 profile = RewardProfileV1.Create(
                Id("reward-profile.mixed"),
                new[]
                {
                    RewardGrantSpecificationV1.Create(
                        Id("grant.guaranteed"),
                        RewardGrantKindV1.Money,
                        Id("currency.money"),
                        RewardQuantityRangeV1.Create(2L, 5L),
                        Array.Empty<RewardScalingInputDescriptorV1>())
                },
                new[]
                {
                    IndependentRewardRollV1.Create(
                        Id("roll.always"),
                        IndependentRewardRollV1.ProbabilityScale,
                        FixedGrant("grant.independent", RewardGrantKindV1.Scrap, "currency.scrap", 2L))
                },
                new[]
                {
                    ExclusiveRewardGroupV1.Create(
                        Id("group.always-grant"),
                        new[]
                        {
                            WeightedRewardOutcomeV1.CreateGrant(
                                Id("outcome.only"),
                                1L,
                                FixedGrant("grant.exclusive", RewardGrantKindV1.Miscellaneous, "item.component", 1L))
                        })
                });

            RewardGenerationResultEnvelopeV1 result = service.GenerateReward(
                RewardRequest(profile, Context(2, 1), 73UL, "mixed"));

            Assert.That(result.Status, Is.EqualTo(RewardGenerationStatusV1.Generated));
            Assert.That(result.Result.Grants, Has.Count.EqualTo(3));
            Assert.That(ContainsDecision(result.RewardTrace.Entries, RewardTraceDecisionKindV1.Guaranteed), Is.True);
            Assert.That(ContainsDecision(result.RewardTrace.Entries, RewardTraceDecisionKindV1.IndependentChance), Is.True);
            Assert.That(ContainsDecision(result.RewardTrace.Entries, RewardTraceDecisionKindV1.ExclusiveSelection), Is.True);
            Assert.That(ContainsDecision(result.RewardTrace.Entries, RewardTraceDecisionKindV1.Quantity), Is.True);
        }

        [Test]
        public void RewardGeneration_ExplicitNoDropIsNotAnEmptyAccident()
        {
            RewardProfileV1 profile = RewardProfileV1.CreateExplicitNoDrop(
                Id("reward-profile.none"));

            RewardGenerationResultEnvelopeV1 result = service.GenerateReward(
                RewardRequest(profile, Context(0, 0), 9UL, "none"));

            Assert.That(result.Status, Is.EqualTo(RewardGenerationStatusV1.ExplicitNoDrop));
            Assert.That(result.Result.Disposition, Is.EqualTo(RewardResultDispositionV1.ExplicitNoDrop));
            Assert.That(result.Result.Grants, Is.Empty);
            Assert.That(result.GenerationTrace.Entries.Count, Is.GreaterThan(0));
        }

        [Test]
        public void RewardGeneration_MissingCustomScalingFailsDeterministicallyWithoutPartialGrant()
        {
            RewardGrantSpecificationV1 grant = RewardGrantSpecificationV1.Create(
                Id("grant.custom"),
                RewardGrantKindV1.Miscellaneous,
                Id("item.custom"),
                RewardQuantityRangeV1.Fixed(1L),
                new[]
                {
                    RewardScalingInputDescriptorV1.Create(
                        Id("scaling.source-tier"),
                        RewardScalingInputKindV1.SourceTier)
                });
            RewardProfileV1 profile = RewardProfileV1.Create(
                Id("reward-profile.custom"),
                new[] { grant },
                Array.Empty<IndependentRewardRollV1>(),
                Array.Empty<ExclusiveRewardGroupV1>());

            RewardGenerationResultEnvelopeV1 left = service.GenerateReward(
                RewardRequest(profile, Context(1, 1), 5UL, "missing-scaling"));
            RewardGenerationResultEnvelopeV1 right = service.GenerateReward(
                RewardRequest(profile, Context(1, 1), 5UL, "missing-scaling"));

            Assert.That(left.Status, Is.EqualTo(RewardGenerationStatusV1.ImpossiblePolicy));
            Assert.That(left.Result, Is.Null);
            Assert.That(left.RewardTrace, Is.Null);
            Assert.That(left.ResultFingerprint, Is.EqualTo(right.ResultFingerprint));
            Assert.That(left.FailureReason, Does.Contain("missing-explicit-scaling-value"));
        }

        [Test]
        public void EquipmentGeneration_EqualInputsProduceEqualImmutableEquipmentAndTrace()
        {
            EquipmentCatalog catalog = BuildCatalog();
            EquipmentGenerationPolicyV1 policy = StandardPolicy(
                new[] { EquipmentCandidate("equipment.armor-alpha", 0, 100, 1.0) },
                new[] { QualityCandidate("quality.common", 0L, 1UL) },
                new[] { AugmentCandidate("augment.power", 0, 100, 1UL) },
                1,
                1,
                true);
            EquipmentGenerationRequestV1 request = EquipmentGenerationRequestV1.Create(
                Id("operation.equipment-equal"),
                Id("equipment-instance.equal"),
                policy,
                catalog,
                Context(10, 2),
                1001UL,
                DeterministicRandom.AlgorithmVersion1);

            EquipmentGenerationResultV1 left = service.GenerateEquipment(request);
            EquipmentGenerationResultV1 right = service.GenerateEquipment(request);

            Assert.That(left.Status, Is.EqualTo(RewardGenerationStatusV1.Generated));
            Assert.That(left.Equipment, Is.EqualTo(right.Equipment));
            Assert.That(left.ResultFingerprint, Is.EqualTo(right.ResultFingerprint));
            Assert.That(left.Trace.ToCanonicalString(), Is.EqualTo(right.Trace.ToCanonicalString()));
        }

        [Test]
        public void EquipmentGeneration_LowAndHighContextsUseExplicitEligibilityRanges()
        {
            EquipmentCatalog catalog = BuildCatalog();
            EquipmentGenerationPolicyV1 policy = StandardPolicy(
                new[]
                {
                    EquipmentCandidate("equipment.armor-alpha", 0, 10, 1.0),
                    EquipmentCandidate("equipment.armor-beta", 11, 100, 1.0)
                },
                new[] { QualityCandidate("quality.common", 0L, 1UL) },
                Array.Empty<AugmentGenerationCandidateV1>(),
                0,
                0,
                true);

            EquipmentGenerationResultV1 low = service.GenerateEquipment(
                EquipmentRequest("low", policy, catalog, Context(5, 1), 77UL));
            EquipmentGenerationResultV1 high = service.GenerateEquipment(
                EquipmentRequest("high", policy, catalog, Context(25, 1), 77UL));

            Assert.That(low.Status, Is.EqualTo(RewardGenerationStatusV1.Generated));
            Assert.That(high.Status, Is.EqualTo(RewardGenerationStatusV1.Generated));
            Assert.That(low.Equipment.DefinitionId, Is.EqualTo(Id("equipment.armor-alpha")));
            Assert.That(high.Equipment.DefinitionId, Is.EqualTo(Id("equipment.armor-beta")));
        }

        [Test]
        public void EquipmentGeneration_NoEligibleCandidateReturnsDeterministicStatus()
        {
            EquipmentCatalog catalog = BuildCatalog();
            EquipmentGenerationPolicyV1 policy = StandardPolicy(
                new[] { EquipmentCandidate("equipment.armor-alpha", 20, 30, 1.0) },
                new[] { QualityCandidate("quality.common", 0L, 1UL) },
                Array.Empty<AugmentGenerationCandidateV1>(),
                0,
                0,
                true);

            EquipmentGenerationResultV1 left = service.GenerateEquipment(
                EquipmentRequest("none", policy, catalog, Context(2, 0), 44UL));
            EquipmentGenerationResultV1 right = service.GenerateEquipment(
                EquipmentRequest("none", policy, catalog, Context(2, 0), 44UL));

            Assert.That(left.Status, Is.EqualTo(RewardGenerationStatusV1.NoEligibleCandidate));
            Assert.That(left.Equipment, Is.Null);
            Assert.That(left.ResultFingerprint, Is.EqualTo(right.ResultFingerprint));
            Assert.That(left.FailureReason, Is.EqualTo("no-eligible-equipment-candidate"));
        }

        [Test]
        public void EquipmentGeneration_CatalogCompatibilityFiltersImpossibleAugmentsBeforeSelection()
        {
            EquipmentCatalog catalog = BuildCatalog();
            EquipmentGenerationPolicyV1 policy = StandardPolicy(
                new[] { EquipmentCandidate("equipment.armor-alpha", 0, 100, 1.0) },
                new[] { QualityCandidate("quality.common", 0L, 1UL) },
                new[]
                {
                    AugmentCandidate("augment.power", 0, 100, 1UL),
                    AugmentCandidate("augment.weapon-only", 0, 100, 1000UL)
                },
                1,
                1,
                true);

            EquipmentGenerationResultV1 result = service.GenerateEquipment(
                EquipmentRequest("compatibility", policy, catalog, Context(10, 0), 6UL));

            Assert.That(result.Status, Is.EqualTo(RewardGenerationStatusV1.Generated));
            Assert.That(result.Equipment.Augments, Has.Count.EqualTo(1));
            Assert.That(result.Equipment.Augments[0].DefinitionId, Is.EqualTo(Id("augment.power")));
            Assert.That(catalog.ValidateInstance(result.Equipment).IsValid, Is.True);
        }

        [Test]
        public void EquipmentGeneration_ImpossibleDuplicateAugmentRequirementFailsWithoutRetryLoop()
        {
            EquipmentCatalog catalog = BuildCatalog();
            EquipmentGenerationPolicyV1 policy = StandardPolicy(
                new[] { EquipmentCandidate("equipment.armor-alpha", 0, 100, 1.0) },
                new[] { QualityCandidate("quality.common", 0L, 1UL) },
                new[] { AugmentCandidate("augment.power", 0, 100, 1UL) },
                2,
                2,
                true);

            EquipmentGenerationResultV1 result = service.GenerateEquipment(
                EquipmentRequest("impossible-duplicate", policy, catalog, Context(10, 0), 99UL));

            Assert.That(result.Status, Is.EqualTo(RewardGenerationStatusV1.ImpossiblePolicy));
            Assert.That(result.Equipment, Is.Null);
            Assert.That(result.FailureReason, Does.Contain("no-compatible-augment-for-required-slot-1"));
        }

        [Test]
        public void EquipmentGeneration_IneligibleCandidateAndInputOrderDoNotShiftGameplayResult()
        {
            EquipmentCatalog catalog = BuildCatalog();
            EquipmentGenerationCandidateV1 eligible = EquipmentCandidate(
                "equipment.armor-alpha",
                0,
                100,
                1.0);
            EquipmentGenerationCandidateV1 ineligible = EquipmentCandidate(
                "equipment.armor-beta",
                50,
                100,
                999.0);
            EquipmentGenerationPolicyV1 basePolicy = StandardPolicy(
                new[] { eligible },
                new[] { QualityCandidate("quality.common", 0L, 1UL) },
                new[] { AugmentCandidate("augment.power", 0, 100, 1UL) },
                1,
                1,
                true);
            EquipmentGenerationPolicyV1 extendedPolicy = StandardPolicy(
                new[] { ineligible, eligible },
                new[] { QualityCandidate("quality.common", 0L, 1UL) },
                new[]
                {
                    AugmentCandidate("augment.weapon-only", 0, 100, 999UL),
                    AugmentCandidate("augment.power", 0, 100, 1UL)
                },
                1,
                1,
                true,
                "generation-policy.extended");

            EquipmentGenerationResultV1 baseResult = service.GenerateEquipment(
                EquipmentRequest("isolation", basePolicy, catalog, Context(10, 0), 0xBEEFUL));
            EquipmentGenerationResultV1 extendedResult = service.GenerateEquipment(
                EquipmentRequest("isolation", extendedPolicy, catalog, Context(10, 0), 0xBEEFUL));

            Assert.That(baseResult.Status, Is.EqualTo(RewardGenerationStatusV1.Generated));
            Assert.That(extendedResult.Status, Is.EqualTo(RewardGenerationStatusV1.Generated));
            Assert.That(
                extendedResult.Equipment.ToCanonicalString(),
                Is.EqualTo(baseResult.Equipment.ToCanonicalString()));
            Assert.That(extendedResult.ContentFingerprint, Is.Not.EqualTo(baseResult.ContentFingerprint));
        }

        [Test]
        public void EquipmentGeneration_HasNoThreeSlotTierOrTenLevelCaps()
        {
            EquipmentCatalog catalog = BuildCatalog();
            EquipmentGenerationPolicyV1 policy = StandardPolicy(
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

            EquipmentGenerationResultV1 result = service.GenerateEquipment(
                EquipmentRequest("uncapped", policy, catalog, Context(40, 4), 1234UL));

            Assert.That(result.Status, Is.EqualTo(RewardGenerationStatusV1.Generated));
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
            EquipmentGenerationCandidateV1 neutral = EquipmentCandidate(
                "equipment.armor-alpha",
                0,
                100,
                1.0);
            EquipmentGenerationCandidateV1 biased = EquipmentGenerationCandidateV1.Create(
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
            IReadOnlyList<RewardTraceEntryV1> entries,
            RewardTraceDecisionKindV1 decision)
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

        private static RewardGenerationRequestV1 RewardRequest(
            RewardProfileV1 profile,
            ProgressionContext context,
            ulong seed,
            string suffix)
        {
            RewardOperationRequestV1 operation = RewardOperationRequestV1.Create(
                Id("run." + suffix),
                Id("source." + suffix),
                Id("operation." + suffix),
                Id("commitment." + suffix),
                profile.ProfileStableId,
                profile.Fingerprint);
            return RewardGenerationRequestV1.Create(
                operation,
                profile,
                context,
                seed,
                DeterministicRandom.AlgorithmVersion1);
        }

        private static EquipmentGenerationRequestV1 EquipmentRequest(
            string suffix,
            EquipmentGenerationPolicyV1 policy,
            EquipmentCatalog catalog,
            ProgressionContext context,
            ulong seed)
        {
            return EquipmentGenerationRequestV1.Create(
                Id("operation." + suffix),
                Id("equipment-instance." + suffix),
                policy,
                catalog,
                context,
                seed,
                DeterministicRandom.AlgorithmVersion1);
        }

        private static RewardGrantSpecificationV1 FixedGrant(
            string grantId,
            RewardGrantKindV1 kind,
            string contentId,
            long quantity)
        {
            return RewardGrantSpecificationV1.CreateFixed(
                Id(grantId),
                kind,
                Id(contentId),
                quantity);
        }

        private static EquipmentGenerationCandidateV1 EquipmentCandidate(
            string definitionId,
            int minimumCharacterLevel,
            int maximumCharacterLevel,
            double sourceBias)
        {
            return EquipmentGenerationCandidateV1.Create(
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

        private static EquipmentQualityCandidateV1 QualityCandidate(
            string qualityId,
            long nominalLevel,
            ulong weight)
        {
            return EquipmentQualityCandidateV1.Create(Id(qualityId), nominalLevel, weight);
        }

        private static AugmentGenerationCandidateV1 AugmentCandidate(
            string definitionId,
            int minimumCharacterLevel,
            int maximumCharacterLevel,
            ulong weight)
        {
            return AugmentGenerationCandidateV1.Create(
                Id(definitionId),
                minimumCharacterLevel,
                maximumCharacterLevel,
                weight);
        }

        private static EquipmentGenerationPolicyV1 StandardPolicy(
            IEnumerable<EquipmentGenerationCandidateV1> equipment,
            IEnumerable<EquipmentQualityCandidateV1> qualities,
            IEnumerable<AugmentGenerationCandidateV1> augments,
            int minimumSlots,
            int maximumSlots,
            bool exactSlots,
            string policyId = "generation-policy.standard")
        {
            return EquipmentGenerationPolicyV1.Create(
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
            EquipmentDefinition weapon = EquipmentDefinition.Create(
                Id("equipment.weapon-fixture"),
                EquipmentCategoryIds.Weapon,
                Id("equipment-family.weapon-fixture"),
                "Weapon Fixture",
                Id("weapon.blaster-machine-gun"),
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
            AugmentDefinition weaponOnly = AugmentDefinition.Create(
                Id("augment.weapon-only"),
                Id("augment-family.weapon"),
                "Weapon Only",
                AugmentCompatibility.Create(
                    new[] { EquipmentCategoryIds.Weapon },
                    Array.Empty<StableId>(),
                    Array.Empty<StableId>(),
                    Array.Empty<StableId>()),
                Array.Empty<StableId>(),
                AugmentDuplicatePolicy.DisallowSameDefinition,
                InclusiveIntRange.Create(1, 2),
                InclusiveIntRange.Create(1, 3));

            EquipmentCatalogBuildResult build = EquipmentCatalog.Build(
                new[] { weapon, armorBeta, armorAlpha },
                new[] { utility, weaponOnly, power, mobility, guard });
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
