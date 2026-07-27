using System;
using System.Collections.Generic;

namespace ShooterMover.Application.Missions.Rooms.Content
{
    public static partial class LevelGridV2Compiler
    {
        private sealed partial class Compiler
        {
            private void ValidateRoomSidecars(RoomSource room)
            {
                room.Floor.Tiles = RequireList(
                    room.Floor.Tiles,
                    room.Root + "floor.json.tiles");
                room.Enemies.Enemies = RequireList(
                    room.Enemies.Enemies,
                    room.Root + "enemies.json.enemies");
                room.Props.Props = RequireList(
                    room.Props.Props,
                    room.Root + "props.json.props");
                room.Decor.Background = RequireList(
                    room.Decor.Background,
                    room.Root + "decor.json.background");
                room.Decor.Foreground = RequireList(
                    room.Decor.Foreground,
                    room.Root + "decor.json.foreground");

                ValidateAuthoredIds(room.Enemies.Enemies, room.Props.Props, room.Root);
                ValidateEncounter(room);

                for (int i = 0; i < room.Enemies.Enemies.Count; i++)
                {
                    string path = room.Root + "enemies.json.enemies[" + i + "]";
                    EnemyDto enemy = Require(room.Enemies.Enemies[i], path);
                    RequireText(enemy.Object, path + ".object");
                    RequireFiniteVector(enemy.Position, path + ".position");
                    RequireFinite(enemy.Rotation, path + ".rotation");
                    if (enemy.Level <= 0)
                    {
                        throw Error(
                            "level-grid-v2-enemy-level-invalid",
                            path + ".level",
                            "Enemy level must be positive.");
                    }
                }

                for (int i = 0; i < room.Props.Props.Count; i++)
                {
                    string path = room.Root + "props.json.props[" + i + "]";
                    PropDto prop = Require(room.Props.Props[i], path);
                    RequireText(prop.Object, path + ".object");
                    RequireFiniteVector(prop.Position, path + ".position");
                    RequireFinite(prop.Rotation, path + ".rotation");
                }

                for (int i = 0; i < room.Floor.Tiles.Count; i++)
                {
                    string path = room.Root + "floor.json.tiles[" + i + "]";
                    TileDto tile = Require(room.Floor.Tiles[i], path);
                    RequireText(tile.Object, path + ".object");
                    FillDto fill = Require(tile.Fill, path + ".fill");
                    RequireVector(fill.From, path + ".fill.from");
                    RequireVector(fill.To, path + ".fill.to");
                }

                ValidateVisuals(
                    room.Decor.Background,
                    room.Root + "decor.json.background");
                ValidateVisuals(
                    room.Decor.Foreground,
                    room.Root + "decor.json.foreground");
            }

            private void ValidateAuthoredIds(
                List<EnemyDto> enemies,
                List<PropDto> props,
                string root)
            {
                var ids = new HashSet<string>(StringComparer.Ordinal);
                for (int i = 0; i < enemies.Count; i++)
                {
                    string path = root + "enemies.json.enemies[" + i + "]";
                    EnemyDto enemy = Require(enemies[i], path);
                    string id = RequireText(enemy.Id, path + ".id");
                    enemy.Id = id;
                    if (!ids.Add(id))
                    {
                        throw Error(
                            "level-grid-v2-placement-id-duplicate",
                            path + ".id",
                            "Duplicate placement ID: " + id);
                    }
                }
                for (int i = 0; i < props.Count; i++)
                {
                    string path = root + "props.json.props[" + i + "]";
                    PropDto prop = Require(props[i], path);
                    string id = RequireText(prop.Id, path + ".id");
                    prop.Id = id;
                    if (!ids.Add(id))
                    {
                        throw Error(
                            "level-grid-v2-placement-id-duplicate",
                            path + ".id",
                            "Duplicate placement ID: " + id);
                    }
                }
            }

