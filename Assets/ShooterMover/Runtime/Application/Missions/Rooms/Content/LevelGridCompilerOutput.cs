using System;
using System.Collections.Generic;
using System.Globalization;

namespace ShooterMover.Application.Missions.Rooms.Content
{
    public static partial class LevelGridCompiler
    {
        private sealed partial class Compiler
        {
            private RoomContentJsonPackage BuildV1Package(
                string levelId,
                string startRoomId,
                string finalRoomId,
                string finalDoorId)
            {
                var ordered = new List<RoomSource>(rooms.Values);
                ordered.Sort((a, b) => CompareRoomIndex(a.Entry, b.Entry));
                var manifest = new RoomContentManifestDto
                {
                    Version = 1,
                    Layout = "layout.level-1-" + SanitizeKey(levelId),
                    StartRoom = startRoomId,
                    TerminalRoom = finalRoomId,
                    Rooms = new List<RoomContentDocumentsDto>(),
                };
                var documents = new Dictionary<string, string>(StringComparer.Ordinal);
                for (int i = 0; i < ordered.Count; i++)
                {
                    RoomSource room = ordered[i];
                    string prefix = "level-1."
                        + i.ToString("00", CultureInfo.InvariantCulture)
                        + "."
                        + SanitizeKey(room.Entry.RoomId);
                    string layoutKey = prefix + ".layout";
                    string enemiesKey = prefix + ".enemies";
                    string propsKey = prefix + ".props";
                    string decorKey = prefix + ".decor";
                    string encounterKey = prefix + ".encounter";
                    manifest.Rooms.Add(new RoomContentDocumentsDto
                    {
                        Layout = layoutKey,
                        Enemies = enemiesKey,
                        Props = propsKey,
                        Decor = decorKey,
                        Encounter = encounterKey,
                    });
                    documents.Add(
                        layoutKey,
                        Serialize(BuildLayout(
                            room,
                            i,
                            startRoomId,
                            finalRoomId,
                            finalDoorId)));
                    documents.Add(
                        enemiesKey,
                        Serialize(new RoomContentEnemiesDto
                        {
                            Room = room.Entry.RoomId,
                            Enemies = room.Enemies.Enemies,
                        }));
                    documents.Add(
                        propsKey,
                        Serialize(new RoomContentPropsDto
                        {
                            Room = room.Entry.RoomId,
                            Props = room.Props.Props,
                        }));
                    documents.Add(
                        decorKey,
                        Serialize(new RoomContentDecorDto
                        {
                            Room = room.Entry.RoomId,
                            Tiles = room.Floor.Tiles,
                            Background = room.Decor.Background,
                            Foreground = room.Decor.Foreground,
                        }));
                    documents.Add(
                        encounterKey,
                        Serialize(BuildEncounter(room, finalRoomId, finalDoorId)));
                }
                return new RoomContentJsonPackage(Serialize(manifest), documents);
            }

            private RoomContentLayoutDto BuildLayout(
                RoomSource room,
                int order,
                string startRoomId,
                string finalRoomId,
                string finalDoorId)
            {
                var spawns = new List<RoomContentSpawnDto>();
                if (string.Equals(
                    room.Entry.RoomId,
                    startRoomId,
                    StringComparison.Ordinal))
                {
                    spawns.Add(new RoomContentSpawnDto
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
                        spawns.Add(new RoomContentSpawnDto
                        {
                            Id = ArrivalId(door.DoorId),
                            Kind = "auxiliary",
                            Position = arrival,
                            Rotation = RotationForArrival(door.Side),
                        });
                    }
                }

                var runtimeDoors = new List<RoomContentDoorDto>();
                for (int i = 0; i < room.Doors.Count; i++)
                {
                    DoorSource door = room.Doors[i];
                    if (!door.Dto.Traversable) continue;
                    bool final = IsFinalDoor(
                        door,
                        finalRoomId,
                        finalDoorId);
                    RoomContentDoorLinkDto link;
                    if (final)
                    {
                        link = new RoomContentDoorLinkDto
                        {
                            Kind = "final-exit",
                            ExitType = "progression",
                        };
                    }
                    else
                    {
                        DoorSource target = door.Other;
                        link = new RoomContentDoorLinkDto
                        {
                            Kind = "room",
                            ExitType = IsProgressionEndpoint(door)
                                ? "progression"
                                : "return",
                            TargetRoom = target.Room.Entry.RoomId,
                            TargetSpawn = ArrivalId(target.DoorId),
                        };
                    }
                    runtimeDoors.Add(new RoomContentDoorDto
                    {
                        Id = door.DoorId,
                        Object = door.Dto.RuntimeObject,
                        Position = door.LocalPosition,
                        Rotation = RotationForDoor(door.Side),
                        Link = link,
                    });
                }
                return new RoomContentLayoutDto
                {
                    Room = room.Entry.RoomId,
                    Order = order,
                    DisplayName = string.IsNullOrWhiteSpace(room.Room.DisplayName)
                        ? room.Entry.RoomId
                        : room.Room.DisplayName.Trim(),
                    Bounds = new LiveBoundsDto
                    {
                        Center = room.Center,
                        Size = room.Size,
                    },
                    Spawns = spawns,
                    Doors = runtimeDoors,
                };
            }

