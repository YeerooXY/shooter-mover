# Level Grid V2 runtime compiler

## Authority boundary

The exported Level Grid V2 folder is the authoring authority:

```text
level.json
+ map.json
+ Rooms/*/room.json
+ Rooms/*/doors.json
+ floor/enemies/props/decor/encounter sidecars
```

The player build does not enumerate or read that folder. The editor compiler converts it into deterministic V1 JSON documents, validates those documents through `RoomContentJsonImporterV1`, and stores them in a build-included `JsonRoomContentDefinition2D` asset.

```text
Level Grid V2 folder
→ LevelGridV2Compiler
→ RoomContentJsonPackageV1
→ RoomContentJsonImporterV1 validation
→ TextAssets + JsonRoomContentDefinition2D
→ existing JsonRoomRuntimeBootstrap2D
→ existing room, encounter, enemy, traversal and completion runtime
```

The V1 importer remains supported for migration and regression coverage. It is the final compatibility gate for compiled V2 content, not the V2 authoring authority.

## Required V2 metadata

`level.json` adds only the runtime facts that cannot be inferred safely:

- `level_id` — authoritative level identity;
- `start_room_id` — exact stable start-room identity;
- `room_ids` and `rooms` — exact room index;
- `final_exit.room_id + final_exit.door_id` — exact terminal endpoint.

`map.json` retains exact endpoint connections:

```text
from: room ID + door ID
↔
to: room ID + door ID
```

For the currently supported bidirectional connection policy, endpoint orientation is also the stable gameplay direction:

- the `from` endpoint compiles as `progression` and defaults to `room-complete`;
- the `to` endpoint compiles as `return` and defaults to `always`.

Moving either room does not reverse these semantics. Designers change progression by changing the stable connection orientation or by authoring an exact door rule—not by moving a room left, right, above or below another room.

Coordinates, slots, labels and folder names are validated authoring metadata. They never become runtime identity or gameplay direction. Every `map.json` node coordinate and slot must match the authoritative room index.

`room.json` contains room-local `runtime_bounds` and an optional `player_start`. Only the configured start room may supply the initial player start.

`doors.json` contains the exact stable door ID, side, traversability, runtime presentation object and `current_local_position`. The playable exporter computes this position with:

```csharp
owningRoom.transform.InverseTransformPoint(door.transform.position)
```

It therefore remains room-local even when the endpoint GameObject sits below helper transforms.

## Stable room-folder migration

Coordinate-derived room folders are storage locations, not ownership. Before playable export writes room metadata, it scans the staged `Rooms/` copy by the `room_id` inside each `room.json` and migrates that exact folder to the room's current coordinate+slot name.

```text
room.moved in Room_1_0_01
→ designer moves room to (4,0)
→ staged folder becomes Room_4_0_01
→ enemies/props/decor/encounter sidecars move with room.moved
```

The migration rejects malformed identity files, duplicate owners and attempted adoption of a folder owned by another room. Folders belonging to deleted rooms are removed from the disposable stage. The staged package must compile and pass the existing V1 importer before it replaces the previous destination.

## Deterministic arrival placement

Every connected destination endpoint receives one generated auxiliary spawn:

```text
arrival-<destination-door-id>
```

The compiler starts from the destination door's room-local position, moves one world unit inward according to the destination side, and clamps the result inside the room bounds with a 0.5-unit safety margin.

This keeps the player:

- inside the owning room;
- off the transition trigger;
- away from the exterior wall;
- independent of hierarchy nesting and room grid coordinates.

Every compiled room link targets the exact generated arrival ID. No target-spawn-kind ambiguity is used.

## Encounter defaults

The compiler applies:

```text
missing encounter.json
or whitespace encounter.json
or exactly empty JSON object {}
→ completion = all-enemies
→ optional_enemy_ids = []
→ door_rules = []
```

The compiler then supplies deterministic default door rules:

- connection `from` endpoints and the final exit: `room-complete`;
- connection `to` endpoints: `always`.

A no-enemy room therefore satisfies `all-enemies` immediately through the existing room runtime.

Any present non-empty encounter must include schema version 2, the owning room, completion, `optional_enemy_ids` and `door_rules`. Malformed or partial encounters, null required arrays, unknown exact door IDs and duplicate exact-door rules are rejected rather than interpreted as defaults.

## Validation gate

A playable compile rejects:

- unsupported schema versions;
- unknown room or door references;
- unknown encounter door references;
- duplicate room, door, placement or connection stable IDs;
- duplicate coordinate+slot room folders;
- unsafe or mismatched folder names;
- disagreement between map-node and room-index coordinates or slots;
- malformed required sidecars or null required arrays;
- malformed or partial non-empty encounters;
- an endpoint used by multiple connections;
- traversable endpoints that are neither connected nor the exact final exit;
- a missing or unknown start room;
- a missing deterministic player start;
- inaccessible indexed rooms;
- a final exit that is missing, non-traversable, connected as a room endpoint, or owned by another room;
- compiled output rejected by the existing V1 importer/object catalogue.

## Unity workflow

1. Add `LevelGridPlayableMetadataV2` to the selected `LevelDesignSceneAuthoringRoot2D`.
2. Assign the exact start room, player start, final-exit room and final-exit door.
3. Orient each bidirectional link from progression endpoint to return endpoint.
4. Use **Tools → Shooter Mover → Level Design → Export Compiler-Ready Grid V2 Package...**.
5. The exporter migrates existing room folders by stable `room_id`, writes into a staged copy, and compiles/import-validates that stage before replacing the destination.
6. Edit tracked room sidecars as required for enemies, props, decor and encounter behavior.
7. Use **Compile Grid V2 Folder...**, or **Compile Tracked Combat Loop Grid V2** for the checked-in sample.
8. The compiler writes generated JSON TextAssets and creates/updates one `JsonRoomContentDefinition2D` asset automatically. No per-room TextAsset assignment is required.
9. Enter the registered level through normal Level Selection. Runtime loading remains `Resources.Load<JsonRoomContentDefinition2D>` and uses the existing gameplay scene.

## Tracked playable package

The repository includes:

```text
Assets/ShooterMover/Content/Definitions/Missions/Rooms/GridV2/CombatLoopTest/
```

Its route is:

```text
STARTER ROOM (0 enemies)
→ SINGLE CONTACT (1 Mobile Blaster Droid)
→ CROSSFIRE (2 Mobile Blaster Droids)
→ final exit after room completion
```

The generated build content is tracked below:

```text
Assets/ShooterMover/Content/Generated/Missions/Rooms/GridV2/CombatLoopTest/
Assets/ShooterMover/Resources/ProductionLevels/CombatLoopTestRoomContent.asset
```
