#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using ShooterMover.Content.Definitions.Missions.Rooms;
using ShooterMover.UnityAdapters.Authoring.LevelDesign;
using UnityEditor;
using UnityEngine;

namespace ShooterMover.Editor.LevelDesign.Foundation
{
    public static partial class LevelGridV2AssetCompiler
    {
        private static void TryPostCommitCleanup(
            string generatedAssetFolder,
            string runtimeStageFolder,
            string committedVersionFolder,
            ILevelGridV2AssetCompilerFaultInjector faultInjector)
        {
            try
            {
                TryRemovePublishingMarker(committedVersionFolder);
                TryDeleteAssetOrFolder(runtimeStageFolder);
                Inject(
                    faultInjector,
                    LevelGridV2AssetCompilerPublishStep.BeforePostCommitCleanup);
                DeleteUnreferencedGeneratedOutput(
                    generatedAssetFolder,
                    committedVersionFolder);
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    "Level Grid V2 asset publication committed successfully, but generated "
                    + "cleanup failed: "
                    + exception.Message);
            }
        }

        private static void DeleteUnreferencedGeneratedOutput(
            string generatedAssetFolder,
            string committedVersionFolder)
        {
            HashSet<string> referencedAssetPaths = FindAllReferencedRoomContentAssetPaths();
            string versionsRoot = generatedAssetFolder + "/" + VersionsFolderName;
            if (AssetDatabase.IsValidFolder(versionsRoot))
            {
                string[] versions = AssetDatabase.GetSubFolders(versionsRoot);
                for (int index = 0; index < versions.Length; index++)
                {
                    string version = versions[index];
                    if (string.Equals(
                        version,
                        committedVersionFolder,
                        StringComparison.Ordinal))
                    {
                        continue;
                    }
                    if (File.Exists(ToAbsolutePath(version + "/" + PublishingMarkerFileName)))
                    {
                        continue;
                    }
                    if (HasReferenceBelow(version, referencedAssetPaths)) continue;
                    DeleteAssetOrThrow(version);
                }
            }

            string absoluteRoot = ToAbsolutePath(generatedAssetFolder);
            if (!Directory.Exists(absoluteRoot)) return;
            string[] legacyJson = Directory.GetFiles(
                absoluteRoot,
                "*.json",
                SearchOption.TopDirectoryOnly);
            for (int index = 0; index < legacyJson.Length; index++)
            {
                string assetPath = generatedAssetFolder
                    + "/"
                    + Path.GetFileName(legacyJson[index]);
                if (referencedAssetPaths.Contains(assetPath)) continue;
                DeleteAssetOrThrow(assetPath);
            }
        }

        private static HashSet<string> FindAllReferencedRoomContentAssetPaths()
        {
            var result = new HashSet<string>(StringComparer.Ordinal);
            string[] guids = AssetDatabase.FindAssets("t:JsonRoomContentDefinition2D");
            for (int index = 0; index < guids.Length; index++)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guids[index]);
                JsonRoomContentDefinition2D asset =
                    AssetDatabase.LoadAssetAtPath<JsonRoomContentDefinition2D>(assetPath);
                if (asset == null) continue;

