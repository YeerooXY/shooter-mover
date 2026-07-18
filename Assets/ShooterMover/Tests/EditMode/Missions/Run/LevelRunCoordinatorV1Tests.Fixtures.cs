using System;
using System.Collections.Generic;
using NUnit.Framework;
using ShooterMover.Application.Holdings;
using ShooterMover.Application.Missions.Results;
using ShooterMover.Application.Missions.Rooms;
using ShooterMover.Application.Missions.Run;
using ShooterMover.Application.Progression.Experience;
using ShooterMover.Application.Progression.Experience.EnemyRewards;
using ShooterMover.Content.Definitions.Missions.Rooms;
using ShooterMover.Contracts.Equipment;
using ShooterMover.Contracts.Flow.Session;
using ShooterMover.Contracts.Holdings;
using ShooterMover.Contracts.Missions.Results;
using ShooterMover.Contracts.Missions.Run;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Enemies;
using ShooterMover.Domain.Equipment;
using ShooterMover.Domain.Holdings;
using ShooterMover.Domain.Progression.Context;
using ShooterMover.Domain.Progression.Curves;
using ShooterMover.Domain.Rewards.Model;

namespace ShooterMover.Tests.EditMode.Missions.Run
{
    public sealed partial class LevelRunCoordinatorV1Tests
    {
        private static EnemyDestroyedNotification CreateDestruction(
            StableId actorId,
            StableId roleId,
            StableId sourceId,
            string suffix)
        {
            EnemyActorState state = EnemyActorState.Create(
                actorId,
                roleId,
                1d,
                2,
                EnemyContactPolicy.Create(
                    EnemyContactMode.None,
                    0d,
                    0.5d,
                    0.02d,
                    4));
            EnemyActorStepResult result = EnemyActorStepper.Step(
                state,
                new[]
                {
                    EnemyActorCommand.Damage(
                        0L,
                        StableId.Create("enemy-death", suffix),
                        sourceId,
                        EnemyContactPolicy.KineticChannelValue,
                        1d),
                });
            for (int index = 0; index < result.Notifications.Count; index++)
            {
                EnemyDestroyedNotification destruction =
                    result.Notifications[index] as EnemyDestroyedNotification;
                if (destruction != null)
                {
                    return destruction;
                }
            }

            throw new InvalidOperationException("Expected destruction.");
        }

        private sealed class Fixture
        {
            private Fixture()
            {
            }

            public string Suffix;
            public StableId ModeId;
            public StableId PlayerActorId;
            public StableId RunId;
            public PlayerRouteProfilePayloadV1 Route;
            public List<EquipmentInstance> EquipmentInstances;
            public EquipmentCatalog Catalog;
            public PlayerHoldingsService Holdings;
            public PlayerExperienceAuthorityV1 Experience;
            public EnemyExperienceRewardServiceV1 Rewards;
            public FakeExistingAuthorityPort ExistingPort;
            public MissionRunResultAuthorityV1 MissionResults;

            public static Fixture Create(string suffix)
            {
                var fixture = new Fixture();
                fixture.Suffix = suffix;
                fixture.ModeId = StableId.Parse("play-mode.solo");
                fixture.PlayerActorId = StableId.Create(
                    "actor",
                    "level-run-player-" + suffix);
                fixture.RunId = StableId.Create("run", "level-run-" + suffix);
                fixture.Catalog = CreateCatalog();
                fixture.Holdings = new PlayerHoldingsService(
                    StableId.Create("authority", "holdings-" + suffix),
                    999L,
                    new CatalogValidator(fixture.Catalog));
                fixture.EquipmentInstances = SeedHoldings(
                    fixture.Holdings,
                    fixture.Catalog,
                    suffix);
                fixture.Route = PlayerRouteProfilePayloadV1.Create(
                    StableId.Create("character", suffix),
                    StableId.Create("loadout", suffix),
                    new[]
                    {
                        fixture.EquipmentInstances[0].InstanceId,
                        fixture.EquipmentInstances[1].InstanceId,
                        fixture.EquipmentInstances[2].InstanceId,
                        fixture.EquipmentInstances[3].InstanceId,
                    });
                fixture.Experience = new PlayerExperienceAuthorityV1(
                    new PlayerExperienceCurveV1(
                        100L,
                        100L,
                        50,
                        new SoftActivationCurveParameters(0.1, 10L, 10L)),
                    ProgressionContext.Create(
                        1,
                        1,
                        StableId.Parse("difficulty.normal"),
                        0,
                        new[] { StableId.Parse("progression-tag.campaign") }));
                fixture.Rewards = new EnemyExperienceRewardServiceV1(
                    fixture.Experience,
                    new EnemyExperienceRewardCatalogV1(
                        new[]
                        {
                            Definition(
                                EnemyExperienceRewardIdsV1.MobileBlasterDroid,
                                40L),
                            Definition(
                                EnemyExperienceRewardIdsV1.BlasterTurret,
                                60L),
                        }));
                fixture.ExistingPort = new FakeExistingAuthorityPort();
                fixture.MissionResults = new MissionRunResultAuthorityV1(
                    fixture.ExistingPort);
                return fixture;
            }

