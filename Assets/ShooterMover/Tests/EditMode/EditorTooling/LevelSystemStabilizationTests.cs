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
    public sealed class LevelSystemStabilizationTests
    {
        private LevelDesignSceneAuthoringRoot2D root;
        private LevelDoorEndpointAuthoring2D configuredFinalExitDoor;
        private string temporaryParent;
        private string outputRoot;

        [SetUp]
        public void SetUp()
        {
            Undo.ClearAll();
            LevelGridPlayableExporter.BeforeCommitForTests = null;
            LevelGridPlayableExporter.AfterBackupMoveForTests = null;
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
            LevelGridPlayableExporter.BeforeCommitForTests = null;
            LevelGridPlayableExporter.AfterBackupMoveForTests = null;
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

            LevelGridPlayableExporter.Export(root, outputRoot);

            Assert.That(File.Exists(Path.Combine(outputRoot, "level.json")), Is.True);
            Assert.That(File.Exists(Path.Combine(outputRoot, "map.json")), Is.True);
            Assert.That(
                File.Exists(Path.Combine(
                    outputRoot,
                    LevelGridPlayableProvenance.FileName)),
                Is.True);
            StringAssert.Contains(
                root.LevelIdText,
                File.ReadAllText(Path.Combine(outputRoot, "level.json")));
        }

        [Test]
        public void SceneMutationDuringStaging_AbortsBeforeReplacingPlayableSource()
        {
            LevelRoomAuthoring2D finalRoom = ConfigurePlayableGraph();
            LevelGridPlayableExporter.Export(root, outputRoot);
            string previousLevel = File.ReadAllText(Path.Combine(outputRoot, "level.json"));

            LevelGridPlayableExporter.BeforeCommitForTests = delegate
            {
                LevelGridEditorOperations.MoveRoom(
                    finalRoom,
                    new Vector2Int(4, -2));
            };

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                () => LevelGridPlayableExporter.Export(root, outputRoot));

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
            LevelGridPlayableExporter.Export(root, outputRoot);
            string previousLevel = File.ReadAllText(Path.Combine(outputRoot, "level.json"));
            string externalMarker = Path.Combine(outputRoot, "external-owner-note.txt");
            File.WriteAllText(externalMarker, "before");

            LevelGridPlayableExporter.BeforeCommitForTests = delegate
            {
                File.WriteAllText(externalMarker, "changed externally");
            };

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                () => LevelGridPlayableExporter.Export(root, outputRoot));

            Assert.That(exception.Message, Does.Contain(outputRoot));
            Assert.That(exception.Message, Does.Contain("external change was preserved"));
            Assert.That(File.ReadAllText(externalMarker), Is.EqualTo("changed externally"));
            Assert.That(
                File.ReadAllText(Path.Combine(outputRoot, "level.json")),
                Is.EqualTo(previousLevel));
        }

        [Test]
        public void FailureAfterBackupMove_RestoresPreviousExactDestination()
        {
            ConfigurePlayableGraph();
            LevelGridPlayableExporter.Export(root, outputRoot);
            string previousLevel = File.ReadAllText(Path.Combine(outputRoot, "level.json"));

            LevelGridPlayableExporter.AfterBackupMoveForTests = delegate
            {
                throw new IOException("Injected failure after backup move.");
            };

            IOException exception = Assert.Throws<IOException>(
                () => LevelGridPlayableExporter.Export(root, outputRoot));

            Assert.That(exception.Message, Does.Contain("Injected failure"));
            Assert.That(Directory.Exists(outputRoot), Is.True);
            Assert.That(
                File.ReadAllText(Path.Combine(outputRoot, "level.json")),
                Is.EqualTo(previousLevel));
            Assert.That(
                Directory.GetDirectories(
                    temporaryParent,
                    ".PublishedLevel.playable-backup-*",
                    SearchOption.TopDirectoryOnly),
                Is.Empty);
        }

        [Test]
        public void ExactFinalExit_IsAllowedUnconnectedAndRejectedBeforeRoomLinkMutation()
        {
            ConfigurePlayableGraph();
            LevelDoorEndpointAuthoring2D connectedDoor =
                FindConnectedDoorExcludingFinalExit();
            int initialLinkCount =
                root.GetComponentsInChildren<LevelDoorLinkAuthoring2D>(true).Length;

            AssertFinalExitConnectionRejected(configuredFinalExitDoor, connectedDoor);
            AssertFinalExitConnectionRejected(connectedDoor, configuredFinalExitDoor);

            Assert.That(
                root.GetComponentsInChildren<LevelDoorLinkAuthoring2D>(true).Length,
                Is.EqualTo(initialLinkCount));
            LevelGridEditorOperations.Validate(
                root,
                LevelGridValidationPurpose.ProductionPublish);
            Assert.That(root.LastGridValidation.CanPublish, Is.True);
        }

        [Test]
        public void ConnectedEndpoint_CannotBecomeFinalExit()
        {
            ConfigurePlayableGraph();
            LevelGridPlayableMetadata metadata =
                root.GetComponent<LevelGridPlayableMetadata>();
            LevelDoorEndpointAuthoring2D connectedDoor =
                FindConnectedDoorExcludingFinalExit(metadata.FinalExitRoom);

            InvalidOperationException setException =
                Assert.Throws<InvalidOperationException>(
                    () => LevelGridPlayableMetadataOperations.SetFinalDoor(
                        root,
                        metadata,
                        connectedDoor));
            Assert.That(setException.Message, Does.Contain("connected room endpoint"));
            Assert.That(metadata.FinalExitDoor, Is.SameAs(configuredFinalExitDoor));

            InvalidOperationException useException =
                Assert.Throws<InvalidOperationException>(
                    () => LevelGridPlayableMetadataOperations.UseDoorAsFinalExit(
                        root,
                        metadata,
                        connectedDoor));
            Assert.That(useException.Message, Does.Contain("connected room endpoint"));
            Assert.That(metadata.FinalExitRoom, Is.SameAs(configuredFinalExitDoor.OwningRoom));
            Assert.That(metadata.FinalExitDoor, Is.SameAs(configuredFinalExitDoor));
        }

        [Test]
        public void FinalExitDoorOutsideExactRoot_IsRejectedEvenWhenItClaimsFinalRoom()
        {
            ConfigurePlayableGraph();
            LevelGridPlayableMetadata metadata =
                root.GetComponent<LevelGridPlayableMetadata>();
            GameObject rogueObject = new GameObject("Rogue Final Door");
            try
            {
                LevelDoorEndpointAuthoring2D rogueDoor =
                    rogueObject.AddComponent<LevelDoorEndpointAuthoring2D>();
                rogueDoor.ConfigureForTests(
                    "door.rogue-final",
                    metadata.FinalExitRoom,
                    LevelDoorSide.East,
                    LevelDoorPlacementMode.EdgeManaged,
                    0.5f,
                    Vector2.zero,
                    true);
                metadata.ConfigureForTests(
                    metadata.StartRoom,
                    metadata.PlayerStartLocalPosition,
                    metadata.PlayerStartRotation,
                    metadata.FinalExitRoom,
                    rogueDoor,
                    metadata.RuntimeDoorObjectId);

                InvalidOperationException exception =
                    Assert.Throws<InvalidOperationException>(
                        () => metadata.ValidateForPlayableExport(root));

                Assert.That(exception.Message, Does.Contain("this level root"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rogueObject);
            }
        }

        [Test]
        public void SnapSelected_PlacementChildUsesPlacementGridInsteadOfRoomRedirect()
        {
            LevelRoomAuthoring2D room = LevelGridEditorOperations.CreateRoom(
                root,
                Vector2Int.zero);
            GameObject placementObject = new GameObject("Placement");
            placementObject.transform.SetParent(room.transform, false);
            LevelPlacementAuthoring2D placement =
                placementObject.AddComponent<LevelPlacementAuthoring2D>();
            placement.ConfigureForTests(
                "spawn.placement-test",
                "socket.placement-test",
                LevelPlacementKind.EnemySpawn,
                room,
                null,
                null,
                placementObject.transform,
                null,
                LevelCollisionPolicy.TriggerOnly,
                string.Empty);
            placement.transform.position = new Vector3(123f, 456f, 0f);

            Assert.That(
                LevelDesignFoundationMenu.TrySnapSelectedPlacement(placementObject),
                Is.True);

            Assert.That(placement.transform.position, Is.EqualTo(room.transform.position));
        }

        [Test]
        public void CompatibilitySurfaces_DelegateOrDisableInsteadOfMutatingDirectly()
        {
            string creationMenu = ReadProjectFile(
                "Assets/ShooterMover/Editor/LevelDesign/Foundation/"
                + "LevelGridAuthoringCreationMenu.cs");
            string compatibilityEditor = ReadProjectFile(
                "Assets/ShooterMover/Editor/LevelDesign/Foundation/"
                + "LevelGridAuthoringV2Editor.cs");
            string foundationEditor = ReadProjectFile(
                "Assets/ShooterMover/Editor/LevelDesign/Foundation/"
                + "LevelDesignFoundationEditor.cs");
            string playableEditor = ReadProjectFile(
                "Assets/ShooterMover/Editor/LevelDesign/Foundation/"
                + "LevelGridEditorWindow.Playable.cs");
            string exporter = ReadProjectFile(
                "Assets/ShooterMover/Editor/LevelDesign/Foundation/"
                + "LevelGridPlayableExporter.cs");
            string legacyGuards = ReadProjectFile(
                "Assets/ShooterMover/Editor/LevelDesign/Foundation/"
                + "LevelGridLegacySurfaceGuards.cs");
            string rootAuthoring = ReadProjectFile(
                "Assets/ShooterMover/Runtime/UnityAdapters/Authoring/LevelDesign/"
                + "LevelDesignSceneAuthoringRoot2D.cs");
            string roomAuthoring = ReadProjectFile(
                "Assets/ShooterMover/Runtime/UnityAdapters/Authoring/LevelDesign/"
                + "LevelRoomAuthoring2D.cs");
            string doorAuthoring = ReadProjectFile(
                "Assets/ShooterMover/Runtime/UnityAdapters/Authoring/LevelDesign/"
                + "LevelDoorEndpointAuthoring2D.cs");
            string linkAuthoring = ReadProjectFile(
                "Assets/ShooterMover/Runtime/UnityAdapters/Authoring/LevelDesign/"
                + "LevelDoorLinkAuthoring2D.cs");
            string metadata = ReadProjectFile(
                "Assets/ShooterMover/Runtime/UnityAdapters/Authoring/LevelDesign/"
                + "LevelGridPlayableMetadata.cs");

            StringAssert.Contains("LevelGridEditorOperations.CreateDoor", creationMenu);
            StringAssert.Contains("LevelGridEditorOperations.TryCreateConnection", creationMenu);
            StringAssert.DoesNotContain("Undo.AddComponent<LevelDoorLinkAuthoring2D>", creationMenu);
            StringAssert.DoesNotContain("new GameObject(\"Door Endpoint\")", creationMenu);

            StringAssert.Contains("LevelGridEditorOperations.DeleteRoom", compatibilityEditor);
            StringAssert.Contains("LevelGridEditorOperations.Validate", compatibilityEditor);
            StringAssert.Contains("LevelGridEditorOperations.IsConnected", compatibilityEditor);
            StringAssert.Contains("IsExactFinalExit", compatibilityEditor);
            StringAssert.DoesNotContain("Undo.DestroyObjectImmediate(room.gameObject)",
                compatibilityEditor);
            StringAssert.DoesNotContain("connection.SourceRoom == room", compatibilityEditor);

            StringAssert.Contains("LevelGridEditorOperations.Validate", foundationEditor);
            StringAssert.Contains("LevelGridEditorWindow.OpenForRoot", foundationEditor);
            StringAssert.Contains("TrySnapSelectedPlacement", foundationEditor);
            StringAssert.Contains("productionValidationRun", foundationEditor);
            StringAssert.DoesNotContain("Runtime importer: not connected", foundationEditor);

            StringAssert.Contains("productionValidationRun", playableEditor);
            StringAssert.DoesNotContain("Legacy/", playableEditor);
            StringAssert.DoesNotContain("Create Three-Room Starter Example", playableEditor);
            StringAssert.DoesNotContain("Export Grid V2 Draft Folder", playableEditor);
            StringAssert.DoesNotContain("Publish Grid V2 Validated Authoring Folder", playableEditor);

            StringAssert.Contains("AfterBackupMoveForTests", exporter);
            StringAssert.Contains("TryRestoreBackup", exporter);
            StringAssert.Contains("StringComparer.Ordinal", exporter);
            StringAssert.DoesNotContain("StringComparer.OrdinalIgnoreCase", exporter);

            StringAssert.DoesNotContain("[ContextMenu(", rootAuthoring);
            StringAssert.DoesNotContain("[ContextMenu(", roomAuthoring);
            StringAssert.DoesNotContain("[ContextMenu(", doorAuthoring);
            StringAssert.DoesNotContain("[ContextMenu(", linkAuthoring);
            StringAssert.Contains(
                "finalExitDoor.GetComponentInParent<LevelDesignSceneAuthoringRoot2D>() != root",
                metadata);

            StringAssert.Contains("DisableThreeRoomStarter", legacyGuards);
            StringAssert.Contains("DisablePhaseOneDraftExport", legacyGuards);
            StringAssert.Contains("DisablePhaseOneValidatedExport", legacyGuards);
            StringAssert.Contains("DisableArbitraryPlayableExport", legacyGuards);
            StringAssert.Contains("DisableTrackedCompilerShortcut", legacyGuards);
            StringAssert.Contains("DisableArbitraryCompilerShortcut", legacyGuards);
        }

        private LevelRoomAuthoring2D ConfigurePlayableGraph()
        {
            LevelRoomAuthoring2D startRoom = LevelGridEditorOperations.CreateRoom(
                root,
                Vector2Int.zero);
            LevelRoomAuthoring2D finalRoom = LevelGridEditorOperations.CreateRoom(
                root,
                Vector2Int.right);
            LevelDoorEndpointAuthoring2D startDoor =
                LevelGridEditorOperations.CreateDoor(
                    startRoom,
                    LevelDoorSide.East,
                    0.5f);
            LevelDoorEndpointAuthoring2D finalEntryDoor =
                LevelGridEditorOperations.CreateDoor(
                    finalRoom,
                    LevelDoorSide.West,
                    0.5f);
            configuredFinalExitDoor = LevelGridEditorOperations.CreateDoor(
                finalRoom,
                LevelDoorSide.East,
                0.5f);

            LevelDoorLinkAuthoring2D link;
            string rejection;
            Assert.That(
                LevelGridEditorOperations.TryCreateConnection(
                    root,
                    startDoor,
                    finalEntryDoor,
                    out link,
                    out rejection),
                Is.True,
                rejection);
            Assert.That(link, Is.Not.Null);

            LevelGridPlayableMetadata metadata =
                LevelGridPlayableMetadataOperations.Add(root);
            LevelGridPlayableMetadataOperations.SetStartRoom(
                root,
                metadata,
                startRoom);
            LevelGridPlayableMetadataOperations.UseDoorAsFinalExit(
                root,
                metadata,
                configuredFinalExitDoor);
            LevelGridEditorOperations.Validate(
                root,
                LevelGridValidationPurpose.ProductionPublish);
            Assert.That(root.LastValidation.IsValid, Is.True);
            Assert.That(root.LastGridValidation.CanPublish, Is.True);
            return finalRoom;
        }

        private LevelDoorEndpointAuthoring2D FindConnectedDoorExcludingFinalExit(
            LevelRoomAuthoring2D requiredRoom = null)
        {
            LevelDoorEndpointAuthoring2D[] doors =
                root.GetComponentsInChildren<LevelDoorEndpointAuthoring2D>(true);
            for (int index = 0; index < doors.Length; index++)
            {
                if (doors[index] != configuredFinalExitDoor
                    && (requiredRoom == null || doors[index].OwningRoom == requiredRoom)
                    && LevelGridEditorOperations.IsConnected(root, doors[index]))
                {
                    return doors[index];
                }
            }

            Assert.Fail("Expected at least one matching connected non-final endpoint.");
            return null;
        }

        private void AssertFinalExitConnectionRejected(
            LevelDoorEndpointAuthoring2D source,
            LevelDoorEndpointAuthoring2D destination)
        {
            LevelDoorLinkAuthoring2D rejectedLink;
            string rejection;
            Assert.That(
                LevelGridEditorOperations.TryCreateConnection(
                    root,
                    source,
                    destination,
                    out rejectedLink,
                    out rejection),
                Is.False);
            Assert.That(rejectedLink, Is.Null);
            Assert.That(rejection, Does.Contain("final-exit endpoint"));
        }

        private static string ReadProjectFile(string assetPath)
        {
            string projectRoot = Directory.GetParent(UnityEngine.Application.dataPath).FullName;
            return File.ReadAllText(Path.GetFullPath(Path.Combine(projectRoot, assetPath)));
        }
    }
}
#endif
