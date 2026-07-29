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
    public sealed class RunConditionRestartAtomicityTests
    {
        [Test]
        public void FailedDefinitionPrevalidationLeavesEveryPortIntactAndNewOperationCanRetry()
        {
            var definitionProvider = new ToggleDefinitionProvider();
            var source = new Source(definitionProvider);
            var authority = new RunSessionState(source);
            RunSessionStartResult started = authority.Start(source.Command());
            RunSessionAggregate run;
            Assert.That(started.Status,
                Is.EqualTo(RunSessionStartStatus.Started),
                started.RejectionCode);
            Assert.That(authority.TryGetRun(started.RunStableId, out run),
                Is.True);

            RunPlayerSnapshot player =
                run.RuntimePorts.Player.ExportSnapshot();
            for (int ordinal = 1; ordinal <= 3; ordinal++)
            {
                RunConditionDeliveryResult delivery =
                    run.DeliverConditionGameplayFact(
                        new RunConditionGameplayFactCommand(
                            Id("operation.atomic-kill-" + ordinal),
                            Death(run.RunStableId, player, ordinal),
                            run.RunStableId,
                            1L,
                            player.ActorInstanceStableId,
                            player.ParticipantStableId,
                            source.Character.CharacterInstanceStableId,
                            1L,
                            ordinal));
                Assert.That(delivery.Status,
                    Is.EqualTo(RunConditionDeliveryStatus.Applied));
            }

            RunConditionLiveSnapshot before =
                run.ExportConditionRuntimeSnapshot();
            string permanentFingerprint = source.Character.Fingerprint;
            definitionProvider.FailReplacement = true;
            var rejectedCommand = new RestartRunSessionCommand(
                Id("operation.atomic-restart-rejected"),
                run.RunStableId,
                1L,
                2L,
                10L,
                RunRestartPolicy.FullTransientReset());

            RunSessionRestartResult rejected = run.Restart(rejectedCommand);
            RunSessionRestartResult rejectedReplay =
                run.Restart(rejectedCommand);

            Assert.That(rejected.Status,
                Is.EqualTo(RunSessionRestartStatus.Rejected));
            Assert.That(rejected.RejectionCode,
                Does.Contain("condition-runtime-reconstruction-prevalidation-failed"));
            Assert.That(rejectedReplay, Is.SameAs(rejected));
            Assert.That(run.LifecycleGeneration, Is.EqualTo(1L));
            Assert.That(run.RuntimePorts.Player.LifecycleGeneration,
                Is.EqualTo(1L));
            Assert.That(run.RuntimePorts.Guns.LifecycleGeneration,
                Is.EqualTo(1L));
            Assert.That(run.RuntimePorts.StatusEffects.LifecycleGeneration,
                Is.EqualTo(1L));
            Assert.That(run.RuntimePorts.ConditionalFacts.LifecycleGeneration,
                Is.EqualTo(1L));
            Assert.That(run.RuntimePorts.ActiveAbilities.LifecycleGeneration,
                Is.EqualTo(1L));
            Assert.That(run.RuntimePorts.Rooms.LifecycleGeneration,
                Is.EqualTo(1L));
            Assert.That(run.ExportConditionRuntimeSnapshot().Fingerprint,
                Is.EqualTo(before.Fingerprint));
            Assert.That(run.RuntimePorts.StatusEffects.ActiveEffectCount,
                Is.EqualTo(1));
            Assert.That(source.Character.Fingerprint,
                Is.EqualTo(permanentFingerprint));

            definitionProvider.FailReplacement = false;
            RunSessionRestartResult retried = run.Restart(
                new RestartRunSessionCommand(
                    Id("operation.atomic-restart-retry"),
                    run.RunStableId,
                    1L,
                    2L,
                    10L,
                    RunRestartPolicy.FullTransientReset()));

            Assert.That(retried.Status,
                Is.EqualTo(RunSessionRestartStatus.Applied));
            Assert.That(run.LifecycleGeneration, Is.EqualTo(2L));
            Assert.That(run.ExportConditionRuntimeSnapshot().AcceptedFactCount,
                Is.Zero);
            Assert.That(run.RuntimePorts.StatusEffects.ActiveEffectCount,
                Is.Zero);
            Assert.That(source.Character.Fingerprint,
                Is.EqualTo(permanentFingerprint));
        }

        private static EnemyDeathFact Death(
            StableId runId,
            RunPlayerSnapshot player,
            int ordinal)
        {
            string suffix = ordinal.ToString();
            return new EnemyDeathFact(
                Id("death.atomic-" + suffix),
                Id("damage.atomic-" + suffix),
                new EnemyLiveIdentity(
                    Id("enemy.atomic-" + suffix),
                    Id("participant.enemy-atomic-" + suffix),
                    runId,
                    Id("room-runtime.main"),
                    Id("room.main"),
                    Id("placement.enemy-atomic-" + suffix)),
                Id("enemy-definition.fixture"),
                1,
                player.LifecycleGeneration,
                player.ActorInstanceStableId,
                player.ParticipantStableId,
                Id("experience-profile.fixture"),
                Id("drop-profile.fixture"),
                EnemyActorDeathCause.IncomingDamage);
        }

        private static StableId Id(string value)
        {
            return StableId.Parse(value);
        }

        private sealed class ToggleDefinitionProvider :
            IRunConditionDefinitionProvider
        {
            public bool FailReplacement { get; set; }

            public ConditionEffectLiveDefinition Resolve(
                StableId runId,
                FrozenCharacterRunInputs frozen,
                RunConditionParticipantSeed participant)
            {
                if (FailReplacement
                    && participant.ActorLifecycleGeneration > 1L)
                {
                    throw new InvalidOperationException(
                        "fixture replacement definition unavailable");
                }
                return new FactWindowEffectFixture(
                    "condition.enemy-kill-burst",
                    "status-effect.enemy-kill-burst",
                    ConditionLiveFactTypeIds.EnemyKilled,
                    3,
                    10L,
                    5L,
                    1.5m)
                    .Build(
                        "condition-runtime.atomic-fixture",
                        "1.0.0",
                        "conditional-source.atomic-fixture");
            }
        }

        private sealed class Source : IRunSessionStartSource
        {
            private readonly ConditionBoundRunSessionLivePortFactory
                factory;

            public Source(IRunConditionDefinitionProvider definitionProvider)
            {
                Character = new CharacterInstanceSnapshot(
                    Id("character.atomic"),
                    Id("loadout-profile.striker"),
                    0,
                    "Atomic Pilot",
                    3L,
                    null);
                factory =
                    new ConditionBoundRunSessionLivePortFactory(
                        new BaseFactory(),
                        definitionProvider);
            }

            public CharacterInstanceSnapshot Character { get; }

            public StartRunSessionCommand Command()
            {
                return new StartRunSessionCommand(
                    Id("operation.atomic-start"),
                    null,
                    "atomic-run-material",
                    Character.CharacterInstanceStableId,
                    Character.Revision,
                    Character.Fingerprint,
                    Id("mission-layout.level-1"),
                    Id("difficulty.normal"),
                    17L,
                    0L,
                    "event-context.none");
            }

            public RunSessionStartMaterial Resolve(
                StartRunSessionCommand command,
                StableId runId)
            {
                FrozenCharacterRunInputs frozen = BuildFrozen(command, runId);
                return RunSessionStartMaterial.Accept(
                    frozen,
                    factory.Create(command, runId, frozen));
            }

            private FrozenCharacterRunInputs BuildFrozen(
                StartRunSessionCommand command,
                StableId runId)
            {
                StableId definitionId =
                    Id("equipment-definition.atomic-rifle");
                StableId qualityId = Id("quality.common");
                EquipmentDefinition definition = EquipmentDefinition.Create(
                    definitionId,
                    EquipmentCategoryIds.Gun,
                    Id("equipment-family.atomic-rifle"),
                    "Atomic Rifle",
                    Id("gun.atomic-rifle"),
                    InclusiveIntRange.Create(1, 100),
                    1,
                    new[]
                    {
                        EquipmentQualityTier.Create(
                            qualityId,
                            "Common",
                            1),
                    },
                    null);
                EquipmentInstance equipment = EquipmentInstance.Create(
                    Id("equipment-instance.atomic-rifle"),
                    definitionId,
                    1,
                    qualityId,
                    null);
                PlayerRouteProfilePayload route =
                    PlayerRouteProfilePayload.Create(
                        Character.CharacterInstanceStableId,
                        Character.ClassDefinitionStableId,
                        new[] { equipment.InstanceId, null, null, null });
                DerivedStatPolicy policy =
                    DerivedStatPolicy.CreateDefault();
                var composer = new DefaultDerivedCharacterStatComposer();
                DerivedCharacterStatsSnapshot stats =
                    composer.DeriveCharacter(
                        new DerivedCharacterStatInput(
                            Character.CharacterInstanceStableId.ToString(),
                            new CharacterBaseStatProfile(
                                "base-profile.atomic",
                                Character.ClassDefinitionStableId.ToString(),
                                1,
                                Character.Fingerprint,
                                new Dictionary<string, decimal>
                                {
                                    {
                                        DerivedStatTargetIds.MaximumHealth,
                                        100m
                                    },
                                    {
                                        DerivedStatTargetIds.MovementSpeed,
                                        5m
                                    },
                                    {
                                        DerivedStatTargetIds.GunCapacity,
                                        4m
                                    },
                                    {
                                        DerivedStatTargetIds.AbilityCapacity,
                                        0m
                                    },
                                }),
                            null,
                            policy));
                RunCombatProfile profile = composer.BuildRunProfile(
                    new RunCombatProfileInput(
                        runId.ToString(),
                        command.Fingerprint,
                        stats,
                        null,
                        null,
                        policy));
                var skills = new RankedSkillAllocationSnapshot(
                    "skill-profile.atomic",
                    Character.ClassDefinitionStableId.ToString(),
                    1L,
                    "1",
                    "fixture",
                    null);
                return new FrozenCharacterRunInputs(
                    Character,
                    route,
                    0L,
                    "loadout-fingerprint.atomic",
                    0L,
                    "holdings-fingerprint.atomic",
                    skills,
                    stats,
                    profile,
                    new[]
                    {
                        new FrozenRunEquipment(
                            Id("gun-slot.slot-1"),
                            equipment,
                            definition),
                    },
                    command.EventModifierContextFingerprint);
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
                    new GunPort(frozen.Equipment.Select(
                        item => item.EquipmentInstanceStableId)),
                    new AbilityPort(),
                    new RoomPort(),
                    new ResultPort(runId));
            }
        }

        private abstract class LifecyclePort : IRunLifecycleLivePort
        {
            protected LifecyclePort(string portId)
            {
                PortId = portId;
                Generation = 1L;
            }

            protected long Generation { get; set; }
            public string PortId { get; }
            public long LifecycleGeneration { get { return Generation; } }
            public virtual string SnapshotFingerprint
            {
                get { return PortId + "|" + Generation; }
            }

            public string ValidateRestart(
                long retiring,
                long replacement,
                long tick)
            {
                return retiring == Generation && replacement == Generation + 1L
                    ? string.Empty
                    : "fixture-generation-mismatch";
            }

            public RunLivePortRestartResult Restart(
                StableId operation,
                long retiring,
                long replacement,
                long tick)
            {
                string rejection = ValidateRestart(
                    retiring,
                    replacement,
                    tick);
                if (string.IsNullOrEmpty(rejection))
                {
                    Generation = replacement;
                }
                return new RunLivePortRestartResult(
                    string.IsNullOrEmpty(rejection),
                    rejection,
                    Generation,
                    SnapshotFingerprint);
            }
        }

        private sealed class PlayerPort : LifecyclePort,
            IRunPlayerLivePort
        {
            public PlayerPort() : base("atomic-player") { }

            public RunPlayerSnapshot ExportSnapshot()
            {
                return new RunPlayerSnapshot(
                    Id("actor.atomic"),
                    Id("participant.atomic"),
                    Generation,
                    100d,
                    100d,
                    0d,
                    0d,
                    0L);
            }

            public override string SnapshotFingerprint
            {
                get { return ExportSnapshot().Fingerprint; }
            }
        }

        private sealed class GunPort : LifecyclePort,
            IRunGunLivePort
        {
            private readonly IReadOnlyList<StableId> equipment;

            public GunPort(IEnumerable<StableId> equipment)
                : base("atomic-guns")
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
            public AbilityPort() : base("atomic-abilities") { }
        }

        private sealed class RoomPort : LifecyclePort,
            IRunRoomLivePort
        {
            public RoomPort() : base("atomic-room")
            {
                CurrentRoomStableId = Id("room.main");
            }

            public StableId CurrentRoomStableId { get; }
        }

        private sealed class ResultPort : IRunMissionResultPort
        {
            private readonly StableId runId;

            public ResultPort(StableId runId)
            {
                this.runId = runId;
            }

            public long Sequence { get; private set; }

            public bool TryGetRun(
                StableId requestedRunId,
                out MissionRunPayload payload)
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
                    Sequence,
                    Sequence,
                    request.OperationStableId,
                    request.Fingerprint,
                    null,
                    null,
                    null,
                    "fixture-no-boxes");
            }

            public MissionRunStateResult EndRun(
                EndRunSessionCommand command,
                PlayerRouteProfilePayload route)
            {
                long before = Sequence++;
                MissionResultPayload result = MissionResultPayload.Create(
                    runId,
                    route,
                    command.CompletionState,
                    new MissionRunStrongboxResult[0],
                    Sequence,
                    0L,
                    MissionRun.Fingerprint("fixture-holdings"),
                    0L,
                    MissionRun.Fingerprint("fixture-openings"));
                return new MissionRunStateResult(
                    MissionRunStateStatus.RunEnded,
                    before,
                    Sequence,
                    command.OperationStableId,
                    command.Fingerprint,
                    null,
                    null,
                    result,
                    string.Empty);
            }
        }
    }
}
