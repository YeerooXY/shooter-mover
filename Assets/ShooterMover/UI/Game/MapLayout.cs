using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using ShooterMover.Domain.Common;
using ShooterMover.UnityAdapters.Authoring.LevelDesign;
using UnityEngine;

namespace ShooterMover.UI.Game
{
    /// <summary>
    /// Fits the authored room grid into centred UI coordinates.
    /// </summary>
    public sealed class MapLayout
    {
        private static readonly Vector2 RoomSize = new Vector2(92f, 56f);
        private static readonly Vector2 RoomStep = new Vector2(118f, 82f);
        private const float Padding = 48f;

        private readonly IReadOnlyList<Room> rooms;
        private readonly Dictionary<StableId, Room> roomsById;
        private readonly Dictionary<StableId, Teleporter> teleportersById;

        private MapLayout(List<Room> configuredRooms, Vector2 size, float scale)
        {
            rooms = configuredRooms.AsReadOnly();
            roomsById = new Dictionary<StableId, Room>();
            teleportersById = new Dictionary<StableId, Teleporter>();
            for (int index = 0; index < configuredRooms.Count; index++)
            {
                Room room = configuredRooms[index];
                roomsById.Add(room.RoomStableId, room);
                for (int teleporterIndex = 0;
                    teleporterIndex < room.Teleporters.Count;
                    teleporterIndex++)
                {
                    Teleporter teleporter = room.Teleporters[teleporterIndex];
                    if (teleportersById.ContainsKey(teleporter.TeleporterStableId))
                    {
                        throw new InvalidOperationException(
                            "map-layout-teleporter-duplicate:"
                            + teleporter.TeleporterStableId);
                    }
                    teleportersById.Add(
                        teleporter.TeleporterStableId,
                        teleporter);
                }
            }
            Size = size;
            Scale = scale;
        }

        public IReadOnlyList<Room> Rooms { get { return rooms; } }
        public Vector2 Size { get; }
        public float Scale { get; }

        public bool TryGetRoom(StableId roomStableId, out Room room)
        {
            room = null;
            return roomStableId != null
                && roomsById.TryGetValue(roomStableId, out room);
        }

        public bool TryGetTeleporter(
            StableId teleporterStableId,
            out Teleporter teleporter)
        {
            teleporter = null;
            return teleporterStableId != null
                && teleportersById.TryGetValue(
                    teleporterStableId,
                    out teleporter);
        }

        public static MapLayout Build(RoomFile source, Vector2 viewportSize)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (viewportSize.x <= 0f || viewportSize.y <= 0f)
                throw new ArgumentOutOfRangeException(nameof(viewportSize));
            if (source.Manifest == null)
                throw new InvalidOperationException("map-layout-manifest-missing");

            ManifestData manifest = Read<ManifestData>(
                source.Manifest.text,
                "manifest");
            if (manifest.rooms == null || manifest.rooms.Length == 0)
                throw new InvalidOperationException("map-layout-room-list-empty");

            StableId startRoom = StableId.Parse(Require(
                manifest.start_room,
                "map-layout-start-room-missing"));
            StableId exitRoom = StableId.Parse(Require(
                manifest.terminal_room,
                "map-layout-exit-room-missing"));
            Dictionary<string, string> documents = IndexDocuments(source.Documents);
            var sourceRooms = new List<RoomSource>(manifest.rooms.Length);
            var roomIds = new HashSet<StableId>();

            for (int index = 0; index < manifest.rooms.Length; index++)
            {
                RoomReference reference = manifest.rooms[index];
                string key = reference == null
                    ? null
                    : Require(
                        reference.layout,
                        "map-layout-room-reference-invalid:" + index);
                string json;
                if (key == null || !documents.TryGetValue(key, out json))
                {
                    throw new InvalidOperationException(
                        "map-layout-room-document-missing:" + key);
                }

                RoomData data = Read<RoomData>(json, key);
                StableId roomStableId = StableId.Parse(Require(
                    data.room,
                    "map-layout-room-id-missing:" + key));
                if (!roomIds.Add(roomStableId))
                {
                    throw new InvalidOperationException(
                        "map-layout-room-duplicate:" + roomStableId);
                }
                if (data.grid_position == null || data.grid_position.Length != 2)
                {
                    throw new InvalidOperationException(
                        "map-layout-grid-position-missing:" + roomStableId);
                }
                if (!ValidBounds(data.bounds))
                {
                    throw new InvalidOperationException(
                        "map-layout-room-bounds-invalid:" + roomStableId);
                }

                bool isStart = roomStableId == startRoom;
                bool isExit = roomStableId == exitRoom;
                Vector2? start = isStart ? FindStart(data.spawns) : null;
                Vector2? target = isExit ? FindTarget(data.doors) : null;
                if (isStart && !start.HasValue)
                {
                    throw new InvalidOperationException(
                        "map-layout-start-point-missing:" + roomStableId);
                }
                if (isExit && !target.HasValue)
                {
                    throw new InvalidOperationException(
                        "map-layout-target-point-missing:" + roomStableId);
                }

                Vector2 boundsCenter = new Vector2(
                    data.bounds.center[0],
                    data.bounds.center[1]);
                Vector2 boundsSize = new Vector2(
                    data.bounds.size[0],
                    data.bounds.size[1]);
                sourceRooms.Add(new RoomSource(
                    roomStableId,
                    string.IsNullOrWhiteSpace(data.display_name)
                        ? roomStableId.ToString()
                        : data.display_name.Trim(),
                    new Vector2Int(
                        data.grid_position[0],
                        data.grid_position[1]),
                    boundsCenter,
                    boundsSize,
                    start,
                    target,
                    ReadTeleporters(
                        roomStableId,
                        boundsCenter,
                        boundsSize,
                        data.teleporters)));
            }

