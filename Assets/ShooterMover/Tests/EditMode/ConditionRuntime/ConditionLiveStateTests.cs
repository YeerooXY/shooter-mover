using System;
using System.Linq;
using NUnit.Framework;
using ShooterMover.ConditionRuntime;
using ShooterMover.Domain.Characters.Stats;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Enemies;
using ShooterMover.Domain.Modifiers.StatusEffects;
using ShooterMover.EnemyRuntimeComposition;

namespace ShooterMover.Tests.EditMode.ConditionRuntime
{
    public sealed partial class ConditionLiveStateTests
    {
        private const string OutgoingDamageTarget =
            DerivedStatTargetIds.OutgoingDamageMultiplier;

        [Test]
        public void ReferenceFixture_HasStableDefinitionAndObservedFactFingerprints()
        {
            ConditionEffectLiveDefinition definition =
                KillDefinition(3, 10L, 8L);
            var registry = new AcceptedGameplayFactBridgeRegistry(
                new IAcceptedGameplayFactBridge[]
                {
                    new EnemyDeathConditionFactBridge(),
                });
            TestRunPorts ports;
            ConditionLiveState runtime = CreateRuntime(
                "run.alpha",
                1L,
                new[] { Player("a", 1L, definition) },
                out ports);
            ConditionFactIngestionResult result =
                DeliverKill(runtime, ports, "a", 1, 2L);

            Assert.That(
                definition.Fingerprint,
                Is.EqualTo(
                    "dbd51e08f25fcd54d271dff5567071d60d9007505b2ee7a90f88a055eda8f6e0"));
            Assert.That(
                registry.Fingerprint,
                Is.EqualTo(
                    "bc7d35c8ed47a43969b83b3ddbf22c4251c99348d38678b1876651f5fe622330"));
            Assert.That(
                result.ObservedFact.Fingerprint,
                Is.EqualTo(
                    "6756f952c38dc7b97b37d3b2a9d5a0d536bad876e9ad9621889da8932e3c9bf8"));
        }

        [Test]
        public void RequiredKillsInsideWindow_ActivateOneEffectExactlyOnce()
        {
            TestRunPorts ports;
            ConditionLiveState runtime = CreateRuntime(
                "run.alpha",
                1L,
                new[] { Player("a", 1L, KillDefinition(3, 10L, 8L)) },
                out ports);

            Assert.That(DeliverKill(runtime, ports, "a", 1, 1L).EffectResults.Count,
                Is.EqualTo(0));
            Assert.That(DeliverKill(runtime, ports, "a", 2, 4L).EffectResults.Count,
                Is.EqualTo(0));
            ConditionFactIngestionResult third =
                DeliverKill(runtime, ports, "a", 3, 7L);

            Assert.That(third.Status, Is.EqualTo(ConditionFactIngestionStatus.Applied));
            Assert.That(third.ConditionResult.Activations.Count, Is.EqualTo(1));
            Assert.That(third.EffectResults.Count, Is.EqualTo(1));
            Assert.That(Participant(third.Snapshot, "a").StatusEffects.ActiveEffects.Count,
                Is.EqualTo(1));
            Assert.That(OutgoingDamage(third.Snapshot, "a"), Is.EqualTo(1.5m));
        }

        [Test]
        public void KillsOutsideWindow_DoNotActivate()
        {
            TestRunPorts ports;
            ConditionLiveState runtime = CreateRuntime(
                "run.alpha",
                1L,
                new[] { Player("a", 1L, KillDefinition(3, 5L, 8L)) },
                out ports);

            DeliverKill(runtime, ports, "a", 1, 0L);
            DeliverKill(runtime, ports, "a", 2, 10L);
            ConditionFactIngestionResult third =
                DeliverKill(runtime, ports, "a", 3, 20L);

            Assert.That(third.ConditionResult.Activations.Count, Is.EqualTo(0));
            Assert.That(Participant(third.Snapshot, "a").StatusEffects.ActiveEffects,
                Is.Empty);
            Assert.That(OutgoingDamage(third.Snapshot, "a"), Is.EqualTo(1m));
        }

