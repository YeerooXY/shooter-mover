# Level Grid Authoring V2 — Phase 1 Editor Foundation

## Status and scope

This PR implements **Track A Phase 1**:

- stable room-grid authoring;
- stable room-owned door endpoints;
- stable endpoint-to-endpoint connections;
- safe room and door editing;
- non-modal validation;
- transactional authoring-folder export.

It does **not** complete the playable-room cutover. The exported package is not
currently consumed by `RoomContentJsonImporterV1`, `RoomContentBundleV1`, or the
COMBAT LOOP TEST runtime path. Exported `level.json` states:

```json
{
  "milestone_scope": "track-a-phase-1-editor-foundation",
  "runtime_import_status": "not-connected"
}
```

The runtime compiler/import migration belongs in a follow-up PR with its own
playable acceptance evidence.

## Identity and naming

Identity, placement, presentation and connectivity are separate:

- moving a room changes its grid coordinate, not its stable `roomId`;
- moving a door changes placement metadata, not its stable `doorId`;
- connections reference exact `room ID + door ID` endpoints;
- GameObject names, optional labels, folders and hierarchy paths are never
  persistent identity.

Room display names remain optional. An unnamed room automatically appears as:

```text
Room 50,51
```

## Coordinate and slot folder model

Every room stores:

```text
grid = [50, 51]
slot = 1
```

Its deterministic folder is:

```text
Room_50_51_01
```

The suffix is not a global room ordinal. Two draft rooms at the same coordinate
must use distinct slots such as `_01` and `_02`.

When a room moves, the exporter finds the old folder by stable `room_id`, migrates
it to the new coordinate+slot path, and moves all sidecars with it.

## Exported authoring folder

```text
Level folder
├── level.json
├── map.json
└── Rooms/
    └── Room_50_51_01/
        ├── room.json
        ├── doors.json
        ├── floor.json
        ├── enemies.json
        ├── props.json
        ├── decor.json
        └── encounter.json
```

`level.json`, `map.json`, `room.json` and `doors.json` are generated from the
current editor graph. Sidecars are created only when missing and are preserved on
later exports.

The initial encounter sidecar uses the agreed authoring default:

```json
{
  "room": "room.example",
  "completion": "all-enemies",
  "optional_enemy_ids": [],
  "door_rules": []
}
```

These Phase 1 sidecars are authoring placeholders, not the current V1 runtime
import schema.

## Transactional export and folder ownership

Export is staged beside the destination and committed only after staged
validation succeeds.

The exporter blocks instead of writing when:

- the destination is a non-empty unrelated folder;
- `level.json` belongs to another level;
- a room folder has no `room.json`;
- a room identity file is malformed or lacks `room_id`;
- multiple folders claim one room ID;
- a desired coordinate+slot folder is owned by another or orphaned room;
- current rooms have duplicated IDs or coordinate+slot pairs.

It never silently attaches unknown enemies, props, decor or encounter files to a
new room. A failed export leaves the destination unchanged.

## Rooms

`LevelRoomAuthoring2D` stores:

- stable room ID;
- editable grid coordinate;
- explicit per-coordinate folder slot;
- cell size and footprint;
- alignment and collider bounds;
- map metadata;
- optional display name.

## Door endpoints

`LevelDoorEndpointAuthoring2D` represents one room-owned physical endpoint.
Each endpoint stores:

- stable door ID;
- owning room;
- north/east/south/west side;
- edge-managed or fixed placement;
- traversable and map-visible state;
- automatic connection-facing policy.

A room may have multiple doors on any side.

### Edge-managed placement and reflow

An edge-managed door stores a normalized edge offset. When connected rooms move,
auto-facing endpoints reflow toward the connected room. Fixed doors are never
auto-reflowed.

A facing mismatch appears non-modally with:

```text
[Reflow] [Keep]
```

`Keep` disables automatic facing for that endpoint.

### Fixed placement

Fixed doors export their captured local position. Dragging a fixed door in the
Scene view updates the stored fixed position through the live authoring loop.
An explicit command also exists:

```text
Tools > Shooter Mover > Level Design > Capture Selected Door As Fixed
```

## Connections

`LevelDoorLinkAuthoring2D` stores stable endpoint references:

```json
{
  "connection_id": "connection.alpha-beta",
  "from": {
    "room_id": "room.alpha",
    "door_id": "door.alpha-east"
  },
  "to": {
    "room_id": "room.beta",
    "door_id": "door.beta-west"
  }
}
```

Each endpoint may participate in at most one connection. The ordinary creation
command refuses to connect an already connected endpoint.

## Problems panel

Open:

```text
Tools > Shooter Mover > Level Design > Open Grid Problems
```

The panel refreshes after hierarchy changes, Undo/Redo and normal Inspector or
Scene property edits. It reports room identities and slots, door identities and
placement, connection integrity, facing mismatches and unresolved traversable
doors.

Problem selection matches both stable ID and diagnostic hierarchy path, so a
duplicate-ID problem focuses the correct object rather than always selecting the
first duplicate.

## Combined validated-authoring gate

Draft saving remains permissive.

Validated authoring publish requires **both**:

```text
existing Level Design Foundation validation has zero errors
AND
Level Grid V2 graph validation has zero errors
```

The existing foundation gate therefore still rejects invalid level IDs, invalid
or duplicated room IDs, missing room bounds, bad grid metadata, overlaps,
broken placements and voids, and invalid legacy door composition.

V2 additionally rejects invalid room slots, endpoint/link failures, multiple
connection use, unresolved traversable doors, and automatic-facing mismatches.

Command:

```text
Tools > Shooter Mover > Level Design > Publish Grid V2 Validated Authoring Folder...
```

This is a validated **authoring** package, not a playable runtime publish.

## Safe deletion

### Room

```text
Tools > Shooter Mover > Level Design > Delete Selected Room (Undoable)
```

One Undo transaction removes the room and attached links while preserving
neighbouring endpoints as unresolved. Routine deletion is non-modal; only an
unusually large deletion asks for confirmation.

### Door

```text
Tools > Shooter Mover > Level Design > Delete Selected Door (Undoable)
```

One Undo transaction:

```text
delete door
→ delete its attached connection, when present
→ preserve the opposite endpoint
→ mark the opposite endpoint unresolved
→ revalidate and open Problems
```

## Three-room example

```text
Tools > Shooter Mover > Level Design > Create Three-Room Starter Example
```

Creates:

```text
[Starter Room (0,0)/01]──[Room 1,0/01]──[Room 2,0/01]
```

See `LEVEL_GRID_AUTHORING_V2_THREE_ROOM_EXAMPLE.md` for the exact folder and JSON
representation.

## Verification

Focused EditMode coverage includes:

- optional labels and stable movement identity;
- duplicate coordinate+slot detection;
- foundation + graph publish source gate;
- transactional folder migration with sidecar preservation;
- deleted-room folder ownership rejection;
- malformed identity blocking;
- atomic door deletion and Undo;
- fixed-door position capture;
- connection-aware edge reflow.

Unity execution remains required before this draft may be marked ready.
