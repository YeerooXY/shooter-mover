# Level System Audit and Forward Plan

**Status:** Planning document  
**Repository basis:** `main` after merged PR #340 (`425984df287bf2f72a616d7693f50f45b3a67d22`)  
**Scope:** Level creation, room/map representation, connected doors, visual editor, JSON pipeline, runtime publication, maintainability, and recommended sequencing.

## Executive summary

The level system has a strong architectural core:

- the Unity scene hierarchy is the interactive topology authority;
- rooms, doors, connections, and levels use stable IDs;
- connections reference exact room-and-door endpoints;
- the exported Level Grid V2 JSON package is deterministic and compiler-validated;
- the pure V2 compiler adapts the package into the retained V1 room runtime;
- compiled Unity assets are published through an immutable, transactional, rollback-capable path;
- production play uses the real level-selection route rather than editor-only injection or fallback selection.

The system is therefore heading in the right direction. The immediate risk is not the JSON format or the connection model. The risk is that several older Phase-1 tools and duplicate mutation routes remain active beside the newer canonical workflow, while the visual editor has accumulated too many responsibilities.

The recommended strategy is to stabilize and consolidate first, then improve the designer workflow, then expand authoring and runtime map capabilities.

## Current representation

```text
Unity scene authoring graph
    ↓ playable export
Level Grid V2 JSON package
    ↓ pure compiler
Room Content V1 JSON package
    ↓ transactional Unity publication
JsonRoomContentDefinition2D + immutable TextAssets
    ↓ production catalogue
Level Selection → PlayableLevel
```

### Interactive authority

The scene hierarchy owns topology:

```text
LevelDesignSceneAuthoringRoot2D
├── LevelRoomAuthoring2D
│   └── LevelDoorEndpointAuthoring2D
├── LevelRoomAuthoring2D
│   └── LevelDoorEndpointAuthoring2D
└── LevelDoorLinkAuthoring2D
```

`LevelGridPlayableMetadataV2` owns the exact start room, player start transform, and final room-plus-door endpoint.

Coordinates, labels, object names, hierarchy order, and folder paths are projections rather than identity. Moving or renaming a room therefore does not invalidate its stable relationships.

### JSON package

The compiler-ready package is split by responsibility:

```text
LevelFolder/
├── level.json
├── map.json
└── Rooms/
    └── Room_<x>_<y>_<slot>/
        ├── room.json
        ├── doors.json
        ├── floor.json
        ├── enemies.json
        ├── props.json
        ├── decor.json
        └── encounter.json
```

- `level.json` owns the stable level ID, room index, start room, and final exit.
- `map.json` owns nodes and exact endpoint-to-endpoint connections.
- `room.json` owns room-local runtime bounds and optional player start.
- `doors.json` owns room-local door endpoints.
- floor, enemy, prop, decor, and encounter sidecars own room content and are preserved during topology re-export.

JSON is a good choice at this boundary because it is reviewable, deterministic, testable outside Unity, and suitable for future external tools. It should remain an exported/build source rather than becoming a second interactive topology authority.

## Strong areas

### Stable identity and exact endpoint connections

Connections use exact `room_id + door_id` endpoints instead of relying on relative room position. Each traversable endpoint can participate in at most one connection. The compiler rejects unknown endpoints, self-connections, reused endpoints, inaccessible rooms, unresolved traversable doors, and invalid final exits.

This is a robust basis for rooms with multiple entrances, deterministic arrivals, room movement, and future special door behaviour.

### Strict compiler boundary

The V2 compiler checks duplicated IDs, map/index disagreement, encounter selectors, optional enemy references, start/final requirements, finite values, reachability, and runtime compatibility. It fails closed instead of normalizing malformed authored data into something playable.

### Transactional publication

The compiled-asset publisher uses:

- in-memory compile and importer validation;
- immutable content-addressed generated versions;
- staged TextAsset and ScriptableObject import;
- a late destination snapshot check;
- atomic authoritative asset replacement;
- exact rollback and rollback validation;
- reference-aware cleanup;
- best-effort post-commit cleanup.

This is the strongest part of the implementation and should remain the canonical publication path.

### Production routing

