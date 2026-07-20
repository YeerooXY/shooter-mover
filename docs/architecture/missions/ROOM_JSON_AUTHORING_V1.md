# ROOM-DATA-001 — Split JSON room authoring

## Goal

Room content should be quick to read and safe to extend. Placing another ordinary enemy must not require manually authoring an entity-instance ID, XP amount, drop table, room-clear role, prefab path, player-damage callback, or reward handler.

The room package is split by responsibility:

- `manifest.json` — graph identity and document references;
- `*.layout.json` — room size, spawn points, doors, and links;
- `*.enemies.json` — enemy type, level, position, and rotation;
- `*.props.json` — gameplay-relevant prop placement;
- `*.decor.json` — tiles, background, and foreground presentation;
- `*.encounter.json` — completion and door-gate policy.

The Level 1 examples are under:

`Assets/ShooterMover/Content/Definitions/Missions/Rooms/Json/Level1`

## Enemy placement

An ordinary enemy requires no authored key:

```json
{
  "object": "enemy.moving-droid",
  "level": 2,
  "position": [4, 4],
  "rotation": 180
}
```

`object` is the durable content/type identity used by future enemy definitions, kill catalogs, statistics, XP projection, drop projection, and end-screen facts. `level` is an independent placement fact. A level-1 mission may therefore place a level-2 droid, and a later mission may still place a level-1 droid.

The importer generates a deterministic concrete runtime instance identity from:

- room identity;
- placement section;
- object identity;
- enemy level;
- position;
- rotation;
- duplicate occurrence when two placements are otherwise identical.

Reordering different anonymous entries does not change their generated identities. Identical placements remain separate concrete instances.

An optional `id` is accepted only as an addressing escape hatch. It is needed when encounter or scripted content must refer to one exact authored placement, such as an optional bonus enemy, objective target, named boss, or controllable spawner. Ordinary enemies should remain anonymous.

Dynamic enemies spawned during a run should derive their concrete identity from the generated spawner-source identity plus an authoritative spawn sequence. A future spawner document therefore does not need to assign IDs to every possible spawned droid.

## Authority boundary

Enemy placement JSON owns only:

- enemy object/type;
- enemy level;
- local position;
- local rotation;
- optional addressing ID.

It does not own:

- health or combat tuning;
- XP amount;
- drop profile or reward rolls;
- kill attribution;
- faction;
- player damage routing;
- runtime prefab construction;
- room-clear participation.

`BuiltInRoomContentObjectCatalogV1` currently maps concise authoring IDs to the existing runtime definition and presentation IDs. The resulting `RoomEnemyPlacementContentV1` preserves the concise object ID and level beside the compiled `AuthorableRoomGraphDefinitionV1`. A future enemy runtime factory can resolve those two facts into a live enemy and emit death facts containing concrete instance, object type, level, killer actor, and killer participant.

XP, drops, kill catalogs, statistics, and Results remain downstream consumers of death facts. They are not embedded in the room file or enemy runtime.

## Encounter policy

Room-clear and door behavior live in `*.encounter.json`, not enemy placements:

```json
{
  "room": "room.level1-entry",
  "completion": "all-enemies",
  "optional_enemy_ids": [],
  "door_rules": [
    {
      "match": { "exit_type": "progression" },
      "open_when": "room-complete"
    }
  ]
}
```

Supported V1 completion values:

- `all-enemies`;
- `always`.

Supported V1 door gates:

- `room-complete`;
- `room-entered`;
- `always`.

Door rules may match by optional authored `door_id`, `exit_type`, or `link_kind`. This avoids mandatory door keys for common progression, return, and final-exit layouts.

## Layout and doors

Room size and door placement live in the layout document:

```json
{
  "room": "room.level1-entry",
  "order": 0,
  "display_name": "DROID APPROACH",
  "bounds": {
    "center": [0, 0],
    "size": [24, 14]
  },
  "spawns": [
    {
      "kind": "forward-entry",
      "position": [-10, 0],
      "rotation": 0
    }
  ],
  "doors": [
    {
      "object": "door.room-standard",
      "position": [11, 0],
      "rotation": 0,
      "link": {
        "kind": "room",
        "exit_type": "progression",
        "target_room": "room.level1-terminal",
        "target_spawn_kind": "forward-entry"
      }
    }
  ]
}
```

Spawn IDs are optional. A door may target a unique spawn kind directly. If a target room has multiple spawns of the requested kind, the importer rejects the ambiguous link and asks for an optional spawn ID.

## Tile-area expansion

Repeated tile coordinates are unnecessary. An inclusive rectangular fill is authored once:

```json
{
  "object": "tile.floor-industrial",
  "fill": {
    "from": [0, 0],
    "to": [20, 20]
  }
}
```

This expands to 441 deterministic tile placements. Reversed corners are normalized. V1 rejects a room document that expands past 10,000 tiles.

Background and foreground objects remain separate arrays from gameplay props.

## Unity adapter

`JsonRoomContentDefinition2D` accepts one manifest `TextAsset` and keyed JSON document `TextAsset` entries. It imports through the engine-neutral `RoomContentJsonImporterV1` and the registered object catalog.

The importer returns:

- the compiled existing `AuthorableRoomGraphDefinitionV1` for ROOM-LIVE;
- enemy placement sidecars containing object type and level;
- prop placement sidecars;
- expanded tile/background/foreground placements;
- a deterministic full-content fingerprint;
- one structured failure issue on invalid input.

## Current integration boundary

This task adds the authoring/import/compilation foundation and a Level 1 JSON example. It deliberately does not replace the current Level 1 scene composition, which still manually binds one existing droid and one existing turret authority.

The safe follow-up is a definition-driven enemy runtime factory that iterates imported enemy placements and automatically registers each spawned authority with room terminal, player-damage, XP, drop, kill-stat, restart, and mission consumers. Only after that factory exists should the live Level 1 cut over from its current two-enemy manual bindings to arbitrary JSON-authored enemy counts.
