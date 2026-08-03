using System;
using System.Collections.Generic;
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

        private MapLayout(List<Room> configuredRooms, Vector2 size, float scale)
        {
            rooms = configuredRooms.AsReadOnly();
            roomsById = new Dictionary<StableId, Room>();
            for (int index = 0; index < configuredRooms.Count; index++)
            {
                Room room = configuredRooms[index];
                roomsById.Add(room.RoomStableId, room);
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

                sourceRooms.Add(new RoomSource(
                    roomStableId,
                    string.IsNullOrWhiteSpace(data.display_name)
                        ? roomStableId.ToString()
                        : data.display_name.Trim(),
                    new Vector2Int(
                        data.grid_position[0],
                        data.grid_position[1]),
                    roomStableId == startRoom,
                    roomStableId == exitRoom));
            }

            return Fit(sourceRooms, viewportSize);
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
                rooms.Add(new Room(
                    source.RoomStableId,
                    source.DisplayName,
                    source.Grid,
                    new Rect(centre - fittedRoomSize * 0.5f, fittedRoomSize),
                    source.IsStart,
                    source.IsExit));
            }

            return new MapLayout(rooms, naturalSize * scale, scale);
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
            internal Room(
                StableId roomStableId,
                string displayName,
                Vector2Int grid,
                Rect rect,
                bool isStart,
                bool isExit)
            {
                RoomStableId = roomStableId;
                DisplayName = displayName;
                Grid = grid;
                Rect = rect;
                IsStart = isStart;
                IsExit = isExit;
            }

            public StableId RoomStableId { get; }
            public string DisplayName { get; }
            public Vector2Int Grid { get; }
            public Rect Rect { get; }
            public bool IsStart { get; }
            public bool IsExit { get; }
        }

        private sealed class RoomSource
        {
            public RoomSource(
                StableId roomStableId,
                string displayName,
                Vector2Int grid,
                bool isStart,
                bool isExit)
            {
                RoomStableId = roomStableId;
                DisplayName = displayName;
                Grid = grid;
                IsStart = isStart;
                IsExit = isExit;
            }

            public StableId RoomStableId { get; }
            public string DisplayName { get; }
            public Vector2Int Grid { get; }
            public bool IsStart { get; }
            public bool IsExit { get; }
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
        }
    }
}
