# VS-003 — Visible Slice Blaster Turret Presentation

This folder is a detachable, read-only presentation package for the accepted
EN-007 Blaster Turret. It contains no enemy, combat, projectile, encounter,
mission, reward, save, registry, or persistence authority.

## Accepted input boundary

`IVisibleSliceBlasterTurretPresentationSource` exposes one getter-only operation:
`TryReadSnapshot`. The final VS-007 composition owner may project already
accepted facts into `VisibleSliceBlasterTurretSnapshot`:

- EN-002 current and maximum health plus active/destroyed lifecycle;
- EN-007 observed cadence phase, elapsed/duration, warning count, accepted shot
  result, fixed-step identity, and restart generation;
- the accepted damage-event sequence;
- injected reduced-effects and grayscale proof modes.

The source adapter must never call `ExecuteFixedStep`, `BlasterTurretCadence.Step`,
`BlasterTurretAuthority.Apply`, `WeaponMount2DAdapter.ExecutePlan`,
`RestartSession`, target setters, projectile execution, encounter transitions, or
persistence services while producing a snapshot. VS-003 does not contain such
calls.

The presentation phase mapping is intentionally explicit:

- EN-007 idle/non-warning observation -> `Idle`;
- EN-007 warning observation -> `Warning`;
- an accepted `ShotExecuted` observation -> one `Firing` presentation frame;
- EN-007 recovery observation -> `Recovery`;
- EN-002 zero health/destroyed -> `Destroyed`;
- accepted inactive/deactivated lifecycle -> `Deactivated`.

Health zero always wins over a scheduled firing observation. The projector
therefore shows `X DESTROYED` and never invents a firing cue.

## Visual package

`VisibleSliceBlasterTurretPresentation.prefab` contains only:

- `Transform`;
- `SpriteRenderer`;
- `VisibleSliceBlasterTurretPresenter`.

Its sprite references the VS-001 asset at:

`Assets/ShooterMover/Art/Prototype/Stage1VisibleSlice/enemy_standing_turret_weak.png`

The renderer reference is replaceable through `SetBodySprite` or by replacing the
prefab's `SpriteRenderer.sprite`. No gameplay code changes are required for later
production art.

### Accepted art revision

VS-003 consumes the clean transparent turret replacement recorded by the VS-001
art refresh. The replacement retains the established Unity GUID, so this prefab
needs no rebinding and the original source revision remains available through
the VS-001 provenance record. This is prototype visual proof, not a final-art
claim.

## Readability contract

The warning is redundant by construction:

- shape: triangle, continuous rail, and four perpendicular ticks;
- text/glyph: `! TRIANGLE + RAIL`;
- count: two-digit warning count;
- timing: remaining seconds.

Color is supplemental. Grayscale keeps the shape/text/count/timing identity.
Reduced-effects mode removes only optional pulse and motion; it does not change
warning visibility, count, timing, glyph, rail, triangle, or ticks. A simultaneous
damage reaction adds `HIT` while preserving the warning. Restart-generation
changes immediately clear stale warning/damage presentation.

## Deterministic transition trace

The focused fixture freezes this expected projection sequence:

```text
generation=0 step=0  Idle      HP 100/100
generation=0 step=1  Warning   count 04 remaining 0.75s
generation=0 step=1  Warning   HP 75/100 + HIT
generation=0 step=2  Firing
generation=0 step=3  Recovery
generation=0 step=4  Warning   HP 25/100 count 01 remaining 0.25s
generation=0 step=5  Destroyed HP 0/100; no firing cue
generation=1 step=0  Idle      HP 100/100; stale transient state cleared
```

The exact byte-stable trace is emitted by
`VisibleSliceBlasterTurretPresentationTests.StateTransitionTrace_IsDeterministic`.

## Focused verification

Pinned editor:

```text
"C:\Program Files\Unity\Hub\Editor\6000.3.19f1\Editor\Unity.exe" -batchmode -nographics -projectPath . -runTests -testPlatform PlayMode -testFilter ShooterMover.Tests.PlayMode.VisibleSliceBlasterTurretPresentation.VisibleSliceBlasterTurretPresentationTests -testResults Artifacts\TestResults\VS-003-VisibleSliceBlasterTurret-PlayMode.xml -logFile Artifacts\Logs\VS-003-VisibleSliceBlasterTurret-PlayMode.log -quit
```

Required captures after VS-007 binds the prefab:

1. default warning/fire/recovery/damage/deactivation sequence;
2. reduced-effects sequence with identical warning identity and timing;
3. grayscale sequence with distinguishable shape/text/count/timing;
4. transparent-edge inspection against the room floor with no checkerboard or
   opaque source rectangle visible.

The tests cover idle, warning, firing, recovery, damage, numeric/normalized
health, zero health, deactivation, restart generation, simultaneous
warning/damage, deterministic trace, getter-only source shape, prefab art
reference, and source/static authority audits.

## Rollback

Remove these two task-owned roots and their exact leaf metadata:

- `Assets/ShooterMover/Runtime/Presentation/VisibleSliceBlasterTurret/`
- `Assets/ShooterMover/Tests/PlayMode/VisibleSliceBlasterTurretPresentation/`

EN-002, EN-003, EN-007, target selection, cadence, projectile execution,
encounters, and persistence remain unchanged; no migration or scene repair is
required.
