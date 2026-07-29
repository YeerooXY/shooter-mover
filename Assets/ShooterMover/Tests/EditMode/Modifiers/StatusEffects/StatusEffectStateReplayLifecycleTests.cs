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
        public void Replay_ReturnsOriginalResultAndConflictDoesNotMutate()
        {
            StatusEffectState authority = CreateAuthority(
                Definition(
                    "status-effect.overdrive",
                    StatusEffectStackingPolicy.Refresh,
                    1,
                    10L,
                    LiveModifierOperation.Multiplicative,
                    1.25m));
            ApplyStatusEffectCommand command = Apply(
                "operation.replayed",
                "status-effect.overdrive",
                "source.skill",
                4L);

            StatusEffectCommandResult first = authority.Apply(command);
            StatusEffectCommandResult exact = authority.Apply(command);
            string beforeConflict = authority.Snapshot.Fingerprint;
            StatusEffectCommandResult conflict = authority.Apply(
                Apply(
                    "operation.replayed",
                    "status-effect.overdrive",
                    "source.other",
                    4L));

            Assert.That(exact, Is.SameAs(first));
            Assert.That(conflict.Status, Is.EqualTo(
                StatusEffectCommandStatus.ConflictingDuplicate));
            Assert.That(
                authority.Snapshot.Fingerprint,
                Is.EqualTo(beforeConflict));
        }

        [Test]
        public void CheckpointRoundTrip_PreservesStateAndReplayHistory()
        {
            StatusEffectCatalog catalog = Catalog(
                Definition(
                    "status-effect.overdrive",
                    StatusEffectStackingPolicy.Refresh,
                    1,
                    20L,
                    LiveModifierOperation.Multiplicative,
                    1.25m));
            var authority =
                new StatusEffectState(SubjectId, 0, catalog);
            ApplyStatusEffectCommand command = Apply(
                "operation.checkpoint-apply",
                "status-effect.overdrive",
                "source.skill",
                5L);
            StatusEffectCommandResult original =
                authority.Apply(command);
            StatusEffectLedgerSnapshot checkpoint =
                authority.ExportSnapshot();

            StatusEffectState restored =
                StatusEffectState.Restore(catalog, checkpoint);
            StatusEffectCommandResult replayed =
                restored.Apply(command);

            Assert.That(
                restored.ExportSnapshot().Fingerprint,
                Is.EqualTo(checkpoint.Fingerprint));
            Assert.That(
                restored.Snapshot.Fingerprint,
                Is.EqualTo(authority.Snapshot.Fingerprint));
            Assert.That(
                replayed.Fingerprint,
                Is.EqualTo(original.Fingerprint));
            Assert.That(
                replayed.State.Fingerprint,
                Is.EqualTo(original.State.Fingerprint));
        }

        [Test]
        public void LifecycleRestart_ClearsRunLocalEffectsAndRejectsStaleGeneration()
        {
            StatusEffectState authority = CreateAuthority(
                Definition(
                    "status-effect.overdrive",
                    StatusEffectStackingPolicy.Refresh,
                    1,
                    20L,
                    LiveModifierOperation.Multiplicative,
                    1.25m));
            authority.Apply(
                Apply(
                    "operation.before-restart",
                    "status-effect.overdrive",
                    "source.skill",
                    2L));

            StatusEffectCommandResult restart = authority.Restart(
                new RestartStatusEffectLifecycleCommand(
                    "operation.restart",
                    SubjectId,
                    0,
                    1,
                    3L));
            StatusEffectCommandResult stale = authority.Apply(
                Apply(
                    "operation.stale-generation",
                    "status-effect.overdrive",
                    "source.skill",
                    4L,
                    0));

            Assert.That(restart.Action, Is.EqualTo(
                StatusEffectCommandAction.Restarted));
            Assert.That(authority.LifecycleGeneration, Is.EqualTo(1));
            Assert.That(authority.Snapshot.ActiveEffects, Is.Empty);
            Assert.That(stale.Status, Is.EqualTo(
                StatusEffectCommandStatus.LifecycleMismatch));
        }

        [Test]
        public void FactWindowActivation_CreatesGenericKillingSpreeEffect()
        {
            var conditionAuthority =
                new FactWindowConditionState(
                    SubjectId,
                    new[]
                    {
                        new FactWindowConditionDefinition(
                            "condition.killing-spree",
                            "fact.enemy-killed",
                            3,
                            5L,
                            10L),
                    });
            var bridge = new FactWindowStatusEffectBridge(
                new[]
                {
                    new FactWindowStatusEffectBinding(
                        "condition.killing-spree",
                        "status-effect.killing-spree",
                        "skill.killing-spree"),
                });
            StatusEffectState effects = CreateAuthority(
                Definition(
                    "status-effect.killing-spree",
                    StatusEffectStackingPolicy.Refresh,
                    1,
                    10L,
                    LiveModifierOperation.Multiplicative,
                    1.25m));

            conditionAuthority.Apply(Kill("fact.kill-one", 10L));
            conditionAuthority.Apply(Kill("fact.kill-two", 12L));
            LiveObservedFactResult conditionResult =
                conditionAuthority.Apply(Kill("fact.kill-three", 14L));

            ApplyStatusEffectCommand applyCommand;
            bool created = bridge.TryCreateApplyCommand(
                conditionResult.Activations.Single(),
                "operation.killing-spree-activation",
                0,
                out applyCommand);
            StatusEffectCommandResult effectResult =
                effects.Apply(applyCommand);

            Assert.That(created, Is.True);
            Assert.That(effectResult.Action, Is.EqualTo(
                StatusEffectCommandAction.Applied));
            Assert.That(
                effects.Snapshot.ModifierProjection
                    .Evaluate("combat.damage-multiplier", 1m)
                    .FinalValue,
                Is.EqualTo(1.25m));
        }

    }
}
