using System;
using NUnit.Framework;
using ShooterMover.Application.Rewards.Strongboxes;
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
        public void LowBoxesTrailAndPremiumBoxesLeadPlayerLevelOnAverage()
        {
            StrongboxHybridLootPolicy tierOne =
                StrongboxHybridLootCatalog.GetByTierNumber(1);
            StrongboxHybridLootPolicy tierEight =
                StrongboxHybridLootCatalog.GetByTierNumber(8);
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
            Assert.That(tierOneAverage, Is.LessThan(47.0));
            Assert.That(tierEightAverage, Is.GreaterThan(51.0));
            Assert.That(tierEightAverage, Is.GreaterThan(tierOneAverage + 5.0));
        }

        [Test]
        public void TwelveLevelTailHasPointZeroThreeThreeFivePercentRelativeAffinity()
        {
            StrongboxHybridLootPolicy policy =
                StrongboxHybridLootCatalog.GetByTierNumber(4);
            StrongboxTargetLevelRoll target =
                policy.RollTargetLevel(30, 77UL, 1, 0UL);

            double centered = policy.EvaluateDefinitionWeight(
                target,
                target.TargetLevel,
                1.0,
                StrongboxDefinitionRarityIds.Common);
            double tail = policy.EvaluateDefinitionWeight(
                target,
                target.TargetLevel + 12,
                1.0,
                StrongboxDefinitionRarityIds.Common);
            double ratio = tail / centered;

            Assert.That(ratio, Is.EqualTo(0.000335).Within(0.0000001));
            Assert.That(policy.EvaluateDefinitionWeight(
                    target,
                    target.TargetLevel + 13,
                    1.0,
                    StrongboxDefinitionRarityIds.Common),
                Is.EqualTo(0.0));
        }

        [Test]
        public void HybridInstanceLevelUsesEightyTwentyCenterThenNearbyVariation()
        {
            StrongboxHybridLootPolicy policy =
                StrongboxHybridLootCatalog.GetByTierNumber(4);
            StrongboxTargetLevelRoll target = null;
            ulong ordinal = 0UL;
            for (; ordinal < 4096UL; ordinal++)
            {
                StrongboxTargetLevelRoll candidate = policy.RollTargetLevel(
                    7,
                    123456UL,
                    1,
                    ordinal);
                if (candidate.TargetLevel == 7)
                {
                    target = candidate;
                    break;
                }
            }

            Assert.That(target, Is.Not.Null);
            StrongboxInstanceLevelRoll item = policy.RollInstanceLevel(
                target,
                19,
                StrongboxDefinitionRarityIds.Legendary,
                123456UL,
                1,
                ordinal);

            Assert.That(item.HybridCenterLevel, Is.EqualTo(9));
            Assert.That(item.VariationOffset, Is.InRange(-4, 4));
            Assert.That(item.ItemLevel, Is.InRange(5, 13));
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
        public void TierEightMapsFullWeaponOutcomeToFullTwoSlotGear()
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
        public void TierElevenGuaranteesNormalMaximumAndCanRollBothOvercaps()
        {
            StrongboxHybridLootPolicy policy =
                StrongboxHybridLootCatalog.GetByTierNumber(11);
            bool sawFourthSlot = false;
            bool sawLevelEleven = false;

            for (ulong ordinal = 0UL; ordinal < 2048UL; ordinal++)
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
                Assert.That(signature.SharedLevel, Is.InRange(10, 11));
                sawFourthSlot |= signature.SlotCount == 4;
                sawLevelEleven |= signature.SharedLevel == 11;
            }

            Assert.That(sawFourthSlot, Is.True);
            Assert.That(sawLevelEleven, Is.True);
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
    }
}
