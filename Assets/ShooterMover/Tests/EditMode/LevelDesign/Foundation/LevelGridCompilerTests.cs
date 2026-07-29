#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using ShooterMover.Application.Missions.Rooms.Content;
using ShooterMover.Content.Definitions.Missions.Rooms;
using ShooterMover.UnityAdapters.Authoring.LevelDesign;
using UnityEngine;

namespace ShooterMover.Tests.EditMode.LevelDesign.Foundation
{
    public sealed class LevelGridCompilerTests
    {
        [Test]
        public void ValidThreeRoomPackage_CompilesThroughExistingV1Importer()
        {
            LevelGridCompileResult result = Compile(BuildValidSource());

            Assert.That(result.IsValid, Is.True, FirstIssue(result));
            Assert.That(result.LevelId, Is.EqualTo("level.test-level-1"));
            RoomContentImportResult imported = RoomContentJsonImporter.Import(
                result.Package,
                BuiltInRoomContentObjectCatalog.Create());
            Assert.That(imported.IsValid, Is.True, ImportIssue(imported));
            Assert.That(result.Package.Documents.Values.Any(
                json => json.Contains("arrival-door-single-west")), Is.True);
            Assert.That(result.Package.Documents.Values.Any(
                json => json.Contains("\"position\":[-10,0]")), Is.True);
        }

        [Test]
        public void MissingOrEmptyEncounter_DefaultsToAllEnemies()
        {
            Dictionary<string, string> source = BuildValidSource();
            source.Remove("Rooms/Room_0_0_01/encounter.json");
            source["Rooms/Room_2_0_01/encounter.json"] = "{}";

            LevelGridCompileResult result = Compile(source);

            Assert.That(result.IsValid, Is.True, FirstIssue(result));
            int defaults = result.Package.Documents.Values.Count(
                json => json.Contains("\"completion\":\"all-enemies\""));
            Assert.That(defaults, Is.EqualTo(3));
        }

        [Test]
        public void MalformedEncounter_IsImportErrorRatherThanEmptyEncounter()
        {
            Dictionary<string, string> source = BuildValidSource();
            source["Rooms/Room_1_0_01/encounter.json"] = "{not-json";

            AssertIssue(source, "level-level-1-json-invalid");
        }

        [Test]
        public void PartialEncounter_IsRejectedInsteadOfDefaulted()
        {
            Dictionary<string, string> source = BuildValidSource();
            source["Rooms/Room_1_0_01/encounter.json"] =
                "{\"room\":\"room.single\"}";

            AssertIssue(source, "level-level-1-json-invalid");
        }

        [Test]
        public void UnknownEncounterDoorReference_IsRejected()
        {
            Dictionary<string, string> source = BuildValidSource();
            source["Rooms/Room_1_0_01/encounter.json"] = "{" +
                "\"schema_version\":2," +
                "\"room\":\"room.single\"," +
                "\"completion\":\"all-enemies\"," +
                "\"optional_enemy_ids\":[]," +
                "\"door_rules\":[{" +
                "\"match\":{\"door_id\":\"door.typo\"}," +
                "\"open_when\":\"always\"}]}";

            AssertIssue(source, "level-level-1-encounter-door-unknown");
        }

        [Test]
        public void NullRequiredSidecarArray_IsRejected()
        {
            Dictionary<string, string> source = BuildValidSource();
            source["Rooms/Room_1_0_01/enemies.json"] = "{" +
                "\"schema_version\":2," +
                "\"room\":\"room.single\"," +
                "\"enemies\":null}";

            AssertIssue(source, "level-level-1-array-required");
        }

        [Test]
        public void UnknownRoomReference_IsRejected()
        {
            Dictionary<string, string> source = BuildValidSource();
            source["map.json"] = source["map.json"].Replace(
                "\"room_id\":\"room.double\",\"door_id\":\"door-double-west\"",
                "\"room_id\":\"room.unknown\",\"door_id\":\"door-double-west\"");

            AssertIssue(source, "level-level-1-room-reference-unknown");
        }