            return Fit(sourceRooms, viewportSize);
        }

        private static List<TeleporterSource> ReadTeleporters(
            StableId roomStableId,
            Vector2 boundsCenter,
            Vector2 boundsSize,
            TeleporterData[] source)
        {
            TeleporterData[] values = source ?? Array.Empty<TeleporterData>();
            var result = new List<TeleporterSource>(values.Length);
            var ids = new HashSet<StableId>();
            Vector2 half = boundsSize * 0.5f;
            for (int index = 0; index < values.Length; index++)
            {
                TeleporterData value = values[index];
                if (value == null)
                {
                    throw new InvalidOperationException(
                        "map-layout-teleporter-invalid:"
                        + roomStableId
                        + ":"
                        + index);
                }
                StableId teleporterStableId = StableId.Parse(Require(
                    value.id,
                    "map-layout-teleporter-id-missing:"
                        + roomStableId
                        + ":"
                        + index));
                if (!ids.Add(teleporterStableId))
                {
                    throw new InvalidOperationException(
                        "map-layout-teleporter-duplicate:"
                        + teleporterStableId);
                }
                if (!ValidPoint(value.position)
                    || float.IsNaN(value.rotation)
                    || float.IsInfinity(value.rotation))
                {
                    throw new InvalidOperationException(
                        "map-layout-teleporter-position-invalid:"
                        + teleporterStableId);
                }
                if (!string.Equals(
                    value.unlock_when,
                    "room-complete",
                    StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException(
                        "map-layout-teleporter-unlock-unsupported:"
                        + teleporterStableId);
                }

                Vector2 position = new Vector2(
                    value.position[0],
                    value.position[1]);
                Vector2 fromCentre = position - boundsCenter;
                if (Mathf.Abs(fromCentre.x) > half.x
                    || Mathf.Abs(fromCentre.y) > half.y)
                {
                    throw new InvalidOperationException(
                        "map-layout-teleporter-outside-room:"
                        + teleporterStableId);
                }
                result.Add(new TeleporterSource(
                    teleporterStableId,
                    position,
                    value.rotation,
                    value.enabled));
            }
            return result;
        }

        private static MapLayout Fit(
            IReadOnlyList<RoomSource> sourceRooms,
            Vector2 viewportSize)
        {
            int minX = sourceRooms[0].Grid.x;
            int maxX = minX;
            int minY = sourceRooms[0].Grid.y;
            int maxY = minY;
            for (int index = 1; index < sourceRooms.Count; index++)
            {
                Vector2Int grid = sourceRooms[index].Grid;
                minX = Mathf.Min(minX, grid.x);
                maxX = Mathf.Max(maxX, grid.x);
                minY = Mathf.Min(minY, grid.y);
                maxY = Mathf.Max(maxY, grid.y);
            }

            Vector2 naturalSize = new Vector2(
                (maxX - minX) * RoomStep.x + RoomSize.x,
                (maxY - minY) * RoomStep.y + RoomSize.y);
            Vector2 available = new Vector2(
                Mathf.Max(1f, viewportSize.x - Padding * 2f),
                Mathf.Max(1f, viewportSize.y - Padding * 2f));
            float scale = Mathf.Min(
                1f,
                available.x / naturalSize.x,
                available.y / naturalSize.y);
            Vector2 fittedRoomSize = RoomSize * scale;
            Vector2 gridCentre = new Vector2(
                (minX + maxX) * 0.5f,
                (minY + maxY) * 0.5f);

            var rooms = new List<Room>(sourceRooms.Count);
            for (int index = 0; index < sourceRooms.Count; index++)
            {
                RoomSource source = sourceRooms[index];
                Vector2 centre = new Vector2(
                    (source.Grid.x - gridCentre.x) * RoomStep.x * scale,
                    (source.Grid.y - gridCentre.y) * RoomStep.y * scale);
                Rect rect = new Rect(
                    centre - fittedRoomSize * 0.5f,
                    fittedRoomSize);
                var teleporters = new List<Teleporter>(
                    source.Teleporters.Count);
                for (int teleporterIndex = 0;
                    teleporterIndex < source.Teleporters.Count;
                    teleporterIndex++)
                {
                    TeleporterSource teleporter =
                        source.Teleporters[teleporterIndex];
                    Vector2? mapPosition = Place(
                        rect,
                        source,
                        teleporter.LocalPosition);
                    if (!mapPosition.HasValue)
                        continue;
                    teleporters.Add(new Teleporter(
                        teleporter.TeleporterStableId,
                        source.RoomStableId,
                        mapPosition.Value,
                        teleporter.LocalPosition,
                        teleporter.LocalRotationDegrees,
                        teleporter.Enabled));
                }

                rooms.Add(new Room(
                    source.RoomStableId,
                    source.DisplayName,
                    source.Grid,
                    rect,
                    Place(rect, source, source.Start),
                    Place(rect, source, source.Target),
                    teleporters));
            }

            return new MapLayout(rooms, naturalSize * scale, scale);
        }

        private static Vector2? Place(
            Rect room,
            RoomSource source,
            Vector2? local)
        {
            if (!local.HasValue) return null;
            Vector2 half = source.BoundsSize * 0.5f;
            Vector2 normalized = new Vector2(
                (local.Value.x - source.BoundsCenter.x) / half.x,
                (local.Value.y - source.BoundsCenter.y) / half.y);
            normalized.x = Mathf.Clamp(normalized.x, -0.78f, 0.78f);
            normalized.y = Mathf.Clamp(normalized.y, -0.68f, 0.68f);
            return room.center + new Vector2(
                normalized.x * room.width * 0.5f,
                normalized.y * room.height * 0.5f);
        }

        private static Vector2? FindStart(SpawnData[] spawns)
        {
            SpawnData[] values = spawns ?? Array.Empty<SpawnData>();
            for (int index = 0; index < values.Length; index++)
            {
                SpawnData value = values[index];
                if (value != null
                    && string.Equals(
                        value.kind,
                        "player",
                        StringComparison.OrdinalIgnoreCase)
                    && ValidPoint(value.position))
                {
                    return new Vector2(value.position[0], value.position[1]);
                }
            }
            return null;
        }

        private static Vector2? FindTarget(DoorData[] doors)
        {
            DoorData[] values = doors ?? Array.Empty<DoorData>();
            for (int index = 0; index < values.Length; index++)
            {
                DoorData value = values[index];
                if (value != null
                    && value.link != null
                    && string.Equals(
                        value.link.kind,
                        "final-exit",
                        StringComparison.OrdinalIgnoreCase)
                    && ValidPoint(value.position))
                {
                    return new Vector2(value.position[0], value.position[1]);
                }
            }
            return null;
        }

        private static bool ValidBounds(BoundsData value)
        {
            return value != null
                && ValidPoint(value.center)
                && ValidPoint(value.size)
                && value.size[0] > 0f
                && value.size[1] > 0f;
        }

        private static bool ValidPoint(float[] value)
        {
            return value != null
                && value.Length == 2
                && !float.IsNaN(value[0])
                && !float.IsInfinity(value[0])
                && !float.IsNaN(value[1])
                && !float.IsInfinity(value[1]);
        }

        private static Dictionary<string, string> IndexDocuments(
            IReadOnlyList<RoomDocument> source)
        {
            var result = new Dictionary<string, string>(StringComparer.Ordinal);
            for (int index = 0; index < source.Count; index++)
            {
                RoomDocument document = source[index];
                if (document == null
                    || string.IsNullOrWhiteSpace(document.Key)
                    || document.Document == null)
                {
                    throw new InvalidOperationException(
                        "map-layout-document-invalid:" + index);
                }
                result.Add(document.Key.Trim(), document.Document.text);
            }
            return result;
        }

        private static T Read<T>(string json, string name)
            where T : class
        {
            if (string.IsNullOrWhiteSpace(json))
                throw new InvalidOperationException("map-layout-json-empty:" + name);
            try
            {
                T value = JsonUtility.FromJson<T>(json);
                if (value == null)
                    throw new InvalidOperationException(
                        "map-layout-json-root-invalid:" + name);
                return value;
            }
            catch (InvalidOperationException)
            {
                throw;
            }
            catch (Exception exception)
            {
                throw new InvalidOperationException(
                    "map-layout-json-invalid:" + name,
                    exception);
            }
        }

        private static string Require(string value, string error)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new InvalidOperationException(error);
            return value.Trim();
        }

