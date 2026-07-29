#if UNITY_EDITOR
using System;
using System.IO;
using ShooterMover.Application.Missions.Rooms.Content;
using ShooterMover.Content.Definitions.Levels.Selection;
using ShooterMover.Content.Definitions.Missions.Rooms;
using ShooterMover.UnityAdapters.Authoring.LevelDesign;
using UnityEditor;

namespace ShooterMover.Editor.LevelDesign.Foundation
{
    public enum LevelGridPlayableStatusKind
    {
        NotConfigured,
        Invalid,
        Valid,
        ValidButNotExported,
        ExportedButStale,
        ExportCurrent,
        CompiledButStale,
        CompiledCurrent,
        NotRegistered,
        Registered,
        ReadyToPlay,
    }

    public sealed class LevelGridPlayableStatus
    {
        internal LevelGridPlayableStatus(LevelGridPlayableBuildPaths paths)
        {
            Paths = paths;
        }

        public LevelGridPlayableBuildPaths Paths { get; }
        public LevelGridPlayableStatusKind AuthoringStatus { get; internal set; }
        public string AuthoringDetail { get; internal set; } = string.Empty;
        public LevelGridPlayableStatusKind MetadataStatus { get; internal set; }
        public string MetadataDetail { get; internal set; } = string.Empty;
        public LevelGridPlayableStatusKind ExportStatus { get; internal set; }
        public string ExportDetail { get; internal set; } = string.Empty;
        public LevelGridPlayableStatusKind CompiledStatus { get; internal set; }
        public string CompiledDetail { get; internal set; } = string.Empty;
        public LevelGridPlayableStatusKind CatalogueStatus { get; internal set; }
        public string CatalogueDetail { get; internal set; } = string.Empty;
        public LevelGridPlayableStatusKind PlayStatus { get; internal set; }
        public string PlayDetail { get; internal set; } = string.Empty;
        public bool AuthoringValid { get; internal set; }
        public bool MetadataValid { get; internal set; }
        public bool ExportCurrent { get; internal set; }
        public bool CompiledCurrent { get; internal set; }
        public bool Registered { get; internal set; }
        public bool PlayReady { get; internal set; }
        public JsonRoomContentDefinition2D CompiledAsset { get; internal set; }
        public PlayableLevelDefinition CatalogueEntry { get; internal set; }
        public string PublishedVersionId { get; internal set; } = string.Empty;
    }

    public static class LevelGridPlayableStatusEvaluator
    {
        public static LevelGridPlayableStatus Evaluate(
            LevelDesignSceneAuthoringRoot2D root)
        {
            if (root == null)
            {
                return new LevelGridPlayableStatus(null)
                {
                    AuthoringStatus = LevelGridPlayableStatusKind.NotConfigured,
                    MetadataStatus = LevelGridPlayableStatusKind.NotConfigured,
                    ExportStatus = LevelGridPlayableStatusKind.NotConfigured,
                    CompiledStatus = LevelGridPlayableStatusKind.NotConfigured,
                    CatalogueStatus = LevelGridPlayableStatusKind.NotConfigured,
                    PlayStatus = LevelGridPlayableStatusKind.NotConfigured,
                    PlayDetail = "Select a level root.",
                };
            }

            LevelGridPlayableBuildPaths paths;
            try
            {
                paths = LevelGridPlayableBuildPaths.Resolve(root);
            }
            catch (Exception exception)
            {
                return InvalidWithoutPaths(exception.Message);
            }

            var status = new LevelGridPlayableStatus(paths);
            EvaluateAuthoring(root, status);
            EvaluateMetadata(root, status);
            EvaluateExport(root, status);
            EvaluateCompile(status);
            EvaluateCatalogue(status);
            status.PlayReady = status.AuthoringValid
                && status.MetadataValid
                && status.ExportCurrent
                && status.CompiledCurrent
                && status.Registered;
            status.PlayStatus = status.PlayReady
                ? LevelGridPlayableStatusKind.ReadyToPlay
                : LevelGridPlayableStatusKind.Invalid;
            status.PlayDetail = status.PlayReady
                ? "Ready through the production level-selection route."
                : FirstBlockingDetail(status);
            return status;
        }

        private static void EvaluateAuthoring(
            LevelDesignSceneAuthoringRoot2D root,
            LevelGridPlayableStatus status)
        {
            bool productionPurpose = root.LastGridValidation.Purpose
                == LevelGridValidationPurpose.ProductionPublish;
            status.AuthoringValid = root.LastValidation.IsValid
                && root.LastGridValidation.CanPublish
                && productionPurpose;
            status.AuthoringStatus = status.AuthoringValid
                ? LevelGridPlayableStatusKind.Valid
                : LevelGridPlayableStatusKind.Invalid;
            status.AuthoringDetail = status.AuthoringValid
                ? "Foundation and Grid V2 ProductionPublish validation pass."
                : !productionPurpose
                    ? "Run Validate Playable to execute the production validation gate."
                    : "Foundation or Grid V2 production validation has errors.";
        }

