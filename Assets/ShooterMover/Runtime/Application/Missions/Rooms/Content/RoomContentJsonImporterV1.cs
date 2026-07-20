using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Security.Cryptography;
using System.Text;
using ShooterMover.Contracts.Missions.Rooms;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Missions.Rooms;

namespace ShooterMover.Application.Missions.Rooms.Content
{
    public static partial class RoomContentJsonImporterV1
    {
        public const int CurrentVersion = 1;
        private const int MaximumExpandedTiles = 10000;

        public static RoomContentImportResultV1 Import(
            RoomContentJsonPackageV1 package,
            IRoomContentObjectCatalogV1 objectCatalog)
        {
            if (package == null)
            {
                return Failure(
                    "room-content-package-missing",
                    "$",
                    "A room-content JSON package is required.");
            }
            if (objectCatalog == null)
            {
                return Failure(
                    "room-content-object-catalog-missing",
                    "$",
                    "A room-content object catalog is required.");
            }

            try
            {
                ManifestDto manifest = Deserialize<ManifestDto>(
                    package.ManifestJson,
                    "$.manifest");
                if (manifest.Version != CurrentVersion)
                {
                    throw Mapping(
                        "room-content-version-unsupported",
                        "$.manifest.version",
                        "Expected room-content version "
                        + CurrentVersion
                        + " but received "
                        + manifest.Version
                        + ".");
                }

                StableId layoutStableId = ParseStableId(
                    manifest.Layout,
                    "$.manifest.layout");
                StableId startRoomStableId = ParseStableId(
                    manifest.StartRoom,
                    "$.manifest.start_room");
                StableId terminalRoomStableId = ParseStableId(
                    manifest.TerminalRoom,
                    "$.manifest.terminal_room");
                List<RoomDocumentsDto> roomDocuments = RequireList(
                    manifest.Rooms,
                    "$.manifest.rooms");
                if (roomDocuments.Count == 0)
                {
                    throw Mapping(
                        "room-content-room-list-empty",
                        "$.manifest.rooms",
                        "A room-content manifest requires at least one room.");
                }

                var sources = new List<RoomSource>();
                var roomsById = new Dictionary<StableId, RoomSource>();
                for (int index = 0; index < roomDocuments.Count; index++)
                {
                    string path = "$.manifest.rooms[" + index + "]";
                    RoomDocumentsDto reference = Require(
                        roomDocuments[index],
                        path);
                    RoomSource source = LoadRoomSource(
                        package,
                        reference,
                        path);
                    if (roomsById.ContainsKey(source.RoomStableId))
                    {
                        throw Mapping(
                            "room-content-room-duplicate",
                            path,
                            "Room identity appears more than once: "
                            + source.RoomStableId);
                    }
                    roomsById.Add(source.RoomStableId, source);
                    sources.Add(source);
                }

                if (!roomsById.ContainsKey(startRoomStableId))
                {
                    throw Mapping(
                        "room-content-start-room-unknown",
                        "$.manifest.start_room",
                        "Unknown start room: " + startRoomStableId);
                }
                if (!roomsById.ContainsKey(terminalRoomStableId))
                {
                    throw Mapping(
                        "room-content-terminal-room-unknown",
                        "$.manifest.terminal_room",
                        "Unknown terminal room: " + terminalRoomStableId);
                }

                var compiler = new Compiler(
                    layoutStableId,
                    startRoomStableId,
                    terminalRoomStableId,
                    sources,
                    roomsById,
                    objectCatalog);
                return new RoomContentImportResultV1(compiler.Compile(), null);
            }
            catch (RoomContentMappingException exception)
            {
                return Failure(exception.Code, exception.Path, exception.Message);
            }
            catch (Exception exception)
            {
                if (exception is OutOfMemoryException
                    || exception is StackOverflowException
                    || exception is AccessViolationException)
                {
                    throw;
                }

                return Failure(
                    "room-content-invalid",
                    "$",
                    exception.GetType().Name + ": " + exception.Message);
            }
        }

        private static RoomSource LoadRoomSource(
            RoomContentJsonPackageV1 package,
            RoomDocumentsDto reference,
            string path)
        {
            string layoutKey = RequireText(reference.Layout, path + ".layout");
            string enemiesKey = RequireText(reference.Enemies, path + ".enemies");
            string propsKey = RequireText(reference.Props, path + ".props");
            string decorKey = RequireText(reference.Decor, path + ".decor");
            string encounterKey = RequireText(reference.Encounter, path + ".encounter");

            RoomLayoutDto layout = Deserialize<RoomLayoutDto>(
                RequireDocument(package, layoutKey, path + ".layout"),
                "$documents[\"" + layoutKey + "\"]");
            EnemiesDto enemies = Deserialize<EnemiesDto>(
                RequireDocument(package, enemiesKey, path + ".enemies"),
                "$documents[\"" + enemiesKey + "\"]");
            PropsDto props = Deserialize<PropsDto>(
                RequireDocument(package, propsKey, path + ".props"),
                "$documents[\"" + propsKey + "\"]");
            DecorDto decor = Deserialize<DecorDto>(
                RequireDocument(package, decorKey, path + ".decor"),
                "$documents[\"" + decorKey + "\"]");
            EncounterDto encounter = Deserialize<EncounterDto>(
                RequireDocument(package, encounterKey, path + ".encounter"),
                "$documents[\"" + encounterKey + "\"]");

            StableId roomStableId = ParseStableId(
                layout.Room,
                "$documents[\"" + layoutKey + "\"].room");
            RequireMatchingRoom(
                roomStableId,
                enemies.Room,
                "$documents[\"" + enemiesKey + "\"].room");
            RequireMatchingRoom(
                roomStableId,
                props.Room,
                "$documents[\"" + propsKey + "\"].room");
            RequireMatchingRoom(
                roomStableId,
                decor.Room,
                "$documents[\"" + decorKey + "\"].room");
            RequireMatchingRoom(
                roomStableId,
                encounter.Room,
                "$documents[\"" + encounterKey + "\"].room");

            return new RoomSource(
                roomStableId,
                layout,
                enemies,
                props,
                decor,
                encounter,
                layoutKey,
                enemiesKey,
                propsKey,
                decorKey,
                encounterKey);
        }