        public sealed class Room
        {
            private readonly IReadOnlyList<Teleporter> teleporters;

            internal Room(
                StableId roomStableId,
                string displayName,
                Vector2Int grid,
                Rect rect,
                Vector2? start,
                Vector2? target,
                List<Teleporter> configuredTeleporters)
            {
                RoomStableId = roomStableId;
                DisplayName = displayName;
                Grid = grid;
                Rect = rect;
                Start = start;
                Target = target;
                teleporters = new ReadOnlyCollection<Teleporter>(
                    configuredTeleporters
                    ?? throw new ArgumentNullException(
                        nameof(configuredTeleporters)));
            }

            public StableId RoomStableId { get; }
            public string DisplayName { get; }
            public Vector2Int Grid { get; }
            public Rect Rect { get; }
            public Vector2? Start { get; }
            public Vector2? Target { get; }
            public IReadOnlyList<Teleporter> Teleporters
            {
                get { return teleporters; }
            }
        }

        public sealed class Teleporter
        {
            internal Teleporter(
                StableId teleporterStableId,
                StableId roomStableId,
                Vector2 mapPosition,
                Vector2 localPosition,
                float localRotationDegrees,
                bool enabled)
            {
                TeleporterStableId = teleporterStableId;
                RoomStableId = roomStableId;
                MapPosition = mapPosition;
                LocalPosition = localPosition;
                LocalRotationDegrees = localRotationDegrees;
                Enabled = enabled;
            }

