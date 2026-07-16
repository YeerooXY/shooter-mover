using System;
using System.Collections.Generic;
using System.Globalization;
using NUnit.Framework;
using ShooterMover.Application.Rewards.Generation;
using ShooterMover.Application.Rewards.Strongboxes;
using ShooterMover.Contracts.Rewards;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Common.Random;
using ShooterMover.Domain.Equipment;
using ShooterMover.Domain.Progression.Context;
using ShooterMover.Domain.Progression.Curves;
using ShooterMover.Domain.Rewards.Generation;
using ShooterMover.Domain.Rewards.Model;
using ShooterMover.Domain.Rewards.Strongboxes;

namespace ShooterMover.Tests.EditMode.StatisticalVerification
{
    public sealed class StrongboxStatisticalVerificationTests
    {
        private const ulong EquipmentSimulationSeed = 0x535441542D303031UL;
        private static readonly StableId TierId = Id("stat.strongbox.tier");
        private static readonly StableId EquipmentPoolId = Id("stat.pool.weapons");
        private static readonly StableId ScrapCurrencyId = Id("currency.scrap");
        private static readonly StableId CommonQualityId = Id("quality.common");
        private static readonly StableId RareQualityId = Id("quality.rare");

        [TestCase(100)]
        [TestCase(1000)]
        public void SeededStrongboxOpenBatchesRemainExactlyReproducible(int openCount)
        {
            EquipmentBatch first = RunEquipmentBatch(openCount, EquipmentSimulationSeed);
            EquipmentBatch replay = RunEquipmentBatch(openCount, EquipmentSimulationSeed);

            Assert.That(first.RejectionCount, Is.Zero);
            Assert.That(replay.RejectionCount, Is.Zero);
            Assert.That(replay.Fingerprint, Is.EqualTo(first.Fingerprint));
            Assert.That(replay.EquipmentCount, Is.EqualTo(first.EquipmentCount));
            Assert.That(replay.DuplicateDefinitionOpenCount, Is.EqualTo(first.DuplicateDefinitionOpenCount));
            Assert.That(replay.UniqueInstanceCount, Is.EqualTo(first.UniqueInstanceCount));
            Assert.That(replay.UnderMeanCount, Is.EqualTo(first.UnderMeanCount));
            Assert.That(replay.OverMeanCount, Is.EqualTo(first.OverMeanCount));
            Assert.That(replay.RareQualityCount, Is.EqualTo(first.RareQualityCount));
        }

        [Test]
        public void StrongboxLevelQualityAndDuplicateDistributionsStayInsideToleranceBands()
        {
            EquipmentBatch batch = RunEquipmentBatch(1000, EquipmentSimulationSeed);

            Assert.That(batch.RejectionCount, Is.Zero);
            Assert.That(batch.EquipmentCount, Is.EqualTo(2000));
            Assert.That(batch.UniqueInstanceCount, Is.EqualTo(batch.EquipmentCount),
                "Definitions may repeat, but every granted equipment instance must retain a unique identity.");
            Assert.That(batch.DuplicateDefinitionOpenCount, Is.GreaterThan(0),
                "Sampling with replacement must produce valid same-definition pairs in a sufficiently large batch.");

            StatisticalVerificationAssertions.Proportion(
                "same-definition strongbox pairs",
                batch.DuplicateDefinitionOpenCount,
                batch.OpenCount,
                0.35,
                0.65);
            StatisticalVerificationAssertions.Proportion(
                "rare quality",
                batch.RareQualityCount,
                batch.EquipmentCount,
                0.15,
                0.35);
            StatisticalVerificationAssertions.Proportion(
                "below-mean item levels",
                batch.UnderMeanCount,
                batch.EquipmentCount,
                0.25,
                0.55);
            StatisticalVerificationAssertions.Proportion(
                "above-mean item levels",
                batch.OverMeanCount,
                batch.EquipmentCount,
                0.25,
                0.55);
            Assert.That(
                StatisticalVerificationAssertions.Mean(
                    batch.TotalDifferenceFromMean,
                    batch.EquipmentCount),
                Is.InRange(-1.0, 1.0));
        }

