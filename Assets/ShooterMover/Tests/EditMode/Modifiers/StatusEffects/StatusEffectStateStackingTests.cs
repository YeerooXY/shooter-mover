using System;
using System.Linq;
using NUnit.Framework;
using ShooterMover.Application.Modifiers;
using ShooterMover.Application.Modifiers.StatusEffects;
using ShooterMover.Domain.Modifiers;
using ShooterMover.Domain.Modifiers.StatusEffects;

namespace ShooterMover.Tests.EditMode.Modifiers.StatusEffects
{
    public sealed partial class StatusEffectStateTests
    {
        [Test]
        public void RefreshPolicy_UsesOneSharedStackAndLatestSource()
        {
            StatusEffectState authority = CreateAuthority(
                Definition(
                    "status-effect.haste",
                    StatusEffectStackingPolicy.Refresh,
                    1,
                    10L,
                    LiveModifierOperation.Percentage,
                    0.2m,
                    "combat.movement-speed"));

            authority.Apply(
                Apply(
                    "operation.haste-one",
                    "status-effect.haste",
                    "source.medic-one",
                    2L));
            string stackId = authority.Snapshot.ActiveEffects
                .Single().Stacks.Single().StackId;

            StatusEffectCommandResult refreshed = authority.Apply(
                Apply(
                    "operation.haste-two",
                    "status-effect.haste",
                    "source.medic-two",
                    7L));

            ActiveStatusEffectStackSnapshot stack =
                authority.Snapshot.ActiveEffects
                    .Single().Stacks.Single();
            Assert.That(refreshed.Action, Is.EqualTo(
                StatusEffectCommandAction.Refreshed));
            Assert.That(stack.StackId, Is.EqualTo(stackId));
            Assert.That(stack.SourceId, Is.EqualTo("source.medic-two"));
            Assert.That(stack.ExpiresAtTickExclusive, Is.EqualTo(17L));
        }

        [Test]
        public void ReplacePolicy_ReplacesPriorSourceAndLogicalStack()
        {
            StatusEffectState authority = CreateAuthority(
                Definition(
                    "status-effect.elemental-stance",
                    StatusEffectStackingPolicy.Replace,
                    1,
                    30L,
                    LiveModifierOperation.Flat,
                    3m,
                    "combat.contact-damage"));

            authority.Apply(
                Apply(
                    "operation.stance-one",
                    "status-effect.elemental-stance",
                    "source.fire",
                    1L));
            string priorStackId = authority.Snapshot.ActiveEffects
                .Single().Stacks.Single().StackId;

            StatusEffectCommandResult replaced = authority.Apply(
                Apply(
                    "operation.stance-two",
                    "status-effect.elemental-stance",
                    "source.cryo",
                    2L));

            ActiveStatusEffectStackSnapshot stack =
                authority.Snapshot.ActiveEffects
                    .Single().Stacks.Single();
            Assert.That(replaced.Action, Is.EqualTo(
                StatusEffectCommandAction.Replaced));
            Assert.That(stack.StackId, Is.Not.EqualTo(priorStackId));
            Assert.That(stack.SourceId, Is.EqualTo("source.cryo"));
        }

        [Test]
        public void IgnorePolicy_PreservesFirstSourceUntilExpiry()
        {
            StatusEffectState authority = CreateAuthority(
                Definition(
                    "status-effect.shielded",
                    StatusEffectStackingPolicy.Ignore,
                    1,
                    8L,
                    LiveModifierOperation.Flat,
                    20m,
                    "combat.armor"));

            authority.Apply(
                Apply(
                    "operation.shield-one",
                    "status-effect.shielded",
                    "source.generator-one",
                    1L));
            StatusEffectCommandResult ignored = authority.Apply(
                Apply(
                    "operation.shield-two",
                    "status-effect.shielded",
                    "source.generator-two",
                    2L));

            Assert.That(ignored.Status, Is.EqualTo(
                StatusEffectCommandStatus.AcceptedNoChange));
            Assert.That(ignored.Action, Is.EqualTo(
                StatusEffectCommandAction.Ignored));
            Assert.That(
                authority.Snapshot.ActiveEffects
                    .Single().Stacks.Single().SourceId,
                Is.EqualTo("source.generator-one"));
        }

        [Test]
        public void Dispel_RemovesOnlyMatchingCategory()
        {
            StatusEffectState authority = CreateAuthority(
                Definition(
                    "status-effect.burning",
                    StatusEffectStackingPolicy.Refresh,
                    1,
                    100L,
                    LiveModifierOperation.Percentage,
                    -0.1m,
                    "combat.movement-speed",
                    "dispel.harmful"),
                Definition(
                    "status-effect.overdrive",
                    StatusEffectStackingPolicy.Refresh,
                    1,
                    100L,
                    LiveModifierOperation.Multiplicative,
                    1.2m,
                    "combat.damage-multiplier",
                    "dispel.beneficial"));

            authority.Apply(
                Apply(
                    "operation.apply-burning",
                    "status-effect.burning",
                    "source.enemy",
                    1L));
            authority.Apply(
                Apply(
                    "operation.apply-overdrive",
                    "status-effect.overdrive",
                    "source.skill",
                    2L));

            StatusEffectCommandResult result = authority.Dispel(
                new DispelStatusEffectsCommand(
                    "operation.cleanse",
                    SubjectId,
                    0,
                    3L,
                    "dispel.harmful"));

            Assert.That(result.Action, Is.EqualTo(
                StatusEffectCommandAction.Dispelled));
            Assert.That(result.AffectedStackCount, Is.EqualTo(1));
            Assert.That(
                authority.Snapshot.ActiveEffects
                    .Select(item => item.EffectId),
                Is.EqualTo(new[] { "status-effect.overdrive" }));
        }

    }
}
