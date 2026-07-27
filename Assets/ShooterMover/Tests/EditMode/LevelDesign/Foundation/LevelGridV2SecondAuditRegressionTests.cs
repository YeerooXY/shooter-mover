#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using ShooterMover.Application.Missions.Rooms.Content;
using ShooterMover.Content.Definitions.Missions.Rooms;
using ShooterMover.UnityAdapters.Authoring.LevelDesign;
using UnityEngine;

namespace ShooterMover.Tests.EditMode.LevelDesign.Foundation
{
    public sealed class LevelGridV2SecondAuditRegressionTests
    {
        [Test]
        public void BroadAuthoredDoorRule_ReplacesDefaultWithoutAmbiguity()
        {
            Dictionary<string, string> source = BuildValidSource();
            source["Rooms/Room_1_0_01/encounter.json"] = "{" +
                "\"schema_version\":2," +
                "\"room\":\"room.target\"," +
                "\"completion\":\"all-enemies\"," +
                "\"optional_enemy_ids\":[]," +
                "\"door_rules\":[{" +
                "\"match\":{\"exit_type\":\"return\"}," +
                "\"open_when\":\"room-entered\"}]}";

            LevelGridV2CompileResult compile = Compile(source);

            Assert.That(compile.IsValid, Is.True, FirstIssue(compile));
            RoomContentImportResultV1 imported = RoomContentJsonImporterV1.Import(
                compile.Package,
                BuiltInRoomContentObjectCatalogV1.Create());
            Assert.That(imported.IsValid, Is.True, ImportIssue(imported));
        }

        [Test]
        public void AuthoredDoorRuleThatMatchesNoRuntimeDoor_IsRejected()
        {
            Dictionary<string, string> source = BuildValidSource();
            source["Rooms/Room_0_0_01/encounter.json"] = "{" +
                "\"schema_version\":2," +
                "\"room\":\"room.start\"," +
                "\"completion\":\"all-enemies\"," +
                "\"optional_enemy_ids\":[]," +
                "\"door_rules\":[{" +
                "\"match\":{\"link_kind\":\"final-exit\"}," +
                "\"open_when\":\"always\"}]}";

            AssertIssue(source, "level-grid-v2-encounter-door-rule-unmatched");
        }

        [Test]
        public void PlayerStartOnNonStartRoom_IsRejected()
        {
            Dictionary<string, string> source = BuildValidSource();
            source["Rooms/Room_1_0_01/room.json"] =
                source["Rooms/Room_1_0_01/room.json"].Replace(
                    "\"runtime_bounds\"",
                    "\"player_start\":{\"position\":[0,0],\"rotation\":0},\"runtime_bounds\"");

            AssertIssue(source, "level-grid-v2-player-start-extra");
        }

        [Test]
        public void PlayerStartOutsideStartRoomBounds_IsRejected()
        {
            Dictionary<string, string> source = BuildValidSource();
            source["Rooms/Room_0_0_01/room.json"] =
                source["Rooms/Room_0_0_01/room.json"].Replace(
                    "\"position\":[-9,0]",
                    "\"position\":[100,0]");

            AssertIssue(source, "level-grid-v2-player-start-outside-bounds");
        }

        [Test]
        public void UnknownOptionalEnemyId_IsRejectedByV2Compiler()
        {
            Dictionary<string, string> source = BuildValidSource();
            source["Rooms/Room_1_0_01/encounter.json"] =
                source["Rooms/Room_1_0_01/encounter.json"].Replace(
                    "\"optional_enemy_ids\":[]",
                    "\"optional_enemy_ids\":[\"enemy.missing\"]");

            AssertIssue(source, "level-grid-v2-optional-enemy-unknown");
        }

        [Test]
        public void ExportDestinationGuard_RejectsDifferentLevelOwnership()
        {
            string root = Path.Combine(
                Path.GetTempPath(),
                "shooter-mover-grid-v2-destination-" + Guid.NewGuid().ToString("N"));
            try
            {
                Directory.CreateDirectory(root);
                File.WriteAllText(
                    Path.Combine(root, "level.json"),
                    "{\"schema_version\":2,\"level_id\":\"level.someone-else\"}");

                InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                    () => LevelGridV2RoomFolderMigration.ValidateDestinationRoot(
                        root,
                        "level.mine"));
                Assert.That(exception.Message, Does.Contain("level.someone-else"));
                Assert.That(exception.Message, Does.Contain("level.mine"));
            }
            finally
            {
                if (Directory.Exists(root)) Directory.Delete(root, true);
            }
        }

