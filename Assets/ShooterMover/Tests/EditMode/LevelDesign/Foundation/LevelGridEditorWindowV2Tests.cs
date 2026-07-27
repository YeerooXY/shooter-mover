#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using ShooterMover.Editor.LevelDesign.Foundation;
using ShooterMover.UnityAdapters.Authoring.LevelDesign;
using UnityEditor;
using UnityEngine;

namespace ShooterMover.Tests.EditMode.LevelDesign.Foundation
{
    public sealed class LevelGridEditorWindowV2Tests
    {
        [Test]
        public void Window_OpensWithoutSelectedRoot()
        {
            Selection.activeObject = null;
            LevelGridEditorWindowV2 window =
                EditorWindow.GetWindow<LevelGridEditorWindowV2>();
            try
            {
                window.SetActiveRootForTests(null);
                window.RebuildProjectionForTests();
                Assert.That(window.ActiveRoot, Is.Null);
                Assert.That(window.Projection.Rooms, Is.Empty);
            }
            finally
            {
                window.Close();
            }
        }

        [Test]
        public void Projection_UsesExistingSceneRooms()
        {
            GameObject rootObject = new GameObject("Root");
            try
            {
                LevelDesignSceneAuthoringRoot2D root = CreateRoot(rootObject);
                LevelRoomAuthoring2D room = CreateRoom(
                    root.transform,
                    "room.projected",
                    new Vector2Int(2, -3),
                    1);
                CreateDoor(room, "door.projected", LevelDoorSideV2.North);

                LevelGridEditorProjectionV2 projection =
                    LevelGridEditorProjectionV2.Build(root);

                Assert.That(projection.Rooms.Count, Is.EqualTo(1));
                Assert.That(projection.Rooms[0].Room, Is.SameAs(room));
                Assert.That(projection.Rooms[0].Doors.Count, Is.EqualTo(1));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        public void RoomMovement_UpdatesGridWithoutChangingStableId()
        {
            GameObject rootObject = new GameObject("Root");
            try
            {
                LevelDesignSceneAuthoringRoot2D root = CreateRoot(rootObject);
                LevelRoomAuthoring2D room = CreateRoom(
                    root.transform,
                    "room.stable-move",
                    Vector2Int.zero,
                    1);
                string before = room.RoomIdText;

                LevelGridEditorOperationsV2.MoveRoom(
                    room,
                    new Vector2Int(6, -4));

                Assert.That(room.GridCoordinate, Is.EqualTo(new Vector2Int(6, -4)));
                Assert.That(room.RoomIdText, Is.EqualTo(before));
                Assert.That(room.FolderSlot, Is.EqualTo(1));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        public void RoomCreation_UsesNextFreePerCoordinateSlot()
        {
            GameObject rootObject = new GameObject("Root");
            try
            {
                LevelDesignSceneAuthoringRoot2D root = CreateRoot(rootObject);
                Vector2Int coordinate = new Vector2Int(4, 9);
                CreateRoom(root.transform, "room.slot-1", coordinate, 1);
                CreateRoom(root.transform, "room.slot-3", coordinate, 3);

                Assert.That(
                    LevelGridEditorOperationsV2.FindNextFreeFolderSlot(root, coordinate),
                    Is.EqualTo(2));
                LevelRoomAuthoring2D created =
                    LevelGridEditorOperationsV2.CreateRoom(root, coordinate);
                Assert.That(created.FolderSlot, Is.EqualTo(2));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        public void DoorCreation_AllowsMultipleDoorsOnSameSide()
        {
            GameObject rootObject = new GameObject("Root");
            try
            {
                LevelDesignSceneAuthoringRoot2D root = CreateRoot(rootObject);
                LevelRoomAuthoring2D room = CreateRoom(
                    root.transform,
                    "room.multi-door",
                    Vector2Int.zero,
                    1);

                LevelDoorEndpointAuthoring2D first =
                    LevelGridEditorOperationsV2.CreateDoor(
                        room,
                        LevelDoorSideV2.East,
                        0.25f);
                LevelDoorEndpointAuthoring2D second =
                    LevelGridEditorOperationsV2.CreateDoor(
                        room,
                        LevelDoorSideV2.East,
                        0.75f);

                Assert.That(first.Side, Is.EqualTo(LevelDoorSideV2.East));
                Assert.That(second.Side, Is.EqualTo(LevelDoorSideV2.East));
                Assert.That(first.DoorIdText, Is.Not.EqualTo(second.DoorIdText));
                Assert.That(
                    room.GetComponentsInChildren<LevelDoorEndpointAuthoring2D>(true)
                        .Length,
                    Is.EqualTo(2));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        public void ConnectionCreation_RefusesAlreadyConnectedEndpoint()
        {
            GameObject rootObject = new GameObject("Root");
            try
            {
                LevelDesignSceneAuthoringRoot2D root = CreateRoot(rootObject);
                LevelRoomAuthoring2D firstRoom = CreateRoom(
                    root.transform,
                    "room.first",
                    Vector2Int.zero,
                    1);
                LevelRoomAuthoring2D secondRoom = CreateRoom(
                    root.transform,
                    "room.second",
                    Vector2Int.right,
                    1);
                LevelRoomAuthoring2D thirdRoom = CreateRoom(
                    root.transform,
                    "room.third",
                    Vector2Int.up,
                    1);
                LevelDoorEndpointAuthoring2D first = CreateDoor(
                    firstRoom,
                    "door.first",
                    LevelDoorSideV2.East);
                LevelDoorEndpointAuthoring2D second = CreateDoor(
                    secondRoom,
                    "door.second",
                    LevelDoorSideV2.West);
                LevelDoorEndpointAuthoring2D third = CreateDoor(
                    thirdRoom,
                    "door.third",
                    LevelDoorSideV2.South);
                CreateConnection(root.transform, firstRoom, first, secondRoom, second);

                LevelDoorLinkAuthoring2D created;
                string rejection;
                bool accepted = LevelGridEditorOperationsV2.TryCreateConnection(
                    root,
                    first,
                    third,
                    out created,
                    out rejection);

                Assert.That(accepted, Is.False);
                Assert.That(created, Is.Null);
                StringAssert.Contains("already connected", rejection);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        public void ConnectionCreation_CreatesExactEndpointLink()
        {
            GameObject rootObject = new GameObject("Root");
            try
            {
                LevelDesignSceneAuthoringRoot2D root = CreateRoot(rootObject);
                LevelRoomAuthoring2D left = CreateRoom(
                    root.transform,
                    "room.left",
                    Vector2Int.zero,
                    1);
                LevelRoomAuthoring2D right = CreateRoom(
                    root.transform,
                    "room.right",
                    Vector2Int.right,
                    1);
                LevelDoorEndpointAuthoring2D leftDoor = CreateDoor(
                    left,
                    "door.left-east",
                    LevelDoorSideV2.East);
                LevelDoorEndpointAuthoring2D rightDoor = CreateDoor(
                    right,
                    "door.right-west",
                    LevelDoorSideV2.West);

                LevelDoorLinkAuthoring2D created;
                string rejection;
                bool accepted = LevelGridEditorOperationsV2.TryCreateConnection(
                    root,
                    leftDoor,
                    rightDoor,
                    out created,
                    out rejection);

                Assert.That(accepted, Is.True, rejection);
                Assert.That(created.SourceRoom, Is.SameAs(left));
                Assert.That(created.SourceDoor, Is.SameAs(leftDoor));
                Assert.That(created.DestinationRoom, Is.SameAs(right));
                Assert.That(created.DestinationDoor, Is.SameAs(rightDoor));
                LevelGridConnectionRecordV2 record = created.BuildRecord();
                Assert.That(record.SourceRoomId, Is.EqualTo(left.RoomIdText));
                Assert.That(record.SourceDoorId, Is.EqualTo(leftDoor.DoorIdText));
                Assert.That(record.DestinationRoomId, Is.EqualTo(right.RoomIdText));
                Assert.That(record.DestinationDoorId, Is.EqualTo(rightDoor.DoorIdText));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        public void DeleteConnection_PreservesBothEndpoints()
        {
            GameObject rootObject = new GameObject("Root");
            try
            {
                LevelDesignSceneAuthoringRoot2D root = CreateRoot(rootObject);
                LevelRoomAuthoring2D left = CreateRoom(
                    root.transform,
                    "room.left",
                    Vector2Int.zero,
                    1);
                LevelRoomAuthoring2D right = CreateRoom(
                    root.transform,
                    "room.right",
                    Vector2Int.right,
                    1);
                LevelDoorEndpointAuthoring2D leftDoor = CreateDoor(
                    left,
                    "door.left",
                    LevelDoorSideV2.East);
                LevelDoorEndpointAuthoring2D rightDoor = CreateDoor(
                    right,
                    "door.right",
                    LevelDoorSideV2.West);
                LevelDoorLinkAuthoring2D link = CreateConnection(
                    root.transform,
                    left,
                    leftDoor,
                    right,
                    rightDoor);

                LevelGridEditorOperationsV2.DeleteConnection(link);

                Assert.That(
                    root.GetComponentsInChildren<LevelDoorLinkAuthoring2D>(true),
                    Is.Empty);
                Assert.That(
                    root.GetComponentsInChildren<LevelDoorEndpointAuthoring2D>(true),
                    Has.Member(leftDoor));
                Assert.That(
                    root.GetComponentsInChildren<LevelDoorEndpointAuthoring2D>(true),
                    Has.Member(rightDoor));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        public void DeleteDoor_PreservesOppositeEndpoint()
        {
            GameObject rootObject = new GameObject("Root");
            try
            {
                LevelDesignSceneAuthoringRoot2D root = CreateRoot(rootObject);
                LevelRoomAuthoring2D left = CreateRoom(
                    root.transform,
                    "room.left",
                    Vector2Int.zero,
                    1);
                LevelRoomAuthoring2D right = CreateRoom(
                    root.transform,
                    "room.right",
                    Vector2Int.right,
                    1);
                LevelDoorEndpointAuthoring2D leftDoor = CreateDoor(
                    left,
                    "door.left",
                    LevelDoorSideV2.East);
                LevelDoorEndpointAuthoring2D rightDoor = CreateDoor(
                    right,
                    "door.right",
                    LevelDoorSideV2.West);
                CreateConnection(root.transform, left, leftDoor, right, rightDoor);

                LevelGridEditorOperationsV2.DeleteDoor(leftDoor);

                Assert.That(
                    root.GetComponentsInChildren<LevelDoorLinkAuthoring2D>(true),
                    Is.Empty);
                LevelDoorEndpointAuthoring2D[] remaining =
                    root.GetComponentsInChildren<LevelDoorEndpointAuthoring2D>(true);
                Assert.That(remaining.Length, Is.EqualTo(1));
                Assert.That(remaining[0], Is.SameAs(rightDoor));
                Assert.That(root.LastGridValidation.UnconnectedTraversableDoorCount, Is.EqualTo(1));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        public void DeleteRoom_PreservesNeighbouringEndpoint()
        {
            GameObject rootObject = new GameObject("Root");
            try
            {
                LevelDesignSceneAuthoringRoot2D root = CreateRoot(rootObject);
                LevelRoomAuthoring2D left = CreateRoom(
                    root.transform,
                    "room.left",
                    Vector2Int.zero,
                    1);
                LevelRoomAuthoring2D right = CreateRoom(
                    root.transform,
                    "room.right",
                    Vector2Int.right,
                    1);
                LevelDoorEndpointAuthoring2D leftDoor = CreateDoor(
                    left,
                    "door.left",
                    LevelDoorSideV2.East);
                LevelDoorEndpointAuthoring2D rightDoor = CreateDoor(
                    right,
                    "door.right",
                    LevelDoorSideV2.West);
                CreateConnection(root.transform, left, leftDoor, right, rightDoor);

                bool deleted = LevelGridEditorOperationsV2.DeleteRoom(left, false);

                Assert.That(deleted, Is.True);
                Assert.That(
                    root.GetComponentsInChildren<LevelRoomAuthoring2D>(true),
                    Has.Member(right));
                Assert.That(
                    root.GetComponentsInChildren<LevelDoorEndpointAuthoring2D>(true),
                    Has.Member(rightDoor));
                Assert.That(
                    root.GetComponentsInChildren<LevelDoorLinkAuthoring2D>(true),
                    Is.Empty);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        public void Undo_RestoresRoomMovement()
        {
            GameObject rootObject = new GameObject("Root");
            try
            {
                LevelDesignSceneAuthoringRoot2D root = CreateRoot(rootObject);
                LevelRoomAuthoring2D room = CreateRoom(
                    root.transform,
                    "room.undo-move",
                    new Vector2Int(1, 2),
                    1);

                LevelGridEditorOperationsV2.MoveRoom(room, new Vector2Int(8, -2));
                Undo.PerformUndo();

                Assert.That(room.GridCoordinate, Is.EqualTo(new Vector2Int(1, 2)));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        public void Undo_RemovesCreatedDoorInOneStep()
        {
            GameObject rootObject = new GameObject("Root");
            try
            {
                LevelDesignSceneAuthoringRoot2D root = CreateRoot(rootObject);
                LevelRoomAuthoring2D room = CreateRoom(
                    root.transform,
                    "room.undo-door",
                    Vector2Int.zero,
                    1);

                LevelGridEditorOperationsV2.CreateDoor(
                    room,
                    LevelDoorSideV2.North,
                    0.5f);
                Undo.PerformUndo();

                Assert.That(
                    room.GetComponentsInChildren<LevelDoorEndpointAuthoring2D>(true),
                    Is.Empty);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        public void Undo_RemovesCreatedConnectionInOneStep()
        {
            GameObject rootObject = new GameObject("Root");
            try
            {
                LevelDesignSceneAuthoringRoot2D root = CreateRoot(rootObject);
                LevelRoomAuthoring2D left = CreateRoom(
                    root.transform,
                    "room.left",
                    Vector2Int.zero,
                    1);
                LevelRoomAuthoring2D right = CreateRoom(
                    root.transform,
                    "room.right",
                    Vector2Int.right,
                    1);
                LevelDoorEndpointAuthoring2D leftDoor = CreateDoor(
                    left,
                    "door.left",
                    LevelDoorSideV2.East);
                LevelDoorEndpointAuthoring2D rightDoor = CreateDoor(
                    right,
                    "door.right",
                    LevelDoorSideV2.West);
                LevelDoorLinkAuthoring2D created;
                string rejection;
                Assert.That(
                    LevelGridEditorOperationsV2.TryCreateConnection(
                        root,
                        leftDoor,
                        rightDoor,
                        out created,
                        out rejection),
                    Is.True,
                    rejection);

                Undo.PerformUndo();

                Assert.That(
                    root.GetComponentsInChildren<LevelDoorLinkAuthoring2D>(true),
                    Is.Empty);
                Assert.That(
                    root.GetComponentsInChildren<LevelDoorEndpointAuthoring2D>(true)
                        .Length,
                    Is.EqualTo(2));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        public void ProblemSelection_UsesStableIdAndDiagnosticPathForDuplicate()
        {
            GameObject rootObject = new GameObject("Root");
            try
            {
                LevelDesignSceneAuthoringRoot2D root = CreateRoot(rootObject);
                GameObject branchA = new GameObject("Branch A");
                branchA.transform.SetParent(root.transform, false);
                GameObject branchB = new GameObject("Branch B");
                branchB.transform.SetParent(root.transform, false);
                CreateRoom(
                    branchA.transform,
                    "room.duplicate",
                    Vector2Int.zero,
                    1);
                LevelRoomAuthoring2D target = CreateRoom(
                    branchB.transform,
                    "room.duplicate",
                    Vector2Int.right,
                    1);

                LevelGridValidationResultV2 validation = root.ValidateGridAuthoring(
                    LevelGridValidationPurposeV2.Draft);
                LevelGridProblemV2 targetProblem = validation.Problems.First(
                    problem => problem.Code == LevelGridProblemCodeV2.DuplicateRoomIdentity
                        && problem.DiagnosticLocation.Contains("Branch B"));

                Component selected = LevelGridEditorProblemLocatorV2.SelectExact(
                    root,
                    targetProblem);

                Assert.That(selected, Is.SameAs(target));
                Assert.That(Selection.activeGameObject, Is.SameAs(target.gameObject));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        public void EditorSource_DoesNotIntroduceIndependentJsonOrRuntimeGraph()
        {
            string windowSource = File.ReadAllText(
                "Assets/ShooterMover/Editor/LevelDesign/Foundation/"
                    + "LevelGridEditorWindowV2.cs");
            string operationsSource = File.ReadAllText(
                "Assets/ShooterMover/Editor/LevelDesign/Foundation/"
                    + "LevelGridEditorOperationsV2.cs");
            string combined = windowSource + operationsSource;

            StringAssert.Contains("LevelDesignSceneAuthoringRoot2D", combined);
            StringAssert.Contains("LevelRoomAuthoring2D", combined);
            StringAssert.Contains("LevelDoorEndpointAuthoring2D", combined);
            StringAssert.Contains("LevelDoorLinkAuthoring2D", combined);
            StringAssert.DoesNotContain("JsonUtility", combined);
            StringAssert.DoesNotContain("File.WriteAllText", combined);
            StringAssert.DoesNotContain("ScriptableObject.CreateInstance", combined);
            StringAssert.DoesNotContain("RoomContentJsonImporterV1", combined);
        }

        [Test]
        public void FixedDoorPosition_IsStoredRelativeToOwningRoom()
        {
            GameObject rootObject = new GameObject("Root");
            try
            {
                LevelDesignSceneAuthoringRoot2D root = CreateRoot(rootObject);
                LevelRoomAuthoring2D room = CreateRoom(
                    root.transform,
                    "room.fixed",
                    Vector2Int.zero,
                    1);
                room.transform.position = new Vector3(10f, 20f, 0f);
                GameObject helper = new GameObject("Door Helpers");
                helper.transform.SetParent(room.transform, false);
                helper.transform.localPosition = new Vector3(2f, 3f, 0f);
                GameObject doorObject = new GameObject("Fixed Door");
                doorObject.transform.SetParent(helper.transform, false);
                LevelDoorEndpointAuthoring2D door =
                    doorObject.AddComponent<LevelDoorEndpointAuthoring2D>();
                door.ConfigureAuthoring(
                    "door.fixed",
                    room,
                    LevelDoorSideV2.North,
                    LevelDoorPlacementModeV2.Fixed,
                    0.5f,
                    new Vector2(4f, 5f),
                    true,
                    false);

                door.SnapToPlacement();
                Vector3 expected = room.transform.TransformPoint(new Vector3(4f, 5f, 0f));

                Assert.That(
                    Vector3.Distance(door.transform.position, expected),
                    Is.LessThan(0.001f));
                Assert.That(door.FixedLocalPosition, Is.EqualTo(new Vector2(4f, 5f)));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        private static LevelDesignSceneAuthoringRoot2D CreateRoot(GameObject rootObject)
        {
            LevelDesignSceneAuthoringRoot2D root =
                rootObject.AddComponent<LevelDesignSceneAuthoringRoot2D>();
            root.ConfigureForTests("level.grid-editor-tests");
            return root;
        }

        private static LevelRoomAuthoring2D CreateRoom(
            Transform parent,
            string roomId,
            Vector2Int coordinate,
            int folderSlot)
        {
            GameObject roomObject = new GameObject(roomId);
            roomObject.transform.SetParent(parent, false);
            BoxCollider2D bounds = roomObject.AddComponent<BoxCollider2D>();
            bounds.size = Vector2.one;
            LevelRoomAuthoring2D room =
                roomObject.AddComponent<LevelRoomAuthoring2D>();
            room.ConfigureForTests(
                roomId,
                coordinate,
                Vector2.one,
                Vector2Int.one,
                bounds);
            room.ConfigureFolderSlotForTests(folderSlot);
            room.SnapToAuthoredGrid();
            return room;
        }

        private static LevelDoorEndpointAuthoring2D CreateDoor(
            LevelRoomAuthoring2D room,
            string doorId,
            LevelDoorSideV2 side)
        {
            GameObject doorObject = new GameObject(doorId);
            doorObject.transform.SetParent(room.transform, false);
            LevelDoorEndpointAuthoring2D door =
                doorObject.AddComponent<LevelDoorEndpointAuthoring2D>();
            door.ConfigureAuthoring(
                doorId,
                room,
                side,
                LevelDoorPlacementModeV2.EdgeManaged,
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
            GameObject connectionObject = new GameObject("Connection");
            connectionObject.transform.SetParent(parent, false);
            LevelDoorLinkAuthoring2D connection =
                connectionObject.AddComponent<LevelDoorLinkAuthoring2D>();
            connection.ConfigureConnection(
                "connection." + Guid.NewGuid().ToString("N"),
                sourceRoom,
                sourceDoor,
                destinationRoom,
                destinationDoor,
                LevelDoorTravelPolicy.Bidirectional);
            return connection;
        }
    }
}
#endif
