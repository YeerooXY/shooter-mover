using System;
using NUnit.Framework;
using ShooterMover.Application.Rewards.Strongboxes;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Common.Random;
using ShooterMover.Domain.Rewards.Strongboxes;

namespace ShooterMover.Tests.EditMode.Rewards.Strongboxes
{
    public sealed class StrongboxHybridLootTests
    {
        [Test]
        public void EqualInputsProduceByteEquivalentRolls()
        {
            StrongboxHybridLootPolicy policy =
                StrongboxHybridLootCatalog.GetByTierNumber(8);

            StrongboxTargetLevelRoll firstTarget =
                policy.RollTargetLevel(30, 0xA55AUL, 1, 4UL);
            StrongboxTargetLevelRoll secondTarget =
                policy.RollTargetLevel(30, 0xA55AUL, 1, 4UL);
            StrongboxInstanceLevelRoll firstLevel =
                policy.RollInstanceLevel(
                    firstTarget,
                    firstTarget.TargetLevel + 3,
                    StrongboxDefinitionRarityIds.Epic,
                    0xA55AUL,
                    1,
                    4UL);
            StrongboxInstanceLevelRoll secondLevel =
                policy.RollInstanceLevel(
                    secondTarget,
                    secondTarget.TargetLevel + 3,
                    StrongboxDefinitionRarityIds.Epic,
                    0xA55AUL,
                    1,
                    4UL);
            StrongboxAugmentSignature firstSignature =
                policy.RollAugmentSignature(
                    30,
                    firstLevel.ItemLevel,
                    StrongboxDefinitionRarityIds.Epic,
                    3,
                    4,
                    0xA55AUL,
                    1,
                    4UL);
            StrongboxAugmentSignature secondSignature =
                policy.RollAugmentSignature(
                    30,
                    secondLevel.ItemLevel,
                    StrongboxDefinitionRarityIds.Epic,
                    3,
                    4,
                    0xA55AUL,
                    1,
                    4UL);

            Assert.That(secondTarget.Fingerprint, Is.EqualTo(firstTarget.Fingerprint));
            Assert.That(secondLevel.Fingerprint, Is.EqualTo(firstLevel.Fingerprint));
            Assert.That(secondSignature.Fingerprint, Is.EqualTo(firstSignature.Fingerprint));
            Assert.That(secondSignature.ToCanonicalString(),
                Is.EqualTo(firstSignature.ToCanonicalString()));
        }

        [Test]
        public void TierOneStaysNearPlayerAndHigherBoxesLeadProgressively()
        {
            StrongboxHybridLootPolicy tierOne =
                StrongboxHybridLootCatalog.GetByTierNumber(1);
            StrongboxHybridLootPolicy tierEight =
                StrongboxHybridLootCatalog.GetByTierNumber(8);
            StrongboxHybridLootPolicy tierEleven =
                StrongboxHybridLootCatalog.GetByTierNumber(11);

            Assert.That(tierOne.MinimumTargetDelta, Is.EqualTo(0));
            Assert.That(tierOne.MostLikelyTargetDelta, Is.EqualTo(0));
            Assert.That(tierOne.MaximumTargetDelta, Is.EqualTo(1));
            Assert.That(tierEight.MinimumTargetDelta, Is.EqualTo(4));
            Assert.That(tierEight.MostLikelyTargetDelta, Is.EqualTo(6));
            Assert.That(tierEight.MaximumTargetDelta, Is.EqualTo(8));
            Assert.That(tierEleven.MinimumTargetDelta, Is.EqualTo(8));
            Assert.That(tierEleven.MostLikelyTargetDelta, Is.EqualTo(10));
            Assert.That(tierEleven.MaximumTargetDelta, Is.EqualTo(12));

            long tierOneTotal = 0L;
            long tierEightTotal = 0L;
            const int samples = 4096;
            for (ulong ordinal = 0UL; ordinal < samples; ordinal++)
            {
                tierOneTotal += tierOne.RollTargetLevel(
                    50,
                    0x1001UL,
                    DeterministicRandom.AlgorithmVersion1,
                    ordinal).TargetLevel;
                tierEightTotal += tierEight.RollTargetLevel(
                    50,
                    0x8008UL,
                    DeterministicRandom.AlgorithmVersion1,
                    ordinal).TargetLevel;
            }

            double tierOneAverage = tierOneTotal / (double)samples;
            double tierEightAverage = tierEightTotal / (double)samples;
            Assert.That(tierOneAverage, Is.InRange(50.0, 51.0));
            Assert.That(tierEightAverage, Is.InRange(55.0, 57.0));
            Assert.That(tierEightAverage, Is.GreaterThan(tierOneAverage + 5.0));
        }

