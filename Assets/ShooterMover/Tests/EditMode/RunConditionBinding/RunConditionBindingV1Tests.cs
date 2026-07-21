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
    public sealed class RunConditionBindingV1Tests
    {
        [Test]
        public void ProductionCompositionUsesOneRealConditionAndEffectOwner()
        {
            var fixture = new Fixture();
            RunSessionAggregateV1 run = fixture.Start("composition");
            var condition = run.RuntimePorts.ConditionalFacts
                as ExistingConditionRuntimeRunPortV1;
            var status = run.RuntimePorts.StatusEffects
                as ConditionOwnedStatusEffectRunPortV1;

            Assert.That(condition, Is.Not.Null);
            Assert.That(run.RuntimePorts.ConditionalFacts,
                Is.Not.TypeOf<DelegatedConditionalFactRunPortV1>());
            Assert.That(status, Is.Not.Null);
            Assert.That(status.ConditionRuntime, Is.SameAs(condition));
            Assert.That(condition.Authority, Is.Not.Null);

            RunConditionRuntimeSnapshotV1 initial =
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
            RunSessionAggregateV1 run = fixture.Start("activation");
            string permanent = fixture.Character.Fingerprint;
            string frozenProfile = run.FrozenInputs.CombatProfile.Fingerprint;

            RunConditionDeliveryResultV1 first = Kill(run, "a", 1, 1L);
            Kill(run, "a", 2, 2L);
            RunConditionDeliveryResultV1 third = Kill(run, "a", 3, 3L);

            Assert.That(first.Status,
                Is.EqualTo(RunConditionDeliveryStatusV1.Applied));
            Assert.That(third.Snapshot.Participants.Single().ActiveConditionIds,
                Does.Contain("condition.enemy-kill-burst"));
            Assert.That(Participant(run, "a").ActiveEffectCount, Is.EqualTo(1));
            Assert.That(run.ExportConditionModifierProjection(Id("participant.a"))
                    .Evaluate(DerivedStatTargetIdsV1.OutgoingDamageMultiplier, 1m)
                    .FinalValue,
                Is.EqualTo(1.5m));

            RunConditionGameplayFactCommandV1 original =
                Delivery(run, "delivery-replay", Death(run.RunStableId, "a", 8, 1L), "a", 4L);
            RunConditionDeliveryResultV1 applied =
                run.DeliverConditionGameplayFact(original);
            Kill(run, "a", 9, 5L);
            RunConditionDeliveryResultV1 replay =
                run.DeliverConditionGameplayFact(original);
            RunConditionDeliveryResultV1 conflict =
                run.DeliverConditionGameplayFact(
                    Delivery(run, "delivery-replay", Death(run.RunStableId, "a", 10, 1L), "a", 4L));

            Assert.That(replay.Status,
                Is.EqualTo(RunConditionDeliveryStatusV1.ExactReplay));
            Assert.That(replay.Snapshot.Fingerprint,
                Is.EqualTo(applied.Snapshot.Fingerprint));
            Assert.That(conflict.Status,
                Is.EqualTo(RunConditionDeliveryStatusV1.ConflictingDuplicate));

            var advance = new RunConditionAdvanceCommandV1(
                Id("operation.advance-expiry"), run.RunStableId, 1L, 12L);
            RunConditionAdvanceResultV1 advanced =
                run.AdvanceConditionRuntime(advance);
            RunConditionAdvanceResultV1 advanceReplay =
                run.AdvanceConditionRuntime(advance);
            RunConditionAdvanceResultV1 advanceConflict =
                run.AdvanceConditionRuntime(new RunConditionAdvanceCommandV1(
                    Id("operation.advance-expiry"), run.RunStableId, 1L, 13L));

            Assert.That(advanced.Status,
                Is.EqualTo(RunConditionAdvanceStatusV1.Applied));
            Assert.That(advanceReplay.Status,
                Is.EqualTo(RunConditionAdvanceStatusV1.ExactReplay));
            Assert.That(advanceReplay.Snapshot.Fingerprint,
                Is.EqualTo(advanced.Snapshot.Fingerprint));
            Assert.That(advanceConflict.Status,
                Is.EqualTo(RunConditionAdvanceStatusV1.ConflictingDuplicate));
            Assert.That(Participant(run, "a").ActiveEffectCount, Is.Zero);
            Assert.That(run.FrozenInputs.CombatProfile.Fingerprint,
                Is.EqualTo(frozenProfile));
            Assert.That(fixture.Character.Fingerprint, Is.EqualTo(permanent));
        }

        [Test]
        public void ParticipantsRunsRestartAndTerminalStateRemainIsolated()
        {
            var fixture = new Fixture(new TwoParticipantSeeds());
            RunSessionAggregateV1 runA = fixture.Start("isolation-a");
            RunSessionAggregateV1 runB = fixture.Start("isolation-b");

            for (int index = 1; index <= 3; index++)
            {
                Kill(runA, "a", index, index);
            }
            Assert.That(Participant(runA, "a").ActiveEffectCount, Is.EqualTo(1));
            Assert.That(Participant(runA, "b").ActiveEffectCount, Is.Zero);
            Assert.That(Participant(runB, "a").ActiveEffectCount, Is.Zero);

            RunConditionDeliveryResultV1 unattributed =
                runA.DeliverConditionGameplayFact(
                    Delivery(runA, "unattributed",
                        Death(runA.RunStableId, "a", 20, 1L, false), "a", 4L));
            RunConditionDeliveryResultV1 wrongRun =
                runB.DeliverConditionGameplayFact(new RunConditionGameplayFactCommandV1(
                    Id("operation.wrong-run"),
                    Death(runA.RunStableId, "a", 21, 1L),
                    runA.RunStableId, 1L, Id("actor.a"), Id("participant.a"),
                    Id("character.a"), 1L, 4L));
            Assert.That(unattributed.Status,
                Is.EqualTo(RunConditionDeliveryStatusV1.Rejected));
            Assert.That(unattributed.DiagnosticCode,
                Is.EqualTo("condition-enemy-death-killer-unattributed"));
            Assert.That(wrongRun.Status,
                Is.EqualTo(RunConditionDeliveryStatusV1.WrongRun));

            string skill = runA.FrozenInputs.SkillSnapshot.Fingerprint;
            string permanent = fixture.Character.Fingerprint;
            RunSessionRestartResultV1 restart = runA.Restart(
                new RestartRunSessionCommandV1(
                    Id("operation.restart-condition"), runA.RunStableId,
                    1L, 2L, 10L, RunRestartPolicyV1.FullTransientReset()));
            Assert.That(restart.Status,
                Is.EqualTo(RunSessionRestartStatusV1.Applied));
            Assert.That(runA.LifecycleGeneration, Is.EqualTo(2L));
            Assert.That(runA.ExportConditionRuntimeSnapshot().AcceptedFactCount,
                Is.Zero);
            Assert.That(Participant(runA, "a").ActiveEffectCount, Is.Zero);
            Assert.That(Participant(runA, "a").ActorLifecycleGeneration,
                Is.EqualTo(2L));
            Assert.That(runA.FrozenInputs.SkillSnapshot.Fingerprint,
                Is.EqualTo(skill));

            RunConditionDeliveryResultV1 stale =
                runA.DeliverConditionGameplayFact(new RunConditionGameplayFactCommandV1(
                    Id("operation.stale-death"),
                    Death(runA.RunStableId, "a", 30, 1L),
                    runA.RunStableId, 1L, Id("actor.a"), Id("participant.a"),
                    Id("character.a"), 1L, 11L));
            Assert.That(stale.Status,
                Is.EqualTo(RunConditionDeliveryStatusV1.StaleLifecycle));

            RunSessionEndResultV1 ended = runA.End(new EndRunSessionCommandV1(
                Id("operation.end-condition"), runA.RunStableId, 2L,
                MissionRunCompletionStateV1.Completed, 20L));
            RunSessionEndResultV1 endReplay = runA.End(ended.Command);
            Assert.That(ended.Status, Is.EqualTo(RunSessionEndStatusV1.Ended));
            Assert.That(endReplay, Is.SameAs(ended));
            Assert.That(Kill(runA, "a", 40, 21L).Status,
                Is.EqualTo(RunConditionDeliveryStatusV1.RunEnded));
            Assert.That(runA.AdvanceConditionRuntime(
                    new RunConditionAdvanceCommandV1(
                        Id("operation.after-end"), runA.RunStableId, 2L, 21L))
                    .Status,
                Is.EqualTo(RunConditionAdvanceStatusV1.RunEnded));
            Assert.That(fixture.Character.Fingerprint, Is.EqualTo(permanent));
        }

        private static RunConditionDeliveryResultV1 Kill(
            RunSessionAggregateV1 run, string actor, int ordinal, long tick)
        {
            return run.DeliverConditionGameplayFact(
                Delivery(run, "kill-" + actor + "-" + ordinal,
                    Death(run.RunStableId, actor, ordinal, run.LifecycleGeneration),
                    actor, tick));
        }

        private static RunConditionGameplayFactCommandV1 Delivery(
            RunSessionAggregateV1 run,
            string operation,
            EnemyDeathFactV1 death,
            string actor,
            long tick)
        {
            return new RunConditionGameplayFactCommandV1(
                Id("operation." + operation), death, run.RunStableId,
                run.LifecycleGeneration, Id("actor." + actor),
                Id("participant." + actor), Id("character." + actor),
                run.LifecycleGeneration, tick);
        }

        private static EnemyDeathFactV1 Death(
            StableId runId, string actor, int ordinal, long targetGeneration,
            bool attributed = true)
        {
            string suffix = actor + "-" + ordinal;
            var identity = new EnemyRuntimeIdentityV1(
                Id("enemy." + suffix), Id("participant.enemy-" + suffix), runId,
                Id("room-runtime.main"), Id("room.main"),
                Id("placement.enemy-" + suffix));
            return new EnemyDeathFactV1(
                Id("death." + suffix), Id("damage." + suffix), identity,
                Id("enemy-definition.fixture"), 1, targetGeneration,
                attributed ? Id("actor." + actor) : null,
                attributed ? Id("participant." + actor) : null,
                Id("experience-profile.fixture"), Id("drop-profile.fixture"),
                EnemyActorDeathCause.IncomingDamage);
        }

        private static RunConditionParticipantSnapshotV1 Participant(
            RunSessionAggregateV1 run, string actor)
        {
            return run.ExportConditionRuntimeSnapshot().Participants.Single(
                item => item.ParticipantStableId == Id("participant." + actor));
        }

        private static StableId Id(string value)
        {
            return StableId.Parse(value);
        }

        private sealed class DefinitionProvider : IRunConditionDefinitionProviderV1
        {
            public ConditionEffectRuntimeDefinitionV1 Resolve(
                StableId runStableId,
                FrozenCharacterRunInputsV1 frozenInputs,
                RunConditionParticipantSeedV1 participant)
            {
                return new FactWindowEffectFixtureV1(
                    "condition.enemy-kill-burst",
                    "status-effect.enemy-kill-burst",
                    ConditionRuntimeFactTypeIdsV1.EnemyKilled,
                    3, 10L, 5L, 1.5m)
                    .Build("condition-runtime.fixture", "1.0.0",
                        "conditional-source.fixture");
            }
        }

        private sealed class TwoParticipantSeeds :
            IRunConditionParticipantSeedProviderV1
        {
            public IReadOnlyList<RunConditionParticipantSeedV1> Resolve(
                StableId runStableId,
                long generation,
                FrozenCharacterRunInputsV1 frozenInputs,
                IRunPlayerRuntimePortV1 playerRuntime)
            {
                return new[]
                {
                    new RunConditionParticipantSeedV1(
                        Id("participant.a"), Id("character.a"), Id("actor.a"),
                        generation, frozenInputs.SkillSnapshot.Fingerprint),
                    new RunConditionParticipantSeedV1(
                        Id("participant.b"), Id("character.b"), Id("actor.b"),
                        generation, frozenInputs.SkillSnapshot.Fingerprint),
                };
            }
        }

        private sealed class Fixture : IRunSessionStartSourceV1
        {
            private readonly ProductionConditionBoundRunSessionRuntimePortFactoryV1
                factory;

            public Fixture(
                IRunConditionParticipantSeedProviderV1 participants = null)
            {
                Character = new CharacterInstanceSnapshotV1(
                    Id("character.a"), Id("loadout-profile.striker"), 0,
                    "Condition Pilot", 4L, null);
                factory = new ProductionConditionBoundRunSessionRuntimePortFactoryV1(
                    new BaseFactory(), new DefinitionProvider(), participants);
            }

            public CharacterInstanceSnapshotV1 Character { get; }

            public RunSessionAggregateV1 Start(string suffix)
            {
                var authority = new RunSessionAuthorityV1(this);
                RunSessionStartResultV1 started = authority.Start(
                    new StartRunSessionCommandV1(
                        Id("operation.start-" + suffix), null,
                        "condition-run-" + suffix,
                        Character.CharacterInstanceStableId,
                        Character.Revision, Character.Fingerprint,
                        Id("mission-layout.level-1"), Id("difficulty.normal"),
                        suffix.Length, 0L, "event-context.none"));
                Assert.That(started.Status,
                    Is.EqualTo(RunSessionStartStatusV1.Started),
                    started.RejectionCode);
                RunSessionAggregateV1 run;
                Assert.That(authority.TryGetRun(started.RunStableId, out run),
                    Is.True);
                return run;
            }

            public RunSessionStartMaterialV1 Resolve(
                StartRunSessionCommandV1 command, StableId runId)
            {
                FrozenCharacterRunInputsV1 frozen = BuildFrozen(command, runId);
                return RunSessionStartMaterialV1.Accept(
                    frozen, factory.Create(command, runId, frozen));
            }

            private FrozenCharacterRunInputsV1 BuildFrozen(
                StartRunSessionCommandV1 command, StableId runId)
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
                PlayerRouteProfilePayloadV1 route =
                    PlayerRouteProfilePayloadV1.Create(
                        Character.CharacterInstanceStableId,
                        Character.ClassDefinitionStableId,
                        new[] { equipment.InstanceId, null, null, null });
                DerivedStatPolicyV1 policy = DerivedStatPolicyV1.CreateDefault();
                var input = new DerivedCharacterStatInputV1(
                    Character.CharacterInstanceStableId.ToString(),
                    new CharacterBaseStatProfileV1(
                        "base-profile.condition", Character.ClassDefinitionStableId.ToString(),
                        1, "base-profile-v1", new Dictionary<string, decimal>
                        {
                            { DerivedStatTargetIdsV1.MaximumHealth, 100m },
                            { DerivedStatTargetIdsV1.MovementSpeed, 5m },
                            { DerivedStatTargetIdsV1.WeaponCapacity, 4m },
                            { DerivedStatTargetIdsV1.AbilityCapacity, 0m },
                            { DerivedStatTargetIdsV1.OutgoingDamageMultiplier, 1m },
                        }), null, policy);
                var composer = new DefaultDerivedCharacterStatComposerV1();
                DerivedCharacterStatsSnapshotV1 stats =
                    composer.DeriveCharacter(input);
                RunCombatProfileV1 profile = composer.BuildRunProfile(
                    new RunCombatProfileInputV1(
                        runId.ToString(), command.Fingerprint, stats,
                        null, null, policy));
                var skills = new RankedSkillAllocationSnapshotV2(
                    "skill-profile.condition", Character.ClassDefinitionStableId.ToString(),
                    2L, "1", "fixture", null);
                return new FrozenCharacterRunInputsV1(
                    Character, route, 0L, "loadout-fingerprint", 0L,
                    "holdings-fingerprint", skills, stats, profile,
                    new[]
                    {
                        new FrozenRunEquipmentV1(
                            Id("weapon-slot.slot-1"), equipment, definition),
                    }, command.EventModifierContextFingerprint);
            }
        }

        private sealed class BaseFactory :
            IRunSessionNonConditionRuntimePortFactoryV1
        {
            public RunSessionNonConditionRuntimePortsV1 Create(
                StartRunSessionCommandV1 command,
                StableId runId,
                FrozenCharacterRunInputsV1 frozen)
            {
                return new RunSessionNonConditionRuntimePortsV1(
                    new PlayerPort(),
                    new WeaponPort(frozen.Equipment.Select(
                        item => item.EquipmentInstanceStableId)),
                    new AbilityPort(),
                    new RoomPort(),
                    new ResultPort(runId));
            }
        }

        private abstract class LifecyclePort : IRunLifecycleRuntimePortV1
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
            public RunRuntimePortRestartResultV1 Restart(
                StableId operation, long retiring, long replacement, long tick)
            {
                string rejection = ValidateRestart(retiring, replacement, tick);
                if (string.IsNullOrEmpty(rejection)) Generation = replacement;
                return new RunRuntimePortRestartResultV1(
                    string.IsNullOrEmpty(rejection), rejection, Generation,
                    SnapshotFingerprint);
            }
        }

        private sealed class PlayerPort : LifecyclePort, IRunPlayerRuntimePortV1
        {
            public PlayerPort() : base("fixture-player") { }
            public RunPlayerRuntimeSnapshotV1 ExportSnapshot()
            {
                return new RunPlayerRuntimeSnapshotV1(
                    Id("actor.a"), Id("participant.a"), Generation,
                    100d, 100d, 0d, 0d, 0L);
            }
            public override string SnapshotFingerprint
            {
                get { return ExportSnapshot().Fingerprint; }
            }
        }

        private sealed class WeaponPort : LifecyclePort, IRunWeaponRuntimePortV1
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
            IRunActiveAbilityRuntimePortV1
        {
            public AbilityPort() : base("fixture-abilities") { }
        }

        private sealed class RoomPort : LifecyclePort, IRunRoomRuntimePortV1
        {
            public RoomPort() : base("fixture-room")
            {
                CurrentRoomStableId = Id("room.main");
            }
            public StableId CurrentRoomStableId { get; }
        }

        private sealed class ResultPort : IRunMissionResultPortV1
        {
            private readonly StableId runId;
            public ResultPort(StableId runId) { this.runId = runId; }
            public long Sequence { get; private set; }
            public bool TryGetRun(StableId id, out MissionRunPayloadV1 payload)
            {
                payload = null;
                return false;
            }
            public MissionRunAuthorityResultV1 RecordCollectedStrongbox(
                RunStrongboxCollectionRequestV1 request,
                PlayerRouteProfilePayloadV1 route)
            {
                return new MissionRunAuthorityResultV1(
                    MissionRunAuthorityStatusV1.InvalidRequest,
                    Sequence, Sequence, request.OperationStableId,
                    request.Fingerprint, null, null, null, "fixture-no-boxes");
            }
            public MissionRunAuthorityResultV1 EndRun(
                EndRunSessionCommandV1 command,
                PlayerRouteProfilePayloadV1 route)
            {
                long before = Sequence++;
                MissionResultPayloadV1 result = MissionResultPayloadV1.Create(
                    runId, route, command.CompletionState,
                    new MissionRunStrongboxResultV1[0], Sequence, 0L,
                    MissionRunCanonicalV1.Fingerprint("fixture-holdings"), 0L,
                    MissionRunCanonicalV1.Fingerprint("fixture-openings"));
                return new MissionRunAuthorityResultV1(
                    MissionRunAuthorityStatusV1.RunEnded,
                    before, Sequence, command.OperationStableId,
                    command.Fingerprint, null, null, result, string.Empty);
            }
        }
    }
}
