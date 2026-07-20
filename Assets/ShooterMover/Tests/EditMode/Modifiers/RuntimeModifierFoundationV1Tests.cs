using System.Collections.Generic;
using NUnit.Framework;
using ShooterMover.Application.Modifiers;
using ShooterMover.Domain.Modifiers;
using ShooterMover.Domain.Progression.Skills;

namespace ShooterMover.Tests.EditMode.Modifiers
{
    public sealed class RuntimeModifierFoundationV1Tests
    {
        [Test]
        public void CritChanceSkill_UsesExistingSkillDescriptorWithoutNewCodePath()
        {
            var allocation = new RankedSkillAllocationSnapshotV2(
                "profile.striker-one",
                "striker",
                1L,
                "skills.schema.v2",
                "content.fixture",
                new Dictionary<string, int>());
            var skillEffects = new SkillEffectSnapshotV2(
                allocation,
                new[]
                {
                    new SkillEffectContributionV2(
                        "skill.precision-training#1",
                        new SkillEffectDescriptorV2(
                            "combat.critical-chance",
                            SkillModifierKindV2.Flat,
                            0.15m)),
                });

            RuntimeModifierEvaluationV1 result =
                SkillEffectModifierAdapterV1.Adapt(skillEffects).Evaluate(
                    "combat.critical-chance",
                    0.05m,
                    null,
                    0m,
                    1m);

            Assert.That(result.FinalValue, Is.EqualTo(0.20m));
            Assert.That(result.AppliedModifiers.Count, Is.EqualTo(1));
        }

        [Test]
        public void EventModifier_AppliesOnlyWhenEventConditionIsActive()
        {
            var modifiers = new RuntimeModifierSnapshotV1(
                new[]
                {
                    new RuntimeModifierDefinitionV1(
                        "event.double-drops-2026",
                        "rewards.strongbox-drop-weight",
                        RuntimeModifierOperationV1.Multiplicative,
                        2m,
                        "event.double-drops-active"),
                });

            RuntimeModifierEvaluationV1 normal = modifiers.Evaluate(
                "rewards.strongbox-drop-weight",
                100m);
            RuntimeModifierEvaluationV1 eventValue = modifiers.Evaluate(
                "rewards.strongbox-drop-weight",
                100m,
                new[] { "event.double-drops-active" });

            Assert.That(normal.FinalValue, Is.EqualTo(100m));
            Assert.That(eventValue.FinalValue, Is.EqualTo(200m));
        }

        [Test]
        public void KillingSpree_ActivatesFromThreeKillsInsideWindow()
        {
            var conditions = new FactWindowConditionAuthorityV1(
                "participant.player-one",
                new[]
                {
                    new FactWindowConditionDefinitionV1(
                        "condition.killing-spree",
                        "fact.enemy-killed",
                        3,
                        5L,
                        10L),
                });
            var modifiers = new RuntimeModifierSnapshotV1(
                new[]
                {
                    new RuntimeModifierDefinitionV1(
                        "skill.killing-spree",
                        "combat.damage-multiplier",
                        RuntimeModifierOperationV1.Multiplicative,
                        1.25m,
                        "condition.killing-spree"),
                });

            conditions.Apply(Kill("fact.kill-one", 10L));
            conditions.Apply(Kill("fact.kill-two", 12L));
            RuntimeObservedFactResultV1 activation =
                conditions.Apply(Kill("fact.kill-three", 14L));

            RuntimeModifierEvaluationV1 active = modifiers.Evaluate(
                "combat.damage-multiplier",
                1m,
                conditions.ActiveConditionIdsAt(14L));
            RuntimeModifierEvaluationV1 expired = modifiers.Evaluate(
                "combat.damage-multiplier",
                1m,
                conditions.ActiveConditionIdsAt(24L));

            Assert.That(activation.Activations.Count, Is.EqualTo(1));
            Assert.That(active.FinalValue, Is.EqualTo(1.25m));
            Assert.That(expired.FinalValue, Is.EqualTo(1m));
        }

