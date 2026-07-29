using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ShooterMover.Application.Runs.Session;
using ShooterMover.Contracts.Flow.Session;
using ShooterMover.Contracts.Missions.Results;
using ShooterMover.Domain.Characters.Stats;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Equipment;
using ShooterMover.Domain.Persistence.Accounts;
using ShooterMover.Domain.Progression.Skills;

namespace ShooterMover.Tests.EditMode.RunSessions
{
    public sealed class RunSessionDurableEndTests
    {
        [Test]
        public void AcceptedDurabilityEndsTheRunOnceAndExactCommandReplays()
        {
            Fixture fixture = Fixture.Create();
            int durableCalls = 0;

            RunSessionEndResult first = fixture.Run.EndWithDurableAcceptance(
                fixture.EndCommand,
                candidate =>
                {
                    durableCalls++;
                    Assert.That(candidate.Receipt, Is.Not.Null);
                    return RunSessionDurableAcceptanceResult.Accepted();
                });
            RunSessionEndResult replay = fixture.Run.EndWithDurableAcceptance(
                fixture.EndCommand,
                candidate =>
                {
                    Assert.Fail("Durability callback must not run for a successful exact replay.");
                    return RunSessionDurableAcceptanceResult.Accepted();
                });

            Assert.That(first.Status, Is.EqualTo(RunSessionEndStatus.Ended));
            Assert.That(replay, Is.SameAs(first));
            Assert.That(fixture.Run.LifecycleState,
                Is.EqualTo(RunSessionLifecycleState.Ended));
            Assert.That(fixture.Run.DurableEndState,
                Is.EqualTo(RunSessionDurableEndState.None));
            Assert.That(fixture.Run.PendingDurableEndCandidate, Is.Null);
            Assert.That(fixture.MissionResults.EndRunCallCount, Is.EqualTo(1));
            Assert.That(durableCalls, Is.EqualTo(1));
        }

        [Test]
        public void RetryableRejectionRetainsSameCandidateAndRetriesOnlyDurability()
        {
            Fixture fixture = Fixture.Create();
            RunSessionEndResult callbackCandidate = null;
            int durableCalls = 0;

            RunSessionEndResult rejected = fixture.Run.EndWithDurableAcceptance(
                fixture.EndCommand,
                candidate =>
                {
                    callbackCandidate = candidate;
                    durableCalls++;
                    return RunSessionDurableAcceptanceResult.Retryable(
                        "fixture-transient");
                });
            RunSessionEndResult retained = fixture.Run.PendingDurableEndCandidate;
            RunSessionEndResult accepted = fixture.Run.EndWithDurableAcceptance(
                fixture.EndCommand,
                candidate =>
                {
                    durableCalls++;
                    Assert.That(candidate, Is.SameAs(retained));
                    return RunSessionDurableAcceptanceResult.Accepted();
                });

            Assert.That(rejected.Status, Is.EqualTo(RunSessionEndStatus.Rejected));
            Assert.That(callbackCandidate, Is.SameAs(retained));
            Assert.That(retained, Is.Not.Null);
            Assert.That(fixture.Run.DurableEndState,
                Is.EqualTo(RunSessionDurableEndState.None));
            Assert.That(accepted.Status, Is.EqualTo(RunSessionEndStatus.Ended));
            Assert.That(fixture.MissionResults.EndRunCallCount, Is.EqualTo(1));
            Assert.That(durableCalls, Is.EqualTo(2));
        }