        [Test]
        public void ExactDuplicateDeathDelivery_DoesNotIncrementWindow()
        {
            TestRunPorts ports;
            ConditionLiveState runtime = CreateRuntime(
                "run.alpha",
                1L,
                new[] { Player("a", 1L, KillDefinition(2, 10L, 8L)) },
                out ports);
            EnemyDeathFact death = Death("run.alpha", "a", 1, 1L);
            ports.CurrentTick = 2L;
            AcceptedGameplayFactDelivery delivery = Delivery(
                "delivery.a.1",
                death,
                "run.alpha",
                "a",
                1L,
                2L);

            ConditionFactIngestionResult first = runtime.Ingest(delivery);
            string beforeReplay = first.Snapshot.Fingerprint;
            ConditionFactIngestionResult replay = runtime.Ingest(delivery);
            ConditionFactIngestionResult replayThroughNewDelivery = runtime.Ingest(
                Delivery(
                    "delivery.a.1.retry",
                    death,
                    "run.alpha",
                    "a",
                    1L,
                    2L));
            ConditionFactIngestionResult secondUnique =
                DeliverKill(runtime, ports, "a", 2, 3L);

            Assert.That(replay.Status,
                Is.EqualTo(ConditionFactIngestionStatus.ExactDuplicateNoChange));
            Assert.That(replay.Snapshot.Fingerprint, Is.EqualTo(beforeReplay));
            Assert.That(replayThroughNewDelivery.Status,
                Is.EqualTo(ConditionFactIngestionStatus.ExactDuplicateNoChange));
            Assert.That(replayThroughNewDelivery.Snapshot.Fingerprint,
                Is.EqualTo(beforeReplay));
            Assert.That(secondUnique.ConditionResult.Activations.Count, Is.EqualTo(1));
            Assert.That(secondUnique.Snapshot.AcceptedFacts.Count, Is.EqualTo(2));
        }

        [Test]
        public void ConflictingDuplicateDeath_RejectsWithoutMutation()
        {
            TestRunPorts ports;
            ConditionLiveState runtime = CreateRuntime(
                "run.alpha",
                1L,
                new[] { Player("a", 1L, KillDefinition(2, 10L, 8L)) },
                out ports);
            EnemyDeathFact original = Death("run.alpha", "a", 1, 1L);
            ports.CurrentTick = 2L;
            ConditionFactIngestionResult accepted = runtime.Ingest(
                Delivery("delivery.a.1", original, "run.alpha", "a", 1L, 2L));
            string fingerprint = accepted.Snapshot.Fingerprint;

            EnemyDeathFact changed = Death(
                "run.alpha",
                "a",
                1,
                1L,
                targetSuffix: "changed");
            ConditionFactIngestionResult conflict = runtime.Ingest(
                Delivery("delivery.a.conflict", changed, "run.alpha", "a", 1L, 2L));

            Assert.That(conflict.Status,
                Is.EqualTo(ConditionFactIngestionStatus.ConflictingDuplicate));
            Assert.That(conflict.DiagnosticCode,
                Is.EqualTo("condition-source-fact-conflicting-duplicate"));
            Assert.That(conflict.Snapshot.Fingerprint, Is.EqualTo(fingerprint));
            Assert.That(conflict.Snapshot.AcceptedFacts.Count, Is.EqualTo(1));
        }

        [Test]
        public void ConflictingDeliveryOperationId_RejectsWithoutMutation()
        {
            TestRunPorts ports;
            ConditionLiveState runtime = CreateRuntime(
                "run.alpha",
                1L,
                new[] { Player("a", 1L, KillDefinition(2, 10L, 8L)) },
                out ports);
            ports.CurrentTick = 2L;
            ConditionFactIngestionResult accepted = runtime.Ingest(
                Delivery(
                    "delivery.shared",
                    Death("run.alpha", "a", 1, 1L),
                    "run.alpha",
                    "a",
                    1L,
                    2L));
            string fingerprint = accepted.Snapshot.Fingerprint;

            ConditionFactIngestionResult conflict = runtime.Ingest(
                Delivery(
                    "delivery.shared",
                    Death("run.alpha", "a", 2, 1L),
                    "run.alpha",
                    "a",
                    1L,
                    2L));

            Assert.That(conflict.Status,
                Is.EqualTo(ConditionFactIngestionStatus.ConflictingDuplicate));
            Assert.That(conflict.DiagnosticCode,
                Is.EqualTo("condition-delivery-operation-conflicting-duplicate"));
            Assert.That(conflict.Snapshot.Fingerprint, Is.EqualTo(fingerprint));
            Assert.That(conflict.Snapshot.AcceptedFacts.Count, Is.EqualTo(1));
        }

        [Test]
        public void Participants_MaintainIndependentWindowsAndEffects()
        {
            TestRunPorts ports;
            ConditionEffectLiveDefinition definition =
                KillDefinition(2, 10L, 8L);
            ConditionLiveState runtime = CreateRuntime(
                "run.alpha",
                1L,
                new[]
                {
                    Player("a", 1L, definition),
                    Player("b", 1L, definition),
                },
                out ports);

            DeliverKill(runtime, ports, "a", 1, 1L);
            DeliverKill(runtime, ports, "b", 1, 2L);
            ConditionFactIngestionResult aSecond =
                DeliverKill(runtime, ports, "a", 2, 3L);

            Assert.That(Participant(aSecond.Snapshot, "a").StatusEffects.ActiveEffects.Count,
                Is.EqualTo(1));
            Assert.That(Participant(aSecond.Snapshot, "b").StatusEffects.ActiveEffects,
                Is.Empty);
            Assert.That(OutgoingDamage(aSecond.Snapshot, "a"), Is.EqualTo(1.5m));
            Assert.That(OutgoingDamage(aSecond.Snapshot, "b"), Is.EqualTo(1m));
        }

