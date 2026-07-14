# Room Projection v1

## Status and dependency

Room Projection v1 defines immutable, engine-independent contracts for loading,
reading, refreshing, connecting and unloading room presentations without giving
a room durable mission authority.

It consumes merged **CS-006 Mission Messages v1**:

- `MissionSequence` identifies the committed position represented by a projection
  key;
- `MissionCommandEnvelope` is the only room-to-mission write request;
- `MissionCommandEvaluation` reports protocol admission only, not mission-domain
  acceptance.

`MissionRunState` remains the sole authoritative owner of permanent clear state,
rewards, routes, checkpoints, objectives and mission completion.

## Identity

`RoomProjectionIdentity` separates two identities:

- `RoomId` is the stable durable identity referenced by mission state;
- `ProjectionId` identifies one loaded presentation instance.

This distinction allows two rooms to be loaded additively, and also allows a room
to be unloaded and projected again, without making a Unity object or lifecycle
instance the durable room record.

## Projection keys

`RoomProjectionKey` contains:

```text
run_id
room_id
mission_sequence
```

The key identifies the authoritative mission slice a room asks to present. A key
must target the same durable `RoomId` as the lifecycle identity. Refresh within a
loaded projection must remain in the same run and may not regress to a lower
mission sequence.

The projection contract does not define mission state fields. A future
authoritative application service supplies an immutable projection DTO through
`IRoomProjectionStateReader.Read<TProjection>`.

Reads return one explicit status:

- `Found` with the requested immutable projection DTO;
- `UnknownKey` with no fabricated fallback state.

Unknown keys are not interpreted as uncleared, zero rewards, a closed route, an
inactive checkpoint or an incomplete objective. Presentation must fail closed or
request a later valid key.

## Services and authority boundary

`RoomProjectionServices` supplies exactly two ports:

1. `IRoomProjectionStateReader` reads an authoritative projection by key;
2. `IRoomMissionCommandSubmitter` submits a typed `MissionCommandEnvelope` for
   authoritative validation.

There are intentionally no room-facing methods such as `SetCleared`,
`GrantReward`, `OpenRoute`, `ActivateCheckpoint`, `CompleteObjective`, `Save` or
`Persist`. A room may request consideration through the typed CS-006 command
vocabulary, but only mission-domain logic may accept a transition and mutate
`MissionRunState`.

Protocol admission through `MissionCommandEvaluation.IsAccepted` means only that
identity, payload version and expected sequence passed the CS-006 gate. It is not
proof that a room is permanently clear or that any other durable mission fact
changed.

## Sockets and connections

`RoomSocket` explicitly binds:

```text
projection identity
socket StableId
direction = inbound | outbound | bidirectional
```

`RoomConnection` joins two compatible sockets from distinct loaded projections.
Endpoint order is canonical, so constructing the same connection in reverse
produces equal values and deterministic hashes.

Connection compatibility is limited to address and direction:

- outbound may connect to inbound;
- bidirectional may satisfy either side;
- two sockets on the same projection do not form a cross-room connection;
- incompatible directions are rejected;
- asking a connection for an unknown endpoint is rejected.

The connection does not create or mutate a `LevelGraph`, unlock a route, load a
scene, or prove traversal eligibility. Those decisions belong to later authored
graph and mission-domain work.

## Lifecycle

`RoomProjectionLifecycle` is immutable and functional. Every operation returns a
`RoomProjectionTransition` containing the current and next state. Rejected and
idempotent transitions retain the same state object.

```text
                 Load(key)
       +--------------------------+
       |                          v
  +----------+               +----------+
  | Unloaded |               |  Loaded  |
  +----------+               +----------+
       ^                          |   ^
       | CompleteUnload           |   | Refresh(newer key)
       |                          |   | Reload(key)
  +-----------+  BeginUnload      |   |
  | Unloading | <-----------------+   |
  +-----------+                      |
       | ResumeInterruptedUnload ----+
       | Reload(key) -----------------+
```

### Load

- `Unloaded + Load(key)` becomes `Loaded`.
- Repeating `Load` with the active key is `NoChange`.
- Loading a different key while already loaded, or loading while unloading, is
  rejected; callers use `Refresh`, `Reload`, or interrupted-unload recovery
  explicitly.

### Refresh

- Refresh is valid only while loaded.
- Repeating the active key is `NoChange`.
- A higher mission sequence in the same run replaces only the presented key.
- A lower sequence is rejected as `StaleProjectionKey`.
- A different run is rejected as `DifferentRun`.

Refresh never refreshes shop stock, rewards, objectives, checkpoints, route
truth or permanent room completion by itself. It only asks presentation to read
a newer authoritative projection.

### Reload

- Reload may restore an unloaded projection, replace a loaded projection key, or
  recover from the unloading phase.
- Repeating the same loaded key is `NoChange`.
- A reload cannot regress below a retained key while loaded or unloading.
- After completed unload, no key is retained; the caller must supply the current
  authoritative key again.

### Unload and interruption

- `BeginUnload` moves `Loaded` to `Unloading`; repeating it is `NoChange`.
- `CompleteUnload` moves `Unloading` to `Unloaded` and discards the presentation
  key; repeating completion is `NoChange`.
- Completing unload directly from `Loaded` is rejected.
- `ResumeAfterInterruptedUnload` returns `Unloading` to `Loaded` with the same
  key; repeating recovery while loaded is `NoChange`.

The unloading phase does not mark a room clear, abandon rewards, close routes,
deactivate checkpoints, reset objectives or write persistence.

## Two additive rooms

Two additive rooms use distinct `RoomProjectionIdentity` values and independent
lifecycles. They may share a run and committed mission sequence while retaining
different durable room IDs and projection IDs. Refreshing, unloading or
recovering one lifecycle has no effect on the other.

Cross-room communication uses stable socket addresses, connection values and
Mission Messages v1 commands. It never uses direct scene-object references.

## Immutability and engine boundary

Concrete contract values are sealed and expose getter-only state. Lifecycle
changes create new values. The namespace has no `UnityEngine`, scene, prefab,
`GameObject`, `MonoBehaviour`, `ScriptableObject`, transform, collider, camera or
serialization dependency.

The generic projection reader does not define a mutable universal room model.
Each future projection DTO remains owned and versioned by its defining contract.

## Explicit non-goals

Room Projection v1 does not add:

- `MissionRunState` or any second durable mission-state owner;
- room, encounter, reward, route, checkpoint or objective rules;
- scenes, prefabs, additive-scene loading, addressables or room implementations;
- `RoomDefinition`, content packages, manifests or serialized assets;
- `LevelGraph`, graph nodes, graph mutation or traversal decisions;
- persistence, snapshots, journals, recovery, save migration or reload storage;
- networking, transport, remote services or retry timers;
- direct scene-object references or implicit Unity execution ordering.
