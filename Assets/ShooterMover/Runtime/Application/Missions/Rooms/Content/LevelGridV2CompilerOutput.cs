using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.Serialization.Json;
using System.Text;

namespace ShooterMover.Application.Missions.Rooms.Content
{
    public static partial class LevelGridV2Compiler
    {
        private sealed partial class Compiler
        {
            private RoomContentJsonPackageV1 BuildV1Package(
                string levelId,
                string startRoomId,
                string finalRoomId,
                string finalDoorId)
            {
                var ordered = new List<RoomSource>(rooms.Values);
                ordered.Sort((a, b) => CompareRoomIndex(a.Entry, b.Entry));
                var manifest = new V1ManifestDto
                {
                    Version = 1,
                    Layout = "layout.grid-v2-" + SanitizeKey(levelId),
                    StartRoom = startRoomId,
                    TerminalRoom = finalRoomId,
                    Rooms = new List<V1RoomDocumentsDto>(),
                };
                var documents = new Dictionary<string, string>(StringComparer.Ordinal);
                for (int i = 0; i < ordered.Count; i++)
                {
                    RoomSource room = ordered[i];
                    string prefix = "grid-v2." + i.ToString("00", CultureInfo.InvariantCulture) + "." + SanitizeKey(room.Entry.RoomId);
                    string layoutKey = prefix + ".layout";
                    string enemiesKey = prefix + ".enemies";
                    string propsKey = prefix + ".props";
                    string decorKey = prefix + ".decor";
                    string encounterKey = prefix + ".encounter";
                    manifest.Rooms.Add(new V1RoomDocumentsDto
                    {
                        Layout = layoutKey,
                        Enemies = enemiesKey,
                        Props = propsKey,
                        Decor = decorKey,
                        Encounter = encounterKey,
                    });
                    documents.Add(layoutKey, Serialize(BuildLayout(room, i, startRoomId, finalRoomId, finalDoorId)));
                    documents.Add(enemiesKey, Serialize(new V1EnemiesDto { Room = room.Entry.RoomId, Enemies = room.Enemies.Enemies }));
                    documents.Add(propsKey, Serialize(new V1PropsDto { Room = room.Entry.RoomId, Props = room.Props.Props }));
                    documents.Add(decorKey, Serialize(new V1DecorDto
                    {
                        Room = room.Entry.RoomId,
                        Tiles = room.Floor.Tiles,
                        Background = room.Decor.Background,
                        Foreground = room.Decor.Foreground,
                    }));
                    documents.Add(encounterKey, Serialize(BuildEncounter(room, finalRoomId, finalDoorId)));
                }
                return new RoomContentJsonPackageV1(Serialize(manifest), documents);
            }

