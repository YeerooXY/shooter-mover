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
        [Test]
        public void UnsupportedFactType_FailsClosedWithDiagnostic()
        {
            TestRunPorts ports;
            ConditionLiveState runtime = CreateRuntime(
                "run.alpha",
                1L,
                new[] { Player("a", 1L, KillDefinition(1, 10L, 8L)) },
                out ports);
            ports.CurrentTick = 1L;
            var unsupported = new AcceptedGameplayFactDelivery(
                "delivery.unsupported",
                new UnsupportedFact(),
                Id("run.alpha"),
                1L,
                Id("actor.a"),
                Id("participant.a"),
                Id("character.a"),
                1L,
                1L);

            ConditionFactIngestionResult result = runtime.Ingest(unsupported);

            Assert.That(result.Status,
                Is.EqualTo(ConditionFactIngestionStatus.Rejected));
            Assert.That(result.DiagnosticCode,
                Is.EqualTo("condition-fact-type-unsupported"));
            Assert.That(result.Snapshot.AcceptedFacts, Is.Empty);
        }

        [Test]
        public void UnrelatedObjectiveFixture_UsesSamePublicAuthorityAndAdapterRegistry()
        {
            TestRunPorts ports = new TestRunPorts(
                new ConditionRunLifecycleSnapshot(Id("run.alpha"), 1L));
            ConditionEffectLiveDefinition objectiveDefinition =
                new FactWindowEffectFixture(
                    "condition.objective-burst",
                    "status-effect.objective-burst",
                    "gameplay.objective-collected",
                    2,
                    20L,
                    4L,
                    1.25m)
                .Build(
                    "condition-runtime.objective-fixture",
                    "1.0.0",
                    "conditional-source.objective-fixture");
            var adapters = new AcceptedGameplayFactBridgeRegistry(
                new IAcceptedGameplayFactBridge[]
                {
                    new EnemyDeathConditionFactBridge(),
                    new ObjectiveFactBridge(),
                });
            var runtime = new ConditionLiveState(
                ports,
                ports,
                adapters,
                new ConditionRunDefinition(
                    ports.Current,
                    new[] { Player("a", 1L, objectiveDefinition) }));

            ports.CurrentTick = 1L;
            ConditionFactIngestionResult first = runtime.Ingest(
                ObjectiveDelivery("delivery.objective.1", 1, 1L));
            ports.CurrentTick = 2L;
            ConditionFactIngestionResult second = runtime.Ingest(
                ObjectiveDelivery("delivery.objective.2", 2, 2L));

            Assert.That(first.ConditionResult.Activations, Is.Empty);
            Assert.That(second.ConditionResult.Activations.Count, Is.EqualTo(1));
            Assert.That(OutgoingDamage(second.Snapshot, "a"), Is.EqualTo(1.25m));
        }

        [Test]
        public void DeterministicReplay_ProducesEquivalentSnapshotsAndFingerprints()
        {
            TestRunPorts firstPorts;
            TestRunPorts secondPorts;
            ConditionEffectLiveDefinition definition =
                KillDefinition(3, 10L, 6L);
            ConditionLiveState first = CreateRuntime(
                "run.alpha",
                1L,
                new[] { Player("a", 1L, definition) },
                out firstPorts);
            ConditionLiveState second = CreateRuntime(
                "run.alpha",
                1L,
                new[] { Player("a", 1L, definition) },
                out secondPorts);

            for (int index = 1; index <= 3; index++)
            {
                DeliverKill(first, firstPorts, "a", index, index * 2L);
                DeliverKill(second, secondPorts, "a", index, index * 2L);
            }
            string beforeDuplicate = first.Snapshot.Fingerprint;
            EnemyDeathFact third = Death("run.alpha", "a", 3, 1L);
            firstPorts.CurrentTick = 6L;
            ConditionFactIngestionResult replay = first.Ingest(
                Delivery(
                    "delivery.a.3",
                    third,
                    "run.alpha",
                    "a",
                    1L,
                    6L));

            Assert.That(first.Snapshot.Fingerprint,
                Is.EqualTo(second.Snapshot.Fingerprint));
            Assert.That(replay.Snapshot.Fingerprint, Is.EqualTo(beforeDuplicate));
            Assert.That(
                first.Snapshot.AcceptedFacts.Select(item => item.Fingerprint),
                Is.EqualTo(
                    second.Snapshot.AcceptedFacts.Select(
                        item => item.Fingerprint)));
        }

        private static ConditionLiveState CreateRuntime(
            string runId,
            long runGeneration,
            ConditionLiveParticipantDefinition[] participants,
            out TestRunPorts ports)
        {
            ports = new TestRunPorts(
                new ConditionRunLifecycleSnapshot(Id(runId), runGeneration));
            return new ConditionLiveState(
                ports,
                ports,
                new AcceptedGameplayFactBridgeRegistry(
                    new IAcceptedGameplayFactBridge[]
                    {
                        new EnemyDeathConditionFactBridge(),
                    }),
                new ConditionRunDefinition(ports.Current, participants));
        }

        private static ConditionEffectLiveDefinition KillDefinition(
            int requiredKills,
            long windowTicks,
            long durationTicks,
            StatusEffectStackingPolicy stackingPolicy =
                StatusEffectStackingPolicy.Ignore)
        {
            return new FactWindowEffectFixture(
                "condition.enemy-kill-burst",
                "status-effect.enemy-kill-burst",
                ConditionLiveFactTypeIds.EnemyKilled,
                requiredKills,
                windowTicks,
                durationTicks,
                1.5m,
                stackingPolicy)
                .Build(
                    "condition-runtime.enemy-kill-fixture",
                    "1.0.0",
                    "conditional-source.enemy-kill-fixture");
        }

        private static ConditionLiveParticipantDefinition Player(
            string suffix,
            long lifecycle,
            ConditionEffectLiveDefinition definition,
            string skillFingerprint = "skills.fixture")
        {
            return new ConditionLiveParticipantDefinition(
                Id("participant." + suffix),
                Id("character." + suffix),
                Id("actor." + suffix),
                lifecycle,
                skillFingerprint,
                definition);
        }

        private static ConditionFactIngestionResult DeliverKill(
            ConditionLiveState runtime,
            TestRunPorts ports,
            string playerSuffix,
            int ordinal,
            long tick,
            string runId = "run.alpha",
            long lifecycle = 1L,
            long runGeneration = 1L)
        {
            ports.CurrentTick = tick;
            return runtime.Ingest(
                Delivery(
                    "delivery." + playerSuffix + "." + ordinal,
                    Death(runId, playerSuffix, ordinal, lifecycle),
                    runId,
                    playerSuffix,
                    lifecycle,
                    tick,
                    runGeneration));
        }

        private static AcceptedGameplayFactDelivery Delivery(
            string operationId,
            EnemyDeathFact death,
            string runId,
            string playerSuffix,
            long lifecycle,
            long tick,
            long runGeneration = 1L)
        {
            return new AcceptedGameplayFactDelivery(
                operationId,
                death,
                Id(runId),
                runGeneration,
                Id("actor." + playerSuffix),
                Id("participant." + playerSuffix),
                Id("character." + playerSuffix),
                lifecycle,
                tick);
        }

        private static EnemyDeathFact Death(
            string runId,
            string playerSuffix,
            int ordinal,
            long targetLifecycle,
            string targetSuffix = null)
        {
            string enemySuffix = targetSuffix ?? ordinal.ToString();
            var identity = new EnemyLiveIdentity(
                Id("enemy." + enemySuffix),
                Id("participant.enemy-" + enemySuffix),
                Id(runId),
                Id("room-runtime.main"),
                Id("room.main"),
                Id("placement.enemy-" + enemySuffix));
            return new EnemyDeathFact(
                Id("death." + playerSuffix + "-" + ordinal),
                Id("damage." + playerSuffix + "-" + ordinal),
                identity,
                Id("enemy-definition.fixture"),
                1,
                targetLifecycle,
                Id("actor." + playerSuffix),
                Id("participant." + playerSuffix),
                Id("experience-profile.fixture"),
                Id("drop-profile.fixture"),
                EnemyActorDeathCause.IncomingDamage);
        }

        private static ConditionParticipantSnapshot Participant(
            ConditionLiveSnapshot snapshot,
            string suffix)
        {
            return snapshot.Participants.Single(item =>
                item.Definition.ParticipantId == Id("participant." + suffix));
        }

        private static decimal OutgoingDamage(
            ConditionLiveSnapshot snapshot,
            string suffix)
        {
            return Participant(snapshot, suffix)
                .StatusEffects.ModifierProjection
                .Evaluate(OutgoingDamageTarget, 1m)
                .FinalValue;
        }

        private static AcceptedGameplayFactDelivery ObjectiveDelivery(
            string operationId,
            int ordinal,
            long tick)
        {
            return new AcceptedGameplayFactDelivery(
                operationId,
                new ObjectiveFact(
                    "objective-fact." + ordinal,
                    Id("objective.relay-" + ordinal)),
                Id("run.alpha"),
                1L,
                Id("actor.a"),
                Id("participant.a"),
                Id("character.a"),
                1L,
                tick);
        }

        private static StableId Id(string value)
        {
            return StableId.Parse(value);
        }

        private sealed class TestRunPorts : IConditionRunClock,
            IConditionRunLifecycle
        {
            public TestRunPorts(ConditionRunLifecycleSnapshot current)
            {
                Current = current;
            }

            public long CurrentTick { get; set; }
            public ConditionRunLifecycleSnapshot Current { get; set; }
        }

        private sealed class UnsupportedFact
        {
        }

        private sealed class ObjectiveFact
        {
            public ObjectiveFact(string factId, StableId objectiveId)
            {
                FactId = factId;
                ObjectiveId = objectiveId;
            }

            public string FactId { get; }
            public StableId ObjectiveId { get; }
        }

        private sealed class ObjectiveFactBridge :
            IAcceptedGameplayFactBridge,
            IAcceptedGameplayFactSourceFingerprint
        {
            public Type SourceFactRuntimeType
            {
                get { return typeof(ObjectiveFact); }
            }

            public string SourceFactTypeId
            {
                get { return "objective-runtime.collected-v1"; }
            }

            public string ComputeSourceFactFingerprint(object sourceFact)
            {
                ObjectiveFact fact = sourceFact as ObjectiveFact;
                if (fact == null)
                    throw new ArgumentException(
                        "The objective adapter can fingerprint only ObjectiveFact values.",
                        nameof(sourceFact));
                return ConditionSourceFactFingerprint.Compute(fact);
            }

            public bool TryAdapt(
                AcceptedGameplayFactDelivery delivery,
                out ConditionObservedGameplayFact observedFact,
                out string diagnosticCode)
            {
                ObjectiveFact fact = delivery == null
                    ? null
                    : delivery.SourceFact as ObjectiveFact;
                if (fact == null)
                {
                    observedFact = null;
                    diagnosticCode = "condition-objective-fact-invalid";
                    return false;
                }

                observedFact = new ConditionObservedGameplayFact(
                    fact.FactId,
                    SourceFactTypeId,
                    fact.FactId,
                    "gameplay.objective-collected",
                    delivery.RunId,
                    delivery.RunLifecycleGeneration,
                    delivery.SourceActorId,
                    delivery.SubjectParticipantId,
                    delivery.SourceCharacterId,
                    fact.ObjectiveId,
                    delivery.SubjectParticipantId,
                    delivery.SourceActorLifecycleGeneration,
                    1L,
                    delivery.AuthoritativeTick);
                diagnosticCode = string.Empty;
                return true;
            }
        }
    }
}
