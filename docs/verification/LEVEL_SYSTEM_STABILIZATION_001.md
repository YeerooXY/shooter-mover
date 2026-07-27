# LEVEL-SYSTEM-STABILIZATION-001 verification record

**Status:** Implementation in progress  
**Exact starting `main` SHA:** `67c4b756fd0cd8bd2a032e8f173c83a5f7844438`  
**Branch:** `agent/level-system-stabilization-001`  
**Primary planning evidence:** `docs/audits/LEVEL_SYSTEM_AUDIT_AND_FORWARD_PLAN.md` from draft PR #343  
**Scope:** Consolidate and prove the existing level-authoring foundation without adding a new level-system feature family.

## Requested visible behavior

The Unity editor must present one trustworthy production workflow:

```text
Open Level Grid Editor
→ select the exact LevelDesignSceneAuthoringRoot2D
→ edit rooms, doors and exact endpoint links
→ Validate
→ Build
→ inspect current / stale / error status
→ open production Level Selection
→ enter the exact registered built level
```

Only compilation-relevant scene authoring, playable metadata, source package, generated publication, compiled asset or catalogue state may change playable freshness. Editor selection, panning, zooming and other view state must not mark a level stale.

Errors must identify the affected object, stable identity, path or operation. Unknown, stale, unsupported, mismatched or ambiguous state fails closed.

## Authority map

| Concept | Authoritative owner | Projections / adapters that must not become authorities |
|---|---|---|
| Interactive level topology | Scene hierarchy under the exact `LevelDesignSceneAuthoringRoot2D` | Editor projection, Problems UI, gizmos, JSON output |
| Room identity | `LevelRoomAuthoring2D.RoomIdText` | display name, hierarchy name, grid coordinate, folder name, ordering |
| Door identity and room ownership | `LevelDoorEndpointAuthoring2D.DoorIdText` plus exact `OwningRoom` | hierarchy position, side, local/world coordinate |
| Link identity and endpoints | `LevelDoorLinkAuthoring2D.ConnectionIdText` plus exact room-and-door references | rendered lines, relative room position |
| Playable start/final metadata | `LevelGridPlayableMetadataV2` exact object references | editor popup indices, labels, selected objects |
| Scene mutations | `LevelGridEditorOperationsV2` | menus, inspectors, Problems window, editor view models |
| Production validation/build orchestration | `LevelGridPlayableBuildFacadeV2` | toolbar/menu callbacks |
| Compiler-ready source package | canonical `LevelGridV2PlayableExporter` transaction | Phase-1 draft/validated-authoring exporter |
| Generated publication | `LevelGridV2AssetCompiler` transactional immutable-version publication | generated folder contents viewed in UI |
| Destination resolution | `LevelGridPlayableBuildPathsV2`, derived from exact stable level ID | display names, manually chosen paths |
| Production level registration | `ProductionPlayableLevelCatalogV1` exact stable-ID and Resource-path entry | editor registration text |
| Production entry route | production Level Selection and its existing selected-character/navigation authorities | editor-only injection or fallback play route |

## Expected changed files and ownership

Expected production/editor changes are limited to:

- Level Grid editor entry points;
- compatibility menus and inspectors that currently duplicate topology operations;
- explicit guards that disable stale Phase-1/export/compiler menu surfaces;
- the canonical playable source exporter late snapshot protection.

Expected focused regression coverage is limited to:

- source guards proving compatibility surfaces delegate to canonical operations;
- hostile late source/destination mutation checks at the playable export commit boundary.

Expected documentation is limited to this verification record and workflow wording needed to describe the single supported route.

## Forbidden systems and files

This task must not modify or create:

- JSON V2, V1 runtime or catalogue replacement architecture;
- room-content visual authoring;
- a Create Level wizard;
- runtime minimap or discovery state;
- gameplay rewards, weapons, XP, inventory or save data;
- selected-character, navigation, room-clear or persistence authorities;
- scenes, prefabs or generated assets merely to make static inspection look complete;
- fallback levels, characters, profiles, rewards or catalogue entries.

