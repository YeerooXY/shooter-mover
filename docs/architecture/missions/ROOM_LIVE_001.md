# ROOM-LIVE-001 — Authorable Room Runtime

## Purpose

`ROOM-LIVE-001` adds a reusable Unity composition boundary over the merged
`RoomRuntimeAuthorityV1` from `ROOM-RUNTIME-001`.

The live layer creates room presentation from authored definitions and retains room state
without putting room-specific branches into `Stage1VisibleSliceController`.

## Ownership

`RoomRuntimeAuthorityV1` remains the only occupancy and room-clear authority. It owns:

- authored occupant registration;
- concrete occupant identity;
- active/inactive room occupancy;
- terminal facts;
- clear transitions;
- lifecycle generation and restart reconstruction.

`RoomLiveRuntimeAuthorityV1` composes that authority with the existing
`RoomMissionLayoutV1` and owns only live coordination state:

- collected drop instance IDs;
- opened door instance IDs;
- current authored spawn point;
- accepted traversal;
- final-exit state;
- immutable live-room projections.

It never infers occupancy from Unity hierarchy state, prefab names, enemy types, or object
counts.

## Authorable definitions

`AuthorableRoomGraphDefinitionV1` is deterministic and engine-independent. Each
`AuthorableRoomDefinitionV1` contains:

- stable room ID and display name;
- bounds;
- spawn points;
- enemy and prop placements;
- concrete placement instance IDs;
- presentation references;
- doors and exit links;
- completion conditions;
- room targets or a final-exit link.

The Unity `AuthorableRoomGraphDefinition2D` asset converts inspector data into the durable
contract. `RoomPresentationCatalog2D` separately resolves stable presentation IDs to Unity
prefabs, so prefab references are not persisted as durable room-state data.

## Runtime composition

`RoomRuntimeComposition2D`:

1. builds the durable definition;
2. validates every presentation reference;
3. constructs `RoomLiveRuntimeAuthorityV1`;
4. instantiates the current room from placement and door definitions;
5. attaches generic concrete-instance relays;
6. renders door state from immutable projections;
7. rebuilds presentation after accepted traversal or restart.

There is no moving-droid, turret, prop-family, room-number, or Stage 1 branch in the
composition code. Enemy/prop packages forward accepted terminal facts through
`RoomPlacedInstance2D.ReportTerminal(...)` using their concrete instance and operation IDs.
Downstream drop presentation may use `RoomDropInstance2D` and the retained collected-drop
projection.

## Example Level 1 definition

`Level1AuthorableRoomDefinitionV1` demonstrates:

- Room 1: one required moving droid and one non-participating cover prop;
- a completion-gated forward door to Room 2;
- Room 2: one required blaster turret;
- a completion-gated return door to Room 1;
- a completion-gated final exit;
- concrete identities for every enemy, prop, door, spawn, and exit.

Returning to Room 1 renders its retained projection, so its defeated droid does not respawn.
Restart increments the occupancy lifecycle generation and rebuilds the authored initial state.

## Failure behavior

- unknown exits reject without mutation;
- malformed target-room or target-spawn links reject during definition construction;
- closed doors reject traversal;
- missing presentation IDs reject before room construction;
- exact operation replay is a duplicate no-op;
- conflicting reuse of an operation ID rejects;
- restart does not reuse terminal, drop, or door state from the previous generation.

## Controller boundary

`Stage1VisibleSliceController.cs` is intentionally unchanged. This task supplies the reusable
composition root and contracts required for a later scene migration without adding another
room-occupancy model or moving room logic into the retained controller.
