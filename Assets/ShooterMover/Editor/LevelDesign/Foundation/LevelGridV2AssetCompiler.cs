#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using ShooterMover.Application.Missions.Rooms.Content;
using ShooterMover.Content.Definitions.Missions.Rooms;
using ShooterMover.UnityAdapters.Authoring.LevelDesign;
using UnityEditor;
using UnityEngine;

namespace ShooterMover.Editor.LevelDesign.Foundation
{
    /// <summary>
    /// Editor/build-time bridge from an exported Level Grid V2 folder to the existing runtime
    /// JSON asset boundary. Generated TextAssets are published immutably and the build-included
    /// resource asset is switched only after the complete replacement has imported and validated.
    /// </summary>
    public static partial class LevelGridV2AssetCompiler
    {
        private static readonly object PublishGate = new object();
        private static readonly UTF8Encoding Utf8WithoutBom = new UTF8Encoding(false);

        public const string TrackedCombatLoopSource =
            "Assets/ShooterMover/Content/Definitions/Missions/Rooms/GridV2/CombatLoopTest";
        public const string TrackedCombatLoopGenerated =
            "Assets/ShooterMover/Content/Generated/Missions/Rooms/GridV2/CombatLoopTest";
        public const string TrackedCombatLoopResource =
            "Assets/ShooterMover/Resources/ProductionLevels/CombatLoopTestRoomContent.asset";

        [MenuItem(
            "Tools/Shooter Mover/Level Design/Compile Tracked Combat Loop Grid V2",
            priority = 252)]
        private static void CompileTrackedCombatLoop()
        {
            CompileAndReport(
                TrackedCombatLoopSource,
                TrackedCombatLoopGenerated,
                TrackedCombatLoopResource);
        }

        [MenuItem(
            "Tools/Shooter Mover/Level Design/Compile Grid V2 Folder...",
            priority = 253)]
        private static void CompileSelectedFolder()
        {
            string sourceRoot = EditorUtility.OpenFolderPanel(
                "Choose Level Grid V2 Folder",
                Application.dataPath,
                string.Empty);
            if (string.IsNullOrWhiteSpace(sourceRoot)) return;

            string assetPath = EditorUtility.SaveFilePanelInProject(
                "Choose Compiled Room Content Asset",
                "GridV2RoomContent",
                "asset",
                "Choose the build-included JsonRoomContentDefinition2D asset.",
                "Assets/ShooterMover/Resources/ProductionLevels");
            if (string.IsNullOrWhiteSpace(assetPath)) return;

            string levelFolderName = new DirectoryInfo(sourceRoot).Name;
            string generatedRoot =
                "Assets/ShooterMover/Content/Generated/Missions/Rooms/GridV2/"
                + SanitizeFileName(levelFolderName);
            CompileAndReport(sourceRoot, generatedRoot, assetPath);
        }

        public static JsonRoomContentDefinition2D CompileToAsset(
            string sourceRoot,
            string generatedAssetFolder,
            string roomContentAssetPath)
        {
            return CompileToAssetInternal(
                sourceRoot,
                generatedAssetFolder,
                roomContentAssetPath,
                null);
        }

        internal static JsonRoomContentDefinition2D CompileToAssetForTests(
            string sourceRoot,
            string generatedAssetFolder,
            string roomContentAssetPath,
            ILevelGridV2AssetCompilerFaultInjector faultInjector)
        {
            return CompileToAssetInternal(
                sourceRoot,
                generatedAssetFolder,
                roomContentAssetPath,
                faultInjector);
        }

