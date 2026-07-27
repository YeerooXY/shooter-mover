#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using ShooterMover.Application.Missions.Rooms.Content;
using ShooterMover.UnityAdapters.Authoring.LevelDesign;
using UnityEngine;

namespace ShooterMover.Editor.LevelDesign.Foundation
{
    [Serializable]
    internal sealed class LevelGridPlayableProvenanceRecordV2
    {
        public int schema_version = 1;
        public int compiler_schema_version;
        public string level_id = string.Empty;
        public string scene_fingerprint = string.Empty;
        public string source_package_fingerprint = string.Empty;
    }

    /// <summary>
    /// Durable export provenance. The record is intentionally not a .json file so the canonical
    /// compiler does not treat it as a room-content document.
    /// </summary>
    public static class LevelGridPlayableProvenanceV2
    {
        public const string FileName = "level-grid.playable.provenance";

        public static void Write(
            LevelDesignSceneAuthoringRoot2D root,
            string absolutePackageRoot)
        {
            if (root == null) throw new ArgumentNullException(nameof(root));
            if (string.IsNullOrWhiteSpace(absolutePackageRoot))
            {
                throw new ArgumentException(
                    "An absolute exported package path is required.",
                    nameof(absolutePackageRoot));
            }

            var record = new LevelGridPlayableProvenanceRecordV2
            {
                schema_version = 1,
                compiler_schema_version = LevelGridV2Compiler.CurrentVersion,
                level_id = root.LevelIdText == null ? string.Empty : root.LevelIdText.Trim(),
                scene_fingerprint = ComputeSceneFingerprint(root),
                source_package_fingerprint = ComputeSourcePackageFingerprint(
                    absolutePackageRoot),
            };
            File.WriteAllText(
                Path.Combine(absolutePackageRoot, FileName),
                JsonUtility.ToJson(record, true) + Environment.NewLine,
                new UTF8Encoding(false));
        }

        public static bool TryRead(
            string absolutePackageRoot,
            out string levelId,
            out string sceneFingerprint,
            out string sourcePackageFingerprint,
            out int compilerSchemaVersion)
        {
            levelId = string.Empty;
            sceneFingerprint = string.Empty;
            sourcePackageFingerprint = string.Empty;
            compilerSchemaVersion = 0;
            if (string.IsNullOrWhiteSpace(absolutePackageRoot)) return false;
            string path = Path.Combine(absolutePackageRoot, FileName);
            if (!File.Exists(path)) return false;
            try
            {
                LevelGridPlayableProvenanceRecordV2 record =
                    JsonUtility.FromJson<LevelGridPlayableProvenanceRecordV2>(
                        File.ReadAllText(path));
                if (record == null
                    || record.schema_version != 1
                    || string.IsNullOrWhiteSpace(record.level_id)
                    || string.IsNullOrWhiteSpace(record.scene_fingerprint)
                    || string.IsNullOrWhiteSpace(record.source_package_fingerprint))
                {
                    return false;
                }
                levelId = record.level_id.Trim();
                sceneFingerprint = record.scene_fingerprint.Trim();
                sourcePackageFingerprint = record.source_package_fingerprint.Trim();
                compilerSchemaVersion = record.compiler_schema_version;
                return true;
            }
            catch (Exception exception)
            {
                if (IsFatal(exception)) throw;
                return false;
            }
        }