            private void ValidateEncounter(RoomSource room)
            {
                string completion = RequireText(
                    room.Encounter.Completion,
                    room.Root + "encounter.json.completion");
                if (!string.Equals(completion, "all-enemies", StringComparison.Ordinal)
                    && !string.Equals(completion, "always", StringComparison.Ordinal))
                {
                    throw Error(
                        "level-grid-v2-completion-unsupported",
                        room.Root + "encounter.json.completion",
                        "Completion must be all-enemies or always.");
                }
                room.Encounter.Completion = completion;

                ValidateOptionalEnemyIds(room);
                ValidateEncounterRules(room);
            }

            private void ValidateOptionalEnemyIds(RoomSource room)
            {
                List<string> values = RequireList(
                    room.Encounter.OptionalEnemyIds,
                    room.Root + "encounter.json.optional_enemy_ids");
                var enemyIds = new HashSet<string>(StringComparer.Ordinal);
                for (int i = 0; i < room.Enemies.Enemies.Count; i++)
                {
                    EnemyDto enemy = Require(
                        room.Enemies.Enemies[i],
                        room.Root + "enemies.json.enemies[" + i + "]");
                    enemyIds.Add(RequireText(
                        enemy.Id,
                        room.Root + "enemies.json.enemies[" + i + "].id"));
                }

                var seen = new HashSet<string>(StringComparer.Ordinal);
                for (int i = 0; i < values.Count; i++)
                {
                    string path = room.Root
                        + "encounter.json.optional_enemy_ids[" + i + "]";
                    string id = RequireText(values[i], path);
                    values[i] = id;
                    if (!seen.Add(id))
                    {
                        throw Error(
                            "level-grid-v2-optional-enemy-duplicate",
                            path,
                            "Optional enemy ID is duplicated: " + id);
                    }
                    if (!enemyIds.Contains(id))
                    {
                        throw Error(
                            "level-grid-v2-optional-enemy-unknown",
                            path,
                            "Optional enemy ID does not exist in this room: " + id);
                    }
                }
            }

            private void ValidateEncounterRules(RoomSource room)
            {
                List<DoorRuleDto> rules = RequireList(
                    room.Encounter.DoorRules,
                    room.Root + "encounter.json.door_rules");
                var directDoorRules = new HashSet<string>(StringComparer.Ordinal);
                for (int i = 0; i < rules.Count; i++)
                {
                    string path = room.Root + "encounter.json.door_rules[" + i + "]";
                    DoorRuleDto rule = Require(rules[i], path);
                    DoorMatchDto match = Require(rule.Match, path + ".match");
                    bool hasDoor = !string.IsNullOrWhiteSpace(match.DoorId);
                    bool hasExitType = !string.IsNullOrWhiteSpace(match.ExitType);
                    bool hasLinkKind = !string.IsNullOrWhiteSpace(match.LinkKind);
                    if (!hasDoor && !hasExitType && !hasLinkKind)
                    {
                        throw Error(
                            "level-grid-v2-door-rule-selector-empty",
                            path + ".match",
                            "A door rule must select by door_id, exit_type, or link_kind.");
                    }

                    if (hasDoor)
                    {
                        string doorId = match.DoorId.Trim();
                        bool found = false;
                        for (int doorIndex = 0; doorIndex < room.Doors.Count; doorIndex++)
                        {
                            if (string.Equals(
                                room.Doors[doorIndex].DoorId,
                                doorId,
                                StringComparison.Ordinal))
                            {
                                found = true;
                                break;
                            }
                        }
                        if (!found)
                        {
                            throw Error(
                                "level-grid-v2-encounter-door-unknown",
                                path + ".match.door_id",
                                "Encounter rule references a door not owned by this room: "
                                    + doorId);
                        }
                        if (!directDoorRules.Add(doorId))
                        {
                            throw Error(
                                "level-grid-v2-encounter-door-rule-duplicate",
                                path + ".match.door_id",
                                "More than one explicit encounter rule references door: "
                                    + doorId);
                        }
                        match.DoorId = doorId;
                    }

                    if (hasExitType)
                    {
                        string exitType = match.ExitType.Trim();
                        if (!string.Equals(exitType, "progression", StringComparison.Ordinal)
                            && !string.Equals(exitType, "return", StringComparison.Ordinal))
                        {
                            throw Error(
                                "level-grid-v2-door-rule-exit-type-invalid",
                                path + ".match.exit_type",
                                "Door-rule exit_type must be progression or return.");
                        }
                        match.ExitType = exitType;
                    }
                    if (hasLinkKind)
                    {
                        string linkKind = match.LinkKind.Trim();
                        if (!string.Equals(linkKind, "room", StringComparison.Ordinal)
                            && !string.Equals(linkKind, "final-exit", StringComparison.Ordinal))
                        {
                            throw Error(
                                "level-grid-v2-door-rule-link-kind-invalid",
                                path + ".match.link_kind",
                                "Door-rule link_kind must be room or final-exit.");
                        }
                        match.LinkKind = linkKind;
                    }

                    string openWhen = RequireText(rule.OpenWhen, path + ".open_when");
                    if (!string.Equals(openWhen, "room-complete", StringComparison.Ordinal)
                        && !string.Equals(openWhen, "room-entered", StringComparison.Ordinal)
                        && !string.Equals(openWhen, "always", StringComparison.Ordinal))
                    {
                        throw Error(
                            "level-grid-v2-door-rule-gate-invalid",
                            path + ".open_when",
                            "Door-rule open_when must be room-complete, room-entered, or always.");
                    }
                    rule.OpenWhen = openWhen;
                }
            }

