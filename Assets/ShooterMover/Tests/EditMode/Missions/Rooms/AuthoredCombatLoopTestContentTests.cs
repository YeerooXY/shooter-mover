using System.Collections.Generic;
using NUnit.Framework;
using ShooterMover.Application.Missions.Rooms;
using ShooterMover.Application.Missions.Rooms.Content;
using ShooterMover.Content.Definitions.Levels.Selection;
using ShooterMover.Contracts.Missions.Rooms;
using ShooterMover.Domain.Common;
using ShooterMover.UnityAdapters.Authoring.LevelDesign;
using UnityEngine;

namespace ShooterMover.Tests.EditMode.Missions.Rooms
{
    public sealed class AuthoredCombatLoopTestContentTests
    {
        private static readonly StableId LevelId =
            StableId.Parse("level.authored-json-combat-loop-test");
        private static readonly StableId StagingId =
            StableId.Parse("room.combat-loop-staging");
        private static readonly StableId SingleId =
            StableId.Parse("room.combat-loop-single");
        private static readonly StableId DoubleId =
            StableId.Parse("room.combat-loop-double");

        [Test]
        public void CatalogueRegistersSeparateCombatLoopLevel()
        {
            ProductionPlayableLevelDefinitionV1 definition;
            Assert.That(
                ProductionPlayableLevelCatalogV1.TryResolve(LevelId, out definition),
                Is.True);
            Assert.That(definition.DisplayName, Is.EqualTo("COMBAT LOOP TEST"));
            Assert.That(
                definition.GameplayScenePath,
                Is.EqualTo(ProductionPlayableLevelCatalogV1.PlayableLevelScenePath));
            Assert.That(
                definition.RoomContentResourcePath,
                Is.EqualTo("ProductionLevels/CombatLoopTestRoomContent"));
            Assert.That(
                definition.EnemyCatalogResourcePath,
                Is.EqualTo("ProductionLevels/Level1EnemyCatalog"));
        }

        [Test]
        public void DocumentsImportWithExpectedRoomsEnemiesAndContinuousDivider()
        {
            RoomContentBundleV1 bundle = ImportBundle();
            AuthorableRoomGraphDefinitionV1 graph = bundle.RuntimeDefinition;
            AuthorableRoomDefinitionV1 staging = graph.GetRoom(StagingId);
            AuthorableRoomDefinitionV1 single = graph.GetRoom(SingleId);
            AuthorableRoomDefinitionV1 doubleRoom = graph.GetRoom(DoubleId);

            Assert.That(graph.Rooms.Count, Is.EqualTo(3));
            Assert.That(graph.StartRoomStableId, Is.EqualTo(StagingId));
            Assert.That(graph.TerminalRoomStableId, Is.EqualTo(DoubleId));
            Assert.That(CountEnemies(staging), Is.Zero);
            Assert.That(CountEnemies(single), Is.EqualTo(1));
            Assert.That(CountEnemies(doubleRoom), Is.EqualTo(2));
            Assert.That(
                RequiredCompletion(single).Kind,
                Is.EqualTo(RoomCompletionConditionKindV1.AllBlockingOccupantsTerminal));
            Assert.That(
                RequiredCompletion(doubleRoom).Kind,
                Is.EqualTo(RoomCompletionConditionKindV1.AllBlockingOccupantsTerminal));

            int playerSpawns = 0;
            for (int roomIndex = 0; roomIndex < graph.Rooms.Count; roomIndex++)
            {
                for (int spawnIndex = 0;
                    spawnIndex < graph.Rooms[roomIndex].SpawnPoints.Count;
                    spawnIndex++)
                {
                    if (graph.Rooms[roomIndex].SpawnPoints[spawnIndex].Kind
                        == RoomSpawnPointKindV1.Player)
                    {
                        playerSpawns++;
                    }
                }
            }
            Assert.That(playerSpawns, Is.EqualTo(1));

            var dividerY = new List<double>();
            for (int index = 0; index < staging.Placements.Count; index++)
            {
                RoomPlacedEntityDefinitionV1 placement = staging.Placements[index];
                if (placement.PlacementKind != RoomLivePlacementKindV1.Prop)
                {
                    continue;
                }
                Assert.That(placement.LocalPosition.X, Is.EqualTo(0d));
                dividerY.Add(placement.LocalPosition.Y);
            }
            dividerY.Sort();
            Assert.That(dividerY.Count, Is.EqualTo(14));
            Assert.That(dividerY[0], Is.EqualTo(-6.5d));
            Assert.That(dividerY[dividerY.Count - 1], Is.EqualTo(6.5d));
            for (int index = 1; index < dividerY.Count; index++)
            {
                Assert.That(dividerY[index] - dividerY[index - 1], Is.EqualTo(1d));
            }
            Assert.That(FindFinalExit(staging), Is.Not.Null);
        }

