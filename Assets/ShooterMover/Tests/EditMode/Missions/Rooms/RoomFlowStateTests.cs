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
    public sealed partial class RoomFlowStateTests
    {
[Test]
        public void TenIndependentlyAuthoredEnemyInstances_AreTrackedSeparately()
        {
            RoomFlowState authority = new RoomFlowState(
                StableId.Parse("room-runtime-instance.test-ten"),
                CreateTenEnemyDefinition());
            RoomLiveRoomView room = authority.GetRoomProjection(
                Level1AuthorableRoomDefinition.EntryRoomStableId);

            Assert.That(room.ActiveOccupants.Count, Is.EqualTo(10));
            authority.ReportOccupantTerminal(
                Operation("ten-terminal-3"),
                room.RoomStableId,
                StableId.Parse("enemy-instance.test-ten-03"));

            RoomLiveRoomView after = authority.GetRoomProjection(
                room.RoomStableId);
            Assert.That(after.ActiveOccupants.Count, Is.EqualTo(9));
            Assert.That(after.DefeatedOccupants.Count, Is.EqualTo(1));
            Assert.That(after.IsCleared, Is.False);
            Assert.That(after.IsCompleted, Is.False);
        }

[Test]
        public void RequiredEnemyAlive_RoomDoesNotClearCompleteOrOpenItsGate()
        {
            RoomFlowState authority = CreateAuthority("required-alive");
            RoomLiveRoomView room = authority.GetRoomProjection(
                Level1AuthorableRoomDefinition.EntryRoomStableId);

            Assert.That(room.IsActive, Is.True);
            Assert.That(room.IsCurrent, Is.True);
            Assert.That(room.IsVisited, Is.True);
            Assert.That(room.IsCleared, Is.False);
            Assert.That(room.IsCompleted, Is.False);
            Assert.That(
                room.IsConditionSatisfied(
                    Level1AuthorableRoomDefinition.EntryClearConditionStableId),
                Is.False);
            Assert.That(
                room.IsDoorOpen(Level1AuthorableRoomDefinition.ForwardDoorStableId),
                Is.False);
        }

[Test]
        public void ConfiguredCondition_OpensDoorExactlyOnceUnderOperationReplay()
        {
            RoomFlowState authority = CreateAuthority("door-once");
            StableId operation = Operation("door-once-terminal");

            RoomLiveOperationResult first = authority.ReportOccupantTerminal(
                operation,
                Level1AuthorableRoomDefinition.EntryRoomStableId,
                Level1AuthorableRoomDefinition.MovingDroidInstanceStableId);
            long sequence = authority.CurrentProjection.Sequence;
            RoomLiveOperationResult duplicate = authority.ReportOccupantTerminal(
                operation,
                Level1AuthorableRoomDefinition.EntryRoomStableId,
                Level1AuthorableRoomDefinition.MovingDroidInstanceStableId);

            RoomLiveRoomView room = authority.GetRoomProjection(
                Level1AuthorableRoomDefinition.EntryRoomStableId);
            Assert.That(first.Status, Is.EqualTo(RoomLiveOperationStatus.Applied));
            Assert.That(
                duplicate.Status,
                Is.EqualTo(RoomLiveOperationStatus.DuplicateNoChange));
            Assert.That(room.IsCleared, Is.True);
            Assert.That(room.IsCompleted, Is.True);
            Assert.That(
                room.IsConditionSatisfied(
                    Level1AuthorableRoomDefinition.EntryClearConditionStableId),
                Is.True);
            Assert.That(room.OpenedDoorInstanceStableIds.Count, Is.EqualTo(1));
            Assert.That(authority.CurrentProjection.Sequence, Is.EqualTo(sequence));
        }

[Test]
        public void TerminalRoom_UsesIndependentReturnAndFinalDoorConditions()
        {
            RoomFlowState authority = CreateAuthority("independent-gates");
            CompleteEntryAndTraverse(authority, "independent-gates");

            RoomLiveRoomView entered = authority.GetRoomProjection(
                Level1AuthorableRoomDefinition.TerminalRoomStableId);
            Assert.That(entered.IsVisited, Is.True);
            Assert.That(entered.IsCleared, Is.False);
            Assert.That(entered.IsCompleted, Is.False);
            Assert.That(
                entered.IsConditionSatisfied(
                    Level1AuthorableRoomDefinition.TerminalEnteredConditionStableId),
                Is.True);
            Assert.That(
                entered.IsDoorOpen(Level1AuthorableRoomDefinition.ReturnDoorStableId),
                Is.True);
            Assert.That(
                entered.IsDoorOpen(Level1AuthorableRoomDefinition.FinalDoorStableId),
                Is.False);

            authority.ReportOccupantTerminal(
                Operation("independent-gates-turret"),
                Level1AuthorableRoomDefinition.TerminalRoomStableId,
                Level1AuthorableRoomDefinition.TurretInstanceStableId);
            RoomLiveRoomView completed = authority.GetRoomProjection(
                Level1AuthorableRoomDefinition.TerminalRoomStableId);
            Assert.That(completed.IsCleared, Is.True);
            Assert.That(completed.IsCompleted, Is.True);
            Assert.That(
                completed.IsDoorOpen(Level1AuthorableRoomDefinition.ReturnDoorStableId),
                Is.True);
            Assert.That(
                completed.IsDoorOpen(Level1AuthorableRoomDefinition.FinalDoorStableId),
                Is.True);
        }

[Test]
        public void ClearAndMissionCompletion_RemainDistinctForUnvisitedRoom()
        {
            RoomFlowState authority = CreateAuthority("clear-not-complete");
            authority.ReportOccupantTerminal(
                Operation("early-terminal-fact"),
                Level1AuthorableRoomDefinition.TerminalRoomStableId,
                Level1AuthorableRoomDefinition.TurretInstanceStableId);

            RoomLiveRoomView terminal = authority.GetRoomProjection(
                Level1AuthorableRoomDefinition.TerminalRoomStableId);
            Assert.That(terminal.IsActive, Is.False);
            Assert.That(terminal.IsCurrent, Is.False);
            Assert.That(terminal.IsVisited, Is.False);
            Assert.That(terminal.IsCleared, Is.True);
            Assert.That(terminal.IsCompleted, Is.False);
            Assert.That(terminal.OpenedDoorInstanceStableIds, Is.Empty);
        }
    }
}
