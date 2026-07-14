# Stage 1 Short-Route Shell

## Status and ownership

EH-005 owns one additive, test-only route scene and its supporting fixture and
PlayMode smoke tests. The shell exists to verify deterministic multi-space
presentation lifecycle behavior before route gameplay, encounters, mission
state, rewards, persistence, or a `LevelGraph` exist.

Owned implementation paths:

- `Assets/ShooterMover/Tests/PlayMode/EvidenceHarness/Scenes/Stage1ShortRouteShell.unity`
- `Assets/ShooterMover/TestSupport/EvidenceHarness/Stage1ShortRouteFixture.cs`
- `Assets/ShooterMover/Tests/PlayMode/EvidenceHarness/Stage1ShortRouteSmokeTests.cs`
- this document

The scene and paired Unity metadata are single-owner serialized assets. Runtime
placeholder visuals are created with `HideFlags.DontSave`; they are not content
assets, prefabs, registry entries, or durable route data.

## Consumed contracts and foundations

The fixture consumes, without extending:

- the running `Bootstrap` composition root and its additive-scene baseline;
- EH-002 Evidence Configuration v1 through
  `EvidenceRunConfigurationLoader`;
- CS-007 Room Projection v1 identities, keys, sockets, connections, read-only
  state-reader port, and immutable lifecycle transitions;
- EH-004's convention that test-owned evidence scenes are local, planar,
  resettable, and loaded additively from Bootstrap.

No accepted requirement, planning artifact, generated task artifact, runtime
Domain file, content package, shared registry, build setting, or gameplay
implementation is changed.

## Stable marker projection

The shell declares exactly five local marker anchors. Their transforms are
presentation coordinates only; stable IDs carry cross-space identity.

| Load order | Marker | Kind | Room ID | Projection ID | Local position |
|---:|---|---|---|---|---|
| 0 | `route.start` | Start | `room.short-route-start` | `projection.short-route-start-v1` | `(-4, -2, 0)` |
| 1 | `route.arena-entry` | Arena entry | `room.short-route-arena` | `projection.short-route-arena-v1` | `(-2, 1.5, 0)` |
| 2 | `route.connector` | Connector | `room.short-route-connector` | `projection.short-route-connector-v1` | `(0, 0, 0)` |
| 3 | `route.review-end` | Completion review marker | `room.short-route-review` | `projection.short-route-review-v1` | `(2.5, 2, 0)` |
| 4 | `route.restart` | Restart return marker | `room.short-route-restart` | `projection.short-route-restart-v1` | `(4, -2, 0)` |

“Completion review” is a spatial review endpoint, not mission completion truth.
“Restart” is a test-navigation marker, not a durable checkpoint or session
reset authority.

## Stable socket connections

Connections serialize strings only. No connection stores a `GameObject`,
`Transform`, component, scene handle, prefab, or reference into another
additive room.

| Connection | From | Outbound socket | To | Inbound socket |
|---|---|---|---|---|
| `connection.start-arena` | `route.start` | `socket.start-out` | `route.arena-entry` | `socket.arena-in` |
| `connection.arena-connector` | `route.arena-entry` | `socket.arena-out` | `route.connector` | `socket.connector-in` |
| `connection.connector-review` | `route.connector` | `socket.connector-out` | `route.review-end` | `socket.review-in` |
| `connection.review-restart` | `route.review-end` | `socket.review-out` | `route.restart` | `socket.restart-in` |
| `connection.restart-start` | `route.restart` | `socket.restart-out` | `route.start` | `socket.start-in` |

At runtime the fixture resolves each endpoint to a
`RoomProjectionIdentity`, creates compatible `RoomSocket` values, and validates
an immutable `RoomConnection`. An unknown endpoint fails closed and is reported
as validation data; it never fabricates an alternate route.

## Deterministic lifecycle order

Each marker owns an independent CS-007 `RoomProjectionLifecycle` value.
Lifecycle state is disposable presentation state only.

- Enter/load order: start → arena entry → connector → review end → restart.
- Leave/unload order: restart → review end → connector → arena entry → start.
- Reload order: start → arena entry → connector → review end → restart.
- Repeating an already-completed enter, begin-unload, complete-unload, reload,
  or interrupted-unload recovery uses CS-007 `NoChange` semantics.
- An interrupted unload retains its projection key, can resume to `Loaded`, or
  can be recovered explicitly by `Reload`.
- Completing scene unload destroys all fixture objects and returns to the
  Bootstrap-only baseline.

