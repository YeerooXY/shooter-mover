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
    public sealed class LevelGridEditorWindowTests
    {
        [Test]
        public void Window_OpensWithoutSelectedRoot()
        {
            Selection.activeObject = null;
            LevelGridEditorWindow window =
                EditorWindow.GetWindow<LevelGridEditorWindow>();
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
                LevelDraft root = CreateRoot(rootObject);
                LevelRoom room = CreateRoom(
                    root.transform,
                    "room.projected",
                    new Vector2Int(2, -3),
                    1);
                CreateDoor(room, "door.projected", LevelDoorSide.North);

                LevelGridEditorView projection =
                    LevelGridEditorView.Build(root);

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
                LevelDraft root = CreateRoot(rootObject);
                LevelRoom room = CreateRoom(
                    root.transform,
                    "room.stable-move",
                    Vector2Int.zero,
                    1);
                string before = room.RoomIdText;

                LevelGridEditorOperations.MoveRoom(
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
                LevelDraft root = CreateRoot(rootObject);
                Vector2Int coordinate = new Vector2Int(4, 9);
                CreateRoom(root.transform, "room.slot-1", coordinate, 1);
                CreateRoom(root.transform, "room.slot-3", coordinate, 3);

                Assert.That(
                    LevelGridEditorOperations.FindNextFreeFolderSlot(root, coordinate),
                    Is.EqualTo(2));
                LevelRoom created =
                    LevelGridEditorOperations.CreateRoom(root, coordinate);
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
                LevelDraft root = CreateRoot(rootObject);
                LevelRoom room = CreateRoom(
                    root.transform,
                    "room.multi-door",
                    Vector2Int.zero,
                    1);

                DoorEndpoint first =
                    LevelGridEditorOperations.CreateDoor(
                        room,
                        LevelDoorSide.East,
                        0.25f);
                DoorEndpoint second =
                    LevelGridEditorOperations.CreateDoor(
                        room,
                        LevelDoorSide.East,
                        0.75f);

                Assert.That(first.Side, Is.EqualTo(LevelDoorSide.East));
                Assert.That(second.Side, Is.EqualTo(LevelDoorSide.East));
                Assert.That(first.DoorIdText, Is.Not.EqualTo(second.DoorIdText));
                Assert.That(
                    room.GetComponentsInChildren<DoorEndpoint>(true)
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
                LevelDraft root = CreateRoot(rootObject);
                LevelRoom firstRoom = CreateRoom(
                    root.transform,
                    "room.first",
                    Vector2Int.zero,
                    1);
                LevelRoom secondRoom = CreateRoom(
                    root.transform,
                    "room.second",
                    Vector2Int.right,
                    1);
                LevelRoom thirdRoom = CreateRoom(
                    root.transform,
                    "room.third",
                    Vector2Int.up,
                    1);
                DoorEndpoint first = CreateDoor(
                    firstRoom,
                    "door.first",
                    LevelDoorSide.East);
                DoorEndpoint second = CreateDoor(
                    secondRoom,
                    "door.second",
                    LevelDoorSide.West);
                DoorEndpoint third = CreateDoor(
                    thirdRoom,
                    "door.third",
                    LevelDoorSide.South);
                CreateConnection(root.transform, firstRoom, first, secondRoom, second);

                DoorLink created;
                string rejection;
                bool accepted = LevelGridEditorOperations.TryCreateConnection(
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
                LevelDraft root = CreateRoot(rootObject);
                LevelRoom left = CreateRoom(
                    root.transform,
                    "room.left",
                    Vector2Int.zero,
                    1);
                LevelRoom right = CreateRoom(
                    root.transform,
                    "room.right",
                    Vector2Int.right,
                    1);
                DoorEndpoint leftDoor = CreateDoor(
                    left,
                    "door.left-east",
                    LevelDoorSide.East);
                DoorEndpoint rightDoor = CreateDoor(
                    right,
                    "door.right-west",
                    LevelDoorSide.West);

                DoorLink created;
                string rejection;
                bool accepted = LevelGridEditorOperations.TryCreateConnection(
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
                LevelGridConnectionRecord record = created.BuildRecord();
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
                LevelDraft root = CreateRoot(rootObject);
                LevelRoom left = CreateRoom(
                    root.transform,
                    "room.left",
                    Vector2Int.zero,
                    1);
                LevelRoom right = CreateRoom(
                    root.transform,
                    "room.right",
                    Vector2Int.right,
                    1);
                DoorEndpoint leftDoor = CreateDoor(
                    left,
                    "door.left",
                    LevelDoorSide.East);
                DoorEndpoint rightDoor = CreateDoor(
                    right,
                    "door.right",
                    LevelDoorSide.West);
                DoorLink link = CreateConnection(
                    root.transform,
                    left,
                    leftDoor,
                    right,
                    rightDoor);

                LevelGridEditorOperations.DeleteConnection(link);

                Assert.That(
                    root.GetComponentsInChildren<DoorLink>(true),
                    Is.Empty);
                Assert.That(
                    root.GetComponentsInChildren<DoorEndpoint>(true),
                    Has.Member(leftDoor));
                Assert.That(
                    root.GetComponentsInChildren<DoorEndpoint>(true),
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
                LevelDraft root = CreateRoot(rootObject);
                LevelRoom left = CreateRoom(
                    root.transform,
                    "room.left",
                    Vector2Int.zero,
                    1);
                LevelRoom right = CreateRoom(
                    root.transform,
                    "room.right",
                    Vector2Int.right,
                    1);
                DoorEndpoint leftDoor = CreateDoor(
                    left,
                    "door.left",
                    LevelDoorSide.East);
                DoorEndpoint rightDoor = CreateDoor(
                    right,
                    "door.right",
                    LevelDoorSide.West);
                CreateConnection(root.transform, left, leftDoor, right, rightDoor);

                LevelGridEditorOperations.DeleteDoor(leftDoor);

                Assert.That(
                    root.GetComponentsInChildren<DoorLink>(true),
                    Is.Empty);
                DoorEndpoint[] remaining =
                    root.GetComponentsInChildren<DoorEndpoint>(true);
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
                LevelDraft root = CreateRoot(rootObject);
                LevelRoom left = CreateRoom(
                    root.transform,
                    "room.left",
                    Vector2Int.zero,
                    1);
                LevelRoom right = CreateRoom(
                    root.transform,
                    "room.right",
                    Vector2Int.right,
                    1);
                DoorEndpoint leftDoor = CreateDoor(
                    left,
                    "door.left",
                    LevelDoorSide.East);
                DoorEndpoint rightDoor = CreateDoor(
                    right,
                    "door.right",
                    LevelDoorSide.West);
                CreateConnection(root.transform, left, leftDoor, right, rightDoor);

                bool deleted = LevelGridEditorOperations.DeleteRoom(left, false);

                Assert.That(deleted, Is.True);
                Assert.That(
                    root.GetComponentsInChildren<LevelRoom>(true),
                    Has.Member(right));
                Assert.That(
                    root.GetComponentsInChildren<DoorEndpoint>(true),
                    Has.Member(rightDoor));
                Assert.That(
                    root.GetComponentsInChildren<DoorLink>(true),
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
                LevelDraft root = CreateRoot(rootObject);
                LevelRoom room = CreateRoom(
                    root.transform,
                    "room.undo-move",
                    new Vector2Int(1, 2),
                    1);

                LevelGridEditorOperations.MoveRoom(room, new Vector2Int(8, -2));
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
                LevelDraft root = CreateRoot(rootObject);
                LevelRoom room = CreateRoom(
                    root.transform,
                    "room.undo-door",
                    Vector2Int.zero,
                    1);

                LevelGridEditorOperations.CreateDoor(
                    room,
                    LevelDoorSide.North,
                    0.5f);
                Undo.PerformUndo();

                Assert.That(
                    room.GetComponentsInChildren<DoorEndpoint>(true),
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
                LevelDraft root = CreateRoot(rootObject);
                LevelRoom left = CreateRoom(
                    root.transform,
                    "room.left",
                    Vector2Int.zero,
                    1);
                LevelRoom right = CreateRoom(
                    root.transform,
                    "room.right",
                    Vector2Int.right,
                    1);
                DoorEndpoint leftDoor = CreateDoor(
                    left,
                    "door.left",
                    LevelDoorSide.East);
                DoorEndpoint rightDoor = CreateDoor(
                    right,
                    "door.right",
                    LevelDoorSide.West);
                DoorLink created;
                string rejection;
                Assert.That(
                    LevelGridEditorOperations.TryCreateConnection(
                        root,
                        leftDoor,
                        rightDoor,
                        out created,
                        out rejection),
                    Is.True,
                    rejection);

                Undo.PerformUndo();

                Assert.That(
                    root.GetComponentsInChildren<DoorLink>(true),
                    Is.Empty);
                Assert.That(
                    root.GetComponentsInChildren<DoorEndpoint>(true)
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
                LevelDraft root = CreateRoot(rootObject);
                GameObject branchA = new GameObject("Branch A");
                branchA.transform.SetParent(root.transform, false);
                GameObject branchB = new GameObject("Branch B");
                branchB.transform.SetParent(root.transform, false);
                CreateRoom(
                    branchA.transform,
                    "room.duplicate",
                    Vector2Int.zero,
                    1);
                LevelRoom target = CreateRoom(
                    branchB.transform,
                    "room.duplicate",
                    Vector2Int.right,
                    1);

                LevelGridValidationResult validation = root.ValidateGridAuthoring(
                    LevelGridValidationPurpose.Draft);
                LevelGridProblem targetProblem = validation.Problems.First(
                    problem => problem.Code == LevelGridProblemCode.DuplicateRoomIdentity
                        && problem.DiagnosticLocation.Contains("Branch B"));

                Component selected = LevelGridEditorProblemLocator.SelectExact(
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
                    + "LevelGridEditorWindow.cs");
            string operationsSource = File.ReadAllText(
                "Assets/ShooterMover/Editor/LevelDesign/Foundation/"
                    + "LevelGridEditorOperations.cs");
            string combined = windowSource + operationsSource;

            StringAssert.Contains("LevelDraft", combined);
            StringAssert.Contains("LevelRoom", combined);
            StringAssert.Contains("DoorEndpoint", combined);
            StringAssert.Contains("DoorLink", combined);
            StringAssert.DoesNotContain("JsonUtility", combined);
            StringAssert.DoesNotContain("File.WriteAllText", combined);
            StringAssert.DoesNotContain("ScriptableObject.CreateInstance", combined);
            StringAssert.DoesNotContain("RoomContentJsonImporter", combined);
        }

        [Test]
        public void FixedDoorPosition_IsStoredRelativeToOwningRoom()
        {
            GameObject rootObject = new GameObject("Root");
            try
            {
                LevelDraft root = CreateRoot(rootObject);
                LevelRoom room = CreateRoom(
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
                DoorEndpoint door =
                    doorObject.AddComponent<DoorEndpoint>();
                door.ConfigureAuthoring(
                    "door.fixed",
                    room,
                    LevelDoorSide.North,
                    LevelDoorPlacementMode.Fixed,
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

        private static LevelDraft CreateRoot(GameObject rootObject)
        {
            LevelDraft root =
                rootObject.AddComponent<LevelDraft>();
            root.ConfigureForTests("level.grid-editor-tests");
            return root;
        }

        private static LevelRoom CreateRoom(
            Transform parent,
            string roomId,
            Vector2Int coordinate,
            int folderSlot)
        {
            GameObject roomObject = new GameObject(roomId);
            roomObject.transform.SetParent(parent, false);
            BoxCollider2D bounds = roomObject.AddComponent<BoxCollider2D>();
            bounds.size = Vector2.one;
            LevelRoom room =
                roomObject.AddComponent<LevelRoom>();
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

        private static DoorEndpoint CreateDoor(
            LevelRoom room,
            string doorId,
            LevelDoorSide side)
        {
            GameObject doorObject = new GameObject(doorId);
            doorObject.transform.SetParent(room.transform, false);
            DoorEndpoint door =
                doorObject.AddComponent<DoorEndpoint>();
            door.ConfigureAuthoring(
                doorId,
                room,
                side,
                LevelDoorPlacementMode.EdgeManaged,
                0.5f,
                Vector2.zero,
                true,
                true);
            door.SnapToPlacement();
            return door;
        }

        private static DoorLink CreateConnection(
            Transform parent,
            LevelRoom sourceRoom,
            DoorEndpoint sourceDoor,
            LevelRoom destinationRoom,
            DoorEndpoint destinationDoor)
        {
            GameObject connectionObject = new GameObject("Connection");
            connectionObject.transform.SetParent(parent, false);
            DoorLink connection =
                connectionObject.AddComponent<DoorLink>();
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
