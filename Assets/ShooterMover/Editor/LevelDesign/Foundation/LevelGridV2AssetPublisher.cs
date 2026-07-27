#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using ShooterMover.Application.Missions.Rooms.Content;
using ShooterMover.UnityAdapters.Authoring.LevelDesign;
using UnityEditor;
using UnityEngine;

namespace ShooterMover.Editor.LevelDesign.Foundation
{
    internal enum LevelGridV2AssetPublishFailurePoint
    {
        AfterVersionFilesWritten,
        AfterVersionAssetsImported,
        AfterCandidateAssetSaved,
        AfterAuthoritativeFileReplaced,
        AfterAuthoritativeAssetImported,
        BeforePostCommitCleanup,
    }

    /// <summary>
    /// Publishes one already-validated compiled package without mutating the previously playable
    /// Resource asset or any TextAsset it references before the candidate is complete.
    /// </summary>
    internal static class LevelGridV2AssetPublisher
    {
        private static readonly UTF8Encoding Utf8WithoutBom = new UTF8Encoding(false);
        private static LevelGridV2AssetPublishFailurePoint? injectedFailurePoint;

        public static JsonRoomContentDefinition2D Publish(
            RoomContentJsonPackageV1 package,
            string generatedAssetFolder,
            string roomContentAssetPath)
        {
            if (package == null) throw new ArgumentNullException(nameof(package));
            ValidateAssetPath(generatedAssetFolder, nameof(generatedAssetFolder));
            ValidateAssetPath(roomContentAssetPath, nameof(roomContentAssetPath));

            var keys = new List<string>(package.Documents.Keys);
            keys.Sort(StringComparer.Ordinal);
            string versionId = ComputeVersionId(package, keys);
            string versionsFolder = generatedAssetFolder + "/Versions";
            string versionFolder = versionsFolder + "/" + versionId;
            bool versionCreated = false;
            string candidatePath = null;

            try
            {
                VersionAssets version = PrepareVersion(
                    package,
                    keys,
                    versionFolder,
                    out versionCreated);
                ThrowIfInjected(LevelGridV2AssetPublishFailurePoint.AfterVersionAssetsImported);

                candidatePath = CreateCandidateAsset(
                    generatedAssetFolder,
                    versionId,
                    roomContentAssetPath,
                    version.Manifest,
                    version.Documents);
                ThrowIfInjected(LevelGridV2AssetPublishFailurePoint.AfterCandidateAssetSaved);

                JsonRoomContentDefinition2D candidate =
                    AssetDatabase.LoadAssetAtPath<JsonRoomContentDefinition2D>(candidatePath);
                ValidateRuntimeAsset(candidate, "staged candidate");

                JsonRoomContentDefinition2D committed = ReplaceAuthoritativeAsset(
                    candidatePath,
                    roomContentAssetPath);

                TryRunPostCommitCleanup(
                    generatedAssetFolder,
                    versionFolder,
                    candidatePath);
                return committed;
            }
            catch
            {
                TryDeleteAsset(candidatePath);
                if (versionCreated)
                {
                    TryDeleteAsset(versionFolder);
                }
                throw;
            }
        }

        internal static IDisposable InjectFailureForTests(
            LevelGridV2AssetPublishFailurePoint failurePoint)
        {
            LevelGridV2AssetPublishFailurePoint? previous = injectedFailurePoint;
            injectedFailurePoint = failurePoint;
            return new FailureInjectionScope(previous);
        }

        private static VersionAssets PrepareVersion(
            RoomContentJsonPackageV1 package,
            IList<string> keys,
            string versionFolder,
            out bool versionCreated)
        {
            string manifestPath = versionFolder + "/compiled.manifest.json";
            var documentPaths = new Dictionary<string, string>(StringComparer.Ordinal);
            var expected = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [manifestPath] = WithTrailingNewLine(package.ManifestJson),
            };
            for (int index = 0; index < keys.Count; index++)
            {
                string path = versionFolder
                    + "/"
                    + index.ToString("00")
                    + "_"
                    + SanitizeFileName(keys[index])
                    + ".json";
                documentPaths.Add(keys[index], path);
                expected.Add(path, WithTrailingNewLine(package.Documents[keys[index]]));
            }