            private static void ValidateVisuals(
                List<VisualDto> values,
                string root)
            {
                for (int i = 0; i < values.Count; i++)
                {
                    string path = root + "[" + i + "]";
                    VisualDto visual = Require(values[i], path);
                    RequireText(visual.Object, path + ".object");
                    RequireFiniteVector(visual.Position, path + ".position");
                    RequireFinite(visual.Rotation, path + ".rotation");
                }
            }

            private static double RequireFinite(double value, string path)
            {
                if (!IsFinite(value))
                {
                    throw Error(
                        "level-grid-v2-number-invalid",
                        path,
                        "A finite numeric value is required.");
                }
                return value;
            }

            private void ValidateLevelRoomLists()
            {
                List<string> ids = RequireList(level.RoomIds, "$.level.room_ids");
                var listed = new HashSet<string>(StringComparer.Ordinal);
                for (int i = 0; i < ids.Count; i++)
                {
                    string id = RequireText(ids[i], "$.level.room_ids[" + i + "]");
                    if (!listed.Add(id))
                    {
                        throw Error(
                            "level-grid-v2-room-id-duplicate",
                            "$.level.room_ids",
                            "Duplicate room ID: " + id);
                    }
                    if (!rooms.ContainsKey(id))
                    {
                        throw Error(
                            "level-grid-v2-room-index-missing",
                            "$.level.room_ids[" + i + "]",
                            "Unknown indexed room: " + id);
                    }
                }
                if (listed.Count != rooms.Count)
                {
                    throw Error(
                        "level-grid-v2-room-index-incomplete",
                        "$.level.room_ids",
                        "room_ids must contain every indexed room exactly once.");
                }
            }

            private void ValidateMapNodes()
            {
                List<MapNodeDto> nodes = RequireList(map.Nodes, "$.map.nodes");
                var nodeIds = new HashSet<string>(StringComparer.Ordinal);
                for (int i = 0; i < nodes.Count; i++)
                {
                    string path = "$.map.nodes[" + i + "]";
                    MapNodeDto node = Require(nodes[i], path);
                    string roomId = RequireText(node.RoomId, path + ".room_id");
                    RoomSource room;
                    if (!rooms.TryGetValue(roomId, out room))
                    {
                        throw Error(
                            "level-grid-v2-map-room-unknown",
                            path + ".room_id",
                            "Unknown room: " + roomId);
                    }
                    if (!nodeIds.Add(roomId))
                    {
                        throw Error(
                            "level-grid-v2-map-room-duplicate",
                            path + ".room_id",
                            "Duplicate map node: " + roomId);
                    }
                    RequireSameVector(
                        room.Entry.GridPosition,
                        node.GridPosition,
                        path + ".grid_position");
                    if (node.Slot != room.Entry.Slot)
                    {
                        throw Error(
                            "level-grid-v2-map-slot-mismatch",
                            path + ".slot",
                            "Map node slot must match the authoritative room index.");
                    }
                }
                if (nodeIds.Count != rooms.Count)
                {
                    throw Error(
                        "level-grid-v2-map-incomplete",
                        "$.map.nodes",
                        "Map nodes must contain every room exactly once.");
                }
            }