        [Test]
        public void KillingSpree_DoesNotActivateWhenKillsFallOutsideWindow()
        {
            var conditions = new FactWindowConditionAuthorityV1(
                "participant.player-one",
                new[]
                {
                    new FactWindowConditionDefinitionV1(
                        "condition.killing-spree",
                        "fact.enemy-killed",
                        3,
                        5L,
                        10L),
                });

            conditions.Apply(Kill("fact.kill-one", 1L));
            conditions.Apply(Kill("fact.kill-two", 7L));
            RuntimeObservedFactResultV1 result =
                conditions.Apply(Kill("fact.kill-three", 8L));

            Assert.That(result.Activations, Is.Empty);
            Assert.That(
                conditions.IsConditionActive(
                    "condition.killing-spree",
                    8L),
                Is.False);
        }

        [Test]
        public void ObservedFacts_AreIdempotentAndRejectConflictingReuse()
        {
            var conditions = new FactWindowConditionAuthorityV1(
                "participant.player-one",
                new[]
                {
                    new FactWindowConditionDefinitionV1(
                        "condition.one-kill",
                        "fact.enemy-killed",
                        1,
                        1L,
                        2L),
                });
            RuntimeObservedFactV1 fact = Kill("fact.kill-one", 4L);

            RuntimeObservedFactResultV1 applied = conditions.Apply(fact);
            RuntimeObservedFactResultV1 duplicate = conditions.Apply(fact);
            RuntimeObservedFactResultV1 conflict = conditions.Apply(
                new RuntimeObservedFactV1(
                    "fact.kill-one",
                    "fact.enemy-killed",
                    "participant.player-one",
                    5L));

            Assert.That(applied.Status, Is.EqualTo(
                RuntimeObservedFactStatusV1.Applied));
            Assert.That(duplicate.Status, Is.EqualTo(
                RuntimeObservedFactStatusV1.ExactDuplicateNoChange));
            Assert.That(conflict.Status, Is.EqualTo(
                RuntimeObservedFactStatusV1.ConflictingDuplicate));
        }

        [Test]
        public void UnrelatedFacts_DoNotRequireConditionSpecificBranches()
        {
            var conditions = new FactWindowConditionAuthorityV1(
                "participant.player-one",
                new[]
                {
                    new FactWindowConditionDefinitionV1(
                        "condition.killing-spree",
                        "fact.enemy-killed",
                        2,
                        10L,
                        10L),
                });

            RuntimeObservedFactResultV1 result = conditions.Apply(
                new RuntimeObservedFactV1(
                    "fact.prop-one",
                    "fact.prop-destroyed",
                    "participant.player-one",
                    1L));

            Assert.That(result.Status, Is.EqualTo(
                RuntimeObservedFactStatusV1.Applied));
            Assert.That(result.Activations, Is.Empty);
        }

        [Test]
        public void ModifierOrderingAndFingerprint_AreInputOrderIndependent()
        {
            var first = new RuntimeModifierDefinitionV1(
                "skill.a",
                "combat.damage",
                RuntimeModifierOperationV1.Flat,
                5m);
            var second = new RuntimeModifierDefinitionV1(
                "event.a",
                "combat.damage",
                RuntimeModifierOperationV1.Percentage,
                0.10m);

            var left = new RuntimeModifierSnapshotV1(new[] { first, second });
            var right = new RuntimeModifierSnapshotV1(new[] { second, first });

            Assert.That(left.Fingerprint, Is.EqualTo(right.Fingerprint));
            Assert.That(
                left.Evaluate("combat.damage", 100m).FinalValue,
                Is.EqualTo(115.5m));
        }

        private static RuntimeObservedFactV1 Kill(
            string factId,
            long tick)
        {
            return new RuntimeObservedFactV1(
                factId,
                "fact.enemy-killed",
                "participant.player-one",
                tick);
        }
    }
}
