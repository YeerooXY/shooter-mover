using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ShooterMover.Application.Runs.Session;
using ShooterMover.ConditionRuntime;
using ShooterMover.Contracts.Flow.Session;
using ShooterMover.Contracts.Missions.Results;
using ShooterMover.Domain.Characters.Stats;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Enemies;
using ShooterMover.Domain.Equipment;
using ShooterMover.Domain.Persistence.Accounts;
using ShooterMover.Domain.Progression.Skills;
using ShooterMover.EnemyRuntimeComposition;
using ShooterMover.RunConditionIntegration;

namespace ShooterMover.Tests.EditMode.RunConditionBinding
{
    public sealed class RunConditionBindingTests
    {
        [Test]
        public void ProductionCompositionUsesOneRealConditionAndEffectOwner()
        {
            var fixture = new Fixture();
            RunSessionAggregate run = fixture.Start("composition");
            var condition = run.RuntimePorts.ConditionalFacts
                as ExistingConditionLiveRunPort;
            var status = run.RuntimePorts.StatusEffects
                as ConditionOwnedStatusEffectRunPort;

            Assert.That(condition, Is.Not.Null);
            Assert.That(run.RuntimePorts.ConditionalFacts,
                Is.Not.TypeOf<DelegatedConditionalFactRunPort>());
            Assert.That(status, Is.Not.Null);
            Assert.That(status.ConditionRuntime, Is.SameAs(condition));
            Assert.That(condition.Authority, Is.Not.Null);

            RunConditionLiveSnapshot initial =
                run.ExportConditionRuntimeSnapshot();
            Assert.That(initial.RunStableId, Is.EqualTo(run.RunStableId));
            Assert.That(initial.LifecycleGeneration, Is.EqualTo(1L));
            Assert.That(initial.Participants.Single().CharacterStableId,
                Is.EqualTo(fixture.Character.CharacterInstanceStableId));
            Assert.That(initial.Participants.Single().ParticipantStableId,
                Is.EqualTo(Id("participant.a")));
            Assert.That(status.ActiveEffectCount, Is.Zero);
        }

        [Test]
        public void AcceptedDeathsActivateDataDefinedModifierAndReplaySafely()
        {
            var fixture = new Fixture();
            RunSessionAggregate run = fixture.Start("activation");
            string permanent = fixture.Character.Fingerprint;
            string frozenProfile = run.FrozenInputs.CombatProfile.Fingerprint;

            RunConditionDeliveryResult first = Kill(run, "a", 1, 1L);
            Kill(run, "a", 2, 2L);
            RunConditionDeliveryResult third = Kill(run, "a", 3, 3L);

            Assert.That(first.Status,
                Is.EqualTo(RunConditionDeliveryStatus.Applied));
            Assert.That(third.Snapshot.Participants.Single().ActiveConditionIds,
                Does.Contain("condition.enemy-kill-burst"));
            Assert.That(Participant(run, "a").ActiveEffectCount, Is.EqualTo(1));
            Assert.That(run.ExportConditionModifierProjection(Id("participant.a"))
                    .Evaluate(DerivedStatTargetIds.OutgoingDamageMultiplier, 1m)
                    .FinalValue,
                Is.EqualTo(1.5m));

            RunConditionGameplayFactCommand original =
                Delivery(run, "delivery-replay", Death(run.RunStableId, "a", 8, 1L), "a", 4L);
            RunConditionDeliveryResult applied =
                run.DeliverConditionGameplayFact(original);
            Kill(run, "a", 9, 5L);
            RunConditionDeliveryResult replay =
                run.DeliverConditionGameplayFact(original);
            RunConditionDeliveryResult conflict =
                run.DeliverConditionGameplayFact(
                    Delivery(run, "delivery-replay", Death(run.RunStableId, "a", 10, 1L), "a", 4L));

            Assert.That(replay.Status,
                Is.EqualTo(RunConditionDeliveryStatus.ExactReplay));
            Assert.That(replay.Snapshot.Fingerprint,
                Is.EqualTo(applied.Snapshot.Fingerprint));
            Assert.That(conflict.Status,
                Is.EqualTo(RunConditionDeliveryStatus.ConflictingDuplicate));

            var advance = new RunConditionAdvanceCommand(
                Id("operation.advance-expiry"), run.RunStableId, 1L, 12L);
            RunConditionAdvanceResult advanced =
                run.AdvanceConditionRuntime(advance);
            RunConditionAdvanceResult advanceReplay =
                run.AdvanceConditionRuntime(advance);
            RunConditionAdvanceResult advanceConflict =
                run.AdvanceConditionRuntime(new RunConditionAdvanceCommand(
                    Id("operation.advance-expiry"), run.RunStableId, 1L, 13L));

            Assert.That(advanced.Status,
                Is.EqualTo(RunConditionAdvanceStatus.Applied));
            Assert.That(advanceReplay.Status,
                Is.EqualTo(RunConditionAdvanceStatus.ExactReplay));
            Assert.That(advanceReplay.Snapshot.Fingerprint,
                Is.EqualTo(advanced.Snapshot.Fingerprint));
            Assert.That(advanceConflict.Status,
                Is.EqualTo(RunConditionAdvanceStatus.ConflictingDuplicate));
            Assert.That(Participant(run, "a").ActiveEffectCount, Is.Zero);
            Assert.That(run.FrozenInputs.CombatProfile.Fingerprint,
                Is.EqualTo(frozenProfile));
            Assert.That(fixture.Character.Fingerprint, Is.EqualTo(permanent));
        }

