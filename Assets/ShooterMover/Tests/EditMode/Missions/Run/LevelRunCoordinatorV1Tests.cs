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
        [TearDown]
        public void TearDown()
        {
            MissionResultsRouteContextV1.Clear();
        }

        [Test]
        public void ValidRoute_StartsAndInvalidLevelFailsClosed()
        {
            Fixture fixture = Fixture.Create("start");

            LevelRunCoordinatorV1 coordinator;
            string rejection;
            LevelRunStartStatusV1 valid = fixture.Start(
                LevelRunCoordinatorV1.Level1StableId,
                out coordinator,
                out rejection);
            LevelRunStartStatusV1 invalid = fixture.Start(
                StableId.Parse("level.stage-2"),
                out coordinator,
                out rejection);

            Assert.That(valid, Is.EqualTo(LevelRunStartStatusV1.Started));
            Assert.That(invalid, Is.EqualTo(LevelRunStartStatusV1.WrongLevel));
            Assert.That(coordinator, Is.Null);
            Assert.That(rejection, Is.EqualTo(
                "level-run-selected-level-unsupported"));
        }

        [Test]
        public void NullRouteFailsClosedBeforeMissionRunStarts()
        {
            Fixture fixture = Fixture.Create("null-route");
            LevelRunCoordinatorV1 coordinator;
            string rejection;
            LevelRunStartStatusV1 status = LevelRunCoordinatorV1.TryCreate(
                null,
                fixture.ModeId,
                LevelRunCoordinatorV1.Level1StableId,
                fixture.RunId,
                Level1RoomGraphDefinitionV1.TerminalRoomStableId,
                new RoomMissionLayoutV1(
                    Level1RoomGraphDefinitionV1.Create()),
                new HoldingsLevelRunLoadoutResolverV1(
                    fixture.Holdings,
                    fixture.Catalog),
                fixture.Rewards,
                fixture.MissionResults,
                new MissionRunAuthorityCheckpointV1(
                    fixture.Holdings.Sequence,
                    fixture.Holdings.ExportSnapshot().Fingerprint,
                    fixture.ExistingPort.OpeningSequence,
                    fixture.ExistingPort.OpeningFingerprint),
                out coordinator,
                out rejection);

            Assert.That(status, Is.EqualTo(
                LevelRunStartStatusV1.InvalidRoutePayload));
            Assert.That(coordinator, Is.Null);
            Assert.That(rejection, Is.EqualTo(
                "level-run-route-payload-invalid"));
            Assert.That(fixture.MissionResults.Sequence, Is.Zero);
        }

        [Test]
        public void DifferentRunsReceiveDifferentStableIds()
        {
            Fixture first = Fixture.Create("run-one");
            Fixture second = Fixture.Create("run-two");

            LevelRunCoordinatorV1 firstCoordinator = first.StartOrThrow();
            LevelRunCoordinatorV1 secondCoordinator = second.StartOrThrow();

            Assert.That(
                firstCoordinator.RunStableId,
                Is.Not.EqualTo(secondCoordinator.RunStableId));
        }

        [Test]
        public void RestartCreatesNewRunIdentityAndFreshRunScopedState()
        {
            Fixture fixture = Fixture.Create("restart");
            LevelRunCoordinatorV1 original = fixture.StartOrThrow();
            original.RegisterPlayerSource(
                fixture.PlayerActorId,
                fixture.Route.SelectedCharacterStableId);
            original.RegisterRoomEnemies(
                Level1RoomGraphDefinitionV1.EntryRoomStableId,
                Array.Empty<StableId>());

            LevelRunCoordinatorV1 restarted;
            string rejection;
            LevelRunStartStatusV1 status = original.TryRestart(
                new FixedRunIdFactory(
                    StableId.Parse("run.level-run-restart-next")),
                new MissionRunAuthorityCheckpointV1(
                    fixture.Holdings.Sequence,
                    fixture.Holdings.ExportSnapshot().Fingerprint,
                    fixture.ExistingPort.OpeningSequence,
                    fixture.ExistingPort.OpeningFingerprint),
                out restarted,
                out rejection);

            Assert.That(status, Is.EqualTo(LevelRunStartStatusV1.Started), rejection);
            Assert.That(restarted.RunStableId, Is.Not.EqualTo(original.RunStableId));
            Assert.That(restarted.RoomLayout.CurrentSnapshot.Sequence, Is.Zero);
            Assert.That(restarted.RoomLayout.CurrentRoomState.IsCompleted, Is.False);
            Assert.That(restarted.ExportContributions(), Is.Empty);
        }

        [Test]
        public void ExactEquipmentInstancesResolveAndDistinctWeaponsRemainDistinct()
        {
            Fixture fixture = Fixture.Create("loadout");
            LevelRunCoordinatorV1 coordinator = fixture.StartOrThrow();

            Assert.That(coordinator.Loadout.Slots.Count, Is.EqualTo(4));
            Assert.That(
                coordinator.Loadout.Slots[0].EquipmentInstanceStableId,
                Is.EqualTo(fixture.EquipmentInstances[0].InstanceId));
            Assert.That(
                coordinator.Loadout.Slots[0].RuntimeWeaponStableId,
                Is.EqualTo(StableId.Parse("weapon.blaster-machine-gun")));
            Assert.That(
                coordinator.Loadout.Slots[1].RuntimeWeaponStableId,
                Is.EqualTo(StableId.Parse("weapon.shotgun")));
            Assert.That(
                coordinator.Loadout.Slots[0].RuntimeWeaponStableId,
                Is.Not.EqualTo(
                    coordinator.Loadout.Slots[1].RuntimeWeaponStableId));
            Assert.That(coordinator.TrySelectActiveSlot(1), Is.True);
            Assert.That(
                coordinator.ActiveWeapon.RuntimeWeaponStableId,
                Is.EqualTo(StableId.Parse("weapon.shotgun")));
        }

        [Test]
        public void RoomWithMultipleEnemiesClearsOnlyAfterAllAcceptedDeaths()
        {
            Fixture fixture = Fixture.Create("multi-enemy");
            LevelRunCoordinatorV1 coordinator = fixture.StartOrThrow();
            StableId room = Level1RoomGraphDefinitionV1.EntryRoomStableId;
            StableId firstEnemy = StableId.Parse("enemy-instance.multi-first");
            StableId secondEnemy = StableId.Parse("enemy-instance.multi-second");
            coordinator.RegisterPlayerSource(
                fixture.PlayerActorId,
                fixture.Route.SelectedCharacterStableId);
            Assert.That(
                coordinator.RegisterRoomEnemies(
                    room,
                    new[] { firstEnemy, secondEnemy }),
                Is.True);

            LevelRunEnemyDestructionResultV1 first = coordinator.RecordEnemyDestroyed(
                room,
                EnemyExperienceRewardIdsV1.MobileBlasterDroid,
                1,
                CreateDestruction(
                    firstEnemy,
                    EnemyExperienceRewardIdsV1.MobileBlasterDroid,
                    fixture.PlayerActorId,
                    "multi-first"));
            Assert.That(first.RoomBecameClear, Is.False);
            Assert.That(
                coordinator.RoomLayout.CurrentRoomState.IsCompleted,
                Is.False);

            LevelRunEnemyDestructionResultV1 second = coordinator.RecordEnemyDestroyed(
                room,
                EnemyExperienceRewardIdsV1.BlasterTurret,
                1,
                CreateDestruction(
                    secondEnemy,
                    EnemyExperienceRewardIdsV1.BlasterTurret,
                    fixture.PlayerActorId,
                    "multi-second"));
            Assert.That(second.RoomBecameClear, Is.True);
            Assert.That(
                coordinator.RoomLayout.CurrentRoomState.IsCompleted,
                Is.True);
        }

        [Test]
        public void ConflictingRoomRegistrationIsRejectedWithoutReplacingState()
        {
            Fixture fixture = Fixture.Create("registration-conflict");
            LevelRunCoordinatorV1 coordinator = fixture.StartOrThrow();
            StableId room = Level1RoomGraphDefinitionV1.EntryRoomStableId;
            StableId first = StableId.Parse("enemy-instance.registration-first");
            StableId second = StableId.Parse("enemy-instance.registration-second");

            Assert.That(
                coordinator.RegisterRoomEnemies(room, new[] { first }),
                Is.True);
            Assert.That(
                coordinator.RegisterRoomEnemies(room, new[] { second }),
                Is.False);
            Assert.That(
                coordinator.RecordEnemyDestroyed(
                    room,
                    EnemyExperienceRewardIdsV1.MobileBlasterDroid,
                    1,
                    CreateDestruction(
                        second,
                        EnemyExperienceRewardIdsV1.MobileBlasterDroid,
                        fixture.PlayerActorId,
                        "registration-second")).Status,
                Is.EqualTo(LevelRunEnemyDestructionStatusV1.UnregisteredEnemy));
        }

        [Test]
        public void EmptyCurrentRoomIsImmediatelyClearAndRegistrationIsIdempotent()
        {
            Fixture fixture = Fixture.Create("empty");
            LevelRunCoordinatorV1 coordinator = fixture.StartOrThrow();

            Assert.That(
                coordinator.RegisterRoomEnemies(
                    Level1RoomGraphDefinitionV1.EntryRoomStableId,
                    Array.Empty<StableId>()),
                Is.True);
            Assert.That(coordinator.RoomLayout.CurrentRoomState.IsCompleted, Is.True);
            Assert.That(
                coordinator.RegisterRoomEnemies(
                    Level1RoomGraphDefinitionV1.EntryRoomStableId,
                    Array.Empty<StableId>()),
                Is.True);
            Assert.That(coordinator.RoomLayout.CurrentSnapshot.Sequence, Is.EqualTo(1L));
        }

    }
}
