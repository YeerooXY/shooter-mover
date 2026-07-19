using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using ShooterMover.Application.Missions.Rooms;
using ShooterMover.Content.Definitions.Missions.Rooms;
using ShooterMover.Contracts.Missions.Rooms;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Missions.Rooms;
using ShooterMover.UnityAdapters.Missions.Rooms;

namespace ShooterMover.Tests.EditMode.Missions.Rooms
{
    public sealed partial class RoomLiveRuntimeAuthorityTests
    {
[Test]
        public void TenIndependentlyAuthoredEnemyInstances_AreTrackedSeparately()
        {
            RoomLiveRuntimeAuthorityV1 authority = new RoomLiveRuntimeAuthorityV1(
                StableId.Parse("room-runtime-instance.test-ten"),
                CreateTenEnemyDefinition());
            RoomLiveRoomProjectionV1 room = authority.GetRoomProjection(
                Level1AuthorableRoomDefinitionV1.EntryRoomStableId);

            Assert.That(room.ActiveOccupants.Count, Is.EqualTo(10));
            authority.ReportOccupantTerminal(
                Operation("ten-terminal-3"),
                room.RoomStableId,
                StableId.Parse("enemy-instance.test-ten-03"));

            RoomLiveRoomProjectionV1 after = authority.GetRoomProjection(
                room.RoomStableId);
            Assert.That(after.ActiveOccupants.Count, Is.EqualTo(9));
            Assert.That(after.DefeatedOccupants.Count, Is.EqualTo(1));
            Assert.That(after.IsCleared, Is.False);
            Assert.That(after.IsCompleted, Is.False);
        }

[Test]
        public void RequiredEnemyAlive_RoomDoesNotClearCompleteOrOpenItsGate()
        {
            RoomLiveRuntimeAuthorityV1 authority = CreateAuthority("required-alive");
            RoomLiveRoomProjectionV1 room = authority.GetRoomProjection(
                Level1AuthorableRoomDefinitionV1.EntryRoomStableId);

            Assert.That(room.IsActive, Is.True);
            Assert.That(room.IsCurrent, Is.True);
            Assert.That(room.IsVisited, Is.True);
            Assert.That(room.IsCleared, Is.False);
            Assert.That(room.IsCompleted, Is.False);
            Assert.That(
                room.IsConditionSatisfied(
                    Level1AuthorableRoomDefinitionV1.EntryClearConditionStableId),
                Is.False);
            Assert.That(
                room.IsDoorOpen(Level1AuthorableRoomDefinitionV1.ForwardDoorStableId),
                Is.False);
        }

[Test]
        public void ConfiguredCondition_OpensDoorExactlyOnceUnderOperationReplay()
        {
            RoomLiveRuntimeAuthorityV1 authority = CreateAuthority("door-once");
            StableId operation = Operation("door-once-terminal");

            RoomLiveOperationResultV1 first = authority.ReportOccupantTerminal(
                operation,
                Level1AuthorableRoomDefinitionV1.EntryRoomStableId,
                Level1AuthorableRoomDefinitionV1.MovingDroidInstanceStableId);
            long sequence = authority.CurrentProjection.Sequence;
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
            Assert.That(room.IsCleared, Is.True);
            Assert.That(room.IsCompleted, Is.True);
            Assert.That(
                room.IsConditionSatisfied(
                    Level1AuthorableRoomDefinitionV1.EntryClearConditionStableId),
                Is.True);
            Assert.That(room.OpenedDoorInstanceStableIds.Count, Is.EqualTo(1));
            Assert.That(authority.CurrentProjection.Sequence, Is.EqualTo(sequence));
        }

[Test]
        public void TerminalRoom_UsesIndependentReturnAndFinalDoorConditions()
        {
            RoomLiveRuntimeAuthorityV1 authority = CreateAuthority("independent-gates");
            CompleteEntryAndTraverse(authority, "independent-gates");

            RoomLiveRoomProjectionV1 entered = authority.GetRoomProjection(
                Level1AuthorableRoomDefinitionV1.TerminalRoomStableId);
            Assert.That(entered.IsVisited, Is.True);
            Assert.That(entered.IsCleared, Is.False);
            Assert.That(entered.IsCompleted, Is.False);
            Assert.That(
                entered.IsConditionSatisfied(
                    Level1AuthorableRoomDefinitionV1.TerminalEnteredConditionStableId),
                Is.True);
            Assert.That(
                entered.IsDoorOpen(Level1AuthorableRoomDefinitionV1.ReturnDoorStableId),
                Is.True);
            Assert.That(
                entered.IsDoorOpen(Level1AuthorableRoomDefinitionV1.FinalDoorStableId),
                Is.False);

            authority.ReportOccupantTerminal(
                Operation("independent-gates-turret"),
                Level1AuthorableRoomDefinitionV1.TerminalRoomStableId,
                Level1AuthorableRoomDefinitionV1.TurretInstanceStableId);
            RoomLiveRoomProjectionV1 completed = authority.GetRoomProjection(
                Level1AuthorableRoomDefinitionV1.TerminalRoomStableId);
            Assert.That(completed.IsCleared, Is.True);
            Assert.That(completed.IsCompleted, Is.True);
            Assert.That(
                completed.IsDoorOpen(Level1AuthorableRoomDefinitionV1.ReturnDoorStableId),
                Is.True);
            Assert.That(
                completed.IsDoorOpen(Level1AuthorableRoomDefinitionV1.FinalDoorStableId),
                Is.True);
        }

[Test]
        public void ClearAndMissionCompletion_RemainDistinctForUnvisitedRoom()
        {
            RoomLiveRuntimeAuthorityV1 authority = CreateAuthority("clear-not-complete");
            authority.ReportOccupantTerminal(
                Operation("early-terminal-fact"),
                Level1AuthorableRoomDefinitionV1.TerminalRoomStableId,
                Level1AuthorableRoomDefinitionV1.TurretInstanceStableId);

            RoomLiveRoomProjectionV1 terminal = authority.GetRoomProjection(
                Level1AuthorableRoomDefinitionV1.TerminalRoomStableId);
            Assert.That(terminal.IsActive, Is.False);
            Assert.That(terminal.IsCurrent, Is.False);
            Assert.That(terminal.IsVisited, Is.False);
            Assert.That(terminal.IsCleared, Is.True);
            Assert.That(terminal.IsCompleted, Is.False);
            Assert.That(terminal.OpenedDoorInstanceStableIds, Is.Empty);
        }
    }
}