        [Test]
        public void DuplicateCoordinateAndSlot_IsRejected()
        {
            Dictionary<string, string> source = BuildValidSource();
            source["level.json"] = source["level.json"].Replace(
                "\"room_id\":\"room.single\",\"grid_position\":[1,0]",
                "\"room_id\":\"room.single\",\"grid_position\":[0,0]");

            AssertIssue(source, "level-level-1-coordinate-slot-duplicate");
        }

        [Test]
        public void MapCoordinateMismatch_IsRejected()
        {
            Dictionary<string, string> source = BuildValidSource();
            source["map.json"] = source["map.json"].Replace(
                "\"room_id\":\"room.single\",\"grid_position\":[1,0],\"slot\":1",
                "\"room_id\":\"room.single\",\"grid_position\":[8,3],\"slot\":1");

            AssertIssue(source, "level-level-1-coordinate-mismatch");
        }

        [Test]
        public void OneEndpointUsedByTwoConnections_IsRejected()
        {
            Dictionary<string, string> source = BuildValidSource();
            source["map.json"] = MapWithEndpointReuse();

            AssertIssue(source, "level-level-1-endpoint-reused");
        }

        [Test]
        public void TraversableUnresolvedDoor_IsRejected()
        {
            Dictionary<string, string> source = BuildValidSource();
            source["map.json"] = MapWithOnlyFirstConnection();

            AssertIssue(source, "level-level-1-traversable-door-unresolved");
        }

        [Test]
        public void MissingStartRoom_IsRejected()
        {
            Dictionary<string, string> source = BuildValidSource();
            source["level.json"] = source["level.json"].Replace(
                "\"start_room_id\":\"room.starter\"",
                "\"start_room_id\":\"room.missing\"");

            AssertIssue(source, "level-level-1-start-room-missing");
        }

        [Test]
        public void InaccessibleRequiredRoom_IsRejected()
        {
            Dictionary<string, string> source = BuildValidSource();
            source["map.json"] = MapWithOnlyFirstConnection();
            source["Rooms/Room_2_0_01/doors.json"] =
                source["Rooms/Room_2_0_01/doors.json"].Replace(
                    "\"door_id\":\"door-double-west\",\"side\":\"West\",\"current_local_position\":[-11,0],\"traversable\":true",
                    "\"door_id\":\"door-double-west\",\"side\":\"West\",\"current_local_position\":[-11,0],\"traversable\":false");
            source["Rooms/Room_1_0_01/doors.json"] =
                source["Rooms/Room_1_0_01/doors.json"].Replace(
                    "\"door_id\":\"door-single-east\",\"side\":\"East\",\"current_local_position\":[11,0],\"traversable\":true",
                    "\"door_id\":\"door-single-east\",\"side\":\"East\",\"current_local_position\":[11,0],\"traversable\":false");

            AssertIssue(source, "level-level-1-room-inaccessible");
        }

        [Test]
        public void InvalidFinalExitReference_IsRejected()
        {
            Dictionary<string, string> source = BuildValidSource();
            source["level.json"] = source["level.json"].Replace(
                "\"door_id\":\"door-double-exit\"",
                "\"door_id\":\"door.missing\"");

            AssertIssue(source, "level-level-1-final-exit-invalid");
        }

        [Test]
        public void MovingRooms_DoesNotReverseConnectionGameplay()
        {
            LevelGridCompileResult result = Compile(BuildValidSource(5, 0, -5));

            Assert.That(result.IsValid, Is.True, FirstIssue(result));
            string layout = FindDocument(result, "\"room\":\"room.single\"", "\"spawns\"");
            AssertFieldAfter(layout, "\"id\":\"door-single-west\"", "\"exit_type\":\"return\"");
            AssertFieldAfter(layout, "\"id\":\"door-single-east\"", "\"exit_type\":\"progression\"");

            string encounter = FindDocument(
                result,
                "\"room\":\"room.single\"",
                "\"door_rules\"");
            AssertFieldAfter(encounter, "\"door_id\":\"door-single-west\"", "\"open_when\":\"always\"");
            AssertFieldAfter(encounter, "\"door_id\":\"door-single-east\"", "\"open_when\":\"room-complete\"");
        }

