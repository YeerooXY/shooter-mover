#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using ShooterMover.Application.Missions.Rooms.Content;
using ShooterMover.Content.Definitions.Missions.Rooms;
using ShooterMover.UnityAdapters.Authoring.LevelDesign;
using UnityEditor;
using UnityEngine;

namespace ShooterMover.Editor.LevelDesign.Foundation
{
    internal enum LevelGridV2AssetCompilerPublishStep
    {
        AfterVersionTextAssetsImported,
        AfterStagedRuntimeAssetValidated,
        BeforeAuthoritativeAssetSwitch,
        AfterAuthoritativeAssetFileReplaced,
        BeforePostCommitCleanup,
    }

    internal interface ILevelGridV2AssetCompilerFaultInjector
    {
        void OnStep(LevelGridV2AssetCompilerPublishStep step);
    }

    public static partial class LevelGridV2AssetCompiler
    {
        private const string VersionsFolderName = "Versions";
        private const string RuntimeStagesFolderName = "__RuntimeStages";
        private const string PublishingMarkerFileName = ".level-grid-v2-publishing";

        private sealed class DestinationSnapshot
        {
            public DestinationSnapshot(
                bool assetExists,
                string assetHash,
                bool metaExists,
                string metaHash)
            {
                AssetExists = assetExists;
                AssetHash = assetHash;
                MetaExists = metaExists;
                MetaHash = metaHash;
            }

            public bool AssetExists { get; }
            public string AssetHash { get; }
            public bool MetaExists { get; }
            public string MetaHash { get; }
        }

        private sealed class PublishedVersion
        {
            public PublishedVersion(
                string folderPath,
                TextAsset manifest,
                RoomContentJsonDocumentAsset2D[] documents,
                bool createdByTransaction)
            {
                FolderPath = folderPath;
                Manifest = manifest;
                Documents = documents;
                CreatedByTransaction = createdByTransaction;
            }

            public string FolderPath { get; }
            public TextAsset Manifest { get; }
            public RoomContentJsonDocumentAsset2D[] Documents { get; }
            public bool CreatedByTransaction { get; }
        }

        private static JsonRoomContentDefinition2D PublishCompiledPackage(
            RoomContentJsonPackageV1 package,
            string generatedAssetFolder,
            string roomContentAssetPath,
            DestinationSnapshot destinationSnapshot,
            ILevelGridV2AssetCompilerFaultInjector faultInjector)
        {
            string transactionId = Guid.NewGuid().ToString("N");
            string runtimeStageFolder = generatedAssetFolder
                + "/"
                + RuntimeStagesFolderName
                + "/"
                + transactionId;
            string runtimeStageAssetPath = runtimeStageFolder + "/RoomContent.asset";
            PublishedVersion version = null;
            bool committed = false;
            try
            {
                version = PreparePublishedVersion(
                    package,
                    generatedAssetFolder,
                    transactionId,
                    faultInjector);
                JsonRoomContentDefinition2D stagedAsset = CreateAndValidateRuntimeStage(
                    runtimeStageAssetPath,
                    roomContentAssetPath,
                    version.Manifest,
                    version.Documents);
                Inject(
                    faultInjector,
                    LevelGridV2AssetCompilerPublishStep.AfterStagedRuntimeAssetValidated);

                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                LoadAndValidateVersion(
                    package,
                    version.FolderPath,
                    version.CreatedByTransaction);
                AssetDatabase.SaveAssets();
                AssetDatabase.ImportAsset(
                    runtimeStageAssetPath,
                    ImportAssetOptions.ForceSynchronousImport);
                stagedAsset = AssetDatabase.LoadAssetAtPath<JsonRoomContentDefinition2D>(
                    runtimeStageAssetPath);
                ValidateRuntimeAsset(stagedAsset, runtimeStageAssetPath);

                Inject(
                    faultInjector,
                    LevelGridV2AssetCompilerPublishStep.BeforeAuthoritativeAssetSwitch);
                JsonRoomContentDefinition2D published = ReplaceAuthoritativeAssetAtomically(
                    runtimeStageAssetPath,
                    roomContentAssetPath,
                    destinationSnapshot,
                    faultInjector);
                committed = true;

                TryPostCommitCleanup(
                    generatedAssetFolder,
                    runtimeStageFolder,
                    version.FolderPath,
                    faultInjector);
                return published;
            }
            catch
            {
                TryDeleteAssetOrFolder(runtimeStageFolder);
                if (!committed
                    && version != null
                    && version.CreatedByTransaction)
                {
                    TryRemovePublishingMarker(version.FolderPath);
                    TryDeleteVersionIfUnreferenced(version.FolderPath);
                }
                throw;
            }
        }