        [Test]
        public void RoomMigration_AllowsCoordinateVacatedByDeletedRoom()
        {
            string root = Path.Combine(
                Path.GetTempPath(),
                "shooter-mover-grid-v2-vacated-" + Guid.NewGuid().ToString("N"));
            string roomsRoot = Path.Combine(root, "Rooms");
            string activeOld = Path.Combine(roomsRoot, "Room_0_0_01");
            string deletedOld = Path.Combine(roomsRoot, "Room_1_0_01");
            GameObject roomObject = new GameObject("Surviving Room");
            try
            {
                Directory.CreateDirectory(activeOld);
                Directory.CreateDirectory(deletedOld);
                File.WriteAllText(
                    Path.Combine(activeOld, "room.json"),
                    "{\"room_id\":\"room.survivor\"}");
                File.WriteAllText(
                    Path.Combine(activeOld, "enemies.json"),
                    "survivor-content");
                File.WriteAllText(
                    Path.Combine(deletedOld, "room.json"),
                    "{\"room_id\":\"room.deleted\"}");
                File.WriteAllText(
                    Path.Combine(deletedOld, "enemies.json"),
                    "must-not-be-adopted");

                LevelRoomAuthoring2D room = roomObject.AddComponent<LevelRoomAuthoring2D>();
                room.ConfigureForTests(
                    "room.survivor",
                    new Vector2Int(1, 0),
                    Vector2.one,
                    Vector2Int.one,
                    null);

                IReadOnlyDictionary<string, string> migrated =
                    LevelGridV2RoomFolderMigration.Prepare(
                        new[] { room },
                        roomsRoot);
                string newFolder = migrated["room.survivor"];

                Assert.That(Path.GetFileName(newFolder), Is.EqualTo("Room_1_0_01"));
                Assert.That(
                    File.ReadAllText(Path.Combine(newFolder, "enemies.json")),
                    Is.EqualTo("survivor-content"));
                Assert.That(Directory.Exists(activeOld), Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(roomObject);
                if (Directory.Exists(root)) Directory.Delete(root, true);
            }
        }

        private static LevelGridV2CompileResult Compile(
            Dictionary<string, string> source)
        {
            return LevelGridV2Compiler.Compile(new LevelGridV2SourcePackage(source));
        }

        private static void AssertIssue(
            Dictionary<string, string> source,
            string expectedCode)
        {
            LevelGridV2CompileResult result = Compile(source);
            Assert.That(result.IsValid, Is.False);
            Assert.That(result.Issues, Is.Not.Empty);
            Assert.That(
                result.Issues[0].Code,
                Is.EqualTo(expectedCode),
                result.Issues[0].ToString());
        }

        private static string FirstIssue(LevelGridV2CompileResult result)
        {
            return result.Issues.Count == 0
                ? string.Empty
                : result.Issues[0].ToString();
        }

        private static string ImportIssue(RoomContentImportResultV1 result)
        {
            return result.Issues.Count == 0
                ? string.Empty
                : result.Issues[0].Code + " at " + result.Issues[0].Path
                    + ": " + result.Issues[0].Message;
        }

        private static Dictionary<string, string> BuildValidSource()
        {
            var source = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["level.json"] = "{" +
                    "\"schema_version\":2," +
                    "\"level_id\":\"level.second-audit\"," +
                    "\"start_room_id\":\"room.start\"," +
                    "\"final_exit\":{\"room_id\":\"room.target\",\"door_id\":\"door.target-exit\"}," +
                    "\"room_ids\":[\"room.start\",\"room.target\"]," +
                    "\"rooms\":[" +
                    "{\"room_id\":\"room.start\",\"grid_position\":[0,0],\"slot\":1,\"folder\":\"Room_0_0_01\"}," +
                    "{\"room_id\":\"room.target\",\"grid_position\":[1,0],\"slot\":1,\"folder\":\"Room_1_0_01\"}]}" ,
                ["map.json"] = "{" +
                    "\"schema_version\":2," +
                    "\"nodes\":[" +
                    "{\"room_id\":\"room.start\",\"grid_position\":[0,0],\"slot\":1}," +
                    "{\"room_id\":\"room.target\",\"grid_position\":[1,0],\"slot\":1}]," +
                    "\"connections\":[{" +
                    "\"connection_id\":\"connection.start-target\"," +
                    "\"from\":{\"room_id\":\"room.start\",\"door_id\":\"door.start-east\"}," +
                    "\"to\":{\"room_id\":\"room.target\",\"door_id\":\"door.target-west\"}," +
                    "\"travel_policy\":\"Bidirectional\"}]}" ,
            };

            AddRoom(
                source,
                "Room_0_0_01",
                "room.start",
                0,
                "\"player_start\":{\"position\":[-9,0],\"rotation\":0},",
                "[{\"door_id\":\"door.start-east\",\"side\":\"East\",\"current_local_position\":[11,0],\"traversable\":true,\"runtime_object\":\"door.room-standard\"}]",
                "[]");
            AddRoom(
                source,
                "Room_1_0_01",
                "room.target",
                1,
                string.Empty,
                "[{\"door_id\":\"door.target-west\",\"side\":\"West\",\"current_local_position\":[-11,0],\"traversable\":true,\"runtime_object\":\"door.room-standard\"}," +
                "{\"door_id\":\"door.target-exit\",\"side\":\"East\",\"current_local_position\":[11,0],\"traversable\":true,\"runtime_object\":\"door.room-standard\"}]",
                "[{\"id\":\"enemy.target\",\"object\":\"enemy.moving-droid\",\"level\":1,\"position\":[4,0],\"rotation\":180}]");
            return source;
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
                "\"schema_version\":2,\"room_id\":\"" + roomId
                + "\",\"doors\":" + doors + "}";
            source[root + "floor.json"] = "{" +
                "\"schema_version\":2,\"room\":\"" + roomId
                + "\",\"tiles\":[{\"object\":\"tile.floor-industrial\","
                + "\"fill\":{\"from\":[-12,-7],\"to\":[12,7]}}]}";
            source[root + "enemies.json"] = "{" +
                "\"schema_version\":2,\"room\":\"" + roomId
                + "\",\"enemies\":" + enemies + "}";
            source[root + "props.json"] = "{" +
                "\"schema_version\":2,\"room\":\"" + roomId
                + "\",\"props\":[]}";
            source[root + "decor.json"] = "{" +
                "\"schema_version\":2,\"room\":\"" + roomId
                + "\",\"background\":[],\"foreground\":[]}";
            source[root + "encounter.json"] = "{" +
                "\"schema_version\":2,\"room\":\"" + roomId
                + "\",\"completion\":\"all-enemies\","
                + "\"optional_enemy_ids\":[],\"door_rules\":[]}";
        }
    }
}
#endif