            public StableId TeleporterStableId { get; }
            public StableId RoomStableId { get; }
            public Vector2 MapPosition { get; }
            public Vector2 LocalPosition { get; }
            public float LocalRotationDegrees { get; }
            public bool Enabled { get; }
        }

        private sealed class RoomSource
        {
            public RoomSource(
                StableId roomStableId,
                string displayName,
                Vector2Int grid,
                Vector2 boundsCenter,
                Vector2 boundsSize,
                Vector2? start,
                Vector2? target,
                List<TeleporterSource> teleporters)
            {
                RoomStableId = roomStableId;
                DisplayName = displayName;
                Grid = grid;
                BoundsCenter = boundsCenter;
                BoundsSize = boundsSize;
                Start = start;
                Target = target;
                Teleporters = teleporters
                    ?? throw new ArgumentNullException(nameof(teleporters));
            }

            public StableId RoomStableId { get; }
            public string DisplayName { get; }
            public Vector2Int Grid { get; }
            public Vector2 BoundsCenter { get; }
            public Vector2 BoundsSize { get; }
            public Vector2? Start { get; }
            public Vector2? Target { get; }
            public List<TeleporterSource> Teleporters { get; }
        }

        private sealed class TeleporterSource
        {
            public TeleporterSource(
                StableId teleporterStableId,
                Vector2 localPosition,
                float localRotationDegrees,
                bool enabled)
            {
                TeleporterStableId = teleporterStableId;
                LocalPosition = localPosition;
                LocalRotationDegrees = localRotationDegrees;
                Enabled = enabled;
            }

            public StableId TeleporterStableId { get; }
            public Vector2 LocalPosition { get; }
            public float LocalRotationDegrees { get; }
            public bool Enabled { get; }
        }

        [Serializable]
        private sealed class ManifestData
        {
            public string start_room;
            public string terminal_room;
            public RoomReference[] rooms;
        }

        [Serializable]
        private sealed class RoomReference
        {
            public string layout;
        }

        [Serializable]
        private sealed class RoomData
        {
            public string room;
            public string display_name;
            public int[] grid_position;
            public BoundsData bounds;
            public SpawnData[] spawns;
            public DoorData[] doors;
            public TeleporterData[] teleporters;
        }

        [Serializable]
        private sealed class BoundsData
        {
            public float[] center;
            public float[] size;
        }

        [Serializable]
        private sealed class SpawnData
        {
            public string kind;
            public float[] position;
        }

        [Serializable]
        private sealed class DoorData
        {
            public float[] position;
            public DoorLinkData link;
        }

        [Serializable]
        private sealed class DoorLinkData
        {
            public string kind;
        }

        [Serializable]
        private sealed class TeleporterData
        {
            public string id;
            public float[] position;
            public float rotation;
            public bool enabled;
            public string unlock_when;
        }
    }
}
