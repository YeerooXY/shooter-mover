using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;

namespace ShooterMover.Application.Missions.Rooms.Content
{
    /// <summary>
    /// Immutable in-memory view of one exported Level Grid V2 folder. Runtime code consumes the
    /// compiled Unity asset; only editor/build-time code is expected to populate this package.
    /// </summary>
    public sealed class LevelGridV2SourcePackage
    {
        private readonly Dictionary<string, string> documents;

        public LevelGridV2SourcePackage(IDictionary<string, string> documents)
        {
            if (documents == null) throw new ArgumentNullException(nameof(documents));
            this.documents = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, string> pair in documents)
            {
                string key = NormalizePath(pair.Key);
                if (string.IsNullOrEmpty(key))
                {
                    throw new ArgumentException("A Level Grid V2 document path is required.", nameof(documents));
                }
                if (pair.Value == null)
                {
                    throw new ArgumentException("Level Grid V2 document content cannot be null: " + key, nameof(documents));
                }
                if (this.documents.ContainsKey(key))
                {
                    throw new ArgumentException("Duplicate Level Grid V2 document path: " + key, nameof(documents));
                }
                this.documents.Add(key, pair.Value);
            }
        }

        public bool TryGet(string relativePath, out string json)
        {
            return documents.TryGetValue(NormalizePath(relativePath), out json);
        }

