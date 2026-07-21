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
    public sealed class RunConditionRestartAtomicityV1Tests
    {
        [Test]
        public void FailedDefinitionPrevalidationLeavesEveryPortIntactAndNewOperationCanRetry()
        {
            var definitionProvider = new ToggleDefinitionProvider();
            var source = new Source(definitionProvider);
            var authority = new RunSessionAuthorityV1(source);
            RunSessionStartResultV1 started = authority.Start(source.Command());
            RunSessionAggregateV1 run;
            Assert.That(started.Status,
                Is.EqualTo(RunSessionStartStatusV1.Started),
                started.RejectionCode);
            Assert.That(authority.TryGetRun(started.RunStableId, out run),
                Is.True);

            RunPlayerRuntimeSnapshotV1 player =
                run.RuntimePorts.Player.ExportSnapshot();
            for (int ordinal = 1; ordinal <= 3; ordinal++)
            {
                RunConditionDeliveryResultV1 delivery =
                    run.DeliverConditionGameplayFact(
                        new RunConditionGameplayFactCommandV1(
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
                    Is.EqualTo(RunConditionDeliveryStatusV1.Applied));
            }

            RunConditionRuntimeSnapshotV1 before =
                run.ExportConditionRuntimeSnapshot();
            string permanentFingerprint = source.Character.Fingerprint;
            definitionProvider.FailReplacement = true;
            var rejectedCommand = new RestartRunSessionCommandV1(
                Id("operation.atomic-restart-rejected"),
                run.RunStableId,
                1L,
                2L,
                10L,
                RunRestartPolicyV1.FullTransientReset());

            RunSessionRestartResultV1 rejected = run.Restart(rejectedCommand);
            RunSessionRestartResultV1 rejectedReplay =
                run.Restart(rejectedCommand);

            Assert.That(rejected.Status,
                Is.EqualTo(RunSessionRestartStatusV1.Rejected));
            Assert.That(rejected.RejectionCode,
                Does.Contain("condition-runtime-reconstruction-prevalidation-failed"));
            Assert.That(rejectedReplay, Is.SameAs(rejected));
            Assert.That(run.LifecycleGeneration, Is.EqualTo(1L));
            Assert.That(run.RuntimePorts.Player.LifecycleGeneration,
                Is.EqualTo(1L));
            Assert.That(run.RuntimePorts.Weapons.LifecycleGeneration,
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
            RunSessionRestartResultV1 retried = run.Restart(
                new RestartRunSessionCommandV1(
                    Id("operation.atomic-restart-retry"),
                    run.RunStableId,
                    1L,
                    2L,
                    10L,
                    RunRestartPolicyV1.FullTransientReset()));

            Assert.That(retried.Status,
                Is.EqualTo(RunSessionRestartStatusV1.Applied));
            Assert.That(run.LifecycleGeneration, Is.EqualTo(2L));
            Assert.That(run.ExportConditionRuntimeSnapshot().AcceptedFactCount,
                Is.Zero);
            Assert.That(run.RuntimePorts.StatusEffects.ActiveEffectCount,
                Is.Zero);
            Assert.That(source.Character.Fingerprint,
                Is.EqualTo(permanentFingerprint));
        }

        private static EnemyDeathFactV1 Death(
            StableId runId,
            RunPlayerRuntimeSnapshotV1 player,
            int ordinal)
        {
            string suffix = ordinal.ToString();
            return new EnemyDeathFactV1(
                Id("death.atomic-" + suffix),
                Id("damage.atomic-" + suffix),
                new EnemyRuntimeIdentityV1(
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
            IRunConditionDefinitionProviderV1
        {
            public bool FailReplacement { get; set; }

            public ConditionEffectRuntimeDefinitionV1 Resolve(
                StableId runId,
                FrozenCharacterRunInputsV1 frozen,
                RunConditionParticipantSeedV1 participant)
            {
                if (FailReplacement
                    && participant.ActorLifecycleGeneration > 1L)
                {
                    throw new InvalidOperationException(
                        "fixture replacement definition unavailable");
                }
                return new FactWindowEffectFixtureV1(
                    "condition.enemy-kill-burst",
                    "status-effect.enemy-kill-burst",
                    ConditionRuntimeFactTypeIdsV1.EnemyKilled,
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

        private sealed class Source : IRunSessionStartSourceV1
        {
            private readonly ProductionConditionBoundRunSessionRuntimePortFactoryV1
                factory;

            public Source(IRunConditionDefinitionProviderV1 definitionProvider)
            {
                Character = new CharacterInstanceSnapshotV1(
                    Id("character.atomic"),
                    Id("loadout-profile.striker"),
                    0,
                    "Atomic Pilot",
                    3L,
                    null);
                factory =
                    new ProductionConditionBoundRunSessionRuntimePortFactoryV1(
                        new BaseFactory(),
                        definitionProvider);
            }

            public CharacterInstanceSnapshotV1 Character { get; }

            public StartRunSessionCommandV1 Command()
            {
                return new StartRunSessionCommandV1(
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

            public RunSessionStartMaterialV1 Resolve(
                StartRunSessionCommandV1 command,
                StableId runId)
            {
                FrozenCharacterRunInputsV1 frozen = BuildFrozen(command, runId);
                return RunSessionStartMaterialV1.Accept(
                    frozen,
                    factory.Create(command, runId, frozen));
            }

            private FrozenCharacterRunInputsV1 BuildFrozen(
                StartRunSessionCommandV1 command,
                StableId runId)
            {
                StableId definitionId =
                    Id("equipment-definition.atomic-rifle");
                StableId qualityId = Id("quality.common");
                EquipmentDefinition definition = EquipmentDefinition.Create(
                    definitionId,
                    EquipmentCategoryIds.Weapon,
                    Id("equipment-family.atomic-rifle"),
                    "Atomic Rifle",
                    Id("weapon.atomic-rifle"),
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
                PlayerRouteProfilePayloadV1 route =
                    PlayerRouteProfilePayloadV1.Create(
                        Character.CharacterInstanceStableId,
                        Character.ClassDefinitionStableId,
                        new[] { equipment.InstanceId, null, null, null });
                DerivedStatPolicyV1 policy =
                    DerivedStatPolicyV1.CreateDefault();
                var composer = new DefaultDerivedCharacterStatComposerV1();
                DerivedCharacterStatsSnapshotV1 stats =
                    composer.DeriveCharacter(
                        new DerivedCharacterStatInputV1(
                            Character.CharacterInstanceStableId.ToString(),
                            new CharacterBaseStatProfileV1(
                                "base-profile.atomic",
                                Character.ClassDefinitionStableId.ToString(),
                                1,
                                Character.Fingerprint,
                                new Dictionary<string, decimal>
                                {
                                    {
                                        DerivedStatTargetIdsV1.MaximumHealth,
                                        100m
                                    },
                                    {
                                        DerivedStatTargetIdsV1.MovementSpeed,
                                        5m
                                    },
                                    {
                                        DerivedStatTargetIdsV1.WeaponCapacity,
                                        4m
                                    },
                                    {
                                        DerivedStatTargetIdsV1.AbilityCapacity,
                                        0m
                                    },
                                }),
                            null,
                            policy));
                RunCombatProfileV1 profile = composer.BuildRunProfile(
                    new RunCombatProfileInputV1(
                        runId.ToString(),
                        command.Fingerprint,
                        stats,
                        null,
                        null,
                        policy));
                var skills = new RankedSkillAllocationSnapshotV2(
                    "skill-profile.atomic",
                    Character.ClassDefinitionStableId.ToString(),
                    1L,
                    "1",
                    "fixture",
                    null);
                return new FrozenCharacterRunInputsV1(
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
                        new FrozenRunEquipmentV1(
                            Id("weapon-slot.slot-1"),
                            equipment,
                            definition),
                    },
                    command.EventModifierContextFingerprint);
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

            public RunRuntimePortRestartResultV1 Restart(
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
                return new RunRuntimePortRestartResultV1(
                    string.IsNullOrEmpty(rejection),
                    rejection,
                    Generation,
                    SnapshotFingerprint);
            }
        }

        private sealed class PlayerPort : LifecyclePort,
            IRunPlayerRuntimePortV1
        {
            public PlayerPort() : base("atomic-player") { }

            public RunPlayerRuntimeSnapshotV1 ExportSnapshot()
            {
                return new RunPlayerRuntimeSnapshotV1(
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

        private sealed class WeaponPort : LifecyclePort,
            IRunWeaponRuntimePortV1
        {
            private readonly IReadOnlyList<StableId> equipment;

            public WeaponPort(IEnumerable<StableId> equipment)
                : base("atomic-weapons")
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
            public AbilityPort() : base("atomic-abilities") { }
        }

        private sealed class RoomPort : LifecyclePort,
            IRunRoomRuntimePortV1
        {
            public RoomPort() : base("atomic-room")
            {
                CurrentRoomStableId = Id("room.main");
            }

            public StableId CurrentRoomStableId { get; }
        }

        private sealed class ResultPort : IRunMissionResultPortV1
        {
            private readonly StableId runId;

            public ResultPort(StableId runId)
            {
                this.runId = runId;
            }

            public long Sequence { get; private set; }

            public bool TryGetRun(
                StableId requestedRunId,
                out MissionRunPayloadV1 payload)
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
                    Sequence,
                    Sequence,
                    request.OperationStableId,
                    request.Fingerprint,
                    null,
                    null,
                    null,
                    "fixture-no-boxes");
            }

            public MissionRunAuthorityResultV1 EndRun(
                EndRunSessionCommandV1 command,
                PlayerRouteProfilePayloadV1 route)
            {
                long before = Sequence++;
                MissionResultPayloadV1 result = MissionResultPayloadV1.Create(
                    runId,
                    route,
                    command.CompletionState,
                    new MissionRunStrongboxResultV1[0],
                    Sequence,
                    0L,
                    MissionRunCanonicalV1.Fingerprint("fixture-holdings"),
                    0L,
                    MissionRunCanonicalV1.Fingerprint("fixture-openings"));
                return new MissionRunAuthorityResultV1(
                    MissionRunAuthorityStatusV1.RunEnded,
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