        [Test]
        public void TwelveLevelTailHasPointZeroThreeThreeFivePercentRelativeAffinity()
        {
            StrongboxHybridLootPolicy policy =
                StrongboxHybridLootCatalog.GetByTierNumber(4);
            StrongboxTargetLevelRoll target =
                policy.RollTargetLevel(30, 77UL, 1, 0UL);

            double centered = policy.EvaluateDefinitionAffinity(
                target,
                target.TargetLevel,
                1.0);
            double tail = policy.EvaluateDefinitionAffinity(
                target,
                target.TargetLevel + 12,
                1.0);
            double ratio = tail / centered;

            Assert.That(ratio, Is.EqualTo(0.000335).Within(0.0000001));
            Assert.That(policy.EvaluateDefinitionAffinity(
                    target,
                    target.TargetLevel + 13,
                    1.0),
                Is.EqualTo(0.0));
        }

        [Test]
        public void HybridInstanceLevelUsesEightyTwentyCenterThenNearbyVariation()
        {
            StrongboxHybridLootPolicy policy =
                StrongboxHybridLootCatalog.GetByTierNumber(4);
            StrongboxTargetLevelRoll target =
                policy.RollTargetLevel(7, 123456UL, 1, 0UL);
            int definitionPeakLevel = target.TargetLevel + 12;

            StrongboxInstanceLevelRoll item = policy.RollInstanceLevel(
                target,
                definitionPeakLevel,
                StrongboxDefinitionRarityIds.Legendary,
                123456UL,
                1,
                0UL);

            Assert.That(
                item.HybridCenterLevel,
                Is.EqualTo(target.TargetLevel + 2));
            Assert.That(item.VariationOffset, Is.InRange(-4, 4));
            Assert.That(
                item.ItemLevel,
                Is.InRange(
                    item.HybridCenterLevel - 4,
                    item.HybridCenterLevel + 4));
            Assert.That(item.DefinitionDistanceFromTarget, Is.EqualTo(12));
        }

        [Test]
        public void InstanceLevelRejectsDefinitionOutsideSelectionRadius()
        {
            StrongboxHybridLootPolicy policy =
                StrongboxHybridLootCatalog.GetByTierNumber(4);
            StrongboxTargetLevelRoll target =
                policy.RollTargetLevel(30, 77UL, 1, 0UL);

            Assert.Throws<ArgumentOutOfRangeException>(delegate
            {
                policy.RollInstanceLevel(
                    target,
                    target.TargetLevel + 13,
                    StrongboxDefinitionRarityIds.Common,
                    77UL,
                    1,
                    0UL);
            });
        }

        [Test]
        public void RarityWeightsAreDirectFiveBandOdds()
        {
            AssertRarityWeights(1, 80000, 16000, 3500, 500, 0);
            AssertRarityWeights(5, 45000, 31000, 18000, 5999, 1);
            AssertRarityWeights(8, 20000, 28000, 31000, 20950, 50);
            AssertRarityWeights(10, 7000, 15000, 30000, 47000, 1000);
            AssertRarityWeights(11, 2000, 7000, 24000, 64500, 2500);
        }

        [Test]
        public void ArtifactSelectionUsesAJackpotLadder()
        {
            int[] expected = { 0, 0, 0, 0, 1, 5, 20, 50, 250, 1000, 2500 };
            for (int tier = 1; tier <= expected.Length; tier++)
            {
                Assert.That(
                    StrongboxHybridLootCatalog.GetByTierNumber(tier)
                        .GetRaritySelectionWeight(
                            StrongboxDefinitionRarityIds.Artifact),
                    Is.EqualTo(expected[tier - 1]),
                    "Tier " + tier);
            }
        }