        private static void RequireMatchingRoom(
            StableId expected,
            string value,
            string path)
        {
            StableId actual = ParseStableId(value, path);
            if (actual != expected)
            {
                throw Mapping(
                    "room-content-document-room-mismatch",
                    path,
                    "Expected " + expected + " but received " + actual + ".");
            }
        }

        private static string RequireDocument(
            RoomContentJsonPackageV1 package,
            string key,
            string path)
        {
            string json;
            if (!package.TryGetDocument(key, out json))
            {
                throw Mapping(
                    "room-content-document-missing",
                    path,
                    "The manifest references a missing document: " + key);
            }
            return json;
        }

        private static T Deserialize<T>(string json, string path)
            where T : class
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                throw Mapping(
                    "room-content-json-empty",
                    path,
                    "JSON content is required.");
            }

            try
            {
                var serializer = new DataContractJsonSerializer(
                    typeof(T),
                    new DataContractJsonSerializerSettings
                    {
                        UseSimpleDictionaryFormat = true,
                    });
                using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(json)))
                {
                    T value = serializer.ReadObject(stream) as T;
                    if (value == null)
                    {
                        throw Mapping(
                            "room-content-json-root-invalid",
                            path,
                            "JSON root must be an object.");
                    }
                    return value;
                }
            }
            catch (RoomContentMappingException)
            {
                throw;
            }
            catch (Exception exception)
            {
                if (!(exception is SerializationException)
                    && !(exception is FormatException)
                    && !(exception is InvalidDataContractException))
                {
                    throw;
                }

                throw Mapping(
                    "room-content-json-invalid",
                    path,
                    "Malformed room-content JSON: " + exception.Message);
            }
        }

        private static StableId ParseStableId(string value, string path)
        {
            try
            {
                return StableId.Parse(RequireText(value, path));
            }
            catch (Exception exception)
            {
                throw Mapping(
                    "room-content-stable-id-invalid",
                    path,
                    exception.Message);
            }
        }

        private static string RequireText(string value, string path)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw Mapping(
                    "room-content-value-required",
                    path,
                    "A non-empty value is required.");
            }
            return value.Trim();
        }

        private static T Require<T>(T value, string path)
            where T : class
        {
            if (value == null)
            {
                throw Mapping(
                    "room-content-value-required",
                    path,
                    "A value is required.");
            }
            return value;
        }

        private static List<T> RequireList<T>(List<T> values, string path)
        {
            if (values == null)
            {
                throw Mapping(
                    "room-content-array-required",
                    path,
                    "An array is required. Use an empty array when no entries exist.");
            }
            return values;
        }

        private static RoomVector2V1 ParseVector(double[] values, string path)
        {
            if (values == null || values.Length != 2)
            {
                throw Mapping(
                    "room-content-vector-invalid",
                    path,
                    "A vector must contain exactly two numeric values.");
            }
            return new RoomVector2V1(values[0], values[1]);
        }

        private static int[] ParseGridVector(int[] values, string path)
        {
            if (values == null || values.Length != 2)
            {
                throw Mapping(
                    "room-content-grid-vector-invalid",
                    path,
                    "A grid vector must contain exactly two integer values.");
            }
            return values;
        }

        private static RoomContentImportResultV1 Failure(
            string code,
            string path,
            string message)
        {
            return new RoomContentImportResultV1(
                null,
                new[] { new RoomContentImportIssueV1(code, path, message) });
        }

        private static RoomContentMappingException Mapping(
            string code,
            string path,
            string message)
        {
            return new RoomContentMappingException(code, path, message);
        }

        private sealed class Compiler
        {
            private readonly StableId layoutStableId;
            private readonly StableId startRoomStableId;
            private readonly StableId terminalRoomStableId;
            private readonly List<RoomSource> sources;
            private readonly Dictionary<StableId, RoomSource> roomsById;
            private readonly IRoomContentObjectCatalogV1 objectCatalog;
            private readonly GeneratedIdentityFactory identities =
                new GeneratedIdentityFactory();
            private readonly List<RoomEnemyPlacementContentV1> enemies =
                new List<RoomEnemyPlacementContentV1>();
            private readonly List<RoomPropPlacementContentV1> props =
                new List<RoomPropPlacementContentV1>();
            private readonly List<RoomVisualPlacementContentV1> visuals =
                new List<RoomVisualPlacementContentV1>();

            public Compiler(
                StableId layoutStableId,
                StableId startRoomStableId,
                StableId terminalRoomStableId,
                List<RoomSource> sources,
                Dictionary<StableId, RoomSource> roomsById,
                IRoomContentObjectCatalogV1 objectCatalog)
            {
                this.layoutStableId = layoutStableId;
                this.startRoomStableId = startRoomStableId;
                this.terminalRoomStableId = terminalRoomStableId;
                this.sources = sources;
                this.roomsById = roomsById;
                this.objectCatalog = objectCatalog;
            }

            public RoomContentBundleV1 Compile()
            {
                PrepareSpawns();
                var rooms = new List<AuthorableRoomDefinitionV1>();
                for (int index = 0; index < sources.Count; index++)
                {
                    rooms.Add(CompileRoom(sources[index]));
                }

                var graph = new AuthorableRoomGraphDefinitionV1(
                    layoutStableId,
                    startRoomStableId,
                    terminalRoomStableId,
                    rooms);
                return new RoomContentBundleV1(graph, enemies, props, visuals);
            }

            private void PrepareSpawns()
            {
                for (int sourceIndex = 0; sourceIndex < sources.Count; sourceIndex++)
                {
                    RoomSource source = sources[sourceIndex];
                    List<SpawnDto> authored = RequireList(
                        source.Layout.Spawns,
                        "$documents[\"" + source.LayoutKey + "\"].spawns");
                    if (authored.Count == 0)
                    {
                        throw Mapping(
                            "room-content-spawn-list-empty",
                            "$documents[\"" + source.LayoutKey + "\"].spawns",
                            "Every room requires at least one spawn point.");
                    }

                    for (int index = 0; index < authored.Count; index++)
                    {
                        string path = "$documents[\""
                            + source.LayoutKey
                            + "\"].spawns["
                            + index
                            + "]";
                        SpawnDto dto = Require(authored[index], path);
                        RoomSpawnPointKindV1 kind = ParseSpawnKind(
                            dto.Kind,
                            path + ".kind");
                        RoomVector2V1 position = ParseVector(
                            dto.Position,
                            path + ".position");
                        string signature = kind
                            + "|"
                            + Number(position.X)
                            + "|"
                            + Number(position.Y)
                            + "|"
                            + Number(dto.Rotation);
                        StableId id = identities.Create(
                            "entry",
                            source.RoomStableId,
                            "spawn",
                            dto.Id,
                            signature,
                            path);
                        var definition = new RoomSpawnPointDefinitionV1(
                            id,
                            kind,
                            position,
                            dto.Rotation);
                        source.AddSpawn(dto.Id, kind, definition, path);
                    }
                }
            }

            private AuthorableRoomDefinitionV1 CompileRoom(RoomSource source)
            {
                if (source.Layout.Order < 0)
                {
                    throw Mapping(
                        "room-content-room-order-invalid",
                        "$documents[\"" + source.LayoutKey + "\"].order",
                        "Room order cannot be negative.");
                }

                BoundsDto bounds = Require(
                    source.Layout.Bounds,
                    "$documents[\"" + source.LayoutKey + "\"].bounds");
                RoomBoundsV1 roomBounds = new RoomBoundsV1(
                    ParseVector(
                        bounds.Center,
                        "$documents[\"" + source.LayoutKey + "\"].bounds.center"),
                    ParseVector(
                        bounds.Size,
                        "$documents[\"" + source.LayoutKey + "\"].bounds.size"));

                var placements = new List<RoomPlacedEntityDefinitionV1>();
                CompileEnemies(source, placements);
                CompileProps(source, placements);
                CompileDecor(source);

                var conditions = new Dictionary<string, RoomCompletionConditionDefinitionV1>(
                    StringComparer.Ordinal);
                RoomCompletionConditionDefinitionV1 completion =
                    CreateCompletionCondition(source);
                conditions.Add("room-complete", completion);

                var doors = new List<RoomDoorDefinitionV1>();
                var exits = new List<RoomExitLinkDefinitionV1>();
                CompileDoors(source, conditions, doors, exits);

                return new AuthorableRoomDefinitionV1(
                    source.RoomStableId,
                    source.Layout.Order,
                    RequireText(
                        source.Layout.DisplayName,
                        "$documents[\"" + source.LayoutKey + "\"].display_name"),
                    roomBounds,
                    source.SpawnDefinitions,
                    placements,
                    doors,
                    exits,
                    conditions.Values);
            }

            private void CompileEnemies(
                RoomSource source,
                List<RoomPlacedEntityDefinitionV1> placements)
            {
                List<EnemyDto> authored = RequireList(
                    source.Enemies.Enemies,
                    "$documents[\"" + source.EnemiesKey + "\"].enemies");
                HashSet<string> optionalIds = BuildOptionalEnemyIds(source);
                var seenAuthoredIds = new HashSet<string>(StringComparer.Ordinal);

                for (int index = 0; index < authored.Count; index++)
                {
                    string path = "$documents[\""
                        + source.EnemiesKey
                        + "\"].enemies["
                        + index
                        + "]";
                    EnemyDto dto = Require(authored[index], path);
                    string authoredId = NormalizeOptionalId(dto.Id);
                    if (authoredId != null && !seenAuthoredIds.Add(authoredId))
                    {
                        throw Mapping(
                            "room-content-authored-id-duplicate",
                            path + ".id",
                            "Enemy authored ID is duplicated: " + authoredId);
                    }
                    if (dto.Level <= 0)
                    {
                        throw Mapping(
                            "room-content-enemy-level-invalid",
                            path + ".level",
                            "Enemy level must be greater than zero.");
                    }

                    RoomContentObjectDefinitionV1 objectDefinition = ResolveObject(
                        dto.Object,
                        RoomContentObjectKindV1.Enemy,
                        path + ".object");
                    RoomVector2V1 position = ParseVector(
                        dto.Position,
                        path + ".position");
                    string signature = objectDefinition.ObjectStableId
                        + "|"
                        + dto.Level
                        + "|"
                        + Number(position.X)
                        + "|"
                        + Number(position.Y)
                        + "|"
                        + Number(dto.Rotation);
                    StableId instanceId = identities.Create(
                        "enemy-instance",
                        source.RoomStableId,
                        "enemy",
                        authoredId,
                        signature,
                        path);
                    RoomOccupantClearRoleV1 role = authoredId != null
                        && optionalIds.Contains(authoredId)
                        ? RoomOccupantClearRoleV1.OptionalEnemy
                        : RoomOccupantClearRoleV1.RequiredEnemy;
                    placements.Add(new RoomPlacedEntityDefinitionV1(
                        instanceId,
                        RoomLivePlacementKindV1.Enemy,
                        objectDefinition.RuntimeDefinitionStableId,
                        objectDefinition.PresentationStableId,
                        role,
                        position,
                        dto.Rotation));
                    enemies.Add(new RoomEnemyPlacementContentV1(
                        instanceId,
                        source.RoomStableId,
                        objectDefinition.ObjectStableId,
                        dto.Level,
                        position,
                        dto.Rotation,
                        authoredId));
                }

                foreach (string optionalId in optionalIds)
                {
                    if (!seenAuthoredIds.Contains(optionalId))
                    {
                        throw Mapping(
                            "room-content-optional-enemy-unknown",
                            "$documents[\""
                            + source.EncounterKey
                            + "\"].optional_enemy_ids",
                            "Optional enemy ID does not exist in the enemy placement document: "
                            + optionalId);
                    }
                }
            }

            private HashSet<string> BuildOptionalEnemyIds(RoomSource source)
            {
                List<string> values = RequireList(
                    source.Encounter.OptionalEnemyIds,
                    "$documents[\""
                    + source.EncounterKey
                    + "\"].optional_enemy_ids");
                var result = new HashSet<string>(StringComparer.Ordinal);
                for (int index = 0; index < values.Count; index++)
                {
                    string id = RequireText(
                        values[index],
                        "$documents[\""
                        + source.EncounterKey
                        + "\"].optional_enemy_ids["
                        + index
                        + "]");
                    if (!result.Add(id))
                    {
                        throw Mapping(
                            "room-content-optional-enemy-duplicate",
                            "$documents[\""
                            + source.EncounterKey
                            + "\"].optional_enemy_ids["
                            + index
                            + "]",
                            "Optional enemy ID is duplicated: " + id);
                    }
                }
                return result;
            }

            private void CompileProps(
                RoomSource source,
                List<RoomPlacedEntityDefinitionV1> placements)
            {
                List<PropDto> authored = RequireList(
                    source.Props.Props,
                    "$documents[\"" + source.PropsKey + "\"].props");
                var seenAuthoredIds = new HashSet<string>(StringComparer.Ordinal);
                for (int index = 0; index < authored.Count; index++)
                {
                    string path = "$documents[\""
                        + source.PropsKey
                        + "\"].props["
                        + index
                        + "]";
                    PropDto dto = Require(authored[index], path);
                    string authoredId = NormalizeOptionalId(dto.Id);
                    if (authoredId != null && !seenAuthoredIds.Add(authoredId))
                    {
                        throw Mapping(
                            "room-content-authored-id-duplicate",
                            path + ".id",
                            "Prop authored ID is duplicated: " + authoredId);
                    }

                    RoomContentObjectDefinitionV1 objectDefinition = ResolveObject(
                        dto.Object,
                        RoomContentObjectKindV1.Prop,
                        path + ".object");
                    RoomVector2V1 position = ParseVector(
                        dto.Position,
                        path + ".position");
                    string signature = objectDefinition.ObjectStableId
                        + "|"
                        + Number(position.X)
                        + "|"
                        + Number(position.Y)
                        + "|"
                        + Number(dto.Rotation);
                    StableId instanceId = identities.Create(
                        "prop-instance",
                        source.RoomStableId,
                        "prop",
                        authoredId,
                        signature,
                        path);
                    placements.Add(new RoomPlacedEntityDefinitionV1(
                        instanceId,
                        RoomLivePlacementKindV1.Prop,
                        objectDefinition.RuntimeDefinitionStableId,
                        objectDefinition.PresentationStableId,
                        RoomOccupantClearRoleV1.NonParticipant,
                        position,
                        dto.Rotation));
                    props.Add(new RoomPropPlacementContentV1(
                        instanceId,
                        source.RoomStableId,
                        objectDefinition.ObjectStableId,
                        position,
                        dto.Rotation,
                        authoredId));
                }
            }

            private void CompileDecor(RoomSource source)
            {
                List<TileDto> tiles = RequireList(
                    source.Decor.Tiles,
                    "$documents[\"" + source.DecorKey + "\"].tiles");
                int expandedTiles = 0;
                for (int index = 0; index < tiles.Count; index++)
                {
                    string path = "$documents[\""
                        + source.DecorKey
                        + "\"].tiles["
                        + index
                        + "]";
                    TileDto dto = Require(tiles[index], path);
                    RoomContentObjectDefinitionV1 objectDefinition = ResolveObject(
                        dto.Object,
                        RoomContentObjectKindV1.Tile,
                        path + ".object");
                    FillDto fill = Require(dto.Fill, path + ".fill");
                    int[] from = ParseGridVector(fill.From, path + ".fill.from");
                    int[] to = ParseGridVector(fill.To, path + ".fill.to");
                    int minX = Math.Min(from[0], to[0]);
                    int maxX = Math.Max(from[0], to[0]);
                    int minY = Math.Min(from[1], to[1]);
                    int maxY = Math.Max(from[1], to[1]);
                    long count = ((long)maxX - minX + 1L)
                        * ((long)maxY - minY + 1L);
                    if (count <= 0L
                        || count > MaximumExpandedTiles
                        || expandedTiles + count > MaximumExpandedTiles)
                    {
                        throw Mapping(
                            "room-content-tile-fill-too-large",
                            path + ".fill",
                            "Expanded tile placement count exceeds "
                            + MaximumExpandedTiles
                            + ".");
                    }

                    for (int y = minY; y <= maxY; y++)
                    {
                        for (int x = minX; x <= maxX; x++)
                        {
                            RoomVector2V1 position = new RoomVector2V1(x, y);
                            string signature = objectDefinition.ObjectStableId
                                + "|"
                                + x
                                + "|"
                                + y;
                            StableId instanceId = identities.Create(
                                "visual-instance",
                                source.RoomStableId,
                                "tile",
                                null,
                                signature,
                                path);
                            visuals.Add(new RoomVisualPlacementContentV1(
                                instanceId,
                                source.RoomStableId,
                                objectDefinition.ObjectStableId,
                                RoomContentVisualLayerV1.Tile,
                                position,
                                0d));
                        }
                    }
                    expandedTiles += (int)count;
                }

                CompileVisualList(
                    source,
                    source.Decor.Background,
                    RoomContentObjectKindV1.Background,
                    RoomContentVisualLayerV1.Background,
                    "background");
                CompileVisualList(
                    source,
                    source.Decor.Foreground,
                    RoomContentObjectKindV1.Foreground,
                    RoomContentVisualLayerV1.Foreground,
                    "foreground");
            }

            private void CompileVisualList(
                RoomSource source,
                List<VisualDto> authored,
                RoomContentObjectKindV1 objectKind,
                RoomContentVisualLayerV1 layer,
                string section)
            {
                authored = RequireList(
                    authored,
                    "$documents[\""
                    + source.DecorKey
                    + "\"]."
                    + section);
                for (int index = 0; index < authored.Count; index++)
                {
                    string path = "$documents[\""
                        + source.DecorKey
                        + "\"]."
                        + section
                        + "["
                        + index
                        + "]";
                    VisualDto dto = Require(authored[index], path);
                    RoomContentObjectDefinitionV1 objectDefinition = ResolveObject(
                        dto.Object,
                        objectKind,
                        path + ".object");
                    RoomVector2V1 position = ParseVector(
                        dto.Position,
                        path + ".position");
                    string signature = objectDefinition.ObjectStableId
                        + "|"
                        + Number(position.X)
                        + "|"
                        + Number(position.Y)
                        + "|"
                        + Number(dto.Rotation);
                    StableId instanceId = identities.Create(
                        "visual-instance",
                        source.RoomStableId,
                        section,
                        null,
                        signature,
                        path);
                    visuals.Add(new RoomVisualPlacementContentV1(
                        instanceId,
                        source.RoomStableId,
                        objectDefinition.ObjectStableId,
                        layer,
                        position,
                        dto.Rotation));
                }
            }

            private RoomCompletionConditionDefinitionV1 CreateCompletionCondition(
                RoomSource source)
            {
                string completion = RequireText(
                    source.Encounter.Completion,
                    "$documents[\""
                    + source.EncounterKey
                    + "\"].completion");
                RoomCompletionConditionKindV1 kind;
                if (string.Equals(completion, "all-enemies", StringComparison.Ordinal))
                {
                    kind = RoomCompletionConditionKindV1.AllBlockingOccupantsTerminal;
                }
                else if (string.Equals(completion, "always", StringComparison.Ordinal))
                {
                    kind = RoomCompletionConditionKindV1.AlwaysSatisfied;
                }
                else
                {
                    throw Mapping(
                        "room-content-completion-unsupported",
                        "$documents[\""
                        + source.EncounterKey
                        + "\"].completion",
                        "Supported completion values are all-enemies and always.");
                }

                StableId id = identities.Create(
                    "completion",
                    source.RoomStableId,
                    "condition",
                    "room-complete",
                    completion,
                    "$documents[\""
                    + source.EncounterKey
                    + "\"].completion");
                return new RoomCompletionConditionDefinitionV1(
                    id,
                    kind,
                    null,
                    true);
            }

            private void CompileDoors(
                RoomSource source,
                Dictionary<string, RoomCompletionConditionDefinitionV1> conditions,
                List<RoomDoorDefinitionV1> doors,
                List<RoomExitLinkDefinitionV1> exits)
            {
                List<DoorDto> authored = RequireList(
                    source.Layout.Doors,
                    "$documents[\"" + source.LayoutKey + "\"].doors");
                List<DoorRuleDto> rules = RequireList(
                    source.Encounter.DoorRules,
                    "$documents[\""
                    + source.EncounterKey
                    + "\"].door_rules");
                var seenAuthoredIds = new HashSet<string>(StringComparer.Ordinal);

                for (int index = 0; index < authored.Count; index++)
                {
                    string path = "$documents[\""
                        + source.LayoutKey
                        + "\"].doors["
                        + index
                        + "]";
                    DoorDto dto = Require(authored[index], path);
                    DoorLinkDto link = Require(dto.Link, path + ".link");
                    string authoredId = NormalizeOptionalId(dto.Id);
                    if (authoredId != null && !seenAuthoredIds.Add(authoredId))
                    {
                        throw Mapping(
                            "room-content-authored-id-duplicate",
                            path + ".id",
                            "Door authored ID is duplicated: " + authoredId);
                    }

                    RoomContentObjectDefinitionV1 objectDefinition = ResolveObject(
                        dto.Object,
                        RoomContentObjectKindV1.Door,
                        path + ".object");
                    RoomLiveLinkKindV1 linkKind = ParseLinkKind(
                        link.Kind,
                        path + ".link.kind");
                    RoomExitTypeV1 exitType = ParseExitType(
                        link.ExitType,
                        path + ".link.exit_type");
                    RoomVector2V1 position = ParseVector(
                        dto.Position,
                        path + ".position");
                    string signature = objectDefinition.ObjectStableId
                        + "|"
                        + linkKind
                        + "|"
                        + exitType
                        + "|"
                        + (link.TargetRoom ?? string.Empty)
                        + "|"
                        + (link.TargetSpawn ?? string.Empty)
                        + "|"
                        + (link.TargetSpawnKind ?? string.Empty)
                        + "|"
                        + Number(position.X)
                        + "|"
                        + Number(position.Y)
                        + "|"
                        + Number(dto.Rotation);
                    StableId doorId = identities.Create(
                        "door-instance",
                        source.RoomStableId,
                        "door",
                        authoredId,
                        signature,
                        path);
                    StableId exitId = identities.Create(
                        "exit",
                        source.RoomStableId,
                        "exit",
                        authoredId,
                        signature,
                        path);

                    DoorRuleDto rule = ResolveDoorRule(
                        rules,
                        authoredId,
                        linkKind,
                        exitType,
                        source,
                        path);
                    RoomCompletionConditionDefinitionV1 gate = ResolveGateCondition(
                        source,
                        rule,
                        conditions,
                        path);

                    StableId targetRoom = null;
                    StableId targetSpawn = null;
                    if (linkKind == RoomLiveLinkKindV1.Room)
                    {
                        targetRoom = ParseStableId(
                            link.TargetRoom,
                            path + ".link.target_room");
                        RoomSource targetSource;
                        if (!roomsById.TryGetValue(targetRoom, out targetSource))
                        {
                            throw Mapping(
                                "room-content-target-room-unknown",
                                path + ".link.target_room",
                                "Unknown target room: " + targetRoom);
                        }
                        targetSpawn = ResolveTargetSpawn(
                            targetSource,
                            link,
                            path + ".link");
                    }
                    else if (!string.IsNullOrWhiteSpace(link.TargetRoom)
                        || !string.IsNullOrWhiteSpace(link.TargetSpawn)
                        || !string.IsNullOrWhiteSpace(link.TargetSpawnKind))
                    {
                        throw Mapping(
                            "room-content-final-exit-target-invalid",
                            path + ".link",
                            "Final exits cannot reference a target room or spawn.");
                    }

                    doors.Add(new RoomDoorDefinitionV1(
                        doorId,
                        objectDefinition.PresentationStableId,
                        exitId,
                        new[] { gate.ConditionStableId },
                        position,
                        dto.Rotation));
                    exits.Add(new RoomExitLinkDefinitionV1(
                        exitId,
                        doorId,
                        linkKind,
                        exitType,
                        targetRoom,
                        targetSpawn));
                }
            }

            private DoorRuleDto ResolveDoorRule(
                List<DoorRuleDto> rules,
                string authoredId,
                RoomLiveLinkKindV1 linkKind,
                RoomExitTypeV1 exitType,
                RoomSource source,
                string doorPath)
            {
                DoorRuleDto selected = null;
                for (int index = 0; index < rules.Count; index++)
                {
                    string path = "$documents[\""
                        + source.EncounterKey
                        + "\"].door_rules["
                        + index
                        + "]";
                    DoorRuleDto candidate = Require(rules[index], path);
                    DoorMatchDto match = Require(candidate.Match, path + ".match");
                    bool hasSelector = !string.IsNullOrWhiteSpace(match.DoorId)
                        || !string.IsNullOrWhiteSpace(match.ExitType)
                        || !string.IsNullOrWhiteSpace(match.LinkKind);
                    if (!hasSelector)
                    {
                        throw Mapping(
                            "room-content-door-rule-selector-empty",
                            path + ".match",
                            "A door rule must select by door_id, exit_type, or link_kind.");
                    }

                    bool matches = true;
                    if (!string.IsNullOrWhiteSpace(match.DoorId))
                    {
                        matches &= authoredId != null
                            && string.Equals(
                                authoredId,
                                match.DoorId.Trim(),
                                StringComparison.Ordinal);
                    }
                    if (!string.IsNullOrWhiteSpace(match.ExitType))
                    {
                        matches &= ParseExitType(
                            match.ExitType,
                            path + ".match.exit_type") == exitType;
                    }
                    if (!string.IsNullOrWhiteSpace(match.LinkKind))
                    {
                        matches &= ParseLinkKind(
                            match.LinkKind,
                            path + ".match.link_kind") == linkKind;
                    }
                    if (!matches) continue;

                    if (selected != null)
                    {
                        throw Mapping(
                            "room-content-door-rule-ambiguous",
                            doorPath,
                            "More than one encounter rule matches this door.");
                    }
                    selected = candidate;
                }

                if (selected == null)
                {
                    throw Mapping(
                        "room-content-door-rule-missing",
                        doorPath,
                        "No encounter door rule matches this door.");
                }
                return selected;
            }

            private RoomCompletionConditionDefinitionV1 ResolveGateCondition(
                RoomSource source,
                DoorRuleDto rule,
                Dictionary<string, RoomCompletionConditionDefinitionV1> conditions,
                string doorPath)
            {
                string openWhen = RequireText(rule.OpenWhen, doorPath + ".open_when");
                if (string.Equals(openWhen, "room-complete", StringComparison.Ordinal))
                {
                    return conditions["room-complete"];
                }
                if (!string.Equals(openWhen, "room-entered", StringComparison.Ordinal)
                    && !string.Equals(openWhen, "always", StringComparison.Ordinal))
                {
                    throw Mapping(
                        "room-content-door-gate-unsupported",
                        doorPath + ".open_when",
                        "Supported door gates are room-complete, room-entered, and always.");
                }

                RoomCompletionConditionDefinitionV1 condition;
                if (conditions.TryGetValue(openWhen, out condition))
                {
                    return condition;
                }

                StableId id = identities.Create(
                    "completion",
                    source.RoomStableId,
                    "condition",
                    openWhen,
                    openWhen,
                    doorPath + ".open_when");
                condition = new RoomCompletionConditionDefinitionV1(
                    id,
                    RoomCompletionConditionKindV1.AlwaysSatisfied,
                    null,
                    false);
                conditions.Add(openWhen, condition);
                return condition;
            }

            private StableId ResolveTargetSpawn(
                RoomSource target,
                DoorLinkDto link,
                string path)
            {
                if (!string.IsNullOrWhiteSpace(link.TargetSpawn))
                {
                    RoomSpawnPointDefinitionV1 spawn;
                    if (!target.SpawnsByAuthoredId.TryGetValue(
                            link.TargetSpawn.Trim(),
                            out spawn))
                    {
                        throw Mapping(
                            "room-content-target-spawn-unknown",
                            path + ".target_spawn",
                            "Unknown target spawn ID in "
                            + target.RoomStableId
                            + ": "
                            + link.TargetSpawn);
                    }
                    return spawn.SpawnPointStableId;
                }

                RoomSpawnPointKindV1 kind = ParseSpawnKind(
                    link.TargetSpawnKind,
                    path + ".target_spawn_kind");
                List<RoomSpawnPointDefinitionV1> matches;
                if (!target.SpawnsByKind.TryGetValue(kind, out matches)
                    || matches.Count == 0)
                {
                    throw Mapping(
                        "room-content-target-spawn-kind-unknown",
                        path + ".target_spawn_kind",
                        "Target room has no spawn of kind " + kind + ".");
                }
                if (matches.Count != 1)
                {
                    throw Mapping(
                        "room-content-target-spawn-kind-ambiguous",
                        path + ".target_spawn_kind",
                        "Target room has multiple spawns of kind "
                        + kind
                        + "; add an optional spawn ID and target_spawn reference.");
                }
                return matches[0].SpawnPointStableId;
            }

            private RoomContentObjectDefinitionV1 ResolveObject(
                string value,
                RoomContentObjectKindV1 kind,
                string path)
            {
                StableId objectId = ParseStableId(value, path);
                RoomContentObjectDefinitionV1 definition;
                if (!objectCatalog.TryResolve(objectId, kind, out definition)
                    || definition == null)
                {
                    throw Mapping(
                        "room-content-object-unknown",
                        path,
                        "No " + kind + " content definition is registered for " + objectId + ".");
                }
                if (definition.Kind != kind || definition.ObjectStableId != objectId)
                {
                    throw Mapping(
                        "room-content-object-catalog-conflict",
                        path,
                        "The room-content catalog returned conflicting object facts for "
                        + objectId
                        + ".");
                }
                return definition;
            }
        }

        private sealed class RoomSource
        {
            private readonly List<RoomSpawnPointDefinitionV1> spawnDefinitions =
                new List<RoomSpawnPointDefinitionV1>();
            private readonly Dictionary<string, RoomSpawnPointDefinitionV1>
                spawnsByAuthoredId = new Dictionary<string, RoomSpawnPointDefinitionV1>(
                    StringComparer.Ordinal);
            private readonly Dictionary<RoomSpawnPointKindV1, List<RoomSpawnPointDefinitionV1>>
                spawnsByKind = new Dictionary<RoomSpawnPointKindV1, List<RoomSpawnPointDefinitionV1>>();

            public RoomSource(
                StableId roomStableId,
                RoomLayoutDto layout,
                EnemiesDto enemies,
                PropsDto props,
                DecorDto decor,
                EncounterDto encounter,
                string layoutKey,
                string enemiesKey,
                string propsKey,
                string decorKey,
                string encounterKey)
            {
                RoomStableId = roomStableId;
                Layout = layout;
                Enemies = enemies;
                Props = props;
                Decor = decor;
                Encounter = encounter;
                LayoutKey = layoutKey;
                EnemiesKey = enemiesKey;
                PropsKey = propsKey;
                DecorKey = decorKey;
                EncounterKey = encounterKey;
            }

            public StableId RoomStableId { get; }

            public RoomLayoutDto Layout { get; }

            public EnemiesDto Enemies { get; }

            public PropsDto Props { get; }

            public DecorDto Decor { get; }

            public EncounterDto Encounter { get; }

            public string LayoutKey { get; }

            public string EnemiesKey { get; }

            public string PropsKey { get; }

            public string DecorKey { get; }

            public string EncounterKey { get; }

            public IReadOnlyList<RoomSpawnPointDefinitionV1> SpawnDefinitions
            {
                get { return spawnDefinitions; }
            }

            public Dictionary<string, RoomSpawnPointDefinitionV1> SpawnsByAuthoredId
            {
                get { return spawnsByAuthoredId; }
            }

            public Dictionary<RoomSpawnPointKindV1, List<RoomSpawnPointDefinitionV1>>
                SpawnsByKind
            {
                get { return spawnsByKind; }
            }

            public void AddSpawn(
                string authoredId,
                RoomSpawnPointKindV1 kind,
                RoomSpawnPointDefinitionV1 definition,
                string path)
            {
                string normalizedId = NormalizeOptionalId(authoredId);
                if (normalizedId != null)
                {
                    if (spawnsByAuthoredId.ContainsKey(normalizedId))
                    {
                        throw Mapping(
                            "room-content-authored-id-duplicate",
                            path + ".id",
                            "Spawn authored ID is duplicated: " + normalizedId);
                    }
                    spawnsByAuthoredId.Add(normalizedId, definition);
                }

                List<RoomSpawnPointDefinitionV1> byKind;
                if (!spawnsByKind.TryGetValue(kind, out byKind))
                {
                    byKind = new List<RoomSpawnPointDefinitionV1>();
                    spawnsByKind.Add(kind, byKind);
                }
                byKind.Add(definition);
                spawnDefinitions.Add(definition);
            }
        }

        private sealed class GeneratedIdentityFactory
        {
            private readonly Dictionary<string, int> occurrences =
                new Dictionary<string, int>(StringComparer.Ordinal);

            public StableId Create(
                string namespaceName,
                StableId roomStableId,
                string section,
                string authoredId,
                string anonymousSignature,
                string path)
            {
                string normalizedId = NormalizeOptionalId(authoredId);
                string signature = normalizedId == null
                    ? "anonymous|" + anonymousSignature
                    : "authored|" + normalizedId;
                string occurrenceKey = namespaceName
                    + "|"
                    + roomStableId
                    + "|"
                    + section
                    + "|"
                    + signature;
                int occurrence;
                occurrences.TryGetValue(occurrenceKey, out occurrence);
                occurrences[occurrenceKey] = occurrence + 1;

                string hashInput = roomStableId
                    + "|"
                    + section
                    + "|"
                    + signature
                    + "|"
                    + occurrence.ToString(CultureInfo.InvariantCulture);
                string token;
                using (SHA256 sha = SHA256.Create())
                {
                    byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(hashInput));
                    var builder = new StringBuilder(24);
                    for (int index = 0; index < 12; index++)
                    {
                        builder.Append(hash[index].ToString(
                            "x2",
                            CultureInfo.InvariantCulture));
                    }
                    token = "room-content-" + builder;
                }

                try
                {
                    return StableId.Create(namespaceName, token);
                }
                catch (Exception exception)
                {
                    throw Mapping(
                        "room-content-generated-id-invalid",
                        path,
                        exception.Message);
                }
            }
        }

        private sealed class RoomContentMappingException : Exception
        {
            public RoomContentMappingException(
                string code,
                string path,
                string message)
                : base(message)
            {
                Code = code;
                Path = path;
            }

            public string Code { get; }

            public string Path { get; }
        }

        private static string NormalizeOptionalId(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }

        private static string Number(double value)
        {
            return value.ToString("R", CultureInfo.InvariantCulture);
        }

        private static RoomSpawnPointKindV1 ParseSpawnKind(
            string value,
            string path)
        {
            string text = RequireText(value, path);
            if (string.Equals(text, "forward-entry", StringComparison.Ordinal))
            {
                return RoomSpawnPointKindV1.ForwardEntry;
            }
            if (string.Equals(text, "return-entry", StringComparison.Ordinal))
            {
                return RoomSpawnPointKindV1.ReturnEntry;
            }
            if (string.Equals(text, "player", StringComparison.Ordinal))
            {
                return RoomSpawnPointKindV1.Player;
            }
            if (string.Equals(text, "auxiliary", StringComparison.Ordinal))
            {
                return RoomSpawnPointKindV1.Auxiliary;
            }

            throw Mapping(
                "room-content-spawn-kind-unsupported",
                path,
                "Unsupported spawn kind: " + text);
        }

        private static RoomLiveLinkKindV1 ParseLinkKind(
            string value,
            string path)
        {
            string text = RequireText(value, path);
            if (string.Equals(text, "room", StringComparison.Ordinal))
            {
                return RoomLiveLinkKindV1.Room;
            }
            if (string.Equals(text, "final-exit", StringComparison.Ordinal))
            {
                return RoomLiveLinkKindV1.FinalExit;
            }

            throw Mapping(
                "room-content-link-kind-unsupported",
                path,
                "Unsupported link kind: " + text);
        }

        private static RoomExitTypeV1 ParseExitType(
            string value,
            string path)
        {
            string text = RequireText(value, path);
            if (string.Equals(text, "progression", StringComparison.Ordinal))
            {
                return RoomExitTypeV1.Progression;
            }
            if (string.Equals(text, "return", StringComparison.Ordinal))
            {
                return RoomExitTypeV1.Return;
            }
            if (string.Equals(text, "optional", StringComparison.Ordinal))
            {
                return RoomExitTypeV1.Optional;
            }
            if (string.Equals(text, "secret", StringComparison.Ordinal))
            {
                return RoomExitTypeV1.Secret;
            }

            throw Mapping(
                "room-content-exit-type-unsupported",
                path,
                "Unsupported exit type: " + text);
        }
    }
}