            string absoluteVersion = ToAbsolutePath(versionFolder);
            versionCreated = !Directory.Exists(absoluteVersion);
            if (versionCreated)
            {
                EnsureAssetFolder(versionFolder);
                foreach (KeyValuePair<string, string> entry in expected)
                {
                    WriteNewAssetText(entry.Key, entry.Value);
                }
                ThrowIfInjected(LevelGridV2AssetPublishFailurePoint.AfterVersionFilesWritten);
            }
            else
            {
                ValidateExistingVersion(versionFolder, expected);
            }

            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            TextAsset manifest = LoadRequiredTextAsset(manifestPath, "manifest");
            var documents = new RoomContentJsonDocumentAsset2D[keys.Count];
            for (int index = 0; index < keys.Count; index++)
            {
                string key = keys[index];
                TextAsset text = LoadRequiredTextAsset(documentPaths[key], "document");
                var entry = new RoomContentJsonDocumentAsset2D();
                entry.ConfigureCompiledAsset(key, text);
                documents[index] = entry;
            }
            return new VersionAssets(manifest, documents);
        }

        private static string CreateCandidateAsset(
            string generatedAssetFolder,
            string versionId,
            string roomContentAssetPath,
            TextAsset manifest,
            RoomContentJsonDocumentAsset2D[] documents)
        {
            string transactionFolder = generatedAssetFolder + "/Transactions";
            EnsureAssetFolder(transactionFolder);
            string candidatePath = transactionFolder
                + "/candidate-"
                + versionId
                + "-"
                + Guid.NewGuid().ToString("N")
                + ".asset";

            JsonRoomContentDefinition2D candidate =
                ScriptableObject.CreateInstance<JsonRoomContentDefinition2D>();
            candidate.name = Path.GetFileNameWithoutExtension(roomContentAssetPath);
            candidate.ConfigureCompiledAssets(manifest, documents);
            AssetDatabase.CreateAsset(candidate, candidatePath);
            EditorUtility.SetDirty(candidate);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(
                candidatePath,
                ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);

            JsonRoomContentDefinition2D imported =
                AssetDatabase.LoadAssetAtPath<JsonRoomContentDefinition2D>(candidatePath);
            if (imported == null)
            {
                throw new InvalidOperationException(
                    "The staged room-content candidate did not import: " + candidatePath);
            }
            return candidatePath;
        }