        [Test]
        public void ParticipantsRunsRestartAndTerminalStateRemainIsolated()
        {
            var fixture = new Fixture(new TwoParticipantSeeds());
            RunSessionAggregate runA = fixture.Start("isolation-a");
            RunSessionAggregate runB = fixture.Start("isolation-b");

            for (int index = 1; index <= 3; index++)
            {
                Kill(runA, "a", index, index);
            }
            Assert.That(Participant(runA, "a").ActiveEffectCount, Is.EqualTo(1));
            Assert.That(Participant(runA, "b").ActiveEffectCount, Is.Zero);
            Assert.That(Participant(runB, "a").ActiveEffectCount, Is.Zero);

            RunConditionDeliveryResult unattributed =
                runA.DeliverConditionGameplayFact(
                    Delivery(runA, "unattributed",
                        Death(runA.RunStableId, "a", 20, 1L, false), "a", 4L));
            RunConditionDeliveryResult wrongRun =
                runB.DeliverConditionGameplayFact(new RunConditionGameplayFactCommand(
                    Id("operation.wrong-run"),
                    Death(runA.RunStableId, "a", 21, 1L),
                    runA.RunStableId, 1L, Id("actor.a"), Id("participant.a"),
                    Id("character.a"), 1L, 4L));
            Assert.That(unattributed.Status,
                Is.EqualTo(RunConditionDeliveryStatus.Rejected));
            Assert.That(unattributed.DiagnosticCode,
                Is.EqualTo("condition-enemy-death-killer-unattributed"));
            Assert.That(wrongRun.Status,
                Is.EqualTo(RunConditionDeliveryStatus.WrongRun));

            string skill = runA.FrozenInputs.SkillSnapshot.Fingerprint;
            string permanent = fixture.Character.Fingerprint;
            RunSessionRestartResult restart = runA.Restart(
                new RestartRunSessionCommand(
                    Id("operation.restart-condition"), runA.RunStableId,
                    1L, 2L, 10L, RunRestartPolicy.FullTransientReset()));
            Assert.That(restart.Status,
                Is.EqualTo(RunSessionRestartStatus.Applied));
            Assert.That(runA.LifecycleGeneration, Is.EqualTo(2L));
            Assert.That(runA.ExportConditionRuntimeSnapshot().AcceptedFactCount,
                Is.Zero);
            Assert.That(Participant(runA, "a").ActiveEffectCount, Is.Zero);
            Assert.That(Participant(runA, "a").ActorLifecycleGeneration,
                Is.EqualTo(2L));
            Assert.That(runA.FrozenInputs.SkillSnapshot.Fingerprint,
                Is.EqualTo(skill));

            RunConditionDeliveryResult stale =
                runA.DeliverConditionGameplayFact(new RunConditionGameplayFactCommand(
                    Id("operation.stale-death"),
                    Death(runA.RunStableId, "a", 30, 1L),
                    runA.RunStableId, 1L, Id("actor.a"), Id("participant.a"),
                    Id("character.a"), 1L, 11L));
            Assert.That(stale.Status,
                Is.EqualTo(RunConditionDeliveryStatus.StaleLifecycle));

            RunSessionEndResult ended = runA.End(new EndRunSessionCommand(
                Id("operation.end-condition"), runA.RunStableId, 2L,
                MissionRunCompletionState.Completed, 20L));
            RunSessionEndResult endReplay = runA.End(ended.Command);
            Assert.That(ended.Status, Is.EqualTo(RunSessionEndStatus.Ended));
            Assert.That(endReplay, Is.SameAs(ended));
            Assert.That(Kill(runA, "a", 40, 21L).Status,
                Is.EqualTo(RunConditionDeliveryStatus.RunEnded));
            Assert.That(runA.AdvanceConditionRuntime(
                    new RunConditionAdvanceCommand(
                        Id("operation.after-end"), runA.RunStableId, 2L, 21L))
                    .Status,
                Is.EqualTo(RunConditionAdvanceStatus.RunEnded));
            Assert.That(fixture.Character.Fingerprint, Is.EqualTo(permanent));
        }