                var serialized = new SerializedObject(asset);
                SerializedProperty manifest = serialized.FindProperty("manifest");
                AddReferencedAssetPath(result, manifest);
                SerializedProperty documents = serialized.FindProperty("documents");
                if (documents == null || !documents.isArray) continue;
                for (int documentIndex = 0;
                    documentIndex < documents.arraySize;
                    documentIndex++)
                {
                    SerializedProperty entry = documents.GetArrayElementAtIndex(documentIndex);
                    AddReferencedAssetPath(result, entry.FindPropertyRelative("document"));
                }
            }
            return result;
        }

        private static void AddReferencedAssetPath(
            ISet<string> result,
            SerializedProperty property)
        {
            if (property == null) return;
            UnityEngine.Object value = property.objectReferenceValue;
            if (value == null) return;
            string path = AssetDatabase.GetAssetPath(value);
            if (!string.IsNullOrEmpty(path)) result.Add(path);
        }

        private static bool HasReferenceBelow(
            string folderPath,
            IEnumerable<string> referencedAssetPaths)
        {
            string prefix = folderPath.EndsWith("/", StringComparison.Ordinal)
                ? folderPath
                : folderPath + "/";
            foreach (string path in referencedAssetPaths)
            {
                if (path.StartsWith(prefix, StringComparison.Ordinal)) return true;
            }
            return false;
        }

        private static void TryDeleteVersionIfUnreferenced(string folderPath)
        {
            try
            {
                HashSet<string> references = FindAllReferencedRoomContentAssetPaths();
                if (!HasReferenceBelow(folderPath, references))
                {
                    DeleteAssetOrThrow(folderPath);
                }
            }
            catch (Exception exception)
            {
                if (IsFatal(exception)) throw;
                Debug.LogWarning(
                    "Level Grid V2 publication failed before commit and could not remove "
                    + "unreferenced version '"
                    + folderPath
                    + "': "
                    + exception.Message);
            }
        }

        private static void WritePublishingMarker(string folderPath, string transactionId)
        {
            File.WriteAllText(
                ToAbsolutePath(folderPath + "/" + PublishingMarkerFileName),
                transactionId + Environment.NewLine,
                Utf8WithoutBom);
        }

        private static void TryRemovePublishingMarker(string folderPath)
        {
            TryDeleteFile(ToAbsolutePath(folderPath + "/" + PublishingMarkerFileName));
        }

        private static List<string> SortedKeys(RoomContentJsonPackageV1 package)
        {
            var keys = new List<string>(package.Documents.Keys);
            keys.Sort(StringComparer.Ordinal);
            return keys;
        }

        private static string DocumentAssetPath(string folderPath, int index, string key)
        {
            return folderPath
                + "/"
                + index.ToString("00")
                + "_"
                + SanitizeFileName(key)
                + ".json";
        }

        private static string ComputePackageVersionId(RoomContentJsonPackageV1 package)
        {
            var canonical = new StringBuilder();
            AppendHashPart(canonical, "manifest", package.ManifestJson);
            List<string> keys = SortedKeys(package);
            for (int index = 0; index < keys.Count; index++)
            {
                AppendHashPart(canonical, keys[index], package.Documents[keys[index]]);
            }

            byte[] bytes = Encoding.UTF8.GetBytes(canonical.ToString());
            using (SHA256 sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(bytes);
                var result = new StringBuilder(hash.Length * 2);
                for (int index = 0; index < hash.Length; index++)
                {
                    result.Append(hash[index].ToString("x2"));
                }
                return result.ToString(0, 32);
            }
        }

        private static void AppendHashPart(StringBuilder builder, string key, string content)
        {
            string safeContent = content ?? string.Empty;
            builder.Append(key.Length).Append(':').Append(key);
            builder.Append(safeContent.Length).Append(':').Append(safeContent);
        }

        private static void RequireTextAssetContent(
            TextAsset asset,
            string assetPath,
            string expected)
        {
            if (asset == null)
            {
                throw new InvalidOperationException(
                    "Generated JSON did not import as a TextAsset: " + assetPath);
            }
            string actualText = NormalizeLineEndings(asset.text);
            string expectedText = NormalizeLineEndings(
                (expected ?? string.Empty) + Environment.NewLine);
            if (!string.Equals(actualText, expectedText, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Generated version content does not match the compiled package: " + assetPath);
            }
        }

        private static string NormalizeLineEndings(string value)
        {
            return (value ?? string.Empty)
                .Replace("\r\n", "\n")
                .Replace('\r', '\n');
        }

        private static void WriteAssetText(string assetPath, string content)
        {
            EnsureAssetFolder(Path.GetDirectoryName(assetPath).Replace('\\', '/'));
            File.WriteAllText(
                ToAbsolutePath(assetPath),
                (content ?? string.Empty) + Environment.NewLine,
                Utf8WithoutBom);
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
                    string guid = AssetDatabase.CreateFolder(current, segments[index]);
                    if (string.IsNullOrEmpty(guid))
                    {
                        throw new IOException("Could not create Unity asset folder: " + next);
                    }
                }
                current = next;
            }
        }

        private static void DeleteAssetOrThrow(string assetPath)
        {
            if (!AssetDatabase.DeleteAsset(assetPath))
            {
                throw new IOException("Could not delete Unity asset: " + assetPath);
            }
        }

        private static void TryDeleteAssetOrFolder(string assetPath)
        {
            try
            {
                if (AssetDatabase.IsValidFolder(assetPath)
                    || AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetPath) != null)
                {
                    if (!AssetDatabase.DeleteAsset(assetPath))
                    {
                        throw new IOException("Could not delete Unity asset: " + assetPath);
                    }
                    return;
                }

                string absolute = ToAbsolutePath(assetPath);
                if (Directory.Exists(absolute)) Directory.Delete(absolute, true);
                else if (File.Exists(absolute)) File.Delete(absolute);
                string meta = absolute + ".meta";
                if (File.Exists(meta)) File.Delete(meta);
            }
            catch (Exception exception)
            {
                if (IsFatal(exception)) throw;
                Debug.LogWarning(
                    "Could not clean transaction-owned Unity asset path '"
                    + assetPath
                    + "': "
                    + exception.Message);
            }
        }

        private static void TryDeleteFile(string path)
        {
            try
            {
                if (File.Exists(path)) File.Delete(path);
            }
            catch (Exception exception)
            {
                if (IsFatal(exception)) throw;
                Debug.LogWarning(
                    "Could not delete temporary file '"
                    + path
                    + "': "
                    + exception.Message);
            }
        }

        private static void Inject(
            ILevelGridV2AssetCompilerFaultInjector faultInjector,
            LevelGridV2AssetCompilerPublishStep step)
        {
            if (faultInjector != null) faultInjector.OnStep(step);
        }
    }
}
#endif