        [Test]
        public void TierSevenGuaranteesTwoAndTierEightGuaranteesFullNormalSlots()
        {
            StrongboxHybridLootPolicy tierSeven =
                StrongboxHybridLootCatalog.GetByTierNumber(7);
            StrongboxHybridLootPolicy tierEight =
                StrongboxHybridLootCatalog.GetByTierNumber(8);

            for (ulong ordinal = 0UL; ordinal < 2048UL; ordinal++)
            {
                StrongboxAugmentSignature seven =
                    tierSeven.RollAugmentSignature(
                        30,
                        30,
                        StrongboxDefinitionRarityIds.Epic,
                        3,
                        4,
                        7007UL,
                        1,
                        ordinal);
                StrongboxAugmentSignature eight =
                    tierEight.RollAugmentSignature(
                        30,
                        30,
                        StrongboxDefinitionRarityIds.Epic,
                        3,
                        4,
                        8008UL,
                        1,
                        ordinal);

                Assert.That(seven.SlotCount, Is.InRange(2, 3));
                Assert.That(seven.SharedLevel, Is.InRange(6, 10));
                Assert.That(eight.SlotCount, Is.EqualTo(3));
                Assert.That(eight.SharedLevel, Is.InRange(6, 10));
            }
        }

        [Test]
        public void TierEightMapsFullGunOutcomeToFullTwoSlotGear()
        {
            StrongboxHybridLootPolicy policy =
                StrongboxHybridLootCatalog.GetByTierNumber(8);

            for (ulong ordinal = 0UL; ordinal < 512UL; ordinal++)
            {
                StrongboxAugmentSignature signature =
                    policy.RollAugmentSignature(
                        30,
                        30,
                        StrongboxDefinitionRarityIds.Rare,
                        2,
                        3,
                        8800UL,
                        1,
                        ordinal);
                Assert.That(signature.SlotCount, Is.EqualTo(2));
                Assert.That(signature.HasOvercapSlot, Is.False);
            }
        }

        [Test]
        public void LowerLevelItemsTiltTowardLevelTenWithoutRemovingHighItemJackpots()
        {
            StrongboxHybridLootPolicy policy =
                StrongboxHybridLootCatalog.GetByTierNumber(8);
            int lowerLevelTen = 0;
            int higherLevelTen = 0;
            const int samples = 4096;

            for (ulong ordinal = 0UL; ordinal < samples; ordinal++)
            {
                StrongboxAugmentSignature lower =
                    policy.RollAugmentSignature(
                        30,
                        24,
                        StrongboxDefinitionRarityIds.Epic,
                        3,
                        4,
                        0x1010UL,
                        1,
                        ordinal);
                StrongboxAugmentSignature higher =
                    policy.RollAugmentSignature(
                        30,
                        36,
                        StrongboxDefinitionRarityIds.Epic,
                        3,
                        4,
                        0x2020UL,
                        1,
                        ordinal);
                if (lower.SharedLevel == 10) lowerLevelTen++;
                if (higher.SharedLevel == 10) higherLevelTen++;
            }

            Assert.That(lowerLevelTen, Is.GreaterThan(higherLevelTen));
            Assert.That(higherLevelTen, Is.GreaterThan(0));
        }

        [Test]
        public void ArtifactRarityIsSupportedButGatedByTierProfile()
        {
            StrongboxHybridLootPolicy tierOne =
                StrongboxHybridLootCatalog.GetByTierNumber(1);
            StrongboxHybridLootPolicy tierEleven =
                StrongboxHybridLootCatalog.GetByTierNumber(11);
            StrongboxTargetLevelRoll oneTarget =
                tierOne.RollTargetLevel(50, 1UL, 1, 0UL);
            StrongboxTargetLevelRoll elevenTarget =
                tierEleven.RollTargetLevel(50, 11UL, 1, 0UL);

            Assert.That(tierOne.EvaluateDefinitionWeight(
                    oneTarget,
                    oneTarget.TargetLevel,
                    1.0,
                    StrongboxDefinitionRarityIds.Artifact),
                Is.EqualTo(0.0));
            Assert.That(tierEleven.EvaluateDefinitionWeight(
                    elevenTarget,
                    elevenTarget.TargetLevel,
                    1.0,
                    StrongboxDefinitionRarityIds.Artifact),
                Is.GreaterThan(0.0));
        }

