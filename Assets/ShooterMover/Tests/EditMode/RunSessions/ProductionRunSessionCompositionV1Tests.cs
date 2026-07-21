using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ShooterMover.Application.Flow.Production;
using ShooterMover.Application.Inventory.LoadoutScreen;
using ShooterMover.Application.Persistence.Accounts;
using ShooterMover.Application.Persistence.Composition;
using ShooterMover.Application.Runs.Session;
using ShooterMover.Contracts.Flow.Session;
using ShooterMover.Contracts.Missions.Results;
using ShooterMover.Domain.Characters.Stats;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Persistence.Accounts;
using ShooterMover.Domain.Progression.Skills;

namespace ShooterMover.Tests.EditMode.RunSessions
{
    public sealed class ProductionRunSessionCompositionV1Tests
    {
        [Test]
        public void AccountBackedCharacterFreezesExactLoadoutAndNextRunSeesHubChange()
        {
            CharacterCompositionCoordinatorV1 composition =
                CreateSelectedProductionCharacter();
            try
            {
                var graph = (ProductionCharacterRuntimeGraphV1)
                    composition.ActiveRuntime;
                var source = new ProductionCharacterRunSessionStartSourceV1(
                    composition,
                    new FixtureStatInputResolver(),
                    new FixtureRuntimePortFactory());
                var authority = new RunSessionAuthorityV1(source);

                RunSessionStartResultV1 firstStart = authority.Start(
                    Command(graph, "production-start-a", 101L));
                RunSessionAggregateV1 first;
                Assert.That(firstStart.Status,
                    Is.EqualTo(RunSessionStartStatusV1.Started));
                Assert.That(authority.TryGetRun(firstStart.RunStableId, out first),
                    Is.True);

                StableId firstSlotBefore = first.FrozenInputs.RoutePayload
                    .WeaponSlots[0].EquipmentInstanceStableId;
                StableId secondSlotBefore = first.FrozenInputs.RoutePayload
                    .WeaponSlots[1].EquipmentInstanceStableId;
                Assert.That(firstSlotBefore,
                    Is.EqualTo(ProductionStarterWeaponCatalogV1
                        .BlasterEquipmentInstanceStableId));
                Assert.That(secondSlotBefore,
                    Is.EqualTo(ProductionStarterWeaponCatalogV1
                        .ShotgunEquipmentInstanceStableId));
                Assert.That(first.FrozenInputs.Equipment.Select(item =>
                        item.EquipmentInstanceStableId),
                    Does.Contain(firstSlotBefore));
                Assert.That(first.FrozenInputs.Character,
                    Is.SameAs(graph.Character));

                InventoryLoadoutAuthoritySnapshotV1 loadoutBefore =
                    graph.LoadoutRuntime.LoadoutAuthority.ExportSnapshot();
                List<InventoryLoadoutSlotBindingV1> swapped = loadoutBefore
                    .Bindings
                    .Select(binding => new InventoryLoadoutSlotBindingV1(
                        binding.SlotStableId,
                        binding.SlotStableId == InventoryLoadoutSlotIdsV1.WeaponOne
                            ? secondSlotBefore
                            : (binding.SlotStableId
                                    == InventoryLoadoutSlotIdsV1.WeaponTwo
                                ? firstSlotBefore
                                : binding.EquipmentInstanceStableId)))
                    .ToList();
                InventoryLoadoutAuthorityResultV1 applied = graph.LoadoutRuntime
                    .LoadoutAuthority.Apply(
                        new InventoryLoadoutAuthorityCommandV1(
                            loadoutBefore.Sequence,
                            graph.LoadoutRuntime.Holdings.Sequence,
                            swapped));
                Assert.That(applied.Succeeded, Is.True, applied.RejectionCode);

                Assert.That(first.FrozenInputs.RoutePayload.WeaponSlots[0]
                        .EquipmentInstanceStableId,
                    Is.EqualTo(firstSlotBefore));
                Assert.That(first.FrozenInputs.RoutePayload.WeaponSlots[1]
                        .EquipmentInstanceStableId,
                    Is.EqualTo(secondSlotBefore));

                RunSessionStartResultV1 secondStart = authority.Start(
                    Command(graph, "production-start-b", 102L));
                RunSessionAggregateV1 second;
                Assert.That(secondStart.Status,
                    Is.EqualTo(RunSessionStartStatusV1.Started));
                Assert.That(authority.TryGetRun(secondStart.RunStableId, out second),
                    Is.True);
                Assert.That(second.FrozenInputs.RoutePayload.WeaponSlots[0]
                        .EquipmentInstanceStableId,
                    Is.EqualTo(secondSlotBefore));
                Assert.That(second.FrozenInputs.RoutePayload.WeaponSlots[1]
                        .EquipmentInstanceStableId,
                    Is.EqualTo(firstSlotBefore));
                Assert.That(second.FrozenInputs.LoadoutFingerprint,
                    Is.Not.EqualTo(first.FrozenInputs.LoadoutFingerprint));
                Assert.That(second.FrozenInputs.Fingerprint,
                    Is.Not.EqualTo(first.FrozenInputs.Fingerprint));
                Assert.That(first.RunStableId, Is.Not.EqualTo(second.RunStableId));
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
            StableId characterId = Id("character-instance.run-session");
            StableId classId = Id("loadout-profile.juggernaut");
            PlayerRouteProfilePayloadV1 legacyRoute =
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
                "Run Session Pilot",
                legacyRoute);
            IReadOnlyList<SaveComponentSnapshotV1> components =
                PlayerAccountRestoreCoordinatorV1.ExportComponents(
                    starter.SaveAdapters);
            starter.Dispose();

            var character = new CharacterInstanceSnapshotV1(
                characterId,
                classId,
                0,
                "Run Session Pilot",
                0L,
                components);
            var slots = new CharacterInstanceSnapshotV1[
                PlayerAccountSnapshotV1.CharacterSlotCount];
            slots[0] = character;
            var account = new PlayerAccountSnapshotV1(
                Id("account.run-session"),
                0L,
                slots,
                null);
            var composition = new CharacterCompositionCoordinatorV1(
                new PlayerAccountSaveAuthorityV1(account),
                factory,      }

        Saved);
            CharacterCompositionResultV1 selected = composition.Select(0);
            Assert.That(selected.Succeeded, Is.True, selected.Diagnostic);
            return composition;
        }

