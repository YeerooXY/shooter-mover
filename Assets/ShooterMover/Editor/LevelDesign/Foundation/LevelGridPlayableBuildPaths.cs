#if UNITY_EDITOR
using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using ShooterMover.UnityAdapters.Authoring.LevelDesign;
using UnityEditor;
using UnityEngine;

namespace ShooterMover.Editor.LevelDesign.Foundation
{
    /// <summary>
    /// Deterministic project destinations derived from the stable level identity. Paths are a
    /// projection of identity, never the identity authority itself.
    /// </summary>
    public sealed class LevelGridPlayableBuildPaths
    {
        public const string Level1Id =
            "level.level-1";
        public const string LevelSelectionScenePath =
            "Assets/ShooterMover/Scenes/Flow/LevelSelection/LevelSelection.unity";
        public const string CatalogueSourcePath =
            "Assets/ShooterMover/Content/Definitions/Levels/Selection/LevelSelectionCatalogDefinition.cs";
        internal const string GeneratedOwnerMarkerFileName = ".level-grid-owner";

        private LevelGridPlayableBuildPaths(
            string levelId,
            string sourcePackagePath,
            string generatedAssetFolder,
            string compiledAssetPath,
            string resourcePath,
            bool level1)
        {
            LevelId = levelId;
            SourcePackagePath = sourcePackagePath;
            GeneratedAssetFolder = generatedAssetFolder;
            CompiledAssetPath = compiledAssetPath;
            ResourcePath = resourcePath;
            IsLevel1 = level1;
        }

        public string LevelId { get; }
        public string SourcePackagePath { get; }
        public string GeneratedAssetFolder { get; }
        public string CompiledAssetPath { get; }
        public string ResourcePath { get; }
        public bool IsLevel1 { get; }

        public string SourcePackageAbsolutePath
        {
            get { return ToAbsoluteProjectPath(SourcePackagePath); }
        }

        internal string GeneratedOwnerMarkerPath
        {
            get { return GeneratedAssetFolder + "/" + GeneratedOwnerMarkerFileName; }
        }

        public static LevelGridPlayableBuildPaths Resolve(
            LevelDraft root)
        {
            if (root == null) throw new ArgumentNullException(nameof(root));
            return Resolve(root.LevelIdText);
        }

        public static LevelGridPlayableBuildPaths Resolve(string levelId)
        {
            string normalized = RequireLevelId(levelId);
            if (string.Equals(
                normalized,
                Level1Id,
                StringComparison.Ordinal))
            {
                return new LevelGridPlayableBuildPaths(
                    normalized,
                    LevelGridAssetCompiler.Level1Source,
                    LevelGridAssetCompiler.Level1Generated,
                    LevelGridAssetCompiler.Level1Resource,
                    "Levels/Level1RoomContent",
                    true);
            }

            string token = BuildCollisionResistantToken(normalized);
            string assetStem = BuildAssetStem(normalized) + "_" + ShortHash(normalized);
            return new LevelGridPlayableBuildPaths(
                normalized,
                "Assets/ShooterMover/Content/Definitions/Missions/Rooms/Levels/Published/"
                    + token,
                "Assets/ShooterMover/Content/Generated/Missions/Rooms/Levels/"
                    + token,
                "Assets/ShooterMover/Resources/Levels/"
                    + assetStem
                    + "RoomContent.asset",
                "Levels/" + assetStem + "RoomContent",
                false);
        }

        public void ValidateDestinationOwnership()
        {
            if (Directory.Exists(SourcePackageAbsolutePath))
            {
                LevelGridRoomFolderMigration.ValidateDestinationRoot(
                    SourcePackageAbsolutePath,
                    LevelId);
            }

            ValidateGeneratedDestinationOwnership();

            string compiledAbsolute = ToAbsoluteProjectPath(CompiledAssetPath);
            if (!File.Exists(compiledAbsolute)) return;

            RoomFile existing =
                AssetDatabase.LoadAssetAtPath<RoomFile>(
                    CompiledAssetPath);
            if (existing == null)
            {
                throw new InvalidOperationException(
                    "The configured compiled destination exists but is not a "
                    + nameof(RoomFile)
                    + ": "
                    + CompiledAssetPath);
            }

            var serialized = new SerializedObject(existing);
            SerializedProperty manifest = serialized.FindProperty("manifest");
            UnityEngine.Object manifestObject = manifest == null
                ? null
                : manifest.objectReferenceValue;
            string manifestPath = manifestObject == null
                ? string.Empty
                : AssetDatabase.GetAssetPath(manifestObject);
            string expectedPrefix = GeneratedAssetFolder.EndsWith(
                "/",
                StringComparison.Ordinal)
                ? GeneratedAssetFolder
                : GeneratedAssetFolder + "/";
            if (string.IsNullOrEmpty(manifestPath)
                || !manifestPath.StartsWith(expectedPrefix, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "The configured compiled destination is owned by a different generated "
                    + "level output. Expected references below '"
                    + GeneratedAssetFolder
                    + "' but found '"
                    + manifestPath
                    + "'.");
            }
        }

