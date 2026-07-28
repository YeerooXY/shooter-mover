# LEVEL-SYSTEM-STABILIZATION-001 Self-Audit Remediation

**Status:** Targeted remediation complete in source; Unity execution remains pending.  
**Repository basis:** exact refreshed `main` SHA `67c4b756fd0cd8bd2a032e8f173c83a5f7844438`.  
**Implementation PR:** #349, branch `agent/level-system-stabilization-001`.  
**Self-audit code-remediation head before this evidence commit:** `3e1ad0d377612ab2995a7f489d665c9afa0ca451`.

## Requested visible behavior

The trusted level-authoring route must not expose retired Phase-1 commands, placements selected under rooms must still snap correctly, production status must distinguish Draft from ProductionPublish validation, and an intentional final-exit endpoint must not appear as a generic unresolved-door warning.

A playable-source export that fails after moving the previous destination to backup must restore that exact destination. If restoration cannot complete because the path is externally occupied or the move fails, the previous package must remain preserved at an explicit recovery path and the operation must report both failures.

## Authority map

- Scene objects below the exact `LevelDesignSceneAuthoringRoot2D` remain authored topology authority.
- `LevelGridEditorOperationsV2` remains the explicit Grid V2 command authority.
- `LevelGridPlayableMetadataV2` remains exact start/final object-reference authority.
- `LevelGridPlayableValidationV2` remains shared playable-graph validation composition.
- `LevelGridV2PlayableExporter` remains compiler-ready source-package transaction authority.
- Compatibility menus, inspectors, gizmos and tests remain projections or callers, never write authorities.

## Self-audit findings remediated

### Placement snapping regression

`Snap Selected To Authored Grid` now checks an exact `LevelPlacementAuthoring2D` before treating a selected child as room context. The actual placement snap is exposed through an internal non-modal helper so EditMode coverage cannot block on an editor dialog.

### Legacy actions inside the canonical editor

The `Legacy/` submenu and its calls to the starter, draft-export and validated-authoring commands were removed from `LevelGridEditorWindowV2.Playable.cs`. The retained top-level legacy commands remain disabled through Unity menu validation guards for migration/test compatibility.

### Direct component context workflows

User-visible `[ContextMenu]` entry points were removed from the exact root, Grid V2 room, endpoint and link authoring components. Their narrow public methods remain callable by canonical editor operations and tests.

The root's direct context validation implementations were removed rather than retained as dead alternate workflows. Automatic/internal Draft projection validation remains read-only diagnostic refresh and does not publish output.

### Truthful validation presentation

The root inspector and integrated Problems panel now require `LevelGridValidationPurposeV2.ProductionPublish` before displaying production publication as allowed. A clean Draft result is displayed as production validation **not run**.

### Exact final-exit presentation and ownership

A valid configured final-exit endpoint is no longer colored as a generic unconnected traversable warning. Metadata validation now also requires the exact final-exit door to belong to the exact level root, preventing Validate/Build disagreement for a foreign hierarchy object that merely claims the final room.

### Playable-source rollback edge

The exporter now has an explicit after-backup/before-switch hostile injection boundary. On pre-commit failure it:

1. removes the staged package best-effort;
2. restores the exact backup when the destination is absent;
3. preserves the backup and reports its exact path if the destination is occupied or restoration fails;
4. rethrows the original failure after a successful rollback;
5. keeps the stage-to-destination move as the commit point.

Directory snapshot ordering now uses ordinal path ordering rather than case-insensitive ordering, avoiding ambiguous ordering for case-distinct paths on case-sensitive filesystems.

## Focused tests authored

The EditorTooling fixture now covers:

- successful canonical source export;
- scene mutation during staging;
- destination mutation during staging;
- failure after the previous destination has moved to backup, including exact restoration;
- the final-exit unconnected exception and hostile link reuse;
- a final-exit door outside the exact root while claiming the final room;
- placement snapping for a placement nested under a room;
- source guards for legacy submenu removal, production-purpose status, final-exit presentation, context-menu removal, exact-root metadata validation and deterministic snapshot ordering.

The tests use the existing `InternalsVisibleTo("ShooterMover.Tests.EditorTooling")` assembly boundary. No public test-only API was introduced.

## Failure-mode result

| Condition | Result |
|---|---|
| Placement nested under room | Exact placement snap executes; room redirect is not selected. |
| Draft validation is clean | Production status says `not run`, not `allowed`. |
| Valid final exit is unconnected | Playable validation accepts it and gizmo presentation does not warn generically. |
| Final exit is reused by a room link | Production validation fails closed. |
| Final-exit door is outside exact root | Metadata validation fails before export. |
| Source changes during staging | Export aborts without replacing destination. |
| Destination changes during staging | External change is preserved and export aborts. |
| Failure after backup move | Exact prior destination is restored before the original failure escapes. |
| Rollback destination becomes occupied | External destination is not overwritten; backup recovery path is reported. |
| Post-commit cleanup fails | Committed destination remains authoritative; cleanup is best effort. |
| Retry after contained failure | Retry starts from the current exact scene and destination state; no duplicate publication is adopted. |

## Validation actually performed

- Exact branch ancestry and merge base: **verified**.
- Current changed-file ownership and PR diff: **reviewed**.
- Modified production and test patches: **reviewed statically**.
- Runtime/editor assembly separation: **reviewed**.
- Friend-assembly access for internal EditorTooling hooks: **verified from `AssemblyInfo.cs`**.
- Generated assets, scenes, prefabs and playable packages: **not changed**.
- GitHub Actions / CI: **not available for this head**.
- Unity import/domain reload: **not executed**.
- Unity compilation: **not executed**.
- Unity EditMode/Editor tests: **authored, not executed**.
- Manual editor/gameplay acceptance: **not executed**.

## Exact Unity acceptance route still required

- [ ] Import the branch and complete domain reload without errors.
- [ ] Run `ShooterMover.Tests.EditorTooling.LevelDesign.Foundation.LevelSystemStabilizationV2Tests`.
- [ ] Select a placement nested below a room and execute **Snap Selected To Authored Grid**.
- [ ] Confirm room and Grid V2 door selection redirects to the canonical editor instead of directly snapping.
- [ ] Confirm the canonical editor contains no Legacy submenu.
- [ ] Confirm root, room, Grid V2 door and link component menus contain no direct identity/snap/validation commands removed by this remediation.
- [ ] Run Draft validation and confirm production status says **not run**.
- [ ] Run Production validation and confirm allowed/blocked status reflects that result.
- [ ] Confirm the configured unconnected final exit is not shown as a generic warning.
- [ ] Build successfully, then inject/observe a pre-commit failure after backup movement and confirm the previous source package is restored.
- [ ] Open production Level Selection and enter the exact registered level.

## Remaining limits and debt

- Unity compilation and execution evidence remain mandatory before the PR leaves draft.
- Serialized authoring fields remain scene-authority inputs and can be edited through ordinary Unity serialization; malformed or ambiguous state must continue to fail validation rather than be silently normalized.
- Legacy helper implementations remain compiled for migration fixtures/tests, but their production menu routes are disabled and no longer advertised by the canonical editor.
- The exact final-exit link-reuse diagnostic still reuses `DoorUsedByMultipleConnections` with a specialized message.
- Transaction-leftover discovery and user-facing recovery tooling remain later hardening work; this patch reports the exact backup path but does not add a recovery window.