            private void LoadConnections()
            {
                List<ConnectionDto> values = RequireList(
                    map.Connections,
                    "$.map.connections");
                var connectionIds = new HashSet<string>(StringComparer.Ordinal);
                for (int i = 0; i < values.Count; i++)
                {
                    string path = "$.map.connections[" + i + "]";
                    ConnectionDto connection = Require(values[i], path);
                    string id = RequireText(
                        connection.ConnectionId,
                        path + ".connection_id");
                    if (!connectionIds.Add(id))
                    {
                        throw Error(
                            "level-grid-v2-connection-id-duplicate",
                            path + ".connection_id",
                            "Duplicate connection stable ID: " + id);
                    }
                    if (!string.Equals(
                        RequireText(connection.TravelPolicy, path + ".travel_policy"),
                        "Bidirectional",
                        StringComparison.OrdinalIgnoreCase))
                    {
                        throw Error(
                            "level-grid-v2-travel-policy-unsupported",
                            path + ".travel_policy",
                            "The playable V2 compiler currently requires Bidirectional connections.");
                    }
                    DoorSource from = ResolveEndpoint(connection.From, path + ".from");
                    DoorSource to = ResolveEndpoint(connection.To, path + ".to");
                    if (!from.Dto.Traversable || !to.Dto.Traversable)
                    {
                        throw Error(
                            "level-grid-v2-connection-door-nontraversable",
                            path,
                            "Every endpoint in a playable room connection must be traversable.");
                    }
                    if (ReferenceEquals(from.Room, to.Room))
                    {
                        throw Error(
                            "level-grid-v2-self-connection",
                            path,
                            "A connection must join different rooms.");
                    }
                    RegisterEndpointUse(from, connection, path + ".from");
                    RegisterEndpointUse(to, connection, path + ".to");
                    from.Connection = connection;
                    from.Other = to;
                    to.Connection = connection;
                    to.Other = from;
                }
            }

            private DoorSource ResolveEndpoint(EndpointDto endpoint, string path)
            {
                endpoint = Require(endpoint, path);
                string roomId = RequireText(endpoint.RoomId, path + ".room_id");
                string doorId = RequireText(endpoint.DoorId, path + ".door_id");
                RoomSource room;
                if (!rooms.TryGetValue(roomId, out room))
                {
                    throw Error(
                        "level-grid-v2-room-reference-unknown",
                        path + ".room_id",
                        "Unknown room: " + roomId);
                }
                DoorSource door;
                if (!doors.TryGetValue(doorId, out door)
                    || !ReferenceEquals(door.Room, room))
                {
                    throw Error(
                        "level-grid-v2-door-reference-unknown",
                        path + ".door_id",
                        "Unknown door endpoint: " + roomId + " + " + doorId);
                }
                return door;
            }

            private void RegisterEndpointUse(
                DoorSource door,
                ConnectionDto connection,
                string path)
            {
                string key = door.Room.Entry.RoomId + "::" + door.DoorId;
                if (connectionByEndpoint.ContainsKey(key))
                {
                    throw Error(
                        "level-grid-v2-endpoint-reused",
                        path,
                        "A door endpoint may be used by only one connection: " + key);
                }
                connectionByEndpoint.Add(key, connection);
            }