        [Test]
        public void UnderLevelItemsReceiveMoreAugmentSlotBudgetThanOverLevelItems()
        {
            AugmentCompensationBatch first = RunAugmentCompensationBatch(2000, 0xA961E17UL);
            AugmentCompensationBatch replay = RunAugmentCompensationBatch(2000, 0xA961E17UL);

            Assert.That(replay.Fingerprint, Is.EqualTo(first.Fingerprint));
            Assert.That(first.UnderCount, Is.GreaterThan(400));
            Assert.That(first.OverCount, Is.GreaterThan(400));

            double underMean = StatisticalVerificationAssertions.Mean(
                first.UnderSlotTotal,
                first.UnderCount);
            double overMean = StatisticalVerificationAssertions.Mean(
                first.OverSlotTotal,
                first.OverCount);

            Assert.That(underMean, Is.GreaterThan(overMean + 0.75),
                "Lower item-level rolls should be compensated by a materially larger augment-slot budget.");
            Assert.That(underMean, Is.InRange(2.0, 4.0));
            Assert.That(overMean, Is.InRange(0.0, 2.0));
        }

        [Test]
        public void SoftNominalLevelRequirementAllowsGenerationBelowAndAboveNominalLevel()
        {
            EquipmentCatalog catalog = BuildSoftRequirementCatalog();
            EquipmentGenerationPolicyV1 policy = EquipmentGenerationPolicyV1.Create(
                Id("stat.policy.soft-level"),
                new[]
                {
                    EquipmentGenerationCandidateV1.Create(
                        Id("stat.weapon.soft-level"),
                        0,
                        100,
                        0,
                        100,
                        Array.Empty<StableId>(),
                        50L,
                        InclusiveIntRange.Create(50, 60),
                        1.0,
                        1.0)
                },
                new[] { EquipmentQualityCandidateV1.Create(CommonQualityId, 0L, 1UL) },
                Array.Empty<AugmentGenerationCandidateV1>(),
                0,
                0,
                true,
                new SoftActivationCurveParameters(0.10, 10L, 10L),
                new ObsolescenceCurveParameters(30L, 20.0, 0.25));
            RewardGenerationServiceV1 generator = new RewardGenerationServiceV1();

            for (int index = 0; index < 100; index++)
            {
                EquipmentGenerationResultV1 lower = generator.GenerateEquipment(
                    EquipmentGenerationRequestV1.Create(
                        Id("stat.soft.low.op." + index.ToString("D3", CultureInfo.InvariantCulture)),
                        Id("stat.soft.low.item." + index.ToString("D3", CultureInfo.InvariantCulture)),
                        policy,
                        catalog,
                        Context(45),
                        StatisticalVerificationAssertions.Seed(0x5017UL, index),
                        DeterministicRandom.AlgorithmVersion1));
                EquipmentGenerationResultV1 higher = generator.GenerateEquipment(
                    EquipmentGenerationRequestV1.Create(
                        Id("stat.soft.high.op." + index.ToString("D3", CultureInfo.InvariantCulture)),
                        Id("stat.soft.high.item." + index.ToString("D3", CultureInfo.InvariantCulture)),
                        policy,
                        catalog,
                        Context(55),
                        StatisticalVerificationAssertions.Seed(0x5017UL, index),
                        DeterministicRandom.AlgorithmVersion1));

                Assert.That(lower.Status, Is.EqualTo(RewardGenerationStatusV1.Generated));
                Assert.That(higher.Status, Is.EqualTo(RewardGenerationStatusV1.Generated));
                Assert.That(lower.Equipment.DefinitionId, Is.EqualTo(Id("stat.weapon.soft-level")));
                Assert.That(higher.Equipment.DefinitionId, Is.EqualTo(Id("stat.weapon.soft-level")));
                Assert.That(lower.Equipment.ItemLevel, Is.InRange(50, 60));
                Assert.That(higher.Equipment.ItemLevel, Is.InRange(50, 60));
            }
        }

