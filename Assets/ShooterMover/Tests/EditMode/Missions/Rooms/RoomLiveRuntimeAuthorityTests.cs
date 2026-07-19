using System;
using System.Collections.Generic;
using NUnit.Framework;
using ShooterMover.Application.Missions.Rooms;
using ShooterMover.Content.Definitions.Missions.Rooms;
using ShooterMover.Contracts.Missions.Rooms;
using ShooterMover.Domain.Common;

namespace ShooterMover.Tests.EditMode.Missions.Rooms
{
    public sealed class RoomLiveRuntimeAuthorityTests
    {
        [Test]
        public void TenIndependentlyAuthoredEnemyInstances_AreTrackedSeparately()
        {
            AuthorableRoomGraphDefinitionV1 definition = CreateTenEnemyDefinition();
            var authority = new RoomLiveRuntimeAuthorityV1(
                StableId.Parse("room-runtime-instance.test-ten"),
                definition);
            RoomLiveRoomProjectionV1 entry = authority.GetRoomProjection(
                Level1AuthorableRoomDefinitionV1.EntryRoomStableId);

            Assert.That(entry.ActiveOccupants.Count, Is.EqualTo(10));
            Assert.That(entry.DefeatedOccupants, Is.Empty);

            authority.ReportOccupantTerminal(
                Operation("ten-terminal-3"),
                entry.RoomStableId,
                StableId.Parse("enemy-instance.test-ten-03"));

            RoomLiveRoomProjectionV1 after = authority.GetRoomProjection(
                entry.RoomStableId);
            Assert.That(after.ActiveOccupants.Count, Is.EqualTo(9));
            Assert.That(after.DefeatedOccupants.Count, Is.EqualTo(1));
            Assert.That(after.IsCompleted, Is.False);
        }

        [Test]
        public void RequiredEnemyAlive_RoomDoesNotClearAndDoorStaysClosed()
        {
            RoomLiveRuntimeAuthorityV1 authority = CreateAuthority("required-alive");
            RoomLiveRoomProjectionV1 room = authority.GetRoomProjection(
                Level1AuthorableRoomDefinitionV1.EntryRoomStableId);

            Assert.That(room.IsCompleted, Is.False);
            Assert.That(
                room.IsDoorOpen(Level1AuthorableRoomDefinitionV1.ForwardDoorStableId),
                Is.False);
            Assert.That(
                authority.MissionLayout.GetExitState(
                    Level1AuthorableRoomDefinitionV1.ForwardExitStableId).IsAvailable,
                Is.False);
        }

        [Test]
        public void CompletingRoom_OpensConfiguredDoorExactlyOnce()
        {
            RoomLiveRuntimeAuthorityV1 authority = CreateAuthority("door-once");
            StableId operation = Operation("door-once-terminal");

            RoomLiveOperationResultV1 first = authority.ReportOccupantTerminal(
                operation,
                Level1AuthorableRoomDefinitionV1.EntryRoomStableId,
                Level1AuthorableRoomDefinitionV1.MovingDroidInstanceStableId);
            long sequenceAfterFirst = authority.CurrentProjection.Sequence;
            RoomLiveOperationResultV1 duplicate = authority.ReportOccupantTerminal(
                operation,
                Level1AuthorableRoomDefinitionV1.EntryRoomStableId,
                Level1AuthorableRoomDefinitionV1.MovingDroidInstanceStableId);

            RoomLiveRoomProjectionV1 room = authority.GetRoomProjection(
                Level1AuthorableRoomDefinitionV1.EntryRoomStableId);
            Assert.That(first.Status, Is.EqualTo(RoomLiveOperationStatusV1.Applied));
            Assert.That(
                duplicate.Status,
                Is.EqualTo(RoomLiveOperationStatusV1.DuplicateNoChange));
            Assert.That(room.IsCompleted, Is.True);
            Assert.That(room.OpenedDoorInstanceStableIds.Count, Is.EqualTo(1));
            Assert.That(authority.CurrentProjection.Sequence, Is.EqualTo(sequenceAfterFirst));
            Assert.That(
                room.IsDoorOpen(Level1AuthorableRoomDefinitionV1.ForwardDoorStableId),
                Is.True);
        }