        [Test]
        public void TierElevenAuthorsHalfPercentLevelTwelveAndPointOneFiveCombinedJackpot()
        {
            StrongboxHybridLootPolicy policy =
                StrongboxHybridLootCatalog.GetByTierNumber(11);
            ulong levelTotal = OutcomeTotal(policy.AugmentLevelOutcomes);
            ulong slotTotal = OutcomeTotal(policy.AugmentSlotOutcomes);
            ulong levelTwelve = OutcomeWeight(policy.AugmentLevelOutcomes, 12);
            ulong fourSlots = OutcomeWeight(policy.AugmentSlotOutcomes, 4);

            Assert.That(levelTotal, Is.EqualTo(2000UL));
            Assert.That(levelTwelve, Is.EqualTo(10UL));
            Assert.That(100.0 * levelTwelve / levelTotal,
                Is.EqualTo(0.5).Within(0.0000001));
            Assert.That(100.0 * fourSlots / slotTotal,
                Is.EqualTo(30.0).Within(0.0000001));
            Assert.That(
                100.0 * levelTwelve * fourSlots / (levelTotal * slotTotal),
                Is.EqualTo(0.15).Within(0.0000001));
        }

        [Test]
        public void TierElevenGuaranteesNormalMaximumAndCanRollOvercaps()
        {
            StrongboxHybridLootPolicy policy =
                StrongboxHybridLootCatalog.GetByTierNumber(11);
            bool sawFourthSlot = false;
            bool sawLevelElevenOrTwelve = false;

            for (ulong ordinal = 0UL; ordinal < 4096UL; ordinal++)
            {
                StrongboxAugmentSignature signature =
                    policy.RollAugmentSignature(
                        50,
                        55,
                        StrongboxDefinitionRarityIds.Artifact,
                        3,
                        4,
                        0x1111UL,
                        1,
                        ordinal);
                Assert.That(signature.SlotCount, Is.InRange(3, 4));
                Assert.That(signature.SharedLevel, Is.InRange(10, 12));
                sawFourthSlot |= signature.SlotCount == 4;
                sawLevelElevenOrTwelve |= signature.SharedLevel >= 11;
            }

            Assert.That(sawFourthSlot, Is.True);
            Assert.That(sawLevelElevenOrTwelve, Is.True);
        }

        [Test]
        public void EarlyBoxesCanStillProduceZeroCapacity()
        {
            StrongboxHybridLootPolicy policy =
                StrongboxHybridLootCatalog.GetByTierNumber(1);
            bool sawZero = false;
            bool sawNonZero = false;

            for (ulong ordinal = 0UL; ordinal < 512UL; ordinal++)
            {
                StrongboxAugmentSignature signature =
                    policy.RollAugmentSignature(
                        7,
                        5,
                        StrongboxDefinitionRarityIds.Common,
                        3,
                        4,
                        100UL,
                        1,
                        ordinal);
                sawZero |= signature.SlotCount == 0
                    && signature.SharedLevel == 0;
                sawNonZero |= signature.SlotCount > 0
                    && signature.SharedLevel > 0;
            }

            Assert.That(sawZero, Is.True);
            Assert.That(sawNonZero, Is.True);
        }

        private static void AssertRarityWeights(
            int tier,
            int common,
            int rare,
            int epic,
            int legendary,
            int artifact)
        {
            StrongboxHybridLootPolicy policy =
                StrongboxHybridLootCatalog.GetByTierNumber(tier);
            Assert.That(policy.GetRaritySelectionWeight(
                    StrongboxDefinitionRarityIds.Common),
                Is.EqualTo(common));
            Assert.That(policy.GetRaritySelectionWeight(
                    StrongboxDefinitionRarityIds.Rare),
                Is.EqualTo(rare));
            Assert.That(policy.GetRaritySelectionWeight(
                    StrongboxDefinitionRarityIds.Epic),
                Is.EqualTo(epic));
            Assert.That(policy.GetRaritySelectionWeight(
                    StrongboxDefinitionRarityIds.Legendary),
                Is.EqualTo(legendary));
            Assert.That(policy.GetRaritySelectionWeight(
                    StrongboxDefinitionRarityIds.Artifact),
                Is.EqualTo(artifact));
            Assert.That(common + rare + epic + legendary + artifact,
                Is.EqualTo(100000));
        }

        private static ulong OutcomeTotal(
            System.Collections.Generic.IReadOnlyList<StrongboxWeightedIntOutcome> outcomes)
        {
            ulong total = 0UL;
            for (int index = 0; index < outcomes.Count; index++)
            {
                total += outcomes[index].Weight;
            }
            return total;
        }

        private static ulong OutcomeWeight(
            System.Collections.Generic.IReadOnlyList<StrongboxWeightedIntOutcome> outcomes,
            int value)
        {
            for (int index = 0; index < outcomes.Count; index++)
            {
                if (outcomes[index].Value == value)
                {
                    return outcomes[index].Weight;
                }
            }
            return 0UL;
        }
    }
}
