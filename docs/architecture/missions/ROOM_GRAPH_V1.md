# Room Graph v1

## Purpose

ROOM-001 defines the engine-independent source of truth for multi-room mission topology and the restart-safe runtime state that advances through it. It deliberately contains no scene loading, GameObject lookup, UI coordinates, door animation, map rendering, save transport, combat behavior, or reward authority.

The implementation is split into four owned layers:

- `Domain/Missions/Rooms`: immutable validated topology and deterministic definition fingerprints;
- `Contracts/Missions/Rooms`: runtime state, snapshot, result, and public authority contracts;
- `Application/Missions/Rooms`: the single mutable room-layout authority;
- `Content/Definitions/Missions/Rooms`: authored Level 1 two-room content.

## Authority boundary

`RoomGraphDefinitionV1` is the only graph-truth owner. Runtime state and snapshots never duplicate room entries, connections, targets, directionality, door links, ordering, or unlock rules. They contain only:

- the layout identity;
- the exact definition fingerprint;
- a monotonic operation sequence within the current attempt;
- per-room availability/current/visited/completed state;
- per-exit availability state;
- the deterministic snapshot fingerprint.

Consumers such as gameplay, transition presentation, maps, save transport, and authoring tools read the validated definition and `IRoomMissionLayoutV1`; they must not build a second topology model from scene names or UI positions.

## Stable identities

Every authored entity uses `StableId`:

- mission layout;
- room;
- room entry;
- connection;
- exit;
- optional door link.

Room order is explicit and unique. Entry and exit order is explicit and unique within the owning room. Canonical lists are sorted by order and then stable identity, so authoring collection order does not change fingerprints.

## Typed exits and connections

Connections are either:

- `OneWay`, containing exactly one exit; or
- `Bidirectional`, containing exactly two reciprocal exits.

Each exit has a semantic type:

- `Progression`;
- `Return`;
- `Optional`;
- `Secret`.

An exit identifies its source room and target entry, not only a target room. This preserves stable entry identity for later placement and transition adapters without making those adapters graph authorities.

A connection may reference one stable door-link identity. Door links are optional, but any declared link must exist, be used by exactly one connection, and may not dangle.

## Validation

`RoomGraphDefinitionV1.ValidateAndCreate` validates the entire graph before constructing an immutable definition. Invalid input returns ordered actionable issues and no partial graph.

Validation includes:

- missing or duplicate layout entities;
- duplicate room, entry, exit, connection, or door-link identities;
- duplicate room order and duplicate entry/exit order within a room;
- missing room/entry/exit endpoints;
- unsupported availability, directionality, or exit type;
- invalid connection exit count;
- self-links;
- invalid or missing unlock prerequisites;
- mismatched reciprocal exits;
- dangling, multiply-used, or unused door links;
- invalid start or terminal room;
- unreachable required rooms and terminal room.

A successful result supplies the only constructible `RoomGraphDefinitionV1` instance. Invalid definitions cannot become live runtime state.

## Runtime state

`RoomMissionLayoutV1` starts from the definition's configured start room:

- start room: `Available`, `Current`, `Visited`, not completed;
- other rooms: configured `Locked` or `Available`;
- exits: available only when not initially locked;
- targets reachable through an available exit from an available source are promoted to `Available`.

### Complete current room

Completing the current room is idempotent. The first completion:

1. marks the current room completed;
2. unlocks exits whose completed-room prerequisite is now satisfied;
3. promotes targets of newly available exits;
4. increments the runtime sequence;
5. exports a new canonical snapshot.

A repeated completion returns `NoChange` and does not advance sequence.

### Traverse

Traversal accepts one stable exit identity. It rejects unknown exits, exits from another room, locked exits, and locked targets without mutation. A successful traversal:

1. clears `Current` on the source while preserving visited/completed facts;
2. marks the target `Available`, `Current`, and `Visited`;
3. increments sequence;
4. exports a new canonical snapshot.

The model does not load a scene or animate a door. ROOMTRANS-001 may later translate an accepted traversal fact into presentation and scene behavior.

### Restart

`Restart()` rebuilds the exact initial state from the immutable definition, resets sequence to zero, and reproduces the initial snapshot fingerprint. Repeated restart at the initial state is a no-op.

## Snapshots and import

`RoomGraphSnapshotV1` uses string identities so external persistence data can be parsed and rejected before live state changes. Canonicalization uses length-prefixed tokens and SHA-256 (`sha256:<lowercase hex>`).

Import is atomic and fail-closed. It validates:

- schema version;
- layout identity;
- exact definition fingerprint;
- snapshot fingerprint;
- non-negative sequence;
- exact room and exit identity sets;
- exactly one current room;
- current/completed implies visited;
- locked rooms contain no progress;
- exit unlock prerequisites;
- available exits do not regress authored initially-available exits;
- available exits from available sources have available targets;
- canonical reconstruction matches the supplied fingerprint.

No field is applied until every check succeeds. Importing the current snapshot returns `DuplicateNoChange`.

## Authored Level 1 graph

`Level1RoomGraphDefinitionV1` contains exactly two required rooms:

1. `room.level1-entry` — start room, initially available;
2. `room.level1-terminal` — terminal room, initially locked.

The rooms are joined by `connection.level1-entry-terminal` and `door-link.level1-entry-terminal`:

- `exit.level1-entry-to-terminal`: typed `Progression`;
- `exit.level1-terminal-to-entry`: typed `Return`.

Both exits unlock when the entry room is completed. The resulting terminal room is then available and traversable. This is authored model data only and does not edit or compose a gameplay scene.

## Determinism

Definition and snapshot fingerprints are independent of input enumeration order. Canonical representation uses:

- invariant-culture numeric formatting;
- explicit enum integer values;
- explicit boolean `0`/`1` tokens;
- length-prefixed keys and values;
- stable ordering before hashing;
- SHA-256 over UTF-8 canonical text.

The focused tests verify equivalent definitions and reordered snapshots produce identical fingerprints.

## Test and proof boundary

Focused EditMode tests live in `ShooterMover.Tests.EditMode.Missions.Rooms` and cover valid graphs, invalid references, duplicates, typed exits, reverse links, door links, reachability, state transitions, restart, snapshot round trip, atomic rejection, deterministic fingerprints, debug projection, and absence of `UnityEngine` references.

The required Unity command writes `artifacts/test-results/ROOM-001-EditMode.xml`. A passing Unity result must not be claimed unless that XML exists and reports zero failures. Connector-side static inspection or authored tests are not Unity execution proof.

## Non-goals

- UI or mission-map presentation;
- scene composition or scene switching;
- door animation or collision;
- save-file transport or migration;
- combat, objectives, rewards, inventory, or run authority;
- procedural layout generation;
- editing `Stage1VisibleSlice.unity` or any shared project settings.
