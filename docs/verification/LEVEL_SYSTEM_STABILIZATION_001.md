# LEVEL-SYSTEM-STABILIZATION-001 verification record

**Status:** Implementation complete; Unity execution and manual acceptance remain pending  
**Exact starting `main` SHA:** `67c4b756fd0cd8bd2a032e8f173c83a5f7844438`  
**Branch:** `agent/level-system-stabilization-001`  
**Draft PR:** #349  
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
| Scene mutation commands | `LevelGridEditorOperationsV2` | menus, inspectors, Problems window, editor view models |
| Playable-aware topology validation | `LevelGridPlayableValidationV2` over the authoritative records and exact final-exit IDs | exporter-specific rewrites, status-only exceptions |
| Production validation/build orchestration | `LevelGridPlayableBuildFacadeV2` | toolbar/menu callbacks |
| Compiler-ready source package | canonical `LevelGridV2PlayableExporter` transaction | Phase-1 draft/validated-authoring exporter |
| Generated publication | `LevelGridV2AssetCompiler` transactional immutable-version publication | generated folder contents viewed in UI |
| Destination resolution | `LevelGridPlayableBuildPathsV2`, derived from exact stable level ID | display names, manually chosen paths |
| Production level registration | `ProductionPlayableLevelCatalogV1` exact stable-ID and Resource-path entry | editor registration text |
| Production entry route | production Level Selection and its existing selected-character/navigation authorities | editor-only injection or fallback play route |

## Production changes

### Canonical editor route

- Added `LevelGridEditorWindowV2.OpenForRoot` so compatibility callers open the exact authoring root rather than relying on ambient selection.
- Routed GameObject door creation and endpoint linking through `LevelGridEditorOperationsV2`.
- Routed inspector/menu validation and room deletion through `LevelGridEditorOperationsV2`.
- Redirected the retained Problems entry point into the integrated Level Grid editor.
- Removed direct room/door Grid V2 snapping from the generic foundation menu; the command now directs the user to canonical Move/Reflow operations.
- Preserved unrelated legacy placement snapping.

### Retired production surfaces

Validation callbacks now disable these stale or bypass-capable menu routes while retaining their code for migration fixtures and focused tests:

- Phase-1 three-room starter creation;
- Phase-1 draft export;
- Phase-1 validated-authoring publication;
- arbitrary compiler-ready package export;
- tracked-level compiler shortcut;
- arbitrary-folder compiler shortcut.

The stale inspector statement that the runtime importer is not connected was replaced with the actual compiler-ready source → transactional publication → exact catalogue → production Level Selection route.

### Consistent final-exit validation

`LevelGridPlayableValidationV2` now owns the one playable topology exception:

- the exact configured final-exit door remains a traversable runtime exit but is not required to connect to another authored room;
- the same exact endpoint cannot also participate in a room-to-room link;
- invalid use reports the exact connection ID, endpoint identity and diagnostic location.

Both scene validation/status and playable export use this same contract. The JSON/V2/V1 architecture was not rewritten.

### Late export conflict protection

Playable source export now:

1. validates exact metadata, foundation, graph and destination ownership;
2. captures the compilation-relevant scene fingerprint;
3. captures a deterministic destination snapshot covering relative directories, file lengths and SHA-256 file contents;
4. stages, compiles and runtime-import-validates the replacement package;
5. rechecks source and destination immediately before replacement;
6. verifies the moved rollback backup still matches the captured destination;
7. commits only when the validated stage occupies the exact destination.

A source change blocks with the exact level ID. A destination change blocks with the exact path and preserves the external change for explicit reconciliation.

## Forbidden systems and files preserved

This task did not modify or create:

- a replacement JSON V2, V1 runtime or catalogue architecture;
- room-content visual authoring;
- a Create Level wizard;
- runtime minimap or discovery state;
- gameplay rewards, weapons, XP, inventory or save data;
- selected-character, navigation, room-clear or persistence authorities;
- fallback levels, characters, profiles, rewards or catalogue entries;
- scenes, prefabs, compiler-ready source packages, generated versions or compiled assets merely to make static inspection appear complete.

## Stable identities preserved

- stable level ID;
- every room ID;
- every door ID;
- every link ID;
- exact link room-and-door endpoints;
- exact playable start room;
- exact final room-plus-door endpoint;
- exact production catalogue stable ID and Resource path.

Movement, rename, selection, pan, zoom, room-folder migration and publication retries do not replace or infer these identities from paths, names, coordinates, hierarchy order or indices.

## Transaction contract

### Scene mutation

`LevelGridEditorOperationsV2` remains the sole command authority for Grid V2 room, door, link, deletion, reflow and explicit validation operations.

- **Before commit:** validate exact selected objects and ownership; create or record all Undo participants.
- **Commit point:** the grouped Unity Undo operation is collapsed after the complete scene mutation.
- **Rollback:** Unity Undo restores the complete grouped mutation, including dependent links where applicable.
- **Retry:** creates allocate new stable identities; rejected connection attempts create nothing; deletion/reflow retries do not affect unrelated objects.

