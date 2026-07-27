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
    public sealed class LevelGridEditorSecondAuditV2Tests
    {
        [Test]
        public void UndoRedoRefresh_RevalidatesRestoredTopology()
        {
            GameObject rootObject = new GameObject("Root");
            try
            {
                LevelDesignSceneAuthoringRoot2D root = CreateRoot(rootObject);
                LevelRoomAuthoring2D room = CreateRoom(root.transform, Vector2Int.zero);
                LevelDoorEndpointAuthoring2D door = CreateDoor(room);
                LevelGridEditorOperationsV2.Validate(root, LevelGridValidationPurposeV2.Draft);
                Assert.That(root.LastGridValidation.UnconnectedTraversableDoorCount, Is.EqualTo(1));

                LevelGridEditorOperationsV2.DeleteDoor(door);
                Assert.That(root.LastGridValidation.UnconnectedTraversableDoorCount, Is.Zero);
                Undo.PerformUndo();
                InvokeLiveValidation("OnUndoRedoPerformed");
                InvokeLiveValidation("RefreshPendingRoots");

                Assert.That(
                    root.GetComponentsInChildren<LevelDoorEndpointAuthoring2D>(true),
                    Has.Length.EqualTo(1));
                Assert.That(root.LastGridValidation.UnconnectedTraversableDoorCount, Is.EqualTo(1));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        public void DeleteRoom_RemovesMalformedLinkByEndpointOwnership()
        {
            GameObject rootObject = new GameObject("Root");
            try
            {
                LevelDesignSceneAuthoringRoot2D root = CreateRoot(rootObject);
                LevelRoomAuthoring2D removedRoom = CreateRoom(root.transform, Vector2Int.zero);
                LevelRoomAuthoring2D remainingRoom = CreateRoom(root.transform, Vector2Int.right);
                LevelDoorEndpointAuthoring2D removedDoor = CreateDoor(removedRoom);
                LevelDoorEndpointAuthoring2D remainingDoor = CreateDoor(remainingRoom);
                LevelDoorLinkAuthoring2D malformed = CreateConnection(
                    root.transform,
                    removedRoom,
                    removedDoor,
                    remainingRoom,
                    remainingDoor);
                malformed.ConfigureConnection(
                    malformed.ConnectionIdText,
                    remainingRoom,
                    removedDoor,
                    remainingRoom,
                    remainingDoor,
                    LevelDoorTravelPolicy.Bidirectional);

                Assert.That(LevelGridEditorOperationsV2.DeleteRoom(removedRoom, false), Is.True);
                Assert.That(root.GetComponentsInChildren<LevelDoorLinkAuthoring2D>(true), Is.Empty);
                Assert.That(
                    root.GetComponentsInChildren<LevelDoorEndpointAuthoring2D>(true),
                    Has.Member(remainingDoor));

                Undo.PerformUndo();
                Assert.That(root.GetComponentsInChildren<LevelRoomAuthoring2D>(true), Has.Length.EqualTo(2));
                Assert.That(root.GetComponentsInChildren<LevelDoorLinkAuthoring2D>(true), Has.Length.EqualTo(1));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        public void FixedDoorMove_CapturesStoredPositionInSameUndoGroup()
        {
            GameObject rootObject = new GameObject("Root");
            try
            {
                LevelDesignSceneAuthoringRoot2D root = CreateRoot(rootObject);
                LevelDoorEndpointAuthoring2D door = CreateFixedDoor(
                    CreateRoom(root.transform, Vector2Int.zero));
                Vector3 originalLocal = door.transform.localPosition;
                Vector2 originalFixed = door.FixedLocalPosition;

                Undo.IncrementCurrentGroup();
                int group = Undo.GetCurrentGroup();
                Undo.SetCurrentGroupName("Move Fixed Level Door");
                Undo.RecordObject(door.transform, "Move Fixed Level Door");
                door.transform.localPosition = new Vector3(3f, 4f, 0f);
                InvokeLiveValidation("CaptureFixedDoorPositionWithUndo", door);
                Undo.CollapseUndoOperations(group);

                Assert.That(door.FixedLocalPosition, Is.EqualTo(new Vector2(3f, 4f)));
                Undo.PerformUndo();
                Assert.That(door.transform.localPosition, Is.EqualTo(originalLocal));
                Assert.That(door.FixedLocalPosition, Is.EqualTo(originalFixed));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        public void Validation_MigratesLegacyFixedDoorWithoutMovingIt()
        {
            GameObject rootObject = new GameObject("Root");
            try
            {
                LevelDesignSceneAuthoringRoot2D root = CreateRoot(rootObject);
                LevelRoomAuthoring2D room = CreateRoom(root.transform, Vector2Int.zero);
                room.transform.position = new Vector3(10f, 20f, 0f);
                GameObject helper = new GameObject("Door Helper");
                helper.transform.SetParent(room.transform, false);
                helper.transform.localPosition = new Vector3(2f, 1f, 0f);
                helper.transform.localRotation = Quaternion.Euler(0f, 0f, 25f);
                GameObject doorObject = new GameObject("Legacy Door");
                doorObject.transform.SetParent(helper.transform, false);
                LevelDoorEndpointAuthoring2D door =
                    doorObject.AddComponent<LevelDoorEndpointAuthoring2D>();
                door.ConfigureAuthoring(
                    LevelDesignAuthoringId.New("door"),
                    room,
                    LevelDoorSideV2.North,
                    LevelDoorPlacementModeV2.Fixed,
                    0.5f,
                    Vector2.zero,
                    true,
                    false);
                door.ConfigureLegacyFixedPositionForTests(new Vector2(4f, 5f));
                Vector3 worldBefore = door.transform.position;

                LevelGridEditorOperationsV2.Validate(root, LevelGridValidationPurposeV2.Draft);

                Assert.That(door.UsesOwningRoomFixedPositionSpace, Is.True);
                Assert.That(Vector3.Distance(door.transform.position, worldBefore), Is.LessThan(0.001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        public void HierarchyRefresh_PreservesProductionPurpose()
        {
            GameObject rootObject = new GameObject("Root");
            try
            {
                LevelDesignSceneAuthoringRoot2D root = CreateRoot(rootObject);
                LevelRoomAuthoring2D room = CreateRoom(root.transform, Vector2Int.zero);
                LevelGridEditorOperationsV2.Validate(
                    root,
                    LevelGridValidationPurposeV2.ProductionPublish);

                CreateDoor(room);
                InvokeLiveValidation("OnHierarchyChanged");
                InvokeLiveValidation("RefreshPendingRoots");

                Assert.That(root.LastGridValidation.UnconnectedTraversableDoorCount, Is.EqualTo(1));
                Assert.That(
                    root.LastGridValidation.Purpose,
                    Is.EqualTo(LevelGridValidationPurposeV2.ProductionPublish));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        public void UnrelatedHierarchyChange_DoesNotRevalidateRoot()
        {
            GameObject rootObject = new GameObject("Root");
            GameObject unrelated = null;
            try
            {
                LevelDesignSceneAuthoringRoot2D root = CreateRoot(rootObject);
                CreateRoom(root.transform, Vector2Int.zero);
                LevelGridEditorOperationsV2.Validate(
                    root,
                    LevelGridValidationPurposeV2.ProductionPublish);
                LevelGridValidationResultV2 before = root.LastGridValidation;

                unrelated = new GameObject("Unrelated Hierarchy Object");
                InvokeLiveValidation("OnHierarchyChanged");
                InvokeLiveValidation("RefreshPendingRoots");

                Assert.That(root.LastGridValidation, Is.SameAs(before));
            }
            finally
            {
                if (unrelated != null)
                {
                    UnityEngine.Object.DestroyImmediate(unrelated);
                }
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        public void ValidationRefresh_ClearsResolvedSelectedProblem()
        {
            GameObject rootObject = new GameObject("Root");
            LevelGridEditorWindowV2 window = null;
            try
            {
                LevelDesignSceneAuthoringRoot2D root = CreateRoot(rootObject);
                LevelDoorEndpointAuthoring2D door = CreateDoor(
                    CreateRoom(root.transform, Vector2Int.zero));
                window = ScriptableObject.CreateInstance<LevelGridEditorWindowV2>();
                window.SetActiveRootForTests(root);
                LevelGridProblemV2 problem = root.LastGridValidation.Problems.First(
                    candidate => candidate.Code
                        == LevelGridProblemCodeV2.UnconnectedTraversableDoor);
                FieldInfo selectedProblem = typeof(LevelGridEditorWindowV2).GetField(
                    "selectedProblem",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(selectedProblem, Is.Not.Null);
                selectedProblem.SetValue(window, problem);

                LevelGridEditorOperationsV2.UpdateDoor(
                    door,
                    door.Side,
                    door.PlacementMode,
                    door.EdgeOffset,
                    door.FixedLocalPosition,
                    false,
                    door.VisibleOnMap,
                    door.AutoFaceConnection);

                Assert.That(selectedProblem.GetValue(window), Is.Null);
            }
            finally
            {
                if (window != null)
                {
                    UnityEngine.Object.DestroyImmediate(window);
                }
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        public void SourceGuard_UsesSafeValidationHooks()
        {
            const string directory =
                "Assets/ShooterMover/Editor/LevelDesign/Foundation";
            string liveValidation = File.ReadAllText(
                directory + "/LevelGridAuthoringV2LiveValidation.cs");
            StringAssert.Contains("Undo.undoRedoPerformed += OnUndoRedoPerformed", liveValidation);
            StringAssert.Contains("EditorApplication.hierarchyChanged += OnHierarchyChanged", liveValidation);
            StringAssert.DoesNotContain(
                "ObjectChangeEvents.changesPublished",
                File.ReadAllText(directory + "/LevelGridEditorWindowV2.cs"));

            string endpointSource = File.ReadAllText(
                "Assets/ShooterMover/Runtime/UnityAdapters/Authoring/LevelDesign/"
                    + "LevelDoorEndpointAuthoring2D.cs");
            int onValidateStart = endpointSource.IndexOf(
                "private void OnValidate()",
                StringComparison.Ordinal);
            int nextMethod = endpointSource.IndexOf(
                "private Vector2 ResolveCurrentRoomRelativePosition()",
                onValidateStart,
                StringComparison.Ordinal);
            Assert.That(onValidateStart, Is.GreaterThanOrEqualTo(0));
            Assert.That(nextMethod, Is.GreaterThan(onValidateStart));
            StringAssert.DoesNotContain(
                "MigrateFixedPositionSpaceForAuthoring",
                endpointSource.Substring(onValidateStart, nextMethod - onValidateStart));
        }

        private static object InvokeLiveValidation(string methodName, params object[] arguments)
        {
            MethodInfo method = typeof(LevelGridAuthoringV2LiveValidation).GetMethod(
                methodName,
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, methodName);
            return method.Invoke(null, arguments);
        }

        private static LevelDesignSceneAuthoringRoot2D CreateRoot(GameObject rootObject)
        {
            LevelDesignSceneAuthoringRoot2D root =
                rootObject.AddComponent<LevelDesignSceneAuthoringRoot2D>();
            root.ConfigureForTests(LevelDesignAuthoringId.New("level"));
            return root;
        }

        private static LevelRoomAuthoring2D CreateRoom(Transform parent, Vector2Int coordinate)
        {
            GameObject roomObject = new GameObject("Room");
            roomObject.transform.SetParent(parent, false);
            BoxCollider2D bounds = roomObject.AddComponent<BoxCollider2D>();
            bounds.size = Vector2.one;
            LevelRoomAuthoring2D room = roomObject.AddComponent<LevelRoomAuthoring2D>();
            room.ConfigureForTests(
                LevelDesignAuthoringId.New("room"),
                coordinate,
                Vector2.one,
                Vector2Int.one,
                bounds);
            room.ConfigureFolderSlotForTests(1);
            room.SnapToAuthoredGrid();
            return room;
        }

        private static LevelDoorEndpointAuthoring2D CreateDoor(LevelRoomAuthoring2D room)
        {
            GameObject doorObject = new GameObject("Door");
            doorObject.transform.SetParent(room.transform, false);
            LevelDoorEndpointAuthoring2D door =
                doorObject.AddComponent<LevelDoorEndpointAuthoring2D>();
            door.ConfigureAuthoring(
                LevelDesignAuthoringId.New("door"),
                room,
                LevelDoorSideV2.North,
                LevelDoorPlacementModeV2.EdgeManaged,
                0.5f,
                Vector2.zero,
                true,
                true);
            door.SnapToPlacement();
            return door;
        }

        private static LevelDoorEndpointAuthoring2D CreateFixedDoor(LevelRoomAuthoring2D room)
        {
            LevelDoorEndpointAuthoring2D door = CreateDoor(room);
            door.ConfigureAuthoring(
                door.DoorIdText,
                room,
                LevelDoorSideV2.North,
                LevelDoorPlacementModeV2.Fixed,
                0.5f,
                Vector2.zero,
                true,
                false);
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
                LevelDesignAuthoringId.New("connection"),
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
