# Mobile Blaster Droid package

`enemy.mobile-blaster-droid` is the moving ranged ordinary enemy for Stage 1.

## Accepted dependency path

- EN-002 owns immutable health, contact-disabled policy, damage, death, and restart state.
- EN-003 owns Rigidbody2D movement projection, target observation, contact routing, and enemy hit intake.
- WP-003 supplies `weapon.blaster-machine-gun`, its normal runtime profile, and its automatic-projectile behavior module.
- CB-009 executes the resulting immutable plan through `WeaponMount2DAdapter`.
- WP-002 supplies `ProjectileExecutionPlanAdapter` and the referenced `BoundedProjectile2D.prefab`.

The enemy package does not declare projectile speed, lifetime, radius, channel, damage, or a new projectile class. The prefab references the accepted shared projectile asset directly.

## Behavior boundary

The droid uses deterministic range positioning only:

- outside the preferred band it approaches directly;
- inside the preferred band it retreats directly;
- within the band it stops;
- it does not strafe, predict target motion, search the scene, or use navigation.

Firing is a closed three-phase schedule:

1. **Ready** starts a wind-up and locks the current non-predictive direction.
2. **WindUp** displays a growing direction line, then submits exactly one normal Blaster plan.
3. **Recovery** compresses the temporary body outline and prevents another attempt until the bounded recovery completes.

Target loss cancels the pending wind-up. Death, deactivation, and restart additionally reset WP-002 projectile instances so no stale shot crosses a session boundary.

## Manual readability check

Place this package and the Blaster Turret in the evidence arena at equal target distance. Confirm the Mobile Blaster Droid is distinguishable by its toward/away range correction, while its growing directional wind-up and compressed recovery remain readable without relying on color. Capture one frame during wind-up and one during recovery. Temporary generated line presentation expires when final art is supplied.

## Rollback

Remove this folder and `Assets/ShooterMover/Tests/PlayMode/Enemies/MobileBlasterDroidPackageTests.cs`. No registry output, scene, save schema, shared adapter, weapon package, or projectile primitive requires rollback.
