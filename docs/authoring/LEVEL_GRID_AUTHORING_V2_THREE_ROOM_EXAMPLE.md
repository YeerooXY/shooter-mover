# Level Grid Authoring V2 — Three-Room Starter Example

## Scene authoring command

Select an object below a `LevelDesignSceneAuthoringRoot2D`, then use:

`Tools > Shooter Mover > Level Design > Create Three-Room Starter Example`

The command creates one atomic, undoable authoring group:

```text
Three-Room Starter Example
├── Starter Room                         grid (0,0)
│   └── East Door
├── Room 1,0                             grid (1,0)
│   ├── West Door
│   └── East Door
├── Room 2,0                             grid (2,0)
│   └── West Door
├── Starter to Room 1,0
└── Room 1,0 to Room 2,0
```

Only the first room has an explicit optional display name: `Starter Room`.
The two rooms to its right keep an empty `displayName` and therefore appear with
automatic labels derived from their coordinates.

Each room, door endpoint, and connection receives a generated stable ID. The IDs
below are shortened readable examples of the same structure.

## First exported folder layout

On first export, the rooms are sorted by grid coordinate and receive generated
human-readable folder names:

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
    ├── Room_1_0_02/
    │   ├── room.json
    │   ├── doors.json
    │   ├── floor.json
    │   ├── enemies.json
    │   ├── props.json
    │   ├── decor.json
    │   └── encounter.json
    └── Room_2_0_03/
        ├── room.json
        ├── doors.json
        ├── floor.json
        ├── enemies.json
        ├── props.json
        ├── decor.json
        └── encounter.json
```

The folder names are presentation only. The `room_id` inside each `room.json` is
the authoritative identity.

If a room is later moved, the exporter finds its existing folder by reading the
stable `room_id`. It updates the coordinates in `room.json` without replacing or
losing the separately authored floor, enemy, prop, decor, or encounter sidecars.
The folder therefore does not need to be renamed whenever a room moves.

## `level.json`

```json
{
  "schema_version": 2,
  "level_id": "level.three-room-starter",
  "authoring_state": "production",
  "room_ids": [
    "room.starter",
    "room.right-01",
    "room.right-02"
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
      "label": "Starter Room",
      "visible_on_map": true
    },
    {
      "room_id": "room.right-01",
      "grid_position": [1, 0],
      "label": "Room 1,0",
      "visible_on_map": true
    },
    {
      "room_id": "room.right-02",
      "grid_position": [2, 0],
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

## Starter room

`Rooms/Room_0_0_01/room.json`

```json
{
  "schema_version": 2,
  "room_id": "room.starter",
  "display_name": "Starter Room",
  "automatic_label": "Starter Room",
  "grid_position": [0, 0],
  "footprint_cells": [1, 1],
  "visible_on_map": true
}
```

`Rooms/Room_0_0_01/doors.json`

```json
{
  "schema_version": 2,
  "room_id": "room.starter",
  "doors": [
    {
      "door_id": "door.starter-east",
      "side": "East",
      "placement_mode": "EdgeManaged",
      "edge_offset": 0.5,
      "fixed_local_position": [0, 0],
      "traversable": true,
      "visible_on_map": true
    }
  ]
}
```

## First room to the right

`Rooms/Room_1_0_02/room.json`

```json
{
  "schema_version": 2,
  "room_id": "room.right-01",
  "display_name": "",
  "automatic_label": "Room 1,0",
  "grid_position": [1, 0],
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
      "traversable": true,
      "visible_on_map": true
    },
    {
      "door_id": "door.right-01-west",
      "side": "West",
      "placement_mode": "EdgeManaged",
      "edge_offset": 0.5,
      "fixed_local_position": [0, 0],
      "traversable": true,
      "visible_on_map": true
    }
  ]
}
```

## Second room to the right

`Rooms/Room_2_0_03/room.json`

```json
{
  "schema_version": 2,
  "room_id": "room.right-02",
  "display_name": "",
  "automatic_label": "Room 2,0",
  "grid_position": [2, 0],
  "footprint_cells": [1, 1],
  "visible_on_map": true
}
```

Its `doors.json` contains the west endpoint connected to the middle room.

## Empty sidecars

For every new room, the exporter scaffolds the remaining files only when they do
not already exist. For example:

```json
{
  "schema_version": 2,
  "room_id": "room.starter",
  "content_kind": "enemies",
  "items": []
}
```

The same shape is used initially for `floor.json`, `props.json`, `decor.json`, and
`encounter.json`, with the corresponding `content_kind`. Later exports do not
overwrite those authored sidecars.

## Map interpretation

The example produces this graph:

```text
[Starter Room]──[Room 1,0]──[Room 2,0]
```

The three room coordinates become map-node positions. The two stable endpoint
connections become map lines. Later visit and completion state can change node
presentation without changing the folder structure or connection identities.