        private static PublishedVersion PreparePublishedVersion(
            RoomContentJsonPackageV1 package,
            string generatedAssetFolder,
            string transactionId,
            ILevelGridV2AssetCompilerFaultInjector faultInjector)
        {
            string versionId = ComputePackageVersionId(package);
            string versionsRoot = generatedAssetFolder + "/" + VersionsFolderName;
            string finalFolder = versionsRoot + "/v-" + versionId;
            string stagingFolder = finalFolder + ".__staging-" + transactionId;

            EnsureAssetFolder(versionsRoot);
            if (AssetDatabase.IsValidFolder(finalFolder))
            {
                string markerPath = finalFolder + "/" + PublishingMarkerFileName;
                bool hasMarker = File.Exists(ToAbsolutePath(markerPath));
                if (hasMarker)
                {
                    HashSet<string> references = FindAllReferencedRoomContentAssetPaths();
                    if (!HasReferenceBelow(finalFolder, references))
                    {
                        throw new InvalidOperationException(
                            "Generated version is still marked as publishing and is not "
                            + "referenced by a committed runtime asset: "
                            + finalFolder);
                    }
                }
                PublishedVersion existing = LoadAndValidateVersion(
                    package,
                    finalFolder,
                    false);
                if (hasMarker) TryRemovePublishingMarker(finalFolder);
                return existing;
            }

            if (Directory.Exists(ToAbsolutePath(finalFolder)))
            {
                throw new InvalidOperationException(
                    "Generated version path exists but is not a valid Unity folder: " + finalFolder);
            }

            bool movedToFinal = false;
            try
            {
                EnsureAssetFolder(stagingFolder);
                WriteVersionFiles(package, stagingFolder);
                WritePublishingMarker(stagingFolder, transactionId);
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                LoadAndValidateVersion(package, stagingFolder, true);
                Inject(
                    faultInjector,
                    LevelGridV2AssetCompilerPublishStep.AfterVersionTextAssetsImported);

                string moveError = AssetDatabase.MoveAsset(stagingFolder, finalFolder);
                if (!string.IsNullOrEmpty(moveError))
                {
                    throw new InvalidOperationException(
                        "Could not publish generated version folder: " + moveError);
                }
                movedToFinal = true;

                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                return LoadAndValidateVersion(package, finalFolder, true);
            }
            catch
            {
                TryDeleteAssetOrFolder(stagingFolder);
                if (movedToFinal)
                {
                    TryRemovePublishingMarker(finalFolder);
                    TryDeleteVersionIfUnreferenced(finalFolder);
                }
                throw;
            }
        }

        private static void WriteVersionFiles(
            RoomContentJsonPackageV1 package,
            string folderPath)
        {
            List<string> keys = SortedKeys(package);
            WriteAssetText(folderPath + "/compiled.manifest.json", package.ManifestJson);
            for (int index = 0; index < keys.Count; index++)
            {
                WriteAssetText(
                    DocumentAssetPath(folderPath, index, keys[index]),
                    package.Documents[keys[index]]);
            }
        }

        private static PublishedVersion LoadAndValidateVersion(
            RoomContentJsonPackageV1 package,
            string folderPath,
            bool createdByTransaction)
        {
            string manifestPath = folderPath + "/compiled.manifest.json";
            TextAsset manifest = AssetDatabase.LoadAssetAtPath<TextAsset>(manifestPath);
            RequireTextAssetContent(manifest, manifestPath, package.ManifestJson);

            List<string> keys = SortedKeys(package);
            var documents = new RoomContentJsonDocumentAsset2D[keys.Count];
            for (int index = 0; index < keys.Count; index++)
            {
                string path = DocumentAssetPath(folderPath, index, keys[index]);
                TextAsset text = AssetDatabase.LoadAssetAtPath<TextAsset>(path);
                RequireTextAssetContent(text, path, package.Documents[keys[index]]);
                var entry = new RoomContentJsonDocumentAsset2D();
                entry.ConfigureCompiledAsset(keys[index], text);
                documents[index] = entry;
            }

            var candidate = ScriptableObject.CreateInstance<JsonRoomContentDefinition2D>();
            try
            {
                candidate.ConfigureCompiledAssets(manifest, documents);
                ValidateRuntimeAsset(candidate, folderPath);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(candidate);
            }

            return new PublishedVersion(folderPath, manifest, documents, createdByTransaction);
        }

