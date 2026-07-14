# Stage 1 benchmark arena shell

## Purpose and authority

EH-004 owns one additive, test-only 2D arena used to prove evidence-harness
setup, spatial sockets, reset determinism, and bounded scene lifecycle before any
movement, weapon, enemy, reward, or durable room-state implementation exists.

Owned implementation:

- `Assets/ShooterMover/Tests/PlayMode/EvidenceHarness/Scenes/Stage1BenchmarkArena.unity`
- `Assets/ShooterMover/TestSupport/EvidenceHarness/Stage1BenchmarkArenaFixture.cs`
- `Assets/ShooterMover/Tests/PlayMode/EvidenceHarness/Stage1BenchmarkArenaSmokeTests.cs`
- this document

The scene and fixture are verification infrastructure. They do not define a
`RoomDefinition`, encounter, checkpoint, objective, clear state, reward,
progression state, content registry, save data, or gameplay balance. Later
systems may project test actors into the named sockets, but they must not edit or
reinterpret the evidence marker IDs.

## Consumed foundation

The fixture consumes, without modifying:

- the UF-006 explicit composition-root lifecycle;
- the UF-007 `Bootstrap` scene and its one running `BootstrapSceneAdapter`;
- the UF-008 additive scene-loading convention; and
- the EH-002 canonical evidence configuration and strict loader.

`LoadFromCanonicalConfiguration` rejects invalid EH-002 bytes before requesting
the arena load. A successful load records the resolved configuration fingerprint
and run seed in the deterministic arena snapshot. The fixture requires Bootstrap
to be loaded and running, then loads this scene additively. It does not use
`GameObject.Find`, `FindObjectOfType`, scene-wide component discovery for socket
resolution, a service locator, or cross-scene serialized references.

## Authored 2D envelope

All authored transforms use `z = 0`, identity rotation, and unit scale.

| Element | Center or position | Size | Behavior |
|---|---:|---:|---|
| Arena shell | `(0, 0)` | `24 x 14` | test-only background placeholder |
| Camera bounds | `(0, 0)` | `24 x 14` | enabled trigger `BoxCollider2D`; no camera component |
| Collision north | `(0, 7.25)` | `25 x 0.5` | solid `BoxCollider2D` |
| Collision east | `(12.25, 0)` | `0.5 x 14` | solid `BoxCollider2D` |
| Collision south | `(0, -7.25)` | `25 x 0.5` | solid `BoxCollider2D` |
| Collision west | `(-12.25, 0)` | `0.5 x 14` | solid `BoxCollider2D` |

The four wall colliders form a combined envelope from `(-12.5, -7.5)` to
`(12.5, 7.5)`. Camera bounds remain strictly inside that collision envelope.
There are no `Rigidbody2D` components because the shell owns static geometry
only. There are no 3D colliders, rigidbodies, joints, cameras, lights, listeners,
or navigation behavior.

## Stable marker contract

Marker IDs are ordinal, case-sensitive, and unique. Their values are evidence
identities, not display labels.

| Marker ID | Kind | Position | Sorting order |
|---|---|---:|---:|
| `arena.shell.v1` | shell | `(0, 0)` | `-100` |
| `bounds.collision` | collision envelope | `(0, 0)` | `-95` |
| `bounds.camera` | camera bounds | `(0, 0)` | `-90` |
| `socket.player.primary` | player spawn | `(0, -4.5)` | `10` |
| `socket.target.north` | target spawn | `(0, 4.5)` | `20` |
| `socket.target.east` | target spawn | `(8, 0)` | `20` |
| `socket.target.south` | target spawn | `(0, -2)` | `20` |
| `socket.target.west` | target spawn | `(-8, 0)` | `20` |
| `socket.hazard.northwest` | hazard spawn | `(-6, 3)` | `30` |
| `socket.hazard.southeast` | hazard spawn | `(6, -3)` | `30` |
| `probe.performance.center` | performance probe | `(0, 0)` | `40` |
| `probe.performance.north` | performance probe | `(0, 6)` | `40` |
| `probe.performance.south` | performance probe | `(0, -6)` | `40` |
| `hook.combat.spawn` | empty combat hook | `(-10, 5.5)` | `50` |
| `hook.combat.cleanup` | empty combat hook | `(10, -5.5)` | `50` |

