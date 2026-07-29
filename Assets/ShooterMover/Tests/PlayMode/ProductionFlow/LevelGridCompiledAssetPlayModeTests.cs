using System.Collections;
using NUnit.Framework;
using ShooterMover.Application.Missions.Rooms;
using ShooterMover.Application.Missions.Rooms.Content;
using ShooterMover.Contracts.Missions.Rooms;
using ShooterMover.Domain.Common;
using ShooterMover.UnityAdapters.Authoring.LevelDesign;
using UnityEngine;
using UnityEngine.TestTools;

namespace ShooterMover.Tests.PlayMode.ProductionFlow
{
    public sealed class LevelGridCompiledAssetPlayModeTests
    {
        private static readonly StableId StarterId =
            StableId.Parse("room.combat-loop-starter");
        private static readonly StableId SingleId =
            StableId.Parse("room.combat-loop-single");
        private static readonly StableId DoubleId =
            StableId.Parse("room.combat-loop-double");

        [UnityTest]
        public IEnumerator TrackedCombatLoopAsset_LoadsAndImportsAfterRuntimeFrame()
        {
            yield return null;

            RoomContentBundle bundle = ImportBundle();
            Assert.That(bundle.Enemies.Count, Is.EqualTo(3));
            Assert.That(bundle.RuntimeDefinition.Rooms.Count, Is.EqualTo(3));
            Assert.That(bundle.RuntimeDefinition.StartRoomStableId, Is.EqualTo(StarterId));
            Assert.That(bundle.RuntimeDefinition.TerminalRoomStableId, Is.EqualTo(DoubleId));
        }

        [UnityTest]
        public IEnumerator TrackedCombatLoopAuthority_TraversesGatesAndRestartsCleanly()
        {
            yield return null;

            RoomContentBundle bundle = ImportBundle();
            AuthorableRoomGraphDefinition graph = bundle.RuntimeDefinition;
            AuthorableRoomDefinition starter = graph.GetRoom(StarterId);
            AuthorableRoomDefinition single = graph.GetRoom(SingleId);
            AuthorableRoomDefinition doubleRoom = graph.GetRoom(DoubleId);
            var authority = new RoomFlowState(
                StableId.Parse("room-runtime-instance.grid-v2-playmode"),
                graph);

            RoomExitLinkDefinition starterToSingle =
                FindRoomExit(starter, SingleId);
            RoomLiveRoomView starterProjection =
                authority.GetRoomProjection(StarterId);
            Assert.That(starterProjection.ActiveOccupants.Count, Is.Zero);
            Assert.That(
                starterProjection.IsDoorOpen(starterToSingle.DoorInstanceStableId),
                Is.True);
            AssertArrival(single, starterToSingle.TargetSpawnPointStableId, -10d, 0d);
            Assert.That(
                authority.Traverse(Op("enter-single"), starterToSingle.ExitStableId).Status,
                Is.EqualTo(RoomLiveOperationStatus.Applied));

            RoomExitLinkDefinition singleReturn =
                FindRoomExit(single, StarterId);
            RoomExitLinkDefinition singleToDouble =
                FindRoomExit(single, DoubleId);
            RoomLiveRoomView singleBefore =
                authority.GetRoomProjection(SingleId);
            Assert.That(singleBefore.ActiveOccupants.Count, Is.EqualTo(1));
            Assert.That(
                singleBefore.IsDoorOpen(singleReturn.DoorInstanceStableId),
                Is.True);
            Assert.That(
                singleBefore.IsDoorOpen(singleToDouble.DoorInstanceStableId),
                Is.False);

            StableId singleEnemy = singleBefore.ActiveOccupants[0].EntityStableId;
            Assert.That(
                authority.ReportOccupantTerminal(
                    Op("kill-single"),
                    SingleId,
                    singleEnemy).Status,
                Is.EqualTo(RoomLiveOperationStatus.Applied));
            Assert.That(
                authority.GetRoomProjection(SingleId)
                    .IsDoorOpen(singleToDouble.DoorInstanceStableId),
                Is.True);
            AssertArrival(doubleRoom, singleToDouble.TargetSpawnPointStableId, -10d, 0d);
            Assert.That(
                authority.Traverse(Op("enter-double"), singleToDouble.ExitStableId).Status,
                Is.EqualTo(RoomLiveOperationStatus.Applied));

            RoomExitLinkDefinition doubleReturn =
                FindRoomExit(doubleRoom, SingleId);
            RoomExitLinkDefinition finalExit = FindFinalExit(doubleRoom);
            RoomLiveRoomView doubleBefore =
                authority.GetRoomProjection(DoubleId);
            Assert.That(doubleBefore.ActiveOccupants.Count, Is.EqualTo(2));
            Assert.That(
                doubleBefore.IsDoorOpen(doubleReturn.DoorInstanceStableId),
                Is.True);
            Assert.That(
                doubleBefore.IsDoorOpen(finalExit.DoorInstanceStableId),
                Is.False);

            StableId first = doubleBefore.ActiveOccupants[0].EntityStableId;
            StableId second = doubleBefore.ActiveOccupants[1].EntityStableId;
            Assert.That(
                authority.ReportOccupantTerminal(
                    Op("kill-double-first"),
                    DoubleId,
                    first).Status,
                Is.EqualTo(RoomLiveOperationStatus.Applied));
            Assert.That(
                authority.GetRoomProjection(DoubleId)
                    .IsDoorOpen(finalExit.DoorInstanceStableId),
                Is.False);
            Assert.That(
                authority.ReportOccupantTerminal(
                    Op("kill-double-second"),
                    DoubleId,
                    second).Status,
                Is.EqualTo(RoomLiveOperationStatus.Applied));
            Assert.That(
                authority.GetRoomProjection(DoubleId)
                    .IsDoorOpen(finalExit.DoorInstanceStableId),
                Is.True);
            Assert.That(
                authority.Traverse(Op("final-exit"), finalExit.ExitStableId).Status,
                Is.EqualTo(RoomLiveOperationStatus.Applied));

            var restarted = new RoomFlowState(
                StableId.Parse("room-runtime-instance.grid-v2-playmode-restart"),
                graph);
            Assert.That(
                restarted.GetRoomProjection(StarterId).ActiveOccupants.Count,
                Is.Zero);
            Assert.That(
                restarted.GetRoomProjection(SingleId).ActiveOccupants.Count,
                Is.EqualTo(1));
            Assert.That(
                restarted.GetRoomProjection(DoubleId).ActiveOccupants.Count,
                Is.EqualTo(2));
            Assert.That(
                restarted.GetRoomProjection(StarterId)
                    .IsDoorOpen(starterToSingle.DoorInstanceStableId),
                Is.True);
        }

