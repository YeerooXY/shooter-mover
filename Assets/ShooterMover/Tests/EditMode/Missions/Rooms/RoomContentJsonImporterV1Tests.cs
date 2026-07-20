using System.Collections.Generic;
using NUnit.Framework;
using ShooterMover.Application.Missions.Rooms.Content;
using ShooterMover.Content.Definitions.Missions.Rooms;
using ShooterMover.Contracts.Missions.Rooms;
using ShooterMover.Domain.Common;

namespace ShooterMover.Tests.EditMode.Missions.Rooms
{
    public sealed class RoomContentJsonImporterV1Tests
    {
        [Test]
        public void AnonymousEnemies_PreserveTypeAndIndependentLevelFacts()
        {
            RoomContentImportResultV1 result = Import(
                EntryEnemies(
                    "{\"object\":\"enemy.moving-droid\",\"level\":1,"
                    + "\"position\":[4,3],\"rotation\":180},"
                    + "{\"object\":\"enemy.moving-droid\",\"level\":2,"
                    + "\"position\":[4,4],\"rotation\":180}"),
                EntryDecor(0, 0, 1, 1));

            Assert.That(result.IsValid, Is.True, FirstIssue(result));
            Assert.That(result.Bundle.Enemies.Count, Is.EqualTo(3));
            RoomEnemyPlacementContentV1 first = result.Bundle.Enemies[0];
            RoomEnemyPlacementContentV1 second = result.Bundle.Enemies[1];
            Assert.That(first.ObjectStableId, Is.EqualTo(StableId.Parse("enemy.moving-droid")));
            Assert.That(second.ObjectStableId, Is.EqualTo(first.ObjectStableId));
            Assert.That(first.Level, Is.EqualTo(1));
            Assert.That(second.Level, Is.EqualTo(2));
            Assert.That(first.AuthoredId, Is.Null);
            Assert.That(second.AuthoredId, Is.Null);
            Assert.That(first.InstanceStableId, Is.Not.EqualTo(second.InstanceStableId));

            AuthorableRoomDefinitionV1 room = result.Bundle.RuntimeDefinition.GetRoom(
                StableId.Parse("room.test-entry"));
            Assert.That(room.Placements.Count, Is.EqualTo(3));
            int runtimeDroids = 0;
            for (int index = 0; index < room.Placements.Count; index++)
            {
                RoomPlacedEntityDefinitionV1 placement = room.Placements[index];
                if (placement.PlacementKind == RoomLivePlacementKindV1.Enemy
                    && placement.DefinitionStableId
                        == StableId.Parse("enemy.mobile-blaster-droid"))
                {
                    runtimeDroids++;
                }
            }
            Assert.That(runtimeDroids, Is.EqualTo(2));
        }

        [Test]
        public void ReorderingDifferentAnonymousEnemies_PreservesGeneratedIdentityByFacts()
        {
            string levelOne = "{\"object\":\"enemy.moving-droid\",\"level\":1,"
                + "\"position\":[4,3],\"rotation\":180}";
            string levelTwo = "{\"object\":\"enemy.moving-droid\",\"level\":2,"
                + "\"position\":[4,4],\"rotation\":180}";
            RoomContentImportResultV1 first = Import(
                EntryEnemies(levelOne + "," + levelTwo),
                EntryDecor(0, 0, 0, 0));
            RoomContentImportResultV1 second = Import(
                EntryEnemies(levelTwo + "," + levelOne),
                EntryDecor(0, 0, 0, 0));

            Assert.That(first.IsValid, Is.True, FirstIssue(first));
            Assert.That(second.IsValid, Is.True, FirstIssue(second));
            Assert.That(
                FindEnemy(first.Bundle, 1).InstanceStableId,
                Is.EqualTo(FindEnemy(second.Bundle, 1).InstanceStableId));
            Assert.That(
                FindEnemy(first.Bundle, 2).InstanceStableId,
                Is.EqualTo(FindEnemy(second.Bundle, 2).InstanceStableId));
            Assert.That(first.Bundle.Fingerprint, Is.EqualTo(second.Bundle.Fingerprint));
        }

        [Test]
        public void TileFill_ExpandsInclusiveRectangleWithoutRepeatedCoordinates()
        {
            RoomContentImportResultV1 result = Import(
                EntryEnemies(
                    "{\"object\":\"enemy.moving-droid\",\"level\":1,"
                    + "\"position\":[4,3],\"rotation\":180}"),
                EntryDecor(0, 0, 2, 1));

            Assert.That(result.IsValid, Is.True, FirstIssue(result));
            int entryTileCount = 0;
            var positions = new HashSet<string>();
            for (int index = 0; index < result.Bundle.Visuals.Count; index++)
            {
                RoomVisualPlacementContentV1 visual = result.Bundle.Visuals[index];
                if (visual.RoomStableId != StableId.Parse("room.test-entry")) continue;
                entryTileCount++;
                positions.Add(visual.LocalPosition.X + "," + visual.LocalPosition.Y);
            }

            Assert.That(entryTileCount, Is.EqualTo(6));
            Assert.That(positions.Count, Is.EqualTo(6));
        }

