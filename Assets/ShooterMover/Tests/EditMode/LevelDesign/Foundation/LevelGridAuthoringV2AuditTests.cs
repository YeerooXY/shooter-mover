#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShooterMover.Editor.LevelDesign.Foundation;
using ShooterMover.UnityAdapters.Authoring.LevelDesign;
using UnityEditor;
using UnityEngine;

namespace ShooterMover.Tests.EditMode.LevelDesign.Foundation
{
    public sealed class LevelGridAuthoringV2AuditTests
    {
        private string temporaryRoot;

        [SetUp]
        public void SetUp()
        {
            temporaryRoot = Path.Combine(
                Path.GetTempPath(),
                "ShooterMover-LevelGridV2-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(temporaryRoot);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(temporaryRoot))
            {
                Directory.Delete(temporaryRoot, true);
            }
        }

        [Test]
        public void ProductionExporterSource_RequiresFoundationAndGraphValidation()
        {
            string source = File.ReadAllText(
                "Assets/ShooterMover/Editor/LevelDesign/Foundation/"
                + "LevelGridAuthoringV2JsonExporter.cs");

            StringAssert.Contains("root.ValidateHierarchy()", source);
            StringAssert.Contains("!foundationValidation.IsValid", source);
            StringAssert.Contains("!gridValidation.CanPublish", source);
        }

        [Test]
        public void MovingRoom_MigratesFolderAndPreservesSidecars()
        {
            GameObject rootObject = new GameObject("Root");
            try
            {
                LevelDesignSceneAuthoringRoot2D root =
                    rootObject.AddComponent<LevelDesignSceneAuthoringRoot2D>();
                root.ConfigureForTests("level.folder-migration");
                LevelRoomAuthoring2D room = CreateRoom(
                    root.transform,
                    "room.migrating",
                    Vector2Int.zero,
                    1,
                    true);

                string output = Path.Combine(temporaryRoot, "Level");
                ExportTransaction(root, output, LevelGridValidationPurposeV2.Draft);
                string originalFolder = Path.Combine(output, "Rooms", "Room_0_0_01");
                string markerPath = Path.Combine(originalFolder, "props.json");
                const string marker = "{\"room\":\"room.migrating\",\"props\":[\"keep-me\"]}";
                File.WriteAllText(markerPath, marker);

                room.ConfigureForTests(
                    "room.migrating",
                    new Vector2Int(4, 1),
                    Vector2.one,
                    Vector2Int.one,
                    room.RoomBounds);
                room.ConfigureFolderSlotForTests(1);
                ExportTransaction(root, output, LevelGridValidationPurposeV2.Draft);

                string migratedFolder = Path.Combine(output, "Rooms", "Room_4_1_01");
                Assert.That(Directory.Exists(originalFolder), Is.False);
                Assert.That(Directory.Exists(migratedFolder), Is.True);
                Assert.That(
                    File.ReadAllText(Path.Combine(migratedFolder, "props.json")),
                    Is.EqualTo(marker));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        public void DeletedRoomFolder_IsNeverAdoptedByAnotherRoom()
        {
            GameObject rootObject = new GameObject("Root");
            try
            {
                LevelDesignSceneAuthoringRoot2D root =
                    rootObject.AddComponent<LevelDesignSceneAuthoringRoot2D>();
                root.ConfigureForTests("level.folder-ownership");
                LevelRoomAuthoring2D roomA = CreateRoom(
                    root.transform,
                    "room.owner-a",
                    Vector2Int.zero,
                    1,
                    true);

                string output = Path.Combine(temporaryRoot, "Level");
                ExportTransaction(root, output, LevelGridValidationPurposeV2.Draft);
                UnityEngine.Object.DestroyImmediate(roomA.gameObject);
                CreateRoom(
                    root.transform,
                    "room.owner-b",
                    Vector2Int.zero,
                    1,
                    true);

                TargetInvocationException exception = Assert.Throws<TargetInvocationException>(
                    () => ExportTransaction(
                        root,
                        output,
                        LevelGridValidationPurposeV2.Draft));
                Assert.That(exception.InnerException, Is.TypeOf<InvalidOperationException>());
                StringAssert.Contains(
                    "already belongs to room 'room.owner-a'",
                    exception.InnerException.Message);

                string persisted = File.ReadAllText(
                    Path.Combine(output, "Rooms", "Room_0_0_01", "room.json"));
                StringAssert.Contains("room.owner-a", persisted);
                StringAssert.DoesNotContain("room.owner-b", persisted);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        public void MalformedExistingRoomIdentity_BlocksWithoutReplacingDestination()
        {
            GameObject rootObject = new GameObject("Root");
            try
            {
                LevelDesignSceneAuthoringRoot2D root =
                    rootObject.AddComponent<LevelDesignSceneAuthoringRoot2D>();
                root.ConfigureForTests("level.malformed-folder");
                CreateRoom(
                    root.transform,
                    "room.malformed",
                    Vector2Int.zero,
                    1,
                    true);

                string output = Path.Combine(temporaryRoot, "Level");
                ExportTransaction(root, output, LevelGridValidationPurposeV2.Draft);
                string roomJson = Path.Combine(
                    output,
                    "Rooms",
                    "Room_0_0_01",
                    "room.json");
                File.WriteAllText(roomJson, "{ not valid json");

                Assert.Throws<TargetInvocationException>(
                    () => ExportTransaction(
                        root,
                        output,
                        LevelGridValidationPurposeV2.Draft));
                Assert.That(File.ReadAllText(roomJson), Is.EqualTo("{ not valid json"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        public void DeleteDoor_RemovesLinkPreservesOppositeAndUndoesAtomically()
        {
            GameObject rootObject = new GameObject("Root");
            try
            {
                LevelDesignSceneAuthoringRoot2D root =
                    rootObject.AddComponent<LevelDesignSceneAuthoringRoot2D>();
                root.ConfigureForTests("level.door-delete");
                LevelRoomAuthoring2D left = CreateRoom(
                    root.transform,
                    "room.left",
                    Vector2Int.zero,
                    1,
                    true);
                LevelRoomAuthoring2D right = CreateRoom(
                    root.transform,
                    "room.right",
                    Vector2Int.right,
                    1,
                    true);
                LevelDoorEndpointAuthoring2D leftDoor = CreateDoor(
                    left,
                    "door.left-east",
                    LevelDoorSideV2.East);
                LevelDoorEndpointAuthoring2D rightDoor = CreateDoor(
                    right,
                    "door.right-west",
                    LevelDoorSideV2.West);
                CreateConnection(root.transform, left, leftDoor, right, rightDoor);

                MethodInfo delete = typeof(LevelGridDoorOperationsV2).GetMethod(
                    "DeleteDoorUndoable",
                    BindingFlags.NonPublic | BindingFlags.Static);
                Assert.That(delete, Is.Not.Null);
                delete.Invoke(null, new object[] { leftDoor });

                Assert.That(
                    root.GetComponentsInChildren<LevelDoorLinkAuthoring2D>(true),
                    Is.Empty);
                Assert.That(
                    root.GetComponentsInChildren<LevelDoorEndpointAuthoring2D>(true),
                    Has.Member(rightDoor));
                Assert.That(root.LastGridValidation.UnconnectedTraversableDoorCount, Is.EqualTo(1));

                Undo.PerformUndo();
                Assert.That(
                    root.GetComponentsInChildren<LevelDoorLinkAuthoring2D>(true).Length,
                    Is.EqualTo(1));
                Assert.That(
                    root.GetComponentsInChildren<LevelDoorEndpointAuthoring2D>(true).Length,
                    Is.EqualTo(2));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        public void FixedDoor_CapturesDraggedLocalPosition()
        {
            GameObject roomObject = new GameObject("Room");
            try
            {
                LevelRoomAuthoring2D room =
                    roomObject.AddComponent<LevelRoomAuthoring2D>();
                room.ConfigureForTests(
                    "room.fixed",
                    Vector2Int.zero,
                    Vector2.one,
                    Vector2Int.one,
                    null);
                LevelDoorEndpointAuthoring2D door = CreateDoor(
                    room,
                    "door.fixed",
                    LevelDoorSideV2.North,
                    LevelDoorPlacementModeV2.Fixed);

                door.transform.localPosition = new Vector3(3.25f, -1.5f, 0f);
                door.CaptureCurrentFixedPosition();

                Assert.That(door.FixedLocalPosition, Is.EqualTo(new Vector2(3.25f, -1.5f)));
                Assert.That(door.ResolveTargetLocalPosition().x, Is.EqualTo(3.25f));
                Assert.That(door.ResolveTargetLocalPosition().y, Is.EqualTo(-1.5f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(roomObject);
            }
        }

        [Test]
        public void MovingConnectedRoom_ReflowsAutoFacingEdgeDoor()
        {
            GameObject rootObject = new GameObject("Root");
            try
            {
                LevelDesignSceneAuthoringRoot2D root =
                    rootObject.AddComponent<LevelDesignSceneAuthoringRoot2D>();
                root.ConfigureForTests("level.reflow");
                LevelRoomAuthoring2D source = CreateRoom(
                    root.transform,
                    "room.source",
                    Vector2Int.zero,
                    1,
                    true);
                LevelRoomAuthoring2D destination = CreateRoom(
                    root.transform,
                    "room.destination",
                    new Vector2Int(0, 3),
                    1,
                    true);
                LevelDoorEndpointAuthoring2D sourceDoor = CreateDoor(
                    source,
                    "door.source",
                    LevelDoorSideV2.East);
                LevelDoorEndpointAuthoring2D destinationDoor = CreateDoor(
                    destination,
                    "door.destination",
                    LevelDoorSideV2.West);
                CreateConnection(
                    root.transform,
                    source,
                    sourceDoor,
                    destination,
                    destinationDoor);

                Assert.That(LevelGridDoorOperationsV2.ReflowDoor(root, sourceDoor), Is.True);
                Assert.That(sourceDoor.Side, Is.EqualTo(LevelDoorSideV2.North));
                Assert.That(LevelGridDoorOperationsV2.ReflowDoor(root, destinationDoor), Is.True);
                Assert.That(destinationDoor.Side, Is.EqualTo(LevelDoorSideV2.South));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        public void DuplicateCoordinateSlot_IsRejectedIndependentlyOfGlobalOrdering()
        {
            LevelRoomRecord roomA = RoomRecord("room.a", new Vector2Int(50, 51));
            LevelRoomRecord roomB = RoomRecord("room.b", new Vector2Int(50, 51));
            LevelGridRoomRecordV2 gridA = new LevelGridRoomRecordV2(
                roomA.RoomId,
                roomA.GridCoordinate,
                Vector2Int.one,
                1,
                "room-a");
            LevelGridRoomRecordV2 gridB = new LevelGridRoomRecordV2(
                roomB.RoomId,
                roomB.GridCoordinate,
                Vector2Int.one,
                1,
                "room-b");

            LevelGridValidationResultV2 result =
                LevelGridAuthoringV2CompositeValidator.Validate(
                    new[] { roomA, roomB },
                    new[] { gridA, gridB },
                    Array.Empty<LevelGridDoorRecordV2>(),
                    Array.Empty<LevelGridConnectionRecordV2>(),
                    LevelGridValidationPurposeV2.Draft);

            Assert.That(
                result.Problems.Count(
                    issue => issue.Code == LevelGridProblemCodeV2.DuplicateRoomFolderSlot),
                Is.EqualTo(2));
        }

        private static void ExportTransaction(
            LevelDesignSceneAuthoringRoot2D root,
            string output,
            LevelGridValidationPurposeV2 purpose)
        {
            MethodInfo export = typeof(LevelGridAuthoringV2JsonExporter).GetMethod(
                "ExportTransaction",
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(export, Is.Not.Null);
            export.Invoke(null, new object[] { root, output, purpose });
        }

        private static LevelRoomAuthoring2D CreateRoom(
            Transform parent,
            string id,
            Vector2Int coordinate,
            int slot,
            bool withBounds)
        {
            GameObject roomObject = new GameObject(id);
            roomObject.transform.SetParent(parent, false);
            BoxCollider2D bounds = withBounds
                ? roomObject.AddComponent<BoxCollider2D>()
                : null;
            if (bounds != null)
            {
                bounds.size = Vector2.one;
            }

            LevelRoomAuthoring2D room =
                roomObject.AddComponent<LevelRoomAuthoring2D>();
            room.ConfigureForTests(
                id,
                coordinate,
                Vector2.one,
                Vector2Int.one,
                bounds);
            room.ConfigureFolderSlotForTests(slot);
            room.SnapToAuthoredGrid();
            return room;
        }

        private static LevelDoorEndpointAuthoring2D CreateDoor(
            LevelRoomAuthoring2D room,
            string id,
            LevelDoorSideV2 side,
            LevelDoorPlacementModeV2 mode = LevelDoorPlacementModeV2.EdgeManaged)
        {
            GameObject doorObject = new GameObject(id);
            doorObject.transform.SetParent(room.transform, false);
            LevelDoorEndpointAuthoring2D door =
                doorObject.AddComponent<LevelDoorEndpointAuthoring2D>();
            door.ConfigureAuthoring(
                id,
                room,
                side,
                mode,
                0.5f,
                Vector2.zero,
                true,
                true);
            door.SnapToPlacement();
            return door;
        }

        private static LevelDoorLinkAuthoring2D CreateConnection(
            Transform parent,
            LevelRoomAuthoring2D sourceRoom,
            LevelDoorEndpointAuthoring2D sourceDoor,
            LevelRoomAuthoring2D destinationRoom,
            LevelDoorEndpointAuthoring2D destinationDoor)
        {
            GameObject linkObject = new GameObject("connection.test");
            linkObject.transform.SetParent(parent, false);
            LevelDoorLinkAuthoring2D link =
                linkObject.AddComponent<LevelDoorLinkAuthoring2D>();
            link.ConfigureConnection(
                "connection.test",
                sourceRoom,
                sourceDoor,
                destinationRoom,
                destinationDoor);
            return link;
        }

        private static LevelRoomRecord RoomRecord(string id, Vector2Int coordinate)
        {
            return new LevelRoomRecord(
                id,
                coordinate,
                Vector2.one,
                Vector2Int.one,
                LevelRoomAlignment.GridOrigin,
                new Rect(coordinate.x, coordinate.y, 1f, 1f),
                true,
                0,
                coordinate,
                true,
                id);
        }
    }
}
#endif