## Stable identities that must survive

- stable level ID;
- every room ID;
- every door ID;
- every link ID;
- exact link room-and-door endpoints;
- exact playable start room;
- exact final room-plus-door endpoint;
- exact production catalogue stable ID and Resource path.

Movement, rename, selection, pan, zoom, room-folder migration and publication retries must not replace or infer these identities from paths, names, coordinates, hierarchy order or indices.

## Transaction contract

### Scene mutation

`LevelGridEditorOperationsV2` is the sole command authority for Grid V2 room, door, link, deletion, reflow and validation operations.

- **Before commit:** validate exact selected objects and ownership; create or record all Undo participants.
- **Commit point:** the grouped Unity Undo operation is collapsed after the complete scene mutation.
- **Rollback:** Unity Undo restores the complete grouped mutation, including dependent links where applicable.
- **Retry:** creates allocate new stable identities; rejected connection attempts create nothing; deletion/reflow retries must not affect unrelated objects.

### Playable source export

- **Before commit:** validate metadata, foundation and graph; verify exact destination ownership; capture the exact compilation-relevant scene fingerprint and destination snapshot; create and validate a staged package; recheck source and destination immediately before replacement.
- **Commit point:** the validated stage occupies the authoritative source-package destination.
- **Rollback:** any failure before commit removes the stage and restores the exact previous destination from backup if it was moved.
- **Post-commit cleanup:** backup and metadata cleanup are best effort and cannot make a committed export appear uncommitted.
- **Retry:** deterministic export over the same authoritative source reconciles the same destination; a conflicting external change blocks rather than being overwritten.

### Compiled asset publication

The accepted immutable-version `LevelGridV2AssetCompiler` transaction remains authoritative and is not rewritten here.

- **Commit point:** atomic replacement of the authoritative `JsonRoomContentDefinition2D` after staged import and runtime validation.
- **Rollback:** exact previous asset bytes and references are restored and validated if the switch fails.
- **Retry:** content-addressed immutable versions prevent duplicate or ambiguous publication; cleanup is reference-aware and best effort after commit.

## Runtime, editor, persistence and assembly boundaries

- Scene topology components remain runtime-assembly types because scenes serialize them.
- UnityEditor APIs, menus, inspectors, windows, filesystem publication and `AssetDatabase` operations remain in the Editor-only assembly.
- Editor projections and status models never write authoritative topology directly.
- Exported JSON is a deterministic build boundary, not an interactive graph authority.
- Generated TextAssets and compiled ScriptableObjects remain projections of validated source.
- Production Level Selection remains the only supported play-entry route from this workflow.

## Failure-mode matrix

| Condition | Required behavior |
|---|---|
| Move room | Preserve room, door, link and playable identities; reflow through the canonical command; mark source stale only when compilation-relevant state changed. |
| Rename room or hierarchy object | Preserve all stable identities and exact references. |
| Delete room | Remove every link touching the room by room reference, door ownership or hierarchy relationship; preserve unrelated endpoints; Undo restores the complete operation. |
| Delete door | Remove every attached link; preserve the opposite endpoint; Undo restores exact references. |
| Duplicate identity | Validation fails closed with the exact duplicated stable ID and object location. |
| Malformed or missing data | Validation/export/compile fails closed with an exact object, path or operation. |
| Stale source or generated state | Status reports the exact stale boundary; Build uses only the canonical current source. |
| Source changes during export | Abort before destination replacement; previous playable source remains authoritative. |
| Destination changes during export | Abort before overwrite; preserve the external change for explicit reconciliation. |
| Failure after destination backup but before stage switch | Restore the exact backup before reporting failure. |
| Failure after source export commit but before compile commit | Keep the new source export; preserve the previous compiled playable asset. |
| Cleanup failure | Report successful committed operation; retain recoverable orphaned backup/version if cleanup fails. |
| Retry | Do not duplicate rooms, doors, links or publications; deterministic state either commits once or fails closed. |
| Undo/Redo | Restore scene topology and exact serialized object references without replacing stable identity. |
| Reload/restart | Reconstruct semantics from scene serialization, source provenance, compiled immutable version and exact catalogue entry. |
| Wrong destination owner | Reject before mutation; never adopt or overwrite another level's folder or asset. |
| View-only change | Pan, zoom and selection do not change the compilation-relevant scene fingerprint or freshness status. |
| Obsolete command surface | Disabled or redirected; cannot bypass canonical mutation, export or build operations. |

