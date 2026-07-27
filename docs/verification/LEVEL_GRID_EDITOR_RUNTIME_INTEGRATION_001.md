# LEVEL-GRID-EDITOR-RUNTIME-INTEGRATION-001

## Branch and dependency

- Requested branch: `agent/level-grid-editor-runtime-integration-001`
- Final merged `main` inspected before dependency selection: `a8a96fad360c57643b907649e1202aa7684dc09e`
- PR #336 merged: `c79853c241133059fd8a8ce73ed1e76ad199e81b`
- PR #337 merged: `a8a96fad360c57643b907649e1202aa7684dc09e`
- Transactional publication candidates inspected: PR #338 and PR #339
- Selected dependency: PR #338 head `6fbb612a775722fa00008e93662553012f2384ad`
- Branch created from that exact #338 head
- Intended PR target: `main`
- Required PR state: draft

PR #338 was selected because it includes the complete transaction surface required by this integration:

- immutable content-addressed generated versions;
- destination snapshot and late external-change guard;
- staged serialized runtime asset validation;
- atomic authoritative `.asset` replacement;
- exact rollback and rollback verification;
- reference-aware old-version retention;
- post-commit best-effort cleanup;
- dedicated Editor tooling assembly and real AssetDatabase failure tests.

PR #339 was not selected because its own status declared the Editor tooling assembly and boundary failure tests unfinished. The inspected cleanup implementation also removed obsolete version folders without first proving that another runtime asset did not reference them.

## Change contract

### Requested behavior

Connect the existing visual Grid V2 editor to the existing playable export, transactional compiled-asset publication, production catalogue verification, and real production level-selection route.

### Authority map

| Concept | Authority |
| --- | --- |
| Topology | `LevelDesignSceneAuthoringRoot2D` hierarchy and room/door/link components |
| Playable metadata | `LevelGridPlayableMetadataV2` on the selected root |
| Live authoring validation | `LevelGridAuthoringV2LiveValidation` and existing validators |
| Playable export | `LevelGridV2PlayableExporter.Export(...)` |
| V2 compilation | pure `LevelGridV2Compiler` |
| Runtime compatibility validation | `RoomContentJsonImporterV1` |
| Generated publication | PR #338 `LevelGridV2AssetCompiler.CompileToAsset(...)` |
| Runtime room content | compiled `JsonRoomContentDefinition2D` |
| Production registration | `ProductionPlayableLevelCatalogV1` |
| Selected level route | `LevelSelectionRouteContextV1` captured by real Level Selection |
| Gameplay entry | existing shared `PlayableLevel` scene and `ProductionPlayableLevelControllerV1` |

### Systems changed

- existing Level Grid Editor window composition and toolbar;
- playable metadata editor operations;
- deterministic build destination projection;
- export provenance and status evaluation;
- callable build façade;
- Editor assembly reference;
- Editor tooling tests;
- authoring and verification documentation.

### Systems intentionally untouched

- topology component schemas;
- pure compiler semantics;
- runtime importer;
- room runtime graph and presentation;
- production catalogue source content;
- production gameplay controller;
- Level Selection controller;
- scenes and prefabs;
- Inventory, weapons, saves, rewards, campaign progression.

### Boundaries

- Editor code remains below `Assets/ShooterMover/Editor` and `UNITY_EDITOR` guarded.
- Runtime assemblies do not reference `UnityEditor`.
- The pure compiler remains Unity-free.
- Status/provenance is a projection; it is not a write authority for topology or metadata.
- The provenance file has no `.json` extension, so the canonical compiler does not ingest it as a room-content document.
- The player build consumes generated TextAssets and `JsonRoomContentDefinition2D`; source authoring folders are not needed.

## Final public integration APIs

### Export

```csharp
LevelGridV2PlayableExporter.Export(root, outputRoot)
```

The menu item and editor workflow both route to this public exporter.

### Compilation and publication

```csharp
LevelGridV2AssetCompiler.CompileToAsset(
    sourceRoot,
    generatedAssetFolder,
    roomContentAssetPath)
```

The window owns no TextAsset or ScriptableObject publication writes.

### Editor extension points

The existing `LevelGridEditorWindowV2` partial-class structure is extended with `LevelGridEditorWindowV2.Playable.cs`. Existing selection, projection, topology operations, live validation, Problems diagnostics, and Undo conventions remain in use.

## Transaction boundaries

### Export transaction

Before commit:

- validate metadata;
- reflow canonical doors;
- validate foundation and production graph;
- validate destination owner;
- copy existing package into stage or create stage;
- write package and provenance;
- compile staged source;
- validate through retained importer.

Commit point:

```text
validated stage moves into the configured source destination
```

Before commit failure:

- stage is removed best-effort;
- previous package is restored when necessary;
- previous package remains authoritative.

After commit:

- backup and metadata cleanup are best-effort;
- cleanup failure does not report the committed export as failed.

