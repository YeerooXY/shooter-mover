#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
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

            string absoluteOutput = Path.GetFullPath(outputRoot);
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
            bool existed = Directory.Exists(absoluteOutput);
            try
            {
                if (existed) CopyDirectory(absoluteOutput, stage);
                else Directory.CreateDirectory(stage);
                WritePackage(root, metadata, rooms, doors, links, stage);
                if (existed) Directory.Move(absoluteOutput, backup);
                Directory.Move(stage, absoluteOutput);
                if (Directory.Exists(backup)) Directory.Delete(backup, true);
            }
            catch
            {
                if (Directory.Exists(stage)) Directory.Delete(stage, true);
                if (Directory.Exists(backup) && !Directory.Exists(absoluteOutput))
                {
                    Directory.Move(backup, absoluteOutput);
                }
                throw;
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
            for (int i = 0; i < rooms.Length; i++)
            {
                roomRecords.Add(rooms[i].BuildRecord());
                gridRooms.Add(rooms[i].BuildGridRecord());
            }

            var doorRecords = new List<LevelGridDoorRecordV2>(doors.Length);
            for (int i = 0; i < doors.Length; i++)
            {
                LevelGridDoorRecordV2 value = doors[i].BuildRecord();
                if (doors[i] == metadata.FinalExitDoor)
                {
                    value = new LevelGridDoorRecordV2(
                        value.DoorId,
                        value.RoomId,
                        value.Side,
                        value.PlacementMode,
                        value.EdgeOffset,
                        value.FixedLocalPosition,
                        false,
                        value.VisibleOnMap,
                        value.AutoFaceConnection,
                        value.DiagnosticLocation);
                }
                doorRecords.Add(value);
            }

            var connectionRecords = new List<LevelGridConnectionRecordV2>(links.Length);
            for (int i = 0; i < links.Length; i++)
            {
                LevelGridConnectionRecordV2 record = links[i].BuildRecord();
                if (string.Equals(record.SourceRoomId, metadata.FinalExitRoom.RoomIdText, StringComparison.Ordinal)
                    && string.Equals(record.SourceDoorId, metadata.FinalExitDoor.DoorIdText, StringComparison.Ordinal)
                    || string.Equals(record.DestinationRoomId, metadata.FinalExitRoom.RoomIdText, StringComparison.Ordinal)
                    && string.Equals(record.DestinationDoorId, metadata.FinalExitDoor.DoorIdText, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        "The exact final-exit endpoint cannot also participate in a room connection.");
                }
                connectionRecords.Add(record);
            }

            LevelGridValidationResultV2 result =
                LevelGridAuthoringV2CompositeValidator.Validate(
                    roomRecords,
                    gridRooms,
                    doorRecords,
                    connectionRecords,
                    LevelGridValidationPurposeV2.ProductionPublish);
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

    }
}
#endif