## Exact manual Unity acceptance route

1. Open the project and wait for Unity import and domain reload to finish.
2. Open the authoring scene containing the registered level.
3. Select the exact `LevelDesignSceneAuthoringRoot2D`.
4. Open **Tools → Shooter Mover → Level Design → Open Level Grid Editor**.
5. Confirm the editor is focused on that exact root.
6. Record a room's stable ID, move the room, and confirm the ID is unchanged.
7. Undo and redo the move and confirm the same room, door and link identities return.
8. Add a door through the visual editor, connect it through the canonical endpoint command, then remove the link and door through the canonical commands.
9. Run **Validate** and inspect object-specific diagnostics.
10. Run **Build** and confirm source, generated, compiled and catalogue status become current/registered.
11. Pan, zoom and change selection; confirm current status remains current.
12. Make one compilation-relevant scene edit; confirm source/build status becomes stale.
13. Restore or rebuild and confirm current status returns.
14. Open the production Level Selection scene from the editor.
15. Choose the exact registered level and enter the playable level through the existing production route.
16. Create a deliberate invalid build input and run Build; confirm failure identifies the exact object/operation and the previously playable compiled asset still runs.
17. Inspect menus and inspectors; confirm no Phase-1 draft/publish, arbitrary playable export or arbitrary compiler command can bypass the visual editor's canonical workflow.

## Validation evidence

This table records only work actually executed in the implementation environment.

| Evidence level | Status | Notes |
|---|---|---|
| Static source review | In progress | Audit findings are being checked against exact current `main` source before each edit. |
| Structural checks | Not yet completed | Final caller, assembly, menu and full-file checks are pending. |
| Unity import/domain reload | Not executed | Requires the target Unity editor environment. |
| Compilation | Not executed | Static inspection is not compilation. |
| Automated tests | Not executed | Authored coverage will be reported separately from execution. |
| Unity EditMode/Editor tests | Not executed | Requires the target Unity editor environment. |
| Unity PlayMode tests | Not executed | Requires the target Unity editor environment. |
| Manual acceptance | Not executed | Follow the exact checklist above in Unity. |
| Performance testing | Not executed | Outside this stabilization patch unless a regression is observed. |

## Generated output policy

No generated source package, immutable generated version, compiled asset, scene or prefab will be changed solely from static inspection. Generated output may be reconciled only by executing the canonical Unity Build workflow and recording that evidence. Until then, the previous playable output remains authoritative.

## Expected conflicts and merge order

This branch is the sole active owner of broad level-editor/export/publication consolidation. It should merge before any branch that:

- decomposes `LevelGridEditorWindowV2` into new services;
- adds a Create Level/catalogue-registration workflow;
- adds room-content authoring;
- adds a runtime map;
- broadly changes production level catalogue or room-runtime composition.

Unrelated gameplay lanes may merge independently only if they do not modify level editor infrastructure, room runtime composition, production catalogue/selection or shared persistence authorities.

## Claim-to-evidence map

To be completed after implementation:

```text
One canonical scene mutation route
→ LevelGridEditorOperationsV2
→ scene hierarchy authority
→ focused delegation/source-guard tests
→ execution evidence pending Unity

Late source/destination conflict protection
→ LevelGridV2PlayableExporter export transaction
→ scene fingerprint + exact destination snapshot
→ hostile mutation regression coverage
→ execution evidence pending Unity

Exact production play route
→ LevelGridPlayableBuildFacadeV2.OpenProductionLevelSelectionScene
→ ProductionPlayableLevelCatalogV1 exact stable-ID/Resource entry
→ existing runtime-integration coverage + manual checklist
→ execution evidence pending Unity
```
