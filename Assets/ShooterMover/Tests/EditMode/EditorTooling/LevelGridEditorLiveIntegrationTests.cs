#if UNITY_EDITOR
using System;
using System.IO;
using NUnit.Framework;
using ShooterMover.Content.Definitions.Levels.Selection;
using ShooterMover.Content.Definitions.Missions.Rooms;
using ShooterMover.Editor.LevelDesign.Foundation;
using ShooterMover.UnityAdapters.Authoring.LevelDesign;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ShooterMover.Tests.EditorTooling.LevelDesign.Foundation
{
    public sealed class LevelGridEditorLiveIntegrationTests
    {
        private LevelDesignSceneAuthoringRoot2D root;

        [SetUp]
        public void SetUp()
        {
            Undo.ClearAll();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            GameObject rootObject = new GameObject("Test Level Root");
            root = rootObject.AddComponent<LevelDesignSceneAuthoringRoot2D>();
            root.ConfigureForTests("level.editor-runtime-integration-test");
        }

        [TearDown]
        public void TearDown()
        {
            Undo.ClearAll();
            if (root != null)
            {
                UnityEngine.Object.DestroyImmediate(root.gameObject);
            }
            CleanupWrongOwnerFixture();
        }

        [Test]
        public void AddPlayableMetadata_IsUndoableAndDoesNotChooseFallbacks()
        {
            LevelGridPlayableMetadata metadata =
                LevelGridPlayableMetadataOperations.Add(root);

            Assert.That(metadata, Is.Not.Null);
            Assert.That(metadata.StartRoom, Is.Null);
            Assert.That(metadata.FinalExitRoom, Is.Null);
            Assert.That(metadata.FinalExitDoor, Is.Null);

            Undo.PerformUndo();

            Assert.That(root.GetComponent<LevelGridPlayableMetadata>(), Is.Null);
        }

        [Test]
        public void AssignStartRoom_UsesExactSelectedReference()
        {
            LevelRoomAuthoring2D first = CreateRoom("room.first", Vector2Int.zero);
            LevelRoomAuthoring2D selected = CreateRoom("room.selected", Vector2Int.right);
            LevelGridPlayableMetadata metadata =
                LevelGridPlayableMetadataOperations.Add(root);

            LevelGridPlayableMetadataOperations.SetStartRoom(root, metadata, selected);

            Assert.That(metadata.StartRoom, Is.SameAs(selected));
            Assert.That(metadata.StartRoom, Is.Not.SameAs(first));
        }

        [Test]
        public void AssignFinalExit_UsesExactRoomAndDoorIdentity()
        {
            LevelRoomAuthoring2D room = CreateRoom("room.final", Vector2Int.zero);
            LevelDoorEndpointAuthoring2D first = CreateDoor(room, "door.first");
            LevelDoorEndpointAuthoring2D selected = CreateDoor(room, "door.selected");
            LevelGridPlayableMetadata metadata =
                LevelGridPlayableMetadataOperations.Add(root);

            LevelGridPlayableMetadataOperations.UseDoorAsFinalExit(
                root,
                metadata,
                selected);

            Assert.That(metadata.FinalExitRoom, Is.SameAs(room));
            Assert.That(metadata.FinalExitDoor, Is.SameAs(selected));
            Assert.That(metadata.FinalExitDoor, Is.Not.SameAs(first));
        }

        [Test]
        public void ChangingFinalRoom_ClearsIncompatibleFinalDoor()
        {
            LevelRoomAuthoring2D firstRoom = CreateRoom("room.first", Vector2Int.zero);
            LevelRoomAuthoring2D secondRoom = CreateRoom("room.second", Vector2Int.right);
            LevelDoorEndpointAuthoring2D firstDoor = CreateDoor(firstRoom, "door.first");
            LevelGridPlayableMetadata metadata =
                LevelGridPlayableMetadataOperations.Add(root);
            LevelGridPlayableMetadataOperations.UseDoorAsFinalExit(
                root,
                metadata,
                firstDoor);

            LevelGridPlayableMetadataOperations.SetFinalRoom(
                root,
                metadata,
                secondRoom);

            Assert.That(metadata.FinalExitRoom, Is.SameAs(secondRoom));
            Assert.That(metadata.FinalExitDoor, Is.Null);
        }

        [Test]
        public void DeletingReferencedStartRoom_ProducesActionableValidationFailure()
        {
            LevelRoomAuthoring2D start = CreateRoom("room.start", Vector2Int.zero);
            LevelRoomAuthoring2D final = CreateRoom("room.final", Vector2Int.right);
            LevelDoorEndpointAuthoring2D exit = CreateDoor(final, "door.exit");
            LevelGridPlayableMetadata metadata = ConfigureMetadata(start, final, exit);

            Assert.That(LevelGridEditorOperations.DeleteRoom(start, false), Is.True);

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                () => metadata.ValidateForPlayableExport(root));
            Assert.That(exception.Message, Does.Contain("start room"));
            Assert.That(metadata.StartRoom, Is.Null);
        }

        [Test]
        public void DeletingReferencedFinalDoor_ProducesActionableValidationFailure()
        {
            LevelRoomAuthoring2D start = CreateRoom("room.start", Vector2Int.zero);
            LevelRoomAuthoring2D final = CreateRoom("room.final", Vector2Int.right);
            LevelDoorEndpointAuthoring2D exit = CreateDoor(final, "door.exit");
            LevelGridPlayableMetadata metadata = ConfigureMetadata(start, final, exit);

            LevelGridEditorOperations.DeleteDoor(exit);

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                () => metadata.ValidateForPlayableExport(root));
            Assert.That(exception.Message, Does.Contain("exact door"));
            Assert.That(metadata.FinalExitDoor, Is.Null);
        }

        [Test]
        public void UndoDeletedFinalDoor_RestoresSerializedMetadataReference()
        {
            LevelRoomAuthoring2D start = CreateRoom("room.start", Vector2Int.zero);
            LevelRoomAuthoring2D final = CreateRoom("room.final", Vector2Int.right);
            LevelDoorEndpointAuthoring2D exit = CreateDoor(final, "door.exit");
            LevelGridPlayableMetadata metadata = ConfigureMetadata(start, final, exit);

            LevelGridEditorOperations.DeleteDoor(exit);
            Undo.PerformUndo();

            LevelDoorEndpointAuthoring2D restored =
                final.GetComponentInChildren<LevelDoorEndpointAuthoring2D>(true);
            Assert.That(restored, Is.Not.Null);
            Assert.That(restored.DoorIdText, Is.EqualTo("door.exit"));
            Assert.That(metadata.FinalExitDoor, Is.SameAs(restored));
            Assert.DoesNotThrow(() => metadata.ValidateForPlayableExport(root));
        }

        [Test]
        public void GenericLevelPaths_NeverResolveToTrackedCombatLoopDestinations()
        {
            LevelGridPlayableBuildPaths generic =
                LevelGridPlayableBuildPaths.Resolve("level.generic-editor-test");

            Assert.That(generic.IsTrackedCombatLoop, Is.False);
            Assert.That(
                generic.SourcePackagePath,
                Is.Not.EqualTo(LevelGridAssetCompiler.TrackedCombatLoopSource));
            Assert.That(
                generic.GeneratedAssetFolder,
                Is.Not.EqualTo(LevelGridAssetCompiler.TrackedCombatLoopGenerated));
            Assert.That(
                generic.CompiledAssetPath,
                Is.Not.EqualTo(LevelGridAssetCompiler.TrackedCombatLoopResource));
            Assert.That(generic.SourcePackagePath, Does.Contain("generic-editor-test"));
        }

        [Test]
        public void WrongCompiledDestinationOwner_IsRejectedBeforeBuildMutation()
        {
            const string levelId = "level.wrong-owner-fixture";
            LevelGridPlayableBuildPaths paths =
                LevelGridPlayableBuildPaths.Resolve(levelId);
            string unrelatedFolder = WrongOwnerFolder;
            EnsureAssetFolder(unrelatedFolder);
            string jsonPath = unrelatedFolder + "/manifest.json";
            File.WriteAllText(ProjectPath(jsonPath), "{}\n");
            AssetDatabase.ImportAsset(
                jsonPath,
                ImportAssetOptions.ForceSynchronousImport);
            TextAsset text = AssetDatabase.LoadAssetAtPath<TextAsset>(jsonPath);
            Assert.That(text, Is.Not.Null);

            EnsureAssetFolder(AssetFolder(paths.CompiledAssetPath));
            var asset = ScriptableObject.CreateInstance<JsonRoomContentDefinition2D>();
            asset.ConfigureCompiledAssets(
                text,
                Array.Empty<RoomContentJsonDocumentAsset2D>());
            AssetDatabase.CreateAsset(asset, paths.CompiledAssetPath);
            AssetDatabase.SaveAssets();

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                paths.ValidateDestinationOwnership);

            Assert.That(exception.Message, Does.Contain("different generated level output"));
            Assert.That(AssetDatabase.LoadAssetAtPath<TextAsset>(jsonPath), Is.Not.Null);
        }

        [Test]
        public void CompilationRelevantRoomEdit_ChangesSceneFingerprint()
        {
            LevelRoomAuthoring2D room = CreateRoom("room.move", Vector2Int.zero);
            string before = LevelGridPlayableProvenance.ComputeSceneFingerprint(root);

            LevelGridEditorOperations.MoveRoom(room, new Vector2Int(4, -3));
            string after = LevelGridPlayableProvenance.ComputeSceneFingerprint(root);

            Assert.That(after, Is.Not.EqualTo(before));
        }

        [Test]
        public void EditorViewAndSelectionChanges_DoNotChangeSceneFingerprint()
        {
            LevelRoomAuthoring2D room = CreateRoom("room.view", Vector2Int.zero);
            string before = LevelGridPlayableProvenance.ComputeSceneFingerprint(root);
            LevelGridEditorWindow window =
                ScriptableObject.CreateInstance<LevelGridEditorWindow>();
            try
            {
                var serialized = new SerializedObject(window);
                serialized.FindProperty("pan").vector2Value = new Vector2(123f, -77f);
                serialized.FindProperty("zoom").floatValue = 1.75f;
                serialized.FindProperty("selectedAuthoringObject").objectReferenceValue = room;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                Selection.activeObject = room;

                string after = LevelGridPlayableProvenance.ComputeSceneFingerprint(root);
                Assert.That(after, Is.EqualTo(before));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(window);
            }
        }

        [Test]
        public void BuildValidation_RequiresProductionValidAuthoring()
        {
            LevelGridPlayableMetadataOperations.Add(root);

            LevelGridPlayableBuildResult result =
                LevelGridPlayableBuildFacade.ValidatePlayable(root);

            Assert.That(result.ValidationPassed, Is.False);
            Assert.That(result.Failure, Is.Not.Null);
            Assert.That(result.Message, Does.Contain("validation"));
        }

        [Test]
        public void UnregisteredLevel_CannotBecomePlayReady()
        {
            root.ConfigureForTests("level.not-registered-editor-test");

            LevelGridPlayableStatus status =
                LevelGridPlayableStatusEvaluator.Evaluate(root);

            Assert.That(status.Registered, Is.False);
            Assert.That(status.PlayReady, Is.False);
            Assert.That(status.CatalogueStatus,
                Is.EqualTo(LevelGridPlayableStatusKind.NotRegistered));
        }

        [Test]
        public void CatalogueResolution_UsesExactStableIdWithoutFallback()
        {
            PlayableLevelDefinition exact;
            Assert.That(
                PlayableLevelCatalog.TryResolve(
                    PlayableLevelCatalog.AuthoredCombatLoopTestLevelStableId,
                    out exact),
                Is.True);
            Assert.That(exact, Is.Not.Null);
            Assert.That(
                exact.LevelStableId,
                Is.EqualTo(
                    PlayableLevelCatalog.AuthoredCombatLoopTestLevelStableId));
            Assert.That(
                exact.RoomContentResourcePath,
                Is.EqualTo("ProductionLevels/CombatLoopTestRoomContent"));
        }

        [Test]
        public void CatalogueMismatchAndNoFallback_AreEnforcedByStatusSource()
        {
            string source = ReadProjectFile(
                "Assets/ShooterMover/Editor/LevelDesign/Foundation/"
                + "LevelGridPlayableStatus.cs");

            StringAssert.Contains("found.RoomContentResourcePath", source);
            StringAssert.Contains("status.Paths.ResourcePath", source);
            StringAssert.Contains("No exact stable-ID entry exists", source);
            StringAssert.DoesNotContain("Entries[0]", source);
            StringAssert.DoesNotContain("FirstLevelStableId", source);
        }

        [Test]
        public void MenuAndEditorBuildActions_RouteThroughSamePublicFacades()
        {
            string compiler = ReadProjectFile(
                "Assets/ShooterMover/Editor/LevelDesign/Foundation/"
                + "LevelGridAssetCompiler.cs");
            string buildFacade = ReadProjectFile(
                "Assets/ShooterMover/Editor/LevelDesign/Foundation/"
                + "LevelGridPlayableBuildFacade.cs");
            string window = ReadProjectFile(
                "Assets/ShooterMover/Editor/LevelDesign/Foundation/"
                + "LevelGridEditorWindow.Playable.cs");

            StringAssert.Contains("CompileToAsset(", compiler);
            StringAssert.Contains("LevelGridAssetCompiler.CompileToAsset(", buildFacade);
            StringAssert.Contains("LevelGridPlayableExporter.Export(", buildFacade);
            StringAssert.Contains("LevelGridPlayableBuildFacade.ExportAndCompile", window);
            StringAssert.DoesNotContain("ExecuteMenuItem", buildFacade);
            StringAssert.DoesNotContain("CompileToAsset(", window);
        }

        [Test]
        public void StatusRefresh_IsChangeDrivenRatherThanTimeDriven()
        {
            string source = ReadProjectFile(
                "Assets/ShooterMover/Editor/LevelDesign/Foundation/"
                + "LevelGridEditorWindow.Playable.cs");

            StringAssert.Contains("playableSceneFingerprint", source);
            StringAssert.Contains("playableSourceSnapshot", source);
            StringAssert.DoesNotContain("timeSinceStartup", source);
            StringAssert.DoesNotContain("nextPlayableStatusCheck", source);
        }

        private LevelGridPlayableMetadata ConfigureMetadata(
            LevelRoomAuthoring2D start,
            LevelRoomAuthoring2D final,
            LevelDoorEndpointAuthoring2D exit)
        {
            LevelGridPlayableMetadata metadata =
                LevelGridPlayableMetadataOperations.Add(root);
            LevelGridPlayableMetadataOperations.SetStartRoom(root, metadata, start);
            LevelGridPlayableMetadataOperations.UseDoorAsFinalExit(
                root,
                metadata,
                exit);
            return metadata;
        }

        private LevelRoomAuthoring2D CreateRoom(string roomId, Vector2Int coordinate)
        {
            GameObject roomObject = new GameObject(roomId);
            roomObject.transform.SetParent(root.transform, false);
            BoxCollider2D bounds = roomObject.AddComponent<BoxCollider2D>();
            bounds.size = new Vector2(20f, 14f);
            LevelRoomAuthoring2D room = roomObject.AddComponent<LevelRoomAuthoring2D>();
            room.ConfigureForTests(
                roomId,
                coordinate,
                new Vector2(20f, 14f),
                Vector2Int.one,
                bounds);
            room.SnapToAuthoredGrid();
            return room;
        }

        private static LevelDoorEndpointAuthoring2D CreateDoor(
            LevelRoomAuthoring2D room,
            string doorId)
        {
            GameObject doorObject = new GameObject(doorId);
            doorObject.transform.SetParent(room.transform, false);
            LevelDoorEndpointAuthoring2D door =
                doorObject.AddComponent<LevelDoorEndpointAuthoring2D>();
            door.ConfigureAuthoring(
                doorId,
                room,
                LevelDoorSide.East,
                LevelDoorPlacementMode.EdgeManaged,
                0.5f,
                Vector2.zero,
                true,
                true);
            door.SnapToPlacement();
            return door;
        }

        private static string WrongOwnerFolder
        {
            get
            {
                return "Assets/ShooterMover/Tests/Temp/"
                    + "LevelGridEditorRuntimeIntegrationWrongOwner";
            }
        }

        private static void CleanupWrongOwnerFixture()
        {
            LevelGridPlayableBuildPaths paths =
                LevelGridPlayableBuildPaths.Resolve("level.wrong-owner-fixture");
            if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(
                paths.CompiledAssetPath) != null)
            {
                AssetDatabase.DeleteAsset(paths.CompiledAssetPath);
            }
            if (AssetDatabase.IsValidFolder(WrongOwnerFolder))
            {
                AssetDatabase.DeleteAsset(WrongOwnerFolder);
            }
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        }

        private static void EnsureAssetFolder(string assetFolder)
        {
            string[] segments = assetFolder.Split('/');
            string current = segments[0];
            for (int index = 1; index < segments.Length; index++)
            {
                string next = current + "/" + segments[index];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    string guid = AssetDatabase.CreateFolder(current, segments[index]);
                    Assert.That(guid, Is.Not.Empty, "Could not create " + next);
                }
                current = next;
            }
        }

        private static string AssetFolder(string assetPath)
        {
            return Path.GetDirectoryName(assetPath).Replace('\\', '/');
        }

        private static string ReadProjectFile(string assetPath)
        {
            return File.ReadAllText(ProjectPath(assetPath));
        }

        private static string ProjectPath(string assetPath)
        {
            string projectRoot = Directory.GetParent(UnityEngine.Application.dataPath).FullName;
            return Path.GetFullPath(Path.Combine(projectRoot, assetPath));
        }
    }
}
#endif
