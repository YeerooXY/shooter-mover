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
room ID + door ID ↔ room ID + door ID
```

Coordinates, slots, labels and folder names are validated authoring metadata. They never become runtime identity.

`room.json` contains room-local `runtime_bounds` and an optional `player_start`. Only the configured start room may supply the initial player start.

`doors.json` contains the exact stable door ID, side, traversability, runtime presentation object and `current_local_position`. The playable exporter computes this position with:

```csharp
owningRoom.transform.InverseTransformPoint(door.transform.position)
```

It therefore remains room-local even when the endpoint GameObject sits below helper transforms.

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
or semantically empty {}
→ completion = all-enemies
→ optional_enemy_ids = []
→ door_rules = []
```

The compiler then supplies deterministic default door rules:

- forward/progression and final-exit doors: `room-complete`;
- return/backtracking doors: `always`.

A no-enemy room therefore satisfies `all-enemies` immediately through the existing room runtime.

Malformed JSON is always an import error. A partially authored encounter is also validated rather than treated as empty.

## Validation gate

A playable compile rejects:

- unsupported schema versions;
- unknown room or door references;
- duplicate room, door or connection stable IDs;
- duplicate coordinate+slot room folders;
- unsafe or mismatched folder names;
- malformed required sidecars;
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
3. Use **Tools → Shooter Mover → Level Design → Export Compiler-Ready Grid V2 Package...**.
4. Edit tracked room sidecars as required for enemies, props, decor and encounter behavior.
5. Use **Compile Grid V2 Folder...**, or **Compile Tracked Combat Loop Grid V2** for the checked-in sample.
6. The compiler writes generated JSON TextAssets and creates/updates one `JsonRoomContentDefinition2D` asset automatically. No per-room TextAsset assignment is required.
7. Enter the registered level through normal Level Selection. Runtime loading remains `Resources.Load<JsonRoomContentDefinition2D>` and uses the existing gameplay scene.

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
