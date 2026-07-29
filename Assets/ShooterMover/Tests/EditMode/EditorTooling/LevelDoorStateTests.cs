#if UNITY_EDITOR
using System;
using System.IO;
using NUnit.Framework;
using ShooterMover.Editor.LevelDesign.Foundation;
using ShooterMover.UnityAdapters.Authoring.LevelDesign;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ShooterMover.Tests.EditorTooling.LevelDesign.Foundation
{
    public sealed class LevelDoorStateTests
    {
        private LevelDesignSceneAuthoringRoot2D root;
        private string temporaryParent;
        private string outputRoot;

        [SetUp]
        public void SetUp()
        {
            Undo.ClearAll();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            GameObject rootObject = new GameObject("Door Authority Test Root");
            root = rootObject.AddComponent<LevelDesignSceneAuthoringRoot2D>();
            root.ConfigureForTests("level.door-authority-test");
            temporaryParent = Path.Combine(
                Path.GetTempPath(),
                "ShooterMover-LevelDoorAuthority-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(temporaryParent);
            outputRoot = Path.Combine(temporaryParent, "PublishedLevel");
        }

        [TearDown]
        public void TearDown()
        {
            Undo.ClearAll();
            if (root != null)
            {
                UnityEngine.Object.DestroyImmediate(root.gameObject);
            }
            if (Directory.Exists(temporaryParent))
            {
                Directory.Delete(temporaryParent, true);
            }
        }

        [Test]
        public void ValidationAndBulkInspection_ReportMismatchWithoutMovingDoor()
        {
            DoorGraph graph = ConfigurePlayableGraph();
            ForceWrongSide(graph.SourceDoor, LevelDoorSide.North);
            LevelDoorSide originalSide = graph.SourceDoor.Side;
            Vector3 originalPosition = graph.SourceDoor.transform.localPosition;

            int mismatches = LevelGridDoorOperations.ReflowAll(root);
            LevelGridAuthoringLiveValidation.ValidateNow(
                root,
                LevelGridValidationPurpose.ProductionPublish,
                true,
                false);

            Assert.That(mismatches, Is.GreaterThanOrEqualTo(1));
            Assert.That(graph.SourceDoor.Side, Is.EqualTo(originalSide));
            Assert.That(
                graph.SourceDoor.transform.localPosition,
                Is.EqualTo(originalPosition));
            Assert.That(
                ContainsProblem(
                    root.LastGridValidation,
                    LevelGridProblemCode.EdgeManagedDoorFacingMismatch,
                    graph.SourceDoor.DoorIdText),
                Is.True);
        }

        [Test]
        public void ExplicitCanonicalReflow_MovesDoorAndUndoRestoresAuthoredMismatch()
        {
            DoorGraph graph = ConfigurePlayableGraph();
            ForceWrongSide(graph.SourceDoor, LevelDoorSide.North);
            Vector3 authoredPosition = graph.SourceDoor.transform.localPosition;
            Undo.ClearAll();

            LevelGridEditorOperations.ReflowDoor(graph.SourceDoor);

            Assert.That(graph.SourceDoor.Side, Is.EqualTo(LevelDoorSide.East));
            Assert.That(
                graph.SourceDoor.transform.localPosition,
                Is.Not.EqualTo(authoredPosition));

            Undo.PerformUndo();

            Assert.That(graph.SourceDoor.Side, Is.EqualTo(LevelDoorSide.North));
            Assert.That(
                graph.SourceDoor.transform.localPosition,
                Is.EqualTo(authoredPosition));
        }

        [Test]
        public void BuildWithMisalignedDoor_FailsWithoutChangingSceneOrPublishingSource()
        {
            DoorGraph graph = ConfigurePlayableGraph();
            ForceWrongSide(graph.SourceDoor, LevelDoorSide.North);
            LevelDoorSide originalSide = graph.SourceDoor.Side;
            Vector3 originalPosition = graph.SourceDoor.transform.localPosition;

            Assert.Throws<InvalidOperationException>(
                () => LevelGridPlayableExporter.Export(root, outputRoot));

            Assert.That(graph.SourceDoor.Side, Is.EqualTo(originalSide));
            Assert.That(
                graph.SourceDoor.transform.localPosition,
                Is.EqualTo(originalPosition));
            Assert.That(Directory.Exists(outputRoot), Is.False);
        }

        [Test]
        public void LiveValidation_DoesNotMigrateLegacyFixedDoorState()
        {
            LevelRoomAuthoring2D room = LevelGridEditorOperations.CreateRoom(
                root,
                Vector2Int.zero);
            LevelDoorEndpointAuthoring2D door = LevelGridEditorOperations.CreateDoor(
                room,
                LevelDoorSide.North,
                0.5f);
            door.ConfigureLegacyFixedPositionForTests(new Vector2(2f, 3f));
            Vector3 originalPosition = door.transform.localPosition;
            Assert.That(door.UsesOwningRoomFixedPositionSpace, Is.False);

            LevelGridAuthoringLiveValidation.ValidateNow(
                root,
                LevelGridValidationPurpose.Draft,
                true,
                false);

            Assert.That(door.UsesOwningRoomFixedPositionSpace, Is.False);
            Assert.That(door.transform.localPosition, Is.EqualTo(originalPosition));
        }

        [Test]
        public void DoorUtilityAndLiveValidation_ExposeNoAlternateMutationSurface()
        {
            string doorUtility = ReadProjectFile(
                "Assets/ShooterMover/Editor/LevelDesign/Foundation/"
                + "LevelGridDoorOperations.cs");
            string liveValidation = ReadProjectFile(
                "Assets/ShooterMover/Editor/LevelDesign/Foundation/"
                + "LevelGridAuthoringLiveValidation.cs");
            string canonicalPanels = ReadProjectFile(
                "Assets/ShooterMover/Editor/LevelDesign/Foundation/"
                + "LevelGridEditorWindow.Panels.cs");

            StringAssert.DoesNotContain("[MenuItem(", doorUtility);
            StringAssert.DoesNotContain("DeleteSelectedDoor", doorUtility);
            StringAssert.DoesNotContain("ReflowSelectedDoor", doorUtility);
            StringAssert.DoesNotContain("CaptureSelectedDoorAsFixed", doorUtility);
            StringAssert.Contains("CountDoorsNeedingReflow", doorUtility);
            StringAssert.DoesNotContain("MigrateLegacyFixedDoorPositions", liveValidation);
            StringAssert.DoesNotContain(
                "LevelGridDoorOperations.ReflowAll(root)",
                liveValidation);

            StringAssert.Contains(
                "LevelGridEditorOperations.ReflowDoor(door)",
                canonicalPanels);
            StringAssert.Contains(
                "LevelGridEditorOperations.KeepDoorPlacement(door)",
                canonicalPanels);
            StringAssert.Contains(
                "LevelGridEditorOperations.DeleteDoor(door)",
                canonicalPanels);
        }

        private DoorGraph ConfigurePlayableGraph()
        {
            LevelRoomAuthoring2D startRoom = LevelGridEditorOperations.CreateRoom(
                root,
                Vector2Int.zero);
            LevelRoomAuthoring2D finalRoom = LevelGridEditorOperations.CreateRoom(
                root,
                Vector2Int.right);
            LevelDoorEndpointAuthoring2D sourceDoor =
                LevelGridEditorOperations.CreateDoor(
                    startRoom,
                    LevelDoorSide.East,
                    0.5f);
            LevelDoorEndpointAuthoring2D destinationDoor =
                LevelGridEditorOperations.CreateDoor(
                    finalRoom,
                    LevelDoorSide.West,
                    0.5f);
            LevelDoorEndpointAuthoring2D finalExitDoor =
                LevelGridEditorOperations.CreateDoor(
                    finalRoom,
                    LevelDoorSide.East,
                    0.5f);

            LevelDoorLinkAuthoring2D link;
            string rejection;
            Assert.That(
                LevelGridEditorOperations.TryCreateConnection(
                    root,
                    sourceDoor,
                    destinationDoor,
                    out link,
                    out rejection),
                Is.True,
                rejection);

            LevelGridPlayableMetadata metadata =
                LevelGridPlayableMetadataOperations.Add(root);
            LevelGridPlayableMetadataOperations.SetStartRoom(
                root,
                metadata,
                startRoom);
            LevelGridPlayableMetadataOperations.UseDoorAsFinalExit(
                root,
                metadata,
                finalExitDoor);

            return new DoorGraph(sourceDoor, destinationDoor, finalExitDoor);
        }

        private static void ForceWrongSide(
            LevelDoorEndpointAuthoring2D door,
            LevelDoorSide side)
        {
            door.ConfigureAuthoring(
                door.DoorIdText,
                door.OwningRoom,
                side,
                LevelDoorPlacementMode.EdgeManaged,
                door.EdgeOffset,
                door.FixedLocalPosition,
                door.Traversable,
                true);
            door.SnapToPlacement();
            EditorUtility.SetDirty(door);
        }

        private static bool ContainsProblem(
            LevelGridValidationResult result,
            LevelGridProblemCode code,
            string authoredId)
        {
            for (int index = 0; index < result.Problems.Count; index++)
            {
                LevelGridProblem problem = result.Problems[index];
                if (problem.Code == code
                    && string.Equals(problem.AuthoredId, authoredId, StringComparison.Ordinal))
                {
                    return true;
                }
            }
            return false;
        }

        private static string ReadProjectFile(string assetPath)
        {
            string projectRoot = Directory.GetParent(UnityEngine.Application.dataPath).FullName;
            return File.ReadAllText(Path.GetFullPath(Path.Combine(projectRoot, assetPath)));
        }

        private sealed class DoorGraph
        {
            public DoorGraph(
                LevelDoorEndpointAuthoring2D sourceDoor,
                LevelDoorEndpointAuthoring2D destinationDoor,
                LevelDoorEndpointAuthoring2D finalExitDoor)
            {
                SourceDoor = sourceDoor;
                DestinationDoor = destinationDoor;
                FinalExitDoor = finalExitDoor;
            }

            public LevelDoorEndpointAuthoring2D SourceDoor { get; private set; }
            public LevelDoorEndpointAuthoring2D DestinationDoor { get; private set; }
            public LevelDoorEndpointAuthoring2D FinalExitDoor { get; private set; }
        }
    }
}
#endif
