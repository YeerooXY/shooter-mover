using System;
using NUnit.Framework;
using ShooterMover.ConditionRuntime;
using ShooterMover.Domain.Modifiers.StatusEffects;
using ShooterMover.EnemyRuntimeComposition;

namespace ShooterMover.Tests.EditMode.ConditionRuntime
{
    public sealed partial class ConditionRuntimeAuthorityV1Tests
    {
        [Test]
        public void ExactDeliveryReplay_AfterLaterMutation_ReturnsStableOriginalSnapshot()
        {
            TestRunPorts ports;
            ConditionRuntimeAuthorityV1 runtime = CreateRuntime(
                "run.alpha",
                1L,
                new[] { Player("a", 1L, KillDefinition(2, 10L, 20L)) },
                out ports);
            EnemyDeathFactV1 deathA = Death("run.alpha", "a", 1, 1L);
            ports.CurrentTick = 1L;
            AcceptedGameplayFactDeliveryV1 deliveryA = Delivery(
                "delivery.a.1",
                deathA,
                "run.alpha",
                "a",
                1L,
                1L);

            ConditionFactIngestionResultV1 first = runtime.Ingest(deliveryA);
            ConditionFactIngestionResultV1 second =
                DeliverKill(runtime, ports, "a", 2, 3L);
            string currentFingerprint = second.Snapshot.Fingerprint;

            ConditionFactIngestionResultV1 replayOne = runtime.Ingest(deliveryA);
            ConditionFactIngestionResultV1 replayTwo = runtime.Ingest(deliveryA);

            Assert.That(replayOne.Status,
                Is.EqualTo(ConditionFactIngestionStatusV1.ExactDuplicateNoChange));
            Assert.That(replayTwo.Status,
                Is.EqualTo(ConditionFactIngestionStatusV1.ExactDuplicateNoChange));
            Assert.That(replayOne.Fingerprint, Is.EqualTo(replayTwo.Fingerprint));
            Assert.That(replayOne.Snapshot.Fingerprint,
                Is.EqualTo(first.Snapshot.Fingerprint));
            Assert.That(replayTwo.Snapshot.Fingerprint,
                Is.EqualTo(first.Snapshot.Fingerprint));
            Assert.That(runtime.Snapshot.Fingerprint, Is.EqualTo(currentFingerprint));
            Assert.That(runtime.Snapshot.AcceptedFacts.Count, Is.EqualTo(2));
        }

        [Test]
        public void ChangedUnsupportedFact_UnderSameDeliveryOperation_Conflicts()
        {
            TestRunPorts ports;
            ConditionRuntimeAuthorityV1 runtime = CreateRuntime(
                "run.alpha",
                1L,
                new[] { Player("a", 1L, KillDefinition(1, 10L, 20L)) },
                out ports);
            ports.CurrentTick = 1L;
            AcceptedGameplayFactDeliveryV1 firstDelivery =
                UnsupportedDelivery(
                    "delivery.invalid",
                    new UnsupportedPayloadFact("payload-a"),
                    1L);
            AcceptedGameplayFactDeliveryV1 changedDelivery =
                UnsupportedDelivery(
                    "delivery.invalid",
                    new UnsupportedPayloadFact("payload-b"),
                    1L);

            ConditionFactIngestionResultV1 first = runtime.Ingest(firstDelivery);
            ConditionFactIngestionResultV1 conflict =
                runtime.Ingest(changedDelivery);

            Assert.That(first.Status,
                Is.EqualTo(ConditionFactIngestionStatusV1.Rejected));
            Assert.That(conflict.Status,
                Is.EqualTo(ConditionFactIngestionStatusV1.ConflictingDuplicate));
            Assert.That(conflict.DiagnosticCode,
                Is.EqualTo("condition-delivery-operation-conflicting-duplicate"));
            Assert.That(conflict.Snapshot.Fingerprint,
                Is.EqualTo(first.Snapshot.Fingerprint));
            Assert.That(runtime.Snapshot.AcceptedFacts, Is.Empty);
        }