        private static void EvaluateMetadata(
            LevelDesignSceneAuthoringRoot2D root,
            LevelGridPlayableStatus status)
        {
            LevelGridPlayableMetadata metadata =
                root.GetComponent<LevelGridPlayableMetadata>();
            if (metadata == null)
            {
                status.MetadataStatus = LevelGridPlayableStatusKind.NotConfigured;
                status.MetadataDetail = "Playable metadata is not configured.";
                return;
            }
            try
            {
                metadata.ValidateForPlayableExport(root);
                status.MetadataValid = true;
                status.MetadataStatus = LevelGridPlayableStatusKind.Valid;
                status.MetadataDetail = "Exact start room and final room-plus-door are valid.";
            }
            catch (Exception exception)
            {
                status.MetadataStatus = LevelGridPlayableStatusKind.Invalid;
                status.MetadataDetail = exception.Message;
            }
        }

        private static void EvaluateExport(
            LevelDesignSceneAuthoringRoot2D root,
            LevelGridPlayableStatus status)
        {
            string absolute = status.Paths.SourcePackageAbsolutePath;
            if (!Directory.Exists(absolute))
            {
                status.ExportStatus = LevelGridPlayableStatusKind.ValidButNotExported;
                status.ExportDetail = "No compiler-ready source package exists.";
                return;
            }
            try
            {
                status.Paths.ValidateDestinationOwnership();
                string levelId;
                string exportedSceneFingerprint;
                string exportedSourceFingerprint;
                int compilerVersion;
                if (!LevelGridPlayableProvenance.TryRead(
                    absolute,
                    out levelId,
                    out exportedSceneFingerprint,
                    out exportedSourceFingerprint,
                    out compilerVersion))
                {
                    status.ExportStatus = LevelGridPlayableStatusKind.ExportedButStale;
                    status.ExportDetail = "The source package has no valid playable provenance.";
                    return;
                }
                if (!string.Equals(levelId, root.LevelIdText, StringComparison.Ordinal))
                {
                    status.ExportStatus = LevelGridPlayableStatusKind.Invalid;
                    status.ExportDetail = "Export provenance belongs to another level ID.";
                    return;
                }
                string currentScene =
                    LevelGridPlayableProvenance.ComputeSceneFingerprint(root);
                string currentSource =
                    LevelGridPlayableProvenance.ComputeSourcePackageFingerprint(absolute);
                if (compilerVersion != LevelGridCompiler.CurrentVersion
                    || !string.Equals(
                        currentScene,
                        exportedSceneFingerprint,
                        StringComparison.Ordinal)
                    || !string.Equals(
                        currentSource,
                        exportedSourceFingerprint,
                        StringComparison.Ordinal))
                {
                    status.ExportStatus = LevelGridPlayableStatusKind.ExportedButStale;
                    status.ExportDetail = compilerVersion != LevelGridCompiler.CurrentVersion
                        ? "The package was exported for a different compiler schema."
                        : !string.Equals(
                            currentScene,
                            exportedSceneFingerprint,
                            StringComparison.Ordinal)
                            ? "Compilation-relevant scene or playable metadata changed."
                            : "An exported JSON sidecar changed outside the canonical export.";
                    return;
                }

                LevelGridCompileResult compile =
                    LevelGridAssetCompiler.CompileFolder(absolute);
                if (compile == null || !compile.IsValid)
                {
                    status.ExportStatus = LevelGridPlayableStatusKind.Invalid;
                    status.ExportDetail = compile != null && compile.Issues.Count > 0
                        ? compile.Issues[0].ToString()
                        : "The exported package does not compile.";
                    return;
                }
                if (!string.Equals(compile.LevelId, root.LevelIdText, StringComparison.Ordinal))
                {
                    status.ExportStatus = LevelGridPlayableStatusKind.Invalid;
                    status.ExportDetail = "Compiled source level identity does not match the root.";
                    return;
                }
                status.ExportCurrent = true;
                status.PublishedVersionId =
                    LevelGridAssetCompiler.ComputePublishedVersionIdForStatus(
                        compile.Package);
                status.ExportStatus = LevelGridPlayableStatusKind.ExportCurrent;
                status.ExportDetail = "Compiler-ready source and provenance are current.";
            }
            catch (Exception exception)
            {
                if (IsFatal(exception)) throw;
                status.ExportStatus = LevelGridPlayableStatusKind.Invalid;
                status.ExportDetail = exception.Message;
            }
        }

