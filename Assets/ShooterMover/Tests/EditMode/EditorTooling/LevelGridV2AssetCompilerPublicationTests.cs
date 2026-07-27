#if UNITY_EDITOR
using System;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using ShooterMover.Content.Definitions.Levels.Selection;
using ShooterMover.Content.Definitions.Missions.Rooms;
using ShooterMover.Editor.LevelDesign.Foundation;
using ShooterMover.UnityAdapters.Authoring.LevelDesign;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace ShooterMover.Tests.EditorTooling.LevelDesign.Foundation
{
    [NonParallelizable]
    public sealed class LevelGridV2AssetCompilerPublicationTests
    {
        private string assetRoot;
        private string generatedRoot;
        private string destinationAssetPath;
        private string sourceRoot;

        [SetUp]
        public void SetUp()
        {
            assetRoot = "Assets/ShooterMover/Tests/Temp/LevelGridV2AssetCompiler-"
                + Guid.NewGuid().ToString("N");
            generatedRoot = assetRoot + "/Generated";
            destinationAssetPath = assetRoot + "/Resources/RoomContent.asset";
            sourceRoot = Path.Combine(
                Path.GetTempPath(),
                "shooter-mover-grid-v2-publication-" + Guid.NewGuid().ToString("N"));
            CopyDirectory(ProjectPath(LevelGridV2AssetCompiler.TrackedCombatLoopSource), sourceRoot);
        }

        [TearDown]
        public void TearDown()
        {
            if (AssetDatabase.IsValidFolder(assetRoot))
            {
                AssetDatabase.DeleteAsset(assetRoot);
            }
            else
            {
                string absoluteAssetRoot = ProjectPath(assetRoot);
                if (Directory.Exists(absoluteAssetRoot))
                {
                    Directory.Delete(absoluteAssetRoot, true);
                }
                if (File.Exists(absoluteAssetRoot + ".meta"))
                {
                    File.Delete(absoluteAssetRoot + ".meta");
                }
            }
            if (Directory.Exists(sourceRoot)) Directory.Delete(sourceRoot, true);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        }

        [Test]
        public void FailureBeforeAuthoritativeSwitch_PreservesPreviousPlayableAsset()
        {
            JsonRoomContentDefinition2D baseline = PublishBaseline();
            byte[] previousBytes = File.ReadAllBytes(ProjectPath(destinationAssetPath));
            string previousManifest = ManifestPath(baseline);
            ChangeCompiledContent();

            Assert.Throws<InjectedPublicationFailureException>(
                () => LevelGridV2AssetCompiler.CompileToAssetForTests(
                    sourceRoot,
                    generatedRoot,
                    destinationAssetPath,
                    new ThrowingFaultInjector(
                        LevelGridV2AssetCompilerPublishStep.BeforeAuthoritativeAssetSwitch)));

            CollectionAssert.AreEqual(
                previousBytes,
                File.ReadAllBytes(ProjectPath(destinationAssetPath)));
            JsonRoomContentDefinition2D restored =
                AssetDatabase.LoadAssetAtPath<JsonRoomContentDefinition2D>(destinationAssetPath);
            AssertValid(restored);
            Assert.That(ManifestPath(restored), Is.EqualTo(previousManifest));
        }

        [Test]
        public void FailureAfterAuthoritativeFileReplacement_RollsBackPreviousPlayableAsset()
        {
            JsonRoomContentDefinition2D baseline = PublishBaseline();
            byte[] previousBytes = File.ReadAllBytes(ProjectPath(destinationAssetPath));
            string previousManifest = ManifestPath(baseline);
            ChangeCompiledContent();

            Assert.Throws<InjectedPublicationFailureException>(
                () => LevelGridV2AssetCompiler.CompileToAssetForTests(
                    sourceRoot,
                    generatedRoot,
                    destinationAssetPath,
                    new ThrowingFaultInjector(
                        LevelGridV2AssetCompilerPublishStep.AfterAuthoritativeAssetFileReplaced)));

            CollectionAssert.AreEqual(
                previousBytes,
                File.ReadAllBytes(ProjectPath(destinationAssetPath)));
            JsonRoomContentDefinition2D restored =
                AssetDatabase.LoadAssetAtPath<JsonRoomContentDefinition2D>(destinationAssetPath);
            AssertValid(restored);
            Assert.That(ManifestPath(restored), Is.EqualTo(previousManifest));
        }

        [Test]
        public void SuccessfulPublish_SwitchesVersionThenRemovesUnreferencedOldVersion()
        {
            JsonRoomContentDefinition2D baseline = PublishBaseline();
            string previousManifest = ManifestPath(baseline);
            string previousVersionFolder = AssetFolder(previousManifest);
            ChangeCompiledContent();

            JsonRoomContentDefinition2D published = LevelGridV2AssetCompiler.CompileToAsset(
                sourceRoot,
                generatedRoot,
                destinationAssetPath);

            AssertValid(published);
            string currentManifest = ManifestPath(published);
            Assert.That(currentManifest, Is.Not.EqualTo(previousManifest));
            Assert.That(currentManifest, Does.StartWith(generatedRoot + "/Versions/v-"));
            Assert.That(AssetDatabase.IsValidFolder(previousVersionFolder), Is.False);
        }

        [Test]
        public void Cleanup_RetainsOldVersionReferencedByAnotherRuntimeAsset()
        {
            JsonRoomContentDefinition2D baseline = PublishBaseline();
            string previousManifest = ManifestPath(baseline);
            string previousVersionFolder = AssetFolder(previousManifest);
            string retainedAssetPath = assetRoot + "/Resources/RetainedRoomContent.asset";
            Assert.That(
                AssetDatabase.CopyAsset(destinationAssetPath, retainedAssetPath),
                Is.True);
            AssetDatabase.ImportAsset(
                retainedAssetPath,
                ImportAssetOptions.ForceSynchronousImport);
            JsonRoomContentDefinition2D retained =
                AssetDatabase.LoadAssetAtPath<JsonRoomContentDefinition2D>(retainedAssetPath);
            AssertValid(retained);
            ChangeCompiledContent();

            JsonRoomContentDefinition2D published = LevelGridV2AssetCompiler.CompileToAsset(
                sourceRoot,
                generatedRoot,
                destinationAssetPath);

            AssertValid(published);
            Assert.That(ManifestPath(published), Is.Not.EqualTo(previousManifest));
            Assert.That(AssetDatabase.IsValidFolder(previousVersionFolder), Is.True);
            Assert.That(ManifestPath(retained), Is.EqualTo(previousManifest));
        }

        [Test]
        public void CleanupFailure_DoesNotTurnCommittedPublishIntoReportedFailure()
        {
            JsonRoomContentDefinition2D baseline = PublishBaseline();
            string previousManifest = ManifestPath(baseline);
            ChangeCompiledContent();
            LogAssert.Expect(
                LogType.Warning,
                new Regex("publication committed successfully, but generated cleanup failed"));

            JsonRoomContentDefinition2D published =
                LevelGridV2AssetCompiler.CompileToAssetForTests(
                    sourceRoot,
                    generatedRoot,
                    destinationAssetPath,
                    new ThrowingFaultInjector(
                        LevelGridV2AssetCompilerPublishStep.BeforePostCommitCleanup));

            AssertValid(published);
            Assert.That(ManifestPath(published), Is.Not.EqualTo(previousManifest));
        }

        [Test]
        public void WrongTypeDestination_FailsClosedBeforeGeneratedOutputIsWritten()
        {
            EnsureAssetFolder(AssetFolder(destinationAssetPath));
            var wrongType = ScriptableObject.CreateInstance<LevelSelectionCatalogDefinitionV1>();
            AssetDatabase.CreateAsset(wrongType, destinationAssetPath);
            AssetDatabase.SaveAssets();

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                () => LevelGridV2AssetCompiler.CompileToAsset(
                    sourceRoot,
                    generatedRoot,
                    destinationAssetPath));

            Assert.That(exception.Message, Does.Contain("not a loadable"));
            Assert.That(AssetDatabase.IsValidFolder(generatedRoot), Is.False);
            Assert.That(
                AssetDatabase.LoadAssetAtPath<LevelSelectionCatalogDefinitionV1>(
                    destinationAssetPath),
                Is.Not.Null);
        }

        private JsonRoomContentDefinition2D PublishBaseline()
        {
            JsonRoomContentDefinition2D asset = LevelGridV2AssetCompiler.CompileToAsset(
                sourceRoot,
                generatedRoot,
                destinationAssetPath);
            AssertValid(asset);
            return asset;
        }

        private void ChangeCompiledContent()
        {
            string roomPath = Path.Combine(
                sourceRoot,
                "Rooms",
                "Room_1_0_01",
                "room.json");
            string content = File.ReadAllText(roomPath);
            Assert.That(content, Does.Contain("SINGLE CONTACT"));
            File.WriteAllText(
                roomPath,
                content.Replace("SINGLE CONTACT", "SINGLE CONTACT REVISED"));
        }

        private static void AssertValid(JsonRoomContentDefinition2D asset)
        {
            Assert.That(asset, Is.Not.Null);
            RoomContentImportResultV1 imported = asset.Import(
                BuiltInRoomContentObjectCatalogV1.Create());
            Assert.That(imported, Is.Not.Null);
            Assert.That(
                imported.IsValid,
                Is.True,
                imported.Issues.Count == 0
                    ? string.Empty
                    : imported.Issues[0].Code + " at " + imported.Issues[0].Path
                        + ": " + imported.Issues[0].Message);
        }

        private static string ManifestPath(JsonRoomContentDefinition2D asset)
        {
            var serialized = new SerializedObject(asset);
            SerializedProperty manifest = serialized.FindProperty("manifest");
            Assert.That(manifest, Is.Not.Null);
            Assert.That(manifest.objectReferenceValue, Is.Not.Null);
            return AssetDatabase.GetAssetPath(manifest.objectReferenceValue);
        }

        private static string AssetFolder(string assetPath)
        {
            return Path.GetDirectoryName(assetPath).Replace('\\', '/');
        }

        private static string ProjectPath(string path)
        {
            if (Path.IsPathRooted(path)) return Path.GetFullPath(path);
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            return Path.GetFullPath(Path.Combine(projectRoot, path));
        }

        private static void CopyDirectory(string source, string destination)
        {
            Directory.CreateDirectory(destination);
            string[] directories = Directory.GetDirectories(
                source,
                "*",
                SearchOption.AllDirectories);
            for (int index = 0; index < directories.Length; index++)
            {
                string relative = directories[index].Substring(source.Length)
                    .TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                Directory.CreateDirectory(Path.Combine(destination, relative));
            }

            string[] files = Directory.GetFiles(source, "*.json", SearchOption.AllDirectories);
            for (int index = 0; index < files.Length; index++)
            {
                string relative = files[index].Substring(source.Length)
                    .TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                string target = Path.Combine(destination, relative);
                Directory.CreateDirectory(Path.GetDirectoryName(target));
                File.Copy(files[index], target, false);
            }
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

        private sealed class ThrowingFaultInjector : ILevelGridV2AssetCompilerFaultInjector
        {
            private readonly LevelGridV2AssetCompilerPublishStep target;

            public ThrowingFaultInjector(LevelGridV2AssetCompilerPublishStep target)
            {
                this.target = target;
            }

            public void OnStep(LevelGridV2AssetCompilerPublishStep step)
            {
                if (step == target) throw new InjectedPublicationFailureException(step);
            }
        }

        private sealed class InjectedPublicationFailureException : Exception
        {
            public InjectedPublicationFailureException(LevelGridV2AssetCompilerPublishStep step)
                : base("Injected Level Grid V2 publication failure at " + step + ".")
            {
            }
        }
    }
}
#endif