        private static RunConditionDeliveryResult Kill(
            RunSessionAggregate run, string actor, int ordinal, long tick)
        {
            return run.DeliverConditionGameplayFact(
                Delivery(run, "kill-" + actor + "-" + ordinal,
                    Death(run.RunStableId, actor, ordinal, run.LifecycleGeneration),
                    actor, tick));
        }

        private static RunConditionGameplayFactCommand Delivery(
            RunSessionAggregate run,
            string operation,
            EnemyDeathFact death,
            string actor,
            long tick)
        {
            return new RunConditionGameplayFactCommand(
                Id("operation." + operation), death, run.RunStableId,
                run.LifecycleGeneration, Id("actor." + actor),
                Id("participant." + actor), Id("character." + actor),
                run.LifecycleGeneration, tick);
        }

        private static EnemyDeathFact Death(
            StableId runId, string actor, int ordinal, long targetGeneration,
            bool attributed = true)
        {
            string suffix = actor + "-" + ordinal;
            var identity = new EnemyLiveIdentity(
                Id("enemy." + suffix), Id("participant.enemy-" + suffix), runId,
                Id("room-runtime.main"), Id("room.main"),
                Id("placement.enemy-" + suffix));
            return new EnemyDeathFact(
                Id("death." + suffix), Id("damage." + suffix), identity,
                Id("enemy-definition.fixture"), 1, targetGeneration,
                attributed ? Id("actor." + actor) : null,
                attributed ? Id("participant." + actor) : null,
                Id("experience-profile.fixture"), Id("drop-profile.fixture"),
                EnemyActorDeathCause.IncomingDamage);
        }

        private static RunConditionParticipantSnapshot Participant(
            RunSessionAggregate run, string actor)
        {
            return run.ExportConditionRuntimeSnapshot().Participants.Single(
                item => item.ParticipantStableId == Id("participant." + actor));
        }

        private static StableId Id(string value)
        {
            return StableId.Parse(value);
        }

        private sealed class DefinitionProvider : IRunConditionDefinitionProvider
        {
            public ConditionEffectLiveDefinition Resolve(
                StableId runStableId,
                FrozenCharacterRunInputs frozenInputs,
                RunConditionParticipantSeed participant)
            {
                return new FactWindowEffectFixture(
                    "condition.enemy-kill-burst",
                    "status-effect.enemy-kill-burst",
                    ConditionLiveFactTypeIds.EnemyKilled,
                    3, 10L, 5L, 1.5m)
                    .Build("condition-runtime.fixture", "1.0.0",
                        "conditional-source.fixture");
            }
        }

        private sealed class TwoParticipantSeeds :
            IRunConditionParticipantSeedProvider
        {
            public IReadOnlyList<RunConditionParticipantSeed> Resolve(
                StableId runStableId,
                long generation,
                FrozenCharacterRunInputs frozenInputs,
                IRunPlayerLivePort playerRuntime)
            {
                return new[]
                {
                    new RunConditionParticipantSeed(
                        Id("participant.a"), Id("character.a"), Id("actor.a"),
                        generation, frozenInputs.SkillSnapshot.Fingerprint),
                    new RunConditionParticipantSeed(
                        Id("participant.b"), Id("character.b"), Id("actor.b"),
                        generation, frozenInputs.SkillSnapshot.Fingerprint),
                };
            }
        }

