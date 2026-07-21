using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ShooterMover.Application.Flow.Production;
using ShooterMover.Application.Inventory.LoadoutScreen;
using ShooterMover.Application.Persistence.Accounts;
using ShooterMover.Application.Persistence.Composition;
using ShooterMover.Application.Runs.Session;
using ShooterMover.ConditionRuntime;
using ShooterMover.Contracts.Flow.Session;
using ShooterMover.Contracts.Missions.Results;
using ShooterMover.Domain.Characters.Stats;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Enemies;
using ShooterMover.Domain.Persistence.Accounts;
using ShooterMover.Domain.Progression.Skills;
using ShooterMover.EnemyRuntimeComposition;
using ShooterMover.RunConditionIntegration;

namespace ShooterMover.Tests.EditMode.RunConditionBinding
{
    public sealed class ProductionConditionBoundRunSessionStartSourceV1Tests
    {
        [Test]
        public void AccountBackedCharacterCreatesConditionRunAndRestartRetiresFacts()
        {
            CharacterCompositionCoordinatorV1 composition =
                CreateSelectedProductionCharacter();
            try
            {
                var graph = (ProductionCharacterRuntimeGraphV1)
                    composition.ActiveRuntime;
                string permanentFingerprint = graph.Character.Fingerprint;
                var source = new ProductionConditionBoundRunSessionStartSourceV1(
                    composition,
                    new StatResolver(),
                    new RuntimeFactory(),
                    new DefinitionProvider());
                var authority = new RunSessionAuthorityV1(source);
                RunSessionStartResultV1 started = authority.Start(
                    Command(graph, "condition-account-backed", 701L));
                RunSessionAggregateV1 run;

                Assert.That(started.Status,
                    Is.EqualTo(RunSessionStartStatusV1.Started),
                    started.RejectionCode);
                Assert.That(authority.TryGetRun(started.RunStableId, out run),
                    Is.True);
                Assert.That(run.RuntimePorts.ConditionalFacts,
                    Is.TypeOf<ExistingConditionRuntimeRunPortV1>());
                Assert.That(run.RuntimePorts.StatusEffects,
                    Is.TypeOf<ConditionOwnedStatusEffectRunPortV1>());

                RunPlayerRuntimeSnapshotV1 player =
                    run.RuntimePorts.Player.ExportSnapshot();
                for (int ordinal = 1; ordinal <= 3; ordinal++)
                {
                    RunConditionDeliveryResultV1 delivery =
                        run.DeliverConditionGameplayFact(
                            new RunConditionGameplayFactCommandV1(
                                Id("operation.account-kill-" + ordinal),
                                Death(run.RunStableId, player, ordinal),
                                run.RunStableId,
                                1L,
                                player.ActorInstanceStableId,
                                player.ParticipantStableId,
                                graph.Character.CharacterInstanceStableId,
                                1L,
                                ordinal));
                    Assert.That(delivery.Status,
                        Is.EqualTo(RunConditionDeliveryStatusV1.Applied));
                }
                Assert.That(run.ExportConditionModifierProjection(
                        player.ParticipantStableId)
                        .Evaluate(
                            DerivedStatTargetIdsV1.OutgoingDamageMultiplier,
                            1m)
                        .FinalValue,
                    Is.EqualTo(1.5m));

                RunSessionRestartResultV1 restart = run.Restart(
                    new RestartRunSessionCommandV1(
                        Id("operation.account-condition-restart"),
                        run.RunStableId,
                        1L,
                        2L,
                        10L,
                        RunRestartPolicyV1.FullTransientReset()));
                Assert.That(restart.Status,
                    Is.EqualTo(RunSessionRestartStatusV1.Applied));
                Assert.That(run.ExportConditionRuntimeSnapshot().AcceptedFactCount,
                    Is.Zero);
                Assert.That(run.RuntimePorts.StatusEffects.ActiveEffectCount,
                    Is.Zero);

                RunConditionDeliveryResultV1 stale =
                    run.DeliverConditionGameplayFact(
                        new RunConditionGameplayFactCommandV1(
                            Id("operation.account-stale-kill"),
                            Death(run.RunStableId, player, 20),
                            run.RunStableId,
                            1L,
                            player.ActorInstanceStableId,
                            player.ParticipantStableId,
                            graph.Character.CharacterInstanceStableId,
                            1L,
                            11L));
                Assert.That(stale.Status,
                    Is.EqualTo(RunConditionDeliveryStatusV1.StaleLifecycle));
                Assert.That(graph.Character.Fingerprint,
                    Is.EqualTo(permanentFingerprint));
            }
            finally
            {
                composition.Dispose();
            }
        }

