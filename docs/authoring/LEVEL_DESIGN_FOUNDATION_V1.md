# Level Design Foundation v1

## Status

`LEVELDES-001` provides a reusable, scene-independent authoring layer for rooms,
doors, player/enemy/prop/reward sockets, pickups, entries/exits, and void
regions.

Launch SHA: `45b276a8c415508ac8c0ddd8283234a6bbb2948e`

This package is an authoring and validation foundation only. It does not own
combat, room graph truth, transitions, map state, reward generation, pickup
claims, enemy behavior, destructible-prop behavior, scene loading, or
`Stage1VisibleSlice`.

## Package layout

- `Runtime/UnityAdapters/Authoring/LevelDesign/`
  - stable authored metadata and bounded hierarchy collection;
  - room, placement, door-connection, and void-region components;
  - deterministic validation records and validation results.
- `ContentPackages/LevelDesign/Foundation/`
  - reusable authoring prefabs;
  - `ConfiguredDoorAuthoring2D`, a thin composition seam over OBJ-001 and
    DOOR-001;
  - optional prefab palette asset type.
- `Editor/LevelDesign/Foundation/`
  - inspector validation;
  - scene gizmos and grid snapping;
  - open-scene validation menu.
- `Tests/EditMode/LevelDesign/Foundation/`
  - validation, identity, prefab composition, source-intake, and forbidden
    dependency checks.

## Authoring hierarchy

Create or drag `LevelDesignSceneRoot.prefab`, then place authored objects below
it. Validation is bounded to descendants of that root. Runtime authoring code
never scans unrelated scenes or uses a global registry.

```text
LevelDesignSceneRoot
├── RoomAnchor (room.alpha)
│   ├── PlayerSpawn
│   ├── EnemySpawn
│   ├── PropPlacement
│   ├── PickupSpawn / RewardSocket
│   └── VoidRegion
├── RoomAnchor (room.beta)
└── ConfiguredDoor
```

The root is only an authoring scope. Future ROOM/MAP/transition tasks may consume
its read-only records but remain the owners of runtime graph and mission truth.

## Stable authored IDs

Every persistent authoring component stores an explicit canonical `StableId`:

- level: `level.prototype-one`
- room: `room.alpha`
- placement: `spawn.alpha-enemy-01`, `prop.alpha-crate-01`
- socket: `socket.alpha-enemy-01`
- door: `door.alpha-beta`
- void: `void.alpha-south`

IDs are not derived from GameObject names, hierarchy paths, transforms, prefab
paths, sibling order, or Unity instance IDs. Normal duplication deliberately
duplicates the ID and produces an editor error until a designer invokes
**Assign New Stable ID** from the component context menu.

## Room metadata

`LevelRoomAuthoring2D` stores:

- room StableId;
- grid coordinate;
- cell size;
- room footprint in cells;
- origin/center/custom alignment;
- explicit room-bounds `Collider2D`;
- sorting order;
- map coordinate and visibility.

Use **Snap Room To Authored Grid** after changing grid metadata. Room overlap is
reported from explicit collider bounds. Touching room edges are allowed.

## Placement and spawn sockets

`LevelPlacementAuthoring2D` covers:

- player spawns;
- enemy spawns;
- prop placements;
- pickup spawns;
- reward sockets;
- entries and exits.

Each placement stores its own StableId and socket StableId, room reference,
local grid coordinate, existing definition/profile reference, existing prefab
reference, presentation root, collision policy, sorting, map visibility, reward
override ID, and restart policy.

The component is intentionally reference-only:

- enemy sockets point at existing enemy definitions and prefabs;
- prop placements point at existing PROP-001 definitions/prefabs;
- pickup and reward sockets point at existing PICK/SRC reward profiles and
  pickup prefabs;
- it does not instantiate, damage, reward, collect, destroy, or reset those
  packages.

## Doors

Drag `ConfiguredDoor.prefab` and configure:

