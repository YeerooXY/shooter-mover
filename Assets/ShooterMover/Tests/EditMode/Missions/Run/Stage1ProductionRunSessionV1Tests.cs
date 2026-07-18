using System;
using NUnit.Framework;
using ShooterMover.Application.Missions.Run;
using ShooterMover.Application.Progression.Experience.EnemyRewards;
using ShooterMover.Content.Definitions.Missions.Rooms;
using ShooterMover.Contracts.Missions.Run;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Enemies;

namespace ShooterMover.Tests.EditMode.Missions.Run
{
    public sealed partial class LevelRunCoordinatorV1Tests
    {
        [Test]
        public void Stage1SessionRegistersRoomsAndRejectsConflictingReplay()
        {
            Fixture fixture = Fixture.Create("stage1-registration");
            var session = new Stage1ProductionRunSessionV1(
                fixture.StartOrThrow());
            StableId enemy = StableId.Parse(
                "enemy-instance.stage1-registration");
            var first = new Stage1RunRoomRegistrationV1(
                Level1RoomGraphDefinitionV1.EntryRoomStableId,
                new[]
                {
                    new Stage1RunEnemyRegistrationV1(
                        enemy,
                        EnemyExperienceRewardIdsV1.MobileBlasterDroid,
                        1),
                });
            var exact = new Stage1RunRoomRegistrationV1(
                Level1RoomGraphDefinitionV1.EntryRoomStableId,
                new[]
                {
                    new Stage1RunEnemyRegistrationV1(
                        enemy,
                        EnemyExperienceRewardIdsV1.MobileBlasterDroid,
                        1),
                });
            var conflict = new Stage1RunRoomRegistrationV1(
                Level1RoomGraphDefinitionV1.EntryRoomStableId,
                new[]
                {
                    new Stage1RunEnemyRegistrationV1(
                        StableId.Parse("enemy-instance.stage1-other"),
                        EnemyExperienceRewardIdsV1.MobileBlasterDroid,
                        1),
                });

            Assert.That(
                session.RegisterRoom(first),
                Is.EqualTo(Stage1RunRegistrationStatusV1.Registered));
            Assert.That(
                session.RegisterRoom(exact),
                Is.EqualTo(
                    Stage1RunRegistrationStatusV1.ExactDuplicateNoChange));
            Assert.That(
                session.RegisterRoom(conflict),
                Is.EqualTo(
                    Stage1RunRegistrationStatusV1.ConflictingDuplicate));
            Assert.That(session.RegisteredRoomCount, Is.EqualTo(1));
            Assert.That(session.RegisteredEnemyCount, Is.EqualTo(1));
        }

        [Test]
        public void Stage1SessionRoutesAcceptedDeathThroughCoordinatorAndXpAuthority()
        {
            Fixture fixture = Fixture.Create("stage1-death");
            var session = new Stage1ProductionRunSessionV1(
                fixture.StartOrThrow());
            StableId enemy = StableId.Parse("enemy-instance.stage1-death");
            Assert.That(
                session.RegisterPlayerSource(
                    fixture.PlayerActorId,
                    fixture.Route.SelectedCharacterStableId),
                Is.True);
            Assert.That(
                session.RegisterRoom(new Stage1RunRoomRegistrationV1(
                    Level1RoomGraphDefinitionV1.EntryRoomStableId,
                    new[]
                    {
                        new Stage1RunEnemyRegistrationV1(
                            enemy,
                            EnemyExperienceRewardIdsV1.MobileBlasterDroid,
                            1),
                    })),
                Is.EqualTo(Stage1RunRegistrationStatusV1.Registered));
            EnemyDestroyedNotification destruction = CreateDestruction(
                enemy,
                EnemyExperienceRewardIdsV1.MobileBlasterDroid,
                fixture.PlayerActorId,
                "stage1-death");

            LevelRunEnemyDestructionResultV1 applied =
                session.RecordEnemyDestroyed(
                    Level1RoomGraphDefinitionV1.EntryRoomStableId,
                    destruction);
            LevelRunEnemyDestructionResultV1 duplicate =
                session.RecordEnemyDestroyed(
                    Level1RoomGraphDefinitionV1.EntryRoomStableId,
                    destruction);

            Assert.That(
                applied.Status,
                Is.EqualTo(LevelRunEnemyDestructionStatusV1.Applied));
            Assert.That(applied.ExperienceEarned, Is.EqualTo(40L));
            Assert.That(applied.RoomBecameClear, Is.True);
            Assert.That(
                duplicate.Status,
                Is.EqualTo(
                    LevelRunEnemyDestructionStatusV1.DuplicateNoChange));
            Assert.That(
                fixture.Experience.CurrentState.CumulativeExperience,
                Is.EqualTo(40L));
            Assert.That(
                session.Coordinator.ExportContributions()[0].KillCount,
                Is.EqualTo(1));
        }

