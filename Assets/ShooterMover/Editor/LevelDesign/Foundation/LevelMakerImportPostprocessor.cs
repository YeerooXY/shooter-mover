#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace ShooterMover.Editor.LevelDesign.Foundation
{
    /// <summary>
    /// The single project-owned import seam for Level Maker output. Level packages contain data
    /// only; they never inject compiler scripts into the Unity project.
    /// </summary>
    public sealed class LevelMakerImportPostprocessor : AssetPostprocessor
    {
        private const string LevelSourcePrefix =
            "Assets/ShooterMover/Content/Definitions/Missions/Rooms/Levels/";
        private static readonly HashSet<string> Pending =
            new HashSet<string>(StringComparer.Ordinal);
        private static bool scheduled;

        private static void OnPostprocessAllAssets(
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            for (int index = 0; index < importedAssets.Length; index++)
            {
                string path = importedAssets[index].Replace('\\', '/');
                if (path.StartsWith(LevelSourcePrefix, StringComparison.Ordinal)
                    && path.EndsWith("/level.json", StringComparison.Ordinal))
                {
                    Pending.Add(path);
                }
            }

            if (Pending.Count == 0 || scheduled) return;
            scheduled = true;
            EditorApplication.delayCall += CompilePending;
        }

        [MenuItem("Tools/Shooter Mover/Level Design/Compile Level Maker Packages", priority = 253)]
        private static void CompileAll()
        {
            string root = Path.Combine(
                Directory.GetParent(UnityEngine.Application.dataPath).FullName,
                LevelSourcePrefix.Replace('/', Path.DirectorySeparatorChar));
            if (!Directory.Exists(root)) return;
            string[] manifests = Directory.GetFiles(
                root,
                "level.json",
                SearchOption.AllDirectories);
            for (int index = 0; index < manifests.Length; index++)
                Pending.Add(ToAssetPath(manifests[index]));
            CompilePending();
        }

        private static void CompilePending()
        {
            scheduled = false;
            string[] manifests = new string[Pending.Count];
            Pending.CopyTo(manifests);
            Pending.Clear();
            Array.Sort(manifests, StringComparer.Ordinal);

            for (int index = 0; index < manifests.Length; index++)
            {
                try
                {
                    string manifest = manifests[index];
                    LevelManifest payload = ReadManifest(manifest);
                    LevelGridPlayableBuildPaths paths =
                        LevelGridPlayableBuildPaths.Resolve(payload.LevelId);
                    paths.ClaimGeneratedDestination();
                    LevelGridAssetCompiler.CompileToAsset(
                        Path.GetDirectoryName(manifest).Replace('\\', '/'),
                        paths.GeneratedAssetFolder,
                        paths.CompiledAssetPath);
                    Debug.Log(
                        "Level Maker compiled '"
                        + payload.LevelId
                        + "' to "
                        + paths.CompiledAssetPath
                        + ".");
                }
                catch (Exception exception)
                {
                    Debug.LogError(
                        "Level Maker package compilation failed for '"
                        + manifests[index]
                        + "': "
                        + exception);
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static LevelManifest ReadManifest(string assetPath)
        {
            string absolute = Path.Combine(
                Directory.GetParent(UnityEngine.Application.dataPath).FullName,
                assetPath.Replace('/', Path.DirectorySeparatorChar));
            using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(
                File.ReadAllText(absolute))))
            {
                var serializer = new DataContractJsonSerializer(typeof(LevelManifest));
                LevelManifest result = serializer.ReadObject(stream) as LevelManifest;
                if (result == null || string.IsNullOrWhiteSpace(result.LevelId))
                    throw new InvalidOperationException("The level manifest has no level_id.");
                return result;
            }
        }

        private static string ToAssetPath(string absolute)
        {
            string project = Directory.GetParent(UnityEngine.Application.dataPath).FullName
                .Replace('\\', '/')
                .TrimEnd('/');
            string normalized = Path.GetFullPath(absolute).Replace('\\', '/');
            return normalized.Substring(project.Length + 1);
        }

        [DataContract]
        private sealed class LevelManifest
        {
            [DataMember(Name = "level_id", IsRequired = true)]
            public string LevelId;
        }
    }
}
#endif
