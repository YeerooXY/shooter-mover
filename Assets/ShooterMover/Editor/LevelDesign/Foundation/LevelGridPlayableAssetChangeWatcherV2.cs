#if UNITY_EDITOR
using System;
using ShooterMover.UnityAdapters.Authoring.LevelDesign;
using UnityEditor;
using UnityEngine;

namespace ShooterMover.Editor.LevelDesign.Foundation
{
    /// <summary>
    /// Invalidates only open editors whose active level owns an imported/deleted/moved source,
    /// generated, compiled, or catalogue asset. This is AssetDatabase-driven and does not install
    /// a background filesystem watcher.
    /// </summary>
    public sealed class LevelGridPlayableAssetChangeWatcherV2 : AssetPostprocessor
    {
        private static void OnPostprocessAllAssets(
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            LevelGridEditorWindowV2[] windows =
                Resources.FindObjectsOfTypeAll<LevelGridEditorWindowV2>();
            for (int index = 0; index < windows.Length; index++)
            {
                LevelGridEditorWindowV2 window = windows[index];
                LevelDesignSceneAuthoringRoot2D root = window == null
                    ? null
                    : window.ActiveRoot;
                if (root == null) continue;

                LevelGridPlayableBuildPathsV2 paths;
                try
                {
                    paths = LevelGridPlayableBuildPathsV2.Resolve(root);
                }
                catch (Exception exception)
                {
                    if (IsFatal(exception)) throw;
                    window.RefreshPlayableStatusAfterExternalAssetChange();
                    continue;
                }

                if (ContainsRelevantPath(importedAssets, paths)
                    || ContainsRelevantPath(deletedAssets, paths)
                    || ContainsRelevantPath(movedAssets, paths)
                    || ContainsRelevantPath(movedFromAssetPaths, paths))
                {
                    window.RefreshPlayableStatusAfterExternalAssetChange();
                }
            }
        }

        private static bool ContainsRelevantPath(
            string[] assetPaths,
            LevelGridPlayableBuildPathsV2 paths)
        {
            if (assetPaths == null || paths == null) return false;
            for (int index = 0; index < assetPaths.Length; index++)
            {
                string path = Normalize(assetPaths[index]);
                if (string.IsNullOrEmpty(path)) continue;
                if (string.Equals(
                        path,
                        Normalize(paths.CompiledAssetPath),
                        StringComparison.Ordinal)
                    || string.Equals(
                        path,
                        Normalize(LevelGridPlayableBuildPathsV2.CatalogueSourcePath),
                        StringComparison.Ordinal)
                    || IsAtOrBelow(path, paths.SourcePackagePath)
                    || IsAtOrBelow(path, paths.GeneratedAssetFolder))
                {
                    return true;
                }
            }
            return false;
        }

        private static bool IsAtOrBelow(string assetPath, string folder)
        {
            string normalizedFolder = Normalize(folder).TrimEnd('/');
            return string.Equals(assetPath, normalizedFolder, StringComparison.Ordinal)
                || assetPath.StartsWith(normalizedFolder + "/", StringComparison.Ordinal);
        }

        private static string Normalize(string path)
        {
            return string.IsNullOrWhiteSpace(path)
                ? string.Empty
                : path.Trim().Replace('\\', '/');
        }

        private static bool IsFatal(Exception exception)
        {
            return exception is OutOfMemoryException
                || exception is StackOverflowException
                || exception is AccessViolationException;
        }
    }

    public sealed partial class LevelGridEditorWindowV2
    {
        internal void RefreshPlayableStatusAfterExternalAssetChange()
        {
            InvalidatePlayableStatus();
        }
    }
}
#endif