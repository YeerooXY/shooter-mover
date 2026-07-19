# ROOM-LIVE-001 — Authorable Room Runtime Foundation

## Scope and live-flow boundary

`ROOM-LIVE-001` adds the reusable authoring, authority-coordination, Unity
presentation, and real-enemy terminal-relay foundation required to migrate a live room
flow onto `ROOM-RUNTIME-001`.

The retained `Stage1VisibleSliceController.cs` and Stage 1 scene are intentionally not
modified because the task explicitly forbids controller edits. Consequently this pull
request must not be described as having replaced the existing Stage 1 production path.
It provides a complete reusable composition path, a real package-backed PlayMode proof,
and the handoff boundary for a later controller/scene migration.

## Authority ownership

`RoomRuntimeAuthorityV1` remains the only authority for:

- authored occupant registration;
- concrete occupant identities;
- active and terminal occupant state;
- room-clear state;
- lifecycle generation and restart reconstruction.

`RoomMissionLayoutV1` remains the room-graph traversal/completion state owner.

`RoomLiveRuntimeAuthorityV1` is the only public mutation boundary coordinating those two
systems. It does not publicly expose either mutable authority. Consumers receive
`IRoomLiveRuntimeQueryV1` and immutable projections only.

The coordinator delegates focused responsibilities to sealed collaborators:

- `RoomOperationJournalV1` — replay and operation-ID conflict handling;
- `RoomRetainedFactStoreV1` — collected-drop and opened-door facts;
- `RoomCompletionEvaluatorV1` — configured condition evaluation;
- `RoomDoorGatePolicyV1` — per-door gate decisions;
- `RoomTraversalCoordinatorV1` — atomic layout traversal and occupancy activation;
- `RoomLiveProjectionBuilderV1` — immutable live snapshots.

## Authorable definitions

`AuthorableRoomGraphDefinitionV1` is deterministic and engine-independent. Each room
contains:

- stable ID and display name;
- bounds;
- authored spawn points;
- enemy and prop placements with concrete instance identities;
- doors;
- exits and next-room/final-exit links;
- explicit exit meaning (`Progression`, `Return`, `Optional`, or `Secret`);
- completion conditions;
- per-door references to one or more condition IDs.

`RoomPresentationCatalog2D` separately resolves durable presentation IDs to Unity
prefabs. Unity object references are therefore not persisted as room-state identity.

## Completion, clear, and door semantics

The following facts are intentionally distinct in `RoomLiveRoomProjectionV1`:

- `IsCleared` — ROOM-RUNTIME-001 reports every blocking occupant terminal;
- `IsCompleted` — ROOM-001 accepted completion for the current visited room;
- `IsVisited` — the mission layout has entered the room;
- `IsCurrent` — the mission layout currently selects the room;
- `IsActive` — ROOM-RUNTIME-001 currently activates the room occupancy.

Completion conditions are executable data, not decorative metadata. Supported V1 kinds
are:

- `AlwaysSatisfied`;
- `AllBlockingOccupantsTerminal`;
- `CollectedDrop` for one exact drop-instance identity.

Each door references exact condition IDs. A door opens only when its room has been
visited and all of that door's referenced conditions are satisfied. Different doors in
the same room may therefore have different gates.

## Unity composition and real terminal facts

`RoomRuntimeComposition2D` is a thin command/presentation adapter. It exposes only the
read-only `IRoomLiveRuntimeQueryV1`, forwards commands through the coordinated authority,
and delegates object lifecycle to `RoomPresentationScene2D`.

For authored enemy placements, the renderer attaches:

- `EnemyActorTerminalFactSource2D`, which binds generically to a real
  `IEnemyActor2DAuthority` component or a component exposing one through a public
  `Authority` property;
- `RoomOccupantTerminalRelay2D`, which reads the accepted EN-002 destroyed state,
  verifies that actor identity equals the authored placed-instance identity, and forwards
  one deterministic terminal operation.

No enemy package names, room numbers, hierarchy names, or missing-GameObject inference
are used. The same relay works with the existing moving-droid runtime and Blaster Turret
package.

`ReportDropCollected(...)` accepts a retained fact only after the existing external
drop/pickup authority has accepted collection. The room runtime does not generate drops,
rewards, inventory, or pickup truth.

## Example Level 1 definition

`Level1AuthorableRoomDefinitionV1` demonstrates:

- Room 1 with one required moving droid and one non-participating prop;
- a forward door gated by the Room 1 clear condition;
- Room 2 with one required Blaster Turret;
- a return door gated by an independent entered-room condition and open immediately on
  accepted Room 2 entry;
- a final door gated independently by the Room 2 clear condition;
- return from Room 2 to Room 1;
- a final exit after Room 2 completion;
- concrete identities for every room, spawn, enemy, prop, condition, door, and exit.

Returning renders retained immutable state, so defeated enemy instances do not respawn.
Restart increments the ROOM-RUNTIME-001 lifecycle generation and rebuilds the authored
initial presentation.

## Graph extensibility and inherited limitation

Exit semantics are authored and are no longer inferred from numeric room order. The live
contract no longer requires a final exit to exist or restricts final exits to one terminal
room.

The underlying merged `RoomGraphDefinitionV1` currently still requires distinct start
and terminal rooms. That is an inherited ROOM-001 limitation, explicitly documented here
rather than hidden inside live-room ordering rules. Supporting a true one-room mission
requires changing ROOM-001 itself and is outside this controller-edit-free task.

## Failure behavior

- unknown exits and links fail closed without mutation;
- malformed target-room, target-spawn, condition, and door references fail during
  definition construction;
- actor/placed-instance identity mismatch fails closed in the terminal relay;
- closed doors reject traversal;
- missing presentation IDs reject before room construction;
- exact operation replay is a duplicate no-op;
- conflicting operation-ID reuse rejects;
- restart clears retained terminal, drop, opened-door, completion, and final-exit state.

## Verification boundary

Focused EditMode and PlayMode test source is included. The PlayMode suite instantiates and
configures the real `MobileBlasterDroidRuntime2D` and `BlasterTurretPackage`, applies real
lethal combat hits, and verifies that the generic relay drives configured gates and
retained room state.

Unity is unavailable in the implementation environment, so passing Unity XML is not
claimed. The pull request must remain draft until the documented focused Unity commands
produce and attach passing EditMode and PlayMode XML.
