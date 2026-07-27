using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;

namespace ShooterMover.Application.Missions.Rooms.Content
{
    public static partial class LevelGridV2Compiler
    {
        private sealed partial class Compiler
        {
            private static double[] DeriveArrival(RoomSource room, DoorSource door)
            {
                double x = door.LocalPosition[0];
                double y = door.LocalPosition[1];
                switch (door.Side)
                {
                    case "North": y -= ArrivalInwardOffset; break;
                    case "East": x -= ArrivalInwardOffset; break;
                    case "South": y += ArrivalInwardOffset; break;
                    default: x += ArrivalInwardOffset; break;
                }
                double minX = room.Center[0] - room.Size[0] * 0.5d + ArrivalBoundsMargin;
                double maxX = room.Center[0] + room.Size[0] * 0.5d - ArrivalBoundsMargin;
                double minY = room.Center[1] - room.Size[1] * 0.5d + ArrivalBoundsMargin;
                double maxY = room.Center[1] + room.Size[1] * 0.5d - ArrivalBoundsMargin;
                return new[] { Math.Max(minX, Math.Min(maxX, x)), Math.Max(minY, Math.Min(maxY, y)) };
            }

            private static double RotationForDoor(string side)
            {
                switch (side)
                {
                    case "North": return 90d;
                    case "East": return 0d;
                    case "South": return -90d;
                    default: return 180d;
                }
            }

            private static double RotationForArrival(string side)
            {
                switch (side)
                {
                    case "North": return -90d;
                    case "East": return 180d;
                    case "South": return 90d;
                    default: return 0d;
                }
            }

            private static string ArrivalId(string doorId) { return "arrival-" + SanitizeKey(doorId); }

            private static int CompareRoomIndex(RoomIndexDto left, RoomIndexDto right)
            {
                int x = left.GridPosition[0].CompareTo(right.GridPosition[0]);
                if (x != 0) return x;
                int y = left.GridPosition[1].CompareTo(right.GridPosition[1]);
                if (y != 0) return y;
                int slot = left.Slot.CompareTo(right.Slot);
                return slot != 0 ? slot : string.CompareOrdinal(left.RoomId, right.RoomId);
            }

            private T ReadRequired<T>(string path, string diagnosticPath) where T : class
            {
                string json;
                if (!source.TryGet(path, out json))
                {
                    throw Error("level-grid-v2-document-missing", diagnosticPath, "Missing required Level Grid V2 document: " + path);
                }
                return Deserialize<T>(json, diagnosticPath);
            }

            private static T Deserialize<T>(string json, string path) where T : class
            {
                if (string.IsNullOrWhiteSpace(json))
                {
                    throw Error("level-grid-v2-json-empty", path, "JSON content is required.");
                }
                try
                {
                    var serializer = new DataContractJsonSerializer(
                        typeof(T),
                        new DataContractJsonSerializerSettings { UseSimpleDictionaryFormat = true });
                    using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(json)))
                    {
                        T value = serializer.ReadObject(stream) as T;
                        if (value == null) throw Error("level-grid-v2-json-root-invalid", path, "JSON root must be an object.");
                        return value;
                    }
                }
                catch (MappingException) { throw; }
                catch (Exception exception)
                {
                    if (!(exception is SerializationException)
                        && !(exception is FormatException)
                        && !(exception is InvalidDataContractException))
                    {
                        throw;
                    }
                    throw Error("level-grid-v2-json-invalid", path, "Malformed Level Grid V2 JSON: " + exception.Message);
                }
            }

            private static string Serialize<T>(T value)
            {
                var serializer = new DataContractJsonSerializer(
                    typeof(T),
                    new DataContractJsonSerializerSettings { UseSimpleDictionaryFormat = true });
                using (var stream = new MemoryStream())
                {
                    serializer.WriteObject(stream, value);
                    return Encoding.UTF8.GetString(stream.ToArray());
                }
            }

            private void RequireVersion(int value, string path)
            {
                if (value != CurrentVersion) throw Error("level-grid-v2-version-unsupported", path, "Expected schema version 2 but received " + value + ".");
            }

            private static T Require<T>(T value, string path) where T : class
            {
                if (value == null) throw Error("level-grid-v2-value-required", path, "A value is required.");
                return value;
            }

            private static List<T> RequireList<T>(List<T> value, string path)
            {
                if (value == null) throw Error("level-grid-v2-array-required", path, "An array is required. Use [] when empty.");
                return value;
            }

