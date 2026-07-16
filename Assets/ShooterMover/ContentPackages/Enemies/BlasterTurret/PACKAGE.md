# Blaster Turret package

## Role

`enemy.blaster-turret` is the fixed-position ranged enemy for Stage 1. The package owns
its tuning, fixed map-facing attack cone, temporary presentation, deterministic
fire/recover policy, prefab root, and package-local composition only.

## Accepted contracts consumed

- EN-001 `Stage1EnemyPackageDescriptor`: ordinary, Kinetic, Immovable, stationary
  positioning, accepted Blaster projectile, safe recovery, and line-of-fire telegraph.
- EN-002 `EnemyActorState` / `EnemyActorStepper`: authoritative health, death,
  idempotency, contact-disabled policy, and restart state.
- EN-003 enemy Unity 2D ports: zero-velocity decision projection, target intake, contact
  boundary, and fixed-step lifecycle.
- WP-003 `BlasterMachineGunPackage`: the normal one-projectile Blaster behavior module
  and immutable runtime profile.
- CB-009 `WeaponMount2DAdapter`: validated plan-to-Physics2D execution.
- WP-002 `ProjectileExecutionPlanAdapter` / `BoundedProjectile2D`: finite projectile
  spawning, owner exclusion, confirmed-hit translation, and session cleanup.

No weapon implementation, shared adapter, scene, registry output, persistence, reward,
or other enemy is modified.

## Tracking and directional eligibility

The turret captures one cardinal home facing from its authored transform. The prefab
can either remain a fixed-direction hazard or rotate toward an in-range player at a
bounded authored angular speed. It fires only when the target is inside the configured
cone around its current barrel direction. Projectiles remain non-homing and preserve
the barrel direction they had when fired.

When tracking is enabled and the player leaves range, the turret returns toward its
authored home facing at a separately configurable speed.

Moving outside the cone stops new attacks and resets pending cadence, but projectiles
already in flight remain active. Walls and solid props stop those projectiles through
ordinary Physics2D collision.

## Destroyed collision

`BlasterTurretAuthoring2D` exposes `Keep Collider When Destroyed`. Disable it for a
non-blocking wreck or enable it when the destroyed turret should remain solid cover.
Restart restores the authored collider state.

## Drag-and-drop scene authoring

`BlasterTurret.prefab` includes `BlasterTurretAuthoring2D`. A level author can drag any
number of copies into a scene, choose Right/Up/Left/Down facing, and move them freely.
The component snaps each copy to its configured grid size and locks rotation to the
chosen cardinal direction in edit mode and again when play starts.

The player/bootstrap owns one `BlasterTurretSceneContext2D`. Once that context receives
the player target and player-shot hit adapter, every placed turret configures itself,
creates its visible finite projectile template, receives a hierarchy-derived unique
runtime identity, registers for player shots, and routes confirmed projectile damage.
Duplicated prefab instances therefore remain independent without per-turret controller
code.

## Deterministic cadence

The package supports two presentation modes:

1. A positive warning duration enters Warning before firing and retains the legacy
   color-independent warning geometry.
2. A zero warning duration fires immediately when eligible, without showing a warning.
3. Recovery must finish before another shot can begin.

Target loss, range loss, facing-cone loss, and point-blank ambiguity reset pending
cadence. Disable, death, and restart additionally cancel package-owned projectiles.

## Stationary identity

The Rigidbody2D is kinematic and FreezeAll. EN-003 receives an explicit decision source
that always returns `(0, 0)` velocity, and the package restores its configured anchor on
fixed and late updates. The temporary silhouette is a broad rectangular fixed base with
a single barrel, deliberately distinct from the mobile shooter.

## Color-independent warning

The warning is not encoded by hue alone. It combines:

- one continuous line-of-fire rail;
- four repeated perpendicular ticks; and
- the visible barrel-to-rail connection.

The same geometry remains understandable in grayscale or with color channels removed.
This is temporary presentation and expires when final art replaces it.

## Focused verification

Run with the pinned editor:

```text
"C:\Program Files\Unity\Hub\Editor\6000.3.19f1\Editor\Unity.exe" -batchmode -nographics -projectPath . -runTests -testPlatform PlayMode -testFilter ShooterMover.Tests.PlayMode.Enemies.BlasterTurretPackageTests -testResults Artifacts\TestResults\BlasterTurretPackageTests.xml -logFile Artifacts\Logs\BlasterTurretPackageTests.log -quit
```

The fixture covers descriptor identity, stationary projection, zero-warning and legacy
warning cadence, cardinal facing, cone boundaries, bounded projectile execution,
target loss, death, restart, and point-blank fail-safe behavior.

## Manual warning capture

Open `BlasterTurret.prefab` in an isolated 2D test scene and configure an explicit target,
target collider, and the shared `BoundedProjectile2D` prefab. Capture these frames in
both normal color and grayscale:

1. idle/recovery: no line warning;
2. warning: center rail plus four perpendicular ticks, before any projectile exists;
3. shot: warning hidden and one Blaster projectile leaving the barrel;
4. obstruction inserted during warning: line disappears and no shot is released;
5. compare beside the Mobile Blaster Droid: fixed broad base and zero translation remain
   visually distinct from the mobile role.

## Limitations

- Temporary generated line geometry is not final art.
- No tracking beam persists outside the optional warning phase.
- No homing, target prediction, burst, or area projectile is used.
- A scene still needs one player-owned `BlasterTurretSceneContext2D`; turrets deliberately
  do not locate private player combat state through names, tags, or global singletons.

## Rollback

Remove this package folder and
`Assets/ShooterMover/Tests/PlayMode/Enemies/BlasterTurretPackageTests.cs` with their
inseparable Unity metadata. No scene, registry, save, or shared-adapter cleanup is needed.
