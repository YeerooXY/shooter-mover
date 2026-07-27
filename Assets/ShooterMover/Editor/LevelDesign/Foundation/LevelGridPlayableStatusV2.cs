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
    public enum LevelGridPlayableStatusKindV2
    {
        NotConfigured,
        Invalid,
        ValidButNotExported,
        ExportedButStale,
        ExportCurrent,
        CompiledButStale,
        CompiledCurrent,
        NotRegistered,
        Registered,
        ReadyToPlay,
    }

    public sealed class LevelGridPlayableStatusV2
    {
        internal LevelGridPlayableStatusV2(LevelGridPlayableBuildPathsV2 paths)
        {
            Paths = paths;
        }

        public LevelGridPlayableBuildPathsV2 Paths { get; }
        public LevelGridPlayableStatusKindV2 AuthoringStatus { get; internal set; }
        public string AuthoringDetail { get; internal set; } = string.Empty;
        public LevelGridPlayableStatusKindV2 MetadataStatus { get; internal set; }
        public string MetadataDetail { get; internal set; } = string.Empty;
        public LevelGridPlayableStatusKindV2 ExportStatus { get; internal set; }
        public string ExportDetail { get; internal set; } = string.Empty;
        public LevelGridPlayableStatusKindV2 CompiledStatus { get; internal set; }
        public string CompiledDetail { get; internal set; } = string.Empty;
        public LevelGridPlayableStatusKindV2 CatalogueStatus { get; internal set; }
        public string CatalogueDetail { get; internal set; } = string.Empty;
        public LevelGridPlayableStatusKindV2 PlayStatus { get; internal set; }
        public string PlayDetail { get; internal set; } = string.Empty;
        public bool AuthoringValid { get; internal set; }
        public bool MetadataValid { get; internal set; }
        public bool ExportCurrent { get; internal set; }
        public bool CompiledCurrent { get; internal set; }
        public bool Registered { get; internal set; }
        public bool PlayReady { get; internal set; }
        public JsonRoomContentDefinition2D CompiledAsset { get; internal set; }
        public ProductionPlayableLevelDefinitionV1 CatalogueEntry { get; internal set; }
        public string PublishedVersionId { get; internal set; } = string.Empty;
    }

    public static class LevelGridPlayableStatusEvaluatorV2
    {
        public static LevelGridPlayableStatusV2 Evaluate(
            LevelDesignSceneAuthoringRoot2D root)
        {
            if (root == null)
            {
                return new LevelGridPlayableStatusV2(null)
                {
                    AuthoringStatus = LevelGridPlayableStatusKindV2.NotConfigured,
                    MetadataStatus = LevelGridPlayableStatusKindV2.NotConfigured,
                    ExportStatus = LevelGridPlayableStatusKindV2.NotConfigured,
                    CompiledStatus = LevelGridPlayableStatusKindV2.NotConfigured,
                    CatalogueStatus = LevelGridPlayableStatusKindV2.NotConfigured,
                    PlayStatus = LevelGridPlayableStatusKindV2.NotConfigured,
                    PlayDetail = "Select a level root.",
                };
            }

            LevelGridPlayableBuildPathsV2 paths;
            try
            {
                paths = LevelGridPlayableBuildPathsV2.Resolve(root);
            }
            catch (Exception exception)
            {
                return InvalidWithoutPaths(exception.Message);
            }

            var status = new LevelGridPlayableStatusV2(paths);
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
                ? LevelGridPlayableStatusKindV2.ReadyToPlay
                : LevelGridPlayableStatusKindV2.Invalid;
            status.PlayDetail = status.PlayReady
                ? "Ready through the production level-selection route."
                : FirstBlockingDetail(status);
            return status;
        }

        private static void EvaluateAuthoring(
            LevelDesignSceneAuthoringRoot2D root,
            LevelGridPlayableStatusV2 status)
        {
            bool productionPurpose = root.LastGridValidation.Purpose
                == LevelGridValidationPurposeV2.ProductionPublish;
            status.AuthoringValid = root.LastValidation.IsValid
                && root.LastGridValidation.CanPublish
                && productionPurpose;
            status.AuthoringStatus = status.AuthoringValid
                ? LevelGridPlayableStatusKindV2.Registered
                : LevelGridPlayableStatusKindV2.Invalid;
            status.AuthoringDetail = status.AuthoringValid
                ? "Foundation and Grid V2 ProductionPublish validation pass."
                : !productionPurpose
                    ? "Run Validate Playable to execute the production validation gate."
                    : "Foundation or Grid V2 production validation has errors.";
        }

        private static void EvaluateMetadata(
            LevelDesignSceneAuthoringRoot2D root,
            LevelGridPlayableStatusV2 status)
        {
            LevelGridPlayableMetadataV2 metadata =
                root.GetComponent<LevelGridPlayableMetadataV2>();
            if (metadata == null)
            {
                status.MetadataStatus = LevelGridPlayableStatusKindV2.NotConfigured;
                status.MetadataDetail = "Playable metadata is not configured.";
                return;
            }
            try
            {
                metadata.ValidateForPlayableExport(root);
                status.MetadataValid = true;
                status.MetadataStatus = LevelGridPlayableStatusKindV2.Registered;
                status.MetadataDetail = "Exact start room and final room-plus-door are valid.";
            }
            catch (Exception exception)
            {
                status.MetadataStatus = LevelGridPlayableStatusKindV2.Invalid;
                status.MetadataDetail = exception.Message;
            }
        }

        private static void EvaluateExport(
            LevelDesignSceneAuthoringRoot2D root,
            LevelGridPlayableStatusV2 status)
        {
            string absolute = status.Paths.SourcePackageAbsolutePath;
            if (!Directory.Exists(absolute))
            {
                status.ExportStatus = LevelGridPlayableStatusKindV2.ValidButNotExported;
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
                if (!LevelGridPlayableProvenanceV2.TryRead(
                    absolute,
                    out levelId,
                    out exportedSceneFingerprint,
                    out exportedSourceFingerprint,
                    out compilerVersion))
                {
                    status.ExportStatus = LevelGridPlayableStatusKindV2.ExportedButStale;
                    status.ExportDetail = "The source package has no valid playable provenance.";
                    return;
                }
                if (!string.Equals(levelId, root.LevelIdText, StringComparison.Ordinal))
                {
                    status.ExportStatus = LevelGridPlayableStatusKindV2.Invalid;
                    status.ExportDetail = "Export provenance belongs to another level ID.";
                    return;
                }
                string currentScene =
                    LevelGridPlayableProvenanceV2.ComputeSceneFingerprint(root);
                string currentSource =
                    LevelGridPlayableProvenanceV2.ComputeSourcePackageFingerprint(absolute);
                if (compilerVersion != LevelGridV2Compiler.CurrentVersion
                    || !string.Equals(
                        currentScene,
                        exportedSceneFingerprint,
                        StringComparison.Ordinal)
                    || !string.Equals(
                        currentSource,
                        exportedSourceFingerprint,
                        StringComparison.Ordinal))
                {
                    status.ExportStatus = LevelGridPlayableStatusKindV2.ExportedButStale;
                    status.ExportDetail = compilerVersion != LevelGridV2Compiler.CurrentVersion
                        ? "The package was exported for a different compiler schema."
                        : !string.Equals(
                            currentScene,
                            exportedSceneFingerprint,
                            StringComparison.Ordinal)
                            ? "Compilation-relevant scene or playable metadata changed."
                            : "An exported JSON sidecar changed outside the canonical export.";
                    return;
                }

                LevelGridV2CompileResult compile =
                    LevelGridV2AssetCompiler.CompileFolder(absolute);
                if (compile == null || !compile.IsValid)
                {
                    status.ExportStatus = LevelGridPlayableStatusKindV2.Invalid;
                    status.ExportDetail = compile != null && compile.Issues.Count > 0
                        ? compile.Issues[0].ToString()
                        : "The exported package does not compile.";
                    return;
                }
                if (!string.Equals(compile.LevelId, root.LevelIdText, StringComparison.Ordinal))
                {
                    status.ExportStatus = LevelGridPlayableStatusKindV2.Invalid;
                    status.ExportDetail = "Compiled source level identity does not match the root.";
                    return;
                }
                status.ExportCurrent = true;
                status.PublishedVersionId =
                    LevelGridV2AssetCompiler.ComputePublishedVersionIdForStatus(
                        compile.Package);
                status.ExportStatus = LevelGridPlayableStatusKindV2.ExportCurrent;
                status.ExportDetail = "Compiler-ready source and provenance are current.";
            }
            catch (Exception exception)
            {
                if (IsFatal(exception)) throw;
                status.ExportStatus = LevelGridPlayableStatusKindV2.Invalid;
                status.ExportDetail = exception.Message;
            }
        }

        private static void EvaluateCompile(LevelGridPlayableStatusV2 status)
        {
            string assetPath = status.Paths.CompiledAssetPath;
            string absolute = LevelGridPlayableBuildPathsV2.ToAbsoluteProjectPath(assetPath);
            if (!File.Exists(absolute))
            {
                status.CompiledStatus = status.ExportCurrent
                    ? LevelGridPlayableStatusKindV2.ExportCurrent
                    : LevelGridPlayableStatusKindV2.CompiledButStale;
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
                RoomContentImportResultV1 import = asset.Import(
                    BuiltInRoomContentObjectCatalogV1.Create());
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
                    ? LevelGridPlayableStatusKindV2.CompiledCurrent
                    : LevelGridPlayableStatusKindV2.CompiledButStale;
                status.CompiledDetail = status.CompiledCurrent
                    ? "The runtime asset references the exact current immutable version."
                    : "The runtime asset is valid but does not reference the current source version.";
            }
            catch (Exception exception)
            {
                if (IsFatal(exception)) throw;
                status.CompiledStatus = LevelGridPlayableStatusKindV2.Invalid;
                status.CompiledDetail = exception.Message;
            }
        }

        private static void EvaluateCatalogue(LevelGridPlayableStatusV2 status)
        {
            ProductionPlayableLevelDefinitionV1 found = null;
            var entries = ProductionPlayableLevelCatalogV1.All;
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
                status.CatalogueStatus = LevelGridPlayableStatusKindV2.NotRegistered;
                status.CatalogueDetail =
                    "No exact stable-ID entry exists in the production catalogue.";
                return;
            }
            if (!string.Equals(
                found.RoomContentResourcePath,
                status.Paths.ResourcePath,
                StringComparison.Ordinal))
            {
                status.CatalogueStatus = LevelGridPlayableStatusKindV2.Invalid;
                status.CatalogueDetail = "The catalogue entry points to '"
                    + found.RoomContentResourcePath
                    + "', not the configured exact Resource asset '"
                    + status.Paths.ResourcePath
                    + "'.";
                return;
            }
            status.Registered = true;
            status.CatalogueStatus = LevelGridPlayableStatusKindV2.Registered;
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

        private static LevelGridPlayableStatusV2 InvalidWithoutPaths(string message)
        {
            return new LevelGridPlayableStatusV2(null)
            {
                AuthoringStatus = LevelGridPlayableStatusKindV2.Invalid,
                MetadataStatus = LevelGridPlayableStatusKindV2.Invalid,
                ExportStatus = LevelGridPlayableStatusKindV2.Invalid,
                CompiledStatus = LevelGridPlayableStatusKindV2.Invalid,
                CatalogueStatus = LevelGridPlayableStatusKindV2.Invalid,
                PlayStatus = LevelGridPlayableStatusKindV2.Invalid,
                AuthoringDetail = message,
                MetadataDetail = message,
                ExportDetail = message,
                CompiledDetail = message,
                CatalogueDetail = message,
                PlayDetail = message,
            };
        }

        private static string FirstBlockingDetail(LevelGridPlayableStatusV2 status)
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