        [Test]
        public void RetryPreservesReceiptLocalSnapshotAndMissionResultExactly()
        {
            Fixture fixture = Fixture.Create();
            fixture.Player.Damage(25d);
            fixture.Run.ApplyLocalMutation(
                new RunLocalMutationCommand(
                    Id("operation.retry-counter"),
                    fixture.Run.RunStableId,
                    fixture.Run.LifecycleGeneration,
                    RunLocalMutationKind.IncrementCounter,
                    "kills",
                    3L,
                    "fixture-counter"));

            fixture.Run.EndWithDurableAcceptance(
                fixture.EndCommand,
                candidate => RunSessionDurableAcceptanceResult.Retryable(
                    "fixture-transient"));

            RunSessionEndResult firstCandidate =
                fixture.Run.PendingDurableEndCandidate;
            RunSessionEndReceipt firstReceipt = firstCandidate.Receipt;
            string localFingerprint = firstReceipt.LocalState.Fingerprint;
            string missionFingerprint = firstReceipt.MissionResult.Fingerprint;
            string receiptFingerprint = firstReceipt.Fingerprint;

            fixture.Run.EndWithDurableAcceptance(
                fixture.EndCommand,
                candidate =>
                {
                    Assert.That(candidate, Is.SameAs(firstCandidate));
                    Assert.That(candidate.Receipt, Is.SameAs(firstReceipt));
                    Assert.That(candidate.Receipt.Fingerprint,
                        Is.EqualTo(receiptFingerprint));
                    Assert.That(candidate.Receipt.LocalState.Fingerprint,
                        Is.EqualTo(localFingerprint));
                    Assert.That(candidate.Receipt.MissionResult.Fingerprint,
                        Is.EqualTo(missionFingerprint));
                    Assert.That(candidate.Receipt.LocalState.Counters["kills"],
                        Is.EqualTo(3L));
                    return RunSessionDurableAcceptanceResult.Accepted();
                });

            Assert.That(fixture.MissionResults.EndRunCallCount, Is.EqualTo(1));
        }

        [Test]
        public void TerminalPreparationFailureIsStickyAndNeverReentersAuthorities()
        {
            Fixture fixture = Fixture.Create();
            int durableCalls = 0;

            RunSessionEndResult first = fixture.Run.EndWithDurableAcceptance(
                fixture.EndCommand,
                candidate =>
                {
                    durableCalls++;
                    return RunSessionDurableAcceptanceResult.Terminal(
                        "fixture-terminal");
                });
            RunSessionEndResult retained =
                fixture.Run.PendingDurableEndCandidate;
            RunSessionEndResult second = fixture.Run.EndWithDurableAcceptance(
                fixture.EndCommand,
                candidate =>
                {
                    durableCalls++;
                    Assert.Fail("Sticky terminal state must not re-enter durability.");
                    return RunSessionDurableAcceptanceResult.Accepted();
                });

            Assert.That(first.Status, Is.EqualTo(RunSessionEndStatus.Rejected));
            Assert.That(second.Status, Is.EqualTo(RunSessionEndStatus.Rejected));
            Assert.That(second.Receipt, Is.SameAs(retained.Receipt));
            Assert.That(fixture.Run.PendingDurableEndCandidate, Is.SameAs(retained));
            Assert.That(fixture.Run.DurableEndState,
                Is.EqualTo(
                    RunSessionDurableEndState.TerminalPreparationFailure));
            Assert.That(fixture.Run.DurableEndDiagnostic,
                Is.EqualTo("fixture-terminal"));
            Assert.That(fixture.MissionResults.EndRunCallCount, Is.EqualTo(1));
            Assert.That(durableCalls, Is.EqualTo(1));
        }

        [Test]
        public void DurableUncertaintyIsStickyAndReportsDistinctState()
        {
            Fixture fixture = Fixture.Create();
            int durableCalls = 0;

            fixture.Run.EndWithDurableAcceptance(
                fixture.EndCommand,
                candidate =>
                {
                    durableCalls++;
                    return RunSessionDurableAcceptanceResult.Uncertain(
                        "fixture-uncertain");
                });
            RunSessionEndResult retained =
                fixture.Run.PendingDurableEndCandidate;
            RunSessionEndResult second = fixture.Run.EndWithDurableAcceptance(
                fixture.EndCommand,
                candidate =>
                {
                    durableCalls++;
                    Assert.Fail("Sticky uncertainty must not re-enter durability.");
                    return RunSessionDurableAcceptanceResult.Accepted();
                });

            Assert.That(second.Status, Is.EqualTo(RunSessionEndStatus.Rejected));
            Assert.That(second.Receipt, Is.SameAs(retained.Receipt));
            Assert.That(fixture.Run.DurableEndState,
                Is.EqualTo(RunSessionDurableEndState.DurableStateUncertain));
            Assert.That(fixture.Run.DurableEndDiagnostic,
                Is.EqualTo("fixture-uncertain"));
            Assert.That(fixture.MissionResults.EndRunCallCount, Is.EqualTo(1));
            Assert.That(durableCalls, Is.EqualTo(1));
        }