The editor does not fabricate character or route context. It verifies exact readiness and opens the real production Level Selection scene. The catalogue must resolve the exact stable level ID to the exact Resource path.

## Main findings and risks

### 1. Duplicate mutation routes remain active

Some older menu commands directly create, connect, reflow, keep, validate, or delete authoring components instead of delegating to the newer canonical editor operations.

The most important example is room deletion: the visual editor uses the newer safe operation, while the older menu command has a separate relationship scan and can behave differently on malformed links. Safety should not depend on which UI surface the designer uses.

All menus, inspectors, windows, and starter tools should delegate to one command layer.

### 2. The Phase-1 exporter is still exposed

The old draft/validated-authoring exporter remains beside the compiler-ready playable exporter. It contains a second implementation of JSON DTOs, staging, migration, ownership checks, and rollback, and still writes Phase-1 `runtime_import_status: not-connected` metadata.

This route should be removed or quarantined as an explicitly diagnostic legacy tool. Production authoring should have one exporter.

### 3. The visual editor has become a large responsibility cluster

`LevelGridEditorWindowV2` is split across partial files, but collectively owns window state, rendering, geometry, input, selection, diagnostics, inspectors, playable metadata, build status, and build actions.

Partial files improve navigation but do not create ownership boundaries. Adding room-content editing directly to the current window would accelerate responsibility growth.

### 4. New-level creation is incomplete

There is no first-class level wizard. Current tools assume an existing authoring root and scene setup. The three-room starter creates topology beneath a selected root but does not create the complete level lifecycle, metadata, output registration, or final-exit setup.

### 5. Map authoring exists, but a player-facing runtime map does not

The source package contains labels, visibility flags, room placement, and exact connections. Several map-only fields are not carried through the V2-to-V1 runtime conversion. The current graph is therefore a good source for a future map, but it is not itself a complete minimap or discovery system.

### 6. Safety claims need Unity execution evidence

The repository contains strong tests and verification plans, but the merged integration record states that Unity compilation, Editor tests, relevant PlayMode tests, manual acceptance, and the documented responsiveness fixture were not executed in the connected audit environment.

The implementation should be described as safe by design until those behaviours are observed in the target Unity environment.

## Five-step forward plan

## Step 1 — Consolidate and prove the existing foundation

**Goal:** Establish one safe route for every level-system mutation and publication operation.

Work:

- remove or redirect duplicate room, door, connection, deletion, reflow, validation, and export menu paths;
- retire the Phase-1 production exporter and stale `not-connected` UI/documentation;
- add a late destination snapshot guard to playable source export;
- add source guards proving UI commands delegate to canonical operations;
- execute Unity import, compilation, Editor tests, EditMode tests, relevant PlayMode tests, rollback tests, manual traversal, Undo/Redo, reload, and restart checks.

**Exit criteria:** One canonical mutation/export/build route, no stale Phase-1 production surface, and recorded Unity evidence.

**Parallelism:** Treat this as the only active implementation stream touching level editor infrastructure, exporters, or publication. Unrelated gameplay systems can continue in parallel.

## Step 2 — Split the editor into bounded services

**Goal:** Prevent the editor from becoming the permanent integration god object.

Introduce a shared indexed topology snapshot:

```text
LevelTopologySnapshot
├── RoomsById
├── DoorsById
├── LinksById
├── DoorsByRoom
├── ConnectionByDoor
└── ProblemsByObject
```

Split responsibilities into focused services or presenters:

- room commands;
- door commands;
- connection commands;
- topology deletion commands;
- selection controller;
- canvas renderer and interaction controller;
- Problems presenter;
- playable panel presenter;
- view-state store.

**Exit criteria:** The EditorWindow primarily composes UI; graph writes and relationship lookup live behind reusable tested boundaries.

**Parallelism:** Mostly serial with Step 1. Documentation and design for Step 3 can proceed, but avoid simultaneous implementation in the same editor files.

## Step 3 — Add a complete level lifecycle workflow

**Goal:** Make creation and registration usable without repository archaeology.

Add a **Create Level** workflow that:

- creates or selects an authoring scene;
- creates one level root with a unique stable ID;
- adds playable metadata;
- creates an initial bounded room;
- explicitly assigns the start room and player start;
- leaves incomplete final-exit choices visible rather than guessing;
- resolves deterministic source/generated/runtime destinations;
- opens the visual editor focused on the new root.