            private RoomContentEncounterDto BuildEncounter(
                RoomSource room,
                string finalRoomId,
                string finalDoorId)
            {
                var rules = new List<DoorRuleDto>();
                int authoredCount = room.Encounter.DoorRules.Count;
                var authoredMatched = new bool[authoredCount];
                for (int i = 0; i < authoredCount; i++)
                {
                    DoorRuleDto rule = Require(
                        room.Encounter.DoorRules[i],
                        room.Root + "encounter.json.door_rules[" + i + "]");
                    rules.Add(rule);
                }

                for (int i = 0; i < room.Doors.Count; i++)
                {
                    DoorSource door = room.Doors[i];
                    if (!door.Dto.Traversable) continue;

                    bool final = IsFinalDoor(
                        door,
                        finalRoomId,
                        finalDoorId);
                    int matchingRule = -1;
                    for (int ruleIndex = 0;
                        ruleIndex < authoredCount;
                        ruleIndex++)
                    {
                        DoorRuleDto candidate = room.Encounter.DoorRules[ruleIndex];
                        if (!RuleMatchesDoor(candidate, door, final)) continue;
                        if (matchingRule >= 0)
                        {
                            throw Error(
                                "level-level-1-encounter-door-rule-ambiguous",
                                room.Root + "encounter.json.door_rules",
                                "More than one authored encounter rule matches traversable door: "
                                    + door.DoorId);
                        }
                        matchingRule = ruleIndex;
                    }

                    if (matchingRule >= 0)
                    {
                        authoredMatched[matchingRule] = true;
                        continue;
                    }

                    bool progression = final || IsProgressionEndpoint(door);
                    rules.Add(new DoorRuleDto
                    {
                        Match = new DoorMatchDto
                        {
                            DoorId = door.DoorId,
                        },
                        OpenWhen = progression ? "room-complete" : "always",
                    });
                }

                for (int i = 0; i < authoredMatched.Length; i++)
                {
                    if (!authoredMatched[i])
                    {
                        throw Error(
                            "level-level-1-encounter-door-rule-unmatched",
                            room.Root + "encounter.json.door_rules[" + i + "]",
                            "The authored encounter rule matches no traversable runtime door.");
                    }
                }

                return new RoomContentEncounterDto
                {
                    Room = room.Entry.RoomId,
                    Completion = room.Encounter.Completion,
                    OptionalEnemyIds = room.Encounter.OptionalEnemyIds,
                    DoorRules = rules,
                };
            }

            private static bool RuleMatchesDoor(
                DoorRuleDto rule,
                DoorSource door,
                bool final)
            {
                DoorMatchDto match = rule.Match;
                if (!string.IsNullOrWhiteSpace(match.DoorId)
                    && !string.Equals(
                        match.DoorId,
                        door.DoorId,
                        StringComparison.Ordinal))
                {
                    return false;
                }

                string exitType = final || IsProgressionEndpoint(door)
                    ? "progression"
                    : "return";
                if (!string.IsNullOrWhiteSpace(match.ExitType)
                    && !string.Equals(
                        match.ExitType,
                        exitType,
                        StringComparison.Ordinal))
                {
                    return false;
                }

                string linkKind = final ? "final-exit" : "room";
                if (!string.IsNullOrWhiteSpace(match.LinkKind)
                    && !string.Equals(
                        match.LinkKind,
                        linkKind,
                        StringComparison.Ordinal))
                {
                    return false;
                }
                return true;
            }

            private static bool IsFinalDoor(
                DoorSource door,
                string finalRoomId,
                string finalDoorId)
            {
                return string.Equals(
                        door.Room.Entry.RoomId,
                        finalRoomId,
                        StringComparison.Ordinal)
                    && string.Equals(
                        door.DoorId,
                        finalDoorId,
                        StringComparison.Ordinal);
            }

            private static bool IsProgressionEndpoint(DoorSource door)
            {
                if (door == null
                    || door.Connection == null
                    || door.Connection.From == null)
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
                    && string.Equals(
                        doorId,
                        door.DoorId,
                        StringComparison.Ordinal);
            }

        }
    }
}