### Playable source export

- **Before commit:** validate metadata, foundation and graph; verify exact destination ownership; capture exact scene and destination snapshots; create and validate a staged package; recheck source and destination immediately before replacement.
- **Commit point:** the validated stage occupies the authoritative source-package destination.
- **Rollback:** failure before commit removes the stage and restores the exact previous destination from backup if it was moved.
- **Post-commit cleanup:** backup and metadata cleanup are best effort and cannot make a committed export appear uncommitted.
- **Retry:** deterministic export over the same authoritative source reconciles the same destination; a conflicting external change blocks rather than being overwritten.

### Compiled asset publication

The accepted immutable-version `LevelGridV2AssetCompiler` transaction remains authoritative and was not rewritten.

- **Commit point:** atomic replacement of the authoritative `JsonRoomContentDefinition2D` after staged import and runtime validation.
- **Rollback:** exact previous asset bytes and references are restored and validated if the switch fails.
- **Retry:** content-addressed immutable versions prevent duplicate or ambiguous publication; cleanup is reference-aware and best effort after commit.

## Runtime, editor, persistence and assembly boundaries

- Scene topology and playable validation components remain runtime-assembly types because scenes serialize or invoke them without UnityEditor dependencies.
- UnityEditor APIs, menus, inspectors, windows, filesystem publication and `AssetDatabase` operations remain in the Editor-only assembly.
- Editor projections and status models never write authoritative topology directly.
- Exported JSON remains a deterministic build boundary, not an interactive graph authority.
- Generated TextAssets and compiled ScriptableObjects remain projections of validated source.
- Production Level Selection remains the only supported play-entry route from this workflow.

## Failure-mode matrix

| Condition | Result |
|---|---|
| Move room | Canonical command preserves room, door, link and playable identities; connected doors reflow. |
| Rename room or hierarchy object | Stable IDs and exact object references remain authoritative. |
| Delete room | Canonical deletion removes every dependent link found through room, door ownership or hierarchy relationships; unrelated endpoints remain. |
| Delete door | Canonical deletion removes attached links; Undo remains grouped. |
| Duplicate identity | Existing validation fails closed with duplicated stable ID and diagnostic location. |
| Malformed or missing data | Validation/export/compile fails closed with an object, identity, path or operation. |
| Unconnected exact final exit | Allowed only for the exact metadata-owned final room-plus-door endpoint. |
| Final exit reused as room link | Rejected with exact connection and final door identity. |
| Stale source or generated state | Existing status evaluator reports the stale boundary; Build uses the canonical source route. |
| Source changes during export | Export aborts before replacement; previous playable source remains authoritative. |
| Destination changes during export | Export aborts before overwrite and preserves the external change. |
| Destination changes while moved to backup | Snapshot mismatch aborts; rollback restores the exact backup. |
| Failure after source export commit but before compile commit | New source remains committed; existing compiler transaction preserves previous playable compiled output. |
| Cleanup failure | Committed operation remains successful; recoverable backup/version may remain for later cleanup. |
| Retry | No duplicate rooms, doors, links or publications; deterministic state commits once or fails closed. |
| Undo/Redo | Existing grouped Unity operations restore topology and serialized object references without replacing stable identity. |
| Reload/restart | Scene serialization, source provenance, immutable generated version and exact catalogue entry reconstruct semantics. |
| Wrong destination owner | Existing ownership checks reject before mutation. |
| View-only change | Existing scene fingerprint excludes pan, zoom and editor selection. |
| Obsolete command surface | Disabled or redirected; cannot execute an alternate production export/build route. |

## Tests authored

`LevelSystemStabilizationV2Tests` adds focused Editor-tooling coverage for:

- successful canonical playable source export;
- scene mutation during staging aborting before source replacement;
- destination mutation during staging being preserved and blocking overwrite;
- exact final-exit positive and hostile link-reuse behavior;
- source guards proving compatibility surfaces delegate to canonical operations or are disabled.

These tests were authored but were not executed in this environment.

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
17. Inspect menus and inspectors; confirm no Phase-1 draft/publish, arbitrary playable export or arbitrary compiler command can execute.

## Validation executed

This table distinguishes source evidence from executed Unity evidence.

| Evidence level | Status | Notes |
|---|---|---|
| Static source review | **Completed** | Read the audit and current source, traced caller/consumer paths, reread every modified production file and inspected exporter partials. |
| Structural checks | **Completed** | Verified exact branch ancestry, changed-file list, runtime/editor separation, `.meta` coverage, menu paths, public callers, transaction boundaries and no generated-content edits. |
| Unity import/domain reload | **Not executed** | No Unity editor is available through the connected GitHub environment. |
| Compilation | **Not executed** | Static source inspection is not compilation. |
| Automated tests | **Not executed** | No workflow runs were present for the PR head. |
| Unity EditMode/Editor tests | **Not executed** | Authored coverage is listed separately above. |
| Unity PlayMode tests | **Not executed** | Requires the target Unity editor environment. |
| Manual acceptance | **Not executed** | Follow the exact checklist above. |
| Performance testing | **Not executed** | No performance-sensitive runtime loop was changed. |

