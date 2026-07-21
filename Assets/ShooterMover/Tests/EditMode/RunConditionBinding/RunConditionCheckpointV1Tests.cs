using System.Collections.Generic;
using NUnit.Framework;
using ShooterMover.Application.Runs.Session;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Modifiers;

namespace ShooterMover.Tests.EditMode.RunConditionBinding
{
    public sealed class RunConditionCheckpointV1Tests
    {
        [Test]
        public void CheckpointCarriesExactRunGenerationAndModifierProjection()
        {
            StableId runId = Id("run-instance.checkpoint");
            var debug = new RunDebugSnapshotV1(
                runId,
                RunSessionLifecycleStateV1.Active,
                2L,
                11L,
                "start-fingerprint",
                "frozen-fingerprint",
                "local-fingerprint",
                new Dictionary<string, string>
                {
                    { "condition-runtime-authority-v1", "condition-port-fingerprint" },
                },
                string.Empty);
            var checkpoint = new RunCheckpointV1(
                new RunRecoveryDiagnosticSnapshotV1(
                    debug,
                    "permanent-character-fingerprint",
                    4L,
                    false),
                new RunLocalStateSnapshotV1(
                    0L,
                    null,
                    null,
                    null));
            var participant = new RunConditionParticipantSnapshotV1(
                Id("participant.checkpoint"),
                Id("character.checkpoint"),
                Id("actor.checkpoint"),
                2L,
                11L,
                new[] { "condition.fixture" },
                1,
                "status-effect-fingerprint",
                new RuntimeModifierSnapshotV1(
                    new[]
                    {
                        new RuntimeModifierDefinitionV1(
                            "status-effect.fixture",
                            "combat.damage-multiplier",
                            RuntimeModifierOperationV1.Multiplicative,
                            1.25m),
                    }));
            var condition = new RunConditionRuntimeSnapshotV1(
                runId,
                2L,
                11L,
                "condition-definition-fingerprint",
                new[] { participant },
                3);

            var combined = new RunConditionCheckpointV1(
                checkpoint,
                condition);

            Assert.That(combined.RunCheckpoint, Is.SameAs(checkpoint));
            Assert.That(combined.ConditionRuntime, Is.SameAs(condition));
            Assert.That(combined.ConditionRuntime.Participants[0]
                    .ModifierProjection
                    .Evaluate("combat.damage-multiplier", 1m)
                    .FinalValue,
                Is.EqualTo(1.25m));
            Assert.That(combined.RunCheckpoint.Recovery.IsPermanentCharacterTruth,
                Is.False);
            Assert.That(combined.Fingerprint, Is.Not.Empty);
        }

        [Test]
        public void CheckpointRejectsAConditionSnapshotFromAnotherGeneration()
        {
            StableId runId = Id("run-instance.checkpoint-mismatch");
            var checkpoint = new RunCheckpointV1(
                new RunRecoveryDiagnosticSnapshotV1(
                    new RunDebugSnapshotV1(
                        runId,
                        RunSessionLifecycleStateV1.Active,
                        1L,
                        0L,
                        "start-fingerprint",
                        "frozen-fingerprint",
                        "local-fingerprint",
                        new Dictionary<string, string>
                        {
                            { "condition-runtime-authority-v1", "condition-port-fingerprint" },
                        },
                        string.Empty),
                    "permanent-character-fingerprint",
                    0L,
                    false),
                new RunLocalStateSnapshotV1(0L, null, null, null));
            var condition = new RunConditionRuntimeSnapshotV1(
                runId,
                2L,
                0L,
                "condition-definition-fingerprint",
                new[]
                {
                    new RunConditionParticipantSnapshotV1(
                        Id("participant.checkpoint-mismatch"),
                        Id("character.checkpoint-mismatch"),
                        Id("actor.checkpoint-mismatch"),
                        2L,
                        0L,
                        null,
                        0,
                        "status-effect-fingerprint",
                        new RuntimeModifierSnapshotV1(null)),
                },
                0);

            Assert.Throws<System.ArgumentException>(() =>
                new RunConditionCheckpointV1(checkpoint, condition));
        }

        private static StableId Id(string value)
        {
            return StableId.Parse(value);
        }
    }
}