        [Test]
        public void ChangedInvalidEnemyDeath_WithSameDiagnostic_Conflicts()
        {
            TestRunPorts ports;
            ConditionRuntimeAuthorityV1 runtime = CreateRuntime(
                "run.alpha",
                1L,
                new[] { Player("a", 1L, KillDefinition(1, 10L, 20L)) },
                out ports);
            ports.CurrentTick = 2L;
            EnemyDeathFactV1 firstDeath = Death(
                "run.alpha",
                "a",
                1,
                1L,
                targetSuffix: "first-invalid");
            EnemyDeathFactV1 changedDeath = Death(
                "run.alpha",
                "a",
                1,
                1L,
                targetSuffix: "second-invalid");

            ConditionFactIngestionResultV1 first = runtime.Ingest(
                Delivery(
                    "delivery.invalid-death",
                    firstDeath,
                    "run.beta",
                    "a",
                    1L,
                    2L));
            ConditionFactIngestionResultV1 conflict = runtime.Ingest(
                Delivery(
                    "delivery.invalid-death",
                    changedDeath,
                    "run.beta",
                    "a",
                    1L,
                    2L));

            Assert.That(first.Status,
                Is.EqualTo(ConditionFactIngestionStatusV1.Rejected));
            Assert.That(first.DiagnosticCode,
                Is.EqualTo("condition-enemy-death-run-mismatch"));
            Assert.That(conflict.Status,
                Is.EqualTo(ConditionFactIngestionStatusV1.ConflictingDuplicate));
            Assert.That(conflict.DiagnosticCode,
                Is.EqualTo("condition-delivery-operation-conflicting-duplicate"));
            Assert.That(conflict.Snapshot.Fingerprint,
                Is.EqualTo(first.Snapshot.Fingerprint));
            Assert.That(runtime.Snapshot.AcceptedFacts, Is.Empty);
        }

        [Test]
        public void Advance_TwoParticipantsWithRegressedClock_FailsBeforeAnyMutation()
        {
            TestRunPorts ports;
            ConditionEffectRuntimeDefinitionV1 definition =
                KillDefinition(
                    1,
                    20L,
                    30L,
                    StatusEffectStackingPolicyV1.Refresh);
            ConditionRuntimeAuthorityV1 runtime = CreateRuntime(
                "run.alpha",
                1L,
                new[]
                {
                    Player("a", 1L, definition),
                    Player("b", 1L, definition),
                },
                out ports);
            DeliverKill(runtime, ports, "a", 1, 2L);
            DeliverKill(runtime, ports, "b", 1, 5L);
            ports.CurrentTick = 5L;
            ConditionRuntimeSnapshotV1 before = runtime.Snapshot;

            ports.CurrentTick = 3L;
            InvalidOperationException rejection = Assert.Throws<InvalidOperationException>(
                () => runtime.Advance("advance.regressed"));

            Assert.That(rejection.Message,
                Does.StartWith("condition-runtime-advance-tick-stale:"));
            ports.CurrentTick = 5L;
            ConditionRuntimeSnapshotV1 afterFailure = runtime.Snapshot;
            Assert.That(afterFailure.Fingerprint, Is.EqualTo(before.Fingerprint));
            Assert.That(Participant(afterFailure, "a").StatusEffects.Fingerprint,
                Is.EqualTo(Participant(before, "a").StatusEffects.Fingerprint));
            Assert.That(Participant(afterFailure, "b").StatusEffects.Fingerprint,
                Is.EqualTo(Participant(before, "b").StatusEffects.Fingerprint));

            ports.CurrentTick = 6L;
            ConditionRuntimeSnapshotV1 accepted =
                runtime.Advance("advance.regressed");
            Assert.That(Participant(accepted, "a").StatusEffects.LatestAcceptedTick,
                Is.EqualTo(6L));
            Assert.That(Participant(accepted, "b").StatusEffects.LatestAcceptedTick,
                Is.EqualTo(6L));
        }

        private static AcceptedGameplayFactDeliveryV1 UnsupportedDelivery(
            string operationId,
            UnsupportedPayloadFact fact,
            long tick)
        {
            return new AcceptedGameplayFactDeliveryV1(
                operationId,
                fact,
                Id("run.alpha"),
                1L,
                Id("actor.a"),
                Id("participant.a"),
                Id("character.a"),
                1L,
                tick);
        }

        private sealed class UnsupportedPayloadFact
        {
            public UnsupportedPayloadFact(string value)
            {
                Value = value;
            }

            public string Value { get; }
        }
    }
}