## Generated and metadata files

- New handwritten Unity scripts and the focused test file include tracked `.meta` files.
- No compiler-ready source package, immutable generated version, compiled resource asset, scene or prefab was modified.
- Existing generated output was intentionally preserved because deterministic regeneration and source/generated agreement cannot be claimed without running the canonical Unity Build workflow.
- The previous playable output therefore remains authoritative until Unity acceptance performs and records a successful Build.

## Diff size and split decision

Compared with exact starting SHA `67c4b756fd0cd8bd2a032e8f173c83a5f7844438`:

- **Production source files:** 8
- **Production additions:** 584 lines
- **Production deletions:** 636 lines
- **Net production change:** −52 lines
- **Focused test code:** 267 lines
- **Documentation:** 220 lines before this final evidence update
- **Unity metadata:** 4 new `.meta` files, 44 lines
- **Total changed files before this final evidence update:** 14
- **Architectural responsibilities:** 3 — editor command consolidation, playable validation consistency, source-export transaction hardening

The production additions cross the approximate 500-line review prompt. A split was considered and rejected because:

- the patch removes more production code than it adds;
- only 8 production source files and exactly 3 responsibilities are involved;
- the changes form one vertical authority route whose intermediate split would temporarily leave competing validation/export semantics;
- the independently risky boundary, late export conflict protection, has focused hostile coverage;
- the PR remains draft until Unity execution proves the combined route.

## Claim-to-evidence map

```text
Claim: Compatibility topology actions have one command authority
→ production path: GameObject menus / inspector / retained Problems entry
  → LevelGridEditorOperationsV2 / LevelGridEditorWindowV2.OpenForRoot
→ authoritative state: exact scene root hierarchy and stable object references
→ tests: CompatibilitySurfaces_DelegateOrDisableInsteadOfMutatingDirectly
→ execution evidence: static full-file and unified-diff review completed
→ limitation: Unity menu invocation and Undo acceptance not executed

Claim: Validate and export agree on the exact final-exit endpoint
→ production path: LevelDesignSceneAuthoringRoot2D and LevelGridV2PlayableExporter
  → LevelGridPlayableValidationV2
→ authoritative state: LevelGridPlayableMetadataV2 exact room-plus-door references
→ tests: successful graph validation/export plus hostile final-exit link reuse
→ execution evidence: static caller/assembly review completed
→ limitation: Editor test assembly not compiled or executed

Claim: Late source or destination changes cannot overwrite the previous source package
→ production path: LevelGridV2PlayableExporter staged transaction
→ authoritative state: scene fingerprint + exact destination content snapshot
→ tests: hostile scene and destination mutation hooks
→ execution evidence: transaction and rollback paths statically reviewed
→ limitation: filesystem fault tests not executed in Unity

Claim: Production entry remains exact catalogue resolution through Level Selection
→ production path: LevelGridPlayableBuildFacadeV2
  → LevelGridV2AssetCompiler
  → ProductionPlayableLevelCatalogV1
  → production Level Selection
→ authoritative state: exact level stable ID and exact Resource path
→ tests: existing runtime-integration source coverage plus manual checklist
→ execution evidence: current production caller path statically inspected
→ limitation: exact level was not entered in Play Mode
```

## Unverified behavior

- Unity import and domain reload of new scripts and metadata;
- C# compilation in the real project assemblies;
- actual execution of the new and existing Editor tests;
- visual editor room move, Undo/Redo, add/remove door and link;
- current/stale/error status transitions in the live editor;
- failed Build preserving the previous compiled playable asset in the real AssetDatabase;
- production Level Selection entering the exact built level;
- platform-specific filesystem behavior during replacement and rollback.

## Remaining design debt

- Legacy Phase-1 helpers remain compiled for migration fixtures and tests, but their production menu surfaces are disabled. Removing the underlying code should be a separate cleanup after migration consumers are proven absent.
- The final-exit link-reuse diagnostic reuses the existing `DoorUsedByMultipleConnections` problem code while supplying an exact specialized message. Introducing a new serialized/problem enum solely for that label was intentionally avoided in this stabilization patch.
- The existing internal draft projection refresh still invokes root validation directly; it does not mutate topology or publish output and therefore remains a read/diagnostic refresh rather than a competing command authority.
- Unity execution evidence is still required before this draft can be considered merge-ready.

## Conflicts and expected merge order

This branch is the sole active owner of broad level-editor/export/publication consolidation. It should merge before any branch that:

- decomposes `LevelGridEditorWindowV2` into new services;
- adds a Create Level/catalogue-registration workflow;
- adds room-content authoring;
- adds a runtime map;
- broadly changes production level catalogue or room-runtime composition.

Unrelated gameplay lanes may merge independently only if they do not modify level editor infrastructure, room runtime composition, production catalogue/selection or shared persistence authorities. PR #343 remains audit/planning evidence rather than implementation.