        [Test]
        public void ReturnTraversal_RetainsDefeatedOccupantsAndCollectedDrops()
        {
            RoomLiveRuntimeAuthorityV1 authority = CreateAuthority("return-retained");
            StableId drop = StableId.Parse("drop-instance.level1-droid-test");
            authority.ReportOccupantTerminal(
                Operation("return-defeat-droid"),
                Level1AuthorableRoomDefinitionV1.EntryRoomStableId,
                Level1AuthorableRoomDefinitionV1.MovingDroidInstanceStableId);
            authority.ReportDropCollected(
                Operation("return-collect-drop"),
                Level1AuthorableRoomDefinitionV1.EntryRoomStableId,
                drop);
            authority.Traverse(
                Operation("return-forward"),
                Level1AuthorableRoomDefinitionV1.ForwardExitStableId);
            authority.ReportOccupantTerminal(
                Operation("return-defeat-turret"),
                Level1AuthorableRoomDefinitionV1.TerminalRoomStableId,
                Level1AuthorableRoomDefinitionV1.TurretInstanceStableId);
            authority.Traverse(
                Operation("return-back"),
                Level1AuthorableRoomDefinitionV1.ReturnExitStableId);

            RoomLiveRoomProjectionV1 returned = authority.GetRoomProjection(
                Level1AuthorableRoomDefinitionV1.EntryRoomStableId);
            Assert.That(returned.IsActive, Is.True);
            Assert.That(returned.IsCompleted, Is.True);
            Assert.That(returned.DefeatedOccupants.Count, Is.EqualTo(1));
            Assert.That(returned.IsDropCollected(drop), Is.True);
            Assert.That(
                returned.IsDoorOpen(Level1AuthorableRoomDefinitionV1.ForwardDoorStableId),
                Is.True);
        }

        [Test]
        public void Restart_RestoresAuthoredRoomStateAndClearsRetainedRunFacts()
        {
            RoomLiveRuntimeAuthorityV1 authority = CreateAuthority("restart");
            StableId drop = StableId.Parse("drop-instance.restart-test");
            authority.ReportOccupantTerminal(
                Operation("restart-defeat"),
                Level1AuthorableRoomDefinitionV1.EntryRoomStableId,
                Level1AuthorableRoomDefinitionV1.MovingDroidInstanceStableId);
            authority.ReportDropCollected(
                Operation("restart-drop"),
                Level1AuthorableRoomDefinitionV1.EntryRoomStableId,
                drop);

            RoomLiveOperationResultV1 result = authority.Restart(
                Operation("restart-runtime"));

            RoomLiveRoomProjectionV1 entry = authority.GetRoomProjection(
                Level1AuthorableRoomDefinitionV1.EntryRoomStableId);
            Assert.That(result.Status, Is.EqualTo(RoomLiveOperationStatusV1.Applied));
            Assert.That(authority.CurrentProjection.LifecycleGeneration, Is.EqualTo(2L));
            Assert.That(entry.IsActive, Is.True);
            Assert.That(entry.IsCompleted, Is.False);
            Assert.That(entry.ActiveOccupants.Count, Is.EqualTo(2));
            Assert.That(entry.DefeatedOccupants, Is.Empty);
            Assert.That(entry.CollectedDropInstanceStableIds, Is.Empty);
            Assert.That(entry.OpenedDoorInstanceStableIds, Is.Empty);
        }

        [Test]
        public void UnknownRoomLink_FailsClosedWithoutMutation()
        {
            RoomLiveRuntimeAuthorityV1 authority = CreateAuthority("unknown-link");
            string before = authority.CurrentProjection.Fingerprint;

            RoomLiveOperationResultV1 result = authority.Traverse(
                Operation("unknown-link-traverse"),
                StableId.Parse("exit.unknown-room-link"));

            Assert.That(result.Status, Is.EqualTo(RoomLiveOperationStatusV1.Rejected));
            Assert.That(result.RejectionCode, Is.EqualTo("room-live-exit-unknown"));
            Assert.That(authority.CurrentProjection.Fingerprint, Is.EqualTo(before));
            Assert.That(
                authority.CurrentProjection.CurrentRoomStableId,
                Is.EqualTo(Level1AuthorableRoomDefinitionV1.EntryRoomStableId));
        }


        [Test]
        public void UnknownTargetRoomLink_DefinitionConstructionFailsClosed()
        {
            AuthorableRoomGraphDefinitionV1 source =
                Level1AuthorableRoomDefinitionV1.Create();
            AuthorableRoomDefinitionV1 entrySource = source.GetRoom(
                Level1AuthorableRoomDefinitionV1.EntryRoomStableId);
            var invalidExit = new RoomExitLinkDefinitionV1(
                Level1AuthorableRoomDefinitionV1.ForwardExitStableId,
                Level1AuthorableRoomDefinitionV1.ForwardDoorStableId,
                RoomLiveLinkKindV1.Room,
                StableId.Parse("room.unknown-target"),
                Level1AuthorableRoomDefinitionV1.TerminalSpawnStableId);
            var invalidEntry = new AuthorableRoomDefinitionV1(
                entrySource.RoomStableId,
                entrySource.Order,
                entrySource.DisplayName,
                entrySource.Bounds,
                entrySource.SpawnPoints,
                entrySource.Placements,
                entrySource.Doors,
                new[] { invalidExit },
                entrySource.CompletionConditions);

            ArgumentException error = Assert.Throws<ArgumentException>(() =>
                new AuthorableRoomGraphDefinitionV1(
                    source.LayoutStableId,
                    source.StartRoomStableId,
                    source.TerminalRoomStableId,
                    new[]
                    {
                        invalidEntry,
                        source.GetRoom(
                            Level1AuthorableRoomDefinitionV1.TerminalRoomStableId),
                    }));

            Assert.That(
                error.Message,
                Does.Contain("room-live-link-target-room-unknown"));
        }