        private static string NormalizePath(string path)
        {
            return string.IsNullOrWhiteSpace(path)
                ? string.Empty
                : path.Trim().Replace('\\', '/').TrimStart('/');
        }
    }

    public sealed class LevelGridV2CompileIssue
    {
        public LevelGridV2CompileIssue(string code, string path, string message)
        {
            Code = code ?? string.Empty;
            Path = path ?? string.Empty;
            Message = message ?? string.Empty;
        }

        public string Code { get; }
        public string Path { get; }
        public string Message { get; }
        public override string ToString() { return Code + " at " + Path + ": " + Message; }
    }

    public sealed class LevelGridV2CompileResult
    {
        private readonly IReadOnlyList<LevelGridV2CompileIssue> issues;

        internal LevelGridV2CompileResult(
            string levelId,
            RoomContentJsonPackageV1 package,
            IReadOnlyList<LevelGridV2CompileIssue> issues)
        {
            LevelId = levelId;
            Package = package;
            this.issues = issues ?? Array.Empty<LevelGridV2CompileIssue>();
        }

        public string LevelId { get; }
        public RoomContentJsonPackageV1 Package { get; }
        public IReadOnlyList<LevelGridV2CompileIssue> Issues { get { return issues; } }
        public bool IsValid { get { return Package != null && issues.Count == 0; } }
    }

    /// <summary>
    /// Converts the stable V2 folder graph into the existing V1 room-content contract. The
    /// output is deterministic and contains no filesystem dependency. Door arrival positions are
    /// derived in owning-room local space by moving one unit inward from the destination door,
    /// then clamping the result inside the authored runtime bounds with a 0.5-unit safety margin.
    /// </summary>
    public static partial class LevelGridV2Compiler
    {
        public const int CurrentVersion = 2;
        private const double ArrivalInwardOffset = 1d;
        private const double ArrivalBoundsMargin = 0.5d;

        public static LevelGridV2CompileResult Compile(LevelGridV2SourcePackage source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            try
            {
                return new Compiler(source).Compile();
            }
            catch (MappingException exception)
            {
                return Failure(exception.LevelId, exception.Code, exception.Path, exception.Message);
            }
            catch (Exception exception)
            {
                if (IsFatal(exception)) throw;
                return Failure(null, "level-grid-v2-invalid", "$", exception.GetType().Name + ": " + exception.Message);
            }
        }

        private static LevelGridV2CompileResult Failure(
            string levelId,
            string code,
            string path,
            string message)
        {
            return new LevelGridV2CompileResult(
                levelId,
                null,
                new[] { new LevelGridV2CompileIssue(code, path, message) });
        }

        private static bool IsFatal(Exception exception)
        {
            return exception is OutOfMemoryException
                || exception is StackOverflowException
                || exception is AccessViolationException;
        }

        private sealed partial class Compiler
        {
            private readonly LevelGridV2SourcePackage source;
            private readonly Dictionary<string, RoomSource> rooms =
                new Dictionary<string, RoomSource>(StringComparer.Ordinal);
            private readonly Dictionary<string, DoorSource> doors =
                new Dictionary<string, DoorSource>(StringComparer.Ordinal);
            private readonly Dictionary<string, ConnectionDto> connectionByEndpoint =
                new Dictionary<string, ConnectionDto>(StringComparer.Ordinal);
            private LevelDto level;
            private MapDto map;

            public Compiler(LevelGridV2SourcePackage source)
            {
                this.source = source;
            }

            public LevelGridV2CompileResult Compile()
            {
                level = ReadRequired<LevelDto>("level.json", "$.level");
                map = ReadRequired<MapDto>("map.json", "$.map");
                RequireVersion(level.SchemaVersion, "$.level.schema_version");
                RequireVersion(map.SchemaVersion, "$.map.schema_version");
                string levelId = RequireText(level.LevelId, "$.level.level_id");
                string startRoomId = RequireText(level.StartRoomId, "$.level.start_room_id");
                EndpointDto finalExit = Require(level.FinalExit, "$.level.final_exit");
                string finalRoomId = RequireText(finalExit.RoomId, "$.level.final_exit.room_id");
                string finalDoorId = RequireText(finalExit.DoorId, "$.level.final_exit.door_id");

                LoadRooms();
                ValidateLevelRoomLists();
                ValidateMapNodes();
                LoadConnections();
                ValidateStartAndFinal(startRoomId, finalRoomId, finalDoorId);
                ValidateTraversableResolution(finalRoomId, finalDoorId);
                ValidateReachability(startRoomId);

                RoomContentJsonPackageV1 package = BuildV1Package(
                    levelId,
                    startRoomId,
                    finalRoomId,
                    finalDoorId);
                return new LevelGridV2CompileResult(
                    levelId,
                    package,
                    Array.Empty<LevelGridV2CompileIssue>());
            }

            private void LoadRooms()
            {
                List<RoomIndexDto> index = RequireList(level.Rooms, "$.level.rooms");
                if (index.Count == 0)
                {
                    throw Error("level-grid-v2-room-list-empty", "$.level.rooms", "A compiled level requires at least one room.");
                }

                var coordinateSlots = new HashSet<string>(StringComparer.Ordinal);
                var folders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                for (int i = 0; i < index.Count; i++)
                {
                    string path = "$.level.rooms[" + i + "]";
                    RoomIndexDto entry = Require(index[i], path);
                    string roomId = RequireText(entry.RoomId, path + ".room_id");
                    if (rooms.ContainsKey(roomId))
                    {
                        throw Error("level-grid-v2-room-id-duplicate", path + ".room_id", "Duplicate room stable ID: " + roomId);
                    }
                    int[] coordinate = RequireVector(entry.GridPosition, path + ".grid_position");
                    if (entry.Slot <= 0)
                    {
                        throw Error("level-grid-v2-room-slot-invalid", path + ".slot", "Room slot must be at least 1.");
                    }
                    string coordinateSlot = coordinate[0].ToString(CultureInfo.InvariantCulture)
                        + "," + coordinate[1].ToString(CultureInfo.InvariantCulture)
                        + ":" + entry.Slot.ToString(CultureInfo.InvariantCulture);
                    if (!coordinateSlots.Add(coordinateSlot))
                    {
                        throw Error("level-grid-v2-coordinate-slot-duplicate", path, "Duplicate coordinate+slot: " + coordinateSlot);
                    }
                    string folder = RequireSafeFolder(entry.Folder, path + ".folder");
                    if (!folders.Add(folder))
                    {
                        throw Error("level-grid-v2-folder-duplicate", path + ".folder", "Room folder is referenced more than once: " + folder);
                    }
                    string expectedFolder = "Room_" + coordinate[0] + "_" + coordinate[1] + "_" + entry.Slot.ToString("00", CultureInfo.InvariantCulture);
                    if (!string.Equals(folder, expectedFolder, StringComparison.OrdinalIgnoreCase))
                    {
                        throw Error("level-grid-v2-folder-coordinate-mismatch", path + ".folder", "Folder must match authored coordinate+slot: " + expectedFolder);
                    }

                    string root = "Rooms/" + folder + "/";
                    RoomDto room = ReadRequired<RoomDto>(root + "room.json", "$documents[\"" + root + "room.json\"]");
                    DoorsDto roomDoors = ReadRequired<DoorsDto>(root + "doors.json", "$documents[\"" + root + "doors.json\"]");
                    RequireVersion(room.SchemaVersion, root + "room.json.schema_version");
                    RequireVersion(roomDoors.SchemaVersion, root + "doors.json.schema_version");
                    RequireEqual(roomId, room.RoomId, root + "room.json.room_id");
                    RequireEqual(roomId, roomDoors.RoomId, root + "doors.json.room_id");
                    RequireSameVector(coordinate, room.GridPosition, root + "room.json.grid_position");
                    if (room.Slot != entry.Slot)
                    {
                        throw Error("level-grid-v2-room-slot-mismatch", root + "room.json.slot", "Room index and room sidecar slot differ.");
                    }
                    RuntimeBoundsDto bounds = Require(room.RuntimeBounds, root + "room.json.runtime_bounds");
                    double[] center = RequireFiniteVector(bounds.Center, root + "room.json.runtime_bounds.center");
                    double[] size = RequireFiniteVector(bounds.Size, root + "room.json.runtime_bounds.size");
                    if (size[0] <= 1d || size[1] <= 1d)
                    {
                        throw Error("level-grid-v2-room-bounds-invalid", root + "room.json.runtime_bounds.size", "Runtime bounds must be larger than the arrival safety margins.");
                    }

                    RoomSource roomSource = new RoomSource(entry, room, root, center, size);
                    LoadDoors(roomSource, roomDoors);
                    roomSource.Floor = ReadRequired<FloorDto>(root + "floor.json", "$documents[\"" + root + "floor.json\"]");
                    roomSource.Enemies = ReadRequired<EnemiesDto>(root + "enemies.json", "$documents[\"" + root + "enemies.json\"]");
                    roomSource.Props = ReadRequired<PropsDto>(root + "props.json", "$documents[\"" + root + "props.json\"]");
                    roomSource.Decor = ReadRequired<DecorDto>(root + "decor.json", "$documents[\"" + root + "decor.json\"]");
                    RequireVersion(roomSource.Floor.SchemaVersion, root + "floor.json.schema_version");
                    RequireVersion(roomSource.Enemies.SchemaVersion, root + "enemies.json.schema_version");
                    RequireVersion(roomSource.Props.SchemaVersion, root + "props.json.schema_version");
                    RequireVersion(roomSource.Decor.SchemaVersion, root + "decor.json.schema_version");
                    ValidateSidecarRoom(roomId, roomSource.Floor.Room, root + "floor.json.room");
                    ValidateSidecarRoom(roomId, roomSource.Enemies.Room, root + "enemies.json.room");
                    ValidateSidecarRoom(roomId, roomSource.Props.Room, root + "props.json.room");
                    ValidateSidecarRoom(roomId, roomSource.Decor.Room, root + "decor.json.room");
                    roomSource.Encounter = ReadEncounterOrDefault(root + "encounter.json", roomId);
                    ValidateRoomSidecars(roomSource);
                    rooms.Add(roomId, roomSource);
                }
            }

        }
    }
}