        [Test]
        public void ConflictingEndOperationRejectsWithoutReplacingCandidate()
        {
            Fixture fixture = Fixture.Create();

            fixture.Run.EndWithDurableAcceptance(
                fixture.EndCommand,
                candidate => RunSessionDurableAcceptanceResult.Retryable(
                    "fixture-transient"));
            RunSessionEndResult retained =
                fixture.Run.PendingDurableEndCandidate;
            var conflict = new EndRunSessionCommand(
                fixture.EndCommand.OperationStableId,
                fixture.Run.RunStableId,
                fixture.Run.LifecycleGeneration,
                MissionRunCompletionState.Failed,
                fixture.EndCommand.AuthoritativeTick);

            RunSessionEndResult result = fixture.Run.EndWithDurableAcceptance(
                conflict,
                candidate =>
                {
                    Assert.Fail("Conflicting command must not invoke durability.");
                    return RunSessionDurableAcceptanceResult.Accepted();
                });

            Assert.That(result.Status,
                Is.EqualTo(RunSessionEndStatus.ConflictingDuplicate));
            Assert.That(fixture.Run.PendingDurableEndCandidate, Is.SameAs(retained));
            Assert.That(fixture.MissionResults.EndRunCallCount, Is.EqualTo(1));
        }

        [Test]
        public void CallbackThrowBecomesStickyDurableUncertainty()
        {
            Fixture fixture = Fixture.Create();

            RunSessionEndResult result = fixture.Run.EndWithDurableAcceptance(
                fixture.EndCommand,
                candidate => throw new InvalidOperationException("fixture"));

            Assert.That(result.Status, Is.EqualTo(RunSessionEndStatus.Rejected));
            Assert.That(fixture.Run.DurableEndState,
                Is.EqualTo(RunSessionDurableEndState.DurableStateUncertain));
            Assert.That(fixture.Run.DurableEndDiagnostic,
                Does.StartWith("run-end-durable-acceptance-threw:"));
            Assert.That(fixture.Run.PendingDurableEndCandidate, Is.Not.Null);
            Assert.That(fixture.MissionResults.EndRunCallCount, Is.EqualTo(1));
        }

        [Test]
        public void NullCallbackResultBecomesStickyDurableUncertainty()
        {
            Fixture fixture = Fixture.Create();

            RunSessionEndResult result = fixture.Run.EndWithDurableAcceptance(
                fixture.EndCommand,
                candidate => null);

            Assert.That(result.Status, Is.EqualTo(RunSessionEndStatus.Rejected));
            Assert.That(fixture.Run.DurableEndState,
                Is.EqualTo(RunSessionDurableEndState.DurableStateUncertain));
            Assert.That(fixture.Run.DurableEndDiagnostic,
                Is.EqualTo("run-end-durable-acceptance-result-null"));
            Assert.That(fixture.Run.PendingDurableEndCandidate, Is.Not.Null);
            Assert.That(fixture.MissionResults.EndRunCallCount, Is.EqualTo(1));
        }

        private static StableId Id(string canonical)
        {
            return StableId.Parse(canonical);
        }

        private sealed class Fixture
        {
            private Fixture(
                RunSessionAggregate run,
                FakePlayerPort player,
                FakeMissionResultPort missionResults,
                EndRunSessionCommand endCommand)
            {
                Run = run;
                Player = player;
                MissionResults = missionResults;
                EndCommand = endCommand;
            }

            public RunSessionAggregate Run { get; }
            public FakePlayerPort Player { get; }
            public FakeMissionResultPort MissionResults { get; }
            public EndRunSessionCommand EndCommand { get; }

            public static Fixture Create()
            {
                var source = new FakeStartSource();
                var authority = new RunSessionState(source);
                StartRunSessionCommand start = source.Command(
                    "durable-end-start",
                    55L);
                RunSessionStartResult started = authority.Start(start);
                Assert.That(started.Status,
                    Is.EqualTo(RunSessionStartStatus.Started));
                RunSessionAggregate run;
                Assert.That(authority.TryGetRun(started.RunStableId, out run),
                    Is.True);
                FakeLiveBundle bundle = source.Bundle(started.RunStableId);
                var end = new EndRunSessionCommand(
                    Id("operation.durable-end"),
                    run.RunStableId,
                    run.LifecycleGeneration,
                    MissionRunCompletionState.Completed,
                    100L);
                return new Fixture(run, bundle.Player, bundle.MissionResults, end);
            }
        }

