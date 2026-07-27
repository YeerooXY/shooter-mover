# Level Grid Editor Playable Workflow

## Purpose

The Level Grid Editor now connects scene-authoritative Grid V2 authoring to the existing production room runtime without introducing another topology, compiler, importer, catalogue, or gameplay bootstrap.

The normal designer route is:

```text
open level-authoring scene
→ select the exact LevelDesignSceneAuthoringRoot2D
→ arrange rooms, doors and links
→ configure Playable Level metadata
→ Validate Playable
→ Build
→ open production Level Selection
→ choose the exact registered level
```

Ordinary topology editing and draft saving remain available while playable metadata is incomplete. Production Build and Play remain blocked until every required gate passes.

## Authoritative models

### Topology

The scene hierarchy is the only interactive topology authority:

```text
LevelDesignSceneAuthoringRoot2D
├── LevelRoomAuthoring2D
├── LevelDoorEndpointAuthoring2D
└── LevelDoorLinkAuthoring2D
```

The Level Grid Editor is a projection and mutation surface over those components. It does not own a separate graph or ScriptableObject topology.

### Playable metadata

`LevelGridPlayableMetadataV2` on the selected root is authoritative for:

- exact start room reference;
- player start room-local position;
- player start rotation;
- exact final-exit room reference;
- exact final-exit door reference;
- runtime door object ID.

No room is inferred from coordinates, labels, hierarchy order, folder names, or connection direction. Adding the component leaves start and final selections empty.

### Export and compilation

- `LevelGridV2PlayableExporter.Export(...)` owns playable source export.
- `LevelGridV2AssetCompiler.CompileToAsset(...)` owns compiled TextAsset and `JsonRoomContentDefinition2D` publication.
- `RoomContentJsonImporterV1` remains the retained runtime compatibility gate.
- `JsonRoomContentDefinition2D` remains the player-build runtime content authority.

The editor window calls these façades directly. It does not invoke menu-item strings or duplicate filesystem writes.

### Production play

Production gameplay remains:

```text
Level Selection
→ exact selected stable level ID
→ ProductionPlayableLevelCatalogV1
→ exact RoomContentResourcePath
→ Resources.Load<JsonRoomContentDefinition2D>
→ existing room importer and runtime composition
→ shared PlayableLevel scene
```

Direct editor-only graph injection is not supported.

## Playable Level panel

The existing `LevelGridEditorWindowV2` contains a dedicated **Playable Level** pane.

When metadata is absent, the pane displays:

```text
Playable metadata is not configured.
```

**Add Playable Metadata** is undoable and does not choose arbitrary rooms.

When metadata exists, the pane provides:

- Start room;
- Player start local position;
- Player start rotation;
- Final-exit room;
- Final-exit door;
- Runtime door object ID.

Room choices are restricted to the active root. Final-door choices are restricted to traversable doors owned by the selected final room. Changing the final room clears an incompatible final-door reference. Deleting referenced objects leaves missing Unity references and visible validation failures; no replacement is selected.

Explicit convenience actions are available:

- **Use selected room as start**;
- **Use selected room as final room**;
- **Use selected door as final exit**.

Every metadata mutation uses grouped Unity Undo, marks the scene dirty, and refreshes the canonical live-validation route.

## Build destinations

Destinations are deterministic projections of the stable `level_id`.

The tracked Combat Loop level keeps its accepted paths:

```text
Source:
Assets/ShooterMover/Content/Definitions/Missions/Rooms/GridV2/CombatLoopTest

Generated:
Assets/ShooterMover/Content/Generated/Missions/Rooms/GridV2/CombatLoopTest

Compiled:
Assets/ShooterMover/Resources/ProductionLevels/CombatLoopTestRoomContent.asset

Resource:
ProductionLevels/CombatLoopTestRoomContent
```

Generic levels use collision-resistant stable-ID-derived paths. They cannot resolve to the tracked Combat Loop destinations. Output paths are not identity authorities.

Before export or compilation:

- an existing source package must belong to the same `level_id`;
- an existing compiled asset must reference TextAssets below the expected generated folder;
- a wrong-type or wrong-owner destination is rejected before mutation.

Project-critical destinations are not stored only in `EditorPrefs`.

## Status meanings

The panel displays separate status for:

- Authoring validation;
- Playable metadata validation;
- Export package;
- Compiled asset;
- Production catalogue;
- Play readiness.

### Authoring and metadata

