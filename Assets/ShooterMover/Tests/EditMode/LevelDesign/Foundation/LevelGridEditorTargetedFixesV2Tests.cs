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
    public sealed class LevelGridEditorTargetedFixesV2Tests
    {
        [Test]
        public void DeleteDoor_RemovesEveryAttachedConnection()
        {
            GameObject rootObject = new GameObject("Root");
            try
            {
                LevelDesignSceneAuthoringRoot2D root = CreateRoot(rootObject);
                LevelRoomAuthoring2D firstRoom = CreateRoom(
                    root.transform,
                    "room.first",
                    Vector2Int.zero,
                    true);
                LevelRoomAuthoring2D secondRoom = CreateRoom(
                    root.transform,
                    "room.second",
                    Vector2Int.right,
                    true);
                LevelRoomAuthoring2D thirdRoom = CreateRoom(
                    root.transform,
                    "room.third",
                    Vector2Int.up,
                    true);
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
                CreateConnection(root.transform, firstRoom, first, thirdRoom, third);

                LevelGridEditorOperationsV2.DeleteDoor(first);

                Assert.That(
                    root.GetComponentsInChildren<LevelDoorLinkAuthoring2D>(true),
                    Is.Empty);
                LevelDoorEndpointAuthoring2D[] remaining =
                    root.GetComponentsInChildren<LevelDoorEndpointAuthoring2D>(true);
                Assert.That(remaining, Has.Member(second));
                Assert.That(remaining, Has.Member(third));
                Assert.That(remaining.Length, Is.EqualTo(2));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        public void LegacyFixedPositionMigration_PreservesWorldPosition()
        {
            GameObject rootObject = new GameObject("Root");
            try
            {
                LevelDesignSceneAuthoringRoot2D root = CreateRoot(rootObject);
                LevelRoomAuthoring2D room = CreateRoom(
                    root.transform,
                    "room.fixed-migration",
                    Vector2Int.zero,
                    true);
                room.transform.position = new Vector3(10f, 20f, 0f);

                GameObject helper = new GameObject("Door Helpers");
                helper.transform.SetParent(room.transform, false);
                helper.transform.localPosition = new Vector3(2f, 3f, 0f);
                helper.transform.localRotation = Quaternion.Euler(0f, 0f, 20f);

                GameObject doorObject = new GameObject("Legacy Fixed Door");
                doorObject.transform.SetParent(helper.transform, false);
                LevelDoorEndpointAuthoring2D door =
                    doorObject.AddComponent<LevelDoorEndpointAuthoring2D>();
                door.ConfigureAuthoring(
                    "door.legacy-fixed",
                    room,
                    LevelDoorSideV2.North,
                    LevelDoorPlacementModeV2.Fixed,
                    0.5f,
                    Vector2.zero,
                    true,
                    false);
                door.ConfigureLegacyFixedPositionForTests(new Vector2(4f, 5f));
                Vector3 worldBefore = door.transform.position;
                Vector3 expectedRoomRelative =
                    room.transform.InverseTransformPoint(worldBefore);

                bool migrated = door.MigrateFixedPositionSpaceForAuthoring();
                door.SnapToPlacement();

                Assert.That(migrated, Is.True);
                Assert.That(door.UsesOwningRoomFixedPositionSpace, Is.True);
                Assert.That(
                    Vector2.Distance(
                        door.FixedLocalPosition,
                        new Vector2(expectedRoomRelative.x, expectedRoomRelative.y)),
                    Is.LessThan(0.001f));
                Assert.That(
                    Vector3.Distance(door.transform.position, worldBefore),
                    Is.LessThan(0.001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        public void SelectingRoot_RunsImmediateDraftValidation()
        {
            GameObject rootObject = new GameObject("Root");
            LevelGridEditorWindowV2 window = null;
            try
            {
                LevelDesignSceneAuthoringRoot2D root = CreateRoot(rootObject);
                LevelRoomAuthoring2D room = CreateRoom(
                    root.transform,
                    LevelDesignAuthoringId.New("room"),
                    Vector2Int.zero,
                    true);
                CreateDoor(
                    room,
                    LevelDesignAuthoringId.New("door"),
                    LevelDoorSideV2.North);
                Assert.That(
                    root.LastGridValidation.UnconnectedTraversableDoorCount,
                    Is.EqualTo(0));

                window = ScriptableObject.CreateInstance<LevelGridEditorWindowV2>();
                window.SetActiveRootForTests(root);

                Assert.That(
                    root.LastGridValidation.Purpose,
                    Is.EqualTo(LevelGridValidationPurposeV2.Draft));
                Assert.That(
                    root.LastGridValidation.UnconnectedTraversableDoorCount,
                    Is.EqualTo(1));
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
        public void Projection_MarksRoomWithFoundationProblem()
        {
            GameObject rootObject = new GameObject("Root");
            try
            {
                LevelDesignSceneAuthoringRoot2D root = CreateRoot(rootObject);
                LevelRoomAuthoring2D room = CreateRoom(
                    root.transform,
                    LevelDesignAuthoringId.New("room"),
                    Vector2Int.zero,
                    false);
                root.ValidateHierarchy();
                root.ValidateGridAuthoring(LevelGridValidationPurposeV2.Draft);

                LevelGridEditorProjectionV2 projection =
                    LevelGridEditorProjectionV2.Build(root);

                Assert.That(projection.Rooms.Count, Is.EqualTo(1));
                Assert.That(projection.Rooms[0].Room, Is.SameAs(room));
                Assert.That(projection.Rooms[0].HasValidationProblem, Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        public void FoundationProblemLocator_UsesStableIdAndDiagnosticPath()
        {
            GameObject rootObject = new GameObject("Root");
            try
            {
                LevelDesignSceneAuthoringRoot2D root = CreateRoot(rootObject);
                GameObject branchA = new GameObject("Branch A");
                branchA.transform.SetParent(root.transform, false);
                GameObject branchB = new GameObject("Branch B");
                branchB.transform.SetParent(root.transform, false);
                string duplicateId = LevelDesignAuthoringId.New("room");
                CreateRoom(
                    branchA.transform,
                    duplicateId,
                    Vector2Int.zero,
                    true);
                LevelRoomAuthoring2D target = CreateRoom(
                    branchB.transform,
                    duplicateId,
                    Vector2Int.right,
                    true);

                LevelDesignValidationResult validation = root.ValidateHierarchy();
                LevelDesignValidationIssue issue = validation.Issues.First(
                    candidate =>
                        candidate.Code
                            == LevelDesignValidationCode.DuplicateAuthoredIdentity
                        && candidate.DiagnosticLocation.Contains("Branch B"));

                Component selected =
                    LevelGridEditorProblemLocatorV2.FindExact(root, issue);

                Assert.That(selected, Is.SameAs(target));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        public void EditorSourceGuard_ScansEveryLevelGridEditorPartial()
        {
            string directory =
                "Assets/ShooterMover/Editor/LevelDesign/Foundation";
            string combined = string.Join(
                "\n",
                Directory.GetFiles(directory, "LevelGridEditor*.cs")
                    .OrderBy(path => path)
                    .Select(File.ReadAllText));

            StringAssert.Contains("LevelDesignSceneAuthoringRoot2D", combined);
            StringAssert.Contains("LevelRoomAuthoring2D", combined);
            StringAssert.Contains("LevelDoorEndpointAuthoring2D", combined);
            StringAssert.Contains("LevelDoorLinkAuthoring2D", combined);
            StringAssert.DoesNotContain("JsonUtility", combined);
            StringAssert.DoesNotContain("File.WriteAllText", combined);
            StringAssert.DoesNotContain("ScriptableObject.CreateInstance", combined);
            StringAssert.DoesNotContain("RoomContentJsonImporterV1", combined);
        }

        private static LevelDesignSceneAuthoringRoot2D CreateRoot(
            GameObject rootObject)
        {
            LevelDesignSceneAuthoringRoot2D root =
                rootObject.AddComponent<LevelDesignSceneAuthoringRoot2D>();
            root.ConfigureForTests(LevelDesignAuthoringId.New("level"));
            return root;
        }

        private static LevelRoomAuthoring2D CreateRoom(
            Transform parent,
            string roomId,
            Vector2Int coordinate,
            bool includeBounds)
        {
            GameObject roomObject = new GameObject(roomId);
            roomObject.transform.SetParent(parent, false);
            BoxCollider2D bounds = includeBounds
                ? roomObject.AddComponent<BoxCollider2D>()
                : null;
            if (bounds != null)
            {
                bounds.size = Vector2.one;
            }
            LevelRoomAuthoring2D room =
                roomObject.AddComponent<LevelRoomAuthoring2D>();
            room.ConfigureForTests(
                roomId,
                coordinate,
                Vector2.one,
                Vector2Int.one,
                bounds);
            room.ConfigureFolderSlotForTests(1);
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
