using System;
using NUnit.Framework;
using ShooterMover.Application.Rewards.Strongboxes;
using ShooterMover.Domain.Common.Random;
using ShooterMover.Domain.Rewards.Strongboxes;

namespace ShooterMover.Tests.EditMode.Rewards.Strongboxes
{
    public sealed class StrongboxHybridLootV1Tests
    {
        [Test]
        public void EqualInputsProduceByteEquivalentRolls()
        {
            StrongboxHybridLootPolicyV1 policy =
                ProductionStrongboxHybridLootCatalogV1.GetByTierNumber(8);

            StrongboxTargetLevelRollV1 firstTarget =
                policy.RollTargetLevel(30, 0xA55AUL, 1, 4UL);
            StrongboxTargetLevelRollV1 secondTarget =
                policy.RollTargetLevel(30, 0xA55AUL, 1, 4UL);
            StrongboxInstanceLevelRollV1 firstLevel =
                policy.RollInstanceLevel(
                    firstTarget,
                    firstTarget.TargetLevel + 3,
                    StrongboxDefinitionRarityIdsV1.Epic,
                    0xA55AUL,
                    1,
                    4UL);
            StrongboxInstanceLevelRollV1 secondLevel =
                policy.RollInstanceLevel(
                    secondTarget,
                    secondTarget.TargetLevel + 3,
                    StrongboxDefinitionRarityIdsV1.Epic,
                    0xA55AUL,
                    1,
                    4UL);
            StrongboxAugmentSignatureV1 firstSignature =
                policy.RollAugmentSignature(
                    30,
                    firstLevel.ItemLevel,
                    StrongboxDefinitionRarityIdsV1.Epic,
                    3,
                    4,
                    0xA55AUL,
                    1,
                    4UL);
            StrongboxAugmentSignatureV1 secondSignature =
                policy.RollAugmentSignature(
                    30,
                    secondLevel.ItemLevel,
                    StrongboxDefinitionRarityIdsV1.Epic,
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
            StrongboxHybridLootPolicyV1 tierOne =
                ProductionStrongboxHybridLootCatalogV1.GetByTierNumber(1);
            StrongboxHybridLootPolicyV1 tierEight =
                ProductionStrongboxHybridLootCatalogV1.GetByTierNumber(8);
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
            StrongboxHybridLootPolicyV1 policy =
                ProductionStrongboxHybridLootCatalogV1.GetByTierNumber(4);
            StrongboxTargetLevelRollV1 target =
                policy.RollTargetLevel(30, 77UL, 1, 0UL);

            double centered = policy.EvaluateDefinitionWeight(
                target,
                target.TargetLevel,
                1.0,
                StrongboxDefinitionRarityIdsV1.Common);
            double tail = policy.EvaluateDefinitionWeight(
                target,
                target.TargetLevel + 12,
                1.0,
                StrongboxDefinitionRarityIdsV1.Common);
            double ratio = tail / centered;

            Assert.That(ratio, Is.EqualTo(0.000335).Within(0.0000001));
            Assert.That(policy.EvaluateDefinitionWeight(
                    target,
                    target.TargetLevel + 13,
                    1.0,
                    StrongboxDefinitionRarityIdsV1.Common),
                Is.EqualTo(0.0));
        }

        [Test]
        public void HybridInstanceLevelUsesEightyTwentyCenterThenNearbyVariation()
        {
            StrongboxHybridLootPolicyV1 policy =
                ProductionStrongboxHybridLootCatalogV1.GetByTierNumber(4);
            StrongboxTargetLevelRollV1 target = null;
            ulong ordinal = 0UL;
            for (; ordinal < 4096UL; ordinal++)
            {
                StrongboxTargetLevelRollV1 candidate = policy.RollTargetLevel(
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
            StrongboxInstanceLevelRollV1 item = policy.RollInstanceLevel(
                target,
                19,
                StrongboxDefinitionRarityIdsV1.Legendary,
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
            StrongboxHybridLootPolicyV1 policy =
                ProductionStrongboxHybridLootCatalogV1.GetByTierNumber(4);
            StrongboxTargetLevelRollV1 target =
                policy.RollTargetLevel(30, 77UL, 1, 0UL);

            Assert.Throws<ArgumentOutOfRangeException>(delegate
            {
                policy.RollInstanceLevel(
                    target,
                    target.TargetLevel + 13,
                    StrongboxDefinitionRarityIdsV1.Common,
                    77UL,
                    1,
                    0UL);
            });
        }

        [Test]
        public void TierSevenGuaranteesTwoAndTierEightGuaranteesFullNormalSlots()
        {
            StrongboxHybridLootPolicyV1 tierSeven =
                ProductionStrongboxHybridLootCatalogV1.GetByTierNumber(7);
            StrongboxHybridLootPolicyV1 tierEight =
                ProductionStrongboxHybridLootCatalogV1.GetByTierNumber(8);

            for (ulong ordinal = 0UL; ordinal < 2048UL; ordinal++)
            {
                StrongboxAugmentSignatureV1 seven =
                    tierSeven.RollAugmentSignature(
                        30,
                        30,
                        StrongboxDefinitionRarityIdsV1.Epic,
                        3,
                        4,
                        7007UL,
                        1,
                        ordinal);
                StrongboxAugmentSignatureV1 eight =
                    tierEight.RollAugmentSignature(
                        30,
                        30,
                        StrongboxDefinitionRarityIdsV1.Epic,
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
            StrongboxHybridLootPolicyV1 policy =
                ProductionStrongboxHybridLootCatalogV1.GetByTierNumber(8);

            for (ulong ordinal = 0UL; ordinal < 512UL; ordinal++)
            {
                StrongboxAugmentSignatureV1 signature =
                    policy.RollAugmentSignature(
                        30,
                        30,
                        StrongboxDefinitionRarityIdsV1.Rare,
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
            StrongboxHybridLootPolicyV1 policy =
                ProductionStrongboxHybridLootCatalogV1.GetByTierNumber(8);
            int lowerLevelTen = 0;
            int higherLevelTen = 0;
            const int samples = 4096;

            for (ulong ordinal = 0UL; ordinal < samples; ordinal++)
            {
                StrongboxAugmentSignatureV1 lower =
                    policy.RollAugmentSignature(
                        30,
                        24,
                        StrongboxDefinitionRarityIdsV1.Epic,
                        3,
                        4,
                        0x1010UL,
                        1,
                        ordinal);
                StrongboxAugmentSignatureV1 higher =
                    policy.RollAugmentSignature(
                        30,
                        36,
                        StrongboxDefinitionRarityIdsV1.Epic,
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
            StrongboxHybridLootPolicyV1 tierOne =
                ProductionStrongboxHybridLootCatalogV1.GetByTierNumber(1);
            StrongboxHybridLootPolicyV1 tierEleven =
                ProductionStrongboxHybridLootCatalogV1.GetByTierNumber(11);
            StrongboxTargetLevelRollV1 oneTarget =
                tierOne.RollTargetLevel(50, 1UL, 1, 0UL);
            StrongboxTargetLevelRollV1 elevenTarget =
                tierEleven.RollTargetLevel(50, 11UL, 1, 0UL);

            Assert.That(tierOne.EvaluateDefinitionWeight(
                    oneTarget,
                    oneTarget.TargetLevel,
                    1.0,
                    StrongboxDefinitionRarityIdsV1.Artifact),
                Is.EqualTo(0.0));
            Assert.That(tierEleven.EvaluateDefinitionWeight(
                    elevenTarget,
                    elevenTarget.TargetLevel,
                    1.0,
                    StrongboxDefinitionRarityIdsV1.Artifact),
                Is.GreaterThan(0.0));
        }

        [Test]
        public void TierElevenGuaranteesNormalMaximumAndCanRollBothOvercaps()
        {
            StrongboxHybridLootPolicyV1 policy =
                ProductionStrongboxHybridLootCatalogV1.GetByTierNumber(11);
            bool sawFourthSlot = false;
            bool sawLevelEleven = false;

            for (ulong ordinal = 0UL; ordinal < 2048UL; ordinal++)
            {
                StrongboxAugmentSignatureV1 signature =
                    policy.RollAugmentSignature(
                        50,
                        55,
                        StrongboxDefinitionRarityIdsV1.Artifact,
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
            StrongboxHybridLootPolicyV1 policy =
                ProductionStrongboxHybridLootCatalogV1.GetByTierNumber(1);
            bool sawZero = false;
            bool sawNonZero = false;

            for (ulong ordinal = 0UL; ordinal < 512UL; ordinal++)
            {
                StrongboxAugmentSignatureV1 signature =
                    policy.RollAugmentSignature(
                        7,
                        5,
                        StrongboxDefinitionRarityIdsV1.Common,
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
