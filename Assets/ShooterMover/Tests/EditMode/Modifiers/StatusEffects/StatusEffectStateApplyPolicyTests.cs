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
        private const string SubjectId = "participant.player-one";

        [Test]
        public void ApplyAndExpire_ProjectsOnlyLiveModifiers()
        {
            StatusEffectState authority = CreateAuthority(
                Definition(
                    "status-effect.overdrive",
                    StatusEffectStackingPolicy.Refresh,
                    1,
                    5L,
                    LiveModifierOperation.Multiplicative,
                    1.25m));

            StatusEffectCommandResult applied = authority.Apply(
                Apply(
                    "operation.apply-overdrive",
                    "status-effect.overdrive",
                    "skill.overdrive",
                    10L));

            Assert.That(applied.IsAccepted, Is.True);
            Assert.That(applied.Action, Is.EqualTo(
                StatusEffectCommandAction.Applied));
            Assert.That(
                authority.Snapshot.ModifierProjection
                    .Evaluate("combat.damage-multiplier", 1m)
                    .FinalValue,
                Is.EqualTo(1.25m));

            StatusEffectCommandResult expired = authority.Advance(
                new AdvanceStatusEffectTickCommand(
                    "operation.expire-overdrive",
                    SubjectId,
                    0,
                    15L));

            Assert.That(expired.Action, Is.EqualTo(
                StatusEffectCommandAction.Expired));
            Assert.That(expired.ExpiredStackCount, Is.EqualTo(1));
            Assert.That(authority.Snapshot.ActiveEffects, Is.Empty);
            Assert.That(
                authority.Snapshot.ModifierProjection
                    .Evaluate("combat.damage-multiplier", 1m)
                    .FinalValue,
                Is.EqualTo(1m));
        }

        [Test]
        public void AddPolicy_StacksDifferentSourcesToAuthoredMaximum()
        {
            StatusEffectState authority = CreateAuthority(
                Definition(
                    "status-effect.armor-plating",
                    StatusEffectStackingPolicy.Add,
                    2,
                    20L,
                    LiveModifierOperation.Flat,
                    5m,
                    "combat.armor"));

            StatusEffectCommandResult first = authority.Apply(
                Apply(
                    "operation.stack-one",
                    "status-effect.armor-plating",
                    "source.teammate-one",
                    1L));
            StatusEffectCommandResult second = authority.Apply(
                Apply(
                    "operation.stack-two",
                    "status-effect.armor-plating",
                    "source.teammate-two",
                    2L));
            StatusEffectCommandResult capped = authority.Apply(
                Apply(
                    "operation.stack-three",
                    "status-effect.armor-plating",
                    "source.teammate-three",
                    3L));

            Assert.That(first.Action, Is.EqualTo(
                StatusEffectCommandAction.Applied));
            Assert.That(second.Action, Is.EqualTo(
                StatusEffectCommandAction.Stacked));
            Assert.That(capped.Status, Is.EqualTo(
                StatusEffectCommandStatus.AcceptedNoChange));
            Assert.That(capped.Action, Is.EqualTo(
                StatusEffectCommandAction.Ignored));
            Assert.That(
                authority.Snapshot.ActiveEffects.Single().Stacks
                    .Select(item => item.SourceId),
                Is.EqualTo(new[]
                {
                    "source.teammate-one",
                    "source.teammate-two",
                }));
            Assert.That(
                authority.Snapshot.ModifierProjection
                    .Evaluate("combat.armor", 0m)
                    .FinalValue,
                Is.EqualTo(10m));
        }

    }
}