        [Test]
        public void EncounterOwnsOptionalRoleRatherThanEnemyPlacement()
        {
            string enemies = "{\"room\":\"room.test-entry\",\"enemies\":["
                + "{\"id\":\"bonus-droid\",\"object\":\"enemy.moving-droid\","
                + "\"level\":4,\"position\":[4,3],\"rotation\":180}]}";
            RoomContentImportResultV1 result = Import(
                enemies,
                EntryDecor(0, 0, 0, 0),
                "{\"room\":\"room.test-entry\",\"completion\":\"all-enemies\","
                + "\"optional_enemy_ids\":[\"bonus-droid\"],\"door_rules\":["
                + "{\"match\":{\"exit_type\":\"progression\"},"
                + "\"open_when\":\"room-complete\"}]}" );

            Assert.That(result.IsValid, Is.True, FirstIssue(result));
            RoomEnemyPlacementContentV1 enemy = result.Bundle.Enemies[0];
            AuthorableRoomDefinitionV1 room = result.Bundle.RuntimeDefinition.GetRoom(
                StableId.Parse("room.test-entry"));
            RoomPlacedEntityDefinitionV1 runtimePlacement;
            Assert.That(
                room.TryGetPlacement(enemy.InstanceStableId, out runtimePlacement),
                Is.True);
            Assert.That(
                runtimePlacement.ClearRole,
                Is.EqualTo(RoomOccupantClearRoleV1.OptionalEnemy));
            Assert.That(enemy.Level, Is.EqualTo(4));
            Assert.That(enemy.AuthoredId, Is.EqualTo("bonus-droid"));
        }

        [Test]
        public void RoomBoundsDoorsAndSpawnKindTargets_CompileIntoRuntimeGraph()
        {
            RoomContentImportResultV1 result = Import(
                EntryEnemies(
                    "{\"object\":\"enemy.moving-droid\",\"level\":1,"
                    + "\"position\":[4,3],\"rotation\":180}"),
                EntryDecor(0, 0, 0, 0));

            Assert.That(result.IsValid, Is.True, FirstIssue(result));
            AuthorableRoomDefinitionV1 entry = result.Bundle.RuntimeDefinition.GetRoom(
                StableId.Parse("room.test-entry"));
            Assert.That(entry.Bounds.Size.X, Is.EqualTo(20d));
            Assert.That(entry.Bounds.Size.Y, Is.EqualTo(12d));
            Assert.That(entry.Doors.Count, Is.EqualTo(1));
            Assert.That(entry.Exits.Count, Is.EqualTo(1));
            Assert.That(
                entry.Exits[0].TargetRoomStableId,
                Is.EqualTo(StableId.Parse("room.test-terminal")));
            AuthorableRoomDefinitionV1 terminal = result.Bundle.RuntimeDefinition.GetRoom(
                StableId.Parse("room.test-terminal"));
            Assert.That(
                terminal.HasSpawnPoint(entry.Exits[0].TargetSpawnPointStableId),
                Is.True);
        }

        [Test]
        public void UnknownRoomObject_RejectsWithoutPartialBundle()
        {
            RoomContentImportResultV1 result = Import(
                EntryEnemies(
                    "{\"object\":\"enemy.not-registered\",\"level\":1,"
                    + "\"position\":[4,3],\"rotation\":180}"),
                EntryDecor(0, 0, 0, 0));

            Assert.That(result.IsValid, Is.False);
            Assert.That(result.Bundle, Is.Null);
            Assert.That(result.Issues, Has.Count.EqualTo(1));
            Assert.That(result.Issues[0].Code, Is.EqualTo("room-content-object-unknown"));
        }

        private static RoomEnemyPlacementContentV1 FindEnemy(
            RoomContentBundleV1 bundle,
            int level)
        {
            for (int index = 0; index < bundle.Enemies.Count; index++)
            {
                RoomEnemyPlacementContentV1 enemy = bundle.Enemies[index];
                if (enemy.RoomStableId == StableId.Parse("room.test-entry")
                    && enemy.Level == level)
                {
                    return enemy;
                }
            }
            Assert.Fail("Enemy level not found: " + level);
            return null;
        }

        private static string FirstIssue(RoomContentImportResultV1 result)
        {
            return result.Issues.Count == 0
                ? string.Empty
                : result.Issues[0].Code
                    + ":"
                    + result.Issues[0].Path
                    + ":"
                    + result.Issues[0].Message;
        }

