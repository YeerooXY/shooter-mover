# Room Runtime V1

## Purpose

`RoomRuntimeAuthorityV1` is the engine-neutral authority for room occupancy and room-clear state within one concrete run/runtime instance.

It is bound to the existing validated `RoomGraphDefinitionV1`. It does **not** define another room graph or placement schema. The graph continues to own room IDs, connectivity, exits, start/terminal identities, and topology. Unity-authored `RoomContentDefinition2D` continues to own prefab and placement data.

The runtime owns only:

- one concrete room-runtime instance identity;
- one lifecycle generation;
- the active room;
- atomic registration of each room's authored occupants;
- occupant entity identity, definition identity, and room-clear role;
- retained terminal facts for the current lifecycle;
- the one-time transition from uncleared to cleared;
- immutable connected-exit eligibility projections;
- replay-safe operations and conflicting-operation rejection;
- restart reconstruction from the registered authored initial state.

It does not own mission completion, traversal, doors, scenes, rewards, XP, results routing, enemy behavior, or presentation.

## Identity boundaries

The model keeps these concepts separate:

- `RuntimeInstanceStableId`: the concrete room-runtime instance for a run;
- `RoomStableId`: the existing graph room identity;
- `EntityStableId`: one concrete registered occupant;
- `DefinitionStableId`: the occupant's reusable content/definition identity;
- `OperationStableId`: one command/replay identity;
- `LifecycleGeneration`: the current restart generation.

Two occupants may share the same definition identity and still remain independent because terminal state is keyed only by concrete entity identity.

## Registration

A Unity adapter registers the complete authored occupancy set for one graph room with `RegisterRoomOccupantsCommandV1`.

Registration is atomic. An empty set is valid and immediately clears the room. Duplicate entity identities in one room are rejected. A second independent registration of the same room is rejected; an exact replay of the original operation is a duplicate no-op.

The registered set becomes the authored initial state used by restart. Runtime terminal facts never rewrite that authored set.

## Clear roles

| Role | Blocks clear while non-terminal |
|---|---:|
| `RequiredEnemy` | yes |
| `ObjectiveEntity` | yes |
| `OptionalEnemy` | no |
| `NonParticipant` | no |

Clear logic does not inspect hierarchy names, package names, object counts, prefab paths, enemy class names, or Unity object discovery.

## Terminal facts and retention

`ReportRoomOccupantTerminalCommandV1` submits a positive terminal fact for one registered entity identity.

Terminal facts are accepted for active or inactive rooms, provided the runtime identity and lifecycle generation match. This allows late accepted facts to remain deterministic while a room is transitioning out of presentation.

The first operation that makes every blocking occupant terminal emits one `RoomClearTransitionV1`. Exact operation replay and later terminal notifications for an already-terminal entity never emit a second transition.

Leaving and returning changes only room activation. Registered occupants and terminal state remain retained for the lifecycle.

## Exit eligibility

Each immutable `RoomOccupancyProjectionV1` contains exactly the exits connected from that room in `RoomGraphDefinitionV1`.

A connected exit is projected eligible only when:

1. the room is active;
2. occupancy has been registered; and
3. the room is cleared.

This projection is presentation eligibility, not mission traversal authority. `RoomMissionLayoutV1` still owns completion, unlock, and traversal semantics. A live coordinator must require both authorities where appropriate rather than copying either authority's state.

## Replay and conflict behavior

Every mutation command carries an operation identity. The runtime stores the canonical command payload for every first-seen operation.

- exact reuse of the same operation and payload returns `DuplicateNoChange`;
- reuse of the same operation identity with another payload returns `Rejected` with `room-operation-id-conflict`;
- a different operation reporting an already-terminal entity returns `NoChange`;
- stale lifecycle generations reject without mutation;
- operation history survives restart, so a pre-restart operation identity cannot be repurposed in the next generation.

## Restart

`RestartRoomRuntimeCommandV1` increments the lifecycle generation, restores every registered room's authored occupant set with all occupants non-terminal, marks the graph start room active, marks other rooms inactive, and recomputes clear state.

Rooms containing only optional/non-participating occupants are therefore cleared in the restored authored state. Restart does not emit room-clear transitions; the coordinator can read the immutable projection after restart.

## Unity adapter boundary

A Unity adapter may:

- obtain concrete authored entity IDs and definition IDs;
- assign the explicit clear role supplied by authored/package data;
- register a complete room occupancy set;
- forward accepted terminal facts with operation and generation identities;
- activate a graph room after accepted traversal;
- enable/disable colliders and renderers from immutable occupant projections;
- enable a door from immutable connected-exit eligibility.

It must not:

- scan hierarchy names or package names;
- count scene objects to infer clear;
- call `FindObjectsByType` for occupancy;
- mutate room state directly;
- infer terminal state from a missing GameObject;
- grant mission completion, rewards, XP, or navigation.

## ROOM-LIVE-001 handoff for `Stage1VisibleSliceController`

