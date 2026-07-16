using System;
using System.Collections.Generic;
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

namespace ShooterMover.Tests.EditMode.Rewards.Strongboxes
{
    public sealed class StrongboxPowerBudgetV1Tests
    {
        [Test]
        public void MeanIsPlayerLevelPlusTierBonusAndRangeIsPlusMinusTwelve()
        {
            StrongboxPowerBudgetPolicyV1 policy = Policy(5, 4000, 0, 10, 500);

            StrongboxItemLevelRollV1 roll = policy.RollItemLevel(40, 123UL, 1, 0UL);

            Assert.That(roll.MeanItemLevel, Is.EqualTo(45));
            Assert.That(roll.MinimumItemLevel, Is.EqualTo(33));
            Assert.That(roll.MaximumItemLevel, Is.EqualTo(57));
            Assert.That(roll.TargetItemLevel, Is.InRange(33, 57));
        }

        [Test]
        public void EqualInputsProduceEqualPowerBudgetPlan()
        {
            StrongboxPowerBudgetPolicyV1 policy = Policy(3, 4000, 0, 10, 500);

            StrongboxItemLevelRollV1 firstLevel = policy.RollItemLevel(20, 0xA55AUL, 1, 4UL);
            StrongboxItemLevelRollV1 secondLevel = policy.RollItemLevel(20, 0xA55AUL, 1, 4UL);
            StrongboxEquipmentRollPlanV1 first = policy.RollAugmentSlots(
                firstLevel, Id("equipment.test"), 10, 0xA55AUL, 1);
            StrongboxEquipmentRollPlanV1 second = policy.RollAugmentSlots(
                secondLevel, Id("equipment.test"), 10, 0xA55AUL, 1);

            Assert.That(second.Fingerprint, Is.EqualTo(first.Fingerprint));
            Assert.That(second.TargetItemLevel, Is.EqualTo(first.TargetItemLevel));
            Assert.That(second.RolledAugmentSlots, Is.EqualTo(first.RolledAugmentSlots));
        }

        [Test]
        public void EveryItemLevelRollRemainsWithinTwelveLevelsOfMean()
        {
            StrongboxPowerBudgetPolicyV1 policy = Policy(7, 9000, 0, 10, 1000);

            for (ulong ordinal = 0UL; ordinal < 1000UL; ordinal++)
            {
                StrongboxItemLevelRollV1 roll = policy.RollItemLevel(50, 987654321UL, 1, ordinal);
                Assert.That(Math.Abs(roll.TargetItemLevel - roll.MeanItemLevel),
                    Is.LessThanOrEqualTo(StrongboxPowerBudgetPolicyV1.MaximumLevelDeviationV1));
            }
        }

        [Test]
        public void UnderLeveledTargetsHaveGreaterExpectedSlotBudgetThanOverLeveledTargets()
        {
            StrongboxPowerBudgetPolicyV1 policy = Policy(0, 4000, 0, 10, 0);
            long underBudget = FindExpectedBudget(policy, true);
            long overBudget = FindExpectedBudget(policy, false);

            Assert.That(underBudget, Is.GreaterThan(overBudget));
            Assert.That(underBudget, Is.GreaterThanOrEqualTo(5000L));
            Assert.That(overBudget, Is.LessThanOrEqualTo(5000L));
        }

        [Test]
        public void AugmentBudgetIsCappedBySelectedEquipmentCapacity()
        {
            StrongboxPowerBudgetPolicyV1 policy = Policy(0, 4000, 0, 10, 0);
            StrongboxItemLevelRollV1 itemLevel = null;
            for (ulong ordinal = 0UL; ordinal < 10000UL; ordinal++)
            {
                StrongboxItemLevelRollV1 candidate = policy.RollItemLevel(50, 88UL, 1, ordinal);
                if (candidate.DifferenceFromMean < 0)
                {
                    itemLevel = candidate;
                    break;
                }
            }

            Assert.That(itemLevel, Is.Not.Null);
            StrongboxEquipmentRollPlanV1 plan = policy.RollAugmentSlots(
                itemLevel,
                Id("equipment.capacity-two"),
                2,
                88UL,
                1);

            Assert.That(plan.EffectiveMaximumAugmentSlots, Is.EqualTo(2));
            Assert.That(plan.RolledAugmentSlots, Is.InRange(0, 2));
        }

