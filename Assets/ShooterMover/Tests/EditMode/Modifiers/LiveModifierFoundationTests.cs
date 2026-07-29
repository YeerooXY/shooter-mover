using System.Collections.Generic;
using NUnit.Framework;
using ShooterMover.Application.Modifiers;
using ShooterMover.Domain.Modifiers;
using ShooterMover.Domain.Progression.Skills;

namespace ShooterMover.Tests.EditMode.Modifiers
{
    public sealed class LiveModifierFoundationTests
    {
        [Test]
        public void CritChanceSkill_UsesExistingSkillDescriptorWithoutNewCodePath()
        {
            var allocation = new RankedSkillAllocationSnapshot(
                "profile.striker-one",
                "striker",
                1L,
                "skills.schema.v2",
                "content.fixture",
                new Dictionary<string, int>());
            var skillEffects = new SkillEffectSnapshot(
                allocation,
                new[]
                {
                    new SkillEffectContribution(
                        "skill.precision-training#1",
                        new SkillEffectDescriptor(
                            "combat.critical-chance",
                            SkillModifierKind.Flat,
                            0.15m)),
                });

            LiveModifierEvaluation result =
                SkillEffectModifierBridge.Adapt(skillEffects).Evaluate(
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
            var modifiers = new LiveModifierSnapshot(
                new[]
                {
                    new LiveModifierDefinition(
                        "event.double-drops-2026",
                        "rewards.strongbox-drop-weight",
                        LiveModifierOperation.Multiplicative,
                        2m,
                        "event.double-drops-active"),
                });

            LiveModifierEvaluation normal = modifiers.Evaluate(
                "rewards.strongbox-drop-weight",
                100m);
            LiveModifierEvaluation eventValue = modifiers.Evaluate(
                "rewards.strongbox-drop-weight",
                100m,
                new[] { "event.double-drops-active" });

            Assert.That(normal.FinalValue, Is.EqualTo(100m));
            Assert.That(eventValue.FinalValue, Is.EqualTo(200m));
        }

        [Test]
        public void KillingSpree_ActivatesFromThreeKillsInsideWindow()
        {
            var conditions = new FactWindowConditionState(
                "participant.player-one",
                new[]
                {
                    new FactWindowConditionDefinition(
                        "condition.killing-spree",
                        "fact.enemy-killed",
                        3,
                        5L,
                        10L),
                });
            var modifiers = new LiveModifierSnapshot(
                new[]
                {
                    new LiveModifierDefinition(
                        "skill.killing-spree",
                        "combat.damage-multiplier",
                        LiveModifierOperation.Multiplicative,
                        1.25m,
                        "condition.killing-spree"),
                });

            conditions.Apply(Kill("fact.kill-one", 10L));
            conditions.Apply(Kill("fact.kill-two", 12L));
            LiveObservedFactResult activation =
                conditions.Apply(Kill("fact.kill-three", 14L));

            LiveModifierEvaluation active = modifiers.Evaluate(
                "combat.damage-multiplier",
                1m,
                conditions.ActiveConditionIdsAt(14L));
            LiveModifierEvaluation expired = modifiers.Evaluate(
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
            var conditions = new FactWindowConditionState(
                "participant.player-one",
                new[]
                {
                    new FactWindowConditionDefinition(
                        "condition.killing-spree",
                        "fact.enemy-killed",
                        3,
                        5L,
                        10L),
                });

            conditions.Apply(Kill("fact.kill-one", 1L));
            conditions.Apply(Kill("fact.kill-two", 7L));
            LiveObservedFactResult result =
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
            var conditions = new FactWindowConditionState(
                "participant.player-one",
                new[]
                {
                    new FactWindowConditionDefinition(
                        "condition.one-kill",
                        "fact.enemy-killed",
                        1,
                        1L,
                        2L),
                });
            LiveObservedFact fact = Kill("fact.kill-one", 4L);

            LiveObservedFactResult applied = conditions.Apply(fact);
            LiveObservedFactResult duplicate = conditions.Apply(fact);
            LiveObservedFactResult conflict = conditions.Apply(
                new LiveObservedFact(
                    "fact.kill-one",
                    "fact.enemy-killed",
                    "participant.player-one",
                    5L));

            Assert.That(applied.Status, Is.EqualTo(
                LiveObservedFactStatus.Applied));
            Assert.That(duplicate.Status, Is.EqualTo(
                LiveObservedFactStatus.ExactDuplicateNoChange));
            Assert.That(conflict.Status, Is.EqualTo(
                LiveObservedFactStatus.ConflictingDuplicate));
        }

        [Test]
        public void UnrelatedFacts_DoNotRequireConditionSpecificBranches()
        {
            var conditions = new FactWindowConditionState(
                "participant.player-one",
                new[]
                {
                    new FactWindowConditionDefinition(
                        "condition.killing-spree",
                        "fact.enemy-killed",
                        2,
                        10L,
                        10L),
                });

            LiveObservedFactResult result = conditions.Apply(
                new LiveObservedFact(
                    "fact.prop-one",
                    "fact.prop-destroyed",
                    "participant.player-one",
                    1L));

            Assert.That(result.Status, Is.EqualTo(
                LiveObservedFactStatus.Applied));
            Assert.That(result.Activations, Is.Empty);
        }

        [Test]
        public void ModifierOrderingAndFingerprint_AreInputOrderIndependent()
        {
            var first = new LiveModifierDefinition(
                "skill.a",
                "combat.damage",
                LiveModifierOperation.Flat,
                5m);
            var second = new LiveModifierDefinition(
                "event.a",
                "combat.damage",
                LiveModifierOperation.Percentage,
                0.10m);

            var left = new LiveModifierSnapshot(new[] { first, second });
            var right = new LiveModifierSnapshot(new[] { second, first });

            Assert.That(left.Fingerprint, Is.EqualTo(right.Fingerprint));
            Assert.That(
                left.Evaluate("combat.damage", 100m).FinalValue,
                Is.EqualTo(115.5m));
        }

        private static LiveObservedFact Kill(
            string factId,
            long tick)
        {
            return new LiveObservedFact(
                factId,
                "fact.enemy-killed",
                "participant.player-one",
                tick);
        }
    }
}