        public static string ComputeSceneFingerprint(
            LevelDesignSceneAuthoringRoot2D root)
        {
            if (root == null) return string.Empty;
            var canonical = new StringBuilder(4096);
            Append(canonical, "compiler", LevelGridV2Compiler.CurrentVersion.ToString(
                CultureInfo.InvariantCulture));
            Append(canonical, "level", root.LevelIdText);

            LevelRoomAuthoring2D[] rooms =
                root.GetComponentsInChildren<LevelRoomAuthoring2D>(true);
            Array.Sort(rooms, CompareRooms);
            for (int index = 0; index < rooms.Length; index++)
            {
                LevelRoomAuthoring2D room = rooms[index];
                Append(canonical, "room.id", room.RoomIdText);
                Append(canonical, "room.display", room.DisplayName);
                Append(canonical, "room.grid", Vector(room.GridCoordinate));
                Append(canonical, "room.slot", room.FolderSlot.ToString(
                    CultureInfo.InvariantCulture));
                Append(canonical, "room.cell", Vector(room.CellSize));
                Append(canonical, "room.footprint", Vector(room.FootprintCells));
                Append(canonical, "room.alignment", room.Alignment.ToString());
                Append(canonical, "room.offset", Vector(room.CustomAlignmentOffset));
                Append(canonical, "room.sort", room.SortingOrder.ToString(
                    CultureInfo.InvariantCulture));
                Append(canonical, "room.map", Vector(room.MapCoordinate));
                Append(canonical, "room.map-visible", room.VisibleOnMap ? "1" : "0");
                Collider2D bounds = room.RoomBounds;
                Append(canonical, "room.bounds-present", bounds == null ? "0" : "1");
                if (bounds != null)
                {
                    Append(canonical, "room.bounds-type", bounds.GetType().FullName);
                    Bounds value = bounds.bounds;
                    Append(canonical, "room.bounds-center", Vector(value.center));
                    Append(canonical, "room.bounds-size", Vector(value.size));
                }
            }

            LevelDoorEndpointAuthoring2D[] doors =
                root.GetComponentsInChildren<LevelDoorEndpointAuthoring2D>(true);
            Array.Sort(doors, CompareDoors);
            for (int index = 0; index < doors.Length; index++)
            {
                LevelDoorEndpointAuthoring2D door = doors[index];
                Append(canonical, "door.id", door.DoorIdText);
                Append(canonical, "door.room", door.OwningRoom == null
                    ? string.Empty
                    : door.OwningRoom.RoomIdText);
                Append(canonical, "door.side", door.Side.ToString());
                Append(canonical, "door.placement", door.PlacementMode.ToString());
                Append(canonical, "door.edge", Float(door.EdgeOffset));
                Append(canonical, "door.fixed", Vector(door.FixedLocalPosition));
                Append(canonical, "door.traversable", door.Traversable ? "1" : "0");
                Append(canonical, "door.map-visible", door.VisibleOnMap ? "1" : "0");
                Append(canonical, "door.auto-face", door.AutoFaceConnection ? "1" : "0");
            }

            LevelDoorLinkAuthoring2D[] links =
                root.GetComponentsInChildren<LevelDoorLinkAuthoring2D>(true);
            Array.Sort(links, CompareLinks);
            for (int index = 0; index < links.Length; index++)
            {
                LevelDoorLinkAuthoring2D link = links[index];
                Append(canonical, "link.id", link.ConnectionIdText);
                Append(canonical, "link.source-room", Id(link.SourceRoom));
                Append(canonical, "link.source-door", Id(link.SourceDoor));
                Append(canonical, "link.destination-room", Id(link.DestinationRoom));
                Append(canonical, "link.destination-door", Id(link.DestinationDoor));
                Append(canonical, "link.policy", link.TravelPolicy.ToString());
            }

            LevelGridPlayableMetadataV2 metadata =
                root.GetComponent<LevelGridPlayableMetadataV2>();
            Append(canonical, "metadata.present", metadata == null ? "0" : "1");
            if (metadata != null)
            {
                Append(canonical, "metadata.start-room", Id(metadata.StartRoom));
                Append(canonical, "metadata.player-position", Vector(
                    metadata.PlayerStartLocalPosition));
                Append(canonical, "metadata.player-rotation", Float(
                    metadata.PlayerStartRotation));
                Append(canonical, "metadata.final-room", Id(metadata.FinalExitRoom));
                Append(canonical, "metadata.final-door", Id(metadata.FinalExitDoor));
                Append(canonical, "metadata.runtime-door", metadata.RuntimeDoorObjectId);
            }
            return Hash(canonical.ToString());
        }