        /// <summary>
        /// Claims a previously absent or empty generic generated destination. The marker is a
        /// narrow, idempotent ownership record; it is not compiled content and does not replace the
        /// canonical asset-publication transaction.
        /// </summary>
        public void ClaimGeneratedDestination()
        {
            ValidateGeneratedDestinationOwnership();
            EnsureAssetFolder(GeneratedAssetFolder);
            string markerAbsolute = ToAbsoluteProjectPath(GeneratedOwnerMarkerPath);
            if (!File.Exists(markerAbsolute))
            {
                File.WriteAllText(
                    markerAbsolute,
                    LevelId + Environment.NewLine,
                    new UTF8Encoding(false));
                AssetDatabase.ImportAsset(
                    GeneratedOwnerMarkerPath,
                    ImportAssetOptions.ForceSynchronousImport);
            }
        }

        public static string ToAbsoluteProjectPath(string projectPath)
        {
            if (string.IsNullOrWhiteSpace(projectPath))
            {
                throw new ArgumentException("A project path is required.", nameof(projectPath));
            }
            if (Path.IsPathRooted(projectPath)) return Path.GetFullPath(projectPath);
            string projectRoot = Directory.GetParent(UnityEngine.Application.dataPath).FullName;
            return Path.GetFullPath(Path.Combine(projectRoot, projectPath));
        }

        private void ValidateGeneratedDestinationOwnership()
        {
            string absoluteFolder = ToAbsoluteProjectPath(GeneratedAssetFolder);
            if (!Directory.Exists(absoluteFolder)) return;

            string markerAbsolute = ToAbsoluteProjectPath(GeneratedOwnerMarkerPath);
            if (File.Exists(markerAbsolute))
            {
                string owner = File.ReadAllText(markerAbsolute).Trim();
                if (!string.Equals(owner, LevelId, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        "The configured generated destination belongs to another level ID: '"
                        + owner
                        + "'. Expected '"
                        + LevelId
                        + "'.");
                }
                return;
            }

            string[] entries = Directory.GetFileSystemEntries(absoluteFolder);
            if (entries.Length == 0 || IsLevel1)
            {
                // The tracked sample predates ownership markers and is bound by an exact known ID.
                return;
            }

            throw new InvalidOperationException(
                "The configured generated destination is non-empty but has no stable level-owner "
                + "marker: "
                + GeneratedAssetFolder);
        }

        private static void EnsureAssetFolder(string assetFolder)
        {
            if (AssetDatabase.IsValidFolder(assetFolder)) return;
            string[] segments = assetFolder.Split('/');
            if (segments.Length == 0
                || !string.Equals(segments[0], "Assets", StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    "Generated destinations must be below Assets/.",
                    nameof(assetFolder));
            }
            string current = segments[0];
            for (int index = 1; index < segments.Length; index++)
            {
                string next = current + "/" + segments[index];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    string guid = AssetDatabase.CreateFolder(current, segments[index]);
                    if (string.IsNullOrEmpty(guid))
                    {
                        throw new IOException("Could not create generated folder: " + next);
                    }
                }
                current = next;
            }
        }

        private static string RequireLevelId(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new InvalidOperationException(
                    "A stable level ID is required before playable destinations can be resolved.");
            }
            string normalized = value.Trim();
            if (!normalized.StartsWith("level.", StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Playable destination resolution requires a stable level.* identity: "
                    + normalized);
            }
            return normalized;
        }

        private static string BuildCollisionResistantToken(string levelId)
        {
            var builder = new StringBuilder(levelId.Length + 9);
            for (int index = 0; index < levelId.Length; index++)
            {
                char c = char.ToLowerInvariant(levelId[index]);
                builder.Append(char.IsLetterOrDigit(c) || c == '-' || c == '_'
                    ? c
                    : '-');
            }
            string token = builder.ToString().Trim('-');
            if (string.IsNullOrEmpty(token)) token = "level";
            return token + "-" + ShortHash(levelId);
        }

        private static string BuildAssetStem(string levelId)
        {
            var builder = new StringBuilder();
            bool upperNext = true;
            for (int index = 0; index < levelId.Length; index++)
            {
                char c = levelId[index];
                if (!char.IsLetterOrDigit(c))
                {
                    upperNext = true;
                    continue;
                }
                builder.Append(upperNext ? char.ToUpperInvariant(c) : c);
                upperNext = false;
            }
            return builder.Length == 0 ? "Level" : builder.ToString();
        }

        private static string ShortHash(string value)
        {
            using (SHA256 sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(value));
                var builder = new StringBuilder(8);
                for (int index = 0; index < 4; index++)
                {
                    builder.Append(hash[index].ToString("x2"));
                }
                return builder.ToString();
            }
        }
    }
}
#endif