The two combat hooks are transforms only. They contain no spawning, damage,
weapon, target-selection, cleanup, reward, or encounter behavior.

## Placeholder readability

The fixture creates local `DontSave` one-pixel sprite placeholders so the empty
shell remains legible without an imported sprite, material, font, prefab, or
content-package dependency. Placeholder colors distinguish player, target,
hazard, performance, and empty combat-hook roles. Sorting is set from the marker
bindings and is included in the snapshot.

These visuals are prototype-only evidence aids. They are not release content,
final art, a palette decision, or a balance commitment. Unloading the scene
removes them with the scene; no object is marked `DontDestroyOnLoad`.

## Deterministic reset and snapshot

On enable, the fixture captures the authored object states. `ResetActiveArena`
then restores every authored local position, rotation, scale, and active flag,
reapplies placeholder visuals, validates the shell, and returns a canonical text
snapshot.

Snapshot v1 includes:

- schema and version;
- scene name;
- resolved EH-002 configuration fingerprint and run seed;
- camera and collision bounds;
- marker ID, kind, hierarchy path, and sorting order;
- object hierarchy, active flags, local transforms, component type names;
- renderer enabled/sorting state; and
- `BoxCollider2D` enabled, trigger, offset, and size state.

Unity instance IDs, allocation order, timestamps, machine paths, frame timing,
and runtime object hashes are deliberately excluded. Therefore two resets and an
unload/reload cycle compare authored structure rather than process-local Unity
allocation details.

A deleted or inactive required socket produces a stable `missing-socket:<id>`
validation error. Reset restores an inactive authored socket. A destroyed
authored transform fails explicitly because reset cannot recreate an altered
serialized hierarchy silently.

## Automated proof

Focused PlayMode fixture:

```text
ShooterMover.Tests.PlayMode.EvidenceHarness.Stage1BenchmarkArenaSmokeTests
```

Coverage includes:

- canonical EH-002 configuration load through the running Bootstrap scene;
- additive load, unload, and Bootstrap baseline restoration;
- unique and complete marker IDs;
- exact camera and collision bounds;
- absence of 3D components and non-planar transforms;
- transform and missing-socket drift followed by reset repair;
- byte-equal first and second reset snapshots;
- byte-equal unload/reload snapshots; and
- in-flight and already-loaded duplicate rejection.

The PlayMode assembly reaches the test-support fixture through reflection because
`TestSupport` currently compiles into `Assembly-CSharp`; EH-004 does not enlarge
the accepted assembly graph or edit an assembly definition.

Connector-only authoring cannot substitute for the required pinned-editor log.
Run the focused fixture in Unity `6000.3.19f1` and attach the resulting
`Stage1BenchmarkArenaSmokeTests` log to the draft pull request before merge.

## Manual shell-review note

**Status: pending pinned-editor review.**

Open `Bootstrap`, enter Play Mode, and invoke the arena through the canonical
EH-002 configuration. Record the following in the pull request:

1. editor revision and configuration fingerprint;
2. whether the `24 x 14` interior reads as one empty combat space at the canonical
   `1280 x 720` windowed viewport;
3. whether player, target, hazard, performance, and empty hook placeholders are
   distinguishable without implying final art;
4. whether collision walls surround the camera envelope cleanly;
5. whether two reset invocations leave the same visible layout; and
6. reviewer name/date plus pass or actionable finding.

No manual pass is claimed by this document. The pull request remains draft until
that note and the focused test log are supplied.

## Limitations and non-goals

- The task does not add this test scene to player build settings. A later owned
  Windows evidence runner may include it by scene name; editor PlayMode loads the
  exact asset path.
- No movement, aiming, input polling, weapons, projectiles, enemies, hazards,
  rewards, route logic, UI, diagnostics recorder, persistence, networking, or
  remote service is implemented.
- Performance probes are stable spatial markers only; they do not sample or log
  performance.
- Camera bounds are data represented by a trigger collider. The Bootstrap camera
  remains owned by UF-007 and is not moved or configured here.
- The shell does not own permanent room, mission, encounter, objective,
  checkpoint, reward, or completion truth.

## Rollback

Delete the owned scene, fixture, smoke test, document, and their inseparable Unity
metadata. No save migration, content repair, registry regeneration, package
change, project-setting restoration, credential cleanup, remote cleanup, or
gameplay rollback is required.
