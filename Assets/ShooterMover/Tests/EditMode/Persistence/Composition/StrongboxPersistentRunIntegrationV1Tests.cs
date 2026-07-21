using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using NUnit.Framework;
using ShooterMover.Application.Flow.Production;
using ShooterMover.Application.Missions.Results;
using ShooterMover.Application.Persistence.Accounts;
using ShooterMover.Application.Persistence.Components;
using ShooterMover.Application.Persistence.Composition;
using ShooterMover.Application.Runs.Session;
using ShooterMover.Contracts.Missions.Results;
using ShooterMover.Domain.Characters.Stats;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Persistence.Accounts;
using ShooterMover.RunConditionIntegration;

namespace ShooterMover.Tests.EditMode.Persistence.Composition
{
    public sealed partial class StrongboxPersistenceCoordinatorV1Tests
    {
        [Test]
        public void SameEndCommandRecoversAfterInnerResultTerminalizedAndSaveRepaired()
        {
            ProductionCharacterRuntimeGraphFactoryV1 factory = Factory();
            CharacterInstanceSnapshotV1 character = StarterCharacter(
                factory,
                0,
                "integrated-end-retry-owner");
            PlayerAccountSnapshotV1 durable = Account(character);
            bool failSave = true;
            var composition = new CharacterCompositionCoordinatorV1(
                new PlayerAccountSaveAuthorityV1(durable),
                factory,
                snapshot =>
                {
                    if (failSave)
                    {
                        return new PlayerAccountStoreResultV1(
                            PlayerAccountStoreStatusV1.IoFailure,
                            "simulated-integrated-save-failure",
                            null);
                    }
                    durable = snapshot;
                    return Saved(snapshot);
                });
            Assert.That(composition.Select(0).Succeeded, Is.True);
            var target = (ProductionCharacterRuntimeGraphV1)
                composition.ActiveRuntime;
            var source = (ProductionCharacterRuntimeGraphV1)
                factory.CreateStarter(
                    0,
                    character.CharacterInstanceStableId,
                    character.ClassDefinitionStableId,
                    character.DisplayName,
                    target.RoutePayload);
            BoxFixture box = AddBox(source, "integrated-end-retry", 1001UL);

            var startSource = new ProductionCharacterRunSessionStartSourceV1(
                composition,
                new IntegrationStatResolverV1(),
                new IntegrationRuntimeFactoryV1(composition, source));
            var runAuthority = new RunSessionAuthorityV1(startSource);
            StableId runId = Id("run.integrated-end-retry");
            var startCommand = new StartRunSessionCommandV1(
                Id("operation.integrated-end-start"),
                runId,
                "integrated-end-retry-material",
                target.Character.CharacterInstanceStableId,
                target.Character.Revision,
                target.Character.Fingerprint,
                Id("mission-layout.integrated-end-retry"),
                Id("difficulty.normal"),
                1001L,
                0L,
                "event-context.none");
            RunSessionStartResultV1 started = runAuthority.Start(startCommand);
            Assert.That(started.Status,
                Is.EqualTo(RunSessionStartStatusV1.Started),
                started.RejectionCode);
            RunSessionAggregateV1 run;
            Assert.That(runAuthority.TryGetRun(runId, out run), Is.True);

            MissionRunAuthorityResultV1 collected =
                run.RecordCollectedStrongbox(
                    new RunStrongboxCollectionRequestV1(
                        Id("operation.integrated-end-collect"),
                        runId,
                        run.LifecycleGeneration,
                        box.Result.DefinitionStableId,
                        box.Result.InstanceStableId,
                        box.Result.Collection.GrantStableId,
                        box.Result.Collection.SourceStableId));
            Assert.That(collected.Succeeded, Is.True,
                collected.RejectionCode);

            var endCommand = new EndRunSessionCommandV1(
                Id("operation.integrated-end"),
                runId,
                run.LifecycleGeneration,
                MissionRunCompletionStateV1.Completed,
                200L);
            RunSessionEndResultV1 failed = run.End(endCommand);

            Assert.That(failed.Status,
                Is.EqualTo(RunSessionEndStatusV1.Rejected));
            Assert.That(failed.RejectionCode,
                Does.Contain("simulated-integrated-save-failure"));
            Assert.That(run.LifecycleState,
                Is.EqualTo(RunSessionLifecycleStateV1.Active));
            Assert.That(
                target.LoadoutRuntime.Holdings.ExportSnapshot()
                    .UniqueHoldings.Any(item =>
                        item.InstanceStableId == box.Result.InstanceStableId),
                Is.False);

            failSave = false;
            RunSessionEndResultV1 recovered = run.End(endCommand);
            RunSessionEndResultV1 replay = run.End(endCommand);

            Assert.That(recovered.Status,
                Is.EqualTo(RunSessionEndStatusV1.Ended),
                recovered.RejectionCode);
            Assert.That(run.LifecycleState,
                Is.EqualTo(RunSessionLifecycleStateV1.Ended));
            Assert.That(replay, Is.SameAs(recovered));
            Assert.That(
                target.LoadoutRuntime.Holdings.ExportSnapshot()
                    .UniqueHoldings.Count(item =>
                        item.InstanceStableId == box.Result.InstanceStableId),
                Is.EqualTo(1));
            Assert.That(
                target.StrongboxAuthority.ExportSnapshot().Contexts.Count(item =>
                    item.InstanceStableId == box.Result.InstanceStableId),
                Is.EqualTo(1));
        }

