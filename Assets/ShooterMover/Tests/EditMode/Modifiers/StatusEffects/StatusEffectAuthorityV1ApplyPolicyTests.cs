using System;
using System.Linq;
using NUnit.Framework;
using ShooterMover.Application.Modifiers;
using ShooterMover.Application.Modifiers.StatusEffects;
using ShooterMover.Domain.Modifiers;
using ShooterMover.Domain.Modifiers.StatusEffects;

namespace ShooterMover.Tests.EditMode.Modifiers.StatusEffects
{
    public sealed partial class StatusEffectAuthorityV1Tests
    {
        private const string SubjectId = "participant.player-one";

        [Test]
        public void ApplyAndExpire_ProjectsOnlyLiveModifiers()
        {
            StatusEffectAuthorityV1 authority = CreateAuthority(
                Definition(
                    "status-effect.overdrive",
                    StatusEffectStackingPolicyV1.Refresh,
                    1,
                    5L,
                    RuntimeModifierOperationV1.Multiplicative,
                    1.25m));

            StatusEffectCommandResultV1 applied = authority.Apply(
                Apply(
                    "operation.apply-overdrive",
                    "status-effect.overdrive",
                    "skill.overdrive",
                    10L));

            Assert.That(applied.IsAccepted, Is.True);
            Assert.That(applied.Action, Is.EqualTo(
                StatusEffectCommandActionV1.Applied));
            Assert.That(
                authority.Snapshot.ModifierProjection
                    .Evaluate("combat.damage-multiplier", 1m)
                    .FinalValue,
                Is.EqualTo(1.25m));

            StatusEffectCommandResultV1 expired = authority.Advance(
                new AdvanceStatusEffectTickCommandV1(
                    "operation.expire-overdrive",
                    SubjectId,
                    0,
                    15L));

            Assert.That(expired.Action, Is.EqualTo(
                StatusEffectCommandActionV1.Expired));
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
            StatusEffectAuthorityV1 authority = CreateAuthority(
                Definition(
                    "status-effect.armor-plating",
                    StatusEffectStackingPolicyV1.Add,
                    2,
                    20L,
                    RuntimeModifierOperationV1.Flat,
                    5m,
                    "combat.armor"));

            StatusEffectCommandResultV1 first = authority.Apply(
                Apply(
                    "operation.stack-one",
                    "status-effect.armor-plating",
                    "source.teammate-one",
                    1L));
            StatusEffectCommandResultV1 second = authority.Apply(
                Apply(
                    "operation.stack-two",
                    "status-effect.armor-plating",
                    "source.teammate-two",
                    2L));
            StatusEffectCommandResultV1 capped = authority.Apply(
                Apply(
                    "operation.stack-three",
                    "status-effect.armor-plating",
                    "source.teammate-three",
                    3L));

            Assert.That(first.Action, Is.EqualTo(
                StatusEffectCommandActionV1.Applied));
            Assert.That(second.Action, Is.EqualTo(
                StatusEffectCommandActionV1.Stacked));
            Assert.That(capped.Status, Is.EqualTo(
                StatusEffectCommandStatusV1.AcceptedNoChange));
            Assert.That(capped.Action, Is.EqualTo(
                StatusEffectCommandActionV1.Ignored));
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
