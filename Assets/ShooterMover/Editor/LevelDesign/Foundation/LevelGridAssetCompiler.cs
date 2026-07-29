#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using ShooterMover.Application.Missions.Rooms.Content;
using ShooterMover.Content.Definitions.Missions.Rooms;
using ShooterMover.UnityAdapters.Authoring.LevelDesign;
using UnityEngine;

namespace ShooterMover.Editor.LevelDesign.Foundation
{
    /// <summary>
    /// Builds an authored level folder into the room asset used by gameplay.
    /// Generated TextAssets are published immutably and the build-included
    /// resource asset is switched only after the complete replacement has imported and validated.
    /// </summary>
    public static partial class LevelGridAssetCompiler
    {
        private static readonly object PublishGate = new object();
        private static readonly UTF8Encoding Utf8WithoutBom = new UTF8Encoding(false);

        public const string Level1Source =
            "Assets/ShooterMover/Content/Definitions/Missions/Rooms/Levels/Level1";
        public const string Level1Generated =
            "Assets/ShooterMover/Content/Generated/Missions/Rooms/Levels/Level1";
        public const string Level1Resource =
            "Assets/ShooterMover/Resources/Levels/Level1RoomContent.asset";

        public static RoomFile CompileToAsset(
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

        internal static RoomFile CompileToAssetForTests(
            string sourceRoot,
            string generatedAssetFolder,
            string roomContentAssetPath,
            ILevelGridAssetCompilerFaultInjector faultInjector)
        {
            return CompileToAssetInternal(
                sourceRoot,
                generatedAssetFolder,
                roomContentAssetPath,
                faultInjector);
        }

        private static RoomFile CompileToAssetInternal(
            string sourceRoot,
            string generatedAssetFolder,
            string roomContentAssetPath,
            ILevelGridAssetCompilerFaultInjector faultInjector)
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
                    "Level source folder does not exist: " + sourceRoot);
            }

            lock (PublishGate)
            {
                DestinationSnapshot destinationSnapshot =
                    ValidateExistingDestination(roomContentAssetPath);
                LevelGridCompileResult compile = CompileAndValidate(sourceRoot);
                return PublishCompiledPackage(
                    compile.Package,
                    generatedAssetFolder,
                    roomContentAssetPath,
                    destinationSnapshot,
                    faultInjector);
            }
        }

        public static LevelGridCompileResult CompileFolder(string sourceRoot)
        {
            string absoluteRoot = ToAbsolutePath(sourceRoot);
            if (!Directory.Exists(absoluteRoot))
            {
                throw new DirectoryNotFoundException(
                    "Level source folder does not exist: " + sourceRoot);
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
            return LevelGridCompiler.Compile(new LevelGridSourcePackage(documents));
        }

        private static LevelGridCompileResult CompileAndValidate(string sourceRoot)
        {
            LevelGridCompileResult compile = CompileFolder(sourceRoot);
            if (compile == null || !compile.IsValid)
            {
                LevelGridCompileIssue issue = compile == null || compile.Issues.Count == 0
                    ? new LevelGridCompileIssue(
                        "level-level-1-compile-invalid",
                        "$",
                        "Compilation failed without a structured issue.")
                    : compile.Issues[0];
                throw new InvalidOperationException(issue.ToString());
            }

            RoomContentImportResult validation = RoomContentJsonImporter.Import(
                compile.Package,
                BuiltInRoomContentObjectCatalog.Create());
            ThrowIfImportInvalid(
                validation,
                "The existing room importer rejected the compiled result.");
            return compile;
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
            string projectRoot = Directory.GetParent(UnityEngine.Application.dataPath).FullName;
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