### Compiled publication transaction

Before commit:

- compile and import-validate in memory;
- write transaction-owned immutable version stage;
- import and verify every TextAsset;
- publish or validate content-addressed version;
- create, save, import, and validate staged runtime asset;
- verify source version and destination snapshot again.

Commit point:

```text
validated staged .asset atomically replaces the authoritative Resource .asset
and the authoritative asset imports and validates successfully
```

Pre-commit failure:

- old Resource asset and every referenced generated TextAsset remain untouched.

Failure after file replacement:

- previous exact `.asset` bytes are restored;
- restored asset is synchronously imported and runtime-validated;
- rollback failure is surfaced separately.

After commit:

- unreferenced versions, legacy JSON, transaction stages, and markers are cleanup;
- cleanup is best-effort and cannot reverse or falsely fail committed publication.

### Build transaction composition

Export and compilation remain separate transactions.

- failed export: compilation does not start;
- successful export + failed compilation: new export remains committed; old compiled asset remains authoritative;
- successful export + successful compilation: both commit.

## Invariant ledger

- topology has one scene authority;
- playable metadata has one component authority;
- export has one canonical route;
- compilation has one canonical route;
- generated publication remains transactional;
- no editor command bypasses production validation;
- stable identity does not depend on coordinates, names, ordering, or paths;
- generic levels never resolve to tracked Combat Loop destinations;
- a failed compile cannot damage the previously playable asset;
- cleanup failure cannot report committed publication as failed;
- the editor cannot report current when scene semantics or source JSON changed;
- pan, zoom, selection, and scrolling do not affect freshness;
- Play never substitutes another level or fallback asset;
- catalogue registration is not performed by C# source rewriting;
- direct editor play does not invent character or route context;
- multiple scene roots remain isolated by explicit active-root selection.

## Failure-mode matrix

| Case | Expected behavior |
| --- | --- |
| Move room | Stable room ID survives; scene fingerprint changes; export and compile become stale |
| Rename room | Stable identity/connections survive; generated display content changes; status becomes stale |
| Move final room | Exact metadata references survive because they are Unity object references |
| Delete start room | Metadata reference becomes missing; validation fails; no fallback |
| Delete final room | Metadata becomes invalid; no fallback |
| Delete final door | Metadata becomes invalid; no fallback |
| Change final room | Incompatible old final-door reference is cleared in the same Undo operation |
| Change final door ownership | Metadata validation rejects exact room/door mismatch |
| Duplicate room/door/link ID | Existing production validators/compiler fail closed |
| Missing playable metadata | Draft editing remains allowed; Build and Play blocked |
| Wrong source owner | Export rejected before stage publication |
| Wrong compiled owner | Build rejected before publication |
| Malformed source package | Status invalid; compiler/importer reject |
| Stale source sidecar | Source fingerprint differs; status stale or invalid |
| Export failure before commit | Previous source package authoritative |
| Export cleanup failure after commit | Export committed with warning |
| Compile failure before switch | Previous runtime asset authoritative |
| Compile failure after file replacement | Exact previous asset restored and validated |
| Compile cleanup failure after commit | Compile committed with warning |
| Retry after failed export | Canonical stage uses a new transaction ID; destination ownership remains checked |
| Retry after failed compile | Content-addressed version is validated or recreated without duplicate semantics |
| Undo metadata edit | Serialized references and live validation restore |
| Undo deletion | Unity restoration may restore exact serialized metadata reference; covered by Editor test |
| Scene reload | Scene components reconstruct metadata; deterministic paths and provenance reconstruct status |
| External source modification | source snapshot/fingerprint invalidates cached status |
| External destination modification | PR #338 destination snapshot guard rejects authoritative switch |
| Unregistered level | Compile allowed; Play blocked with exact explanation |
| Catalogue points to wrong Resource | Play blocked; no fallback |
| Two scene roots | all actions use only explicit active root |
| Tracked + generic level | stable-ID destination resolver prevents cross-wiring |
| Pan/zoom/selection | semantic fingerprint unchanged; no compile-status invalidation |

## Production play path

A direct Play button cannot safely manufacture production selection context. The real route requires:

```text
selected character graph
+ route payload
+ selected mode
+ exact selected level stable ID
→ LevelSelectionRouteContextV1
→ shared PlayableLevel scene
→ ProductionPlayableLevelCatalogV1.TryResolve(exact ID)
→ exact Resource path
→ Resources.Load<JsonRoomContentDefinition2D>
```

The editor therefore exposes **Open production level-selection scene**. It first verifies production authoring, metadata, current export, current compiled asset, exact stable-ID registration, and exact Resource path. It then opens the real Level Selection scene; the designer selects the exact level through the normal flow.

No first-entry, Combat Loop, or generic fallback is used.

## Tests authored

### Inherited from selected PR #338

`LevelGridV2AssetCompilerPublicationTests` covers real AssetDatabase and filesystem publication boundaries:

- failure before authoritative switch preserves previous bytes/references;
- failure after replacement rolls back previous bytes/runtime validity;
- successful immutable-version switch;
- referenced old version retention;
- post-commit cleanup warning semantics;
- wrong-type destination rejection before generated writes.

### Added by this integration

`LevelGridEditorRuntimeIntegrationV2Tests` covers:

- undoable metadata addition without fallback selection;
- exact start-room assignment;
- exact room-plus-door final-exit assignment;
- incompatible final-room change clearing the door;
- actionable missing start-room validation;
- actionable missing final-door validation;
- Undo restoration of deleted final door and metadata reference;
- generic destinations never resolving to Combat Loop paths;
- wrong compiled owner rejection;
- compilation-relevant edit changing semantic fingerprint;
- pan/zoom/selection preserving semantic fingerprint;
- production validation required before Build;
- unregistered level blocked from Play;
- exact catalogue resolution without fallback;
- exact Resource path mismatch source guard;
- menu and editor actions sharing the same export/compiler façades;
- change-driven status source guard.

## Manual acceptance checklist

1. Open a scene containing at least two level roots.
2. Open the Level Grid Editor.
3. Select one root explicitly.
4. Add or configure playable metadata.
5. Select exact start room.
6. Select exact final room and traversable final door.
7. Validate successfully.
8. Build the level.
9. Confirm deterministic source and generated destinations.
10. Confirm the compiled asset is selected and importable.
11. Change a room coordinate and verify status becomes stale.
12. Rebuild and verify status becomes current.
13. Delete the final door and verify Build and Play are blocked.
14. Undo and verify reference and validation recovery.
15. Attempt another level's destination and verify rejection.
16. Inject compile failure and verify old compiled level remains playable.
17. Enter exact registered level through production Level Selection.
18. Restart Unity and verify configuration/status reconstruction.
19. Confirm the other scene root was not modified.
20. Confirm ordinary workflow required no JSON editing.

Responsiveness fixture:

```text
approximately 100 rooms
approximately 300 doors
approximately 150 connections
```

Confirm pan, zoom, selection, Inspector edits, and cached status remain responsive and do not compile on repaint.

## Validation executed

Use only the evidence classifications below.

| Validation | Status |
| --- | --- |
| static source review | executed and passed |
| structural checks | not executed |
| Unity compilation | not executed |
| EditMode tests | not executed |
| Editor integration tests | not executed |
| PlayMode tests | not executed |
| manual acceptance | not executed |
| performance testing | not executed |

Static review means connector-backed API/source tracing and full-file review of the authored integration files. It is not compilation.

## Generated output

No playable source package, generated JSON version, or runtime Resource asset was regenerated by this branch because Unity execution is unavailable.

Tracked generated-output behavior is changed only through the selected PR #338 publication implementation. Its content-addressed version ID is reused by status. Deterministic regeneration and stale reconciliation are covered by authored tests but have not been executed here.

New Unity `.meta` files are tracked for every added C# source/test file with unique GUIDs.

## Unverified behavior

- Unity domain reload and C# compilation;
- Editor assembly references and accessibility under Unity's compiler;
- Unity serialization of the new panel state;
- actual Undo/Redo event ordering for metadata and deleted objects;
- AssetDatabase import and transactional publication tests;
- target-platform `File.Replace` behavior;
- source folder export and `.meta` interaction on target operating systems;
- catalogue registration workflow after adding a new code-authored entry;
- actual Level Selection to gameplay transition;
- scene reload and Unity restart reconstruction;
- 100/300/150 responsiveness fixture;
- manual multi-root isolation;
- all existing EditMode and PlayMode regressions.

## Remaining design debt

- Production catalogue registration is still manual and code-authored.
- Direct editor Play is deferred; the safe command opens production Level Selection.
- The retained V1 room importer remains the compatibility gate.
- Room-content visual placement, encounter, enemy, prop, and decor authoring remain separate tooling work.
- PR #338 must be merged or the integration PR must retain its exact commits; this branch is intentionally based on its unmerged head.

## Completion report template

### Production code

List exact editor, exporter, compiler façade, catalogue verification, and production-route paths changed.

### Tests authored

List positive and hostile coverage separately from execution evidence.

### Validation executed

Report each item only as:

```text
executed and passed
executed and failed
not executed
```

### Generated output

Record whether generated files changed, why they are tracked, how regeneration was checked, how stale output was reconciled, and how rollback was verified.

### Unverified behavior

List every framework/runtime/filesystem/manual/performance behavior not directly observed.

### Remaining design debt

Record manual registration, retained compatibility, and deferred tooling.

### Diff size

Separate handwritten production code, tests, generated output, documentation, file count, and architectural responsibilities.

Keep the PR draft while Unity compilation, Editor tests, PlayMode tests, manual acceptance, or performance checks remain unverified.