        private sealed class FakeStartSource : IRunSessionStartSource
        {
            private readonly Dictionary<StableId, FakeLiveBundle> bundles =
                new Dictionary<StableId, FakeLiveBundle>();

            public FakeStartSource()
            {
                Character = new CharacterInstanceSnapshot(
                    Id("character-instance.durable-end"),
                    Id("loadout-profile.striker"),
                    0,
                    "Durable End Pilot",
                    4L,
                    null);
            }

            public CharacterInstanceSnapshot Character { get; }

            public StartRunSessionCommand Command(
                string operationSuffix,
                long seed)
            {
                return new StartRunSessionCommand(
                    Id("operation." + operationSuffix),
                    null,
                    "durable-end-run-material",
                    Character.CharacterInstanceStableId,
                    Character.Revision,
                    Character.Fingerprint,
                    Id("mission-layout.level-1"),
                    Id("difficulty.normal"),
                    seed,
                    0L,
                    "event-context.none");
            }

            public FakeLiveBundle Bundle(StableId runStableId)
            {
                return bundles[runStableId];
            }

            public RunSessionStartMaterial Resolve(
                StartRunSessionCommand command,
                StableId resolvedRunStableId)
            {
                StableId definitionId =
                    Id("equipment-definition.durable-end-rifle");
                StableId qualityId = Id("quality.common");
                EquipmentDefinition definition = EquipmentDefinition.Create(
                    definitionId,
                    EquipmentCategoryIds.Gun,
                    Id("equipment-family.durable-end-rifle"),
                    "Durable End Rifle",
                    Id("gun.durable-end-rifle"),
                    InclusiveIntRange.Create(1, 100),
                    2,
                    new[]
                    {
                        EquipmentQualityTier.Create(qualityId, "Common", 1),
                    },
                    null);
                EquipmentInstance equipment = EquipmentInstance.Create(
                    Id("equipment-instance.durable-end-rifle"),
                    definitionId,
                    10,
                    qualityId,
                    null);
                PlayerRouteProfilePayload route =
                    PlayerRouteProfilePayload.Create(
                        Character.CharacterInstanceStableId,
                        Character.ClassDefinitionStableId,
                        new[]
                        {
                            equipment.InstanceId,
                            null,
                            null,
                            null,
                        });
                DerivedStatPolicy policy =
                    DerivedStatPolicy.CreateDefault();
                var baseProfile = new CharacterBaseStatProfile(
                    "base-profile.durable-end",
                    Character.ClassDefinitionStableId.ToString(),
                    10,
                    "base-profile-definition-v1",
                    new Dictionary<string, decimal>
                    {
                        { DerivedStatTargetIds.MaximumHealth, 100m },
                        { DerivedStatTargetIds.MovementSpeed, 5m },
                        { DerivedStatTargetIds.GunCapacity, 4m },
                        { DerivedStatTargetIds.AbilityCapacity, 0m },
                    });
                var characterInput = new DerivedCharacterStatInput(
                    Character.CharacterInstanceStableId.ToString(),
                    baseProfile,
                    null,
                    policy);
                var composer = new DefaultDerivedCharacterStatComposer();
                DerivedCharacterStatsSnapshot characterStats =
                    composer.DeriveCharacter(characterInput);
                RunCombatProfile profile = composer.BuildRunProfile(
                    new RunCombatProfileInput(
                        resolvedRunStableId.ToString(),
                        command.Fingerprint,
                        characterStats,
                        null,
                        null,
                        policy));
                var skill = new RankedSkillAllocationSnapshot(
                    "skill-profile.durable-end",
                    Character.ClassDefinitionStableId.ToString(),
                    0L,
                    "1",
                    "fixture",
                    null);
                var frozen = new FrozenCharacterRunInputs(
                    Character,
                    route,
                    0L,
                    "loadout-fingerprint-durable-end",
                    0L,
                    "holdings-fingerprint-durable-end",
                    skill,
                    characterStats,
                    profile,
                    new[]
                    {
                        new FrozenRunEquipment(
                            Id("gun-slot.slot-1"),
                            equipment,
                            definition),
                    },
                    command.EventModifierContextFingerprint);
                var bundle = new FakeLiveBundle(
                    resolvedRunStableId,
                    Character,
                    frozen);
                bundles.Add(resolvedRunStableId, bundle);
                return RunSessionStartMaterial.Accept(
                    frozen,
                    bundle.Ports);
            }
        }