        private sealed class IntegrationStatResolverV1 :
            IProductionRunStatInputResolverV1
        {
            public ProductionRunStatInputResolutionV1 Resolve(
                StartRunSessionCommandV1 command,
                StableId resolvedRunStableId,
                ProductionCharacterRuntimeGraphV1 characterGraph,
                CharacterInstanceSnapshotV1 character,
                ShooterMover.Contracts.Flow.Session.PlayerRouteProfilePayloadV1
                    currentRoutePayload,
                ShooterMover.Domain.Progression.Skills
                    .RankedSkillAllocationSnapshotV2 skillSnapshot,
                IReadOnlyList<FrozenRunEquipmentV1> frozenEquipment)
            {
                var profile = new CharacterBaseStatProfileV1(
                    "base-profile.box-persist-integration",
                    character.ClassDefinitionStableId.ToString(),
                    1,
                    "box-persist-integration-v1",
                    new Dictionary<string, decimal>
                    {
                        { DerivedStatTargetIdsV1.MaximumHealth, 100m },
                        { DerivedStatTargetIdsV1.MovementSpeed, 5m },
                        { DerivedStatTargetIdsV1.WeaponCapacity, 4m },
                        { DerivedStatTargetIdsV1.AbilityCapacity, 0m },
                    });
                return new ProductionRunStatInputResolutionV1(
                    new DerivedCharacterStatInputV1(
                        character.CharacterInstanceStableId.ToString(),
                        profile,
                        null,
                        DerivedStatPolicyV1.CreateDefault()),
                    Array.Empty<DerivedStatModifierSourceV1>(),
                    Array.Empty<string>());
            }
        }

        private sealed class IntegrationRuntimeFactoryV1 :
            IRunSessionRuntimePortFactoryV1
        {
            private readonly CharacterCompositionCoordinatorV1 composition;
            private readonly ProductionCharacterRuntimeGraphV1 source;

            public IntegrationRuntimeFactoryV1(
                CharacterCompositionCoordinatorV1 composition,
                ProductionCharacterRuntimeGraphV1 source)
            {
                this.composition = composition;
                this.source = source;
            }

            public RunSessionRuntimePortsV1 Create(
                StartRunSessionCommandV1 command,
                StableId resolvedRunStableId,
                FrozenCharacterRunInputsV1 frozenInputs)
            {
                var existing = new MissionRunExistingAuthorityPortV1(
                    source.LoadoutRuntime.Holdings,
                    source.StrongboxAuthority.ExportSnapshot);
                var missionAuthority = new MissionRunResultAuthorityV1(existing);
                var inner = new ExistingMissionResultRunPortV1(
                    missionAuthority,
                    source.LoadoutRuntime.Holdings,
                    source.StrongboxAuthority.ExportSnapshot);
                var persistent = new PersistentMissionResultRunPortV1(
                    inner,
                    composition,
                    frozenInputs,
                    composition.Account.Revision);
                var player = new IntegrationPlayerPortV1(
                    frozenInputs.Character.CharacterInstanceStableId,
                    1L,
                    Decimal.ToDouble(
                        frozenInputs.CombatProfile.MaximumHealth));
                return new RunSessionRuntimePortsV1(
                    player,
                    new IntegrationWeaponPortV1(
                        1L,
                        frozenInputs.Equipment.Select(item =>
                            item.EquipmentInstanceStableId)),
                    new IntegrationStatusPortV1(1L),
                    new IntegrationConditionalPortV1(1L),
                    new IntegrationAbilityPortV1(1L),
                    new IntegrationRoomPortV1(1L),
                    persistent);
            }
        }