        [Test]
        public void IndependentSlotsMayProduceSameDefinitionWithUniqueInstances()
        {
            StableId tierId = Id("strongbox.tier-duplicate-test");
            StableId weaponId = Id("equipment.weapon-only-candidate");
            StrongboxPowerBudgetPolicyV1 powerBudget = Policy(5, 1000, 0, 0, 0);
            EquipmentCatalog catalog = BuildSingleWeaponCatalog(weaponId);
            EquipmentGenerationPolicyV1 equipmentPolicy = EquipmentGenerationPolicyV1.Create(
                Id("generation-policy.duplicate-test"),
                new[]
                {
                    EquipmentGenerationCandidateV1.Create(
                        weaponId,
                        25,
                        100,
                        0,
                        100,
                        Array.Empty<StableId>(),
                        0L,
                        InclusiveIntRange.Create(1, 100),
                        1.0,
                        1.0)
                },
                new[] { EquipmentQualityCandidateV1.Create(Id("quality.common"), 0L, 1UL) },
                Array.Empty<AugmentGenerationCandidateV1>(),
                0,
                0,
                true,
                new SoftActivationCurveParameters(0.1, 5L, 5L),
                new ObsolescenceCurveParameters(25L, 15.0, 0.2));
            StrongboxDefinitionV1 strongboxDefinition = CreateStrongboxDefinition(
                tierId,
                equipmentPolicy.PolicyId,
                weaponId,
                2L);
            StrongboxEquipmentGenerationDefinitionCatalogV1 provider =
                new StrongboxEquipmentGenerationDefinitionCatalogV1(
                    new[]
                    {
                        new StrongboxEquipmentGenerationDefinitionV1(
                            tierId,
                            powerBudget,
                            equipmentPolicy,
                            catalog)
                    });
            StrongboxEquipmentGenerationResolverV1 resolver =
                new StrongboxEquipmentGenerationResolverV1(
                    new RewardGenerationServiceV1(),
                    provider);
            StrongboxInstanceContextV1 context = StrongboxInstanceContextV1.Create(
                Id("strongbox.instance-duplicate-test"),
                tierId,
                1234UL,
                DeterministicRandom.AlgorithmVersion1,
                ProgressionContext.Create(20, 1, Id("difficulty.normal"), 1),
                Id("source.test"),
                Id("provenance.test"));
            RewardOperationRequestV1 operation = RewardOperationRequestV1.Create(
                Id("run.test"),
                context.InstanceStableId,
                Id("operation.test"),
                Id("commitment.test"),
                strongboxDefinition.BaseRewardProfile.ProfileStableId,
                strongboxDefinition.Fingerprint);
            RewardGrantV1 grant = RewardGrantV1.Create(
                Id("grant.equipment-two"),
                RewardGrantKindV1.EquipmentReference,
                weaponId,
                2L);

            IReadOnlyList<EquipmentInstance> equipment;
            string rejection;
            bool resolved = resolver.TryResolve(
                strongboxDefinition,
                context,
                operation,
                grant,
                out equipment,
                out rejection);

            Assert.That(resolved, Is.True, rejection);
            Assert.That(context.ProgressionContext.CharacterLevel, Is.EqualTo(20));
            Assert.That(powerBudget.TierLevelBonus, Is.EqualTo(5));
            Assert.That(equipment, Has.Count.EqualTo(2));
            Assert.That(equipment[0].DefinitionId, Is.EqualTo(weaponId));
            Assert.That(equipment[1].DefinitionId, Is.EqualTo(weaponId));
            Assert.That(equipment[1].InstanceId, Is.Not.EqualTo(equipment[0].InstanceId));
        }

        private static long FindExpectedBudget(StrongboxPowerBudgetPolicyV1 policy, bool under)
        {
            for (ulong ordinal = 0UL; ordinal < 10000UL; ordinal++)
            {
                StrongboxItemLevelRollV1 itemLevel = policy.RollItemLevel(50, 77UL, 1, ordinal);
                if ((under && itemLevel.DifferenceFromMean < 0)
                    || (!under && itemLevel.DifferenceFromMean > 0))
                {
                    return policy.RollAugmentSlots(
                        itemLevel,
                        Id("equipment.budget-test"),
                        10,
                        77UL,
                        1).ExpectedAugmentSlotsMilli;
                }
            }

            Assert.Fail("Could not find the requested side of the deterministic distribution.");
            return 0L;
        }

        private static StrongboxPowerBudgetPolicyV1 Policy(
            int bonus,
            int itemSigmaMilli,
            int minimumSlots,
            int maximumSlots,
            int augmentSigmaMilli)
        {
            return StrongboxPowerBudgetPolicyV1.Create(
                bonus,
                itemSigmaMilli,
                minimumSlots,
                maximumSlots,
                augmentSigmaMilli);
        }

        private static StrongboxDefinitionV1 CreateStrongboxDefinition(
            StableId tierId,
            StableId equipmentPolicyId,
            StableId equipmentPoolId,
            long equipmentQuantity)
        {
            RewardGrantSpecificationV1 equipment = RewardGrantSpecificationV1.CreateFixed(
                Id("grant.equipment-two-spec"),
                RewardGrantKindV1.EquipmentReference,
                equipmentPoolId,
                equipmentQuantity);
            RewardProfileV1 profile = RewardProfileV1.Create(
                Id("profile.duplicate-test"),
                new[] { equipment },
                Array.Empty<IndependentRewardRollV1>(),
                Array.Empty<ExclusiveRewardGroupV1>());
            return StrongboxDefinitionV1.Create(
                tierId,
                0,
                1L,
                1L,
                0L,
                StrongboxRewardCountPolicyV1.Create(2, 2),
                StrongboxMandatoryScrapPolicyV1.Create(Id("currency.scrap"), 1L, 1L),
                equipmentPolicyId,
                profile,
                Id("scaling.source-tier"),
                Id("scaling.exceptional"));
        }

        private static EquipmentCatalog BuildSingleWeaponCatalog(StableId weaponId)
        {
            EquipmentQualityTier common = EquipmentQualityTier.Create(
                Id("quality.common"),
                "Common",
                1);
            EquipmentDefinition weapon = EquipmentDefinition.Create(
                weaponId,
                EquipmentCategoryIds.Weapon,
                Id("equipment-family.weapon-only-candidate"),
                "Weapon Candidate",
                Id("weapon.runtime-reference"),
                InclusiveIntRange.Create(1, 100),
                0,
                new[] { common },
                Array.Empty<StableId>());
            EquipmentCatalogBuildResult build = EquipmentCatalog.Build(
                new[] { weapon },
                Array.Empty<AugmentDefinition>());
            if (!build.IsValid)
            {
                throw new InvalidOperationException("Single-weapon test catalog is invalid.");
            }

            return build.Catalog;
        }

        private static StableId Id(string value)
        {
            return StableId.Parse(value);
        }
    }
}