1. OBJ-001 `PlacedObjectAuthoring2D`;
2. DOOR-001 `DoorController2D`;
3. `ConfiguredDoorAuthoring2D`;
4. `LevelDoorConnectionAuthoring2D`;
5. two distinct room references;
6. source/destination socket StableIds and grid edges;
7. distinct closed/open presentation roots;
8. one or more closed-state colliders.

`ConfiguredDoorAuthoring2D` implements `ILevelDoorPackageAdapter` so the
foundation can validate package composition without taking door authority from
DOOR-001. Its preview commands only toggle authored presentation/collision in
edit mode. Runtime opening, conditions, restart participation, and traversal
authorization remain in `DoorController2D`.

## Supplied open-door art

`Assets/ShooterMover/Art/Environment/Doors/UserIntake/door_open.png` is a
byte-identical copy of the exact read-only source:

- source path:
  `source-assets/user-intake/map_items/door_open.png`
- source commit:
  `0b1b654c1fb8cf8208904eb55041fde954cfb560`
- source Git blob:
  `4c0388ff741c23d9e1eb4ff6666d0f4cf669f969`

The target path reuses that exact Git blob rather than re-encoding the image. Its
Unity importer enables alpha transparency, uses a centered full-rect Sprite, and
uses 100 pixels per unit. `PROVENANCE.md` beside the asset records the intake
source. The supplied `ConfiguredDoor.prefab` serializes this Sprite on its
foundation adapter so the exact intake art travels with the reusable door
template.

## Void regions

`LevelVoidRegionAuthoring2D` stores:

- explicit StableId;
- room association;
- explicit trigger `Collider2D`;
- intended effect metadata;
- restart policy.

It is metadata only. Existing VOID/hazard or future integration owners consume
the metadata and decide how player, enemy, projectile, prop, checkpoint, and
respawn authorities are invoked.

## Collision and restart policies

Collision metadata is explicit:

- `None`
- `TriggerOnly`
- `Solid`
- `DoorControlled`

Restart metadata is also explicit:

- `Persistent`
- `ResetProjection`
- `RecreateFromDefinition`

These values document author intent and support validation. They do not bypass
OBJ-001 restart participation or package-specific lifecycle authorities.

## Validation

Use either:

- the **Validate Level Design Foundation** button on the root;
- `Tools > Shooter Mover > Level Design > Validate Selected Foundation`; or
- `Tools > Shooter Mover > Level Design > Validate Open Foundations`.

Validation reports:

- malformed and duplicate authored IDs;
- missing room references;
- missing definition/profile/prefab references;
- missing presentation or colliders;
- invalid room grid metadata;
- overlapping room bounds;
- overlapping solid placements;
- invalid or same-room door links;
- non-adjacent door grid edges;
- missing DOOR-001/OBJ-001 composition;
- missing closed/open door presentation or collision;
- malformed, duplicate, or missing socket references and reward overrides;
- player/enemy spawns inside void regions;
- invalid void colliders.

Validation is fail-closed and does not repair IDs automatically.

## Focused tests

```powershell
$Unity = "C:\Program Files\Unity\Hub\Editor\6000.3.19f1\Editor\Unity.exe"
New-Item -ItemType Directory -Force artifacts/test-results, artifacts/logs | Out-Null
& $Unity -batchmode -nographics -projectPath "$PWD" `
  -runTests -testPlatform EditMode `
  -testFilter "ShooterMover.Tests.EditMode.LevelDesign.Foundation" `
  -testResults "artifacts/test-results/LEVELDES-001-EditMode.xml" `
  -logFile "artifacts/logs/LEVELDES-001-EditMode.log" -quit
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
```

A passing claim requires the XML result with zero failures. Authored tests,
static review, and mergeability are not Unity execution proof.

## Non-goals

This foundation does not provide:

- a complete map or graph editor;
- procedural generation;
- pathfinding;
- combat or enemy logic;
- reward generation or transaction authority;
- pickup collection;
- room transition or scene loading;
- production map assets;
- automatic ID repair;
- Stage 1 scene/controller edits.