        private static EquipmentBatch RunEquipmentBatch(int openCount, ulong rootSeed)
        {
            StrongboxEquipmentFixture fixture = BuildStrongboxEquipmentFixture();
            HashSet<StableId> instanceIds = new HashSet<StableId>();
            List<string> fingerprints = new List<string>(openCount * 2);
            long equipmentCount = 0L;
            long duplicateDefinitionOpenCount = 0L;
            long underMeanCount = 0L;
            long equalMeanCount = 0L;
            long overMeanCount = 0L;
            long rareQualityCount = 0L;
            long totalDifferenceFromMean = 0L;
            long rejectionCount = 0L;

            for (int openIndex = 0; openIndex < openCount; openIndex++)
            {
                string suffix = openIndex.ToString("D4", CultureInfo.InvariantCulture);
                ulong seed = StatisticalVerificationAssertions.Seed(rootSeed, openIndex);
                StrongboxInstanceContextV1 context = StrongboxInstanceContextV1.Create(
                    Id("stat.box.instance." + suffix),
                    TierId,
                    seed,
                    DeterministicRandom.AlgorithmVersion1,
                    Context(40),
                    Id("stat.box.source." + suffix),
                    Id("stat.box.provenance." + suffix),
                    fixture.Definition.Fingerprint);
                RewardOperationRequestV1 operation = RewardOperationRequestV1.Create(
                    Id("stat.run." + suffix),
                    context.InstanceStableId,
                    Id("stat.box.operation." + suffix),
                    Id("stat.box.commitment." + suffix),
                    fixture.Definition.BaseRewardProfile.ProfileStableId,
                    fixture.Definition.Fingerprint);
                RewardGrantV1 grant = RewardGrantV1.Create(
                    Id("stat.box.equipment-grant"),
                    RewardGrantKindV1.EquipmentReference,
                    EquipmentPoolId,
                    2L);

                IReadOnlyList<EquipmentInstance> equipment;
                string rejection;
                bool resolved = fixture.Resolver.TryResolve(
                    fixture.Definition,
                    context,
                    operation,
                    grant,
                    out equipment,
                    out rejection);
                if (!resolved)
                {
                    rejectionCount++;
                    fingerprints.Add("rejected:" + rejection);
                    continue;
                }

                Assert.That(equipment, Has.Count.EqualTo(2));
                if (equipment[0].DefinitionId == equipment[1].DefinitionId)
                {
                    duplicateDefinitionOpenCount++;
                }

                for (int itemIndex = 0; itemIndex < equipment.Count; itemIndex++)
                {
                    EquipmentInstance item = equipment[itemIndex];
                    equipmentCount++;
                    Assert.That(instanceIds.Add(item.InstanceId), Is.True,
                        "Duplicate equipment instance identity " + item.InstanceId + ".");
                    fingerprints.Add(item.Fingerprint);
                    int difference = item.ItemLevel - fixture.MeanItemLevel;
                    totalDifferenceFromMean += difference;
                    if (difference < 0)
                    {
                        underMeanCount++;
                    }
                    else if (difference > 0)
                    {
                        overMeanCount++;
                    }
                    else
                    {
                        equalMeanCount++;
                    }

                    if (item.QualityId == RareQualityId)
                    {
                        rareQualityCount++;
                    }
                    else
                    {
                        Assert.That(item.QualityId, Is.EqualTo(CommonQualityId));
                    }
                }
            }

            return new EquipmentBatch(
                openCount,
                equipmentCount,
                duplicateDefinitionOpenCount,
                instanceIds.Count,
                underMeanCount,
                equalMeanCount,
                overMeanCount,
                rareQualityCount,
                totalDifferenceFromMean,
                rejectionCount,
                StatisticalVerificationAssertions.Fingerprint(fingerprints));
        }

        private static AugmentCompensationBatch RunAugmentCompensationBatch(
            int sampleCount,
            ulong rootSeed)
        {
            StrongboxPowerBudgetPolicyV1 policy = StrongboxPowerBudgetPolicyV1.Create(
                5,
                4000,
                0,
                4,
                750);
            List<string> fingerprints = new List<string>(sampleCount);
            long underCount = 0L;
            long underSlotTotal = 0L;
            long overCount = 0L;
            long overSlotTotal = 0L;

            for (int index = 0; index < sampleCount; index++)
            {
                ulong seed = StatisticalVerificationAssertions.Seed(rootSeed, index);
                StrongboxItemLevelRollV1 level = policy.RollItemLevel(
                    40,
                    seed,
                    DeterministicRandom.AlgorithmVersion1,
                    0UL);
                StrongboxEquipmentRollPlanV1 plan = policy.RollAugmentSlots(
                    level,
                    Id("stat.weapon.alpha"),
                    4,
                    seed,
                    DeterministicRandom.AlgorithmVersion1);
                fingerprints.Add(level.Fingerprint + "|" + plan.Fingerprint);

                if (level.DifferenceFromMean < 0)
                {
                    underCount++;
                    underSlotTotal += plan.RolledAugmentSlots;
                }
                else if (level.DifferenceFromMean > 0)
                {
                    overCount++;
                    overSlotTotal += plan.RolledAugmentSlots;
                }
            }

            return new AugmentCompensationBatch(
                underCount,
                underSlotTotal,
                overCount,
                overSlotTotal,
                StatisticalVerificationAssertions.Fingerprint(fingerprints));
        }