        private sealed class Fixture : IRunSessionStartSource
        {
            private readonly ConditionBoundRunSessionLivePortFactory
                factory;

            public Fixture(
                IRunConditionParticipantSeedProvider participants = null)
            {
                Character = new CharacterInstanceSnapshot(
                    Id("character.a"), Id("loadout-profile.striker"), 0,
                    "Condition Pilot", 4L, null);
                factory = new ConditionBoundRunSessionLivePortFactory(
                    new BaseFactory(), new DefinitionProvider(), participants);
            }

            public CharacterInstanceSnapshot Character { get; }

            public RunSessionAggregate Start(string suffix)
            {
                var authority = new RunSessionState(this);
                RunSessionStartResult started = authority.Start(
                    new StartRunSessionCommand(
                        Id("operation.start-" + suffix), null,
                        "condition-run-" + suffix,
                        Character.CharacterInstanceStableId,
                        Character.Revision, Character.Fingerprint,
                        Id("mission-layout.level-1"), Id("difficulty.normal"),
                        suffix.Length, 0L, "event-context.none"));
                Assert.That(started.Status,
                    Is.EqualTo(RunSessionStartStatus.Started),
                    started.RejectionCode);
                RunSessionAggregate run;
                Assert.That(authority.TryGetRun(started.RunStableId, out run),
                    Is.True);
                return run;
            }

            public RunSessionStartMaterial Resolve(
                StartRunSessionCommand command, StableId runId)
            {
                FrozenCharacterRunInputs frozen = BuildFrozen(command, runId);
                return RunSessionStartMaterial.Accept(
                    frozen, factory.Create(command, runId, frozen));
            }

            private FrozenCharacterRunInputs BuildFrozen(
                StartRunSessionCommand command, StableId runId)
            {
                StableId definitionId = Id("equipment-definition.test-rifle");
                StableId qualityId = Id("quality.common");
                EquipmentDefinition definition = EquipmentDefinition.Create(
                    definitionId, EquipmentCategoryIds.Weapon,
                    Id("equipment-family.test-rifle"), "Test Rifle",
                    Id("weapon.test-rifle"), InclusiveIntRange.Create(1, 100),
                    1, new[]
                    {
                        EquipmentQualityTier.Create(qualityId, "Common", 1),
                    }, null);
                EquipmentInstance equipment = EquipmentInstance.Create(
                    Id("equipment-instance." + command.OperationStableId.Value),
                    definitionId, 1, qualityId, null);
                PlayerRouteProfilePayload route =
                    PlayerRouteProfilePayload.Create(
                        Character.CharacterInstanceStableId,
                        Character.ClassDefinitionStableId,
                        new[] { equipment.InstanceId, null, null, null });
                DerivedStatPolicy policy = DerivedStatPolicy.CreateDefault();
                var input = new DerivedCharacterStatInput(
                    Character.CharacterInstanceStableId.ToString(),
                    new CharacterBaseStatProfile(
                        "base-profile.condition", Character.ClassDefinitionStableId.ToString(),
                        1, "base-profile-v1", new Dictionary<string, decimal>
                        {
                            { DerivedStatTargetIds.MaximumHealth, 100m },
                            { DerivedStatTargetIds.MovementSpeed, 5m },
                            { DerivedStatTargetIds.WeaponCapacity, 4m },
                            { DerivedStatTargetIds.AbilityCapacity, 0m },
                            { DerivedStatTargetIds.OutgoingDamageMultiplier, 1m },
                        }), null, policy);
                var composer = new DefaultDerivedCharacterStatComposer();
                DerivedCharacterStatsSnapshot stats =
                    composer.DeriveCharacter(input);
                RunCombatProfile profile = composer.BuildRunProfile(
                    new RunCombatProfileInput(
                        runId.ToString(), command.Fingerprint, stats,
                        null, null, policy));
                var skills = new RankedSkillAllocationSnapshot(
                    "skill-profile.condition", Character.ClassDefinitionStableId.ToString(),
                    2L, "1", "fixture", null);
                return new FrozenCharacterRunInputs(
                    Character, route, 0L, "loadout-fingerprint", 0L,
                    "holdings-fingerprint", skills, stats, profile,
                    new[]
                    {
                        new FrozenRunEquipment(
                            Id("weapon-slot.slot-1"), equipment, definition),
                    }, command.EventModifierContextFingerprint);
            }
        }