        private static CharacterCompositionCoordinatorV1
            CreateSelectedProductionCharacter()
        {
            ProductionCharacterRuntimeGraphFactoryV1 factory =
                ProductionCharacterRuntimeGraphFactoryV1
                    .CreateVerticalSliceDefaults();
            StableId characterId = Id("character-instance.condition-run");
            StableId classId = Id("loadout-profile.juggernaut");
            PlayerRouteProfilePayloadV1 route =
                PlayerRouteProfilePayloadV1.Create(
                    Id("character.frontier"),
                    classId,
                    new[]
                    {
                        ProductionStarterWeaponCatalogV1
                            .BlasterEquipmentInstanceStableId,
                        ProductionStarterWeaponCatalogV1
                            .ShotgunEquipmentInstanceStableId,
                        ProductionStarterWeaponCatalogV1
                            .RocketEquipmentInstanceStableId,
                        ProductionStarterWeaponCatalogV1
                            .ArcEquipmentInstanceStableId,
                    });
            ICharacterRuntimeGraphV1 starter = factory.CreateStarter(
                0,
                characterId,
                classId,
                "Condition Run Pilot",
                route);
            IReadOnlyList<SaveComponentSnapshotV1> components =
                PlayerAccountRestoreCoordinatorV1.ExportComponents(
                    starter.SaveAdapters);
            starter.Dispose();

            var character = new CharacterInstanceSnapshotV1(
                characterId,
                classId,
                0,
                "Condition Run Pilot",
                0L,
                components);
            var slots = new CharacterInstanceSnapshotV1[
                PlayerAccountSnapshotV1.CharacterSlotCount];
            slots[0] = character;
            var account = new PlayerAccountSnapshotV1(
                Id("account.condition-run"),
                0L,
                slots,
                null);
            var composition = new CharacterCompositionCoordinatorV1(
                new PlayerAccountSaveAuthorityV1(account),
                factory,
                Saved);
            CharacterCompositionResultV1 selected = composition.Select(0);
            Assert.That(selected.Succeeded, Is.True, selected.Diagnostic);
            return composition;
        }

        private static StartRunSessionCommandV1 Command(
            ProductionCharacterRuntimeGraphV1 graph,
            string operation,
            long seed)
        {
            return new StartRunSessionCommandV1(
                Id("operation." + operation),
                null,
                "fixture-material-" + operation,
                graph.Character.CharacterInstanceStableId,
                graph.Character.Revision,
                graph.Character.Fingerprint,
                Id("mission-layout.level-1"),
                Id("difficulty.normal"),
                seed,
                0L,
                "event-context.condition-bind");
        }

        private static PlayerAccountStoreResultV1 Saved(
            PlayerAccountSnapshotV1 snapshot)
        {
            return new PlayerAccountStoreResultV1(
                PlayerAccountStoreStatusV1.Saved,
                string.Empty,
                snapshot);
        }