        private static StrongboxEquipmentFixture BuildStrongboxEquipmentFixture()
        {
            EquipmentCatalog catalog = BuildStrongboxCatalog();
            EquipmentGenerationPolicyV1 generationPolicy = EquipmentGenerationPolicyV1.Create(
                Id("stat.policy.strongbox-equipment"),
                new[]
                {
                    EquipmentCandidate("stat.weapon.alpha"),
                    EquipmentCandidate("stat.weapon.beta")
                },
                new[]
                {
                    EquipmentQualityCandidateV1.Create(CommonQualityId, 0L, 3UL),
                    EquipmentQualityCandidateV1.Create(RareQualityId, 0L, 1UL)
                },
                new[]
                {
                    AugmentCandidate("stat.augment.power"),
                    AugmentCandidate("stat.augment.speed"),
                    AugmentCandidate("stat.augment.guard"),
                    AugmentCandidate("stat.augment.utility")
                },
                0,
                4,
                false,
                new SoftActivationCurveParameters(0.10, 8L, 8L),
                new ObsolescenceCurveParameters(30L, 20.0, 0.25));
            StrongboxPowerBudgetPolicyV1 powerBudget = StrongboxPowerBudgetPolicyV1.Create(
                5,
                4000,
                0,
                4,
                750);

            RewardGrantSpecificationV1 equipmentGrant = RewardGrantSpecificationV1.CreateFixed(
                Id("stat.box.equipment-grant-spec"),
                RewardGrantKindV1.EquipmentReference,
                EquipmentPoolId,
                2L);
            RewardProfileV1 profile = RewardProfileV1.Create(
                Id("stat.profile.strongbox-equipment"),
                new[] { equipmentGrant },
                Array.Empty<IndependentRewardRollV1>(),
                Array.Empty<ExclusiveRewardGroupV1>());
            StrongboxDefinitionV1 definition = StrongboxDefinitionV1.Create(
                TierId,
                0,
                1L,
                1L,
                0L,
                StrongboxRewardCountPolicyV1.Create(2, 2),
                StrongboxMandatoryScrapPolicyV1.Create(ScrapCurrencyId, 1L, 1L),
                generationPolicy.PolicyId,
                profile,
                Id("stat.scaling.source-tier"),
                Id("stat.scaling.exceptional"));
            StrongboxEquipmentGenerationDefinitionCatalogV1 provider =
                new StrongboxEquipmentGenerationDefinitionCatalogV1(
                    new[]
                    {
                        new StrongboxEquipmentGenerationDefinitionV1(
                            TierId,
                            powerBudget,
                            generationPolicy,
                            catalog)
                    });

            return new StrongboxEquipmentFixture(
                definition,
                new StrongboxEquipmentGenerationResolverV1(
                    new RewardGenerationServiceV1(),
                    provider),
                45);
        }

        private static EquipmentCatalog BuildStrongboxCatalog()
        {
            EquipmentQualityTier common = EquipmentQualityTier.Create(CommonQualityId, "Common", 1);
            EquipmentQualityTier rare = EquipmentQualityTier.Create(RareQualityId, "Rare", 2);
            EquipmentDefinition alpha = EquipmentDefinition.Create(
                Id("stat.weapon.alpha"),
                EquipmentCategoryIds.Weapon,
                Id("stat.weapon-family.alpha"),
                "Stat Weapon Alpha",
                Id("stat.weapon-runtime.alpha"),
                InclusiveIntRange.Create(1, 100),
                4,
                new[] { common, rare },
                Array.Empty<StableId>());
            EquipmentDefinition beta = EquipmentDefinition.Create(
                Id("stat.weapon.beta"),
                EquipmentCategoryIds.Weapon,
                Id("stat.weapon-family.beta"),
                "Stat Weapon Beta",
                Id("stat.weapon-runtime.beta"),
                InclusiveIntRange.Create(1, 100),
                4,
                new[] { common, rare },
                Array.Empty<StableId>());
            AugmentCompatibility compatibility = AugmentCompatibility.Create(
                new[] { EquipmentCategoryIds.Weapon },
                Array.Empty<StableId>(),
                Array.Empty<StableId>(),
                Array.Empty<StableId>());
            EquipmentCatalogBuildResult build = EquipmentCatalog.Build(
                new[] { alpha, beta },
                new[]
                {
                    Augment("stat.augment.power", compatibility),
                    Augment("stat.augment.speed", compatibility),
                    Augment("stat.augment.guard", compatibility),
                    Augment("stat.augment.utility", compatibility)
                });
            Assert.That(build.IsValid, Is.True);
            return build.Catalog;
        }