        [Test]
        public void DifferentRuns_AreIsolatedAndCrossRunFactRejects()
        {
            TestRunPorts firstPorts;
            TestRunPorts secondPorts;
            ConditionLiveState first = CreateRuntime(
                "run.alpha",
                1L,
                new[] { Player("a", 1L, KillDefinition(1, 10L, 8L)) },
                out firstPorts);
            ConditionLiveState second = CreateRuntime(
                "run.beta",
                1L,
                new[] { Player("a", 1L, KillDefinition(1, 10L, 8L)) },
                out secondPorts);

            ConditionFactIngestionResult firstAccepted =
                DeliverKill(first, firstPorts, "a", 1, 1L);
            EnemyDeathFact alphaDeath = Death("run.alpha", "a", 2, 1L);
            secondPorts.CurrentTick = 2L;
            ConditionFactIngestionResult crossRun = second.Ingest(
                Delivery("delivery.cross-run", alphaDeath, "run.alpha", "a", 1L, 2L));

            Assert.That(firstAccepted.Snapshot.AcceptedFacts.Count, Is.EqualTo(1));
            Assert.That(crossRun.Status, Is.EqualTo(ConditionFactIngestionStatus.Rejected));
            Assert.That(crossRun.DiagnosticCode,
                Is.EqualTo("condition-enemy-death-run-mismatch"));
            Assert.That(second.Snapshot.AcceptedFacts, Is.Empty);
        }

        [Test]
        public void EffectExpiry_RemovesOutgoingDamageModifierAtAuthoritativeTick()
        {
            TestRunPorts ports;
            ConditionLiveState runtime = CreateRuntime(
                "run.alpha",
                1L,
                new[] { Player("a", 1L, KillDefinition(1, 10L, 5L)) },
                out ports);
            ConditionFactIngestionResult applied =
                DeliverKill(runtime, ports, "a", 1, 2L);
            long expiry = Participant(applied.Snapshot, "a")
                .StatusEffects.ActiveEffects[0].Stacks[0].ExpiresAtTickExclusive;

            ports.CurrentTick = expiry;
            ConditionLiveSnapshot expired = runtime.Advance("advance.expiry");

            Assert.That(Participant(expired, "a").StatusEffects.ActiveEffects, Is.Empty);
            Assert.That(OutgoingDamage(expired, "a"), Is.EqualTo(1m));
        }

        [Test]
        public void RepeatedActivation_FollowsAuthoredRefreshPolicy()
        {
            TestRunPorts ports;
            ConditionLiveState runtime = CreateRuntime(
                "run.alpha",
                1L,
                new[]
                {
                    Player(
                        "a",
                        1L,
                        KillDefinition(
                            1,
                            10L,
                            5L,
                            StatusEffectStackingPolicy.Refresh)),
                },
                out ports);

            ConditionFactIngestionResult first =
                DeliverKill(runtime, ports, "a", 1, 1L);
            string stackId = Participant(first.Snapshot, "a")
                .StatusEffects.ActiveEffects[0].Stacks[0].StackId;
            ConditionFactIngestionResult second =
                DeliverKill(runtime, ports, "a", 2, 3L);
            var stack = Participant(second.Snapshot, "a")
                .StatusEffects.ActiveEffects[0].Stacks[0];

            Assert.That(second.EffectResults.Count, Is.EqualTo(1));
            Assert.That(second.EffectResults[0].Action,
                Is.EqualTo(StatusEffectCommandAction.Refreshed));
            Assert.That(Participant(second.Snapshot, "a")
                .StatusEffects.ActiveEffects[0].Stacks.Count, Is.EqualTo(1));
            Assert.That(stack.StackId, Is.EqualTo(stackId));
            Assert.That(stack.AppliedAtTick, Is.EqualTo(3L));
            Assert.That(stack.ExpiresAtTickExclusive, Is.EqualTo(8L));
        }