Replace manual C# catalogue editing with a data-driven catalogue asset or generated registration definition. Preserve exact stable-ID and Resource-path validation.

**Exit criteria:** A designer can create, register, build, and reach a minimal level through production Level Selection without manually editing C# or JSON topology files.

**Parallelism:** Can run beside an unrelated gameplay feature after Step 1. It should not run concurrently with another branch making large changes to the level editor or production catalogue.

## Step 4 — Add room-content authoring

**Goal:** Turn the level editor from a topology editor into a practical gameplay-content workflow.

Add room-focused authoring for:

- floor tiles;
- enemy placements and levels;
- props;
- background and foreground decor;
- encounter completion;
- optional enemies;
- door rules.

The first implementation should edit the existing room sidecar model rather than introduce another independent content authority. JSON Schema, field-level diagnostics, and room-linked validation can provide an intermediate improvement before a full visual placement surface is complete.

**Exit criteria:** A designer can create a small playable multi-room level with enemies and encounter gating without hand-editing sidecar JSON.

**Parallelism:** Can be divided by sidecar domain only after the shared content contract and mutation boundary are fixed. Avoid several branches independently inventing serialization or placement authority.

## Step 5 — Build the runtime map and long-term hardening

**Goal:** Convert the authored topology into a deliberate player-facing map and make the pipeline sustainable at scale.

Define a dedicated runtime map model covering:

- map node positions and footprints;
- labels and visibility;
- discovered/completed state;
- connection and lock state projection;
- player, objective, final-exit, vendor, or boss markers.

Also complete long-term hardening:

- checked-in JSON Schemas;
- deterministic schema migrations;
- malformed/fuzz fixtures;
- explicit transaction-leftover inspection and recovery tooling;
- 100-room/300-door/150-connection performance evidence;
- a larger stress fixture to establish the real limit;
- a documented decision on whether V1 remains the permanent runtime intermediate representation.

**Exit criteria:** The map is a real runtime feature, schema evolution is explicit, recovery is user-facing, and performance limits are measured rather than assumed.

**Parallelism:** Runtime map UI can run beside schema/recovery work if the compiled map contract is fixed first. Do not begin multiple implementations before agreeing on that authority.

## Recommended feature concurrency

The repository can support several simultaneous feature efforts, but only when each has a clear authority and file boundary.

### Recommended portfolio limit

Use at most **three active implementation streams**:

1. **One shared/core stream** — architecture, integration, save schema, catalogue, editor infrastructure, or common runtime authority.
2. **Up to two isolated feature streams** — features with distinct ownership and minimal overlap with each other or the core stream.

Only one active branch should make broad changes to any of these shared areas at a time:

- level editor infrastructure;
- room runtime composition;
- production level catalogue/selection;
- inventory authority;
- reward/progression authority;
- save schema;
- shared content catalogues.

### Why not more?

The limiting factor is not Git branch count. It is integration ownership. Several branches can compile independently while still making incompatible assumptions about the same runtime authority. Beyond one core lane plus two isolated feature lanes, review load, merge sequencing, duplicated abstractions, and stale assumptions are likely to cost more than the extra parallelism saves.

### Practical sequencing for the current audits

While Step 1 is active, it is reasonable to continue one or two gameplay features that do not modify level-editor/export/publication code. When choosing those features, prefer combinations that do not both change inventory, rewards, save data, or production scene routing.

Before opening each implementation branch, record:

- the authority it changes;
- files/systems it expects to touch;
- upstream contracts it depends on;
- another active branch it may conflict with;
- the merge order.

This keeps concurrency intentional rather than accidental.

## Recommended decision

Proceed with the level subsystem as a **stabilization lane**, not a broad new-feature lane, until Step 1 is complete. In parallel, run no more than two isolated gameplay feature lanes. After the level foundation is proven and canonical routes are consolidated, start the lifecycle/editor decomposition work before adding both room-content authoring and runtime map functionality.

The current architecture is worth keeping. The next gain comes from reducing competing tools and proving the safety model, not replacing the JSON or endpoint design.