        private sealed class BaseFactory :
            IRunSessionNonConditionLivePortFactory
        {
            public RunSessionNonConditionLivePorts Create(
                StartRunSessionCommand command,
                StableId runId,
                FrozenCharacterRunInputs frozen)
            {
                return new RunSessionNonConditionLivePorts(
                    new PlayerPort(),
                    new WeaponPort(frozen.Equipment.Select(
                        item => item.EquipmentInstanceStableId)),
                    new AbilityPort(),
                    new RoomPort(),
                    new ResultPort(runId));
            }
        }

        private abstract class LifecyclePort : IRunLifecycleLivePort
        {
            protected LifecyclePort(string id) { PortId = id; Generation = 1L; }
            protected long Generation { get; set; }
            public string PortId { get; }
            public long LifecycleGeneration { get { return Generation; } }
            public virtual string SnapshotFingerprint
            {
                get { return PortId + "|" + Generation; }
            }
            public string ValidateRestart(long retiring, long replacement, long tick)
            {
                return retiring == Generation && replacement == Generation + 1L
                    ? string.Empty : "fixture-generation-mismatch";
            }
            public RunLivePortRestartResult Restart(
                StableId operation, long retiring, long replacement, long tick)
            {
                string rejection = ValidateRestart(retiring, replacement, tick);
                if (string.IsNullOrEmpty(rejection)) Generation = replacement;
                return new RunLivePortRestartResult(
                    string.IsNullOrEmpty(rejection), rejection, Generation,
                    SnapshotFingerprint);
            }
        }

        private sealed class PlayerPort : LifecyclePort, IRunPlayerLivePort
        {
            public PlayerPort() : base("fixture-player") { }
            public RunPlayerLiveSnapshot ExportSnapshot()
            {
                return new RunPlayerLiveSnapshot(
                    Id("actor.a"), Id("participant.a"), Generation,
                    100d, 100d, 0d, 0d, 0L);
            }
            public override string SnapshotFingerprint
            {
                get { return ExportSnapshot().Fingerprint; }
            }
        }

        private sealed class WeaponPort : LifecyclePort, IRunWeaponLivePort
        {
            private readonly IReadOnlyList<StableId> equipment;
            public WeaponPort(IEnumerable<StableId> equipment)
                : base("fixture-weapons")
            {
                this.equipment = equipment.ToList().AsReadOnly();
            }
            public IReadOnlyList<StableId> FrozenEquipmentInstanceStableIds
            {
                get { return equipment; }
            }
        }

        private sealed class AbilityPort : LifecyclePort,
            IRunActiveAbilityLivePort
        {
            public AbilityPort() : base("fixture-abilities") { }
        }

        private sealed class RoomPort : LifecyclePort, IRunRoomLivePort
        {
            public RoomPort() : base("fixture-room")
            {
                CurrentRoomStableId = Id("room.main");
            }
            public StableId CurrentRoomStableId { get; }
        }

        private sealed class ResultPort : IRunMissionResultPort
        {
            private readonly StableId runId;
            public ResultPort(StableId runId) { this.runId = runId; }
            public long Sequence { get; private set; }
            public bool TryGetRun(StableId id, out MissionRunPayload payload)
            {
                payload = null;
                return false;
            }
            public MissionRunStateResult RecordCollectedStrongbox(
                RunStrongboxCollectionRequest request,
                PlayerRouteProfilePayload route)
            {
                return new MissionRunStateResult(
                    MissionRunStateStatus.InvalidRequest,
                    Sequence, Sequence, request.OperationStableId,
                    request.Fingerprint, null, null, null, "fixture-no-boxes");
            }
            public MissionRunStateResult EndRun(
                EndRunSessionCommand command,
                PlayerRouteProfilePayload route)
            {
                long before = Sequence++;
                MissionResultPayload result = MissionResultPayload.Create(
                    runId, route, command.CompletionState,
                    new MissionRunStrongboxResult[0], Sequence, 0L,
                    MissionRun.Fingerprint("fixture-holdings"), 0L,
                    MissionRun.Fingerprint("fixture-openings"));
                return new MissionRunStateResult(
                    MissionRunStateStatus.RunEnded,
                    before, Sequence, command.OperationStableId,
                    command.Fingerprint, null, null, result, string.Empty);
            }
        }
    }
}
