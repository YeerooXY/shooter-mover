# LEVEL-SYSTEM-STABILIZATION-001 Door Authority Follow-Up

**Status:** Source remediation complete; Unity compilation, tests and manual gameplay remain pending.  
**Repository basis:** exact refreshed `main` SHA `67c4b756fd0cd8bd2a032e8f173c83a5f7844438`.  
**Implementation PR:** #349, branch `agent/level-system-stabilization-001`.

## Why this follow-up exists

A third hostile audit found that the intended single level-authoring workflow was still incomplete:

- `LevelGridDoorOperationsV2` exposed separate top-level and component-context commands for deleting, reflowing, keeping and capturing doors;
- playable export called a bulk reflow helper before taking its source fingerprint;
- live validation could bulk-reflow edge-managed doors and migrate legacy fixed-door storage after unrelated scene, hierarchy or Undo changes.

In game-editor terms, doors still had several hidden control panels, and pressing Validate or Build could move authored doors without the designer explicitly choosing that edit.

## Requested visible behavior

The supported behavior is now:

```text
edit or move rooms/doors
→ validation reports any automatic-facing mismatch
→ exact door remains where the designer authored it
→ designer chooses Reflow or Keep Placement in the canonical Level Grid editor
→ the chosen edit is recorded through the canonical operation and Unity Undo
→ Build either publishes the exact validated scene or fails without moving it
```

Validation and Build are diagnostic/publication actions. They are not authoring commands.

## Authority map

- Scene objects below the exact `LevelDesignSceneAuthoringRoot2D` remain authored topology authority.
- `LevelGridEditorOperationsV2` remains the sole explicit door command façade.
- `LevelGridEditorWindowV2` exposes the supported Reflow, Keep Placement and Delete Door controls.
- `LevelGridDoorOperationsV2` is now an internal mechanism with no `MenuItem` or Context command surface.
- `LevelGridAuthoringV2LiveValidation` is a read-only diagnostic observer for topology and edge-managed placement.
- `LevelGridV2PlayableExporter` publishes only state that already passes production validation.

## Changes made

### Alternate door commands removed

`LevelGridDoorOperationsV2` no longer registers any Unity menu or component-context commands. The old independent entry points for:

- Delete Selected Door;
- right-click Delete Door;
- Reflow Selected Edge Door;
- Keep Selected Door Placement;
- Capture Selected Door As Fixed;

were removed.

Physical helper methods remain internal so the canonical façade can reuse the existing grouped deletion and exact reflow mechanics without duplicating them.

### Bulk reflow made read-only

The retained `ReflowAll` compatibility entry no longer changes any door. It delegates to `CountDoorsNeedingReflow`, which only counts exact edge-managed doors whose side or resolved position disagrees with their connection direction.

This keeps existing compiled callers source-compatible during the stabilization branch while removing bulk mutation authority. Exact movement occurs only through `LevelGridEditorOperationsV2.ReflowDoor`.

### Live validation made read-only

Live validation no longer:

- invokes bulk door reflow;
- tracks roots requiring automatic reflow;
- migrates legacy fixed-door position storage;
- marks a scene dirty merely because diagnostics refreshed.

It still captures the room-relative position when a designer explicitly moves a door already configured as Fixed. That stores the value the designer authored; it does not move the transform or change topology.

### Build behavior

The exporter still performs production validation before publication. Because bulk inspection is read-only, a misaligned automatic-facing door causes production validation and Build to fail closed. The exact scene side and transform remain unchanged, and no new source destination is committed.

The designer can then use the canonical problem action or door inspector:

- **Reflow** — align the exact door to its connected room direction through grouped Undo;
- **Keep Placement** — preserve the authored placement and disable automatic facing;
- **Delete Door** — remove the door and attached connections through the canonical deletion route.

## Failure-mode result

