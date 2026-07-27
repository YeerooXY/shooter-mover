# Level Grid Authoring V2

## Status

Track A adds the stable room-grid and door-endpoint authoring workflow on top of
the existing Level Design Foundation.

The central rule is that **identity, placement, presentation, and connectivity
are separate concerns**:

- moving a room changes its grid position, not its identity;
- moving or re-placing a door changes placement metadata, not its identity;
- a connection stores exact `room ID + door ID` endpoint references;
- folder names, GameObject names, coordinates, labels, and hierarchy paths are
  never persistent identity.

## No mandatory room naming

Designers are **not required to manually name every room**.

`LevelRoomAuthoring2D.displayName` is optional. When it is empty, the editor and
map export use an automatic coordinate label such as:

```text
Room 50,51
```

The generated room folder also requires no authored name:

```text
Room_50_51_01
```

That folder name is only a readable export path. The stable `room_id` inside
`room.json` remains authoritative and does not change when the room moves or the
folder is regenerated.

Meaningful names may still be supplied for unusual rooms, boss arenas, hubs, or
other places where a human label is genuinely useful.

## Exported level folder

The draft exporter and production publisher create:

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

`level.json`, `map.json`, `room.json`, and `doors.json` are regenerated from the
current authoring graph.

The five room-content sidecars are scaffolded only when missing. Existing
`floor.json`, `enemies.json`, `props.json`, `decor.json`, and `encounter.json`
files are preserved so a grid export cannot silently erase separately authored
room content.

## Rooms

The existing `LevelRoomAuthoring2D` remains the room authority and stores:

- canonical stable room ID;
- editable grid coordinate;
- cell size and footprint;
- alignment and bounds;
- map position/visibility metadata;
- optional display name.

Changing the transform, hierarchy position, GameObject name, display name, or
grid coordinate does not change `roomId`.

## Door endpoints

`LevelDoorEndpointAuthoring2D` represents one physical endpoint owned by one
room.

Each endpoint stores:

- canonical stable door ID;
- owning room reference;
- side: north, east, south, or west;
- placement mode;
- traversable state;
- map visibility.

A room may have any number of doors on the same side. Door side and offset are
placement metadata, not identity.

### Edge-managed doors

An edge-managed door stores a normalized offset from `0` to `1` along the
selected room edge. **Snap Door To Placement** recomputes its local position
from the room bounds.

### Fixed doors

A fixed door stores an explicit local position. Resizing or redistributing room
edges does not automatically move it.

Changing between edge-managed and fixed placement does not change the door ID.

## Connections

`LevelDoorLinkAuthoring2D` represents one graph connection. It stores:

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

Coordinates and labels are never connection keys. A room can therefore move
without invalidating its connections.

Each door endpoint may participate in at most one connection.

## Problems panel

Open:

```text
Tools > Shooter Mover > Level Design > Open Grid Problems
```

The panel is non-modal and refreshes after hierarchy edits and undo/redo. It
shows:

- invalid or duplicated door IDs;
- invalid or duplicated connection IDs;
- doors without owning rooms;
- missing room/door endpoints;
- endpoint/room mismatches;
- self-connections;
- door endpoints used by multiple connections;
- unconnected traversable doors.

Each problem has a **Select** action that focuses the relevant room, door, or
connection.

Unconnected traversable endpoints are orange during draft validation and red
after production validation.

## Draft save and production publish

Two validation purposes are intentionally different.

### Draft

```text
Tools > Shooter Mover > Level Design > Validate Grid Draft
```

Unconnected traversable doors are warnings. Draft work remains saveable even
when the graph is incomplete.

### Production publish

```text
Tools > Shooter Mover > Level Design > Publish Grid V2 Production Folder...
```

Every traversable door must resolve to exactly one connection. An unresolved
traversable endpoint becomes an error and publishing is blocked. The Problems
panel opens instead of presenting a repetitive modal confirmation.

A deliberately sealed, decorative, or disabled endpoint must be marked
non-traversable and does not block publishing.

## Room deletion

Use:

```text
Tools > Shooter Mover > Level Design > Delete Selected Room (Undoable)
```

The command performs one atomic Unity Undo transaction:

```text
Delete room
→ room and its owned door endpoints disappear
→ attached connection records are removed
→ connection lines disappear
→ neighbouring door endpoints remain and become visibly unconnected
→ Problems panel lists those endpoints
→ Scene view shows a small Ctrl+Z undo notification
```

Ordinary room deletion has no confirmation dialog. A dialog appears only when
the room exceeds the explicit unusual-destruction threshold: more than 100
objects or more than 8 attached connections. The operation remains undoable.

## JSON export commands

Draft export:

```text
Tools > Shooter Mover > Level Design > Export Grid V2 Draft Folder...
```

Production publish:

```text
Tools > Shooter Mover > Level Design > Publish Grid V2 Production Folder...
```

Room directories are ordered deterministically by grid coordinate and stable ID.
Manual room names are not used to create paths.

## Robokill-style map foundation

The generated `map.json` directly supports the later gameplay map:

| Authoring data | Gameplay-map use |
|---|---|
| room grid coordinate | node position |
| stable room ID | persistent node identity |
| door connection | line between nodes |
| optional/automatic room label | editor or map presentation |
| map visibility | initial presentation rule |
| future visit state | discovered-node presentation |
| future completion state | cleared/completed-node presentation |

Visit and completion state remain runtime/profile data and are deliberately not
stored as mutable authoring identity.

## Focused tests

Run the existing Level Design Foundation EditMode filter; V2 tests share the
same namespace:

```powershell
$Unity = "C:\Program Files\Unity\Hub\Editor\6000.3.19f1\Editor\Unity.exe"
& $Unity -batchmode -nographics -projectPath "$PWD" `
  -runTests -testPlatform EditMode `
  -testFilter "ShooterMover.Tests.EditMode.LevelDesign.Foundation" `
  -testResults "artifacts/test-results/LEVEL-GRID-V2-EditMode.xml" `
  -logFile "artifacts/logs/LEVEL-GRID-V2-EditMode.log" -quit
```

A passing claim requires the generated XML to report zero failures.
