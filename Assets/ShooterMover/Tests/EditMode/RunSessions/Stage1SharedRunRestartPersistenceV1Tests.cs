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
    public sealed class Stage1SharedRunRestartPersistenceV1Tests
    {
        [Test]
        public void AuthoritativeRestart_PreservesAggregateFrozenInputsAndReplayLedgers()
        {
            var source = new StartSource();
            var authority = new RunSessionAuthorityV1(source);
            RunSessionStartResultV1 started = authority.Start(source.Command());
            RunSessionAggregateV1 run;

            Assert.That(started.Status,
                Is.EqualTo(RunSessionStartStatusV1.Started));
            Assert.That(authority.TryGetRun(started.RunStableId, out run),
                Is.True);

            RunSessionAggregateV1 originalAggregate = run;
            StableId originalRunId = run.RunStableId;
            string frozenFingerprint = run.FrozenInputs.Fingerprint;
            string equipmentFingerprint = string.Join(
                ";",
                run.FrozenInputs.Equipment.Select(item => item.Fingerprint));

            StableId factOperation = Id("operation.before-restart-fact");
            var acceptedFact = new RunSessionFactEnvelopeV1(
                factOperation,
                run.RunStableId,
                1L,
                RunSessionFactKindV1.Projectile,
                "fact.before-restart");
            Assert.That(run.AdmitFact(acceptedFact).Status,
                Is.EqualTo(RunSessionFactAdmissionStatusV1.Accepted));

            StableId localOperation = Id("operation.before-restart-pickup");
            var acceptedLocal = new RunLocalMutationCommandV1(
                localOperation,
                run.RunStableId,
                1L,
                RunLocalMutationKindV1.AddTemporaryPickup,
                "pickup.before-restart",
                1L,
                "pickup.before-restart");
            Assert.That(run.ApplyLocalMutation(acceptedLocal).Accepted, Is.True);

            var restart = new RestartRunSessionCommandV1(
                Id("operation.shared-run-restart"),
                run.RunStableId,
                1L,
                2L,
                run.AuthoritativeTick,
                RunRestartPolicyV1.FullTransientReset());
            RunSessionRestartResultV1 applied = run.Restart(restart);
            RunSessionRestartResultV1 exactReplay = run.Restart(restart);
            RunSessionRestartResultV1 conflict = run.Restart(
                new RestartRunSessionCommandV1(
                    restart.OperationStableId,
                    run.RunStableId,
                    1L,
                    2L,
                    run.AuthoritativeTick + 1L,
                    RunRestartPolicyV1.FullTransientReset()));

            RunSessionAggregateV1 resolvedAfterRestart;
            Assert.That(authority.TryGetRun(
                    originalRunId,
                    out resolvedAfterRestart),
                Is.True);
            Assert.That(resolvedAfterRestart, Is.SameAs(originalAggregate));
            Assert.That(authority.RunCount, Is.EqualTo(1));
            Assert.That(run.RunStableId, Is.EqualTo(originalRunId));
            Assert.That(run.LifecycleGeneration, Is.EqualTo(2L));
            Assert.That(run.FrozenInputs.Fingerprint,
                Is.EqualTo(frozenFingerprint));
            Assert.That(string.Join(
                    ";",
                    run.FrozenInputs.Equipment.Select(item => item.Fingerprint)),
                Is.EqualTo(equipmentFingerprint));
            Assert.That(run.ExportLocalState().TemporaryPickups, Is.Empty);

            Assert.That(applied.Status,
                Is.EqualTo(RunSessionRestartStatusV1.Applied));
            Assert.That(exactReplay, Is.SameAs(applied));
            Assert.That(conflict.Status,
                Is.EqualTo(RunSessionRestartStatusV1.ConflictingDuplicate));

            Assert.That(run.AdmitFact(acceptedFact).Status,
                Is.EqualTo(RunSessionFactAdmissionStatusV1.StaleLifecycle));
            Assert.That(run.AdmitFact(
                    new RunSessionFactEnvelopeV1(
                        factOperation,
                        run.RunStableId,
                        2L,
                        RunSessionFactKindV1.Projectile,
                        "fact.reused-after-restart"))
                    .Status,
                Is.EqualTo(
                    RunSessionFactAdmissionStatusV1.ConflictingDuplicate));

            RunLocalMutationResultV1 reusedLocal = run.ApplyLocalMutation(
                new RunLocalMutationCommandV1(
                    localOperation,
                    run.RunStableId,
                    2L,
                    RunLocalMutationKindV1.AddTemporaryPickup,
                    "pickup.reused-after-restart",
                    1L,
                    "pickup.reused-after-restart"));
            Assert.That(reusedLocal.Accepted, Is.False);
            Assert.That(reusedLocal.ConflictingDuplicate, Is.True);

            Assert.That(run.AdmitFact(
                    new RunSessionFactEnvelopeV1(
                        Id("operation.after-restart-fact"),
                        run.RunStableId,
                        2L,
                        RunSessionFactKindV1.Projectile,
                        "fact.after-restart"))
                    .Status,
                Is.EqualTo(RunSessionFactAdmissionStatusV1.Accepted));
            Assert.That(run.ApplyLocalMutation(
                    new RunLocalMutationCommandV1(
                        Id("operation.after-restart-pickup"),
                        run.RunStableId,
                        2L,
                        RunLocalMutationKindV1.AddTemporaryPickup,
                        "pickup.after-restart",
                        1L,
                        "pickup.after-restart"))
                    .Accepted,
                Is.True);
        }

        private static StableId Id(string value)
        {
            return StableId.Parse(value);
        }

        private sealed class StartSource : IRunSessionStartSourceV1
        {
            private readonly CharacterInstanceSnapshotV1 character;

            public StartSource()
            {
                character = new CharacterInstanceSnapshotV1(
                    Id("character-instance.shared-run-restart"),
                    Id("loadout-profile.striker"),
                    0,
                    "Shared Run Pilot",
                    3L,
                    null);
            }

            public StartRunSessionCommandV1 Command()
            {
                return new StartRunSessionCommandV1(
                    Id("operation.start-shared-run-restart"),
                    Id("run.shared-run-restart"),
                    "shared-run-restart-material",
                    character.CharacterInstanceStableId,
                    character.Revision,
                    character.Fingerprint,
                    Id("mission-layout.level-1"),
                    Id("difficulty.normal"),
                    73L,
                    0L,
                    "event-context.shared-run-restart");
            }

            public RunSessionStartMaterialV1 Resolve(
                StartRunSessionCommandV1 command,
                StableId resolvedRunStableId)
            {
                StableId definitionId =
                    Id("equipment-definition.shared-run-rifle");
                StableId qualityId = Id("quality.common");
                EquipmentDefinition definition = EquipmentDefinition.Create(
                    definitionId,
                    EquipmentCategoryIds.Weapon,
                    Id("equipment-family.shared-run-rifle"),
                    "Shared Run Rifle",
                    Id("weapon.shared-run-rifle"),
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
                    Id("equipment-instance.shared-run-rifle"),
                    definitionId,
                    10,
                    qualityId,
                    null);
                PlayerRouteProfilePayloadV1 route =
                    PlayerRouteProfilePayloadV1.Create(
                        character.CharacterInstanceStableId,
                        character.ClassDefinitionStableId,
                        new[]
                        {
                            equipment.InstanceId,
                            null,
                            null,
                            null,
                        });
                DerivedStatPolicyV1 policy =
                    DerivedStatPolicyV1.CreateDefault();
                var characterInput = new DerivedCharacterStatInputV1(
                    character.CharacterInstanceStableId.ToString(),
                    new CharacterBaseStatProfileV1(
                        "base-profile.shared-run-restart",
                        character.ClassDefinitionStableId.ToString(),
                        1,
                        character.Fingerprint,
                        new Dictionary<string, decimal>
                        {
                            { DerivedStatTargetIdsV1.MaximumHealth, 100m },
                            { DerivedStatTargetIdsV1.MovementSpeed, 6m },
                        }),
                    null,
                    policy);
                var composer = new DefaultDerivedCharacterStatComposerV1();
                DerivedCharacterStatsSnapshotV1 stats =
                    composer.DeriveCharacter(characterInput);
                RunCombatProfileV1 combat = composer.BuildRunProfile(
                    new RunCombatProfileInputV1(
                        resolvedRunStableId.ToString(),
                        command.Fingerprint,
                        stats,
                        null,
                        null,
                        policy));
                var skills = new RankedSkillAllocationSnapshotV2(
                    "skill-profile.shared-run-restart",
                    character.ClassDefinitionStableId.ToString(),
                    0L,
                    "1",
                    "shared-run-restart",
                    null);
                var frozen = new FrozenCharacterRunInputsV1(
                    character,
                    route,
                    0L,
                    "loadout.shared-run-restart",
                    0L,
                    "holdings.shared-run-restart",
                    skills,
                    stats,
                    combat,
                    new[]
                    {
                        new FrozenRunEquipmentV1(
                            Id("weapon-slot.slot-1"),
                            equipment,
                            definition),
                    },
                    command.EventModifierContextFingerprint);
                var player = new PlayerPort(
                    Id("actor.shared-run-player"),
                    Id("participant.shared-run-player"),
                    1L);
                return RunSessionStartMaterialV1.Accept(
                    frozen,
                    new RunSessionRuntimePortsV1(
                        player,
                        new WeaponPort(
                            1L,
                            new[] { equipment.InstanceId }),
                        new StatusPort(1L),
                        new ConditionPort(1L),
                        new AbilityPort(1L),
                        new RoomPort(1L),
                        new MissionPort()));
            }
        }

        private abstract class LifecyclePort : IRunLifecycleRuntimePortV1
        {
            protected LifecyclePort(string portId, long generation)
            {
                PortId = portId;
                Generation = generation;
            }

            protected long Generation { get; private set; }
            public string PortId { get; }
            public long LifecycleGeneration { get { return Generation; } }
            public virtual string SnapshotFingerprint
            {
                get { return PortId + "|" + Generation; }
            }

            public string ValidateRestart(
                long retiringLifecycleGeneration,
                long replacementLifecycleGeneration,
                long authoritativeTick)
            {
                return retiringLifecycleGeneration == Generation
                    && replacementLifecycleGeneration == Generation + 1L
                    ? string.Empty
                    : "generation-mismatch";
            }

            public virtual RunRuntimePortRestartResultV1 Restart(
                StableId operationStableId,
                long retiringLifecycleGeneration,
                long replacementLifecycleGeneration,
                long authoritativeTick)
            {
                string rejection = ValidateRestart(
                    retiringLifecycleGeneration,
                    replacementLifecycleGeneration,
                    authoritativeTick);
                if (string.IsNullOrEmpty(rejection))
                {
                    Generation = replacementLifecycleGeneration;
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
            private readonly StableId actorId;
            private readonly StableId participantId;

            public PlayerPort(
                StableId actorId,
                StableId participantId,
                long generation)
                : base("player-runtime", generation)
            {
                this.actorId = actorId;
                this.participantId = participantId;
            }

            public RunPlayerRuntimeSnapshotV1 ExportSnapshot()
            {
                return new RunPlayerRuntimeSnapshotV1(
                    actorId,
                    participantId,
                    LifecycleGeneration,
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

            public WeaponPort(long generation, IEnumerable<StableId> equipment)
                : base("weapon-runtime", generation)
            {
                this.equipment = equipment.ToList().AsReadOnly();
            }

            public IReadOnlyList<StableId> FrozenEquipmentInstanceStableIds
            {
                get { return equipment; }
            }
        }

        private sealed class StatusPort : LifecyclePort,
            IRunStatusEffectRuntimePortV1
        {
            public StatusPort(long generation)
                : base("status-runtime", generation) { }
            public int ActiveEffectCount { get { return 0; } }
        }

        private sealed class ConditionPort : LifecyclePort,
            IRunConditionalFactRuntimePortV1
        {
            public ConditionPort(long generation)
                : base("condition-runtime", generation) { }
        }

        private sealed class AbilityPort : LifecyclePort,
            IRunActiveAbilityRuntimePortV1
        {
            public AbilityPort(long generation)
                : base("ability-runtime", generation) { }
        }

        private sealed class RoomPort : LifecyclePort,
            IRunRoomRuntimePortV1
        {
            public RoomPort(long generation)
                : base("room-runtime", generation) { }
            public StableId CurrentRoomStableId
            {
                get { return Id("room.entry"); }
            }
        }

        private sealed class MissionPort : IRunMissionResultPortV1
        {
            public long Sequence { get; private set; }

            public bool TryGetRun(
                StableId runStableId,
                out MissionRunPayloadV1 runPayload)
            {
                runPayload = null;
                return false;
            }

            public MissionRunAuthorityResultV1 RecordCollectedStrongbox(
                RunStrongboxCollectionRequestV1 request,
                PlayerRouteProfilePayloadV1 routePayload)
            {
                return Invalid(
                    request == null ? null : request.OperationStableId,
                    request == null ? string.Empty : request.Fingerprint);
            }

            public MissionRunAuthorityResultV1 EndRun(
                EndRunSessionCommandV1 command,
                PlayerRouteProfilePayloadV1 routePayload)
            {
                return Invalid(
                    command == null ? null : command.OperationStableId,
                    command == null ? string.Empty : command.Fingerprint);
            }

            private MissionRunAuthorityResultV1 Invalid(
                StableId operationStableId,
                string fingerprint)
            {
                return new MissionRunAuthorityResultV1(
                    MissionRunAuthorityStatusV1.InvalidRequest,
                    Sequence,
                    Sequence,
                    operationStableId,
                    fingerprint,
                    null,
                    null,
                    null,
                    "fixture-not-used");
            }
        }
    }
}