| Condition | Result |
|---|---|
| Connected automatic-facing door is misaligned | Validation reports `EdgeManagedDoorFacingMismatch`; door is not moved. |
| Designer clicks Reflow | Exact door aligns through `LevelGridEditorOperationsV2`; Unity Undo can restore the prior authored mismatch. |
| Designer clicks Keep Placement | Exact door stays authored and automatic facing is disabled through the canonical operation. |
| Designer clicks Delete Door | Exact door and attached connections are removed as one undoable canonical action. |
| Draft or Production validation runs | Diagnostics refresh; topology and edge-managed door transforms remain unchanged. |
| Build runs with a misaligned door | Build fails before publication; door and destination remain unchanged. |
| Hierarchy changes or Undo/Redo occur | Diagnostics refresh without bulk reflow or legacy migration. |
| Designer directly moves a Fixed door | Its explicit authored room-relative value is captured; the observer does not reposition it. |
| Legacy fixed-door storage is encountered | Validation does not silently migrate the scene; record construction remains backward-safe until an explicit supported edit/save path is chosen. |

## Focused tests authored

`LevelDoorAuthorityV2Tests` adds behavioral and source-surface coverage for:

- read-only mismatch counting;
- Production validation called with the legacy `reflow=true` argument without moving the door;
- exact facing mismatch diagnostics;
- explicit canonical Reflow changing the exact door;
- Unity Undo restoring the prior authored side and position;
- Build failure with no scene mutation and no source publication;
- live validation not migrating legacy fixed-door state;
- no `MenuItem` or old independent door-command methods in the internal utility;
- no live-validation bulk-reflow call;
- canonical Level Grid editor source retaining Reflow, Keep Placement and Delete Door controls.

These tests are authored only. They are not claimed as passing until executed in Unity.

## Validation actually performed

- Current branch ancestry and exact base: **reviewed**.
- Current affected source and canonical panel controls: **reviewed statically**.
- Editor/friend-assembly access: **reviewed**.
- New Unity `.meta` coverage: **added**.
- Unity import/domain reload: **not executed**.
- Unity compilation: **not executed**.
- `LevelDoorAuthorityV2Tests`: **authored, not executed**.
- Existing stabilization EditorTooling tests: **not executed**.
- Manual editor/gameplay acceptance: **not executed**.
- GitHub Actions evidence: **not available at time of source remediation**.

## Exact Unity acceptance route

- [ ] Import the branch and complete domain reload with no compile errors.
- [ ] Run `ShooterMover.Tests.EditorTooling.LevelDesign.Foundation.LevelDoorAuthorityV2Tests`.
- [ ] Run `ShooterMover.Tests.EditorTooling.LevelDesign.Foundation.LevelSystemStabilizationV2Tests`.
- [ ] Open the canonical Level Grid editor and confirm a selected door exposes Reflow, Keep Placement and Delete Door.
- [ ] Confirm the old standalone Delete/Reflow/Keep/Capture commands are absent from Tools and the door component context menu.
- [ ] Connect two rooms, manually create an automatic-facing mismatch and run Draft validation; confirm the door does not move.
- [ ] Run Production validation; confirm it remains blocked and the door does not move.
- [ ] Press Build; confirm it fails without changing the door or replacing the previous playable source.
- [ ] Click Reflow; confirm the exact door aligns and the problem clears.
- [ ] Undo and Redo; confirm the exact side and transform round-trip.
- [ ] Repeat with Keep Placement; confirm the authored location remains and automatic facing disables.
- [ ] Build successfully, open production Level Selection and enter the exact registered level.
- [ ] Traverse both directions through the connected doors and finish through the configured final exit.

## Remaining gate

This source follow-up closes the known competing door-command and silent-reflow defects. PR #349 must remain draft until Unity import, compilation, both focused EditorTooling fixtures, Undo/Redo, successful and failed Build behavior, production Level Selection entry, room traversal and final-exit gameplay are executed and recorded.