        private static JsonRoomContentDefinition2D ReplaceAuthoritativeAsset(
            string candidatePath,
            string roomContentAssetPath)
        {
            EnsureAssetFolder(Path.GetDirectoryName(roomContentAssetPath).Replace('\\', '/'));
            AssetDatabase.SaveAssets();

            string candidateAbsolute = ToAbsolutePath(candidatePath);
            string resourceAbsolute = ToAbsolutePath(roomContentAssetPath);
            bool hadPrevious = File.Exists(resourceAbsolute);
            string rollbackRoot = Path.Combine(
                Directory.GetParent(Application.dataPath).FullName,
                "Temp",
                "ShooterMover",
                "LevelGridV2AssetPublish");
            Directory.CreateDirectory(rollbackRoot);
            string rollbackPath = Path.Combine(
                rollbackRoot,
                Path.GetFileName(roomContentAssetPath)
                    + ".rollback-"
                    + Guid.NewGuid().ToString("N"));
            string failedCandidatePath = rollbackPath + ".failed-candidate";
            bool replaced = false;

            try
            {
                if (hadPrevious)
                {
                    File.Replace(candidateAbsolute, resourceAbsolute, rollbackPath);
                }
                else
                {
                    File.Move(candidateAbsolute, resourceAbsolute);
                }
                replaced = true;
                DeleteFileIfExists(candidateAbsolute + ".meta");
                ThrowIfInjected(
                    LevelGridV2AssetPublishFailurePoint.AfterAuthoritativeFileReplaced);

                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                AssetDatabase.ImportAsset(
                    roomContentAssetPath,
                    ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
                JsonRoomContentDefinition2D committed =
                    AssetDatabase.LoadAssetAtPath<JsonRoomContentDefinition2D>(
                        roomContentAssetPath);
                ThrowIfInjected(
                    LevelGridV2AssetPublishFailurePoint.AfterAuthoritativeAssetImported);
                ValidateRuntimeAsset(committed, "committed Resource asset");
                TryDeleteFile(rollbackPath);
                return committed;
            }
            catch (Exception publishException)
            {
                if (!replaced)
                {
                    throw;
                }

                try
                {
                    RestorePreviousAuthoritativeAsset(
                        roomContentAssetPath,
                        resourceAbsolute,
                        rollbackPath,
                        failedCandidatePath,
                        hadPrevious);
                }
                catch (Exception rollbackException)
                {
                    throw new AggregateException(
                        "Level Grid V2 publication failed and rollback also failed.",
                        publishException,
                        rollbackException);
                }
                throw;
            }
        }

        private static void RestorePreviousAuthoritativeAsset(
            string roomContentAssetPath,
            string resourceAbsolute,
            string rollbackPath,
            string failedCandidatePath,
            bool hadPrevious)
        {
            if (hadPrevious)
            {
                if (!File.Exists(rollbackPath))
                {
                    throw new IOException(
                        "The previous Resource asset rollback file is missing: "
                        + rollbackPath);
                }
                DeleteFileIfExists(failedCandidatePath);
                if (File.Exists(resourceAbsolute))
                {
                    File.Replace(rollbackPath, resourceAbsolute, failedCandidatePath);
                }
                else
                {
                    File.Move(rollbackPath, resourceAbsolute);
                }
            }
            else
            {
                DeleteFileIfExists(resourceAbsolute);
            }

            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            if (hadPrevious)
            {
                AssetDatabase.ImportAsset(
                    roomContentAssetPath,
                    ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
                JsonRoomContentDefinition2D restored =
                    AssetDatabase.LoadAssetAtPath<JsonRoomContentDefinition2D>(
                        roomContentAssetPath);
                ValidateRuntimeAsset(restored, "restored previous Resource asset");
            }
            else if (AssetDatabase.LoadAssetAtPath<JsonRoomContentDefinition2D>(
                roomContentAssetPath) != null)
            {
                throw new InvalidOperationException(
                    "Rollback could not remove the newly-created Resource asset.");
            }
            TryDeleteFile(failedCandidatePath);
        }

        private static void ValidateRuntimeAsset(
            JsonRoomContentDefinition2D asset,
            string label)
        {
            if (asset == null)
            {
                throw new InvalidOperationException(
                    "The " + label + " is missing after import.");
            }

            RoomContentImportResultV1 result = asset.Import();
            if (result != null && result.IsValid) return;

            RoomContentImportIssueV1 issue = result != null && result.Issues.Count > 0
                ? result.Issues[0]
                : new RoomContentImportIssueV1(
                    "level-grid-v2-published-asset-invalid",
                    "$",
                    "The published asset failed without a structured import issue.");
            throw new InvalidOperationException(
                "The " + label + " failed runtime import validation: "
                + issue.Code + " at " + issue.Path + ": " + issue.Message);
        }

        private static void ValidateExistingVersion(
            string versionFolder,
            IReadOnlyDictionary<string, string> expected)
        {
            string absoluteFolder = ToAbsolutePath(versionFolder);
            string[] existing = Directory.GetFiles(
                absoluteFolder,
                "*.json",
                SearchOption.TopDirectoryOnly);
            if (existing.Length != expected.Count)
            {
                throw new InvalidOperationException(
                    "Existing immutable generated version has an unexpected JSON file count: "
                    + versionFolder);
            }

            foreach (KeyValuePair<string, string> entry in expected)
            {
                string absolute = ToAbsolutePath(entry.Key);
                if (!File.Exists(absolute)
                    || !string.Equals(
                        File.ReadAllText(absolute),
                        entry.Value,
                        StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        "Existing immutable generated version does not match its content hash: "
                        + entry.Key);
                }
            }
        }

        private static TextAsset LoadRequiredTextAsset(string path, string kind)
        {
            AssetDatabase.ImportAsset(
                path,
                ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            TextAsset asset = AssetDatabase.LoadAssetAtPath<TextAsset>(path);
            if (asset == null)
            {
                throw new InvalidOperationException(
                    "Generated " + kind + " did not import as a TextAsset: " + path);
            }
            return asset;
        }

        private static void TryRunPostCommitCleanup(
            string generatedAssetFolder,
            string activeVersionFolder,
            string candidatePath)
        {
            try
            {
                ThrowIfInjected(LevelGridV2AssetPublishFailurePoint.BeforePostCommitCleanup);
                DeleteLegacyGeneratedJson(generatedAssetFolder);
                DeleteObsoleteVersions(generatedAssetFolder, activeVersionFolder);
                TryDeleteAsset(candidatePath);
                TryDeleteEmptyTransactionFolder(generatedAssetFolder + "/Transactions");
            }
            catch (Exception exception)
            {
                if (IsFatal(exception)) throw;
                Debug.LogWarning(
                    "Level Grid V2 published successfully, but generated cleanup failed: "
                    + exception.Message);
            }
        }

        private static void DeleteLegacyGeneratedJson(string generatedAssetFolder)
        {
            string absoluteFolder = ToAbsolutePath(generatedAssetFolder);
            if (!Directory.Exists(absoluteFolder)) return;
            string[] legacy = Directory.GetFiles(
                absoluteFolder,
                "*.json",
                SearchOption.TopDirectoryOnly);
            for (int index = 0; index < legacy.Length; index++)
            {
                string path = generatedAssetFolder + "/" + Path.GetFileName(legacy[index]);
                if (!AssetDatabase.DeleteAsset(path))
                {
                    DeleteFileIfExists(legacy[index]);
                    DeleteFileIfExists(legacy[index] + ".meta");
                }
            }
        }

        private static void DeleteObsoleteVersions(
            string generatedAssetFolder,
            string activeVersionFolder)
        {
            string versionsFolder = generatedAssetFolder + "/Versions";
            string absoluteVersions = ToAbsolutePath(versionsFolder);
            if (!Directory.Exists(absoluteVersions)) return;
            string[] versions = Directory.GetDirectories(
                absoluteVersions,
                "*",
                SearchOption.TopDirectoryOnly);
            for (int index = 0; index < versions.Length; index++)
            {
                string path = versionsFolder + "/" + Path.GetFileName(versions[index]);
                if (string.Equals(path, activeVersionFolder, StringComparison.Ordinal)) continue;
                if (!AssetDatabase.DeleteAsset(path))
                {
                    Directory.Delete(versions[index], true);
                    DeleteFileIfExists(versions[index] + ".meta");
                }
            }
        }

        private static void TryDeleteEmptyTransactionFolder(string transactionFolder)
        {
            string absolute = ToAbsolutePath(transactionFolder);
            if (!Directory.Exists(absolute)
                || Directory.GetFileSystemEntries(absolute).Length != 0)
            {
                return;
            }
            if (!AssetDatabase.DeleteAsset(transactionFolder))
            {
                Directory.Delete(absolute);
                DeleteFileIfExists(absolute + ".meta");
            }
        }

        private static string ComputeVersionId(
            RoomContentJsonPackageV1 package,
            IList<string> keys)
        {
            var canonical = new StringBuilder();
            AppendCanonical(canonical, "manifest", package.ManifestJson);
            for (int index = 0; index < keys.Count; index++)
            {
                AppendCanonical(canonical, keys[index], package.Documents[keys[index]]);
            }

            byte[] bytes = Utf8WithoutBom.GetBytes(canonical.ToString());
            byte[] hash;
            using (SHA256 sha256 = SHA256.Create())
            {
                hash = sha256.ComputeHash(bytes);
            }
            var result = new StringBuilder(24);
            for (int index = 0; index < 12; index++)
            {
                result.Append(hash[index].ToString("x2"));
            }
            return result.ToString();
        }

        private static void AppendCanonical(
            StringBuilder builder,
            string key,
            string content)
        {
            string safeKey = key ?? string.Empty;
            string safeContent = content ?? string.Empty;
            builder.Append(safeKey.Length).Append(':').Append(safeKey);
            builder.Append(safeContent.Length).Append(':').Append(safeContent);
        }

        private static void WriteNewAssetText(string assetPath, string content)
        {
            string absolute = ToAbsolutePath(assetPath);
            using (var stream = new FileStream(
                absolute,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None))
            using (var writer = new StreamWriter(stream, Utf8WithoutBom))
            {
                writer.Write(content);
            }
        }

        private static string WithTrailingNewLine(string content)
        {
            return (content ?? string.Empty) + Environment.NewLine;
        }

        private static void EnsureAssetFolder(string assetFolder)
        {
            ValidateAssetPath(assetFolder, nameof(assetFolder));
            string[] segments = assetFolder.Split('/');
            string current = segments[0];
            for (int index = 1; index < segments.Length; index++)
            {
                string next = current + "/" + segments[index];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, segments[index]);
                }
                current = next;
            }
        }

        private static void ValidateAssetPath(string path, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(path)
                || (!string.Equals(path, "Assets", StringComparison.Ordinal)
                    && !path.StartsWith("Assets/", StringComparison.Ordinal)))
            {
                throw new ArgumentException(
                    "Unity asset paths must be below Assets/.",
                    parameterName);
            }
        }