            private static string RequireText(string value, string path)
            {
                if (string.IsNullOrWhiteSpace(value)) throw Error("level-grid-v2-value-required", path, "A non-empty value is required.");
                return value.Trim();
            }

            private static string RequireSafeFolder(string value, string path)
            {
                string folder = RequireText(value, path);
                if (folder.Contains("/") || folder.Contains("\\") || folder.Contains(".."))
                {
                    throw Error("level-grid-v2-folder-invalid", path, "Room folder must be one safe folder name.");
                }
                return folder;
            }

            private static string RequireSide(string value, string path)
            {
                string side = RequireText(value, path);
                if (!string.Equals(side, "North", StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(side, "East", StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(side, "South", StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(side, "West", StringComparison.OrdinalIgnoreCase))
                {
                    throw Error("level-grid-v2-door-side-invalid", path, "Door side must be North, East, South, or West.");
                }
                return char.ToUpperInvariant(side[0]) + side.Substring(1).ToLowerInvariant();
            }

            private static int[] RequireVector(int[] value, string path)
            {
                if (value == null || value.Length != 2) throw Error("level-grid-v2-vector-invalid", path, "A vector must contain exactly two values.");
                return value;
            }

            private static double[] RequireFiniteVector(double[] value, string path)
            {
                if (value == null || value.Length != 2 || !IsFinite(value[0]) || !IsFinite(value[1]))
                {
                    throw Error("level-grid-v2-vector-invalid", path, "A vector must contain exactly two finite values.");
                }
                return value;
            }

            private static bool IsFinite(double value) { return !double.IsNaN(value) && !double.IsInfinity(value); }

            private static void RequireSameVector(int[] expected, int[] actual, string path)
            {
                actual = RequireVector(actual, path);
                if (expected[0] != actual[0] || expected[1] != actual[1])
                {
                    throw Error("level-grid-v2-coordinate-mismatch", path, "Room index and room sidecar coordinates differ.");
                }
            }

            private static void RequireEqual(string expected, string actual, string path)
            {
                if (!string.Equals(expected, RequireText(actual, path), StringComparison.Ordinal))
                {
                    throw Error("level-grid-v2-identity-mismatch", path, "Expected " + expected + " but received " + actual + ".");
                }
            }

            private static void ValidateSidecarRoom(string expected, string actual, string path)
            {
                RequireEqual(expected, actual, path);
            }

            private static MappingException Error(string code, string path, string message)
            {
                return new MappingException(null, code, path, message);
            }

            private static string SanitizeKey(string value)
            {
                var builder = new StringBuilder(value == null ? 0 : value.Length);
                if (value != null)
                {
                    for (int i = 0; i < value.Length; i++)
                    {
                        char c = value[i];
                        builder.Append(char.IsLetterOrDigit(c) || c == '-' || c == '_' ? c : '-');
                    }
                }
                return builder.ToString();
            }
        }

        private sealed class RoomSource
        {
            public RoomSource(RoomIndexDto entry, RoomDto room, string root, double[] center, double[] size)
            {
                Entry = entry; Room = room; Root = root; Center = center; Size = size;
            }
            public RoomIndexDto Entry { get; }
            public RoomDto Room { get; }
            public string Root { get; }
            public double[] Center { get; }
            public double[] Size { get; }
            public List<DoorSource> Doors { get; } = new List<DoorSource>();
            public FloorDto Floor { get; set; }
            public EnemiesDto Enemies { get; set; }
            public PropsDto Props { get; set; }
            public DecorDto Decor { get; set; }
            public EncounterDto Encounter { get; set; }
        }

        private sealed class DoorSource
        {
            public DoorSource(RoomSource room, DoorDto dto, string doorId, string side, double[] localPosition)
            {
                Room = room; Dto = dto; DoorId = doorId; Side = side; LocalPosition = localPosition;
            }
            public RoomSource Room { get; }
            public DoorDto Dto { get; }
            public string DoorId { get; }
            public string Side { get; }
            public double[] LocalPosition { get; }
            public ConnectionDto Connection { get; set; }
            public DoorSource Other { get; set; }
        }

        private sealed class MappingException : Exception
        {
            public MappingException(string levelId, string code, string path, string message) : base(message)
            {
                LevelId = levelId; Code = code; Path = path;
            }
            public string LevelId { get; }
            public string Code { get; }
            public string Path { get; }
        }

    }
}