        public static string ComputeSourcePackageFingerprint(string absolutePackageRoot)
        {
            if (string.IsNullOrWhiteSpace(absolutePackageRoot)
                || !Directory.Exists(absolutePackageRoot))
            {
                return string.Empty;
            }
            string root = Path.GetFullPath(absolutePackageRoot);
            string[] files = Directory.GetFiles(root, "*.json", SearchOption.AllDirectories);
            Array.Sort(files, StringComparer.OrdinalIgnoreCase);
            var canonical = new StringBuilder();
            Append(canonical, "compiler", LevelGridV2Compiler.CurrentVersion.ToString(
                CultureInfo.InvariantCulture));
            for (int index = 0; index < files.Length; index++)
            {
                string relative = files[index].Substring(root.Length)
                    .TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                    .Replace('\\', '/');
                Append(canonical, relative, NormalizeLineEndings(
                    File.ReadAllText(files[index])));
            }
            return Hash(canonical.ToString());
        }

        public static string ComputeSourceSnapshot(string absolutePackageRoot)
        {
            if (string.IsNullOrWhiteSpace(absolutePackageRoot)
                || !Directory.Exists(absolutePackageRoot))
            {
                return "missing";
            }
            string[] files = Directory.GetFiles(
                absolutePackageRoot,
                "*",
                SearchOption.AllDirectories);
            Array.Sort(files, StringComparer.OrdinalIgnoreCase);
            long length = 0;
            long latestTicks = 0;
            for (int index = 0; index < files.Length; index++)
            {
                var info = new FileInfo(files[index]);
                length += info.Exists ? info.Length : 0;
                latestTicks = Math.Max(latestTicks, info.Exists
                    ? info.LastWriteTimeUtc.Ticks
                    : 0);
            }
            return files.Length.ToString(CultureInfo.InvariantCulture)
                + ":"
                + length.ToString(CultureInfo.InvariantCulture)
                + ":"
                + latestTicks.ToString(CultureInfo.InvariantCulture);
        }

        private static int CompareRooms(LevelRoomAuthoring2D left, LevelRoomAuthoring2D right)
        {
            return string.CompareOrdinal(Id(left), Id(right));
        }

        private static int CompareDoors(
            LevelDoorEndpointAuthoring2D left,
            LevelDoorEndpointAuthoring2D right)
        {
            return string.CompareOrdinal(Id(left), Id(right));
        }

        private static int CompareLinks(
            LevelDoorLinkAuthoring2D left,
            LevelDoorLinkAuthoring2D right)
        {
            string leftId = left == null ? string.Empty : left.ConnectionIdText;
            string rightId = right == null ? string.Empty : right.ConnectionIdText;
            return string.CompareOrdinal(leftId, rightId);
        }

        private static string Id(LevelRoomAuthoring2D room)
        {
            return room == null ? string.Empty : room.RoomIdText ?? string.Empty;
        }

        private static string Id(LevelDoorEndpointAuthoring2D door)
        {
            return door == null ? string.Empty : door.DoorIdText ?? string.Empty;
        }

        private static string Vector(Vector2 value)
        {
            return Float(value.x) + "," + Float(value.y);
        }

        private static string Vector(Vector2Int value)
        {
            return value.x.ToString(CultureInfo.InvariantCulture)
                + ","
                + value.y.ToString(CultureInfo.InvariantCulture);
        }

        private static string Vector(Vector3 value)
        {
            return Float(value.x) + "," + Float(value.y) + "," + Float(value.z);
        }

        private static string Float(float value)
        {
            return value.ToString("R", CultureInfo.InvariantCulture);
        }

        private static void Append(StringBuilder builder, string key, string value)
        {
            string safeKey = key ?? string.Empty;
            string safeValue = value ?? string.Empty;
            builder.Append(safeKey.Length).Append(':').Append(safeKey);
            builder.Append(safeValue.Length).Append(':').Append(safeValue);
        }

        private static string Hash(string value)
        {
            using (SHA256 sha = SHA256.Create())
            {
                byte[] bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(value ?? string.Empty));
                var builder = new StringBuilder(bytes.Length * 2);
                for (int index = 0; index < bytes.Length; index++)
                {
                    builder.Append(bytes[index].ToString("x2"));
                }
                return builder.ToString();
            }
        }

        private static string NormalizeLineEndings(string value)
        {
            return (value ?? string.Empty).Replace("\r\n", "\n").Replace('\r', '\n');
        }

        private static bool IsFatal(Exception exception)
        {
            return exception is OutOfMemoryException
                || exception is StackOverflowException
                || exception is AccessViolationException;
        }
    }
}
#endif