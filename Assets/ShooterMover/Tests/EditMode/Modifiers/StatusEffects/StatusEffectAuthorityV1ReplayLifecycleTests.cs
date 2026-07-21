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
        [Test]
        public void Replay_ReturnsOriginalResultAndConflictDoesNotMutate()
        {
            StatusEffectAuthorityV1 authority = CreateAuthority(
                Definition(
                    "status-effect.overdrive",
                    StatusEffectStackingPolicyV1.Refresh,
                    1,
                    10L,
                    RuntimeModifierOperationV1.Multiplicative,
                    1.25m));
            ApplyStatusEffectCommandV1 command = Apply(
                "operation.replayed",
                "status-effect.overdrive",
                "source.skill",
                4L);

            StatusEffectCommandResultV1 first = authority.Apply(command);
            StatusEffectCommandResultV1 exact = authority.Apply(command);
            string beforeConflict = authority.Snapshot.Fingerprint;
            StatusEffectCommandResultV1 conflict = authority.Apply(
                Apply(
                    "operation.replayed",
                    "status-effect.overdrive",
                    "source.other",
                    4L));

            Assert.That(exact, Is.SameAs(first));
            Assert.That(conflict.Status, Is.EqualTo(
                StatusEffectCommandStatusV1.ConflictingDuplicate));
            Assert.That(
                authority.Snapshot.Fingerprint,
                Is.EqualTo(beforeConflict));
        }

        [Test]
        public void CheckpointRoundTrip_PreservesStateAndReplayHistory()
        {
            StatusEffectCatalogV1 catalog = Catalog(
                Definition(
                    "status-effect.overdrive",
                    StatusEffectStackingPolicyV1.Refresh,
                    1,
                    20L,
                    RuntimeModifierOperationV1.Multiplicative,
                    1.25m));
            var authority =
                new StatusEffectAuthorityV1(SubjectId, 0, catalog);
            ApplyStatusEffectCommandV1 command = Apply(
                "operation.checkpoint-apply",
                "status-effect.overdrive",
                "source.skill",
                5L);
            StatusEffectCommandResultV1 original =
                authority.Apply(command);
            StatusEffectAuthoritySnapshotV1 checkpoint =
                authority.ExportSnapshot();

            StatusEffectAuthorityV1 restored =
                StatusEffectAuthorityV1.Restore(catalog, checkpoint);
            StatusEffectCommandResultV1 replayed =
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
            StatusEffectAuthorityV1 authority = CreateAuthority(
                Definition(
                    "status-effect.overdrive",
                    StatusEffectStackingPolicyV1.Refresh,
                    1,
                    20L,
                    RuntimeModifierOperationV1.Multiplicative,
                    1.25m));
            authority.Apply(
                Apply(
                    "operation.before-restart",
                    "status-effect.overdrive",
                    "source.skill",
                    2L));

            StatusEffectCommandResultV1 restart = authority.Restart(
                new RestartStatusEffectLifecycleCommandV1(
                    "operation.restart",
                    SubjectId,
                    0,
                    1,
                    3L));
            StatusEffectCommandResultV1 stale = authority.Apply(
                Apply(
                    "operation.stale-generation",
                    "status-effect.overdrive",
                    "source.skill",
                    4L,
                    0));

            Assert.That(restart.Action, Is.EqualTo(
                StatusEffectCommandActionV1.Restarted));
            Assert.That(authority.LifecycleGeneration, Is.EqualTo(1));
            Assert.That(authority.Snapshot.ActiveEffects, Is.Empty);
            Assert.That(stale.Status, Is.EqualTo(
                StatusEffectCommandStatusV1.LifecycleMismatch));
        }

        [Test]
        public void FactWindowActivation_CreatesGenericKillingSpreeEffect()
        {
            var conditionAuthority =
                new FactWindowConditionAuthorityV1(
                    SubjectId,
                    new[]
                    {
                        new FactWindowConditionDefinitionV1(
                            "condition.killing-spree",
                            "fact.enemy-killed",
                            3,
                            5L,
                            10L),
                    });
            var bridge = new FactWindowStatusEffectBridgeV1(
                new[]
                {
                    new FactWindowStatusEffectBindingV1(
                        "condition.killing-spree",
                        "status-effect.killing-spree",
                        "skill.killing-spree"),
                });
            StatusEffectAuthorityV1 effects = CreateAuthority(
                Definition(
                    "status-effect.killing-spree",
                    StatusEffectStackingPolicyV1.Refresh,
                    1,
                    10L,
                    RuntimeModifierOperationV1.Multiplicative,
                    1.25m));

            conditionAuthority.Apply(Kill("fact.kill-one", 10L));
            conditionAuthority.Apply(Kill("fact.kill-two", 12L));
            RuntimeObservedFactResultV1 conditionResult =
                conditionAuthority.Apply(Kill("fact.kill-three", 14L));

            ApplyStatusEffectCommandV1 applyCommand;
            bool created = bridge.TryCreateApplyCommand(
                conditionResult.Activations.Single(),
                "operation.killing-spree-activation",
                0,
                out applyCommand);
            StatusEffectCommandResultV1 effectResult =
                effects.Apply(applyCommand);

            Assert.That(created, Is.True);
            Assert.That(effectResult.Action, Is.EqualTo(
                StatusEffectCommandActionV1.Applied));
            Assert.That(
                effects.Snapshot.ModifierProjection
                    .Evaluate("combat.damage-multiplier", 1m)
                    .FinalValue,
                Is.EqualTo(1.25m));
        }

    }
}
