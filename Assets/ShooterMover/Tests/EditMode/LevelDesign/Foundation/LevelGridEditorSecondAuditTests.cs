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
    public sealed class LevelGridEditorSecondAuditTests
    {
        [Test]
        public void UndoRedoRefresh_RevalidatesRestoredTopology()
        {
            GameObject rootObject = new GameObject("Root");
            try
            {
                LevelDraft root = CreateRoot(rootObject);
                LevelRoom room = CreateRoom(root.transform, Vector2Int.zero);
                DoorEndpoint door = CreateDoor(room);
                LevelGridEditorOperations.Validate(root, LevelGridValidationPurpose.Draft);
                Assert.That(root.LastGridValidation.UnconnectedTraversableDoorCount, Is.EqualTo(1));

                LevelGridEditorOperations.DeleteDoor(door);
                Assert.That(root.LastGridValidation.UnconnectedTraversableDoorCount, Is.Zero);
                Undo.PerformUndo();
                InvokeLiveValidation("OnUndoRedoPerformed");
                InvokeLiveValidation("RefreshPendingRoots");

                Assert.That(
                    root.GetComponentsInChildren<DoorEndpoint>(true),
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
                LevelDraft root = CreateRoot(rootObject);
                LevelRoom removedRoom = CreateRoom(root.transform, Vector2Int.zero);
                LevelRoom remainingRoom = CreateRoom(root.transform, Vector2Int.right);
                DoorEndpoint removedDoor = CreateDoor(removedRoom);
                DoorEndpoint remainingDoor = CreateDoor(remainingRoom);
                DoorLink malformed = CreateConnection(
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

                Assert.That(LevelGridEditorOperations.DeleteRoom(removedRoom, false), Is.True);
                Assert.That(root.GetComponentsInChildren<DoorLink>(true), Is.Empty);
                Assert.That(
                    root.GetComponentsInChildren<DoorEndpoint>(true),
                    Has.Member(remainingDoor));

                Undo.PerformUndo();
                Assert.That(root.GetComponentsInChildren<LevelRoom>(true), Has.Length.EqualTo(2));
                Assert.That(root.GetComponentsInChildren<DoorLink>(true), Has.Length.EqualTo(1));
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
                LevelDraft root = CreateRoot(rootObject);
                DoorEndpoint door = CreateFixedDoor(
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
                LevelDraft root = CreateRoot(rootObject);
                LevelRoom room = CreateRoom(root.transform, Vector2Int.zero);
                room.transform.position = new Vector3(10f, 20f, 0f);
                GameObject helper = new GameObject("Door Helper");
                helper.transform.SetParent(room.transform, false);
                helper.transform.localPosition = new Vector3(2f, 1f, 0f);
                helper.transform.localRotation = Quaternion.Euler(0f, 0f, 25f);
                GameObject doorObject = new GameObject("Legacy Door");
                doorObject.transform.SetParent(helper.transform, false);
                DoorEndpoint door =
                    doorObject.AddComponent<DoorEndpoint>();
                door.ConfigureAuthoring(
                    LevelDesignAuthoringId.New("door"),
                    room,
                    LevelDoorSide.North,
                    LevelDoorPlacementMode.Fixed,
                    0.5f,
                    Vector2.zero,
                    true,
                    false);
                door.ConfigureLegacyFixedPositionForTests(new Vector2(4f, 5f));
                Vector3 worldBefore = door.transform.position;

                LevelGridEditorOperations.Validate(root, LevelGridValidationPurpose.Draft);

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
                LevelDraft root = CreateRoot(rootObject);
                LevelRoom room = CreateRoom(root.transform, Vector2Int.zero);
                LevelGridEditorOperations.Validate(
                    root,
                    LevelGridValidationPurpose.ProductionPublish);

                CreateDoor(room);
                InvokeLiveValidation("OnHierarchyChanged");
                InvokeLiveValidation("RefreshPendingRoots");

                Assert.That(root.LastGridValidation.UnconnectedTraversableDoorCount, Is.EqualTo(1));
                Assert.That(
                    root.LastGridValidation.Purpose,
                    Is.EqualTo(LevelGridValidationPurpose.ProductionPublish));
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
                LevelDraft root = CreateRoot(rootObject);
                CreateRoom(root.transform, Vector2Int.zero);
                LevelGridEditorOperations.Validate(
                    root,
                    LevelGridValidationPurpose.ProductionPublish);
                LevelGridValidationResult before = root.LastGridValidation;

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
            LevelGridEditorWindow window = null;
            try
            {
                LevelDraft root = CreateRoot(rootObject);
                DoorEndpoint door = CreateDoor(
                    CreateRoom(root.transform, Vector2Int.zero));
                window = ScriptableObject.CreateInstance<LevelGridEditorWindow>();
                window.SetActiveRootForTests(root);
                LevelGridProblem problem = root.LastGridValidation.Problems.First(
                    candidate => candidate.Code
                        == LevelGridProblemCode.UnconnectedTraversableDoor);
                FieldInfo selectedProblem = typeof(LevelGridEditorWindow).GetField(
                    "selectedProblem",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(selectedProblem, Is.Not.Null);
                selectedProblem.SetValue(window, problem);

                LevelGridEditorOperations.UpdateDoor(
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
                directory + "/LevelGridAuthoringLiveValidation.cs");
            StringAssert.Contains("Undo.undoRedoPerformed += OnUndoRedoPerformed", liveValidation);
            StringAssert.Contains("EditorApplication.hierarchyChanged += OnHierarchyChanged", liveValidation);
            StringAssert.DoesNotContain(
                "ObjectChangeEvents.changesPublished",
                File.ReadAllText(directory + "/LevelGridEditorWindow.cs"));

            string endpointSource = File.ReadAllText(
                "Assets/ShooterMover/Runtime/UnityAdapters/Authoring/LevelDesign/"
                    + "DoorEndpoint.cs");
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
            MethodInfo method = typeof(LevelGridAuthoringLiveValidation).GetMethod(
                methodName,
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, methodName);
            return method.Invoke(null, arguments);
        }

        private static LevelDraft CreateRoot(GameObject rootObject)
        {
            LevelDraft root =
                rootObject.AddComponent<LevelDraft>();
            root.ConfigureForTests(LevelDesignAuthoringId.New("level"));
            return root;
        }

        private static LevelRoom CreateRoom(Transform parent, Vector2Int coordinate)
        {
            GameObject roomObject = new GameObject("Room");
            roomObject.transform.SetParent(parent, false);
            BoxCollider2D bounds = roomObject.AddComponent<BoxCollider2D>();
            bounds.size = Vector2.one;
            LevelRoom room = roomObject.AddComponent<LevelRoom>();
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

        private static DoorEndpoint CreateDoor(LevelRoom room)
        {
            GameObject doorObject = new GameObject("Door");
            doorObject.transform.SetParent(room.transform, false);
            DoorEndpoint door =
                doorObject.AddComponent<DoorEndpoint>();
            door.ConfigureAuthoring(
                LevelDesignAuthoringId.New("door"),
                room,
                LevelDoorSide.North,
                LevelDoorPlacementMode.EdgeManaged,
                0.5f,
                Vector2.zero,
                true,
                true);
            door.SnapToPlacement();
            return door;
        }

        private static DoorEndpoint CreateFixedDoor(LevelRoom room)
        {
            DoorEndpoint door = CreateDoor(room);
            door.ConfigureAuthoring(
                door.DoorIdText,
                room,
                LevelDoorSide.North,
                LevelDoorPlacementMode.Fixed,
                0.5f,
                Vector2.zero,
                true,
                false);
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
