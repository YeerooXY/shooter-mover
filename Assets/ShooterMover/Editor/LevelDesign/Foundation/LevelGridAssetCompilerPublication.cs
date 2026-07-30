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
    internal enum LevelGridAssetCompilerPublishStep
    {
        AfterVersionTextAssetsImported,
        AfterStagedRuntimeAssetValidated,
        BeforeAuthoritativeAssetSwitch,
        AfterAuthoritativeAssetFileReplaced,
        BeforePostCommitCleanup,
    }

    internal interface ILevelGridAssetCompilerFaultInjector
    {
        void OnStep(LevelGridAssetCompilerPublishStep step);
    }

    public static partial class LevelGridAssetCompiler
    {
        private const string VersionsFolderName = "Versions";
        private const string RuntimeStagesFolderName = "__RuntimeStages";
        private const string PublishingMarkerFileName = ".level-level-1-publishing";

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
                RoomDocument[] documents,
                bool createdByTransaction)
            {
                FolderPath = folderPath;
                Manifest = manifest;
                Documents = documents;
                CreatedByTransaction = createdByTransaction;
            }

            public string FolderPath { get; }
            public TextAsset Manifest { get; }
            public RoomDocument[] Documents { get; }
            public bool CreatedByTransaction { get; }
        }

        private static RoomFile PublishCompiledPackage(
            RoomContentJsonPackage package,
            string generatedAssetFolder,
            string roomContentAssetPath,
            DestinationSnapshot destinationSnapshot,
            ILevelGridAssetCompilerFaultInjector faultInjector)
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
                RoomFile stagedAsset = CreateAndValidateRuntimeStage(
                    runtimeStageAssetPath,
                    roomContentAssetPath,
                    version.Manifest,
                    version.Documents);
                Inject(
                    faultInjector,
                    LevelGridAssetCompilerPublishStep.AfterStagedRuntimeAssetValidated);

                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                LoadAndValidateVersion(
                    package,
                    version.FolderPath,
                    version.CreatedByTransaction);
                AssetDatabase.SaveAssets();
                AssetDatabase.ImportAsset(
                    runtimeStageAssetPath,
                    ImportAssetOptions.ForceSynchronousImport);
                stagedAsset = AssetDatabase.LoadAssetAtPath<RoomFile>(
                    runtimeStageAssetPath);
                ValidateRuntimeAsset(stagedAsset, runtimeStageAssetPath);

                Inject(
                    faultInjector,
                    LevelGridAssetCompilerPublishStep.BeforeAuthoritativeAssetSwitch);
                RoomFile published = ReplaceAuthoritativeAssetAtomically(
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
            RoomContentJsonPackage package,
            string generatedAssetFolder,
            string transactionId,
            ILevelGridAssetCompilerFaultInjector faultInjector)
        {
            string versionId = ComputePackageVersionId(package);
            string versionsRoot = generatedAssetFolder + "/" + VersionsFolderName;
            string finalFolder = versionsRoot + "/v-" + versionId;
            // Keep the staging folder extension-free. Unity treats dotted directory names under
            // Assets inconsistently during synchronous imports and can remove the directory
            // between sidecar writes.
            string stagingFolder = versionsRoot
                + "/__staging-"
                + transactionId
                + "-v-"
                + versionId;

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
            string externalStaging = Path.Combine(
                Path.GetTempPath(),
                "ShooterMover-LevelPublish-" + transactionId);
            try
            {
                // Build the immutable version completely outside Assets, then expose the
                // completed directory to Unity in one move. Unity's asset watcher can otherwise
                // remove an unimported staging directory between immediate sidecar writes.
                AssetDatabase.DisallowAutoRefresh();
                try
                {
                    if (Directory.Exists(externalStaging))
                        Directory.Delete(externalStaging, true);
                    Directory.CreateDirectory(externalStaging);
                    WriteVersionFilesToDirectory(package, externalStaging);
                    File.WriteAllText(
                        Path.Combine(externalStaging, PublishingMarkerFileName),
                        transactionId + Environment.NewLine,
                        Utf8WithoutBom);

                    string stagingAbsolute = ToAbsolutePath(stagingFolder);
                    if (Directory.Exists(stagingAbsolute))
                        Directory.Delete(stagingAbsolute, true);
                    Directory.Move(externalStaging, stagingAbsolute);
                }
                finally
                {
                    if (Directory.Exists(externalStaging))
                        Directory.Delete(externalStaging, true);
                    AssetDatabase.AllowAutoRefresh();
                }
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                LoadAndValidateVersion(package, stagingFolder, true);
                Inject(
                    faultInjector,
                    LevelGridAssetCompilerPublishStep.AfterVersionTextAssetsImported);

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
            RoomContentJsonPackage package,
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

        private static void WriteVersionFilesToDirectory(
            RoomContentJsonPackage package,
            string absoluteFolder)
        {
            File.WriteAllText(
                Path.Combine(absoluteFolder, "compiled.manifest.json"),
                package.ManifestJson + Environment.NewLine,
                Utf8WithoutBom);
            List<string> keys = SortedKeys(package);
            for (int index = 0; index < keys.Count; index++)
            {
                string assetPath = DocumentAssetPath("Assets", index, keys[index]);
                File.WriteAllText(
                    Path.Combine(absoluteFolder, Path.GetFileName(assetPath)),
                    package.Documents[keys[index]] + Environment.NewLine,
                    Utf8WithoutBom);
            }
        }

        private static PublishedVersion LoadAndValidateVersion(
            RoomContentJsonPackage package,
            string folderPath,
            bool createdByTransaction)
        {
            string manifestPath = folderPath + "/compiled.manifest.json";
            TextAsset manifest = AssetDatabase.LoadAssetAtPath<TextAsset>(manifestPath);
            RequireTextAssetContent(manifest, manifestPath, package.ManifestJson);

            List<string> keys = SortedKeys(package);
            var documents = new RoomDocument[keys.Count];
            for (int index = 0; index < keys.Count; index++)
            {
                string path = DocumentAssetPath(folderPath, index, keys[index]);
                TextAsset text = AssetDatabase.LoadAssetAtPath<TextAsset>(path);
                RequireTextAssetContent(text, path, package.Documents[keys[index]]);
                var entry = new RoomDocument();
                entry.ConfigureCompiledAsset(keys[index], text);
                documents[index] = entry;
            }

            var candidate = ScriptableObject.CreateInstance<RoomFile>();
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

        private static RoomFile CreateAndValidateRuntimeStage(
            string runtimeStageAssetPath,
            string roomContentAssetPath,
            TextAsset manifest,
            RoomDocument[] documents)
        {
            EnsureAssetFolder(Path.GetDirectoryName(runtimeStageAssetPath).Replace('\\', '/'));
            var stage = ScriptableObject.CreateInstance<RoomFile>();
            stage.name = Path.GetFileNameWithoutExtension(roomContentAssetPath);
            stage.ConfigureCompiledAssets(manifest, documents);
            AssetDatabase.CreateAsset(stage, runtimeStageAssetPath);
            EditorUtility.SetDirty(stage);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(
                runtimeStageAssetPath,
                ImportAssetOptions.ForceSynchronousImport);

            RoomFile imported =
                AssetDatabase.LoadAssetAtPath<RoomFile>(runtimeStageAssetPath);
            ValidateRuntimeAsset(imported, runtimeStageAssetPath);
            return imported;
        }

        private static RoomFile ReplaceAuthoritativeAssetAtomically(
            string stagedAssetPath,
            string destinationAssetPath,
            DestinationSnapshot destinationSnapshot,
            ILevelGridAssetCompilerFaultInjector faultInjector)
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
                    LevelGridAssetCompilerPublishStep.AfterAuthoritativeAssetFileReplaced);
                AssetDatabase.ImportAsset(
                    destinationAssetPath,
                    ImportAssetOptions.ForceSynchronousImport);
                RoomFile published =
                    AssetDatabase.LoadAssetAtPath<RoomFile>(
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
                        "Level Level publication failed and rollback could not be verified.",
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
                RoomFile restored =
                    AssetDatabase.LoadAssetAtPath<RoomFile>(
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
                RoomFile existing =
                    AssetDatabase.LoadAssetAtPath<RoomFile>(roomContentAssetPath);
                if (existing == null)
                {
                    throw new InvalidOperationException(
                        "The authoritative destination already exists but is not a loadable "
                        + nameof(RoomFile)
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
            RoomFile asset,
            string diagnosticPath)
        {
            if (asset == null)
            {
                throw new InvalidOperationException(
                    "Runtime room-content asset did not import: " + diagnosticPath);
            }
            RoomContentImportResult imported = asset.Import(
                BuiltInRoomContentObjectCatalog.Create());
            ThrowIfImportInvalid(
                imported,
                "Runtime room-content asset failed validation: " + diagnosticPath);
        }

        private static void ThrowIfImportInvalid(
            RoomContentImportResult validation,
            string fallbackMessage)
        {
            if (validation != null && validation.IsValid) return;
            RoomContentImportIssue issue = validation != null
                && validation.Issues.Count > 0
                ? validation.Issues[0]
                : new RoomContentImportIssue(
                    "level-level-1-runtime-validation-missing",
                    "$",
                    fallbackMessage);
            throw new InvalidOperationException(
                issue.Code + " at " + issue.Path + ": " + issue.Message);
        }
    }
}
#endif