            private V1RoomLayoutDto BuildLayout(
                RoomSource room,
                int order,
                string startRoomId,
                string finalRoomId,
                string finalDoorId)
            {
                var spawns = new List<V1SpawnDto>();
                if (string.Equals(room.Entry.RoomId, startRoomId, StringComparison.Ordinal))
                {
                    spawns.Add(new V1SpawnDto
                    {
                        Id = "player-start",
                        Kind = "player",
                        Position = room.Room.PlayerStart.Position,
                        Rotation = room.Room.PlayerStart.Rotation,
                    });
                }
                for (int i = 0; i < room.Doors.Count; i++)
                {
                    DoorSource door = room.Doors[i];
                    if (door.Connection != null)
                    {
                        double[] arrival = DeriveArrival(room, door);
                        spawns.Add(new V1SpawnDto
                        {
                            Id = ArrivalId(door.DoorId),
                            Kind = "auxiliary",
                            Position = arrival,
                            Rotation = RotationForArrival(door.Side),
                        });
                    }
                }

                var runtimeDoors = new List<V1DoorDto>();
                for (int i = 0; i < room.Doors.Count; i++)
                {
                    DoorSource door = room.Doors[i];
                    if (!door.Dto.Traversable) continue;
                    bool final = string.Equals(room.Entry.RoomId, finalRoomId, StringComparison.Ordinal)
                        && string.Equals(door.DoorId, finalDoorId, StringComparison.Ordinal);
                    V1DoorLinkDto link;
                    if (final)
                    {
                        link = new V1DoorLinkDto { Kind = "final-exit", ExitType = "progression" };
                    }
                    else
                    {
                        DoorSource target = door.Other;
                        link = new V1DoorLinkDto
                        {
                            Kind = "room",
                            ExitType = IsProgressionEndpoint(door) ? "progression" : "return",
                            TargetRoom = target.Room.Entry.RoomId,
                            TargetSpawn = ArrivalId(target.DoorId),
                        };
                    }
                    runtimeDoors.Add(new V1DoorDto
                    {
                        Id = door.DoorId,
                        Object = door.Dto.RuntimeObject,
                        Position = door.LocalPosition,
                        Rotation = RotationForDoor(door.Side),
                        Link = link,
                    });
                }
                return new V1RoomLayoutDto
                {
                    Room = room.Entry.RoomId,
                    Order = order,
                    DisplayName = string.IsNullOrWhiteSpace(room.Room.DisplayName)
                        ? room.Entry.RoomId
                        : room.Room.DisplayName.Trim(),
                    Bounds = new RuntimeBoundsDto { Center = room.Center, Size = room.Size },
                    Spawns = spawns,
                    Doors = runtimeDoors,
                };
            }

            private V1EncounterDto BuildEncounter(RoomSource room, string finalRoomId, string finalDoorId)
            {
                var rules = new List<DoorRuleDto>();
                var explicitByDoor = new Dictionary<string, DoorRuleDto>(StringComparer.Ordinal);
                for (int i = 0; i < room.Encounter.DoorRules.Count; i++)
                {
                    DoorRuleDto rule = Require(room.Encounter.DoorRules[i], room.Root + "encounter.json.door_rules[" + i + "]");
                    if (rule.Match != null && !string.IsNullOrWhiteSpace(rule.Match.DoorId))
                    {
                        explicitByDoor.Add(rule.Match.DoorId.Trim(), rule);
                    }
                    rules.Add(rule);
                }
                for (int i = 0; i < room.Doors.Count; i++)
                {
                    DoorSource door = room.Doors[i];
                    if (!door.Dto.Traversable || explicitByDoor.ContainsKey(door.DoorId)) continue;
                    bool final = string.Equals(room.Entry.RoomId, finalRoomId, StringComparison.Ordinal)
                        && string.Equals(door.DoorId, finalDoorId, StringComparison.Ordinal);
                    bool progression = final || IsProgressionEndpoint(door);
                    rules.Add(new DoorRuleDto
                    {
                        Match = new DoorMatchDto { DoorId = door.DoorId },
                        OpenWhen = progression ? "room-complete" : "always",
                    });
                }
                return new V1EncounterDto
                {
                    Room = room.Entry.RoomId,
                    Completion = room.Encounter.Completion,
                    OptionalEnemyIds = room.Encounter.OptionalEnemyIds,
                    DoorRules = rules,
                };
            }

            private static bool IsProgressionEndpoint(DoorSource door)
            {
                if (door == null || door.Connection == null || door.Connection.From == null)
                {
                    return false;
                }
                EndpointDto from = door.Connection.From;
                string roomId = string.IsNullOrWhiteSpace(from.RoomId)
                    ? string.Empty
                    : from.RoomId.Trim();
                string doorId = string.IsNullOrWhiteSpace(from.DoorId)
                    ? string.Empty
                    : from.DoorId.Trim();
                return string.Equals(
                        roomId,
                        door.Room.Entry.RoomId,
                        StringComparison.Ordinal)
                    && string.Equals(doorId, door.DoorId, StringComparison.Ordinal);
            }

        }
    }
}
