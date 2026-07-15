# Blaster Turret package

## Role

`enemy.blaster-turret` is the fixed-position ranged enemy for Stage 1. The package owns
its tuning, temporary presentation, deterministic warn/fire/recover policy, prefab root,
and package-local composition only.

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

## Deterministic cadence

The package cycles through three explicit states:

1. **Idle** enters Warning but cannot fire in the same fixed step.
2. **Warning** shows a solid rail with four perpendicular shape ticks. When the authored
   warning duration has elapsed, exactly one normal Blaster plan is executed.
3. **Recovery** hides the warning and must finish before a new complete Warning step.

Target loss, range loss, obstruction, point-blank ambiguity, disable, death, and restart
reset pending cadence. They also cancel package-owned projectiles still in flight, so a
stale shot cannot survive a lifecycle or line-of-fire invalidation.

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

The fixture covers descriptor identity, stationary projection, deterministic cadence,
accepted Blaster plan and bounded projectile execution, obstruction, target loss, death,
restart, color-independent warning geometry, target-behind line of fire, and point-blank
fail-safe behavior.

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
- No tracking beam persists outside the warning phase.
- No alternate, empowered, homing, burst, or area projectile is used.
- No encounter placement or registry generation is included.

## Rollback

Remove this package folder and
`Assets/ShooterMover/Tests/PlayMode/Enemies/BlasterTurretPackageTests.cs` with their
inseparable Unity metadata. No scene, registry, save, or shared-adapter cleanup is needed.