        [Test]
        public void RunReconstruction_ClearsWindowsAndTemporaryEffects()
        {
            TestRunPorts ports;
            ConditionEffectLiveDefinition definition =
                KillDefinition(2, 10L, 8L);
            ConditionLiveState runtime = CreateRuntime(
                "run.alpha",
                1L,
                new[] { Player("a", 1L, definition) },
                out ports);
            DeliverKill(runtime, ports, "a", 1, 1L);
            DeliverKill(runtime, ports, "a", 2, 2L);
            Assert.That(runtime.Snapshot.AcceptedFacts.Count, Is.EqualTo(2));
            Assert.That(Participant(runtime.Snapshot, "a").StatusEffects.ActiveEffects.Count,
                Is.EqualTo(1));

            ports.CurrentTick = 0L;
            ports.Current = new ConditionRunLifecycleSnapshot(Id("run.beta"), 2L);
            ConditionRunDefinition next = new ConditionRunDefinition(
                ports.Current,
                new[] { Player("a", 2L, definition) });
            ConditionRunReconstructionResult reset = runtime.Reconstruct(
                new ConditionRunReconstructionCommand(
                    "reconstruct.beta",
                    Id("run.alpha"),
                    1L,
                    next));
            ConditionFactIngestionResult firstNewRun =
                DeliverKill(
                    runtime,
                    ports,
                    "a",
                    1,
                    1L,
                    runId: "run.beta",
                    lifecycle: 2L,
                    runGeneration: 2L);

            Assert.That(reset.Status, Is.EqualTo(ConditionFactIngestionStatus.Applied));
            Assert.That(reset.Snapshot.AcceptedFacts, Is.Empty);
            Assert.That(Participant(reset.Snapshot, "a").StatusEffects.ActiveEffects, Is.Empty);
            Assert.That(firstNewRun.ConditionResult.Activations, Is.Empty);
        }

        [Test]
        public void RunReconstruction_PreservesPersistentSkillAllocationInput()
        {
            TestRunPorts ports;
            ConditionEffectLiveDefinition definition =
                KillDefinition(1, 10L, 8L);
            const string skillFingerprint = "skills.persistent-a";
            ConditionLiveState runtime = CreateRuntime(
                "run.alpha",
                1L,
                new[] { Player("a", 1L, definition, skillFingerprint) },
                out ports);

            ports.Current = new ConditionRunLifecycleSnapshot(Id("run.beta"), 2L);
            ports.CurrentTick = 0L;
            ConditionRunDefinition next = new ConditionRunDefinition(
                ports.Current,
                new[] { Player("a", 2L, definition, skillFingerprint) });
            ConditionRunReconstructionResult reset = runtime.Reconstruct(
                new ConditionRunReconstructionCommand(
                    "reconstruct.skills",
                    Id("run.alpha"),
                    1L,
                    next));

            Assert.That(Participant(reset.Snapshot, "a")
                .Definition.PersistentSkillAllocationFingerprint,
                Is.EqualTo(skillFingerprint));
        }

        [Test]
        public void PreviousRunAndStaleActorLifecycleFacts_RejectAfterReconstruction()
        {
            TestRunPorts ports;
            ConditionEffectLiveDefinition definition =
                KillDefinition(1, 10L, 8L);
            ConditionLiveState runtime = CreateRuntime(
                "run.alpha",
                1L,
                new[] { Player("a", 1L, definition) },
                out ports);

            ports.Current = new ConditionRunLifecycleSnapshot(Id("run.beta"), 2L);
            ports.CurrentTick = 0L;
            runtime.Reconstruct(
                new ConditionRunReconstructionCommand(
                    "reconstruct.stale",
                    Id("run.alpha"),
                    1L,
                    new ConditionRunDefinition(
                        ports.Current,
                        new[] { Player("a", 2L, definition) })));

            EnemyDeathFact oldRun = Death("run.alpha", "a", 1, 1L);
            ports.CurrentTick = 1L;
            ConditionFactIngestionResult oldRunResult = runtime.Ingest(
                Delivery("delivery.old-run", oldRun, "run.alpha", "a", 1L, 1L));
            EnemyDeathFact newRun = Death("run.beta", "a", 2, 2L);
            ConditionFactIngestionResult staleLifecycle = runtime.Ingest(
                Delivery(
                    "delivery.stale-lifecycle",
                    newRun,
                    "run.beta",
                    "a",
                    1L,
                    1L,
                    2L));

            Assert.That(oldRunResult.Status, Is.EqualTo(ConditionFactIngestionStatus.Rejected));
            Assert.That(staleLifecycle.Status,
                Is.EqualTo(ConditionFactIngestionStatus.Rejected));
            Assert.That(staleLifecycle.DiagnosticCode,
                Is.EqualTo("condition-fact-source-lifecycle-stale"));
            Assert.That(runtime.Snapshot.AcceptedFacts, Is.Empty);
        }

    }
}