        private static StartRunSessionCommandV1 Command(
            ProductionCharacterRuntimeGraphV1 graph,
                   operationValue,
            long seed)
        {
            return new StartRunSessionCommandV1(
                Id("operation." + operationValue),
                null,
                "fixture-material-" + operationValue,
                graph.Character.CharacterInstanceStableId,
                graph.Character.Revision,
                graph.Character.Fingerprint,
                Id("mission-layout.level-1"),
                Id("difficulty.normal"),
                seed,
                0L,
                "event-context.integration-v1");
        }

        private static PlayerAccountStoreResultV1 Saved(
            PlayerAccountSnapshotV1 snapshot)
        {
            return new PlayerAccountStoreResultV1(
                PlayerAccountStoreStatusV1.Saved,
                string.Empty,
                snapshot);
        }

        private static StableId Id(string value)
        {
            return StableId.Parse(value);
        }

        private sealed class FixtureStatInputResolver :
            IProductionRunStatInputResolverV1
        {
            public ProductionRunStatInputResolutionV1 Resolve(
                StartRunSessionCommandV1 command,      }

        StableId resolvedRunStableId,
                ProductionCharacterRuntimeGraphV1 characterGraph,
                CharacterInstanceSnapshotV1 character,
                PlayerRouteProfilePayloadV1 currentRoutePayload,
                RankedSkillAllocationSnapshotV2 skillSnapshot,
                IReadOnlyList<FrozenRunEquipmentV1> frozenEquipment)
            {
                DerivedStatPolicyV1 policy = DerivedStatPolicyV1.CreateDefault();
                var baseValues = new Dictionary<string, decimal>
                {
                    { DerivedStatTargetIdsV1.MaximumHealth, 100m },
                    { DerivedStatTargetIdsV1.MovementSpeed, 6m },
                };
                return new ProductionRunStatInputResolutionV1(
                    new DerivedCharacterStatInputV1(
                        character.CharacterInstanceStableId.ToString(),
                        new CharacterBaseStatProfileV1(
                            "base-profile.run-session-integration",
                            character.ClassDefinitionStableId.ToString(),
                            1,
                            character.Fingerprint,
                            baseValues),
                        null,
                        policy),
                    null,
                    null);
            }
        }

        private sealed class FixtureRuntimePortFactory :
            IRunSessionRuntimePortFactoryV1
        {
            public RunSessionRuntimePortsV1 Create(
                StartRunSessionCommandV1 command,      }

        StableId resolvedRunStableId,
                FrozenCharacterRunInputsV1 frozenInputs)
            {
                const long generation = 1L;
                return new RunSessionRuntimePortsV1(
                    new FixturePlayerPort(
                        generation,
                        resolvedRunStableId,
                        (double)frozenInputs.CombatProfile.MaximumHealth),
                    new FixtureWeaponPort(
                        generation,
                        frozenInputs.Equipment
                            .Where(item => item.EquipmentDefinition.CategoryId
                                == ShooterMover.Domain.Equipment
                                    .EquipmentCategoryIds.Weapon)
                            .Select(item =>
                                item.EquipmentInstanceStableId)),
                    new FixtureStatusPort(generation),
                    new FixtureConditionalPort(generation),
                    new FixtureAbilityPort(generation),
                    new FixtureRoomPort(generation),
                    new FixtureMissionResultPort());
            }
        }

        private abstract class FixtureLifecyclePort :
            IRunLifecycleRuntimePortV1
        {
            protected FixtureLifecyclePort(string portId, long generation)
            {
                PortId = portId;
                LifecycleGeneration = generation;
            }

            public string PortId { get; }
            public long LifecycleGeneration { get; private set; }
            public virtual string SnapshotFingerprint
            {
                get
                {
                    return PortId + "|" + LifecycleGeneration;
                }
            }

            public string ValidateRestart(
                long retiringLifecycleGeneration,
                long replacementLifecycleGeneration,
                long authoritativeTick)
            {
                return retiringLifecycleGeneration == LifecycleGeneration
                    && replacementLifecycleGeneration == LifecycleGeneration + 1L
                    ? string.Empty
                    : "fixture-generation-mismatch";
            }

            public virtual RunRuntimePortRestartResultV1 Restart(      }

        StableId operationStableId,
                long retiringLifecycleGeneration,
                long replacementLifecycleGeneration,
                long authoritativeTick)
            {
                        string r = ValidateRestart(
                    retiringLifecycleGeneration,
                    replacementLifecycleGeneration,
                    authoritativeTick);
                if (!string.IsNullOrEmpty( string r))
                {
                    return new RunRuntimePortRestartResultV1(
                        false,
                        retring r,
                        LifecycleGeneration,
                        SnapshotFingerprint);
                }
                LifecycleGeneration = replacementLifecycleGeneration;
                return new RunRuntimePortRestartResultV1(
                    true,
                    string.Empty,
                    LifecycleGeneration,
                    SnapshotFingerprint);
            }
        }

        private sealed class FixturePlayerPort : FixtureLifecyclePort,
            IRunPlayerRuntimePortV1
        {
            private readonly StableId actorId;
            private readonly StableId participantId;
            private readonly double maximumHealth;

            public FixturePlayerPort(
                long generation,
                StableId runStableId,
                double maximumHealth)
                : base("fixture-player-runtime", generation)
            {
                actorId = StableId.Create("run-actor", runStableId.Value);
                participantId = StableId.Create(
                    "run-participant",
                    runStableId.Value);
                this.maximumHealth = maximumHealth;
            }

            public RunPlayerRuntimeSnapshotV1 ExportSnapshot()
            {
                return new RunPlayerRuntimeSnapshotV1(
                    actorId,      }

            participantId,
                    LifecycleGeneration,
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

        private sealed class FixtureWeaponPort : FixtureLifecyclePort,
            IRunWeaponRuntimePortV1
        {
            private readonly IReadOnlyList<StableId> equipmentIds;

            public FixtureWeaponPort(
                long generation,
                IEnumerable<StableId> equipmentIds)
                : base("fixture-weapon-runtime", generation)
            {
                this.equipmentIds = equipmentIds.OrderBy(item => item)
                    .ToList()
                    .AsReadOnly();
            }

            public IReadOnlyList<StableId> FrozenEquipmentInstanceStableIds
            {
                get { return equipmentIds; }
            }
        }

        private sealed class FixtureStatusPort : FixtureLifecyclePort,
            IRunStatusEffectRuntimePortV1
        {
            public FixtureStatusPort(long generation)
                : base("fixture-status-runtime", generation) { }
            public int ActiveEffectCount { get { return 0; } }
        }

        private sealed class FixtureConditionalPort : FixtureLifecyclePort,
            IRunConditionalFactRuntimePortV1
        {
            public FixtureConditionalPort(long generation)
                : base("fixture-condition-runtime", generation) { }
        }

        private sealed class FixtureAbilityPort : FixtureLifecyclePort,
            IRunActiveAbilityRuntimePortV1
        {
            public FixtureAbilityPort(long generation)
                : base("fixture-ability-runtime", generation) { }
        }

        private sealed class FixtureRoomPort : FixtureLifecyclePort,
            IRunRoomRuntimePortV1
        {
            public FixtureRoomPort(long generation)
                : base("fixture-room-runtime", generation) { }
            public StableId CurrentRoomStableId
            {
                get { return Id("room.start"); }
            }
        }

        private sealed class FixtureMissionResultPort : IRunMissionResultPortV1
        {
            public long Sequence { get { return 0L; } }

            public bool TryGetRun(      }

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
                EndRunSessionCommandV1 command,      }

        PlayerRouteProfilePayloadV1 routePayload)
            {
                return Invalid(
                    command == null ? null : command.OperationStableId,
                    command == null ? string.Empty : command.Fingerprint);
            }

            private static MissionRunAuthorityResultV1 Invalid(
                StableId operationStableId,
                        squestFingerprint)
            {
                return new MissionRunAuthorityResultV1(
                    MissionRunAuthorityStatusV1.InvalidRequest,
                    0L,
                    0L,
                    operationStableId,
                    requestFingerprint,
                    null,
                    null,
                    null,
                    "fixture-unused-mission-port");
            }
        }
    }
}
