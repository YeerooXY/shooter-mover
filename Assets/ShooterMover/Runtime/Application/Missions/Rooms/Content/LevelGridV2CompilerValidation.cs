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
                room.Floor.Tiles = room.Floor.Tiles ?? new List<TileDto>();
                room.Enemies.Enemies = room.Enemies.Enemies ?? new List<EnemyDto>();
                room.Props.Props = room.Props.Props ?? new List<PropDto>();
                room.Decor.Background = room.Decor.Background ?? new List<VisualDto>();
                room.Decor.Foreground = room.Decor.Foreground ?? new List<VisualDto>();
                ValidateAuthoredIds(room.Enemies.Enemies, room.Props.Props, room.Root);
                for (int i = 0; i < room.Enemies.Enemies.Count; i++)
                {
                    EnemyDto enemy = Require(room.Enemies.Enemies[i], room.Root + "enemies.json.enemies[" + i + "]");
                    RequireText(enemy.Object, room.Root + "enemies.json.enemies[" + i + "].object");
                    RequireFiniteVector(enemy.Position, room.Root + "enemies.json.enemies[" + i + "].position");
                    if (enemy.Level <= 0)
                    {
                        throw Error("level-grid-v2-enemy-level-invalid", room.Root + "enemies.json.enemies[" + i + "].level", "Enemy level must be positive.");
                    }
                }
                for (int i = 0; i < room.Props.Props.Count; i++)
                {
                    PropDto prop = Require(room.Props.Props[i], room.Root + "props.json.props[" + i + "]");
                    RequireText(prop.Object, room.Root + "props.json.props[" + i + "].object");
                    RequireFiniteVector(prop.Position, room.Root + "props.json.props[" + i + "].position");
                }
                for (int i = 0; i < room.Floor.Tiles.Count; i++)
                {
                    TileDto tile = Require(room.Floor.Tiles[i], room.Root + "floor.json.tiles[" + i + "]");
                    RequireText(tile.Object, room.Root + "floor.json.tiles[" + i + "].object");
                    FillDto fill = Require(tile.Fill, room.Root + "floor.json.tiles[" + i + "].fill");
                    RequireVector(fill.From, room.Root + "floor.json.tiles[" + i + "].fill.from");
                    RequireVector(fill.To, room.Root + "floor.json.tiles[" + i + "].fill.to");
                }
            }

            private void ValidateAuthoredIds(List<EnemyDto> enemies, List<PropDto> props, string root)
            {
                var ids = new HashSet<string>(StringComparer.Ordinal);
                for (int i = 0; i < enemies.Count; i++)
                {
                    string id = RequireText(enemies[i].Id, root + "enemies.json.enemies[" + i + "].id");
                    if (!ids.Add(id)) throw Error("level-grid-v2-placement-id-duplicate", root, "Duplicate placement ID: " + id);
                }
                for (int i = 0; i < props.Count; i++)
                {
                    string id = RequireText(props[i].Id, root + "props.json.props[" + i + "].id");
                    if (!ids.Add(id)) throw Error("level-grid-v2-placement-id-duplicate", root, "Duplicate placement ID: " + id);
                }
            }

            private void ValidateLevelRoomLists()
            {
                List<string> ids = RequireList(level.RoomIds, "$.level.room_ids");
                var listed = new HashSet<string>(StringComparer.Ordinal);
                for (int i = 0; i < ids.Count; i++)
                {
                    string id = RequireText(ids[i], "$.level.room_ids[" + i + "]");
                    if (!listed.Add(id)) throw Error("level-grid-v2-room-id-duplicate", "$.level.room_ids", "Duplicate room ID: " + id);
                    if (!rooms.ContainsKey(id)) throw Error("level-grid-v2-room-index-missing", "$.level.room_ids[" + i + "]", "Unknown indexed room: " + id);
                }
                if (listed.Count != rooms.Count)
                {
                    throw Error("level-grid-v2-room-index-incomplete", "$.level.room_ids", "room_ids must contain every indexed room exactly once.");
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
                    if (!rooms.ContainsKey(roomId)) throw Error("level-grid-v2-map-room-unknown", path + ".room_id", "Unknown room: " + roomId);
                    if (!nodeIds.Add(roomId)) throw Error("level-grid-v2-map-room-duplicate", path + ".room_id", "Duplicate map node: " + roomId);
                }
                if (nodeIds.Count != rooms.Count) throw Error("level-grid-v2-map-incomplete", "$.map.nodes", "Map nodes must contain every room exactly once.");
            }

            private void LoadConnections()
            {
                List<ConnectionDto> values = RequireList(map.Connections, "$.map.connections");
                var connectionIds = new HashSet<string>(StringComparer.Ordinal);
                for (int i = 0; i < values.Count; i++)
                {
                    string path = "$.map.connections[" + i + "]";
                    ConnectionDto connection = Require(values[i], path);
                    string id = RequireText(connection.ConnectionId, path + ".connection_id");
                    if (!connectionIds.Add(id)) throw Error("level-grid-v2-connection-id-duplicate", path + ".connection_id", "Duplicate connection stable ID: " + id);
                    if (!string.Equals(RequireText(connection.TravelPolicy, path + ".travel_policy"), "Bidirectional", StringComparison.OrdinalIgnoreCase))
                    {
                        throw Error("level-grid-v2-travel-policy-unsupported", path + ".travel_policy", "The playable V2 compiler currently requires Bidirectional connections.");
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
                    if (ReferenceEquals(from.Room, to.Room)) throw Error("level-grid-v2-self-connection", path, "A connection must join different rooms.");
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
                if (!rooms.TryGetValue(roomId, out room)) throw Error("level-grid-v2-room-reference-unknown", path + ".room_id", "Unknown room: " + roomId);
                DoorSource door;
                if (!doors.TryGetValue(doorId, out door) || !ReferenceEquals(door.Room, room))
                {
                    throw Error("level-grid-v2-door-reference-unknown", path + ".door_id", "Unknown door endpoint: " + roomId + " + " + doorId);
                }
                return door;
            }

            private void RegisterEndpointUse(DoorSource door, ConnectionDto connection, string path)
            {
                string key = door.Room.Entry.RoomId + "::" + door.DoorId;
                if (connectionByEndpoint.ContainsKey(key))
                {
                    throw Error("level-grid-v2-endpoint-reused", path, "A door endpoint may be used by only one connection: " + key);
                }
                connectionByEndpoint.Add(key, connection);
            }

            private void ValidateStartAndFinal(string startRoomId, string finalRoomId, string finalDoorId)
            {
                RoomSource start;
                if (!rooms.TryGetValue(startRoomId, out start))
                {
                    throw Error("level-grid-v2-start-room-missing", "$.level.start_room_id", "Unknown start room: " + startRoomId);
                }
                if (start.Room.PlayerStart == null)
                {
                    throw Error("level-grid-v2-player-start-missing", start.Root + "room.json.player_start", "The start room requires one deterministic player_start.");
                }
                RequireFiniteVector(start.Room.PlayerStart.Position, start.Root + "room.json.player_start.position");
                RoomSource finalRoom;
                DoorSource finalDoor;
                if (!rooms.TryGetValue(finalRoomId, out finalRoom)
                    || !doors.TryGetValue(finalDoorId, out finalDoor)
                    || !ReferenceEquals(finalDoor.Room, finalRoom))
                {
                    throw Error("level-grid-v2-final-exit-invalid", "$.level.final_exit", "Final exit must reference an existing room ID + door ID endpoint.");
                }
                if (!finalDoor.Dto.Traversable)
                {
                    throw Error("level-grid-v2-final-exit-invalid", "$.level.final_exit", "Final exit endpoint must be traversable.");
                }
                if (finalDoor.Connection != null)
                {
                    throw Error("level-grid-v2-final-exit-connected", "$.level.final_exit", "Final exit endpoint cannot also participate in a room connection.");
                }
            }

            private void ValidateTraversableResolution(string finalRoomId, string finalDoorId)
            {
                foreach (DoorSource door in doors.Values)
                {
                    if (!door.Dto.Traversable) continue;
                    bool final = string.Equals(door.Room.Entry.RoomId, finalRoomId, StringComparison.Ordinal)
                        && string.Equals(door.DoorId, finalDoorId, StringComparison.Ordinal);
                    if (!final && door.Connection == null)
                    {
                        throw Error("level-grid-v2-traversable-door-unresolved", door.Room.Root + "doors.json", "Traversable door is neither connected nor the final exit: " + door.DoorId);
                    }
                }
            }

            private void ValidateReachability(string startRoomId)
            {
                var reached = new HashSet<string>(StringComparer.Ordinal) { startRoomId };
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
                            throw Error("level-grid-v2-room-inaccessible", "$.map.connections", "Required room is inaccessible from the start room: " + roomId);
                        }
                    }
                }
            }

        }
    }
}
