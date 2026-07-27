# Level Grid Authoring V2 — Self-Audit

## Scope

Static self-review of draft PR #333 after the initial implementation of Track A —
Level Grid Authoring V2.

The audit focused on identity stability, room movement, endpoint ownership,
connection integrity, deletion/undo behavior, draft safety, production publishing,
folder export, optional room labels, and practical use with roughly 100 rooms.

## Findings fixed during the audit

### 1. Room identity was not part of the V2 production gate

**Severity:** high

The initial endpoint validator indexed rooms but did not independently report
malformed or duplicated room IDs. A graph with duplicated room identities could
therefore reach V2 publishing even though the exported map would contain
ambiguous nodes.

**Fix:** added combined room-and-endpoint validation. Production validation now
reports:

- invalid room stable IDs;
- duplicated room stable IDs;
- overlapping authored grid footprints.

Moving a room still preserves its stable identity. Duplicating a room requires a
new stable identity.

### 2. Invalid draft data could claim the same folder twice

**Severity:** high

When two malformed draft rooms shared an ID, both could resolve to the same
existing room folder. A new room could also select a generated folder name that
already belonged to an old or unrelated room. Generated `room.json` and
`doors.json` files could then be written beside the wrong sidecars.

**Fix:** room folders are now claimed once per export. Existing folders are reused
only when their `room.json` contains the matching stable room ID and they have not
already been claimed. New folders receive a non-destructive numeric suffix when
the preferred generated name already exists.

Draft export therefore remains allowed without silently merging two authored
rooms into one folder.

### 3. The Problems panel could become stale after Inspector edits

**Severity:** medium

The initial panel refreshed after hierarchy changes and Undo/Redo, but a normal
Inspector edit such as changing `traversable`, a door side, or a connection
reference did not immediately refresh its result.

**Fix:** editor Undo-property modifications now schedule a bounded revalidation of
the affected level root and repaint open Problems windows and Scene views.

## Three-room acceptance example added

A one-click editor command now creates:

```text
Starter Room (0,0) -> Room 1,0 -> Room 2,0
```

with four stable door endpoints and two bidirectional stable connections.

Command:

`Tools > Shooter Mover > Level Design > Create Three-Room Starter Example`

The whole creation is one Unity Undo group. Only the first room has an explicit
optional display name. The other two use automatic coordinate labels.

The exact first-export folder representation and example JSON are documented in:

`docs/authoring/LEVEL_GRID_AUTHORING_V2_THREE_ROOM_EXAMPLE.md`

## Behaviors reviewed as correct by static inspection

- room grid position is separate from stable room identity;
- room display names are optional;
- connection records export exact `room_id + door_id` endpoints;
- multiple stable door endpoints may exist on one side;
- edge-managed and fixed placement are represented independently;
- deleting a room removes attached connection records before deleting the room;
- neighbouring room-owned endpoints are not deleted;
- the deletion is grouped as one Unity Undo operation;
- ordinary deletion is non-modal;
- unusually large deletion uses the explicit destructive threshold;
- draft validation keeps unconnected traversable doors as warnings;
- production validation promotes unresolved traversable doors to errors;
- content sidecars are created only when missing;
- moving a room reuses its folder by stable room ID rather than coordinate;
- the exported map is already a stable node-and-line graph.

## Remaining manual verification

This connector environment cannot launch Unity. The following claims are
therefore intentionally not made:

- Unity 6000.3.19f1 compilation succeeds;
- editor scripts import without API/version errors;
- the three-room creation command produces the expected Scene hierarchy;
- production validation passes for the generated example;
- draft and production folders serialize exactly as documented;
- room deletion and Ctrl+Z restoration behave correctly in the Scene view;
- gizmo and Problems-panel performance is comfortable with 100+ rooms;
- no missing-script or serialization warnings appear after domain reload.

The PR should remain draft until those manual checks are completed.

## Audit conclusion

No remaining static data-integrity blocker was found after the fixes above. The
architecture is suitable for the three-room example and for larger levels, but
Unity import and hands-on editor acceptance remain required before merge.
