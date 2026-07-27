# Level Grid Authoring V2 Phase 1 — Self-Audit and Repair Record

## Scope

This document records the static audit history for draft PR #333.

The PR is now explicitly scoped as:

```text
Track A Phase 1
= editor graph + safe authoring operations + transactional authoring export
```

It does **not** claim the complete Track A playable milestone. The current
`RoomContentJsonImporterV1 -> RoomContentBundleV1 -> playable runtime` cutover is
not part of this PR and remains a required follow-up.

## Second validation-loop findings and repairs

### 1. Production publishing bypassed the existing foundation validator

**Original risk:** a fully connected V2 door graph could publish despite an
invalid level ID, missing room bounds, bad grid metadata, overlapping rooms,
broken placements/voids, or invalid legacy door composition.

**Repair:** validated authoring publish now requires both:

```text
LevelDesignFoundationValidator has zero errors
AND
Level Grid V2 validation has zero errors
```

The custom inspector, validation menu, Problems summary, and exporter all show or
apply the combined gate. Draft export remains permissive except for conditions
that could corrupt or misassign files.

### 2. Folder resolution could attach one room's sidecars to another room

**Original risk:** a preferred folder name could be reused after a room was
deleted, `room.json` and `doors.json` could be overwritten, and old enemies,
props, decor or encounter data could silently become the new room's content.
Malformed identity files created a similar ambiguity.

**Repair:** export is now a staged transaction.

- The destination must be empty or a previous export of the same level.
- Every existing room folder must contain a readable `room.json` with `room_id`.
- Malformed or missing identity data blocks export.
- Duplicate folder ownership blocks export.
- A folder owned by another or orphaned room is never adopted.
- Active room folders are found by stable room ID and moved in the staged copy.
- The staged package is validated before it replaces the destination.
- Failed staging leaves the destination unchanged.
- If a final swap cannot be completely rolled back, the emergency backup is
  retained beside the destination rather than deleted by cleanup.

### 3. The PR did not implement the complete playable authoring milestone

**Original problem:** the first PR description implied Track A completion while
its `level.json` and generic sidecars were not the current playable importer
contract. It lacked start/terminal room data, runtime spawn anchors, door object
and link semantics, and a proven route into `RoomContentBundleV1`.

**Repair:** the PR was renamed and re-documented as **Phase 1 editor foundation**.
Exported `level.json` contains:

```json
{
  "milestone_scope": "track-a-phase-1-editor-foundation",
  "runtime_import_status": "not-connected"
}
```

The sidecars no longer pretend to be a generic runtime-ready `items` format. The
encounter scaffold uses `all-enemies`, but all Phase 1 sidecars remain explicitly
classified as authoring placeholders.

The follow-up runtime/compiler PR must prove:

```text
V2 authoring package
-> existing manifest/layout/enemies/props/decor/encounter contract
-> RoomContentJsonImporterV1
-> RoomContentBundleV1
-> existing playable room runtime
```

and must include COMBAT LOOP TEST migration/acceptance evidence.

### 4. Room folder suffix used a global ordinal

**Original risk:** a room at `(50,51)` could become `Room_50_51_17`, and two rooms
at one coordinate did not receive stable `_01` / `_02` slots.

**Repair:** every room now stores an explicit per-coordinate `folderSlot`.

```text
grid = [50, 51]
slot = 1
folder = Room_50_51_01
```

Duplicate coordinate+slot pairs are validation errors. When a room moves, its
folder is migrated to the new coordinate+slot name transactionally while all
sidecars move with it.

### 5. Individual door deletion was not connection-safe

**Original risk:** deleting a door GameObject directly left a broken link
component referencing a missing endpoint.

**Repair:** added:

```text
Tools > Shooter Mover > Level Design > Delete Selected Door (Undoable)
```

and a door component context command. One Unity Undo group:

```text
deletes the selected endpoint
-> deletes its attached link(s)
-> preserves the opposite endpoint
-> revalidates it as unresolved
-> opens Problems
-> supports one-step Undo restoration
```

Routine deletion is non-modal.

### 6. Dragging a fixed door did not update exported fixed placement

**Original risk:** the Transform moved, but `fixedLocalPosition` stayed stale;
export wrote the old coordinate and Snap moved the door backward.

**Repair:** Scene/Inspector property tracking captures Transform local-position
changes into `fixedLocalPosition` for fixed doors. An explicit command is also
available:

```text
Capture Selected Door As Fixed
```

Export writes both the captured fixed position and current local position for
diagnostics.

### 7. Connected edge-managed doors did not react to room movement

**Original risk:** moving a connected room could leave a door on the wrong side.

**Repair:** connected edge-managed endpoints have an `autoFaceConnection` policy.
Room and property edits run connection-aware reflow. Fixed doors remain
untouched. A facing mismatch can also be resolved non-modally through:

```text
[Reflow] [Keep]
```

`Keep` disables automatic facing for that endpoint.

## Smaller concerns repaired

- Problems refresh after ordinary Inspector and Scene property edits.
- Duplicate-ID selection matches stable ID plus diagnostic hierarchy path before
  falling back to first-ID selection.
- Production editor code uses `ConfigureConnection` and `ConfigureAuthoring`, not
  methods named `ConfigureForTests`.
- Creation refuses to connect an endpoint that is already connected.
- Room deletion now starts an explicit Unity Undo group.

## Regression coverage added

Focused EditMode tests now cover or statically assert:

- the combined foundation + V2 publish gate;
- room folder migration with sidecar preservation;
- rejection of deleted-room folder adoption;
- malformed room identity blocking without destination replacement;
- atomic door deletion, opposite-endpoint preservation, and Undo;
- fixed-door position capture;
- connected room direction reflow;
- duplicate coordinate+slot rejection;
- optional room labels and stable room identity;
- draft warning versus production blocking for unresolved doors.

## Three-room Phase 1 example

The one-click example creates:

```text
[Starter Room (0,0)/01]--[Room 1,0/01]--[Room 2,0/01]
```

Its first export is:

```text
Rooms/
├── Room_0_0_01/
├── Room_1_0_01/
└── Room_2_0_01/
```

Only the starter room has an explicit display name. The other two use automatic
coordinate labels.

## Remaining verification and scope blockers

This connector environment cannot run Unity 6000.3.19f1. Therefore the following
are still unverified and the PR must remain draft:

- Unity C# compilation and domain reload;
- EditMode execution with zero failures;
- manual room and door deletion/Undo behavior;
- fixed-door drag synchronization in Scene view;
- room-move reflow and Reflow/Keep interaction;
- staged folder migration on the target operating systems;
- performance and usability with 100+ rooms;
- exact three-room export serialization from Unity.

The full Track A milestone additionally remains blocked on a separate runtime
compiler/import migration and playable COMBAT LOOP TEST acceptance.

## Audit conclusion

After the second validation-loop repairs, no known static data-integrity blocker
remains in the **Phase 1 editor/export scope**. This is not a claim that Track A is
complete or that the branch is ready to merge. Unity validation and the later
runtime cutover are both still required.