        private static string EntryEnemies(string entries)
        {
            if (entries.StartsWith("{\"room\""))
            {
                return entries;
            }
            return "{\"room\":\"room.test-entry\",\"enemies\":["
                + entries
                + "]}";
        }

        private static string EntryDecor(int fromX, int fromY, int toX, int toY)
        {
            return "{\"room\":\"room.test-entry\",\"tiles\":[{"
                + "\"object\":\"tile.floor-industrial\",\"fill\":{"
                + "\"from\":["
                + fromX
                + ","
                + fromY
                + "],\"to\":["
                + toX
                + ","
                + toY
                + "]}}],\"background\":[],\"foreground\":[]}";
        }

        private static RoomContentImportResultV1 Import(
            string entryEnemies,
            string entryDecor,
            string entryEncounter = null)
        {
            string manifest = "{\"version\":1,\"layout\":\"layout.test-json-room\","
                + "\"start_room\":\"room.test-entry\","
                + "\"terminal_room\":\"room.test-terminal\",\"rooms\":["
                + "{\"layout\":\"entry.layout\",\"enemies\":\"entry.enemies\","
                + "\"props\":\"entry.props\",\"decor\":\"entry.decor\","
                + "\"encounter\":\"entry.encounter\"},"
                + "{\"layout\":\"terminal.layout\","
                + "\"enemies\":\"terminal.enemies\",\"props\":\"terminal.props\","
                + "\"decor\":\"terminal.decor\","
                + "\"encounter\":\"terminal.encounter\"}]}";
            var documents = new Dictionary<string, string>
            {
                {
                    "entry.layout",
                    "{\"room\":\"room.test-entry\",\"order\":0,"
                    + "\"display_name\":\"ENTRY\",\"bounds\":{"
                    + "\"center\":[0,0],\"size\":[20,12]},\"spawns\":[{"
                    + "\"kind\":\"forward-entry\",\"position\":[-8,0],"
                    + "\"rotation\":0}],\"doors\":[{"
                    + "\"object\":\"door.room-standard\",\"position\":[9,0],"
                    + "\"rotation\":0,\"link\":{\"kind\":\"room\","
                    + "\"exit_type\":\"progression\","
                    + "\"target_room\":\"room.test-terminal\","
                    + "\"target_spawn_kind\":\"forward-entry\"}}]}"
                },
                { "entry.enemies", entryEnemies },
                {
                    "entry.props",
                    "{\"room\":\"room.test-entry\",\"props\":[{"
                    + "\"object\":\"prop.level1-cover\","
                    + "\"position\":[0,-3],\"rotation\":0}]}"
                },
                { "entry.decor", entryDecor },
                {
                    "entry.encounter",
                    entryEncounter
                    ?? "{\"room\":\"room.test-entry\","
                        + "\"completion\":\"all-enemies\","
                        + "\"optional_enemy_ids\":[],\"door_rules\":[{"
                        + "\"match\":{\"exit_type\":\"progression\"},"
                        + "\"open_when\":\"room-complete\"}]}"
                },
                {
                    "terminal.layout",
                    "{\"room\":\"room.test-terminal\",\"order\":1,"
                    + "\"display_name\":\"TERMINAL\",\"bounds\":{"
                    + "\"center\":[0,0],\"size\":[20,12]},\"spawns\":[{"
                    + "\"kind\":\"forward-entry\",\"position\":[-8,0],"
                    + "\"rotation\":0}],\"doors\":[{"
                    + "\"object\":\"door.room-standard\",\"position\":[9,0],"
                    + "\"rotation\":0,\"link\":{\"kind\":\"final-exit\","
                    + "\"exit_type\":\"progression\"}}]}"
                },
                {
                    "terminal.enemies",
                    "{\"room\":\"room.test-terminal\",\"enemies\":[{"
                    + "\"object\":\"enemy.blaster-turret\",\"level\":1,"
                    + "\"position\":[4,0],\"rotation\":180}]}"
                },
                { "terminal.props", "{\"room\":\"room.test-terminal\",\"props\":[]}" },
                {
                    "terminal.decor",
                    "{\"room\":\"room.test-terminal\",\"tiles\":[],"
                    + "\"background\":[],\"foreground\":[]}"
                },
                {
                    "terminal.encounter",
                    "{\"room\":\"room.test-terminal\","
                    + "\"completion\":\"all-enemies\","
                    + "\"optional_enemy_ids\":[],\"door_rules\":[{"
                    + "\"match\":{\"link_kind\":\"final-exit\"},"
                    + "\"open_when\":\"room-complete\"}]}"
                },
            };

            return RoomContentJsonImporterV1.Import(
                new RoomContentJsonPackageV1(manifest, documents),
                BuiltInRoomContentObjectCatalogV1.Create());
        }
    }
}