        [Test]
        public void FirstDoubleDeathDoesNotCompleteAndSecondCompletesOnce()
        {
            AuthorableRoomGraphDefinitionV1 graph = ImportBundle().RuntimeDefinition;
            AuthorableRoomDefinitionV1 staging = graph.GetRoom(StagingId);
            AuthorableRoomDefinitionV1 single = graph.GetRoom(SingleId);
            AuthorableRoomDefinitionV1 doubleRoom = graph.GetRoom(DoubleId);
            var authority = new RoomLiveRuntimeAuthorityV1(
                StableId.Parse("room-runtime-instance.combat-loop-test"),
                graph);

            RoomExitLinkDefinitionV1 stagingToSingle =
                FindRoomExit(staging, SingleId);
            Assert.That(
                authority.Traverse(Op("enter-single"), stagingToSingle.ExitStableId).Status,
                Is.EqualTo(RoomLiveOperationStatusV1.Applied));

            RoomExitLinkDefinitionV1 singleReturn =
                FindRoomExit(single, StagingId);
            RoomExitLinkDefinitionV1 singleToDouble =
                FindRoomExit(single, DoubleId);
            RoomLiveRoomProjectionV1 singleProjection =
                authority.GetRoomProjection(SingleId);
            Assert.That(
                singleProjection.IsDoorOpen(singleReturn.DoorInstanceStableId),
                Is.True);
            Assert.That(
                singleProjection.IsDoorOpen(singleToDouble.DoorInstanceStableId),
                Is.False);

            Assert.That(
                authority.Traverse(Op("backtrack"), singleReturn.ExitStableId).Status,
                Is.EqualTo(RoomLiveOperationStatusV1.Applied));
            Assert.That(
                authority.Traverse(Op("reenter-single"), stagingToSingle.ExitStableId).Status,
                Is.EqualTo(RoomLiveOperationStatusV1.Applied));

            StableId singleEnemy = authority.GetRoomProjection(SingleId)
                .ActiveOccupants[0].EntityStableId;
            Assert.That(
                authority.ReportOccupantTerminal(
                    Op("kill-single"), SingleId, singleEnemy).Status,
                Is.EqualTo(RoomLiveOperationStatusV1.Applied));
            Assert.That(
                authority.Traverse(Op("enter-double"), singleToDouble.ExitStableId).Status,
                Is.EqualTo(RoomLiveOperationStatusV1.Applied));

            RoomExitLinkDefinitionV1 doubleToStaging =
                FindRoomExit(doubleRoom, StagingId);
            RoomLiveRoomProjectionV1 before = authority.GetRoomProjection(DoubleId);
            Assert.That(before.ActiveOccupants.Count, Is.EqualTo(2));
            Assert.That(
                before.IsDoorOpen(doubleToStaging.DoorInstanceStableId),
                Is.False);

            StableId first = before.ActiveOccupants[0].EntityStableId;
            StableId second = before.ActiveOccupants[1].EntityStableId;
            Assert.That(
                authority.ReportOccupantTerminal(
                    Op("kill-first"), DoubleId, first).Status,
                Is.EqualTo(RoomLiveOperationStatusV1.Applied));

            RoomLiveRoomProjectionV1 afterFirst =
                authority.GetRoomProjection(DoubleId);
            Assert.That(afterFirst.ActiveOccupants.Count, Is.EqualTo(1));
            Assert.That(afterFirst.IsCompleted, Is.False);
            Assert.That(
                afterFirst.IsDoorOpen(doubleToStaging.DoorInstanceStableId),
                Is.False);

            StableId secondOperation = Op("kill-second");
            Assert.That(
                authority.ReportOccupantTerminal(
                    secondOperation, DoubleId, second).Status,
                Is.EqualTo(RoomLiveOperationStatusV1.Applied));
            RoomLiveRoomProjectionV1 afterSecond =
                authority.GetRoomProjection(DoubleId);
            Assert.That(afterSecond.ActiveOccupants.Count, Is.Zero);
            Assert.That(afterSecond.IsCompleted, Is.True);
            Assert.That(
                afterSecond.IsDoorOpen(doubleToStaging.DoorInstanceStableId),
                Is.True);
            long sequence = authority.CurrentProjection.Sequence;

            Assert.That(
                authority.ReportOccupantTerminal(
                    secondOperation, DoubleId, second).Status,
                Is.EqualTo(RoomLiveOperationStatusV1.DuplicateNoChange));
            Assert.That(authority.CurrentProjection.Sequence, Is.EqualTo(sequence));
        }