            public LevelRunStartStatusV1 Start(
                StableId levelId,
                out LevelRunCoordinatorV1 coordinator,
                out string rejection)
            {
                return LevelRunCoordinatorV1.TryCreate(
                    Route,
                    ModeId,
                    levelId,
                    RunId,
                    Level1RoomGraphDefinitionV1.TerminalRoomStableId,
                    new RoomMissionLayoutV1(
                        Level1RoomGraphDefinitionV1.Create()),
                    new HoldingsLevelRunLoadoutResolverV1(Holdings, Catalog),
                    Rewards,
                    MissionResults,
                    new MissionRunAuthorityCheckpointV1(
                        Holdings.Sequence,
                        Holdings.ExportSnapshot().Fingerprint,
                        ExistingPort.OpeningSequence,
                        ExistingPort.OpeningFingerprint),
                    out coordinator,
                    out rejection);
            }

            public LevelRunCoordinatorV1 StartOrThrow()
            {
                LevelRunCoordinatorV1 coordinator;
                string rejection;
                Assert.That(
                    Start(
                        LevelRunCoordinatorV1.Level1StableId,
                        out coordinator,
                        out rejection),
                    Is.EqualTo(LevelRunStartStatusV1.Started),
                    rejection);
                return coordinator;
            }

            private static EnemyExperienceRewardDefinitionV1 Definition(
                StableId enemyId,
                long xp)
            {
                return new EnemyExperienceRewardDefinitionV1(
                    enemyId,
                    new[] { new EnemyExperienceRewardBandV1(1, 100, xp) });
            }

            private static EquipmentCatalog CreateCatalog()
            {
                var common = EquipmentQualityTier.Create(
                    StableId.Parse("equipment-quality.common"),
                    "Common",
                    1);
                EquipmentCatalogBuildResult result = EquipmentCatalog.Build(
                    new[]
                    {
                        Weapon(
                            "equipment.stage1-01-blaster",
                            "family.blaster",
                            "Blaster",
                            "weapon.blaster-machine-gun",
                            common),
                        Weapon(
                            "equipment.stage1-02-shotgun",
                            "family.shotgun",
                            "Shotgun",
                            "weapon.shotgun",
                            common),
                        Weapon(
                            "equipment.stage1-03-rocket",
                            "family.rocket",
                            "Rocket Launcher",
                            "weapon.rocket-launcher",
                            common),
                        Weapon(
                            "equipment.stage1-04-arc",
                            "family.arc",
                            "Arc Gun",
                            "weapon.arc-gun",
                            common),
                    },
                    Array.Empty<AugmentDefinition>());
                Assert.That(result.IsValid, Is.True);
                return result.Catalog;
            }

            private static EquipmentDefinition Weapon(
                string definition,
                string family,
                string name,
                string runtime,
                EquipmentQualityTier quality)
            {
                return EquipmentDefinition.Create(
                    StableId.Parse(definition),
                    EquipmentCategoryIds.Weapon,
                    StableId.Parse(family),
                    name,
                    StableId.Parse(runtime),
                    InclusiveIntRange.Create(1, 100),
                    0,
                    new[] { quality },
                    Array.Empty<StableId>());
            }

            private static List<EquipmentInstance> SeedHoldings(
                PlayerHoldingsService holdings,
                EquipmentCatalog catalog,
                string suffix)
            {
                var result = new List<EquipmentInstance>();
                for (int index = 0;
                    index < catalog.EquipmentDefinitions.Count;
                    index++)
                {
                    EquipmentDefinition definition =
                        catalog.EquipmentDefinitions[index];
                    EquipmentInstance instance = EquipmentInstance.Create(
                        StableId.Create(
                            "equipment-instance",
                            suffix + "-slot-" + (index + 1)),
                        definition.DefinitionId,
                        1,
                        StableId.Parse("equipment-quality.common"),
                        Array.Empty<AugmentInstance>());
                    PlayerHoldingsMutationResultV1 mutation = holdings.Apply(
                        PlayerHoldingsCommandV1.AddEquipment(
                            StableId.Create(
                                "transaction",
                                suffix + "-starter-" + index),
                            StableId.Create(
                                "operation",
                                suffix + "-starter-" + index),
                            holdings.AuthorityStableId,
                            instance,
                            HoldingProvenanceV1.Create(
                                StableId.Create(
                                    "grant",
                                    suffix + "-starter-" + index),
                                StableId.Parse("source.character-starter"))));
                    Assert.That(mutation.Status, Is.EqualTo(
                        PlayerHoldingsMutationStatusV1.Applied));
                    result.Add(instance);
                }

                return result;
            }
        }

    }
}
