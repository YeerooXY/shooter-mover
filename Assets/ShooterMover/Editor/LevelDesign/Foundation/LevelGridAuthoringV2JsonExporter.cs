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
            "Tools/Shooter Mover/Level Design/Publish Grid V2 Production Folder...",
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

            LevelGridValidationResultV2 validation =
                root.ValidateGridAuthoring(purpose);
            if (purpose == LevelGridValidationPurposeV2.ProductionPublish
                && !validation.CanPublish)
            {
                Debug.LogError(
                    "Production publishing is blocked by " + validation.ErrorCount
                        + " level-grid problem(s).",
                    root);
                LevelGridProblemsWindowV2.Open(root);
                return;
            }

            string outputRoot = EditorUtility.OpenFolderPanel(
                purpose == LevelGridValidationPurposeV2.ProductionPublish
                    ? "Publish Level Grid V2"
                    : "Export Level Grid V2 Draft",
                UnityEngine.Application.dataPath,
                (root.LevelIdText ?? "level").Replace('.', '_'));
            if (string.IsNullOrEmpty(outputRoot))
            {
                return;
            }

            Directory.CreateDirectory(outputRoot);
            WriteLevelFolder(root, outputRoot, purpose);
            AssetDatabase.Refresh();
            EditorUtility.RevealInFinder(outputRoot);
            Debug.Log(
                "Level Grid Authoring V2 " + purpose + " written to " + outputRoot,
                root);
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

            string[] roomIds = new string[rooms.Length];
            MapNodeDtoV2[] nodes = new MapNodeDtoV2[rooms.Length];
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
                    label = room.EditorLabel,
                    visible_on_map = room.VisibleOnMap,
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
                            ? "production"
                            : "draft",
                    room_ids = roomIds,
                });
            WriteJson(
                Path.Combine(outputRoot, "map.json"),
                new MapDtoV2
                {
                    schema_version = 2,
                    nodes = nodes,
                    connections = mapConnections,
                });

            string roomsRoot = Path.Combine(outputRoot, "Rooms");
            Directory.CreateDirectory(roomsRoot);
            HashSet<string> claimedRoomFolders = new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);
            for (int index = 0; index < rooms.Length; index++)
            {
                WriteRoomFolder(
                    rooms[index],
                    doors,
                    roomsRoot,
                    index + 1,
                    claimedRoomFolders);
            }
        }

        private static void WriteRoomFolder(
            LevelRoomAuthoring2D room,
            LevelDoorEndpointAuthoring2D[] allDoors,
            string roomsRoot,
            int ordinal,
            ISet<string> claimedRoomFolders)
        {
            string preferredFolderName = "Room_"
                + room.GridCoordinate.x + "_"
                + room.GridCoordinate.y + "_"
                + ordinal.ToString("00");
            string roomRoot = ResolveRoomFolder(
                roomsRoot,
                room.RoomIdText,
                preferredFolderName,
                claimedRoomFolders);
            Directory.CreateDirectory(roomRoot);

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

            WriteScaffoldIfMissing(
                Path.Combine(roomRoot, "floor.json"),
                room.RoomIdText,
                "floor");
            WriteScaffoldIfMissing(
                Path.Combine(roomRoot, "enemies.json"),
                room.RoomIdText,
                "enemies");
            WriteScaffoldIfMissing(
                Path.Combine(roomRoot, "props.json"),
                room.RoomIdText,
                "props");
            WriteScaffoldIfMissing(
                Path.Combine(roomRoot, "decor.json"),
                room.RoomIdText,
                "decor");
            WriteScaffoldIfMissing(
                Path.Combine(roomRoot, "encounter.json"),
                room.RoomIdText,
                "encounter");
        }

        private static string ResolveRoomFolder(
            string roomsRoot,
            string roomId,
            string preferredFolderName,
            ISet<string> claimedRoomFolders)
        {
            string[] existingFolders = Directory.GetDirectories(roomsRoot);
            Array.Sort(existingFolders, StringComparer.OrdinalIgnoreCase);
            for (int index = 0; index < existingFolders.Length; index++)
            {
                string roomJsonPath = Path.Combine(existingFolders[index], "room.json");
                if (!File.Exists(roomJsonPath))
                {
                    continue;
                }

                try
                {
                    RoomIdentityDtoV2 identity = JsonUtility.FromJson<RoomIdentityDtoV2>(
                        File.ReadAllText(roomJsonPath));
                    if (identity != null
                        && string.Equals(
                            identity.room_id,
                            roomId,
                            StringComparison.Ordinal)
                        && claimedRoomFolders.Add(existingFolders[index]))
                    {
                        return existingFolders[index];
                    }
                }
                catch (Exception exception)
                {
                    Debug.LogWarning(
                        "Could not inspect existing room folder '"
                            + existingFolders[index] + "': " + exception.Message);
                }
            }

            string candidate = Path.Combine(roomsRoot, preferredFolderName);
            int suffix = 2;
            while (Directory.Exists(candidate)
                || claimedRoomFolders.Contains(candidate))
            {
                candidate = Path.Combine(
                    roomsRoot,
                    preferredFolderName + "_" + suffix.ToString("00"));
                suffix++;
            }

            claimedRoomFolders.Add(candidate);
            return candidate;
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

        private static void WriteScaffoldIfMissing(
            string path,
            string roomId,
            string contentKind)
        {
            if (File.Exists(path))
            {
                return;
            }

            WriteJson(
                path,
                new RoomSidecarScaffoldDtoV2
                {
                    schema_version = 2,
                    room_id = roomId,
                    content_kind = contentKind,
                    items = Array.Empty<string>(),
                });
        }

        private static void WriteJson(string path, object value)
        {
            File.WriteAllText(
                path,
                JsonUtility.ToJson(value, true) + Environment.NewLine,
                Utf8WithoutBom);
        }

        private static int CompareRooms(
            LevelRoomAuthoring2D left,
            LevelRoomAuthoring2D right)
        {
            int x = left.GridCoordinate.x.CompareTo(right.GridCoordinate.x);
            if (x != 0) return x;
            int y = left.GridCoordinate.y.CompareTo(right.GridCoordinate.y);
            if (y != 0) return y;
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
        private sealed class LevelDtoV2
        {
            public int schema_version;
            public string level_id;
            public string authoring_state;
            public string[] room_ids;
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
            public bool traversable;
            public bool visible_on_map;
        }

        [Serializable]
        private sealed class RoomSidecarScaffoldDtoV2
        {
            public int schema_version;
            public string room_id;
            public string content_kind;
            public string[] items;
        }
    }
}
#endif