        private abstract class IntegrationLifecyclePortV1 :
            IRunLifecycleRuntimePortV1
        {
            protected IntegrationLifecyclePortV1(
                string portId,
                long generation)
            {
                PortId = portId;
                Generation = generation;
            }

            protected long Generation { get; private set; }
            public string PortId { get; }
            public long LifecycleGeneration { get { return Generation; } }
            public string SnapshotFingerprint
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
                        : "integration-generation-invalid";
            }

            public RunRuntimePortRestartResultV1 Restart(
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
                    return new RunRuntimePortRestartResultV1(
                        false,
                        rejection,
                        Generation,
                        SnapshotFingerprint);
                }
                Generation = replacementLifecycleGeneration;
                return new RunRuntimePortRestartResultV1(
                    true,
                    string.Empty,
                    Generation,
                    SnapshotFingerprint);
            }
        }

        private sealed class IntegrationPlayerPortV1 :
            IntegrationLifecyclePortV1,
            IRunPlayerRuntimePortV1
        {
            private readonly StableId actor;
            private readonly StableId participant;
            private readonly double maximumHealth;

            public IntegrationPlayerPortV1(
                StableId characterId,
                long generation,
                double maximumHealth)
                : base("box-persist-player", generation)
            {
                actor = Id("actor.box-persist-" + characterId.Value);
                participant = Id("participant.box-persist-" + characterId.Value);
                this.maximumHealth = maximumHealth;
            }

            public RunPlayerRuntimeSnapshotV1 ExportSnapshot()
            {
                return new RunPlayerRuntimeSnapshotV1(
                    actor,
                    participant,
                    LifecycleGeneration,
                    maximumHealth,
                    maximumHealth,
                    0d,
                    0d,
                    0L);
            }
        }

        private sealed class IntegrationWeaponPortV1 :
            IntegrationLifecyclePortV1,
            IRunWeaponRuntimePortV1
        {
            private readonly ReadOnlyCollection<StableId> equipment;

            public IntegrationWeaponPortV1(
                long generation,
                IEnumerable<StableId> equipment)
                : base("box-persist-weapons", generation)
            {
                this.equipment = new ReadOnlyCollection<StableId>(
                    equipment.ToList());
            }

            public IReadOnlyList<StableId> FrozenEquipmentInstanceStableIds
            {
                get { return equipment; }
            }
        }

        private sealed class IntegrationStatusPortV1 :
            IntegrationLifecyclePortV1,
            IRunStatusEffectRuntimePortV1
        {
            public IntegrationStatusPortV1(long generation)
                : base("box-persist-status", generation) { }
            public int ActiveEffectCount { get { return 0; } }
        }

        private sealed class IntegrationConditionalPortV1 :
            IntegrationLifecyclePortV1,
            IRunConditionalFactRuntimePortV1
        {
            public IntegrationConditionalPortV1(long generation)
                : base("box-persist-condition", generation) { }
        }

        private sealed class IntegrationAbilityPortV1 :
            IntegrationLifecyclePortV1,
            IRunActiveAbilityRuntimePortV1
        {
            public IntegrationAbilityPortV1(long generation)
                : base("box-persist-ability", generation) { }
        }

        private sealed class IntegrationRoomPortV1 :
            IntegrationLifecyclePortV1,
            IRunRoomRuntimePortV1
        {
            public IntegrationRoomPortV1(long generation)
                : base("box-persist-room", generation) { }
            public StableId CurrentRoomStableId
            {
                get { return Id("room.box-persist-integration"); }
            }
        }
    }
}
