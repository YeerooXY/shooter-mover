#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace ShooterMover.UnityAdapters.Authoring.LevelDesign
{
    /// <summary>
    /// Validates a playable export destination and migrates exported room folders by stable room
    /// identity before coordinate-derived folder names are rewritten. The caller operates on a
    /// staged copy, so any failure can discard the stage without mutating the published package.
    /// </summary>
    public static class LevelGridV2RoomFolderMigration
    {
        public static void ValidateDestinationRoot(
            string outputRoot,
            string expectedLevelId)
        {
            if (string.IsNullOrWhiteSpace(outputRoot))
            {
                throw new ArgumentException(
                    "A playable export destination is required.",
                    nameof(outputRoot));
            }
            if (string.IsNullOrWhiteSpace(expectedLevelId))
            {
                throw new ArgumentException(
                    "An authoritative level ID is required.",
                    nameof(expectedLevelId));
            }
            if (!Directory.Exists(outputRoot)) return;

            string[] entries = Directory.GetFileSystemEntries(outputRoot);
            if (entries.Length == 0) return;

            string levelPath = Path.Combine(outputRoot, "level.json");
            if (!File.Exists(levelPath))
            {
                throw new InvalidOperationException(
                    "The selected folder is not empty and has no Level Grid V2 level.json. "
                        + "Choose an empty or previously exported dedicated level folder.");
            }

            LevelIdentityDto identity = ReadLevelIdentity(levelPath);
            string expected = expectedLevelId.Trim();
            if (!string.Equals(identity.level_id, expected, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "The selected folder belongs to level '" + identity.level_id
                        + "', not '" + expected + "'.");
            }
        }

        public static IReadOnlyDictionary<string, string> Prepare(
            LevelRoomAuthoring2D[] rooms,
            string roomsRoot)
        {
            if (rooms == null) throw new ArgumentNullException(nameof(rooms));
            if (string.IsNullOrWhiteSpace(roomsRoot))
            {
                throw new ArgumentException("A Rooms folder is required.", nameof(roomsRoot));
            }

            Directory.CreateDirectory(roomsRoot);
            Dictionary<string, string> existingByRoomId =
                ScanExistingRoomFolders(roomsRoot);
            var activeByRoomId = new Dictionary<string, LevelRoomAuthoring2D>(
                StringComparer.Ordinal);
            var desiredNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            for (int index = 0; index < rooms.Length; index++)
            {
                LevelRoomAuthoring2D room = rooms[index];
                if (room == null || string.IsNullOrWhiteSpace(room.RoomIdText))
                {
                    throw new InvalidOperationException(
                        "Playable export cannot allocate folders while a room or room ID is missing.");
                }
                if (activeByRoomId.ContainsKey(room.RoomIdText))
                {
                    throw new InvalidOperationException(
                        "Playable export cannot allocate folders for duplicate room ID: "
                            + room.RoomIdText);
                }
                activeByRoomId.Add(room.RoomIdText, room);

                string desiredName = BuildRoomFolderName(room);
                if (!desiredNames.Add(desiredName))
                {
                    throw new InvalidOperationException(
                        "Multiple rooms request folder '" + desiredName
                            + "'. Assign a unique slot at that coordinate.");
                }
            }

            // The destination is a disposable staged copy. Remove folders owned by deleted rooms
            // before assigning desired paths so a surviving room may safely move into a vacated
            // coordinate+slot without adopting the deleted room's sidecars.
            foreach (KeyValuePair<string, string> existing in existingByRoomId)
            {
                if (!activeByRoomId.ContainsKey(existing.Key)
                    && Directory.Exists(existing.Value))
                {
                    Directory.Delete(existing.Value, true);
                }
            }

            var temporaryByRoomId = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, LevelRoomAuthoring2D> pair in activeByRoomId)
            {
                string existingPath;
                if (!existingByRoomId.TryGetValue(pair.Key, out existingPath)
                    || !Directory.Exists(existingPath))
                {
                    continue;
                }

                string temporaryPath = Path.Combine(
                    roomsRoot,
                    ".__playable_migrate__" + Guid.NewGuid().ToString("N"));
                Directory.Move(existingPath, temporaryPath);
                temporaryByRoomId.Add(pair.Key, temporaryPath);
            }

            var result = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, LevelRoomAuthoring2D> pair in activeByRoomId)
            {
                string desiredPath = Path.Combine(
                    roomsRoot,
                    BuildRoomFolderName(pair.Value));
                if (Directory.Exists(desiredPath))
                {
                    RoomIdentityDto owner = ReadRoomIdentity(
                        Path.Combine(desiredPath, "room.json"));
                    throw new InvalidOperationException(
                        "Room folder '" + Path.GetFileName(desiredPath)
                            + "' already belongs to room '" + owner.room_id
                            + "'. It will not be adopted by '" + pair.Key + "'.");
                }

                string temporaryPath;
                if (temporaryByRoomId.TryGetValue(pair.Key, out temporaryPath))
                {
                    Directory.Move(temporaryPath, desiredPath);
                }
                else
                {
                    Directory.CreateDirectory(desiredPath);
                }
                result.Add(pair.Key, desiredPath);
            }

            return result;
        }

        private static Dictionary<string, string> ScanExistingRoomFolders(string roomsRoot)
        {
            var result = new Dictionary<string, string>(StringComparer.Ordinal);
            string[] folders = Directory.GetDirectories(roomsRoot);
            Array.Sort(folders, StringComparer.OrdinalIgnoreCase);
            for (int index = 0; index < folders.Length; index++)
            {
                string roomJson = Path.Combine(folders[index], "room.json");
                if (!File.Exists(roomJson))
                {
                    throw new InvalidOperationException(
                        "Room folder '" + Path.GetFileName(folders[index])
                            + "' has no room.json. Unknown sidecars will not be adopted.");
                }

                RoomIdentityDto identity = ReadRoomIdentity(roomJson);
                if (result.ContainsKey(identity.room_id))
                {
                    throw new InvalidOperationException(
                        "Room ID '" + identity.room_id
                            + "' is owned by more than one existing folder.");
                }
                result.Add(identity.room_id, folders[index]);
            }
            return result;
        }

        private static LevelIdentityDto ReadLevelIdentity(string path)
        {
            LevelIdentityDto identity;
            try
            {
                identity = JsonUtility.FromJson<LevelIdentityDto>(File.ReadAllText(path));
            }
            catch (Exception exception)
            {
                throw new InvalidOperationException(
                    "Level identity JSON is malformed: " + path,
                    exception);
            }
            if (identity == null || string.IsNullOrWhiteSpace(identity.level_id))
            {
                throw new InvalidOperationException(
                    "Level identity JSON requires level_id: " + path);
            }
            identity.level_id = identity.level_id.Trim();
            return identity;
        }

        private static RoomIdentityDto ReadRoomIdentity(string path)
        {
            RoomIdentityDto identity;
            try
            {
                identity = JsonUtility.FromJson<RoomIdentityDto>(File.ReadAllText(path));
            }
            catch (Exception exception)
            {
                throw new InvalidOperationException(
                    "Room identity JSON is malformed: " + path,
                    exception);
            }
            if (identity == null || string.IsNullOrWhiteSpace(identity.room_id))
            {
                throw new InvalidOperationException(
                    "Room identity JSON requires room_id: " + path);
            }
            identity.room_id = identity.room_id.Trim();
            return identity;
        }

        private static string BuildRoomFolderName(LevelRoomAuthoring2D room)
        {
            return "Room_" + room.GridCoordinate.x + "_" + room.GridCoordinate.y
                + "_" + room.FolderSlot.ToString("00");
        }

        [Serializable]
        private sealed class LevelIdentityDto
        {
            public string level_id;
        }

        [Serializable]
        private sealed class RoomIdentityDto
        {
            public string room_id;
        }
    }
}
#endif
