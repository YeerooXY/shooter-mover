#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
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
    /// <summary>
    /// Exports the editor graph into the compiler-ready V2 package. Unlike the Phase-1 draft
    /// exporter, this command requires explicit start/final metadata and writes room-local runtime
    /// bounds and door coordinates suitable for the build-time compiler.
    /// </summary>
    public static partial class LevelGridV2PlayableExporter
    {
        private static readonly UTF8Encoding Utf8WithoutBom = new UTF8Encoding(false);

        internal static Action BeforeCommitForTests;

        [MenuItem(
            "Tools/Shooter Mover/Level Design/Export Compiler-Ready Grid V2 Package...",
            priority = 254)]
        private static void ExportSelected()
        {
            LevelDesignSceneAuthoringRoot2D root = ResolveSelectedRoot();
            if (root == null)
            {
                EditorUtility.DisplayDialog(
                    "Playable Grid V2 Export",
                    "Select an object below a LevelDesignSceneAuthoringRoot2D.",
                    "OK");
                return;
            }

            string outputRoot = EditorUtility.OpenFolderPanel(
                "Export Compiler-Ready Level Grid V2 Package",
                Application.dataPath,
                (root.LevelIdText ?? "level").Replace('.', '_'));
            if (string.IsNullOrWhiteSpace(outputRoot)) return;

            try
            {
                Export(root, outputRoot);
                AssetDatabase.Refresh();
                EditorUtility.RevealInFinder(outputRoot);
                Debug.Log(
                    "Compiler-ready Level Grid V2 package exported to " + outputRoot,
                    root);
            }
            catch (Exception exception)
            {
                if (exception is OutOfMemoryException
                    || exception is StackOverflowException
                    || exception is AccessViolationException)
                {
                    throw;
                }
                Debug.LogError(
                    "Compiler-ready Level Grid V2 export failed: " + exception.Message,
                    root);
                EditorUtility.DisplayDialog(
                    "Playable Grid V2 Export Failed",
                    exception.Message,
                    "OK");
            }
        }

        public static void Export(
            LevelDesignSceneAuthoringRoot2D root,
            string outputRoot)
        {
            if (root == null) throw new ArgumentNullException(nameof(root));
            if (string.IsNullOrWhiteSpace(outputRoot))
            {
                throw new ArgumentException("An output folder is required.", nameof(outputRoot));
            }

            LevelGridPlayableMetadataV2 metadata =
                root.GetComponent<LevelGridPlayableMetadataV2>();
            if (metadata == null)
            {
                throw new InvalidOperationException(
                    "Add LevelGridPlayableMetadataV2 to the level root before playable export.");
            }
            metadata.ValidateForPlayableExport(root);
            LevelGridDoorOperationsV2.ReflowAll(root);

            LevelDesignValidationResult foundation = root.ValidateHierarchy();
            if (foundation == null || !foundation.IsValid)
            {
                throw new InvalidOperationException(
                    "Existing level-design foundation validation must pass before playable export.");
            }

            LevelRoomAuthoring2D[] rooms =
                root.GetComponentsInChildren<LevelRoomAuthoring2D>(true);
            LevelDoorEndpointAuthoring2D[] doors =
                root.GetComponentsInChildren<LevelDoorEndpointAuthoring2D>(true);
            LevelDoorLinkAuthoring2D[] links =
                root.GetComponentsInChildren<LevelDoorLinkAuthoring2D>(true);
            Array.Sort(rooms, CompareRooms);
            Array.Sort(doors, CompareDoors);
            Array.Sort(links, CompareLinks);
            ValidateGraphAllowingFinalExit(rooms, doors, links, metadata);

            string initialSceneFingerprint =
                LevelGridPlayableProvenanceV2.ComputeSceneFingerprint(root);
            string absoluteOutput = Path.GetFullPath(outputRoot);
            LevelGridV2RoomFolderMigration.ValidateDestinationRoot(
                absoluteOutput,
                root.LevelIdText);
            string initialDestinationSnapshot = ComputeDirectorySnapshot(absoluteOutput);
            string parent = Directory.GetParent(absoluteOutput) == null
                ? null
                : Directory.GetParent(absoluteOutput).FullName;
            if (string.IsNullOrEmpty(parent))
            {
                throw new InvalidOperationException(
                    "Choose a dedicated level folder below a writable parent.");
            }

            string stage = Path.Combine(
                parent,
                "." + Path.GetFileName(absoluteOutput) + ".playable-stage-"
                + Guid.NewGuid().ToString("N"));
            string backup = Path.Combine(
                parent,
                "." + Path.GetFileName(absoluteOutput) + ".playable-backup-"
                + Guid.NewGuid().ToString("N"));
            DeleteSiblingMeta(stage);
            DeleteSiblingMeta(backup);
            bool existed = Directory.Exists(absoluteOutput);
            try
            {
                if (existed) CopyDirectory(absoluteOutput, stage);
                else Directory.CreateDirectory(stage);
                WritePackage(root, metadata, rooms, doors, links, stage);
                LevelGridPlayableProvenanceV2.Write(root, stage);
                ValidateStagedPackage(stage);

                Action beforeCommit = BeforeCommitForTests;
                if (beforeCommit != null)
                {
                    beforeCommit();
                }

                EnsureSourceAndDestinationUnchanged(
                    root,
                    absoluteOutput,
                    initialSceneFingerprint,
                    initialDestinationSnapshot);
                LevelGridV2RoomFolderMigration.ValidateDestinationRoot(
                    absoluteOutput,
                    root.LevelIdText);

                if (existed)
                {
                    Directory.Move(absoluteOutput, backup);
                    string movedDestinationSnapshot = ComputeDirectorySnapshot(backup);
                    if (!string.Equals(
                            movedDestinationSnapshot,
                            initialDestinationSnapshot,
                            StringComparison.Ordinal))
                    {
                        throw new InvalidOperationException(
                            "Playable export aborted before commit because the exact destination '"
                            + absoluteOutput
                            + "' changed while it was being moved to the rollback backup. "
                            + "The staged package was not published.");
                    }
                }

                // Commit point: the fully validated stage becomes the authoritative source package.
                Directory.Move(stage, absoluteOutput);
            }
            catch
            {
                TryDeleteDirectoryAndMeta(stage);
                if (Directory.Exists(backup) && !Directory.Exists(absoluteOutput))
                {
                    Directory.Move(backup, absoluteOutput);
                }
                TryDeleteSiblingMeta(backup);
                throw;
            }

            // The new package is committed once the stage occupies the destination. Cleanup is
            // deliberately best-effort so an orphaned backup cannot turn a successful export into
            // a reported failure after the authoritative package has already changed.
            TryDeleteSiblingMeta(stage);
            TryDeleteDirectoryAndMeta(backup);
        }

        private static void EnsureSourceAndDestinationUnchanged(
            LevelDesignSceneAuthoringRoot2D root,
            string absoluteOutput,
            string initialSceneFingerprint,
            string initialDestinationSnapshot)
        {
            string currentSceneFingerprint =
                LevelGridPlayableProvenanceV2.ComputeSceneFingerprint(root);
            if (!string.Equals(
                    currentSceneFingerprint,
                    initialSceneFingerprint,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Playable export aborted before commit because the scene authoring source for "
                    + "exact level '"
                    + root.LevelIdText
                    + "' changed while its staged package was being validated. Re-run Build from "
                    + "the current Level Grid editor state.");
            }

            string currentDestinationSnapshot = ComputeDirectorySnapshot(absoluteOutput);
            if (!string.Equals(
                    currentDestinationSnapshot,
                    initialDestinationSnapshot,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Playable export aborted before commit because the exact destination '"
                    + absoluteOutput
                    + "' changed while its staged package was being validated. The external change "
                    + "was preserved; reconcile it explicitly and retry Build.");
            }
        }

        private static string ComputeDirectorySnapshot(string directoryPath)
        {
            if (string.IsNullOrWhiteSpace(directoryPath)
                || !Directory.Exists(directoryPath))
            {
                return "missing";
            }

            string root = Path.GetFullPath(directoryPath).TrimEnd(
                Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar);
            var canonical = new StringBuilder(4096);
            AppendSnapshot(canonical, "state", "directory");

            string[] directories = Directory.GetDirectories(
                root,
                "*",
                SearchOption.AllDirectories);
            Array.Sort(directories, StringComparer.OrdinalIgnoreCase);
            for (int index = 0; index < directories.Length; index++)
            {
                AppendSnapshot(
                    canonical,
                    "directory",
                    RelativeSnapshotPath(root, directories[index]));
            }

            string[] files = Directory.GetFiles(
                root,
                "*",
                SearchOption.AllDirectories);
            Array.Sort(files, StringComparer.OrdinalIgnoreCase);
            for (int index = 0; index < files.Length; index++)
            {
                string relative = RelativeSnapshotPath(root, files[index]);
                var info = new FileInfo(files[index]);
                AppendSnapshot(canonical, "file", relative);
                AppendSnapshot(
                    canonical,
                    "length",
                    info.Length.ToString(CultureInfo.InvariantCulture));
                AppendSnapshot(canonical, "sha256", ComputeFileHash(files[index]));
            }

            return ComputeTextHash(canonical.ToString());
        }

        private static string RelativeSnapshotPath(string root, string path)
        {
            return Path.GetFullPath(path).Substring(root.Length)
                .TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                .Replace('\\', '/');
        }

        private static string ComputeFileHash(string path)
        {
            using (SHA256 sha = SHA256.Create())
            using (FileStream stream = File.OpenRead(path))
            {
                return BytesToHex(sha.ComputeHash(stream));
            }
        }

        private static string ComputeTextHash(string value)
        {
            using (SHA256 sha = SHA256.Create())
            {
                return BytesToHex(sha.ComputeHash(
                    Encoding.UTF8.GetBytes(value ?? string.Empty)));
            }
        }

        private static string BytesToHex(byte[] bytes)
        {
            var builder = new StringBuilder(bytes.Length * 2);
            for (int index = 0; index < bytes.Length; index++)
            {
                builder.Append(bytes[index].ToString("x2"));
            }
            return builder.ToString();
        }

        private static void AppendSnapshot(
            StringBuilder builder,
            string key,
            string value)
        {
            string safeKey = key ?? string.Empty;
            string safeValue = value ?? string.Empty;
            builder.Append(safeKey.Length).Append(':').Append(safeKey);
            builder.Append(safeValue.Length).Append(':').Append(safeValue);
        }

        private static void ValidateStagedPackage(string stage)
        {
            LevelGridV2CompileResult compile = LevelGridV2AssetCompiler.CompileFolder(stage);
            if (compile == null || !compile.IsValid)
            {
                string detail = compile != null && compile.Issues.Count > 0
                    ? compile.Issues[0].ToString()
                    : "Compilation failed without a structured issue.";
                throw new InvalidOperationException(
                    "The staged playable package did not compile: " + detail);
            }

            RoomContentImportResultV1 imported = RoomContentJsonImporterV1.Import(
                compile.Package,
                BuiltInRoomContentObjectCatalogV1.Create());
            if (imported == null || !imported.IsValid)
            {
                string detail = imported != null && imported.Issues.Count > 0
                    ? imported.Issues[0].Code + " at " + imported.Issues[0].Path
                        + ": " + imported.Issues[0].Message
                    : "The existing V1 importer rejected the staged package.";
                throw new InvalidOperationException(
                    "The staged playable package failed runtime import validation: " + detail);
            }
        }

        private static void ValidateGraphAllowingFinalExit(
            LevelRoomAuthoring2D[] rooms,
            LevelDoorEndpointAuthoring2D[] doors,
            LevelDoorLinkAuthoring2D[] links,
            LevelGridPlayableMetadataV2 metadata)
        {
            var roomRecords = new List<LevelRoomRecord>(rooms.Length);
            var gridRooms = new List<LevelGridRoomRecordV2>(rooms.Length);
            for (int index = 0; index < rooms.Length; index++)
            {
                roomRecords.Add(rooms[index].BuildRecord());
                gridRooms.Add(rooms[index].BuildGridRecord());
            }

            var doorRecords = new List<LevelGridDoorRecordV2>(doors.Length);
            for (int index = 0; index < doors.Length; index++)
            {
                doorRecords.Add(doors[index].BuildRecord());
            }

            var connectionRecords = new List<LevelGridConnectionRecordV2>(links.Length);
            for (int index = 0; index < links.Length; index++)
            {
                connectionRecords.Add(links[index].BuildRecord());
            }

            LevelGridValidationResultV2 result = LevelGridPlayableValidationV2.Validate(
                roomRecords,
                gridRooms,
                doorRecords,
                connectionRecords,
                LevelGridValidationPurposeV2.ProductionPublish,
                metadata.FinalExitRoom.RoomIdText,
                metadata.FinalExitDoor.DoorIdText);
            if (!result.CanPublish)
            {
                LevelGridProblemV2 issue = result.Problems.Count == 0
                    ? null
                    : result.Problems[0];
                throw new InvalidOperationException(
                    issue == null
                        ? "Level Grid V2 production validation failed."
                        : issue.ToString());
            }
        }

        private static void DeleteSiblingMeta(string directoryPath)
        {
            string metaPath = directoryPath + ".meta";
            if (File.Exists(metaPath)) File.Delete(metaPath);
        }

        private static void TryDeleteSiblingMeta(string directoryPath)
        {
            try
            {
                DeleteSiblingMeta(directoryPath);
            }
            catch (Exception exception)
            {
                if (exception is OutOfMemoryException
                    || exception is StackOverflowException
                    || exception is AccessViolationException)
                {
                    throw;
                }
                Debug.LogWarning(
                    "Playable Grid V2 cleanup could not delete metadata '"
                    + directoryPath + ".meta': " + exception.Message);
            }
        }

        private static void TryDeleteDirectoryAndMeta(string directoryPath)
        {
            try
            {
                if (Directory.Exists(directoryPath)) Directory.Delete(directoryPath, true);
            }
            catch (Exception exception)
            {
                if (exception is OutOfMemoryException
                    || exception is StackOverflowException
                    || exception is AccessViolationException)
                {
                    throw;
                }
                Debug.LogWarning(
                    "Playable Grid V2 cleanup could not delete directory '"
                    + directoryPath + "': " + exception.Message);
            }
            TryDeleteSiblingMeta(directoryPath);
        }

    }
}
#endif
