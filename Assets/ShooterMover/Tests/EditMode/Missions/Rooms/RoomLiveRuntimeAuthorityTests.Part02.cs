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
        public void CollectedDropCondition_IsEvaluatedAsAuthoritativeConfiguredData()
        {
            AuthorableRoomGraphDefinitionV1 source =
                Level1AuthorableRoomDefinitionV1.Create();
            AuthorableRoomDefinitionV1 original = source.GetRoom(
                Level1AuthorableRoomDefinitionV1.EntryRoomStableId);
            StableId conditionId = StableId.Parse("completion.test-exact-drop");
            StableId requiredDropId = StableId.Parse("drop-instance.test-required-drop");
            var door = new RoomDoorDefinitionV1(
                Level1AuthorableRoomDefinitionV1.ForwardDoorStableId,
                Level1AuthorableRoomDefinitionV1.DoorPresentationStableId,
                Level1AuthorableRoomDefinitionV1.ForwardExitStableId,
                new[] { conditionId },
                original.Doors[0].LocalPosition,
                original.Doors[0].LocalRotationDegrees);
            var entry = new AuthorableRoomDefinitionV1(
                original.RoomStableId,
                original.Order,
                original.DisplayName,
                original.Bounds,
                original.SpawnPoints,
                original.Placements,
                new[] { door },
                original.Exits,
                new[]
                {
                    new RoomCompletionConditionDefinitionV1(
                        conditionId,
                        RoomCompletionConditionKindV1.CollectedDrop,
                        requiredDropId,
                        true),
                });
            var authority = new RoomLiveRuntimeAuthorityV1(
                StableId.Parse("room-runtime-instance.test-drop-gate"),
                new AuthorableRoomGraphDefinitionV1(
                    source.LayoutStableId,
                    source.StartRoomStableId,
                    source.TerminalRoomStableId,
                    new[]
                    {
                        entry,
                        source.GetRoom(
                            Level1AuthorableRoomDefinitionV1.TerminalRoomStableId),
                    }));

            RoomLiveRoomProjectionV1 before = authority.GetRoomProjection(
                entry.RoomStableId);
            Assert.That(before.IsCleared, Is.False);
            Assert.That(before.IsCompleted, Is.False);
            Assert.That(before.IsDoorOpen(door.DoorInstanceStableId), Is.False);

            authority.ReportDropCollected(
                Operation("drop-gate-accepted"),
                entry.RoomStableId,
                requiredDropId);
            RoomLiveRoomProjectionV1 after = authority.GetRoomProjection(
                entry.RoomStableId);
            Assert.That(after.IsCleared, Is.False,
                "Configured completion must not overwrite ROOM-RUNTIME-001 clear truth.");
            Assert.That(after.IsCompleted, Is.True);
            Assert.That(after.IsConditionSatisfied(conditionId), Is.True);
            Assert.That(after.IsDoorOpen(door.DoorInstanceStableId), Is.True);
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
        public void Restart_RestoresAuthoredStateAndClearsRetainedFacts()
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
            Assert.That(entry.IsCleared, Is.False);
            Assert.That(entry.IsCompleted, Is.False);
            Assert.That(entry.ActiveOccupants.Count, Is.EqualTo(2));
            Assert.That(entry.DefeatedOccupants, Is.Empty);
            Assert.That(entry.CollectedDropInstanceStableIds, Is.Empty);
            Assert.That(entry.OpenedDoorInstanceStableIds, Is.Empty);
        }
    }
}