        private static JsonRoomContentDefinition2D CompileToAssetInternal(
            string sourceRoot,
            string generatedAssetFolder,
            string roomContentAssetPath,
            ILevelGridV2AssetCompilerFaultInjector faultInjector)
        {
            ValidateAssetPath(generatedAssetFolder, nameof(generatedAssetFolder));
            ValidateAssetPath(roomContentAssetPath, nameof(roomContentAssetPath));
            if (!roomContentAssetPath.EndsWith(".asset", StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException(
                    "The room-content destination must be a Unity .asset path.",
                    nameof(roomContentAssetPath));
            }
            if (string.IsNullOrWhiteSpace(sourceRoot)
                || !Directory.Exists(ToAbsolutePath(sourceRoot)))
            {
                throw new DirectoryNotFoundException(
                    "Level Grid V2 source folder does not exist: " + sourceRoot);
            }

            lock (PublishGate)
            {
                DestinationSnapshot destinationSnapshot =
                    ValidateExistingDestination(roomContentAssetPath);
                LevelGridV2CompileResult compile = CompileAndValidate(sourceRoot);
                return PublishCompiledPackage(
                    compile.Package,
                    generatedAssetFolder,
                    roomContentAssetPath,
                    destinationSnapshot,
                    faultInjector);
            }
        }

        public static LevelGridV2CompileResult CompileFolder(string sourceRoot)
        {
            string absoluteRoot = ToAbsolutePath(sourceRoot);
            if (!Directory.Exists(absoluteRoot))
            {
                throw new DirectoryNotFoundException(
                    "Level Grid V2 source folder does not exist: " + sourceRoot);
            }

            string[] files = Directory.GetFiles(
                absoluteRoot,
                "*.json",
                SearchOption.AllDirectories);
            Array.Sort(files, StringComparer.OrdinalIgnoreCase);
            var documents = new Dictionary<string, string>(StringComparer.Ordinal);
            for (int index = 0; index < files.Length; index++)
            {
                string relative = files[index]
                    .Substring(absoluteRoot.Length)
                    .TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                    .Replace('\\', '/');
                documents.Add(relative, File.ReadAllText(files[index]));
            }
            return LevelGridV2Compiler.Compile(new LevelGridV2SourcePackage(documents));
        }

        private static LevelGridV2CompileResult CompileAndValidate(string sourceRoot)
        {
            LevelGridV2CompileResult compile = CompileFolder(sourceRoot);
            if (compile == null || !compile.IsValid)
            {
                LevelGridV2CompileIssue issue = compile == null || compile.Issues.Count == 0
                    ? new LevelGridV2CompileIssue(
                        "level-grid-v2-compile-invalid",
                        "$",
                        "Compilation failed without a structured issue.")
                    : compile.Issues[0];
                throw new InvalidOperationException(issue.ToString());
            }

            RoomContentImportResultV1 validation = RoomContentJsonImporterV1.Import(
                compile.Package,
                BuiltInRoomContentObjectCatalogV1.Create());
            ThrowIfImportInvalid(
                validation,
                "The existing room importer rejected the compiled result.");
            return compile;
        }

        private static void CompileAndReport(
            string sourceRoot,
            string generatedAssetFolder,
            string roomContentAssetPath)
        {
            try
            {
                JsonRoomContentDefinition2D asset = CompileToAsset(
                    sourceRoot,
                    generatedAssetFolder,
                    roomContentAssetPath);
                Debug.Log(
                    "Level Grid V2 compiled and transactionally published into build-included asset '"
                    + AssetDatabase.GetAssetPath(asset)
                    + "'.",
                    asset);
                Selection.activeObject = asset;
            }
            catch (Exception exception)
            {
                if (IsFatal(exception)) throw;
                Debug.LogError("Level Grid V2 compilation failed: " + exception.Message);
                EditorUtility.DisplayDialog(
                    "Level Grid V2 Compilation Failed",
                    exception.Message,
                    "OK");
            }
        }

        private static bool IsFatal(Exception exception)
        {
            return exception is OutOfMemoryException
                || exception is StackOverflowException
                || exception is AccessViolationException;
        }

        private static string ToAbsolutePath(string path)
        {
            if (Path.IsPathRooted(path)) return Path.GetFullPath(path);
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            return Path.GetFullPath(Path.Combine(projectRoot, path));
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
    }
}
#endif