        [Test]
        public void TerminalRoomCompletion_EnablesReturnAndFinalExit()
        {
            RoomLiveRuntimeAuthorityV1 authority = CreateAuthority("final-exit");
            authority.ReportOccupantTerminal(
                Operation("final-entry"),
                Level1AuthorableRoomDefinitionV1.EntryRoomStableId,
                Level1AuthorableRoomDefinitionV1.MovingDroidInstanceStableId);
            authority.Traverse(
                Operation("final-forward"),
                Level1AuthorableRoomDefinitionV1.ForwardExitStableId);

            RoomLiveRoomProjectionV1 before = authority.GetRoomProjection(
                Level1AuthorableRoomDefinitionV1.TerminalRoomStableId);
            Assert.That(
                before.IsDoorOpen(Level1AuthorableRoomDefinitionV1.FinalDoorStableId),
                Is.False);

            authority.ReportOccupantTerminal(
                Operation("final-turret"),
                Level1AuthorableRoomDefinitionV1.TerminalRoomStableId,
                Level1AuthorableRoomDefinitionV1.TurretInstanceStableId);
            RoomLiveRoomProjectionV1 completed = authority.GetRoomProjection(
                Level1AuthorableRoomDefinitionV1.TerminalRoomStableId);
            Assert.That(
                completed.IsDoorOpen(Level1AuthorableRoomDefinitionV1.ReturnDoorStableId),
                Is.True);
            Assert.That(
                completed.IsDoorOpen(Level1AuthorableRoomDefinitionV1.FinalDoorStableId),
                Is.True);

            RoomLiveOperationResultV1 final = authority.Traverse(
                Operation("final-use-exit"),
                Level1AuthorableRoomDefinitionV1.FinalExitStableId);
            Assert.That(
                final.Status,
                Is.EqualTo(RoomLiveOperationStatusV1.FinalExitReached));
            Assert.That(authority.CurrentProjection.FinalExitReached, Is.True);
        }

        [Test]
        public void RoomGraph_IsDeterministicAndCanonicallySerializable()
        {
            AuthorableRoomGraphDefinitionV1 first =
                Level1AuthorableRoomDefinitionV1.Create();
            AuthorableRoomGraphDefinitionV1 second =
                new AuthorableRoomGraphDefinitionV1(
                    first.LayoutStableId,
                    first.StartRoomStableId,
                    first.TerminalRoomStableId,
                    new[] { first.Rooms[1], first.Rooms[0] });

            Assert.That(second.ToCanonicalJson(), Is.EqualTo(first.ToCanonicalJson()));
            Assert.That(second.Fingerprint, Is.EqualTo(first.Fingerprint));
            Assert.That(
                second.RoomGraphDefinition.Fingerprint,
                Is.EqualTo(first.RoomGraphDefinition.Fingerprint));
            Assert.That(first.ToCanonicalJson(), Does.Contain("\"enemy-instance.level1-room1-moving-droid\""));
            Assert.That(first.ToCanonicalJson(), Does.Contain("\"exit.level1-terminal-final\""));
        }

        private static RoomLiveRuntimeAuthorityV1 CreateAuthority(string suffix)
        {
            return new RoomLiveRuntimeAuthorityV1(
                StableId.Parse("room-runtime-instance.test-" + suffix),
                Level1AuthorableRoomDefinitionV1.Create());
        }

        private static StableId Operation(string suffix)
        {
            return StableId.Parse("operation.room-live-test-" + suffix);
        }

        private static AuthorableRoomGraphDefinitionV1 CreateTenEnemyDefinition()
        {
            AuthorableRoomGraphDefinitionV1 source =
                Level1AuthorableRoomDefinitionV1.Create();
            AuthorableRoomDefinitionV1 sourceEntry = source.GetRoom(
                Level1AuthorableRoomDefinitionV1.EntryRoomStableId);
            var enemies = new List<RoomPlacedEntityDefinitionV1>();
            for (int index = 0; index < 10; index++)
            {
                enemies.Add(new RoomPlacedEntityDefinitionV1(
                    StableId.Parse("enemy-instance.test-ten-" + index.ToString("D2")),
                    RoomLivePlacementKindV1.Enemy,
                    StableId.Parse("enemy.test-shared-definition"),
                    Level1AuthorableRoomDefinitionV1.MovingDroidPresentationStableId,
                    RoomOccupantClearRoleV1.RequiredEnemy,
                    new RoomVector2V1(index, 0d),
                    0d));
            }

            var entry = new AuthorableRoomDefinitionV1(
                sourceEntry.RoomStableId,
                sourceEntry.Order,
                sourceEntry.DisplayName,
                sourceEntry.Bounds,
                sourceEntry.SpawnPoints,
                enemies,
                sourceEntry.Doors,
                sourceEntry.Exits,
                sourceEntry.CompletionConditions);
            return new AuthorableRoomGraphDefinitionV1(
                source.LayoutStableId,
                source.StartRoomStableId,
                source.TerminalRoomStableId,
                new[]
                {
                    entry,
                    source.GetRoom(Level1AuthorableRoomDefinitionV1.TerminalRoomStableId),
                });
        }
    }
}