- **Not configured** — required metadata or root is absent.
- **Invalid** — validation failed.
- **Valid** — the specific gate passes.

### Export

- **Valid but not exported** — production authoring may be valid, but no source package exists.
- **Exported but stale** — scene semantics, metadata, compiler schema, or source JSON changed.
- **Export current** — provenance, source JSON, compiler validation, and level identity agree.

### Compiled asset

- **Not configured** — no compiled asset exists.
- **Compiled but stale** — the asset imports, but references a different immutable source version.
- **Compiled current** — the asset references the exact content-addressed version of the current export.

### Catalogue and play

- **Not registered** — no exact stable-ID production catalogue entry exists.
- **Registered** — exact level ID and exact Resource path agree.
- **Ready to play** — all production gates pass.

Status is cached and change-driven. Pan, zoom, selection, and panel scrolling do not change the semantic scene fingerprint. Full compiler evaluation is not performed on every repaint.

## Provenance and freshness

The canonical exporter writes `level-grid.playable.provenance` into its transaction stage before package validation and publication.

The record contains:

- provenance schema;
- compiler schema version;
- stable level ID;
- scene semantic fingerprint;
- exported JSON package fingerprint.

The scene fingerprint covers stable room, door, and connection identities and their compilation-relevant authored values, playable metadata, and room bounds. The source fingerprint covers every JSON sidecar consumed by the compiler.

Compiled freshness reuses the exact content-addressed version algorithm owned by the transactional asset publisher. The status layer does not define a second compiled-package hash.

## Commands

### Validate Playable

Runs without writing files:

```text
foundation validation
→ Grid V2 ProductionPublish validation
→ playable metadata validation
→ destination ownership validation
```

### Export only

Calls `LevelGridV2PlayableExporter.Export(...)` with the configured deterministic source path.

The source package commits only when the validated stage occupies the destination. Pre-commit failure preserves the previous package. Backup cleanup is best-effort after commit.

### Compile only

Requires a current canonical export, then calls `LevelGridV2AssetCompiler.CompileToAsset(...)`.

The accepted publication route:

```text
compile and import-validate in memory
→ publish immutable generated version
→ import every TextAsset
→ save and validate staged runtime asset
→ verify destination has not changed externally
→ atomically switch the authoritative Resource asset
→ import and validate the authoritative asset
→ clean unreferenced output best-effort
```

A pre-commit compile failure preserves the previous playable asset. A post-switch validation failure rolls back and verifies the previous asset. Cleanup failure after commit is reported as a warning, not transaction failure.

### Build

Build is:

```text
Validate
→ Export Playable
→ Compile Asset
```

Export and compilation are distinct transactions. If export commits and compilation fails, the UI reports that exact partial outcome: the new source package remains committed while the previous compiled asset remains playable.

### Select compiled asset

Selects and pings the exact configured `JsonRoomContentDefinition2D`.

### Open production level-selection scene

Direct Play cannot safely fabricate the selected character and immutable route context owned by production Level Selection. The editor therefore opens the real production Level Selection scene only after all readiness checks pass. The designer then chooses the exact registered level through the normal application route.

The command never selects the first catalogue entry and never falls back to the Combat Loop.

## Catalogue registration

The production catalogue is currently code-authored in:

```text
Assets/ShooterMover/Content/Definitions/Levels/Selection/
LevelSelectionCatalogDefinitionV1.cs
```

The editor does not rewrite C# source. For an unregistered generic level, use:

- **Open catalogue source**;
- **Copy registration values**;
- **Select compiled asset**.

Play remains blocked until an exact stable-ID entry points to the exact generated Resource path.

## Recovery

- Missing metadata: continue draft editing; configure metadata before Build.
- Deleted start/final references: Undo or assign an exact replacement explicitly.
- Stale source: Export again.
- Stale compiled asset: Compile again.
- Wrong output owner: choose the stable-ID-derived destination; do not overwrite another level.
- Failed export: retry after correcting the validation or filesystem problem; the old package remains authoritative.
- Failed compilation: retry after correcting the import/publication problem; the old runtime asset remains authoritative.
- Cleanup warning: publication is already committed; inspect and remove unreferenced leftovers separately.
- Unregistered level: add the code-authored catalogue entry, then recheck status.

## Current limitations

- Catalogue registration remains manual and code-authored.
- Direct Play from the editor is intentionally unavailable because production entry requires selected-character route context.
- Room-content placement, enemies, props, decor, encounters, campaign progression, Inventory, weapons, and save data remain outside this workflow.