        [Test]
        public void DoubleReturnTargetsStagingExitSideSpawn()
        {
            AuthorableRoomGraphDefinitionV1 graph = ImportBundle().RuntimeDefinition;
            AuthorableRoomDefinitionV1 staging = graph.GetRoom(StagingId);
            AuthorableRoomDefinitionV1 doubleRoom = graph.GetRoom(DoubleId);
            RoomSpawnPointDefinitionV1 returnSpawn =
                FindSpawn(staging, RoomSpawnPointKindV1.ReturnEntry);
            RoomSpawnPointDefinitionV1 playerStart =
                FindSpawn(staging, RoomSpawnPointKindV1.Player);
            RoomExitLinkDefinitionV1 returnExit =
                FindRoomExit(doubleRoom, StagingId);

            Assert.That(
                returnExit.TargetSpawnPointStableId,
                Is.EqualTo(returnSpawn.SpawnPointStableId));
            Assert.That(returnSpawn.LocalPosition.X, Is.GreaterThan(0d));
            Assert.That(playerStart.LocalPosition.X, Is.LessThan(0d));
        }

        private static RoomContentBundleV1 ImportBundle()
        {
            JsonRoomContentDefinition2D asset =
                Resources.Load<JsonRoomContentDefinition2D>(
                    "ProductionLevels/CombatLoopTestRoomContent");
            Assert.That(asset, Is.Not.Null);
            RoomContentImportResultV1 result = asset.Import();
            Assert.That(result.IsValid, Is.True, FirstIssue(result));
            return result.Bundle;
        }

        private static string FirstIssue(RoomContentImportResultV1 result)
        {
            return result.Issues.Count == 0
                ? string.Empty
                : result.Issues[0].Code + ":" + result.Issues[0].Path
                    + ":" + result.Issues[0].Message;
        }

        private static int CountEnemies(AuthorableRoomDefinitionV1 room)
        {
            int count = 0;
            for (int index = 0; index < room.Placements.Count; index++)
            {
                if (room.Placements[index].PlacementKind
                    == RoomLivePlacementKindV1.Enemy)
                {
                    count++;
                }
            }
            return count;
        }

        private static RoomCompletionConditionDefinitionV1 RequiredCompletion(
            AuthorableRoomDefinitionV1 room)
        {
            for (int index = 0; index < room.CompletionConditions.Count; index++)
            {
                if (room.CompletionConditions[index].IsRequiredForRoomCompletion)
                {
                    return room.CompletionConditions[index];
                }
            }
            Assert.Fail("Required completion missing: " + room.RoomStableId);
            return null;
        }

        private static RoomExitLinkDefinitionV1 FindRoomExit(
            AuthorableRoomDefinitionV1 room,
            StableId targetRoom)
        {
            for (int index = 0; index < room.Exits.Count; index++)
            {
                if (room.Exits[index].LinkKind == RoomLiveLinkKindV1.Room
                    && room.Exits[index].TargetRoomStableId == targetRoom)
                {
                    return room.Exits[index];
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

        private static RoomSpawnPointDefinitionV1 FindSpawn(
            AuthorableRoomDefinitionV1 room,
            RoomSpawnPointKindV1 kind)
        {
            for (int index = 0; index < room.SpawnPoints.Count; index++)
            {
                if (room.SpawnPoints[index].Kind == kind)
                {
                    return room.SpawnPoints[index];
                }
            }
            Assert.Fail("Spawn missing: " + room.RoomStableId + " " + kind);
            return null;
        }

        private static StableId Op(string token)
        {
            return StableId.Parse("operation.combat-loop-test-" + token);
        }
    }
}