        private static JsonRoomContentDefinition2D CreateAndValidateRuntimeStage(
            string runtimeStageAssetPath,
            string roomContentAssetPath,
            TextAsset manifest,
            RoomContentJsonDocumentAsset2D[] documents)
        {
            EnsureAssetFolder(Path.GetDirectoryName(runtimeStageAssetPath).Replace('\\', '/'));
            var stage = ScriptableObject.CreateInstance<JsonRoomContentDefinition2D>();
            stage.name = Path.GetFileNameWithoutExtension(roomContentAssetPath);
            stage.ConfigureCompiledAssets(manifest, documents);
            AssetDatabase.CreateAsset(stage, runtimeStageAssetPath);
            EditorUtility.SetDirty(stage);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(
                runtimeStageAssetPath,
                ImportAssetOptions.ForceSynchronousImport);

            JsonRoomContentDefinition2D imported =
                AssetDatabase.LoadAssetAtPath<JsonRoomContentDefinition2D>(runtimeStageAssetPath);
            ValidateRuntimeAsset(imported, runtimeStageAssetPath);
            return imported;
        }

        private static JsonRoomContentDefinition2D ReplaceAuthoritativeAssetAtomically(
            string stagedAssetPath,
            string destinationAssetPath,
            DestinationSnapshot destinationSnapshot,
            ILevelGridV2AssetCompilerFaultInjector faultInjector)
        {
            EnsureAssetFolder(Path.GetDirectoryName(destinationAssetPath).Replace('\\', '/'));
            string stagedAbsolute = ToAbsolutePath(stagedAssetPath);
            string destinationAbsolute = ToAbsolutePath(destinationAssetPath);
            string token = Guid.NewGuid().ToString("N");
            string publishTemporary = destinationAbsolute + ".publish-" + token;
            string backupPath = destinationAbsolute + ".backup-" + token;
            RequireDestinationUnchanged(
                destinationAssetPath,
                destinationAbsolute,
                destinationSnapshot);
            bool destinationExisted = destinationSnapshot.AssetExists;
            bool fileReplaced = false;
            Exception originalFailure = null;

            File.Copy(stagedAbsolute, publishTemporary, false);
            try
            {
                if (destinationExisted)
                {
                    File.Replace(publishTemporary, destinationAbsolute, backupPath);
                }
                else
                {
                    File.Move(publishTemporary, destinationAbsolute);
                }
                fileReplaced = true;

                Inject(
                    faultInjector,
                    LevelGridV2AssetCompilerPublishStep.AfterAuthoritativeAssetFileReplaced);
                AssetDatabase.ImportAsset(
                    destinationAssetPath,
                    ImportAssetOptions.ForceSynchronousImport);
                JsonRoomContentDefinition2D published =
                    AssetDatabase.LoadAssetAtPath<JsonRoomContentDefinition2D>(
                        destinationAssetPath);
                ValidateRuntimeAsset(published, destinationAssetPath);
                TryDeleteFile(backupPath);
                return published;
            }
            catch (Exception exception)
            {
                originalFailure = exception;
                if (!fileReplaced) throw;

                try
                {
                    RestoreAuthoritativeAsset(
                        destinationAssetPath,
                        destinationAbsolute,
                        backupPath,
                        destinationExisted);
                }
                catch (Exception rollbackFailure)
                {
                    throw new AggregateException(
                        "Level Grid V2 publication failed and rollback could not be verified.",
                        originalFailure,
                        rollbackFailure);
                }
                throw;
            }
            finally
            {
                TryDeleteFile(publishTemporary);
                if (originalFailure == null) TryDeleteFile(backupPath);
            }
        }

        private static void RestoreAuthoritativeAsset(
            string destinationAssetPath,
            string destinationAbsolute,
            string backupPath,
            bool destinationExisted)
        {
            if (destinationExisted)
            {
                if (!File.Exists(backupPath))
                {
                    throw new IOException(
                        "The previous authoritative asset backup is missing: " + backupPath);
                }
                if (File.Exists(destinationAbsolute))
                {
                    File.Replace(backupPath, destinationAbsolute, null);
                }
                else
                {
                    File.Move(backupPath, destinationAbsolute);
                }
                AssetDatabase.ImportAsset(
                    destinationAssetPath,
                    ImportAssetOptions.ForceSynchronousImport);
                JsonRoomContentDefinition2D restored =
                    AssetDatabase.LoadAssetAtPath<JsonRoomContentDefinition2D>(
                        destinationAssetPath);
                ValidateRuntimeAsset(restored, destinationAssetPath + " (rolled back)");
                return;
            }

            if (File.Exists(destinationAbsolute)) File.Delete(destinationAbsolute);
            string metaPath = destinationAbsolute + ".meta";
            if (File.Exists(metaPath)) File.Delete(metaPath);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            if (File.Exists(destinationAbsolute)
                || AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(destinationAssetPath) != null)
            {
                throw new IOException(
                    "A newly created authoritative asset could not be removed during rollback: "
                    + destinationAssetPath);
            }
        }

