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
        [Test]
        public void DuplicateDeathIncrementsKillAndXpOnlyOnce()
        {
            Fixture fixture = Fixture.Create("duplicate");
            LevelRunCoordinatorV1 coordinator = fixture.StartOrThrow();
            StableId enemy = StableId.Parse("enemy-instance.duplicate");
            coordinator.RegisterPlayerSource(
                fixture.PlayerActorId,
                fixture.Route.SelectedCharacterStableId);
            coordinator.RegisterRoomEnemies(
                Level1RoomGraphDefinitionV1.EntryRoomStableId,
                new[] { enemy });
            EnemyDestroyedNotification destruction = CreateDestruction(
                enemy,
                EnemyExperienceRewardIdsV1.MobileBlasterDroid,
                fixture.PlayerActorId,
                "duplicate");

            LevelRunEnemyDestructionResultV1 first = coordinator.RecordEnemyDestroyed(
                Level1RoomGraphDefinitionV1.EntryRoomStableId,
                EnemyExperienceRewardIdsV1.MobileBlasterDroid,
                1,
                destruction);
            LevelRunEnemyDestructionResultV1 duplicate =
                coordinator.RecordEnemyDestroyed(
                    Level1RoomGraphDefinitionV1.EntryRoomStableId,
                    EnemyExperienceRewardIdsV1.MobileBlasterDroid,
                    1,
                    destruction);
            LevelRunPlayerContributionV1 contribution =
                coordinator.ExportContributions()[0];

            Assert.That(first.Status, Is.EqualTo(
                LevelRunEnemyDestructionStatusV1.Applied));
            Assert.That(duplicate.Status, Is.EqualTo(
                LevelRunEnemyDestructionStatusV1.DuplicateNoChange));
            Assert.That(contribution.KillCount, Is.EqualTo(1));
            Assert.That(contribution.ExperienceEarned, Is.EqualTo(40L));
            Assert.That(fixture.Experience.CurrentState.CumulativeExperience,
                Is.EqualTo(40L));
        }

        [Test]
        public void UnattributedDeathIsNotAssignedToLocalPlayer()
        {
            Fixture fixture = Fixture.Create("unattributed");
            LevelRunCoordinatorV1 coordinator = fixture.StartOrThrow();
            StableId enemy = StableId.Parse("enemy-instance.environment");
            coordinator.RegisterPlayerSource(
                fixture.PlayerActorId,
                fixture.Route.SelectedCharacterStableId);
            coordinator.RegisterRoomEnemies(
                Level1RoomGraphDefinitionV1.EntryRoomStableId,
                new[] { enemy });

            LevelRunEnemyDestructionResultV1 result = coordinator.RecordEnemyDestroyed(
                Level1RoomGraphDefinitionV1.EntryRoomStableId,
                EnemyExperienceRewardIdsV1.MobileBlasterDroid,
                1,
                CreateDestruction(
                    enemy,
                    EnemyExperienceRewardIdsV1.MobileBlasterDroid,
                    StableId.Parse("actor.environment"),
                    "environment"));

            Assert.That(result.Status, Is.EqualTo(
                LevelRunEnemyDestructionStatusV1.Unattributed));
            Assert.That(
                coordinator.ExportContributions()[0].KillCount,
                Is.Zero);
            Assert.That(
                fixture.Experience.CurrentState.CumulativeExperience,
                Is.Zero);
        }

        [Test]
        public void ContributionsExportInStablePlayerOrder()
        {
            Fixture fixture = Fixture.Create("ordering");
            LevelRunCoordinatorV1 coordinator = fixture.StartOrThrow();
            coordinator.RegisterPlayerSource(
                StableId.Parse("actor.source-z"),
                StableId.Parse("character.zulu"));
            coordinator.RegisterPlayerSource(
                StableId.Parse("actor.source-a"),
                StableId.Parse("character.alpha"));

            Assert.That(
                coordinator.ExportContributions()[0].PlayerStableId,
                Is.EqualTo(StableId.Parse("character.alpha")));
            Assert.That(
                coordinator.ExportContributions()[1].PlayerStableId,
                Is.EqualTo(StableId.Parse("character.zulu")));
        }

        [Test]
        public void ExtractionBeforeClearIsRejectedAndRepeatedCompletionIsSameResult()
        {
            Fixture fixture = Fixture.Create("extraction");
            LevelRunCoordinatorV1 coordinator = fixture.StartOrThrow();
            coordinator.RegisterRoomEnemies(
                Level1RoomGraphDefinitionV1.EntryRoomStableId,
                Array.Empty<StableId>());
            coordinator.Traverse(Level1RoomGraphDefinitionV1.ForwardExitStableId);
            coordinator.RegisterRoomEnemies(
                Level1RoomGraphDefinitionV1.TerminalRoomStableId,
                Array.Empty<StableId>());

            LevelRunExtractionResultV1 first = coordinator.RequestExtraction();
            LevelRunExtractionResultV1 duplicate = coordinator.RequestExtraction();

            Assert.That(first.Status, Is.EqualTo(
                LevelRunExtractionStatusV1.Completed));
            Assert.That(duplicate, Is.SameAs(first));
            Assert.That(fixture.ExistingPort.ProjectCalls, Is.EqualTo(1));
            Assert.That(first.MissionResult.CompletionState, Is.EqualTo(
                MissionRunCompletionStateV1.Completed));
        }

        [Test]
        public void ResultsHandoffPreservesExactRouteAndReadDoesNotMutateStrongboxes()
        {
            Fixture fixture = Fixture.Create("results");
            LevelRunCoordinatorV1 coordinator = fixture.StartOrThrow();
            coordinator.RegisterRoomEnemies(
                Level1RoomGraphDefinitionV1.EntryRoomStableId,
                Array.Empty<StableId>());
            coordinator.Traverse(Level1RoomGraphDefinitionV1.ForwardExitStableId);
            coordinator.RegisterRoomEnemies(
                Level1RoomGraphDefinitionV1.TerminalRoomStableId,
                Array.Empty<StableId>());
            LevelRunExtractionResultV1 extraction =
                coordinator.RequestExtraction();
            var session = new MissionResultsSessionV1(extraction.MissionResult);

            MissionResultsRouteContextV1.Capture(
                session,
                fixture.Route,
                fixture.ModeId,
                LevelRunCoordinatorV1.Level1StableId,
                extraction.Summary);
            MissionResultsRoutePayloadV1 first;
            MissionResultsRoutePayloadV1 second;
            Assert.That(MissionResultsRouteContextV1.TryRead(out first), Is.True);
            Assert.That(MissionResultsRouteContextV1.TryRead(out second), Is.True);

            Assert.That(first.RoutePayload, Is.SameAs(fixture.Route));
            Assert.That(second.Session.Snapshot.Fingerprint,
                Is.EqualTo(first.Session.Snapshot.Fingerprint));
            Assert.That(first.Session.UnopenedStrongboxCount, Is.Zero);
            Assert.That(fixture.ExistingPort.ProjectCalls, Is.EqualTo(1));
            MissionResultsRouteContextV1.Clear();
        }

    }
}
