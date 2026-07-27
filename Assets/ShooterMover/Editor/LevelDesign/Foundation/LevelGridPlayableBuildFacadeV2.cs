#if UNITY_EDITOR
using System;
using System.IO;
using ShooterMover.UnityAdapters.Authoring.LevelDesign;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ShooterMover.Editor.LevelDesign.Foundation
{
    public sealed class LevelGridPlayableBuildResultV2
    {
        public bool ValidationPassed { get; internal set; }
        public bool ExportCommitted { get; internal set; }
        public bool CompileCommitted { get; internal set; }
        public JsonRoomContentDefinition2D CompiledAsset { get; internal set; }
        public string Message { get; internal set; } = string.Empty;
        public Exception Failure { get; internal set; }
        public bool Succeeded
        {
            get { return ValidationPassed && Failure == null; }
        }
    }

    /// <summary>
    /// Callable editor façade shared by window actions. Export and asset publication remain separate
    /// canonical transactions owned by the accepted #336/#338 implementations.
    /// </summary>
    public static class LevelGridPlayableBuildFacadeV2
    {
        public static LevelGridPlayableBuildResultV2 ValidatePlayable(
            LevelDesignSceneAuthoringRoot2D root)
        {
            var result = new LevelGridPlayableBuildResultV2();
            try
            {
                LevelGridPlayableBuildPathsV2 paths = ValidateOrThrow(root);
                result.ValidationPassed = true;
                result.Message = "Playable validation passed for " + paths.LevelId + ".";
            }
            catch (Exception exception)
            {
                if (IsFatal(exception)) throw;
                result.Failure = exception;
                result.Message = exception.Message;
            }
            return result;
        }

        public static LevelGridPlayableBuildResultV2 ExportPlayable(
            LevelDesignSceneAuthoringRoot2D root)
        {
            var result = new LevelGridPlayableBuildResultV2();
            try
            {
                LevelGridPlayableBuildPathsV2 paths = ValidateOrThrow(root);
                result.ValidationPassed = true;
                LevelGridV2PlayableExporter.Export(
                    root,
                    paths.SourcePackageAbsolutePath);
                // Export returns only after the staged package occupies the destination. Refresh is
                // post-commit observation and must not redefine the transaction boundary.
                result.ExportCommitted = true;
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                result.Message = "Playable source export committed to "
                    + paths.SourcePackagePath
                    + ".";
            }
            catch (Exception exception)
            {
                if (IsFatal(exception)) throw;
                result.Failure = exception;
                result.Message = result.ExportCommitted
                    ? "Export committed, then a post-commit editor operation failed: "
                        + exception.Message
                    : "Export failed before commit: " + exception.Message;
            }
            return result;
        }

        public static LevelGridPlayableBuildResultV2 CompileAsset(
            LevelDesignSceneAuthoringRoot2D root)
        {
            var result = new LevelGridPlayableBuildResultV2();
            try
            {
                LevelGridPlayableBuildPathsV2 paths = ValidateOrThrow(root);
                result.ValidationPassed = true;
                RequireCurrentExport(root);
                paths.ClaimGeneratedDestination();
                result.CompiledAsset = LevelGridV2AssetCompiler.CompileToAsset(
                    paths.SourcePackagePath,
                    paths.GeneratedAssetFolder,
                    paths.CompiledAssetPath);
                result.CompileCommitted = true;
                result.Message = "Compiled asset publication committed to "
                    + paths.CompiledAssetPath
                    + ".";
            }
            catch (Exception exception)
            {
                if (IsFatal(exception)) throw;
                result.Failure = exception;
                result.Message = "Compile failed before authoritative publication or rolled back "
                    + "after a failed switch; the previous playable asset remains authoritative. "
                    + exception.Message;
            }
            return result;
        }

        public static LevelGridPlayableBuildResultV2 ExportAndCompile(
            LevelDesignSceneAuthoringRoot2D root)
        {
            var result = new LevelGridPlayableBuildResultV2();
            try
            {
                LevelGridPlayableBuildPathsV2 paths = ValidateOrThrow(root);
                result.ValidationPassed = true;
                LevelGridV2PlayableExporter.Export(
                    root,
                    paths.SourcePackageAbsolutePath);
                // This is the export commit point. Later refresh or compile failure must preserve it.
                result.ExportCommitted = true;
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

                paths.ClaimGeneratedDestination();
                result.CompiledAsset = LevelGridV2AssetCompiler.CompileToAsset(
                    paths.SourcePackagePath,
                    paths.GeneratedAssetFolder,
                    paths.CompiledAssetPath);
                result.CompileCommitted = true;
                result.Message = "Export and transactional compiled-asset publication committed.";
            }
            catch (Exception exception)
            {
                if (IsFatal(exception)) throw;
                result.Failure = exception;
                result.Message = result.ExportCommitted
                    ? "Export committed, but a later build step failed before a new compiled asset "
                        + "became authoritative. The previous compiled asset remains authoritative. "
                        + exception.Message
                    : "Export failed before commit; compilation was not started. "
                        + exception.Message;
            }
            return result;
        }

        public static JsonRoomContentDefinition2D SelectCompiledAsset(
            LevelDesignSceneAuthoringRoot2D root)
        {
            LevelGridPlayableBuildPathsV2 paths =
                LevelGridPlayableBuildPathsV2.Resolve(root);
            JsonRoomContentDefinition2D asset =
                AssetDatabase.LoadAssetAtPath<JsonRoomContentDefinition2D>(
                    paths.CompiledAssetPath);
            if (asset == null)
            {
                throw new InvalidOperationException(
                    "No configured compiled asset exists at " + paths.CompiledAssetPath + ".");
            }
            Selection.activeObject = asset;
            EditorGUIUtility.PingObject(asset);
            return asset;
        }

        public static void RevealSourceFolder(LevelDesignSceneAuthoringRoot2D root)
        {
            LevelGridPlayableBuildPathsV2 paths =
                LevelGridPlayableBuildPathsV2.Resolve(root);
            if (!Directory.Exists(paths.SourcePackageAbsolutePath))
            {
                throw new DirectoryNotFoundException(
                    "The source package does not exist: " + paths.SourcePackagePath);
            }
            EditorUtility.RevealInFinder(paths.SourcePackageAbsolutePath);
        }

        public static void RevealGeneratedFolder(LevelDesignSceneAuthoringRoot2D root)
        {
            LevelGridPlayableBuildPathsV2 paths =
                LevelGridPlayableBuildPathsV2.Resolve(root);
            string absolute = LevelGridPlayableBuildPathsV2.ToAbsoluteProjectPath(
                paths.GeneratedAssetFolder);
            if (!Directory.Exists(absolute))
            {
                throw new DirectoryNotFoundException(
                    "The generated folder does not exist: " + paths.GeneratedAssetFolder);
            }
            EditorUtility.RevealInFinder(absolute);
        }

        public static void OpenCatalogueSource()
        {
            MonoScript script = AssetDatabase.LoadAssetAtPath<MonoScript>(
                LevelGridPlayableBuildPathsV2.CatalogueSourcePath);
            if (script == null)
            {
                throw new InvalidOperationException(
                    "The production catalogue source could not be loaded.");
            }
            AssetDatabase.OpenAsset(script);
        }

        public static string CopyRegistrationValues(
            LevelDesignSceneAuthoringRoot2D root)
        {
            LevelGridPlayableBuildPathsV2 paths =
                LevelGridPlayableBuildPathsV2.Resolve(root);
            string text = "level_id: " + paths.LevelId + Environment.NewLine
                + "gameplay_scene: "
                + "Assets/ShooterMover/Scenes/Gameplay/PlayableLevel.unity"
                + Environment.NewLine
                + "room_content_resource: "
                + paths.ResourcePath
                + Environment.NewLine
                + "compiled_asset: "
                + paths.CompiledAssetPath;
            EditorGUIUtility.systemCopyBuffer = text;
            return text;
        }

        public static void OpenProductionLevelSelectionScene(
            LevelDesignSceneAuthoringRoot2D root)
        {
            LevelGridPlayableStatusV2 status =
                LevelGridPlayableStatusEvaluatorV2.Evaluate(root);
            if (!status.PlayReady)
            {
                throw new InvalidOperationException(
                    "Play is blocked: " + status.PlayDetail);
            }
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                throw new InvalidOperationException(
                    "Stop Play Mode before opening the production level-selection scene.");
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                throw new OperationCanceledException(
                    "Opening production Level Selection was cancelled; the authoring scene remains open.");
            }
            EditorSceneManager.OpenScene(
                LevelGridPlayableBuildPathsV2.LevelSelectionScenePath,
                OpenSceneMode.Single);
            Debug.Log(
                "Opened the production level-selection scene. Select exact level '"
                + status.Paths.LevelId
                + "'. Direct editor play is intentionally unavailable because production entry "
                + "requires the selected character and route context owned by Level Selection.");
        }

        private static LevelGridPlayableBuildPathsV2 ValidateOrThrow(
            LevelDesignSceneAuthoringRoot2D root)
        {
            if (root == null) throw new ArgumentNullException(nameof(root));
            LevelGridAuthoringV2LiveValidation.ValidateNow(
                root,
                LevelGridValidationPurposeV2.ProductionPublish,
                true,
                true);
            if (!root.LastValidation.IsValid)
            {
                throw new InvalidOperationException(
                    "Foundation validation must pass before playable build.");
            }
            if (!root.LastGridValidation.CanPublish)
            {
                throw new InvalidOperationException(
                    root.LastGridValidation.Problems.Count == 0
                        ? "Grid V2 ProductionPublish validation failed."
                        : root.LastGridValidation.Problems[0].ToString());
            }
            LevelGridPlayableMetadataV2 metadata =
                root.GetComponent<LevelGridPlayableMetadataV2>();
            if (metadata == null)
            {
                throw new InvalidOperationException(
                    "Playable metadata is not configured.");
            }
            metadata.ValidateForPlayableExport(root);
            LevelGridPlayableBuildPathsV2 paths =
                LevelGridPlayableBuildPathsV2.Resolve(root);
            paths.ValidateDestinationOwnership();
            return paths;
        }

        private static void RequireCurrentExport(LevelDesignSceneAuthoringRoot2D root)
        {
            LevelGridPlayableStatusV2 status =
                LevelGridPlayableStatusEvaluatorV2.Evaluate(root);
            if (!status.ExportCurrent)
            {
                throw new InvalidOperationException(
                    "Compile requires a current canonical playable export: "
                    + status.ExportDetail);
            }
        }

        private static bool IsFatal(Exception exception)
        {
            return exception is OutOfMemoryException
                || exception is StackOverflowException
                || exception is AccessViolationException;
        }
    }
}
#endif