        [Test]
        public void Stage1SessionTraversesAndCapturesResultsExactlyOnce()
        {
            Fixture fixture = Fixture.Create("stage1-complete");
            var session = new Stage1ProductionRunSessionV1(
                fixture.StartOrThrow());
            Assert.That(
                session.RegisterRoom(new Stage1RunRoomRegistrationV1(
                    Level1RoomGraphDefinitionV1.EntryRoomStableId,
                    Array.Empty<Stage1RunEnemyRegistrationV1>())),
                Is.EqualTo(Stage1RunRegistrationStatusV1.Registered));
            Assert.That(
                session.Traverse(
                    Level1RoomGraphDefinitionV1.ForwardExitStableId).Changed,
                Is.True);
            Assert.That(
                session.RegisterRoom(new Stage1RunRoomRegistrationV1(
                    Level1RoomGraphDefinitionV1.TerminalRoomStableId,
                    Array.Empty<Stage1RunEnemyRegistrationV1>())),
                Is.EqualTo(Stage1RunRegistrationStatusV1.Registered));

            Stage1RunCompletionResultV1 first =
                session.CompleteAndCaptureResults();
            Stage1RunCompletionResultV1 retry =
                session.CompleteAndCaptureResults();
            MissionResultsRoutePayloadV1 captured;

            Assert.That(
                first.Status,
                Is.EqualTo(
                    Stage1RunCompletionStatusV1.CompletedAndCaptured));
            Assert.That(
                retry.Status,
                Is.EqualTo(
                    Stage1RunCompletionStatusV1.ExactDuplicateNoChange));
            Assert.That(retry.Extraction, Is.SameAs(first.Extraction));
            Assert.That(session.ResultsCaptured, Is.True);
            Assert.That(
                MissionResultsRouteContextV1.TryRead(out captured),
                Is.True);
            Assert.That(
                captured.RoutePayload,
                Is.SameAs(fixture.Route));
            Assert.That(
                captured.Summary.RunStableId,
                Is.EqualTo(session.Coordinator.RunStableId));
            Assert.That(fixture.ExistingPort.ProjectCalls, Is.EqualTo(1));
        }

        [Test]
        public void Stage1SessionDoesNotFreezeNotReadyExtraction()
        {
            Fixture fixture = Fixture.Create("stage1-not-ready");
            var session = new Stage1ProductionRunSessionV1(
                fixture.StartOrThrow());

            Stage1RunCompletionResultV1 result =
                session.CompleteAndCaptureResults();

            Assert.That(
                result.Status,
                Is.EqualTo(Stage1RunCompletionStatusV1.NotReady));
            Assert.That(session.HasTerminalResult, Is.False);
            Assert.That(session.ResultsCaptured, Is.False);
            Assert.That(MissionResultsRouteContextV1.HasValue, Is.False);
            Assert.That(fixture.ExistingPort.ProjectCalls, Is.Zero);
        }
    }
}
