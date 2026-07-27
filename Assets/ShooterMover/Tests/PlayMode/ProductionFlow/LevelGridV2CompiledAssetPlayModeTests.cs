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
    public sealed class LevelGridV2CompiledAssetPlayModeTests
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

            RoomContentBundleV1 bundle = ImportBundle();
            Assert.That(bundle.Enemies.Count, Is.EqualTo(3));
            Assert.That(bundle.RuntimeDefinition.Rooms.Count, Is.EqualTo(3));
            Assert.That(bundle.RuntimeDefinition.StartRoomStableId, Is.EqualTo(StarterId));
            Assert.That(bundle.RuntimeDefinition.TerminalRoomStableId, Is.EqualTo(DoubleId));
        }

        [UnityTest]
        public IEnumerator TrackedCombatLoopAuthority_TraversesGatesAndRestartsCleanly()
        {
            yield return null;

            RoomContentBundleV1 bundle = ImportBundle();
            AuthorableRoomGraphDefinitionV1 graph = bundle.RuntimeDefinition;
            AuthorableRoomDefinitionV1 starter = graph.GetRoom(StarterId);
            AuthorableRoomDefinitionV1 single = graph.GetRoom(SingleId);
            AuthorableRoomDefinitionV1 doubleRoom = graph.GetRoom(DoubleId);
            var authority = new RoomLiveRuntimeAuthorityV1(
                StableId.Parse("room-runtime-instance.grid-v2-playmode"),
                graph);

            RoomExitLinkDefinitionV1 starterToSingle =
                FindRoomExit(starter, SingleId);
            RoomLiveRoomProjectionV1 starterProjection =
                authority.GetRoomProjection(StarterId);
            Assert.That(starterProjection.ActiveOccupants.Count, Is.Zero);
            Assert.That(
                starterProjection.IsDoorOpen(starterToSingle.DoorInstanceStableId),
                Is.True);
            AssertArrival(single, starterToSingle.TargetSpawnPointStableId, -10d, 0d);
            Assert.That(
                authority.Traverse(Op("enter-single"), starterToSingle.ExitStableId).Status,
                Is.EqualTo(RoomLiveOperationStatusV1.Applied));

            RoomExitLinkDefinitionV1 singleReturn =
                FindRoomExit(single, StarterId);
            RoomExitLinkDefinitionV1 singleToDouble =
                FindRoomExit(single, DoubleId);
            RoomLiveRoomProjectionV1 singleBefore =
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
                Is.EqualTo(RoomLiveOperationStatusV1.Applied));
            Assert.That(
                authority.GetRoomProjection(SingleId)
                    .IsDoorOpen(singleToDouble.DoorInstanceStableId),
                Is.True);
            AssertArrival(doubleRoom, singleToDouble.TargetSpawnPointStableId, -10d, 0d);
            Assert.That(
                authority.Traverse(Op("enter-double"), singleToDouble.ExitStableId).Status,
                Is.EqualTo(RoomLiveOperationStatusV1.Applied));

            RoomExitLinkDefinitionV1 doubleReturn =
                FindRoomExit(doubleRoom, SingleId);
            RoomExitLinkDefinitionV1 finalExit = FindFinalExit(doubleRoom);
            RoomLiveRoomProjectionV1 doubleBefore =
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
                Is.EqualTo(RoomLiveOperationStatusV1.Applied));
            Assert.That(
                authority.GetRoomProjection(DoubleId)
                    .IsDoorOpen(finalExit.DoorInstanceStableId),
                Is.False);
            Assert.That(
                authority.ReportOccupantTerminal(
                    Op("kill-double-second"),
                    DoubleId,
                    second).Status,
                Is.EqualTo(RoomLiveOperationStatusV1.Applied));
            Assert.That(
                authority.GetRoomProjection(DoubleId)
                    .IsDoorOpen(finalExit.DoorInstanceStableId),
                Is.True);
            Assert.That(
                authority.Traverse(Op("final-exit"), finalExit.ExitStableId).Status,
                Is.EqualTo(RoomLiveOperationStatusV1.Applied));

            var restarted = new RoomLiveRuntimeAuthorityV1(
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

        private static RoomContentBundleV1 ImportBundle()
        {
            JsonRoomContentDefinition2D asset =
                Resources.Load<JsonRoomContentDefinition2D>(
                    "ProductionLevels/CombatLoopTestRoomContent");
            Assert.That(asset, Is.Not.Null);

            RoomContentImportResultV1 imported = asset.Import();
            Assert.That(imported.IsValid, Is.True,
                imported.Issues.Count == 0
                    ? string.Empty
                    : imported.Issues[0].Code + " at " + imported.Issues[0].Path
                        + ": " + imported.Issues[0].Message);
            Assert.That(imported.Bundle, Is.Not.Null);
            return imported.Bundle;
        }

        private static RoomExitLinkDefinitionV1 FindRoomExit(
            AuthorableRoomDefinitionV1 room,
            StableId targetRoom)
        {
            for (int index = 0; index < room.Exits.Count; index++)
            {
                RoomExitLinkDefinitionV1 exit = room.Exits[index];
                if (exit.LinkKind == RoomLiveLinkKindV1.Room
                    && exit.TargetRoomStableId == targetRoom)
                {
                    return exit;
                }
            }
            Assert.Fail("Room exit missing: " + room.RoomStableId + " -> " + targetRoom);
            return null;
        }

        private static RoomExitLinkDefinitionV1 FindFinalExit(
            AuthorableRoomDefinitionV1 room)
        {
            for (int index = 0; index < room.Exits.Count; index++)
            {
                if (room.Exits[index].LinkKind == RoomLiveLinkKindV1.FinalExit)
                {
                    return room.Exits[index];
                }
            }
            Assert.Fail("Final exit missing: " + room.RoomStableId);
            return null;
        }

        private static void AssertArrival(
            AuthorableRoomDefinitionV1 target,
            StableId spawnId,
            double expectedX,
            double expectedY)
        {
            for (int index = 0; index < target.SpawnPoints.Count; index++)
            {
                RoomSpawnPointDefinitionV1 spawn = target.SpawnPoints[index];
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