            private void ValidateStartAndFinal(
                string startRoomId,
                string finalRoomId,
                string finalDoorId)
            {
                RoomSource start;
                if (!rooms.TryGetValue(startRoomId, out start))
                {
                    throw Error(
                        "level-grid-v2-start-room-missing",
                        "$.level.start_room_id",
                        "Unknown start room: " + startRoomId);
                }

                foreach (RoomSource room in rooms.Values)
                {
                    bool isStart = string.Equals(
                        room.Entry.RoomId,
                        startRoomId,
                        StringComparison.Ordinal);
                    if (isStart && room.Room.PlayerStart == null)
                    {
                        throw Error(
                            "level-grid-v2-player-start-missing",
                            room.Root + "room.json.player_start",
                            "The start room requires one deterministic player_start.");
                    }
                    if (!isStart && room.Room.PlayerStart != null)
                    {
                        throw Error(
                            "level-grid-v2-player-start-extra",
                            room.Root + "room.json.player_start",
                            "Only the authoritative start room may define player_start.");
                    }
                }

                double[] startPosition = RequireFiniteVector(
                    start.Room.PlayerStart.Position,
                    start.Root + "room.json.player_start.position");
                RequireFinite(
                    start.Room.PlayerStart.Rotation,
                    start.Root + "room.json.player_start.rotation");
                double minX = start.Center[0] - start.Size[0] * 0.5d;
                double maxX = start.Center[0] + start.Size[0] * 0.5d;
                double minY = start.Center[1] - start.Size[1] * 0.5d;
                double maxY = start.Center[1] + start.Size[1] * 0.5d;
                if (startPosition[0] < minX || startPosition[0] > maxX
                    || startPosition[1] < minY || startPosition[1] > maxY)
                {
                    throw Error(
                        "level-grid-v2-player-start-outside-bounds",
                        start.Root + "room.json.player_start.position",
                        "The deterministic player start must lie inside the start-room bounds.");
                }

                RoomSource finalRoom;
                DoorSource finalDoor;
                if (!rooms.TryGetValue(finalRoomId, out finalRoom)
                    || !doors.TryGetValue(finalDoorId, out finalDoor)
                    || !ReferenceEquals(finalDoor.Room, finalRoom))
                {
                    throw Error(
                        "level-grid-v2-final-exit-invalid",
                        "$.level.final_exit",
                        "Final exit must reference an existing room ID + door ID endpoint.");
                }
                if (!finalDoor.Dto.Traversable)
                {
                    throw Error(
                        "level-grid-v2-final-exit-invalid",
                        "$.level.final_exit",
                        "Final exit endpoint must be traversable.");
                }
                if (finalDoor.Connection != null)
                {
                    throw Error(
                        "level-grid-v2-final-exit-connected",
                        "$.level.final_exit",
                        "Final exit endpoint cannot also participate in a room connection.");
                }
            }

            private void ValidateTraversableResolution(
                string finalRoomId,
                string finalDoorId)
            {
                foreach (DoorSource door in doors.Values)
                {
                    if (!door.Dto.Traversable) continue;
                    bool final = string.Equals(
                            door.Room.Entry.RoomId,
                            finalRoomId,
                            StringComparison.Ordinal)
                        && string.Equals(
                            door.DoorId,
                            finalDoorId,
                            StringComparison.Ordinal);
                    if (!final && door.Connection == null)
                    {
                        throw Error(
                            "level-grid-v2-traversable-door-unresolved",
                            door.Room.Root + "doors.json",
                            "Traversable door is neither connected nor the final exit: "
                                + door.DoorId);
                    }
                }
            }

            private void ValidateReachability(string startRoomId)
            {
                var reached = new HashSet<string>(StringComparer.Ordinal)
                {
                    startRoomId,
                };
                var pending = new Queue<string>();
                pending.Enqueue(startRoomId);
                while (pending.Count > 0)
                {
                    RoomSource room = rooms[pending.Dequeue()];
                    for (int i = 0; i < room.Doors.Count; i++)
                    {
                        DoorSource other = room.Doors[i].Other;
                        if (other != null && reached.Add(other.Room.Entry.RoomId))
                        {
                            pending.Enqueue(other.Room.Entry.RoomId);
                        }
                    }
                }
                if (reached.Count != rooms.Count)
                {
                    foreach (string roomId in rooms.Keys)
                    {
                        if (!reached.Contains(roomId))
                        {
                            throw Error(
                                "level-grid-v2-room-inaccessible",
                                "$.map.connections",
                                "Required room is inaccessible from the start room: "
                                    + roomId);
                        }
                    }
                }
            }

        }
    }
}
