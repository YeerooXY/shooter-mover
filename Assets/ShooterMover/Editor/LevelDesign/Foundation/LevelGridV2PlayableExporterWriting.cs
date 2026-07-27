#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using ShooterMover.UnityAdapters.Authoring.LevelDesign;
using UnityEditor;
using UnityEngine;

namespace ShooterMover.Editor.LevelDesign.Foundation
{
    public static partial class LevelGridV2PlayableExporter
    {
        private static void WritePackage(
            LevelDesignSceneAuthoringRoot2D root,
            LevelGridPlayableMetadataV2 metadata,
            LevelRoomAuthoring2D[] rooms,
            LevelDoorEndpointAuthoring2D[] doors,
            LevelDoorLinkAuthoring2D[] links,
            string outputRoot)
        {
            Directory.CreateDirectory(outputRoot);
            string roomsRoot = Path.Combine(outputRoot, "Rooms");
            IReadOnlyDictionary<string, string> foldersByRoom =
                LevelGridV2RoomFolderMigration.Prepare(rooms, roomsRoot);

            var roomIndex = new RoomIndexDto[rooms.Length];
            var roomIds = new string[rooms.Length];
            var nodes = new MapNodeDto[rooms.Length];
            for (int i = 0; i < rooms.Length; i++)
            {
                LevelRoomAuthoring2D room = rooms[i];
                string folder = Path.GetFileName(foldersByRoom[room.RoomIdText]);
                roomIds[i] = room.RoomIdText;
                roomIndex[i] = new RoomIndexDto
                {
                    room_id = room.RoomIdText,
                    grid_position = new[] { room.GridCoordinate.x, room.GridCoordinate.y },
                    slot = room.FolderSlot,
                    folder = folder,
                };
                nodes[i] = new MapNodeDto
                {
                    room_id = room.RoomIdText,
                    grid_position = new[] { room.GridCoordinate.x, room.GridCoordinate.y },
                    slot = room.FolderSlot,
                    label = room.EditorLabel,
                    visible_on_map = room.VisibleOnMap,
                };
            }

            var connections = new ConnectionDto[links.Length];
            for (int i = 0; i < links.Length; i++)
            {
                connections[i] = new ConnectionDto
                {
                    connection_id = links[i].ConnectionIdText,
                    from = Endpoint(links[i].SourceRoom, links[i].SourceDoor),
                    to = Endpoint(links[i].DestinationRoom, links[i].DestinationDoor),
                    travel_policy = links[i].TravelPolicy.ToString(),
                };
            }

            WriteJson(
                Path.Combine(outputRoot, "level.json"),
                new LevelDto
                {
                    schema_version = 2,
                    level_id = root.LevelIdText,
                    authoring_state = "validated-playable",
                    runtime_import_status = "compiler-ready",
                    start_room_id = metadata.StartRoom.RoomIdText,
                    final_exit = Endpoint(metadata.FinalExitRoom, metadata.FinalExitDoor),
                    room_ids = roomIds,
                    rooms = roomIndex,
                });
            WriteJson(
                Path.Combine(outputRoot, "map.json"),
                new MapDto
                {
                    schema_version = 2,
                    nodes = nodes,
                    connections = connections,
                });

            for (int i = 0; i < rooms.Length; i++)
            {
                WriteRoom(
                    rooms[i],
                    doors,
                    metadata,
                    foldersByRoom[rooms[i].RoomIdText]);
            }
        }

        private static void WriteRoom(
            LevelRoomAuthoring2D room,
            LevelDoorEndpointAuthoring2D[] allDoors,
            LevelGridPlayableMetadataV2 metadata,
            string roomRoot)
        {
            Directory.CreateDirectory(roomRoot);
            RuntimeBoundsDto bounds = ResolveRoomLocalBounds(room);
            WriteJson(
                Path.Combine(roomRoot, "room.json"),
                new RoomDto
                {
                    schema_version = 2,
                    room_id = room.RoomIdText,
                    display_name = room.DisplayName,
                    grid_position = new[] { room.GridCoordinate.x, room.GridCoordinate.y },
                    slot = room.FolderSlot,
                    footprint_cells = new[] { room.FootprintCells.x, room.FootprintCells.y },
                    runtime_bounds = bounds,
                    player_start = room == metadata.StartRoom
                        ? new PlayerStartDto
                        {
                            position = new[]
                            {
                                metadata.PlayerStartLocalPosition.x,
                                metadata.PlayerStartLocalPosition.y,
                            },
                            rotation = metadata.PlayerStartRotation,
                        }
                        : null,
                });

            var roomDoors = new List<DoorDto>();
            for (int i = 0; i < allDoors.Length; i++)
            {
                LevelDoorEndpointAuthoring2D door = allDoors[i];
                if (door.OwningRoom != room) continue;
                Vector2 local = LevelGridPlayableMetadataV2.ResolveDoorLocalPosition(room, door.transform);
                roomDoors.Add(new DoorDto
                {
                    door_id = door.DoorIdText,
                    side = door.Side.ToString(),
                    placement_mode = door.PlacementMode.ToString(),
                    current_local_position = new[] { local.x, local.y },
                    traversable = door.Traversable,
                    visible_on_map = door.VisibleOnMap,
                    runtime_object = metadata.RuntimeDoorObjectId,
                });
            }
            WriteJson(
                Path.Combine(roomRoot, "doors.json"),
                new DoorsDto
                {
                    schema_version = 2,
                    room_id = room.RoomIdText,
                    doors = roomDoors.ToArray(),
                });

            WriteJsonIfMissing(
                Path.Combine(roomRoot, "floor.json"),
                new FloorDto
                {
                    schema_version = 2,
                    room = room.RoomIdText,
                    tiles = Array.Empty<TileDto>(),
                });
            WriteJsonIfMissing(
                Path.Combine(roomRoot, "enemies.json"),
                new EnemiesDto
                {
                    schema_version = 2,
                    room = room.RoomIdText,
                    enemies = Array.Empty<EnemyDto>(),
                });
            WriteJsonIfMissing(
                Path.Combine(roomRoot, "props.json"),
                new PropsDto
                {
                    schema_version = 2,
                    room = room.RoomIdText,
                    props = Array.Empty<PropDto>(),
                });
            WriteJsonIfMissing(
                Path.Combine(roomRoot, "decor.json"),
                new DecorDto
                {
                    schema_version = 2,
                    room = room.RoomIdText,
                    background = Array.Empty<VisualDto>(),
                    foreground = Array.Empty<VisualDto>(),
                });
            // encounter.json is deliberately optional. Missing or {} compiles to all-enemies.
        }


    }
}
#endif
