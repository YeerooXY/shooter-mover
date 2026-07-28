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
    [NonParallelizable]
    public sealed class LevelDoorAuthorityV2Tests
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
            ForceWrongSide(graph.SourceDoor, LevelDoorSideV2.North);
            LevelDoorSideV2 originalSide = graph.SourceDoor.Side;
            Vector3 originalPosition = graph.SourceDoor.transform.localPosition;

            int mismatches = LevelGridDoorOperationsV2.ReflowAll(root);
            LevelGridAuthoringV2LiveValidation.ValidateNow(
                root,
                LevelGridValidationPurposeV2.ProductionPublish,
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
                    LevelGridProblemCodeV2.EdgeManagedDoorFacingMismatch,
                    graph.SourceDoor.DoorIdText),
                Is.True);
        }

        [Test]
        public void ExplicitCanonicalReflow_MovesDoorAndUndoRestoresAuthoredMismatch()
        {
            DoorGraph graph = ConfigurePlayableGraph();
            ForceWrongSide(graph.SourceDoor, LevelDoorSideV2.North);
            Vector3 authoredPosition = graph.SourceDoor.transform.localPosition;
            Undo.ClearAll();

            LevelGridEditorOperationsV2.ReflowDoor(graph.SourceDoor);

            Assert.That(graph.SourceDoor.Side, Is.EqualTo(LevelDoorSideV2.East));
            Assert.That(
                graph.SourceDoor.transform.localPosition,
                Is.Not.EqualTo(authoredPosition));

            Undo.PerformUndo();

            Assert.That(graph.SourceDoor.Side, Is.EqualTo(LevelDoorSideV2.North));
            Assert.That(
                graph.SourceDoor.transform.localPosition,
                Is.EqualTo(authoredPosition));
        }

        [Test]
        public void BuildWithMisalignedDoor_FailsWithoutChangingSceneOrPublishingSource()
        {
            DoorGraph graph = ConfigurePlayableGraph();
            ForceWrongSide(graph.SourceDoor, LevelDoorSideV2.North);
            LevelDoorSideV2 originalSide = graph.SourceDoor.Side;
            Vector3 originalPosition = graph.SourceDoor.transform.localPosition;

            Assert.Throws<InvalidOperationException>(
                () => LevelGridV2PlayableExporter.Export(root, outputRoot));

            Assert.That(graph.SourceDoor.Side, Is.EqualTo(originalSide));
            Assert.That(
                graph.SourceDoor.transform.localPosition,
                Is.EqualTo(originalPosition));
            Assert.That(Directory.Exists(outputRoot), Is.False);
        }

        [Test]
        public void LiveValidation_DoesNotMigrateLegacyFixedDoorState()
        {
            LevelRoomAuthoring2D room = LevelGridEditorOperationsV2.CreateRoom(
                root,
                Vector2Int.zero);
            LevelDoorEndpointAuthoring2D door = LevelGridEditorOperationsV2.CreateDoor(
                room,
                LevelDoorSideV2.North,
                0.5f);
            door.ConfigureLegacyFixedPositionForTests(new Vector2(2f, 3f));
            Vector3 originalPosition = door.transform.localPosition;
            Assert.That(door.UsesOwningRoomFixedPositionSpace, Is.False);

            LevelGridAuthoringV2LiveValidation.ValidateNow(
                root,
                LevelGridValidationPurposeV2.Draft,
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
                + "LevelGridDoorOperationsV2.cs");
            string liveValidation = ReadProjectFile(
                "Assets/ShooterMover/Editor/LevelDesign/Foundation/"
                + "LevelGridAuthoringV2LiveValidation.cs");

            StringAssert.DoesNotContain("[MenuItem(", doorUtility);
            StringAssert.DoesNotContain("DeleteSelectedDoor", doorUtility);
            StringAssert.DoesNotContain("ReflowSelectedDoor", doorUtility);
            StringAssert.DoesNotContain("CaptureSelectedDoorAsFixed", doorUtility);
            StringAssert.Contains("CountDoorsNeedingReflow", doorUtility);
            StringAssert.DoesNotContain("MigrateLegacyFixedDoorPositions", liveValidation);
            StringAssert.DoesNotContain(
                "LevelGridDoorOperationsV2.ReflowAll(root)",
                liveValidation);
        }

        private DoorGraph ConfigurePlayableGraph()
        {
            LevelRoomAuthoring2D startRoom = LevelGridEditorOperationsV2.CreateRoom(
                root,
                Vector2Int.zero);
            LevelRoomAuthoring2D finalRoom = LevelGridEditorOperationsV2.CreateRoom(
                root,
                Vector2Int.right);
            LevelDoorEndpointAuthoring2D sourceDoor =
                LevelGridEditorOperationsV2.CreateDoor(
                    startRoom,
                    LevelDoorSideV2.East,
                    0.5f);
            LevelDoorEndpointAuthoring2D destinationDoor =
                LevelGridEditorOperationsV2.CreateDoor(
                    finalRoom,
                    LevelDoorSideV2.West,
                    0.5f);
            LevelDoorEndpointAuthoring2D finalExitDoor =
                LevelGridEditorOperationsV2.CreateDoor(
                    finalRoom,
                    LevelDoorSideV2.East,
                    0.5f);

            LevelDoorLinkAuthoring2D link;
            string rejection;
            Assert.That(
                LevelGridEditorOperationsV2.TryCreateConnection(
                    root,
                    sourceDoor,
                    destinationDoor,
                    out link,
                    out rejection),
                Is.True,
                rejection);

            LevelGridPlayableMetadataV2 metadata =
                LevelGridPlayableMetadataOperationsV2.Add(root);
            LevelGridPlayableMetadataOperationsV2.SetStartRoom(
                root,
                metadata,
                startRoom);
            LevelGridPlayableMetadataOperationsV2.UseDoorAsFinalExit(
                root,
                metadata,
                finalExitDoor);

            return new DoorGraph(sourceDoor, destinationDoor, finalExitDoor);
        }

        private static void ForceWrongSide(
            LevelDoorEndpointAuthoring2D door,
            LevelDoorSideV2 side)
        {
            door.ConfigureAuthoring(
                door.DoorIdText,
                door.OwningRoom,
                side,
                LevelDoorPlacementModeV2.EdgeManaged,
                door.EdgeOffset,
                door.FixedLocalPosition,
                door.Traversable,
                true);
            door.SnapToPlacement();
            EditorUtility.SetDirty(door);
        }

        private static bool ContainsProblem(
            LevelGridValidationResultV2 result,
            LevelGridProblemCodeV2 code,
            string authoredId)
        {
            for (int index = 0; index < result.Problems.Count; index++)
            {
                LevelGridProblemV2 problem = result.Problems[index];
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
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
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