        private sealed class FakeLiveBundle
        {
            public FakeLiveBundle(
                StableId runStableId,
                CharacterInstanceSnapshot character,
                FrozenCharacterRunInputs frozen)
            {
                Player = new FakePlayerPort(
                    Id("actor.player-" + character.CharacterInstanceStableId.Value),
                    Id("participant." + character.CharacterInstanceStableId.Value),
                    1L,
                    Decimal.ToDouble(frozen.CombatProfile.MaximumHealth));
                Guns = new FakeGunPort(
                    1L,
                    frozen.Equipment.Select(
                        item => item.EquipmentInstanceStableId));
                StatusEffects = new FakeStatusEffectPort(1L);
                ConditionalFacts = new FakeConditionalPort(1L);
                ActiveAbilities = new FakeAbilityPort(1L);
                Rooms = new FakeRoomPort(1L);
                MissionResults = new FakeMissionResultPort(runStableId);
                Ports = new RunSessionLivePorts(
                    Player,
                    Guns,
                    StatusEffects,
                    ConditionalFacts,
                    ActiveAbilities,
                    Rooms,
                    MissionResults);
            }

            public FakePlayerPort Player { get; }
            public FakeGunPort Guns { get; }
            public FakeStatusEffectPort StatusEffects { get; }
            public FakeConditionalPort ConditionalFacts { get; }
            public FakeAbilityPort ActiveAbilities { get; }
            public FakeRoomPort Rooms { get; }
            public FakeMissionResultPort MissionResults { get; }
            public RunSessionLivePorts Ports { get; }
        }

        private abstract class FakeLifecyclePort : IRunLifecycleLivePort
        {
            protected FakeLifecyclePort(string portId, long generation)
            {
                PortId = portId;
                Generation = generation;
            }

            protected long Generation { get; set; }
            public int TransientCount { get; set; }
            public string PortId { get; }
            public long LifecycleGeneration { get { return Generation; } }
            public virtual string SnapshotFingerprint
            {
                get { return PortId + "|" + Generation + "|" + TransientCount; }
            }

            public virtual string ValidateRestart(
                long retiringLifecycleGeneration,
                long replacementLifecycleGeneration,
                long authoritativeTick)
            {
                if (retiringLifecycleGeneration != Generation)
                {
                    return "generation-mismatch";
                }
                return replacementLifecycleGeneration == Generation + 1L
                    ? string.Empty
                    : "replacement-invalid";
            }

            public virtual RunLivePortRestartResult Restart(
                StableId operationStableId,
                long retiringLifecycleGeneration,
                long replacementLifecycleGeneration,
                long authoritativeTick)
            {
                string rejection = ValidateRestart(
                    retiringLifecycleGeneration,
                    replacementLifecycleGeneration,
                    authoritativeTick);
                if (!string.IsNullOrEmpty(rejection))
                {
                    return new RunLivePortRestartResult(
                        false,
                        rejection,
                        Generation,
                        SnapshotFingerprint);
                }
                Generation = replacementLifecycleGeneration;
                TransientCount = 0;
                return new RunLivePortRestartResult(
                    true,
                    string.Empty,
                    Generation,
                    SnapshotFingerprint);
            }
        }

        private sealed class FakePlayerPort : FakeLifecyclePort,
            IRunPlayerLivePort
        {
            private readonly StableId actorId;
            private readonly StableId participantId;
            private readonly double maximumHealth;
            private double currentHealth;
            private double x;
            private double y;
            private long acceptedSequence;

            public FakePlayerPort(
                StableId actorId,
                StableId participantId,
                long generation,
                double maximumHealth)
                : base("player-runtime", generation)
            {
                this.actorId = actorId;
                this.participantId = participantId;
                this.maximumHealth = maximumHealth;
                currentHealth = maximumHealth;
            }

            public void Damage(double amount)
            {
                currentHealth = Math.Max(0d, currentHealth - amount);
                acceptedSequence++;
            }

            public RunPlayerSnapshot ExportSnapshot()
            {
                return new RunPlayerSnapshot(
                    actorId,
                    participantId,
                    Generation,
                    currentHealth,
                    maximumHealth,
                    x,
                    y,
                    acceptedSequence);
            }

            public override string SnapshotFingerprint
            {
                get { return ExportSnapshot().Fingerprint; }
            }

            public override RunLivePortRestartResult Restart(
                StableId operationStableId,
                long retiringLifecycleGeneration,
                long replacementLifecycleGeneration,
                long authoritativeTick)
            {
                RunLivePortRestartResult result = base.Restart(
                    operationStableId,
                    retiringLifecycleGeneration,
                    replacementLifecycleGeneration,
                    authoritativeTick);
                if (result.Succeeded)
                {
                    currentHealth = maximumHealth;
                    x = 0d;
                    y = 0d;
                    acceptedSequence++;
                }
                return new RunLivePortRestartResult(
                    result.Succeeded,
                    result.RejectionCode,
                    Generation,
                    SnapshotFingerprint);
            }
        }