        private static EquipmentCatalog BuildSoftRequirementCatalog()
        {
            EquipmentQualityTier common = EquipmentQualityTier.Create(CommonQualityId, "Common", 1);
            EquipmentDefinition weapon = EquipmentDefinition.Create(
                Id("stat.weapon.soft-level"),
                EquipmentCategoryIds.Weapon,
                Id("stat.weapon-family.soft-level"),
                "Soft Level Weapon",
                Id("stat.weapon-runtime.soft-level"),
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

        private static EquipmentGenerationCandidateV1 EquipmentCandidate(string id)
        {
            return EquipmentGenerationCandidateV1.Create(
                Id(id),
                0,
                100,
                0,
                100,
                Array.Empty<StableId>(),
                0L,
                InclusiveIntRange.Create(1, 100),
                1.0,
                1.0);
        }

        private static AugmentGenerationCandidateV1 AugmentCandidate(string id)
        {
            return AugmentGenerationCandidateV1.Create(Id(id), 0, 100, 1UL);
        }

        private static AugmentDefinition Augment(string id, AugmentCompatibility compatibility)
        {
            return AugmentDefinition.Create(
                Id(id),
                Id(id + ".family"),
                id,
                compatibility,
                Array.Empty<StableId>(),
                AugmentDuplicatePolicy.DisallowSameDefinition,
                InclusiveIntRange.Create(1, 3),
                InclusiveIntRange.Create(1, 10));
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

        private static StableId Id(string value)
        {
            return StableId.Parse(value);
        }

        private sealed class StrongboxEquipmentFixture
        {
            public StrongboxEquipmentFixture(
                StrongboxDefinitionV1 definition,
                StrongboxEquipmentGenerationResolverV1 resolver,
                int meanItemLevel)
            {
                Definition = definition;
                Resolver = resolver;
                MeanItemLevel = meanItemLevel;
            }

            public StrongboxDefinitionV1 Definition { get; }
            public StrongboxEquipmentGenerationResolverV1 Resolver { get; }
            public int MeanItemLevel { get; }
        }

        private sealed class EquipmentBatch
        {
            public EquipmentBatch(
                int openCount,
                long equipmentCount,
                long duplicateDefinitionOpenCount,
                int uniqueInstanceCount,
                long underMeanCount,
                long equalMeanCount,
                long overMeanCount,
                long rareQualityCount,
                long totalDifferenceFromMean,
                long rejectionCount,
                string fingerprint)
            {
                OpenCount = openCount;
                EquipmentCount = equipmentCount;
                DuplicateDefinitionOpenCount = duplicateDefinitionOpenCount;
                UniqueInstanceCount = uniqueInstanceCount;
                UnderMeanCount = underMeanCount;
                EqualMeanCount = equalMeanCount;
                OverMeanCount = overMeanCount;
                RareQualityCount = rareQualityCount;
                TotalDifferenceFromMean = totalDifferenceFromMean;
                RejectionCount = rejectionCount;
                Fingerprint = fingerprint;
            }

            public int OpenCount { get; }
            public long EquipmentCount { get; }
            public long DuplicateDefinitionOpenCount { get; }
            public int UniqueInstanceCount { get; }
            public long UnderMeanCount { get; }
            public long EqualMeanCount { get; }
            public long OverMeanCount { get; }
            public long RareQualityCount { get; }
            public long TotalDifferenceFromMean { get; }
            public long RejectionCount { get; }
            public string Fingerprint { get; }
        }

        private sealed class AugmentCompensationBatch
        {
            public AugmentCompensationBatch(
                long underCount,
                long underSlotTotal,
                long overCount,
                long overSlotTotal,
                string fingerprint)
            {
                UnderCount = underCount;
                UnderSlotTotal = underSlotTotal;
                OverCount = overCount;
                OverSlotTotal = overSlotTotal;
                Fingerprint = fingerprint;
            }

            public long UnderCount { get; }
            public long UnderSlotTotal { get; }
            public long OverCount { get; }
            public long OverSlotTotal { get; }
            public string Fingerprint { get; }
        }
    }
}
