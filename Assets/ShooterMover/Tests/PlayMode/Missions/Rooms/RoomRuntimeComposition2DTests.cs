using System.Collections;
using NUnit.Framework;
using ShooterMover.Application.Missions.Rooms;
using ShooterMover.Content.Definitions.Missions.Rooms;
using ShooterMover.Domain.Common;
using ShooterMover.UnityAdapters.Authoring.LevelDesign;
using ShooterMover.UnityAdapters.Missions.Rooms;
using UnityEngine;
using UnityEngine.TestTools;

namespace ShooterMover.Tests.PlayMode.Missions.Rooms
{
    public sealed class RoomRuntimeComposition2DTests
    {
        [UnityTest]
        public IEnumerator TwoRoomLoop_RetainsDefeatedStateAndRestartRestoresAuthoredState()
        {
            GameObject droidPrefab = Template("MovingDroidTemplate");
            GameObject turretPrefab = Template("TurretTemplate");
            GameObject propPrefab = Template("CoverTemplate");
            GameObject doorPrefab = Template("DoorTemplate");
            doorPrefab.AddComponent<BoxCollider2D>();
            RoomPresentationCatalog2D catalog = CreateCatalog(
                droidPrefab,
                turretPrefab,
                propPrefab,
                doorPrefab);
            var host = new GameObject("RoomRuntimeCompositionTestHost");
            RoomRuntimeComposition2D composition =
                host.AddComponent<RoomRuntimeComposition2D>();
            composition.ConfigureForTests(
                Level1AuthorableRoomDefinitionV1.Create(),
                catalog);
            composition.BuildSession(
                StableId.Parse("room-runtime-instance.playmode-two-room"));
            yield return null;

            Assert.That(
                composition.CurrentRoomStableId,
                Is.EqualTo(Level1AuthorableRoomDefinitionV1.EntryRoomStableId));
            Assert.That(composition.SpawnedPlacementCount, Is.EqualTo(2));
            Assert.That(
                composition.TryGetSpawnedPlacement(
                    Level1AuthorableRoomDefinitionV1.MovingDroidInstanceStableId,
                    out RoomPlacedInstance2D droid),
                Is.True);
            Assert.That(
                composition.TryGetSpawnedDoor(
                    Level1AuthorableRoomDefinitionV1.ForwardDoorStableId,
                    out RoomDoorInstance2D forwardDoor),
                Is.True);
            Assert.That(forwardDoor.IsOpen, Is.False);

            droid.ReportTerminal(Operation("playmode-droid-terminal"));
            Assert.That(
                composition.TryGetSpawnedPlacement(
                    Level1AuthorableRoomDefinitionV1.MovingDroidInstanceStableId,
                    out _),
                Is.False);
            Assert.That(forwardDoor.IsOpen, Is.True);

            forwardDoor.TryTraverse(Operation("playmode-forward"));
            yield return null;
            Assert.That(
                composition.CurrentRoomStableId,
                Is.EqualTo(Level1AuthorableRoomDefinitionV1.TerminalRoomStableId));
            Assert.That(composition.SpawnedPlacementCount, Is.EqualTo(1));
            Assert.That(
                composition.TryGetSpawnedPlacement(
                    Level1AuthorableRoomDefinitionV1.TurretInstanceStableId,
                    out RoomPlacedInstance2D turret),
                Is.True);

            turret.ReportTerminal(Operation("playmode-turret-terminal"));
            Assert.That(
                composition.TryGetSpawnedDoor(
                    Level1AuthorableRoomDefinitionV1.ReturnDoorStableId,
                    out RoomDoorInstance2D returnDoor),
                Is.True);
            Assert.That(returnDoor.IsOpen, Is.True);
            Assert.That(
                composition.TryGetSpawnedDoor(
                    Level1AuthorableRoomDefinitionV1.FinalDoorStableId,
                    out RoomDoorInstance2D finalDoor),
                Is.True);
            Assert.That(finalDoor.IsOpen, Is.True);

            returnDoor.TryTraverse(Operation("playmode-return"));
            yield return null;
            Assert.That(
                composition.CurrentRoomStableId,
                Is.EqualTo(Level1AuthorableRoomDefinitionV1.EntryRoomStableId));
            Assert.That(
                composition.TryGetSpawnedPlacement(
                    Level1AuthorableRoomDefinitionV1.MovingDroidInstanceStableId,
                    out _),
                Is.False,
                "Returning to Room 1 must not respawn its defeated moving droid.");
            Assert.That(
                composition.TryGetSpawnedPlacement(
                    Level1AuthorableRoomDefinitionV1.CoverPropInstanceStableId,
                    out _),
                Is.True);

            RoomLiveOperationResultV1 restart = composition.Restart(
                Operation("playmode-restart"));
            yield return null;
            Assert.That(restart.Status, Is.EqualTo(RoomLiveOperationStatusV1.Applied));
            Assert.That(
                composition.CurrentRoomStableId,
                Is.EqualTo(Level1AuthorableRoomDefinitionV1.EntryRoomStableId));
            Assert.That(
                composition.TryGetSpawnedPlacement(
                    Level1AuthorableRoomDefinitionV1.MovingDroidInstanceStableId,
                    out _),
                Is.True,
                "Restart must restore the authored moving droid instance.");
            Assert.That(
                composition.Authority.CurrentProjection.LifecycleGeneration,
                Is.EqualTo(2L));

            Object.Destroy(host);
            Object.Destroy(catalog);
            Object.Destroy(droidPrefab);
            Object.Destroy(turretPrefab);
            Object.Destroy(propPrefab);
            Object.Destroy(doorPrefab);
            yield return null;
        }