`ROOM-RUNTIME-001` intentionally does not modify the 2,192-line controller. `ROOM-LIVE-001` should make surgical changes in the following exact locations.

### Fields

Replace the occupancy/clear responsibility of:

- `roomProjections` (`Dictionary<StableId, DemoRoomProjection>`);
- the nested `DemoRoomProjection.enemyDestroyedReaders` list;
- `DemoRoomProjection.AreAllEnemiesDestroyed`;
- `DemoRoomProjection.RegisterEnemyDestroyedReader`.

Add one `RoomRuntimeAuthorityV1` field plus small Unity-only bindings from concrete occupant entity IDs to their scene adapters. Keep `roomMissionLayout`, `RoomContentDefinition2D`, and door fields because they still own topology/placement lookup and presentation.

### `BuildSession()`

Immediately after creating `RoomMissionLayoutV1`, create `RoomRuntimeAuthorityV1` from the **same** `roomMissionLayout.Definition` and one concrete run-scoped runtime identity. Do not call `Level1RoomGraphDefinitionV1.Create()` a second time for a separate topology truth.

### `BuildAuthoredRooms()`

Replace `new DemoRoomProjection(...)` and the two `RegisterEnemyDestroyedReader(...)` branches.

For each authored room:

1. build its Unity objects as today;
2. obtain each concrete placement/entity ID and reusable definition ID;
3. obtain an explicit clear role from authored/package projection data;
4. submit one atomic `RegisterRoomOccupantsCommandV1`, including an empty list when appropriate;
5. retain only presentation bindings keyed by entity identity;
6. keep door construction as presentation wiring.

The current weapon/enemy-specific construction branches may remain until their own live migrations, but they must no longer decide room participation or clear.

### Accepted terminal facts

The current controller polls droid/turret state through delegates. Replace that with one terminal-fact submission per accepted enemy terminal transition:

- mobile droid accepted terminal fact -> `ReportTerminal(...)` using its concrete entity ID;
- turret accepted terminal fact -> `ReportTerminal(...)` using its concrete entity ID.

Use event/command identities sourced from the accepted terminal authority. Do not mint a second attribution truth from a destroyed GameObject or package name.

### `RefreshArenaFlow()`

Remove `currentProjection.AreAllEnemiesDestroyed`.

Read the current immutable room projection instead. When a `RoomClearTransitionV1` is accepted, let the mode coordinator call `roomMissionLayout.CompleteCurrentRoom()` exactly once. Door presentation should read `RoomOccupancyProjectionV1.IsExitEligible(exitId)` and the corresponding mission-layout exit state; room runtime itself must not call mission completion or traversal.

Keep player-position door-lane detection and terminal mission completion in the controller/mode coordination layer.

### `SwitchRoom(...)`

After `roomMissionLayout.Traverse(...)` succeeds, call `RoomRuntimeAuthorityV1.ActivateRoom(...)` with the exact destination room, operation identity, runtime identity, and lifecycle generation. Then render the returned immutable projection.

Do not reactivate occupants by enemy type or room-name comparison.

### `EnforceCurrentRoomProjection()`

Replace the droid/turret-specific terminal checks with a small generic renderer over registered entity bindings:

- active room + non-terminal occupant -> active renderer/collider/session adapter;
- active room + terminal occupant -> retained terminal presentation;
- inactive room -> presentation inactive without altering terminal state.

### `SetMobileDroidProjectionActive(...)` and `SetTurretProjectionActive(...)`

Keep only package-specific activation/deactivation mechanics that cannot yet be generalized. Their decision input must be the immutable occupant projection, not `entryRoom` or enemy class identity. Remove them when ENEMY-LIVE-001 provides one shared enemy adapter boundary.

### `QuickRestart()`

Before re-rendering room content, submit one `RestartRoomRuntimeCommandV1` using the current room-runtime lifecycle generation and a unique restart operation identity. Use the returned generation for all subsequent room terminal/activation commands. Then restart Unity enemy adapters and render the restored projection.

Do not clear and rebuild occupancy by scanning scene objects.

### Nested type removal

Delete the entire nested `DemoRoomProjection` type after the live authority path is proven.

## Validation scope

Focused EditMode coverage is provided in `RoomRuntimeAuthorityTests` for:

- zero, one, and many occupants;
- required versus optional roles;
- duplicate terminal notification;
- conflicting operation identity;
- leave and return retention;
- restart and stale generation;
- multiple runtime instances;
- identical definitions with distinct entity identities;
- inactive rooms;
- connected exit eligibility;
- package/hierarchy-name independence;
- absence of UnityEngine references in the runtime assemblies.

The intended focused Unity command is:

```text
Unity -batchmode -projectPath . -runTests -testPlatform EditMode -testFilter ShooterMover.Tests.EditMode.Missions.Rooms.RoomRuntimeAuthorityTests -testResults Temp/room-runtime-001-editmode.xml
```

`-quit` is intentionally omitted because `-runTests` exits Unity automatically.