        [Test]
        public void RoomFolderMigration_PreservesSidecarsByStableRoomId()
        {
            string root = Path.Combine(
                Path.GetTempPath(),
                "shooter-mover-level-1-" + Guid.NewGuid().ToString("N"));
            string roomsRoot = Path.Combine(root, "Rooms");
            string oldFolder = Path.Combine(roomsRoot, "Room_1_0_01");
            GameObject roomObject = new GameObject("Moved Room");
            try
            {
                Directory.CreateDirectory(oldFolder);
                File.WriteAllText(
                    Path.Combine(oldFolder, "room.json"),
                    "{\"room_id\":\"room.moved\"}");
                File.WriteAllText(
                    Path.Combine(oldFolder, "enemies.json"),
                    "preserved-sidecar");

                LevelRoom room = roomObject.AddComponent<LevelRoom>();
                room.ConfigureForTests(
                    "room.moved",
                    new Vector2Int(4, 0),
                    Vector2.one,
                    Vector2Int.one,
                    null);

                IReadOnlyDictionary<string, string> result =
                    LevelGridRoomFolderMigration.Prepare(
                        new[] { room },
                        roomsRoot);
                string movedFolder = result["room.moved"];

                Assert.That(Path.GetFileName(movedFolder), Is.EqualTo("Room_4_0_01"));
                Assert.That(Directory.Exists(oldFolder), Is.False);
                Assert.That(
                    File.ReadAllText(Path.Combine(movedFolder, "enemies.json")),
                    Is.EqualTo("preserved-sidecar"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(roomObject);
                if (Directory.Exists(root)) Directory.Delete(root, true);
            }
        }

        [Test]
        public void NestedDoorPosition_IsResolvedRelativeToOwningRoom()
        {
            GameObject roomObject = new GameObject("Room");
            GameObject helper = new GameObject("Helper");
            GameObject doorObject = new GameObject("Door");
            try
            {
                LevelRoom room = roomObject.AddComponent<LevelRoom>();
                room.ConfigureForTests(
                    "room.nested",
                    Vector2Int.zero,
                    Vector2.one,
                    Vector2Int.one,
                    null);
                roomObject.transform.position = new Vector3(10f, -5f, 0f);
                helper.transform.SetParent(roomObject.transform, false);
                helper.transform.localPosition = new Vector3(3f, 2f, 0f);
                doorObject.transform.SetParent(helper.transform, false);
                doorObject.transform.localPosition = new Vector3(1f, -1f, 0f);

                Vector2 local = LevelGridPlayableMetadata.ResolveDoorLocalPosition(
                    room,
                    doorObject.transform);

                Assert.That(local, Is.EqualTo(new Vector2(4f, 1f)));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(roomObject);
            }
        }

        private static void AssertIssue(
            Dictionary<string, string> source,
            string expectedCode)
        {
            LevelGridCompileResult result = Compile(source);
            Assert.That(result.IsValid, Is.False);
            Assert.That(result.Issues, Is.Not.Empty);
            Assert.That(result.Issues[0].Code, Is.EqualTo(expectedCode), result.Issues[0].ToString());
        }

        private static void AssertFieldAfter(string json, string anchor, string expected)
        {
            int anchorIndex = json.IndexOf(anchor, StringComparison.Ordinal);
            Assert.That(anchorIndex, Is.GreaterThanOrEqualTo(0), json);
            int expectedIndex = json.IndexOf(expected, anchorIndex, StringComparison.Ordinal);
            Assert.That(expectedIndex, Is.GreaterThan(anchorIndex), json);
        }

        private static string FindDocument(
            LevelGridCompileResult result,
            string firstMarker,
            string secondMarker)
        {
            return result.Package.Documents.Values.Single(
                json => json.Contains(firstMarker) && json.Contains(secondMarker));
        }

        private static LevelGridCompileResult Compile(Dictionary<string, string> source)
        {
            return LevelGridCompiler.Compile(new LevelGridSourcePackage(source));
        }

        private static string FirstIssue(LevelGridCompileResult result)
        {
            return result.Issues.Count == 0 ? string.Empty : result.Issues[0].ToString();
        }

        private static string ImportIssue(RoomContentImportResult result)
        {
            return result.Issues.Count == 0 ? string.Empty : result.Issues[0].ToString();
        }

        private static Dictionary<string, string> BuildValidSource(
            int starterX = 0,
            int singleX = 1,
            int doubleX = 2)
        {
            string starterFolder = Folder(starterX);
            string singleFolder = Folder(singleX);
            string doubleFolder = Folder(doubleX);
            var source = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["level.json"] = "{" +
                    "\"schema_version\":2," +
                    "\"level_id\":\"level.test-level-1\"," +
                    "\"start_room_id\":\"room.starter\"," +
                    "\"final_exit\":{\"room_id\":\"room.double\",\"door_id\":\"door-double-exit\"}," +
                    "\"room_ids\":[\"room.starter\",\"room.single\",\"room.double\"]," +
                    "\"rooms\":[" +
                    RoomIndex("room.starter", starterX, starterFolder) + "," +
                    RoomIndex("room.single", singleX, singleFolder) + "," +
                    RoomIndex("room.double", doubleX, doubleFolder) + "]}",
                ["map.json"] = FullMap(starterX, singleX, doubleX),
            };
            AddRoom(
                source,
                starterFolder,
                "room.starter",
                starterX,
                "\"player_start\":{\"position\":[-9,0],\"rotation\":0},",
                "[{\"door_id\":\"door-starter-east\",\"side\":\"East\",\"current_local_position\":[11,0],\"traversable\":true,\"runtime_object\":\"door.room-standard\"}]",
                "[]");
            AddRoom(
                source,
                singleFolder,
                "room.single",
                singleX,
                string.Empty,
                "[{\"door_id\":\"door-single-west\",\"side\":\"West\",\"current_local_position\":[-11,0],\"traversable\":true,\"runtime_object\":\"door.room-standard\"}," +
                "{\"door_id\":\"door-single-east\",\"side\":\"East\",\"current_local_position\":[11,0],\"traversable\":true,\"runtime_object\":\"door.room-standard\"}]",
                "[{\"id\":\"single-droid\",\"object\":\"enemy.moving-droid\",\"level\":1,\"position\":[4,0],\"rotation\":180}]");
            AddRoom(
                source,
                doubleFolder,
                "room.double",
                doubleX,
                string.Empty,
                "[{\"door_id\":\"door-double-west\",\"side\":\"West\",\"current_local_position\":[-11,0],\"traversable\":true,\"runtime_object\":\"door.room-standard\"}," +
                "{\"door_id\":\"door-double-exit\",\"side\":\"East\",\"current_local_position\":[11,0],\"traversable\":true,\"runtime_object\":\"door.room-standard\"}]",
                "[{\"id\":\"double-a\",\"object\":\"enemy.moving-droid\",\"level\":1,\"position\":[4,-3],\"rotation\":180}," +
                "{\"id\":\"double-b\",\"object\":\"enemy.moving-droid\",\"level\":1,\"position\":[4,3],\"rotation\":180}]");
            return source;
        }

        private static string Folder(int x)
        {
            return "Room_" + x + "_0_01";
        }

        private static string RoomIndex(string roomId, int x, string folder)
        {
            return "{\"room_id\":\"" + roomId + "\",\"grid_position\":[" + x
                + ",0],\"slot\":1,\"folder\":\"" + folder + "\"}";
        }

        private static void AddRoom(
            IDictionary<string, string> source,
            string folder,
            string roomId,
            int x,
            string playerStart,
            string doors,
            string enemies)
        {
            string root = "Rooms/" + folder + "/";
            source[root + "room.json"] = "{" +
                "\"schema_version\":2," +
                "\"room_id\":\"" + roomId + "\"," +
                "\"display_name\":\"" + roomId + "\"," +
                "\"grid_position\":[" + x + ",0]," +
                "\"slot\":1," + playerStart +
                "\"runtime_bounds\":{\"center\":[0,0],\"size\":[24,14]}}";
            source[root + "doors.json"] = "{" +
                "\"schema_version\":2,\"room_id\":\"" + roomId + "\",\"doors\":" + doors + "}";
            source[root + "floor.json"] = "{" +
                "\"schema_version\":2,\"room\":\"" + roomId + "\",\"tiles\":[{" +
                "\"object\":\"tile.floor-industrial\",\"fill\":{\"from\":[-12,-7],\"to\":[12,7]}}]}";
            source[root + "enemies.json"] = "{" +
                "\"schema_version\":2,\"room\":\"" + roomId + "\",\"enemies\":" + enemies + "}";
            source[root + "props.json"] = "{" +
                "\"schema_version\":2,\"room\":\"" + roomId + "\",\"props\":[]}";
            source[root + "decor.json"] = "{" +
                "\"schema_version\":2,\"room\":\"" + roomId + "\",\"background\":[],\"foreground\":[]}";
            source[root + "encounter.json"] = "{" +
                "\"schema_version\":2,\"room\":\"" + roomId + "\",\"completion\":\"all-enemies\",\"optional_enemy_ids\":[],\"door_rules\":[]}";
        }

        private static string FullMap(
            int starterX = 0,
            int singleX = 1,
            int doubleX = 2)
        {
            return "{" +
                "\"schema_version\":2," +
                "\"nodes\":[" +
                MapNode("room.starter", starterX) + "," +
                MapNode("room.single", singleX) + "," +
                MapNode("room.double", doubleX) + "]," +
                "\"connections\":[" +
                FirstConnection() + "," + SecondConnection() + "]}";
        }

        private static string MapWithOnlyFirstConnection()
        {
            return "{" +
                "\"schema_version\":2," +
                "\"nodes\":[" +
                MapNode("room.starter", 0) + "," +
                MapNode("room.single", 1) + "," +
                MapNode("room.double", 2) + "]," +
                "\"connections\":[" + FirstConnection() + "]}";
        }

        private static string MapWithEndpointReuse()
        {
            return "{" +
                "\"schema_version\":2," +
                "\"nodes\":[" +
                MapNode("room.starter", 0) + "," +
                MapNode("room.single", 1) + "," +
                MapNode("room.double", 2) + "]," +
                "\"connections\":[" + FirstConnection() + "," + SecondConnection() + ",{" +
                "\"connection_id\":\"connection.extra\"," +
                "\"from\":{\"room_id\":\"room.starter\",\"door_id\":\"door-starter-east\"}," +
                "\"to\":{\"room_id\":\"room.double\",\"door_id\":\"door-double-west\"}," +
                "\"travel_policy\":\"Bidirectional\"}]}";
        }

        private static string MapNode(string roomId, int x)
        {
            return "{\"room_id\":\"" + roomId + "\",\"grid_position\":[" + x
                + ",0],\"slot\":1}";
        }

        private static string FirstConnection()
        {
            return "{" +
                "\"connection_id\":\"connection.starter-single\"," +
                "\"from\":{\"room_id\":\"room.starter\",\"door_id\":\"door-starter-east\"}," +
                "\"to\":{\"room_id\":\"room.single\",\"door_id\":\"door-single-west\"}," +
                "\"travel_policy\":\"Bidirectional\"}";
        }

        private static string SecondConnection()
        {
            return "{" +
                "\"connection_id\":\"connection.single-double\"," +
                "\"from\":{\"room_id\":\"room.single\",\"door_id\":\"door-single-east\"}," +
                "\"to\":{\"room_id\":\"room.double\",\"door_id\":\"door-double-west\"}," +
                "\"travel_policy\":\"Bidirectional\"}";
        }
    }
}
#endif
