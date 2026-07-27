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
    public static class LevelGridAuthoringV2JsonExporter
    {
        private static readonly UTF8Encoding Utf8WithoutBom = new UTF8Encoding(false);

        [MenuItem(
            "Tools/Shooter Mover/Level Design/Export Grid V2 Draft Folder...",
            priority = 250)]
        private static void ExportDraft()
        {
            ExportSelected(LevelGridValidationPurposeV2.Draft);
        }

        [MenuItem(
            "Tools/Shooter Mover/Level Design/Publish Grid V2 Validated Authoring Folder...",
            priority = 251)]
        private static void PublishProduction()
        {
            ExportSelected(LevelGridValidationPurposeV2.ProductionPublish);
        }

        private static void ExportSelected(LevelGridValidationPurposeV2 purpose)
        {
            LevelDesignSceneAuthoringRoot2D root = ResolveSelectedRoot();
            if (root == null)
            {
                EditorUtility.DisplayDialog(
                    "Level Grid Export",
                    "Select an object below a LevelDesignSceneAuthoringRoot2D.",
                    "OK");
                return;
            }

            LevelGridDoorOperationsV2.ReflowAll(root);
            LevelGridValidationResultV2 gridValidation =
                root.ValidateGridAuthoring(purpose);
            LevelDesignValidationResult foundationValidation =
                purpose == LevelGridValidationPurposeV2.ProductionPublish
                    ? root.ValidateHierarchy()
                    : null;

            if (purpose == LevelGridValidationPurposeV2.ProductionPublish
                && (foundationValidation == null
                    || !foundationValidation.IsValid
                    || !gridValidation.CanPublish))
            {
                int foundationErrors = foundationValidation == null
                    ? 1
                    : foundationValidation.ErrorCount;
                Debug.LogError(
                    "Validated authoring publish is blocked. Foundation errors: "
                        + foundationErrors + "; V2 graph errors: "
                        + gridValidation.ErrorCount + ".",
                    root);
                if (foundationValidation != null)
                {
                    LevelDesignSceneAuthoringRoot2DEditor.LogResult(
                        root,
                        foundationValidation);
                }
                LevelDesignSceneAuthoringRoot2DEditor.LogGridResult(
                    root,
                    gridValidation);
                LevelGridProblemsWindowV2.Open(root);
                return;
            }

            string outputRoot = EditorUtility.OpenFolderPanel(
                purpose == LevelGridValidationPurposeV2.ProductionPublish
                    ? "Publish Validated Level Grid V2 Authoring Folder"
                    : "Export Level Grid V2 Draft",
                UnityEngine.Application.dataPath,
                (root.LevelIdText ?? "level").Replace('.', '_'));
            if (string.IsNullOrEmpty(outputRoot))
            {
                return;
            }

            try
            {
                ExportTransaction(root, outputRoot, purpose);
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    "Level Grid V2 export failed. The destination was rolled back when "
                        + "possible, and any retained backup was left beside it. "
                        + exception.Message,
                    root);
                EditorUtility.DisplayDialog(
                    "Level Grid Export Blocked",
                    exception.Message,
                    "OK");
                return;
            }

            AssetDatabase.Refresh();
            EditorUtility.RevealInFinder(outputRoot);
            Debug.Log(
                "Level Grid Authoring V2 " + purpose + " written transactionally to "
                    + outputRoot,
                root);
        }

        private static void ExportTransaction(
            LevelDesignSceneAuthoringRoot2D root,
            string outputRoot,
            LevelGridValidationPurposeV2 purpose)
        {
            ValidateDestinationRoot(outputRoot, root.LevelIdText);

            DirectoryInfo outputInfo = new DirectoryInfo(outputRoot);
            string parent = outputInfo.Parent == null
                ? null
                : outputInfo.Parent.FullName;
            if (string.IsNullOrEmpty(parent))
            {
                throw new InvalidOperationException(
                    "Choose a dedicated level folder below a writable parent directory.");
            }

            string transactionId = Guid.NewGuid().ToString("N");
            string stageRoot = Path.Combine(
                parent,
                "." + outputInfo.Name + ".stage-" + transactionId);
            string backupRoot = Path.Combine(
                parent,
                "." + outputInfo.Name + ".backup-" + transactionId);

            bool outputExisted = Directory.Exists(outputRoot);
            try
            {
                if (outputExisted)
                {
                    CopyDirectory(outputRoot, stageRoot);
                }
                else
                {
                    Directory.CreateDirectory(stageRoot);
                }

                WriteLevelFolder(root, stageRoot, purpose);
                ValidateStagedPackage(root, stageRoot);
                CommitStagedDirectory(
                    outputRoot,
                    stageRoot,
                    backupRoot,
                    outputExisted);
            }
            catch
            {
                DeleteDirectoryIfExists(stageRoot);
                if (Directory.Exists(backupRoot) && !Directory.Exists(outputRoot))
                {
                    Directory.Move(backupRoot, outputRoot);
                }
                throw;
            }
            finally
            {
                // Stage data is disposable. A backup is not: if rollback could not
                // complete, leave it in place for manual recovery rather than deleting it.
                DeleteDirectoryIfExists(stageRoot);
            }
        }

        private static void ValidateDestinationRoot(string outputRoot, string levelId)
        {
            if (!Directory.Exists(outputRoot))
            {
                return;
            }

            string[] entries = Directory.GetFileSystemEntries(outputRoot);
            if (entries.Length == 0)
            {
                return;
            }

            string levelPath = Path.Combine(outputRoot, "level.json");
            if (!File.Exists(levelPath))
            {
                throw new InvalidOperationException(
                    "The selected folder is not empty and has no Level Grid V2 level.json. "
                    + "Choose an empty or previously exported dedicated level folder.");
            }

            LevelIdentityDtoV2 identity = ReadLevelIdentity(levelPath);
            if (!string.Equals(identity.level_id, levelId, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "The selected folder belongs to level '" + identity.level_id
                        + "', not '" + levelId + "'.");
            }
        }

        private static void WriteLevelFolder(
            LevelDesignSceneAuthoringRoot2D root,
            string outputRoot,
            LevelGridValidationPurposeV2 purpose)
        {
            LevelRoomAuthoring2D[] rooms =
                root.GetComponentsInChildren<LevelRoomAuthoring2D>(true);
            Array.Sort(rooms, CompareRooms);

            LevelDoorEndpointAuthoring2D[] doors =
                root.GetComponentsInChildren<LevelDoorEndpointAuthoring2D>(true);
            Array.Sort(doors, CompareDoors);

            LevelDoorLinkAuthoring2D[] connections =
                root.GetComponentsInChildren<LevelDoorLinkAuthoring2D>(true);
            Array.Sort(connections, CompareConnections);

            Dictionary<string, string> roomFolders = PrepareRoomFolders(
                rooms,
                Path.Combine(outputRoot, "Rooms"));

            string[] roomIds = new string[rooms.Length];
            MapNodeDtoV2[] nodes = new MapNodeDtoV2[rooms.Length];
            RoomIndexDtoV2[] roomIndex = new RoomIndexDtoV2[rooms.Length];
            for (int index = 0; index < rooms.Length; index++)
            {
                LevelRoomAuthoring2D room = rooms[index];
                roomIds[index] = room.RoomIdText;
                nodes[index] = new MapNodeDtoV2
                {
                    room_id = room.RoomIdText,
                    grid_position = new[]
                    {
                        room.GridCoordinate.x,
                        room.GridCoordinate.y,
                    },
                    slot = room.FolderSlot,
                    label = room.EditorLabel,
                    visible_on_map = room.VisibleOnMap,
                };
                roomIndex[index] = new RoomIndexDtoV2
                {
                    room_id = room.RoomIdText,
                    grid_position = new[]
                    {
                        room.GridCoordinate.x,
                        room.GridCoordinate.y,
                    },
                    slot = room.FolderSlot,
                    folder = Path.GetFileName(roomFolders[room.RoomIdText]),
                };
            }

            MapConnectionDtoV2[] mapConnections =
                new MapConnectionDtoV2[connections.Length];
            for (int index = 0; index < connections.Length; index++)
            {
                LevelDoorLinkAuthoring2D connection = connections[index];
                mapConnections[index] = new MapConnectionDtoV2
                {
                    connection_id = connection.ConnectionIdText,
                    from = BuildEndpoint(connection.SourceRoom, connection.SourceDoor),
                    to = BuildEndpoint(
                        connection.DestinationRoom,
                        connection.DestinationDoor),
                    travel_policy = connection.TravelPolicy.ToString(),
                };
            }

            WriteJson(
                Path.Combine(outputRoot, "level.json"),
                new LevelDtoV2
                {
                    schema_version = 2,
                    level_id = root.LevelIdText,
                    authoring_state =
                        purpose == LevelGridValidationPurposeV2.ProductionPublish
                            ? "validated-authoring"
                            : "draft",
                    milestone_scope = "track-a-phase-1-editor-foundation",
                    runtime_import_status = "not-connected",
                    room_ids = roomIds,
                    rooms = roomIndex,
                });
            WriteJson(
                Path.Combine(outputRoot, "map.json"),
                new MapDtoV2
                {
                    schema_version = 2,
                    nodes = nodes,
                    connections = mapConnections,
                });

            for (int index = 0; index < rooms.Length; index++)
            {
                WriteRoomFolder(
                    rooms[index],
                    doors,
                    roomFolders[rooms[index].RoomIdText]);
            }
        }

        private static Dictionary<string, string> PrepareRoomFolders(
            LevelRoomAuthoring2D[] rooms,
            string roomsRoot)
        {
            Directory.CreateDirectory(roomsRoot);
            Dictionary<string, string> existingByRoomId =
                ScanExistingRoomFolders(roomsRoot);
            Dictionary<string, LevelRoomAuthoring2D> activeByRoomId =
                new Dictionary<string, LevelRoomAuthoring2D>(StringComparer.Ordinal);
            HashSet<string> desiredNames = new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);

            for (int index = 0; index < rooms.Length; index++)
            {
                LevelRoomAuthoring2D room = rooms[index];
                if (string.IsNullOrWhiteSpace(room.RoomIdText)
                    || activeByRoomId.ContainsKey(room.RoomIdText))
                {
                    throw new InvalidOperationException(
                        "Draft export cannot allocate folders while room IDs are blank or duplicated.");
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

            Dictionary<string, string> temporaryByRoomId =
                new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, LevelRoomAuthoring2D> pair in activeByRoomId)
            {
                string existingPath;
                if (!existingByRoomId.TryGetValue(pair.Key, out existingPath))
                {
                    continue;
                }

                string temporaryPath = Path.Combine(
                    roomsRoot,
                    ".__migrate__" + Guid.NewGuid().ToString("N"));
                Directory.Move(existingPath, temporaryPath);
                temporaryByRoomId.Add(pair.Key, temporaryPath);
            }

            Dictionary<string, string> result =
                new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, LevelRoomAuthoring2D> pair in activeByRoomId)
            {
                string desiredPath = Path.Combine(
                    roomsRoot,
                    BuildRoomFolderName(pair.Value));
                if (Directory.Exists(desiredPath))
                {
                    RoomIdentityDtoV2 owner = ReadRoomIdentity(
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

        private static Dictionary<string, string> ScanExistingRoomFolders(
            string roomsRoot)
        {
            Dictionary<string, string> result =
                new Dictionary<string, string>(StringComparer.Ordinal);
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

                RoomIdentityDtoV2 identity = ReadRoomIdentity(roomJson);
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

        private static void WriteRoomFolder(
            LevelRoomAuthoring2D room,
            LevelDoorEndpointAuthoring2D[] allDoors,
            string roomRoot)
        {
            WriteJson(
                Path.Combine(roomRoot, "room.json"),
                new RoomDtoV2
                {
                    schema_version = 2,
                    room_id = room.RoomIdText,
                    display_name = room.DisplayName,
                    automatic_label = room.EditorLabel,
                    grid_position = new[]
                    {
                        room.GridCoordinate.x,
                        room.GridCoordinate.y,
                    },
                    slot = room.FolderSlot,
                    footprint_cells = new[]
                    {
                        room.FootprintCells.x,
                        room.FootprintCells.y,
                    },
                    visible_on_map = room.VisibleOnMap,
                });

            int count = 0;
            for (int index = 0; index < allDoors.Length; index++)
            {
                if (allDoors[index].OwningRoom == room)
                {
                    count++;
                }
            }

            DoorDtoV2[] roomDoors = new DoorDtoV2[count];
            int targetIndex = 0;
            for (int index = 0; index < allDoors.Length; index++)
            {
                LevelDoorEndpointAuthoring2D door = allDoors[index];
                if (door.OwningRoom != room)
                {
                    continue;
                }

                Vector3 localPosition = door.transform.localPosition;
                roomDoors[targetIndex++] = new DoorDtoV2
                {
                    door_id = door.DoorIdText,
                    side = door.Side.ToString(),
                    placement_mode = door.PlacementMode.ToString(),
                    edge_offset = door.EdgeOffset,
                    fixed_local_position = new[]
                    {
                        door.FixedLocalPosition.x,
                        door.FixedLocalPosition.y,
                    },
                    current_local_position = new[]
                    {
                        localPosition.x,
                        localPosition.y,
                    },
                    auto_face_connection = door.AutoFaceConnection,
                    traversable = door.Traversable,
                    visible_on_map = door.VisibleOnMap,
                };
            }

            WriteJson(
                Path.Combine(roomRoot, "doors.json"),
                new DoorsDtoV2
                {
                    schema_version = 2,
                    room_id = room.RoomIdText,
                    doors = roomDoors,
                });

            WriteSidecarIfMissing(
                Path.Combine(roomRoot, "floor.json"),
                new FloorScaffoldDtoV2
                {
                    schema_version = 2,
                    room = room.RoomIdText,
                    tiles = Array.Empty<string>(),
                });
            WriteSidecarIfMissing(
                Path.Combine(roomRoot, "enemies.json"),
                new EnemiesScaffoldDtoV2
                {
                    schema_version = 2,
                    room = room.RoomIdText,
                    enemies = Array.Empty<string>(),
                });
            WriteSidecarIfMissing(
                Path.Combine(roomRoot, "props.json"),
                new PropsScaffoldDtoV2
                {
                    schema_version = 2,
                    room = room.RoomIdText,
                    props = Array.Empty<string>(),
                });
            WriteSidecarIfMissing(
                Path.Combine(roomRoot, "decor.json"),
                new DecorScaffoldDtoV2
                {
                    schema_version = 2,
                    room = room.RoomIdText,
                    background = Array.Empty<string>(),
                    foreground = Array.Empty<string>(),
                });
            WriteSidecarIfMissing(
                Path.Combine(roomRoot, "encounter.json"),
                new EncounterScaffoldDtoV2
                {
                    schema_version = 2,
                    room = room.RoomIdText,
                    completion = "all-enemies",
                    optional_enemy_ids = Array.Empty<string>(),
                    door_rules = Array.Empty<string>(),
                });
        }

        private static void ValidateStagedPackage(
            LevelDesignSceneAuthoringRoot2D root,
            string stageRoot)
        {
            LevelIdentityDtoV2 levelIdentity = ReadLevelIdentity(
                Path.Combine(stageRoot, "level.json"));
            if (!string.Equals(
                levelIdentity.level_id,
                root.LevelIdText,
                StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Staged level identity changed unexpectedly.");
            }

            string roomsRoot = Path.Combine(stageRoot, "Rooms");
            Dictionary<string, string> existing = ScanExistingRoomFolders(roomsRoot);
            LevelRoomAuthoring2D[] rooms =
                root.GetComponentsInChildren<LevelRoomAuthoring2D>(true);
            for (int index = 0; index < rooms.Length; index++)
            {
                LevelRoomAuthoring2D room = rooms[index];
                string path;
                if (!existing.TryGetValue(room.RoomIdText, out path))
                {
                    throw new InvalidOperationException(
                        "Staged export is missing room '" + room.RoomIdText + "'.");
                }

                string expectedName = BuildRoomFolderName(room);
                if (!string.Equals(
                    Path.GetFileName(path),
                    expectedName,
                    StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException(
                        "Room '" + room.RoomIdText + "' was not migrated to '"
                            + expectedName + "'.");
                }
            }
        }

        private static void CommitStagedDirectory(
            string outputRoot,
            string stageRoot,
            string backupRoot,
            bool outputExisted)
        {
            bool destinationMoved = false;
            try
            {
                if (outputExisted)
                {
                    Directory.Move(outputRoot, backupRoot);
                    destinationMoved = true;
                }

                Directory.Move(stageRoot, outputRoot);
                if (destinationMoved)
                {
                    DeleteDirectoryIfExists(backupRoot);
                }
            }
            catch
            {
                if (Directory.Exists(outputRoot))
                {
                    DeleteDirectoryIfExists(outputRoot);
                }
                if (destinationMoved && Directory.Exists(backupRoot))
                {
                    Directory.Move(backupRoot, outputRoot);
                }
                throw;
            }
        }

        private static string BuildRoomFolderName(LevelRoomAuthoring2D room)
        {
            return "Room_" + room.GridCoordinate.x + "_" + room.GridCoordinate.y
                + "_" + room.FolderSlot.ToString("00");
        }

        private static LevelIdentityDtoV2 ReadLevelIdentity(string path)
        {
            if (!File.Exists(path))
            {
                throw new InvalidOperationException("Missing level.json: " + path);
            }

            try
            {
                LevelIdentityDtoV2 identity = JsonUtility.FromJson<LevelIdentityDtoV2>(
                    File.ReadAllText(path));
                if (identity == null || string.IsNullOrWhiteSpace(identity.level_id))
                {
                    throw new InvalidDataException("level_id is missing.");
                }
                return identity;
            }
            catch (Exception exception)
            {
                throw new InvalidOperationException(
                    "Malformed level identity file '" + path + "': "
                        + exception.Message,
                    exception);
            }
        }

        private static RoomIdentityDtoV2 ReadRoomIdentity(string path)
        {
            if (!File.Exists(path))
            {
                throw new InvalidOperationException("Missing room identity file: " + path);
            }

            try
            {
                RoomIdentityDtoV2 identity = JsonUtility.FromJson<RoomIdentityDtoV2>(
                    File.ReadAllText(path));
                if (identity == null || string.IsNullOrWhiteSpace(identity.room_id))
                {
                    throw new InvalidDataException("room_id is missing.");
                }
                return identity;
            }
            catch (Exception exception)
            {
                throw new InvalidOperationException(
                    "Malformed room identity file '" + path + "': "
                        + exception.Message,
                    exception);
            }
        }

        private static EndpointDtoV2 BuildEndpoint(
            LevelRoomAuthoring2D room,
            LevelDoorEndpointAuthoring2D door)
        {
            return new EndpointDtoV2
            {
                room_id = room == null ? string.Empty : room.RoomIdText,
                door_id = door == null ? string.Empty : door.DoorIdText,
            };
        }

        private static void WriteSidecarIfMissing(string path, object value)
        {
            if (!File.Exists(path))
            {
                WriteJson(path, value);
            }
        }

        private static void WriteJson(string path, object value)
        {
            File.WriteAllText(
                path,
                JsonUtility.ToJson(value, true) + Environment.NewLine,
                Utf8WithoutBom);
        }

        private static void CopyDirectory(string source, string destination)
        {
            Directory.CreateDirectory(destination);
            string[] files = Directory.GetFiles(source);
            for (int index = 0; index < files.Length; index++)
            {
                File.Copy(
                    files[index],
                    Path.Combine(destination, Path.GetFileName(files[index])),
                    true);
            }

            string[] directories = Directory.GetDirectories(source);
            for (int index = 0; index < directories.Length; index++)
            {
                CopyDirectory(
                    directories[index],
                    Path.Combine(destination, Path.GetFileName(directories[index])));
            }
        }

        private static void DeleteDirectoryIfExists(string path)
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, true);
            }
        }

        private static int CompareRooms(
            LevelRoomAuthoring2D left,
            LevelRoomAuthoring2D right)
        {
            int x = left.GridCoordinate.x.CompareTo(right.GridCoordinate.x);
            if (x != 0) return x;
            int y = left.GridCoordinate.y.CompareTo(right.GridCoordinate.y);
            if (y != 0) return y;
            int slot = left.FolderSlot.CompareTo(right.FolderSlot);
            if (slot != 0) return slot;
            return string.CompareOrdinal(left.RoomIdText, right.RoomIdText);
        }

        private static int CompareDoors(
            LevelDoorEndpointAuthoring2D left,
            LevelDoorEndpointAuthoring2D right)
        {
            int room = string.CompareOrdinal(
                left.OwningRoom == null ? string.Empty : left.OwningRoom.RoomIdText,
                right.OwningRoom == null ? string.Empty : right.OwningRoom.RoomIdText);
            return room != 0
                ? room
                : string.CompareOrdinal(left.DoorIdText, right.DoorIdText);
        }

        private static int CompareConnections(
            LevelDoorLinkAuthoring2D left,
            LevelDoorLinkAuthoring2D right)
        {
            return string.CompareOrdinal(
                left.ConnectionIdText,
                right.ConnectionIdText);
        }

        private static LevelDesignSceneAuthoringRoot2D ResolveSelectedRoot()
        {
            GameObject selected = Selection.activeGameObject;
            return selected == null
                ? null
                : selected.GetComponentInParent<LevelDesignSceneAuthoringRoot2D>();
        }

        [Serializable]
        private sealed class LevelIdentityDtoV2
        {
            public string level_id;
        }

        [Serializable]
        private sealed class LevelDtoV2
        {
            public int schema_version;
            public string level_id;
            public string authoring_state;
            public string milestone_scope;
            public string runtime_import_status;
            public string[] room_ids;
            public RoomIndexDtoV2[] rooms;
        }

        [Serializable]
        private sealed class RoomIndexDtoV2
        {
            public string room_id;
            public int[] grid_position;
            public int slot;
            public string folder;
        }

        [Serializable]
        private sealed class MapDtoV2
        {
            public int schema_version;
            public MapNodeDtoV2[] nodes;
            public MapConnectionDtoV2[] connections;
        }

        [Serializable]
        private sealed class MapNodeDtoV2
        {
            public string room_id;
            public int[] grid_position;
            public int slot;
            public string label;
            public bool visible_on_map;
        }

        [Serializable]
        private sealed class MapConnectionDtoV2
        {
            public string connection_id;
            public EndpointDtoV2 from;
            public EndpointDtoV2 to;
            public string travel_policy;
        }

        [Serializable]
        private sealed class EndpointDtoV2
        {
            public string room_id;
            public string door_id;
        }

        [Serializable]
        private sealed class RoomIdentityDtoV2
        {
            public string room_id;
        }

        [Serializable]
        private sealed class RoomDtoV2
        {
            public int schema_version;
            public string room_id;
            public string display_name;
            public string automatic_label;
            public int[] grid_position;
            public int slot;
            public int[] footprint_cells;
            public bool visible_on_map;
        }

        [Serializable]
        private sealed class DoorsDtoV2
        {
            public int schema_version;
            public string room_id;
            public DoorDtoV2[] doors;
        }

        [Serializable]
        private sealed class DoorDtoV2
        {
            public string door_id;
            public string side;
            public string placement_mode;
            public float edge_offset;
            public float[] fixed_local_position;
            public float[] current_local_position;
            public bool auto_face_connection;
            public bool traversable;
            public bool visible_on_map;
        }

        [Serializable]
        private sealed class FloorScaffoldDtoV2
        {
            public int schema_version;
            public string room;
            public string[] tiles;
        }

        [Serializable]
        private sealed class EnemiesScaffoldDtoV2
        {
            public int schema_version;
            public string room;
            public string[] enemies;
        }

        [Serializable]
        private sealed class PropsScaffoldDtoV2
        {
            public int schema_version;
            public string room;
            public string[] props;
        }

        [Serializable]
        private sealed class DecorScaffoldDtoV2
        {
            public int schema_version;
            public string room;
            public string[] background;
            public string[] foreground;
        }

        [Serializable]
        private sealed class EncounterScaffoldDtoV2
        {
            public int schema_version;
            public string room;
            public string completion;
            public string[] optional_enemy_ids;
            public string[] door_rules;
        }
    }
}
#endif