        private static void EvaluateCompile(LevelGridPlayableStatus status)
        {
            string assetPath = status.Paths.CompiledAssetPath;
            string absolute = LevelGridPlayableBuildPaths.ToAbsoluteProjectPath(assetPath);
            if (!File.Exists(absolute))
            {
                status.CompiledStatus = LevelGridPlayableStatusKind.NotConfigured;
                status.CompiledDetail = "No compiled runtime asset exists.";
                return;
            }
            try
            {
                status.Paths.ValidateDestinationOwnership();
                JsonRoomContentDefinition2D asset =
                    AssetDatabase.LoadAssetAtPath<JsonRoomContentDefinition2D>(assetPath);
                if (asset == null)
                {
                    throw new InvalidOperationException(
                        "The configured compiled asset cannot be loaded.");
                }
                RoomContentImportResult import = asset.Import(
                    BuiltInRoomContentObjectCatalog.Create());
                if (import == null || !import.IsValid)
                {
                    throw new InvalidOperationException(
                        import != null && import.Issues.Count > 0
                            ? import.Issues[0].Code + " at " + import.Issues[0].Path
                                + ": " + import.Issues[0].Message
                            : "The compiled asset failed runtime import validation.");
                }
                status.CompiledAsset = asset;
                string manifestPath = ManifestPath(asset);
                string expected = status.Paths.GeneratedAssetFolder
                    + "/Versions/v-"
                    + status.PublishedVersionId
                    + "/compiled.manifest.json";
                status.CompiledCurrent = status.ExportCurrent
                    && string.Equals(manifestPath, expected, StringComparison.Ordinal);
                status.CompiledStatus = status.CompiledCurrent
                    ? LevelGridPlayableStatusKind.CompiledCurrent
                    : LevelGridPlayableStatusKind.CompiledButStale;
                status.CompiledDetail = status.CompiledCurrent
                    ? "The runtime asset references the exact current immutable version."
                    : "The runtime asset is valid but does not reference the current source version.";
            }
            catch (Exception exception)
            {
                if (IsFatal(exception)) throw;
                status.CompiledStatus = LevelGridPlayableStatusKind.Invalid;
                status.CompiledDetail = exception.Message;
            }
        }

        private static void EvaluateCatalogue(LevelGridPlayableStatus status)
        {
            PlayableLevelDefinition found = null;
            var entries = PlayableLevelCatalog.All;
            for (int index = 0; index < entries.Count; index++)
            {
                if (string.Equals(
                    entries[index].LevelStableId.ToString(),
                    status.Paths.LevelId,
                    StringComparison.Ordinal))
                {
                    found = entries[index];
                    break;
                }
            }
            status.CatalogueEntry = found;
            if (found == null)
            {
                status.CatalogueStatus = LevelGridPlayableStatusKind.NotRegistered;
                status.CatalogueDetail =
                    "No exact stable-ID entry exists in the production catalogue.";
                return;
            }
            if (!string.Equals(
                found.RoomContentResourcePath,
                status.Paths.ResourcePath,
                StringComparison.Ordinal))
            {
                status.CatalogueStatus = LevelGridPlayableStatusKind.Invalid;
                status.CatalogueDetail = "The catalogue entry points to '"
                    + found.RoomContentResourcePath
                    + "', not the configured exact Resource asset '"
                    + status.Paths.ResourcePath
                    + "'.";
                return;
            }
            status.Registered = true;
            status.CatalogueStatus = LevelGridPlayableStatusKind.Registered;
            status.CatalogueDetail = "Exact stable ID and Resource path are registered.";
        }

        private static string ManifestPath(JsonRoomContentDefinition2D asset)
        {
            var serialized = new SerializedObject(asset);
            SerializedProperty manifest = serialized.FindProperty("manifest");
            return manifest == null || manifest.objectReferenceValue == null
                ? string.Empty
                : AssetDatabase.GetAssetPath(manifest.objectReferenceValue);
        }

        private static LevelGridPlayableStatus InvalidWithoutPaths(string message)
        {
            return new LevelGridPlayableStatus(null)
            {
                AuthoringStatus = LevelGridPlayableStatusKind.Invalid,
                MetadataStatus = LevelGridPlayableStatusKind.Invalid,
                ExportStatus = LevelGridPlayableStatusKind.Invalid,
                CompiledStatus = LevelGridPlayableStatusKind.Invalid,
                CatalogueStatus = LevelGridPlayableStatusKind.Invalid,
                PlayStatus = LevelGridPlayableStatusKind.Invalid,
                AuthoringDetail = message,
                MetadataDetail = message,
                ExportDetail = message,
                CompiledDetail = message,
                CatalogueDetail = message,
                PlayDetail = message,
            };
        }

        private static string FirstBlockingDetail(LevelGridPlayableStatus status)
        {
            if (!status.AuthoringValid) return status.AuthoringDetail;
            if (!status.MetadataValid) return status.MetadataDetail;
            if (!status.ExportCurrent) return status.ExportDetail;
            if (!status.CompiledCurrent) return status.CompiledDetail;
            if (!status.Registered) return status.CatalogueDetail;
            return "The level is not ready to play.";
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