        private static EnemyDeathFactV1 Death(
            StableId runId,
            RunPlayerRuntimeSnapshotV1 player,
            int ordinal)
        {
            string suffix = ordinal.ToString();
            var identity = new EnemyRuntimeIdentityV1(
                Id("enemy.account-" + suffix),
                Id("participant.enemy-account-" + suffix),
                runId,
                Id("room-runtime.main"),
                Id("room.main"),
                Id("placement.enemy-account-" + suffix));
            return new EnemyDeathFactV1(
                Id("death.account-" + suffix),
                Id("damage.account-" + suffix),
                identity,
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

        private sealed class StatResolver : IProductionRunStatInputResolverV1
        {
            public ProductionRunStatInputResolutionV1 Resolve(
                StartRunSessionCommandV1 command,
                StableId runId,
                ProductionCharacterRuntimeGraphV1 graph,
                CharacterInstanceSnapshotV1 character,
                PlayerRouteProfilePayloadV1 route,
                RankedSkillAllocationSnapshotV2 skills,
                IReadOnlyList<FrozenRunEquipmentV1> equipment)
            {
                DerivedStatPolicyV1 policy = DerivedStatPolicyV1.CreateDefault();
                return new ProductionRunStatInputResolutionV1(
                    new DerivedCharacterStatInputV1(
                        character.CharacterInstanceStableId.ToString(),
                        new CharacterBaseStatProfileV1(
                            "base-profile.condition-run",
                            character.ClassDefinitionStableId.ToString(),
                            1,
                            character.Fingerprint,
                            new Dictionary<string, decimal>
                            {
                                { DerivedStatTargetIdsV1.MaximumHealth, 100m },
                                { DerivedStatTargetIdsV1.MovementSpeed, 6m },
                            }),
                        null,
                        policy),
                    null,
                    null);
            }
        }

        private sealed class DefinitionProvider :
            IRunConditionDefinitionProviderV1
        {
            public ConditionEffectRuntimeDefinitionV1 Resolve(
                StableId runId,
                FrozenCharacterRunInputsV1 frozen,
                RunConditionParticipantSeedV1 participant)
            {
                return new FactWindowEffectFixtureV1(
                    "condition.enemy-kill-burst",
                    "status-effect.enemy-kill-burst",
                    ConditionRuntimeFactTypeIdsV1.EnemyKilled,
                    3,
                    10L,
                    5L,
                    1.5m)
                    .Build(
                        "condition-runtime.account-fixture",
                        "1.0.0",
                        "conditional-source.account-fixture");
            }
        }

        private sealed class RuntimeFactory :
            IRunSessionNonConditionRuntimePortFactoryV1
        {
            public RunSessionNonConditionRuntimePortsV1 Create(
                StartRunSessionCommandV1 command,
                StableId runId,
                FrozenCharacterRunInputsV1 frozen)
            {
                return new RunSessionNonConditionRuntimePortsV1(
                    new PlayerPort(runId,
                        (double)frozen.CombatProfile.MaximumHealth),
                    new WeaponPort(frozen.Equipment
                        .Where(item => item.EquipmentDefinition.CategoryId
                            == ShooterMover.Domain.Equipment
                                .EquipmentCategoryIds.Weapon)
                        .Select(item => item.EquipmentInstanceStableId)),
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
            private readonly StableId actorId;
            private readonly StableId participantId;
            private readonly double maximumHealth;

            public PlayerPort(StableId runId, double maximumHealth)
                : base("fixture-player")
            {
                actorId = StableId.Create("run-actor", runId.Value);
                participantId = StableId.Create("run-participant", runId.Value);
                this.maximumHealth = maximumHealth;
            }

            public RunPlayerRuntimeSnapshotV1 ExportSnapshot()
            {
                return new RunPlayerRuntimeSnapshotV1(
                    actorId,
                    participantId,
                    Generation,
                    maximumHealth,
                    maximumHealth,
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
                : base("fixture-weapons")
            {
                this.equipment = equipment.OrderBy(item => item)
                    .ToList()
                    .AsReadOnly();
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

        private sealed class RoomPort : LifecyclePort,
            IRunRoomRuntimePortV1
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