        private sealed class FakeGunPort : FakeLifecyclePort,
            IRunGunLivePort
        {
            private readonly IReadOnlyList<StableId> equipmentIds;

            public FakeGunPort(
                long generation,
                IEnumerable<StableId> equipmentIds)
                : base("gun-runtime", generation)
            {
                this.equipmentIds = equipmentIds.ToList().AsReadOnly();
            }

            public IReadOnlyList<StableId> FrozenEquipmentInstanceStableIds
            {
                get { return equipmentIds; }
            }
        }

        private sealed class FakeStatusEffectPort : FakeLifecyclePort,
            IRunStatusEffectLivePort
        {
            public FakeStatusEffectPort(long generation)
                : base("status-effect-runtime", generation)
            {
            }

            public int ActiveEffectCount { get { return 0; } }
        }

        private sealed class FakeConditionalPort : FakeLifecyclePort,
            IRunConditionalFactLivePort
        {
            public FakeConditionalPort(long generation)
                : base("conditional-runtime", generation)
            {
            }
        }

        private sealed class FakeAbilityPort : FakeLifecyclePort,
            IRunActiveAbilityLivePort
        {
            public FakeAbilityPort(long generation)
                : base("ability-runtime-placeholder", generation)
            {
            }
        }

        private sealed class FakeRoomPort : FakeLifecyclePort,
            IRunRoomLivePort
        {
            public FakeRoomPort(long generation)
                : base("room-runtime", generation)
            {
                CurrentRoomStableId = Id("room.start");
            }

            public StableId CurrentRoomStableId { get; private set; }
        }

        private sealed class FakeMissionResultPort : IRunMissionResultPort
        {
            private readonly StableId runStableId;
            private MissionRunPayload runPayload;

            public FakeMissionResultPort(StableId runStableId)
            {
                this.runStableId = runStableId;
            }

            public long Sequence { get; private set; }
            public int EndRunCallCount { get; private set; }

            public bool TryGetRun(
                StableId requestedRunStableId,
                out MissionRunPayload payload)
            {
                payload = requestedRunStableId == runStableId
                    ? runPayload
                    : null;
                return payload != null;
            }

            public MissionRunStateResult RecordCollectedStrongbox(
                RunStrongboxCollectionRequest request,
                PlayerRouteProfilePayload routePayload)
            {
                return new MissionRunStateResult(
                    MissionRunStateStatus.InvalidRequest,
                    Sequence,
                    Sequence,
                    request.OperationStableId,
                    request.Fingerprint,
                    runPayload,
                    null,
                    null,
                    "fixture-no-strongbox");
            }

            public MissionRunStateResult EndRun(
                EndRunSessionCommand command,
                PlayerRouteProfilePayload routePayload)
            {
                EndRunCallCount++;
                long previous = Sequence;
                Sequence++;
                MissionResultPayload result = MissionResultPayload.Create(
                    runStableId,
                    routePayload,
                    command.CompletionState,
                    Array.Empty<MissionRunStrongboxResult>(),
                    Sequence,
                    0L,
                    MissionRun.Fingerprint("fixture-holdings"),
                    0L,
                    MissionRun.Fingerprint("fixture-openings"));
                return new MissionRunStateResult(
                    MissionRunStateStatus.RunEnded,
                    previous,
                    Sequence,
                    command.OperationStableId,
                    command.Fingerprint,
                    runPayload,
                    null,
                    result,
                    string.Empty);
            }
        }
    }
}
