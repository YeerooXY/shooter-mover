# Level Grid Authoring V2 Phase 1 — Three-Room Starter Example

## Scope

This example demonstrates the **Phase 1 editor graph and safe authoring export**.
It does not claim the exported folder is consumed by the current playable-room
runtime. The package writes `runtime_import_status: "not-connected"` explicitly.

## Scene authoring command

Select an object below a `LevelDesignSceneAuthoringRoot2D`, then use:

`Tools > Shooter Mover > Level Design > Create Three-Room Starter Example`

The command creates one atomic, undoable authoring group:

```text
Three-Room Starter Example
├── Starter Room                         grid (0,0), slot 01
│   └── East Door
├── Room 1,0                             grid (1,0), slot 01
│   ├── West Door
│   └── East Door
├── Room 2,0                             grid (2,0), slot 01
│   └── West Door
├── Starter to Room 1,0
└── Room 1,0 to Room 2,0
```

Only the first room has an explicit display name. The two rooms to its right keep
an empty `displayName` and use automatic coordinate labels.

## First exported folder layout

The suffix is the room's explicit **per-coordinate slot**, not its ordinal in the
whole level:

```text
ThreeRoomStarter/
├── level.json
├── map.json
└── Rooms/
    ├── Room_0_0_01/
    │   ├── room.json
    │   ├── doors.json
    │   ├── floor.json
    │   ├── enemies.json
    │   ├── props.json
    │   ├── decor.json
    │   └── encounter.json
    ├── Room_1_0_01/
    │   ├── room.json
    │   ├── doors.json
    │   ├── floor.json
    │   ├── enemies.json
    │   ├── props.json
    │   ├── decor.json
    │   └── encounter.json
    └── Room_2_0_01/
        ├── room.json
        ├── doors.json
        ├── floor.json
        ├── enemies.json
        ├── props.json
        ├── decor.json
        └── encounter.json
```

For two draft rooms at the same coordinate, their slots must be distinct, for
example `Room_5_2_01` and `Room_5_2_02`. The existing foundation validator may
still reject their physical overlap for validated publishing.

## Moving a room

Suppose `room.right-02` moves from `(2,0)` to `(4,1)` while keeping slot `1`:

```text
before: Rooms/Room_2_0_01/
after:  Rooms/Room_4_1_01/
```

The exporter finds the old folder by stable `room_id`, moves it inside a staged
transaction, writes the new coordinate and slot, validates the staged package,
and then swaps the completed package into place. Existing floor, enemy, prop,
decor and encounter sidecars move with the room.

The exporter never silently adopts another room's old folder. If the desired
path is owned by a deleted/orphaned room, or a `room.json` is malformed, export is
blocked and the destination is left unchanged.

## `level.json`

```json
{
  "schema_version": 2,
  "level_id": "level.three-room-starter",
  "authoring_state": "validated-authoring",
  "milestone_scope": "track-a-phase-1-editor-foundation",
  "runtime_import_status": "not-connected",
  "room_ids": [
    "room.starter",
    "room.right-01",
    "room.right-02"
  ],
  "rooms": [
    {
      "room_id": "room.starter",
      "grid_position": [0, 0],
      "slot": 1,
      "folder": "Room_0_0_01"
    },
    {
      "room_id": "room.right-01",
      "grid_position": [1, 0],
      "slot": 1,
      "folder": "Room_1_0_01"
    },
    {
      "room_id": "room.right-02",
      "grid_position": [2, 0],
      "slot": 1,
      "folder": "Room_2_0_01"
    }
  ]
}
```

## `map.json`

```json
{
  "schema_version": 2,
  "nodes": [
    {
      "room_id": "room.starter",
      "grid_position": [0, 0],
      "slot": 1,
      "label": "Starter Room",
      "visible_on_map": true
    },
    {
      "room_id": "room.right-01",
      "grid_position": [1, 0],
      "slot": 1,
      "label": "Room 1,0",
      "visible_on_map": true
    },
    {
      "room_id": "room.right-02",
      "grid_position": [2, 0],
      "slot": 1,
      "label": "Room 2,0",
      "visible_on_map": true
    }
  ],
  "connections": [
    {
      "connection_id": "connection.starter-right-01",
      "from": {
        "room_id": "room.starter",
        "door_id": "door.starter-east"
      },
      "to": {
        "room_id": "room.right-01",
        "door_id": "door.right-01-west"
      },
      "travel_policy": "Bidirectional"
    },
    {
      "connection_id": "connection.right-01-right-02",
      "from": {
        "room_id": "room.right-01",
        "door_id": "door.right-01-east"
      },
      "to": {
        "room_id": "room.right-02",
        "door_id": "door.right-02-west"
      },
      "travel_policy": "Bidirectional"
    }
  ]
}
```

## Room metadata

`Rooms/Room_1_0_01/room.json`:

```json
{
  "schema_version": 2,
  "room_id": "room.right-01",
  "display_name": "",
  "automatic_label": "Room 1,0",
  "grid_position": [1, 0],
  "slot": 1,
  "footprint_cells": [1, 1],
  "visible_on_map": true
}
```

Its `doors.json` contains two independent stable endpoints:

```json
{
  "schema_version": 2,
  "room_id": "room.right-01",
  "doors": [
    {
      "door_id": "door.right-01-east",
      "side": "East",
      "placement_mode": "EdgeManaged",
      "edge_offset": 0.5,
      "fixed_local_position": [0, 0],
      "current_local_position": [10, 0],
      "auto_face_connection": true,
      "traversable": true,
      "visible_on_map": true
    },
    {
      "door_id": "door.right-01-west",
      "side": "West",
      "placement_mode": "EdgeManaged",
      "edge_offset": 0.5,
      "fixed_local_position": [0, 0],
      "current_local_position": [-10, 0],
      "auto_face_connection": true,
      "traversable": true,
      "visible_on_map": true
    }
  ]
}
```

## Initial sidecars

Sidecars are created only when missing and are preserved on later exports. The
encounter scaffold uses the agreed default rather than a generic `items` list:

```json
{
  "schema_version": 2,
  "room": "room.right-01",
  "completion": "all-enemies",
  "optional_enemy_ids": [],
  "door_rules": []
}
```

The Phase 1 sidecars are authoring placeholders. They are deliberately not
advertised as the current `RoomContentJsonImporterV1` document package.

## Graph interpretation

```text
[Starter Room]──[Room 1,0]──[Room 2,0]
```

Coordinates become map-node positions and stable endpoint links become map
lines. Visit/completion presentation can be added later without changing room or
door identities.