        private static DestinationSnapshot ValidateExistingDestination(
            string roomContentAssetPath)
        {
            string absolutePath = ToAbsolutePath(roomContentAssetPath);
            string metaPath = absolutePath + ".meta";
            bool assetExists = File.Exists(absolutePath);
            bool metaExists = File.Exists(metaPath);
            if (assetExists)
            {
                if (!metaExists)
                {
                    throw new InvalidOperationException(
                        "The authoritative destination exists without Unity metadata; refusing "
                        + "to replace it because its stable GUID cannot be preserved: "
                        + roomContentAssetPath);
                }
                JsonRoomContentDefinition2D existing =
                    AssetDatabase.LoadAssetAtPath<JsonRoomContentDefinition2D>(roomContentAssetPath);
                if (existing == null)
                {
                    throw new InvalidOperationException(
                        "The authoritative destination already exists but is not a loadable "
                        + nameof(JsonRoomContentDefinition2D)
                        + ": "
                        + roomContentAssetPath);
                }
                ValidateRuntimeAsset(existing, roomContentAssetPath + " (current authority)");
            }
            else if (metaExists)
            {
                throw new InvalidOperationException(
                    "The authoritative destination has orphaned Unity metadata: "
                    + roomContentAssetPath
                    + ".meta");
            }

            return new DestinationSnapshot(
                assetExists,
                assetExists ? ComputeFileHash(absolutePath) : string.Empty,
                metaExists,
                metaExists ? ComputeFileHash(metaPath) : string.Empty);
        }

        private static void RequireDestinationUnchanged(
            string destinationAssetPath,
            string destinationAbsolute,
            DestinationSnapshot expected)
        {
            string metaPath = destinationAbsolute + ".meta";
            bool assetExists = File.Exists(destinationAbsolute);
            bool metaExists = File.Exists(metaPath);
            string assetHash = assetExists ? ComputeFileHash(destinationAbsolute) : string.Empty;
            string metaHash = metaExists ? ComputeFileHash(metaPath) : string.Empty;
            if (assetExists != expected.AssetExists
                || metaExists != expected.MetaExists
                || !string.Equals(assetHash, expected.AssetHash, StringComparison.Ordinal)
                || !string.Equals(metaHash, expected.MetaHash, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "The authoritative room-content asset changed during compilation; refusing "
                    + "to overwrite the external change: "
                    + destinationAssetPath);
            }
        }

        private static string ComputeFileHash(string path)
        {
            using (SHA256 sha = SHA256.Create())
            using (FileStream stream = File.OpenRead(path))
            {
                byte[] hash = sha.ComputeHash(stream);
                var result = new StringBuilder(hash.Length * 2);
                for (int index = 0; index < hash.Length; index++)
                {
                    result.Append(hash[index].ToString("x2"));
                }
                return result.ToString();
            }
        }

        private static void ValidateRuntimeAsset(
            JsonRoomContentDefinition2D asset,
            string diagnosticPath)
        {
            if (asset == null)
            {
                throw new InvalidOperationException(
                    "Runtime room-content asset did not import: " + diagnosticPath);
            }
            RoomContentImportResultV1 imported = asset.Import(
                BuiltInRoomContentObjectCatalogV1.Create());
            ThrowIfImportInvalid(
                imported,
                "Runtime room-content asset failed validation: " + diagnosticPath);
        }

        private static void ThrowIfImportInvalid(
            RoomContentImportResultV1 validation,
            string fallbackMessage)
        {
            if (validation != null && validation.IsValid) return;
            RoomContentImportIssueV1 issue = validation != null
                && validation.Issues.Count > 0
                ? validation.Issues[0]
                : new RoomContentImportIssueV1(
                    "level-grid-v2-runtime-validation-missing",
                    "$",
                    fallbackMessage);
            throw new InvalidOperationException(
                issue.Code + " at " + issue.Path + ": " + issue.Message);
        }
    }
}
#endif
