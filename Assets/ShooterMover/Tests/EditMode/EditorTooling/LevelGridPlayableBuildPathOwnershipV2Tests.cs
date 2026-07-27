#if UNITY_EDITOR
using System;
using System.IO;
using NUnit.Framework;
using ShooterMover.Editor.LevelDesign.Foundation;
using UnityEditor;
using UnityEngine;

namespace ShooterMover.Tests.EditorTooling.LevelDesign.Foundation
{
    [NonParallelizable]
    public sealed class LevelGridPlayableBuildPathOwnershipV2Tests
    {
        private const string OwnedLevelId = "level.generated-owner-test";
        private const string WrongLevelId = "level.someone-else";

        [TearDown]
        public void TearDown()
        {
            LevelGridPlayableBuildPathsV2 paths =
                LevelGridPlayableBuildPathsV2.Resolve(OwnedLevelId);
            if (AssetDatabase.IsValidFolder(paths.GeneratedAssetFolder))
            {
                AssetDatabase.DeleteAsset(paths.GeneratedAssetFolder);
            }
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        }

        [Test]
        public void NonEmptyGeneratedFolderWithWrongOwner_IsRejectedBeforeMutation()
        {
            LevelGridPlayableBuildPathsV2 paths =
                LevelGridPlayableBuildPathsV2.Resolve(OwnedLevelId);
            EnsureAssetFolder(paths.GeneratedAssetFolder);
            string markerPath = paths.GeneratedAssetFolder + "/.level-grid-owner";
            File.WriteAllText(ProjectPath(markerPath), WrongLevelId + Environment.NewLine);
            AssetDatabase.ImportAsset(
                markerPath,
                ImportAssetOptions.ForceSynchronousImport);
            string before = File.ReadAllText(ProjectPath(markerPath));

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                paths.ValidateDestinationOwnership);

            Assert.That(exception.Message, Does.Contain("another level ID"));
            Assert.That(File.ReadAllText(ProjectPath(markerPath)), Is.EqualTo(before));
            Assert.That(
                Directory.GetDirectories(ProjectPath(paths.GeneratedAssetFolder)),
                Is.Empty);
        }

        [Test]
        public void ClaimGeneratedDestination_IsIdempotentAndRecordsExactStableId()
        {
            LevelGridPlayableBuildPathsV2 paths =
                LevelGridPlayableBuildPathsV2.Resolve(OwnedLevelId);

            paths.ClaimGeneratedDestination();
            string markerPath = paths.GeneratedAssetFolder + "/.level-grid-owner";
            string first = File.ReadAllText(ProjectPath(markerPath));
            paths.ClaimGeneratedDestination();
            string second = File.ReadAllText(ProjectPath(markerPath));

            Assert.That(first.Trim(), Is.EqualTo(OwnedLevelId));
            Assert.That(second, Is.EqualTo(first));
            Assert.DoesNotThrow(paths.ValidateDestinationOwnership);
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
                    Assert.That(guid, Is.Not.Empty);
                }
                current = next;
            }
        }

        private static string ProjectPath(string assetPath)
        {
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            return Path.GetFullPath(Path.Combine(projectRoot, assetPath));
        }
    }
}
#endif