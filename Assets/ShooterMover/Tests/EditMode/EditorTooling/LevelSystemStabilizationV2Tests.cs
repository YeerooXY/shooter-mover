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
    public sealed class LevelSystemStabilizationV2Tests
    {
        private LevelDesignSceneAuthoringRoot2D root;
        private LevelDoorEndpointAuthoring2D configuredFinalExitDoor;
        private string temporaryParent;
        private string outputRoot;

        [SetUp]
        public void SetUp()
        {
            Undo.ClearAll();
            LevelGridV2PlayableExporter.BeforeCommitForTests = null;
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            GameObject rootObject = new GameObject("Level Stabilization Test Root");
            root = rootObject.AddComponent<LevelDesignSceneAuthoringRoot2D>();
            root.ConfigureForTests("level.stabilization-export-test");
            temporaryParent = Path.Combine(
                Path.GetTempPath(),
                "ShooterMover-LevelSystemStabilization-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(temporaryParent);
            outputRoot = Path.Combine(temporaryParent, "PublishedLevel");
        }

        [TearDown]
        public void TearDown()
        {
            LevelGridV2PlayableExporter.BeforeCommitForTests = null;
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
        public void UnchangedSourceAndDestination_CommitCanonicalPlayableExport()
        {
            ConfigurePlayableGraph();

            LevelGridV2PlayableExporter.Export(root, outputRoot);

            Assert.That(File.Exists(Path.Combine(outputRoot, "level.json")), Is.True);
            Assert.That(File.Exists(Path.Combine(outputRoot, "map.json")), Is.True);
            Assert.That(
                File.Exists(Path.Combine(
                    outputRoot,
                    LevelGridPlayableProvenanceV2.FileName)),
                Is.True);
            StringAssert.Contains(
                root.LevelIdText,
                File.ReadAllText(Path.Combine(outputRoot, "level.json")));
        }

        [Test]
        public void SceneMutationDuringStaging_AbortsBeforeReplacingPlayableSource()
        {
            LevelRoomAuthoring2D finalRoom = ConfigurePlayableGraph();
            LevelGridV2PlayableExporter.Export(root, outputRoot);
            string previousLevel = File.ReadAllText(Path.Combine(outputRoot, "level.json"));

            LevelGridV2PlayableExporter.BeforeCommitForTests = delegate
            {
                LevelGridEditorOperationsV2.MoveRoom(
                    finalRoom,
                    new Vector2Int(4, -2));
            };

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                () => LevelGridV2PlayableExporter.Export(root, outputRoot));

            Assert.That(exception.Message, Does.Contain("scene authoring source"));
            Assert.That(exception.Message, Does.Contain(root.LevelIdText));
            Assert.That(
                File.ReadAllText(Path.Combine(outputRoot, "level.json")),
                Is.EqualTo(previousLevel));
            Assert.That(finalRoom.GridCoordinate, Is.EqualTo(new Vector2Int(4, -2)));
        }

        [Test]
        public void DestinationMutationDuringStaging_IsPreservedAndBlocksOverwrite()
        {
            ConfigurePlayableGraph();
            LevelGridV2PlayableExporter.Export(root, outputRoot);
            string previousLevel = File.ReadAllText(Path.Combine(outputRoot, "level.json"));
            string externalMarker = Path.Combine(outputRoot, "external-owner-note.txt");
            File.WriteAllText(externalMarker, "before");

            LevelGridV2PlayableExporter.BeforeCommitForTests = delegate
            {
                File.WriteAllText(externalMarker, "changed externally");
            };

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                () => LevelGridV2PlayableExporter.Export(root, outputRoot));

            Assert.That(exception.Message, Does.Contain(outputRoot));
            Assert.That(exception.Message, Does.Contain("external change was preserved"));
            Assert.That(File.ReadAllText(externalMarker), Is.EqualTo("changed externally"));
            Assert.That(
                File.ReadAllText(Path.Combine(outputRoot, "level.json")),
                Is.EqualTo(previousLevel));
        }

        [Test]
        public void ExactFinalExit_IsAllowedUnconnectedButRejectedAsRoomLink()
        {
            ConfigurePlayableGraph();
            LevelRoomAuthoring2D extraRoom = LevelGridEditorOperationsV2.CreateRoom(
                root,
                new Vector2Int(2, 0));
            LevelDoorEndpointAuthoring2D extraDoor =
                LevelGridEditorOperationsV2.CreateDoor(
                    extraRoom,
                    LevelDoorSideV2.West,
                    0.5f);

            LevelDoorLinkAuthoring2D invalidLink;
            string rejection;
            Assert.That(
                LevelGridEditorOperationsV2.TryCreateConnection(
                    root,
                    configuredFinalExitDoor,
                    extraDoor,
                    out invalidLink,
                    out rejection),
                Is.True,
                rejection);

            LevelGridEditorOperationsV2.Validate(
                root,
                LevelGridValidationPurposeV2.ProductionPublish);

            Assert.That(root.LastGridValidation.CanPublish, Is.False);
            bool foundExactFailure = false;
            for (int index = 0; index < root.LastGridValidation.Problems.Count; index++)
            {
                LevelGridProblemV2 problem = root.LastGridValidation.Problems[index];
                if (problem.AuthoredId == invalidLink.ConnectionIdText
                    && problem.Message.Contains(configuredFinalExitDoor.DoorIdText))
                {
                    foundExactFailure = true;
                    break;
                }
            }
            Assert.That(foundExactFailure, Is.True);
        }

        [Test]
        public void CompatibilitySurfaces_DelegateOrDisableInsteadOfMutatingDirectly()
        {
            string creationMenu = ReadProjectFile(
                "Assets/ShooterMover/Editor/LevelDesign/Foundation/"
                + "LevelGridAuthoringV2CreationMenu.cs");
            string compatibilityEditor = ReadProjectFile(
                "Assets/ShooterMover/Editor/LevelDesign/Foundation/"
                + "LevelGridAuthoringV2Editor.cs");
            string foundationEditor = ReadProjectFile(
                "Assets/ShooterMover/Editor/LevelDesign/Foundation/"
                + "LevelDesignFoundationEditor.cs");
            string legacyGuards = ReadProjectFile(
                "Assets/ShooterMover/Editor/LevelDesign/Foundation/"
                + "LevelGridLegacySurfaceGuardsV2.cs");

            StringAssert.Contains("LevelGridEditorOperationsV2.CreateDoor", creationMenu);
            StringAssert.Contains("LevelGridEditorOperationsV2.TryCreateConnection", creationMenu);
            StringAssert.DoesNotContain("Undo.AddComponent<LevelDoorLinkAuthoring2D>", creationMenu);
            StringAssert.DoesNotContain("new GameObject(\"Door Endpoint\")", creationMenu);

            StringAssert.Contains("LevelGridEditorOperationsV2.DeleteRoom", compatibilityEditor);
            StringAssert.Contains("LevelGridEditorOperationsV2.Validate", compatibilityEditor);
            StringAssert.Contains("LevelGridEditorOperationsV2.IsConnected", compatibilityEditor);
            StringAssert.DoesNotContain("Undo.DestroyObjectImmediate(room.gameObject)",
                compatibilityEditor);
            StringAssert.DoesNotContain("connection.SourceRoom == room", compatibilityEditor);

            StringAssert.Contains("LevelGridEditorOperationsV2.Validate", foundationEditor);
            StringAssert.Contains("LevelGridEditorWindowV2.OpenForRoot", foundationEditor);
            StringAssert.DoesNotContain("Runtime importer: not connected", foundationEditor);

            StringAssert.Contains("DisableThreeRoomStarter", legacyGuards);
            StringAssert.Contains("DisablePhaseOneDraftExport", legacyGuards);
            StringAssert.Contains("DisablePhaseOneValidatedExport", legacyGuards);
            StringAssert.Contains("DisableArbitraryPlayableExport", legacyGuards);
            StringAssert.Contains("DisableTrackedCompilerShortcut", legacyGuards);
            StringAssert.Contains("DisableArbitraryCompilerShortcut", legacyGuards);
        }

        private LevelRoomAuthoring2D ConfigurePlayableGraph()
        {
            LevelRoomAuthoring2D startRoom = LevelGridEditorOperationsV2.CreateRoom(
                root,
                Vector2Int.zero);
            LevelRoomAuthoring2D finalRoom = LevelGridEditorOperationsV2.CreateRoom(
                root,
                Vector2Int.right);
            LevelDoorEndpointAuthoring2D startDoor =
                LevelGridEditorOperationsV2.CreateDoor(
                    startRoom,
                    LevelDoorSideV2.East,
                    0.5f);
            LevelDoorEndpointAuthoring2D finalEntryDoor =
                LevelGridEditorOperationsV2.CreateDoor(
                    finalRoom,
                    LevelDoorSideV2.West,
                    0.5f);
            configuredFinalExitDoor = LevelGridEditorOperationsV2.CreateDoor(
                finalRoom,
                LevelDoorSideV2.East,
                0.5f);

            LevelDoorLinkAuthoring2D link;
            string rejection;
            Assert.That(
                LevelGridEditorOperationsV2.TryCreateConnection(
                    root,
                    startDoor,
                    finalEntryDoor,
                    out link,
                    out rejection),
                Is.True,
                rejection);
            Assert.That(link, Is.Not.Null);

            LevelGridPlayableMetadataV2 metadata =
                LevelGridPlayableMetadataOperationsV2.Add(root);
            LevelGridPlayableMetadataOperationsV2.SetStartRoom(
                root,
                metadata,
                startRoom);
            LevelGridPlayableMetadataOperationsV2.UseDoorAsFinalExit(
                root,
                metadata,
                configuredFinalExitDoor);
            LevelGridEditorOperationsV2.Validate(
                root,
                LevelGridValidationPurposeV2.ProductionPublish);
            Assert.That(root.LastValidation.IsValid, Is.True);
            Assert.That(root.LastGridValidation.CanPublish, Is.True);
            return finalRoom;
        }

        private static string ReadProjectFile(string assetPath)
        {
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            return File.ReadAllText(Path.GetFullPath(Path.Combine(projectRoot, assetPath)));
        }
    }
}
#endif
