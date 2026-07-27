#if UNITY_EDITOR
using System;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using ShooterMover.UnityAdapters.Authoring.LevelDesign;
using UnityEditor;
using UnityEngine;

namespace ShooterMover.Editor.LevelDesign.Foundation
{
    public static partial class LevelGridV2PlayableExporter
    {
        private static RuntimeBoundsDto ResolveRoomLocalBounds(LevelRoomAuthoring2D room)
        {
            if (room.RoomBounds == null)
            {
                throw new InvalidOperationException(
                    "Playable Grid V2 export requires room bounds for " + room.RoomIdText + ".");
            }
            Bounds world = room.RoomBounds.bounds;
            Vector3 min = room.transform.InverseTransformPoint(world.min);
            Vector3 max = room.transform.InverseTransformPoint(world.max);
            return new RuntimeBoundsDto
            {
                center = new[] { (min.x + max.x) * 0.5f, (min.y + max.y) * 0.5f },
                size = new[] { Mathf.Abs(max.x - min.x), Mathf.Abs(max.y - min.y) },
            };
        }

        private static EndpointDto Endpoint(
            LevelRoomAuthoring2D room,
            LevelDoorEndpointAuthoring2D door)
        {
            return new EndpointDto
            {
                room_id = room == null ? string.Empty : room.RoomIdText,
                door_id = door == null ? string.Empty : door.DoorIdText,
            };
        }

        private static string BuildRoomFolderName(LevelRoomAuthoring2D room)
        {
            return "Room_" + room.GridCoordinate.x + "_" + room.GridCoordinate.y
                + "_" + room.FolderSlot.ToString("00");
        }

        private static void WriteJsonIfMissing(string path, object value)
        {
            if (!File.Exists(path)) WriteJson(path, value);
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
            foreach (string file in Directory.GetFiles(source))
            {
                File.Copy(file, Path.Combine(destination, Path.GetFileName(file)), true);
            }
            foreach (string directory in Directory.GetDirectories(source))
            {
                CopyDirectory(
                    directory,
                    Path.Combine(destination, Path.GetFileName(directory)));
            }
        }

        private static LevelDesignSceneAuthoringRoot2D ResolveSelectedRoot()
        {
            GameObject selected = Selection.activeGameObject;
            return selected == null
                ? null
                : selected.GetComponentInParent<LevelDesignSceneAuthoringRoot2D>();
        }

        private static int CompareRooms(LevelRoomAuthoring2D left, LevelRoomAuthoring2D right)
        {
            int x = left.GridCoordinate.x.CompareTo(right.GridCoordinate.x);
            if (x != 0) return x;
            int y = left.GridCoordinate.y.CompareTo(right.GridCoordinate.y);
            if (y != 0) return y;
            int slot = left.FolderSlot.CompareTo(right.FolderSlot);
            return slot != 0 ? slot : string.CompareOrdinal(left.RoomIdText, right.RoomIdText);
        }

        private static int CompareDoors(LevelDoorEndpointAuthoring2D left, LevelDoorEndpointAuthoring2D right)
        {
            int room = string.CompareOrdinal(
                left.OwningRoom == null ? string.Empty : left.OwningRoom.RoomIdText,
                right.OwningRoom == null ? string.Empty : right.OwningRoom.RoomIdText);
            return room != 0 ? room : string.CompareOrdinal(left.DoorIdText, right.DoorIdText);
        }

        private static int CompareLinks(LevelDoorLinkAuthoring2D left, LevelDoorLinkAuthoring2D right)
        {
            return string.CompareOrdinal(left.ConnectionIdText, right.ConnectionIdText);
        }

        [Serializable] private sealed class LevelDto
        {
            public int schema_version;
            public string level_id;
            public string authoring_state;
            public string runtime_import_status;
            public string start_room_id;
            public EndpointDto final_exit;
            public string[] room_ids;
            public RoomIndexDto[] rooms;
        }
        [Serializable] private sealed class RoomIndexDto
        {
            public string room_id;
            public int[] grid_position;
            public int slot;
            public string folder;
        }
        [Serializable] private sealed class MapDto
        {
            public int schema_version;
            public MapNodeDto[] nodes;
            public ConnectionDto[] connections;
        }
        [Serializable] private sealed class MapNodeDto
        {
            public string room_id;
            public int[] grid_position;
            public int slot;
            public string label;
            public bool visible_on_map;
        }
        [Serializable] private sealed class ConnectionDto
        {
            public string connection_id;
            public EndpointDto from;
            public EndpointDto to;
            public string travel_policy;
        }
        [Serializable] private sealed class EndpointDto
        {
            public string room_id;
            public string door_id;
        }
        [Serializable] private sealed class RoomDto
        {
            public int schema_version;
            public string room_id;
            public string display_name;
            public int[] grid_position;
            public int slot;
            public int[] footprint_cells;
            public RuntimeBoundsDto runtime_bounds;
            public PlayerStartDto player_start;
        }
        [Serializable] private sealed class RuntimeBoundsDto
        {
            public float[] center;
            public float[] size;
        }
        [Serializable] private sealed class PlayerStartDto
        {
            public float[] position;
            public float rotation;
        }
        [Serializable] private sealed class DoorsDto
        {
            public int schema_version;
            public string room_id;
            public DoorDto[] doors;
        }
        [Serializable] private sealed class DoorDto
        {
            public string door_id;
            public string side;
            public string placement_mode;
            public float[] current_local_position;
            public bool traversable;
            public bool visible_on_map;
            public string runtime_object;
        }
        [Serializable] private sealed class FloorDto
        {
            public int schema_version;
            public string room;
            public TileDto[] tiles;
        }
        [Serializable] private sealed class TileDto
        {
            public string @object;
            public FillDto fill;
        }
        [Serializable] private sealed class FillDto
        {
            public int[] from;
            public int[] to;
        }
        [Serializable] private sealed class EnemiesDto
        {
            public int schema_version;
            public string room;
            public EnemyDto[] enemies;
        }
        [Serializable] private sealed class EnemyDto
        {
            public string id;
            public string @object;
            public int level;
            public float[] position;
            public float rotation;
        }
        [Serializable] private sealed class PropsDto
        {
            public int schema_version;
            public string room;
            public PropDto[] props;
        }
        [Serializable] private sealed class PropDto
        {
            public string id;
            public string @object;
            public float[] position;
            public float rotation;
        }
        [Serializable] private sealed class DecorDto
        {
            public int schema_version;
            public string room;
            public VisualDto[] background;
            public VisualDto[] foreground;
        }
        [Serializable] private sealed class VisualDto
        {
            public string @object;
            public float[] position;
            public float rotation;
        }
    }
}
#endif
