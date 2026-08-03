using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using ShooterMover.Domain.Common;
using ShooterMover.UnityAdapters.Authoring.LevelDesign;
using UnityEngine;

namespace ShooterMover.UI.Game
{
    /// <summary>
    /// Reads authored room grid positions and fits the complete level map around the
    /// centre of a UI viewport. The returned room rectangles use centred UI coordinates.
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
                && roomsById.TryGetValue(roomStableId, out room)
                && room != null;
        }

        public static MapLayout Build(RoomFile source, Vector2 viewportSize)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (!IsFinitePositive(viewportSize.x)
                || !IsFinitePositive(viewportSize.y))
            {
                throw new ArgumentOutOfRangeException(nameof(viewportSize));
            }
            if (source.Manifest == null)
            {
                throw new InvalidOperationException("map-layout-manifest-missing");
            }

            ManifestDto manifest = ReadJson<ManifestDto>(
                source.Manifest.text,
                "manifest");
            if (manifest.Rooms == null || manifest.Rooms.Count == 0)
            {
                throw new InvalidOperationException("map-layout-room-list-empty");
            }

            Dictionary<string, string> documents = ReadDocuments(source.Documents);
            var sourceRooms = new List<SourceRoom>();
            var roomIds = new HashSet<StableId>();
            for (int index = 0; index < manifest.Rooms.Count; index++)
            {
                RoomReferenceDto reference = manifest.Rooms[index];
                if (reference == null || string.IsNullOrWhiteSpace(reference.Layout))
                {
                    throw new InvalidOperationException(
                        "map-layout-room-reference-invalid:" + index);
                }

                string key = reference.Layout.Trim();
                string json;
                if (!documents.TryGetValue(key, out json))
                {
                    throw new InvalidOperationException(
                        "map-layout-room-document-missing:" + key);
                }

                RoomDto value = ReadJson<RoomDto>(json, key);
                StableId roomStableId = StableId.Parse(RequireText(
                    value.Room,
                    "map-layout-room-id-missing:" + key));
                if (!roomIds.Add(roomStableId))
                {
                    throw new InvalidOperationException(
                        "map-layout-room-duplicate:" + roomStableId);
                }
                if (value.GridPosition == null || value.GridPosition.Length != 2)
                {
                    throw new InvalidOperationException(
                        "map-layout-grid-position-missing:" + roomStableId);
                }

                sourceRooms.Add(new SourceRoom(
                    roomStableId,
                    string.IsNullOrWhiteSpace(value.DisplayName)
                        ? roomStableId.ToString()
                        : value.DisplayName.Trim(),
                    new Vector2Int(value.GridPosition[0], value.GridPosition[1]),
                    string.Equals(
                        value.Room,
                        manifest.StartRoom,
                        StringComparison.Ordinal),
                    string.Equals(
                        value.Room,
                        manifest.TerminalRoom,
                        StringComparison.Ordinal)));
            }

            return Fit(sourceRooms, viewportSize);
        }

        private static MapLayout Fit(
            IReadOnlyList<SourceRoom> sourceRooms,
            Vector2 viewportSize)
        {
            int minX = sourceRooms[0].Grid.x;
            int maxX = minX;
            int minY = sourceRooms[0].Grid.y;
            int maxY = minY;
            for (int index = 1; index < sourceRooms.Count; index++)
            {
                Vector2Int grid = sourceRooms[index].Grid;
                minX = Math.Min(minX, grid.x);
                maxX = Math.Max(maxX, grid.x);
                minY = Math.Min(minY, grid.y);
                maxY = Math.Max(maxY, grid.y);
            }

            Vector2 naturalSize = new Vector2(
                (maxX - minX) * RoomStep.x + RoomSize.x,
                (maxY - minY) * RoomStep.y + RoomSize.y);
            Vector2 available = new Vector2(
                Math.Max(1f, viewportSize.x - Padding * 2f),
                Math.Max(1f, viewportSize.y - Padding * 2f));
            float scale = Math.Min(
                1f,
                Math.Min(
                    available.x / naturalSize.x,
                    available.y / naturalSize.y));

            float centreX = (minX + maxX) * 0.5f;
            float centreY = (minY + maxY) * 0.5f;
            Vector2 fittedRoomSize = RoomSize * scale;
            var rooms = new List<Room>(sourceRooms.Count);
            for (int index = 0; index < sourceRooms.Count; index++)
            {
                SourceRoom source = sourceRooms[index];
                Vector2 centre = new Vector2(
                    (source.Grid.x - centreX) * RoomStep.x * scale,
                    (source.Grid.y - centreY) * RoomStep.y * scale);
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

        private static Dictionary<string, string> ReadDocuments(
            IReadOnlyList<RoomDocument> source)
        {
            var result = new Dictionary<string, string>(StringComparer.Ordinal);
            if (source == null) return result;
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
                string key = document.Key.Trim();
                if (result.ContainsKey(key))
                {
                    throw new InvalidOperationException(
                        "map-layout-document-duplicate:" + key);
                }
                result.Add(key, document.Document.text);
            }
            return result;
        }

        private static T ReadJson<T>(string json, string source)
            where T : class
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                throw new InvalidOperationException(
                    "map-layout-json-empty:" + source);
            }

            try
            {
                var serializer = new DataContractJsonSerializer(typeof(T));
                using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(json)))
                {
                    T value = serializer.ReadObject(stream) as T;
                    if (value == null)
                    {
                        throw new InvalidOperationException(
                            "map-layout-json-root-invalid:" + source);
                    }
                    return value;
                }
            }
            catch (InvalidOperationException)
            {
                throw;
            }
            catch (Exception exception)
            {
                throw new InvalidOperationException(
                    "map-layout-json-invalid:" + source,
                    exception);
            }
        }

        private static string RequireText(string value, string error)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new InvalidOperationException(error);
            }
            return value.Trim();
        }

        private static bool IsFinitePositive(float value)
        {
            return !float.IsNaN(value)
                && !float.IsInfinity(value)
                && value > 0f;
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

        private sealed class SourceRoom
        {
            public SourceRoom(
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

        [DataContract]
        private sealed class ManifestDto
        {
            [DataMember(Name = "start_room", IsRequired = true)]
            public string StartRoom { get; set; }

            [DataMember(Name = "terminal_room", IsRequired = true)]
            public string TerminalRoom { get; set; }

            [DataMember(Name = "rooms", IsRequired = true)]
            public List<RoomReferenceDto> Rooms { get; set; }
        }

        [DataContract]
        private sealed class RoomReferenceDto
        {
            [DataMember(Name = "layout", IsRequired = true)]
            public string Layout { get; set; }
        }

        [DataContract]
        private sealed class RoomDto
        {
            [DataMember(Name = "room", IsRequired = true)]
            public string Room { get; set; }

            [DataMember(Name = "display_name", IsRequired = true)]
            public string DisplayName { get; set; }

            [DataMember(Name = "grid_position", IsRequired = true)]
            public int[] GridPosition { get; set; }
        }
    }
}