        private static string ToAbsolutePath(string path)
        {
            if (Path.IsPathRooted(path)) return Path.GetFullPath(path);
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            return Path.GetFullPath(Path.Combine(projectRoot, path));
        }

        private static string SanitizeFileName(string value)
        {
            var builder = new StringBuilder();
            string text = string.IsNullOrWhiteSpace(value) ? "compiled" : value.Trim();
            for (int index = 0; index < text.Length; index++)
            {
                char c = text[index];
                builder.Append(char.IsLetterOrDigit(c) || c == '-' || c == '_'
                    ? c
                    : '-');
            }
            return builder.ToString();
        }

        private static void TryDeleteAsset(string assetPath)
        {
            if (string.IsNullOrWhiteSpace(assetPath)) return;
            try
            {
                if (AssetDatabase.DeleteAsset(assetPath)) return;
                string absolute = ToAbsolutePath(assetPath);
                if (Directory.Exists(absolute)) Directory.Delete(absolute, true);
                else DeleteFileIfExists(absolute);
                DeleteFileIfExists(absolute + ".meta");
            }
            catch (Exception exception)
            {
                if (IsFatal(exception)) throw;
                Debug.LogWarning(
                    "Level Grid V2 transaction cleanup failed for '"
                    + assetPath + "': " + exception.Message);
            }
        }