        [UnityTest]
        public IEnumerator FinalDoor_EmitsFinalExitOnlyAfterTerminalRoomCompletion()
        {
            GameObject droidPrefab = Template("MovingDroidTemplateFinal");
            GameObject turretPrefab = Template("TurretTemplateFinal");
            GameObject propPrefab = Template("CoverTemplateFinal");
            GameObject doorPrefab = Template("DoorTemplateFinal");
            doorPrefab.AddComponent<BoxCollider2D>();
            RoomPresentationCatalog2D catalog = CreateCatalog(
                droidPrefab,
                turretPrefab,
                propPrefab,
                doorPrefab);
            var host = new GameObject("RoomRuntimeFinalExitTestHost");
            RoomRuntimeComposition2D composition =
                host.AddComponent<RoomRuntimeComposition2D>();
            composition.ConfigureForTests(
                Level1AuthorableRoomDefinitionV1.Create(),
                catalog);
            composition.BuildSession(
                StableId.Parse("room-runtime-instance.playmode-final"));
            bool finalRaised = false;
            composition.FinalExitReached += () => finalRaised = true;
            yield return null;

            composition.ReportOccupantTerminal(
                Operation("final-droid"),
                Level1AuthorableRoomDefinitionV1.EntryRoomStableId,
                Level1AuthorableRoomDefinitionV1.MovingDroidInstanceStableId);
            composition.Traverse(
                Operation("final-forward"),
                Level1AuthorableRoomDefinitionV1.ForwardExitStableId);
            yield return null;

            composition.TryGetSpawnedDoor(
                Level1AuthorableRoomDefinitionV1.FinalDoorStableId,
                out RoomDoorInstance2D finalDoor);
            Assert.That(finalDoor, Is.Not.Null);
            Assert.That(finalDoor.IsOpen, Is.False);
            RoomLiveOperationResultV1 closed = finalDoor.TryTraverse(
                Operation("final-closed-attempt"));
            Assert.That(closed.Status, Is.EqualTo(RoomLiveOperationStatusV1.Rejected));
            Assert.That(finalRaised, Is.False);

            composition.ReportOccupantTerminal(
                Operation("final-turret"),
                Level1AuthorableRoomDefinitionV1.TerminalRoomStableId,
                Level1AuthorableRoomDefinitionV1.TurretInstanceStableId);
            Assert.That(finalDoor.IsOpen, Is.True);
            RoomLiveOperationResultV1 opened = finalDoor.TryTraverse(
                Operation("final-open-attempt"));
            Assert.That(
                opened.Status,
                Is.EqualTo(RoomLiveOperationStatusV1.FinalExitReached));
            Assert.That(finalRaised, Is.True);

            Object.Destroy(host);
            Object.Destroy(catalog);
            Object.Destroy(droidPrefab);
            Object.Destroy(turretPrefab);
            Object.Destroy(propPrefab);
            Object.Destroy(doorPrefab);
            yield return null;
        }

        private static GameObject Template(string name)
        {
            var template = new GameObject(name);
            template.SetActive(false);
            return template;
        }

        private static RoomPresentationCatalog2D CreateCatalog(
            GameObject droidPrefab,
            GameObject turretPrefab,
            GameObject propPrefab,
            GameObject doorPrefab)
        {
            RoomPresentationCatalog2D catalog =
                ScriptableObject.CreateInstance<RoomPresentationCatalog2D>();
            catalog.ConfigureForTests(
                Entry(
                    Level1AuthorableRoomDefinitionV1.MovingDroidPresentationStableId,
                    droidPrefab),
                Entry(
                    Level1AuthorableRoomDefinitionV1.TurretPresentationStableId,
                    turretPrefab),
                Entry(
                    Level1AuthorableRoomDefinitionV1.CoverPresentationStableId,
                    propPrefab),
                Entry(
                    Level1AuthorableRoomDefinitionV1.DoorPresentationStableId,
                    doorPrefab));
            return catalog;
        }

        private static RoomPresentationCatalogEntry2D Entry(
            StableId stableId,
            GameObject prefab)
        {
            var entry = new RoomPresentationCatalogEntry2D();
            entry.ConfigureForTests(stableId.ToString(), prefab);
            return entry;
        }

        private static StableId Operation(string suffix)
        {
            return StableId.Parse("operation.room-live-playmode-" + suffix);
        }
    }
}