        private static RoomContentBundle ImportBundle()
        {
            JsonRoomContentDefinition2D asset =
                Resources.Load<JsonRoomContentDefinition2D>(
                    "ProductionLevels/CombatLoopTestRoomContent");
            Assert.That(asset, Is.Not.Null);

            RoomContentImportResult imported = asset.Import();
            Assert.That(imported.IsValid, Is.True,
                imported.Issues.Count == 0
                    ? string.Empty
                    : imported.Issues[0].Code + " at " + imported.Issues[0].Path
                        + ": " + imported.Issues[0].Message);
            Assert.That(imported.Bundle, Is.Not.Null);
            return imported.Bundle;
        }

        private static RoomExitLinkDefinition FindRoomExit(
            AuthorableRoomDefinition room,
            StableId targetRoom)
        {
            for (int index = 0; index < room.Exits.Count; index++)
            {
                RoomExitLinkDefinition exit = room.Exits[index];
                if (exit.LinkKind == RoomLiveLinkKind.Room
                    && exit.TargetRoomStableId == targetRoom)
                {
                    return exit;
                }
            }
            Assert.Fail("Room exit missing: " + room.RoomStableId + " -> " + targetRoom);
            return null;
        }

        private static RoomExitLinkDefinition FindFinalExit(
            AuthorableRoomDefinition room)
        {
            for (int index = 0; index < room.Exits.Count; index++)
            {
                if (room.Exits[index].LinkKind == RoomLiveLinkKind.FinalExit)
                {
                    return room.Exits[index];
                }
            }
            Assert.Fail("Final exit missing: " + room.RoomStableId);
            return null;
        }

        private static void AssertArrival(
            AuthorableRoomDefinition target,
            StableId spawnId,
            double expectedX,
            double expectedY)
        {
            for (int index = 0; index < target.SpawnPoints.Count; index++)
            {
                RoomSpawnPointDefinition spawn = target.SpawnPoints[index];
                if (spawn.SpawnPointStableId != spawnId) continue;
                Assert.That(spawn.LocalPosition.X, Is.EqualTo(expectedX));
                Assert.That(spawn.LocalPosition.Y, Is.EqualTo(expectedY));
                return;
            }
            Assert.Fail("Target arrival missing: " + target.RoomStableId + " " + spawnId);
        }

        private static StableId Op(string token)
        {
            return StableId.Parse("operation.grid-v2-playmode-" + token);
        }
    }
}