        private static void TryDeleteFile(string path)
        {
            try
            {
                DeleteFileIfExists(path);
            }
            catch (Exception exception)
            {
                if (IsFatal(exception)) throw;
                Debug.LogWarning(
                    "Level Grid V2 transaction cleanup failed for '"
                    + path + "': " + exception.Message);
            }
        }

        private static void DeleteFileIfExists(string path)
        {
            if (File.Exists(path)) File.Delete(path);
        }

        private static bool IsFatal(Exception exception)
        {
            return exception is OutOfMemoryException
                || exception is StackOverflowException
                || exception is AccessViolationException;
        }

        private static void ThrowIfInjected(
            LevelGridV2AssetPublishFailurePoint failurePoint)
        {
            if (injectedFailurePoint == failurePoint)
            {
                throw new InvalidOperationException(
                    "Injected Level Grid V2 asset publication failure at "
                    + failurePoint + ".");
            }
        }

        private sealed class VersionAssets
        {
            public VersionAssets(
                TextAsset manifest,
                RoomContentJsonDocumentAsset2D[] documents)
            {
                Manifest = manifest;
                Documents = documents;
            }

            public TextAsset Manifest { get; }
            public RoomContentJsonDocumentAsset2D[] Documents { get; }
        }

        private sealed class FailureInjectionScope : IDisposable
        {
            private readonly LevelGridV2AssetPublishFailurePoint? previous;
            private bool disposed;

            public FailureInjectionScope(
                LevelGridV2AssetPublishFailurePoint? previousFailurePoint)
            {
                previous = previousFailurePoint;
            }

            public void Dispose()
            {
                if (disposed) return;
                disposed = true;
                injectedFailurePoint = previous;
            }
        }
    }
}
#endif