The fixture snapshot records marker order, connection hashes, projection phase,
projection-read count, lifecycle operations, and the resolved EH-002 evidence
configuration fingerprint. It records no clear state, objective, checkpoint,
reward, route unlock, completion, save, or persistence fact.

## Read-only projection boundary

`Stage1ShortRouteProjection` is sealed and exposes getter-only scalar data:
marker ID, marker kind, load order, and local presentation coordinates. The
fixture reads it only through `IRoomProjectionStateReader`.

Known keys return `Found`. Unknown keys return `UnknownKey` with no fallback
value. The shell has no `IRoomMissionCommandSubmitter`, submits zero mission
commands, and cannot assert any durable mission transition.

## Manual test controls

Run `Bootstrap` in Play Mode, load the short-route shell through the fixture or
test, and use:

- **Right Arrow / D**: advance to the next marker;
- **Left Arrow / A**: move to the previous marker;
- **R / Home**: return immediately to `route.start`.

The yellow test cursor should follow the five labeled anchors in the declared
order. Advancing from `route.restart` returns to `route.start` in one input.
The Bootstrap camera remains the only camera.

## Automated verification matrix

`Stage1ShortRouteSmokeTests` covers:

| Requirement | Test coverage |
|---|---|
| Marker uniqueness and stable order | exact marker, kind, load-order, anchor, and stable-ID validation |
| No cross-boundary object references | connection bindings contain five strings only; projection DTO exposes no Unity object |
| Lifecycle order and idempotence | repeated enter, reverse leave, repeated leave, forward reload, repeated reload |
| Repeated additive load/unload/reload | byte-equal marker/lifecycle snapshot after complete scene unload and reload |
| Interrupted unload | begin, repeated begin, resume, repeated resume, and reload recovery |
| Missing connection | unknown endpoint reports explicit errors and `missing-endpoint`, then restores cleanly |
| Projection read-only behavior | `Found`, explicit `UnknownKey`, getter-only DTO, zero command submissions |
| Duplicate shell protection | requests during an in-flight load and after load are rejected |
| Manual route semantics | cursor visits every marker, loops restart to start, and supports immediate restart |

The first smoke test emits the route marker and lifecycle snapshot to the Unity
Test Runner log. The lifecycle-order test emits its operation proof separately.

## Required proof record

### Stage1ShortRouteSmokeTests log

Status: **Pending Unity Test Runner execution**.

Record the pinned-editor PlayMode result and attach or paste the complete
`Stage1ShortRouteSmokeTests` log in the EH-005 pull request. All tests must pass
without retained scenes, objects, or in-flight operations.

### Route marker and lifecycle snapshot

Status: **Pending Unity Test Runner execution**.

Use the snapshot logged by
`MarkersConnectionsAndProjectionSurface_AreStableUniqueAndLocal`. Confirm that
it contains the expected schema, five markers, five connections, forward load
order, reverse unload order, zero mission-command submissions, and only
presentation lifecycle phases.

### Human route-shell review note

Status: **Pending human playable review**.

Reviewer record:

- Reviewer:
- Date:
- Unity editor revision:
- Result: Pass / Fail
- Notes:

Checklist:

- [ ] The five spaces are visually distinct and their order is immediately clear.
- [ ] Right/D and Left/A move predictably between adjacent markers.
- [ ] Advancing from restart returns to start in one action.
- [ ] R/Home returns rapidly to start from every marker.
- [ ] Additive load retains the running Bootstrap composition root and camera.
- [ ] Unload returns to the Bootstrap-only baseline.
- [ ] No marker or label implies durable clear, reward, objective, checkpoint,
      route unlock, mission completion, or persistence truth.

## Limitations and non-goals

- This is placeholder test geometry, not Stage 1 route content.
- It does not implement a `LevelGraph`, traversal eligibility, doors, encounters,
  enemies, rewards, objectives, checkpoints, mission completion, save data, or
  persistence.
- It does not load the EH-004 arena as a child room; `route.arena-entry` is only
  the stable projection marker where later integration may connect.
- Editor PlayMode uses `EditorSceneManager.LoadSceneAsyncInPlayMode` so this
  test-owned scene does not require a shared Build Settings edit. Player-build
  scene registration is outside EH-005.

## Rollback

Delete the owned scene, fixture, smoke test, this document, and the three paired
Unity `.meta` files. No save schema, content registry, project setting, runtime
Domain state, or durable mission data requires migration or cleanup.
