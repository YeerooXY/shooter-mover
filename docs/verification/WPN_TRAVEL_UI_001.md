# WPN-TRAVEL-UI-001 — Live projectile travel and truthful weapon UI

## Launch boundary

- Repository: `YeerooXY/shooter-mover`
- Base branch: `main`
- Exact launch SHA: `e63351f17f99cebdec48f0e0b7c45960980648fe`
- Launch state: immediately after merged PR #236
- Branch: `agent/wpn-travel-ui-001-live-projectiles`

## Observed failure

Inventory-backed projectile effects were configured beneath an inactive atomic batch root. `Rigidbody2D.linearVelocity` was assigned during that inactive staging phase, then the parent was activated. In the live Stage 1 scene the projectiles remained at their muzzle positions. A nearby enemy could still overlap the stationary trigger and take damage, but an enemy at range could not be hit.

The retained Stage 1 controller also left its fixture-backed `Stage1WeaponStatusStrip` active after the Hub loadout runtime was adopted. That strip displayed representative Shotgun/Rocket/Arc states rather than the actual equipped mounts.

## Repair

- Projectile configuration and launch are now separate operations.
- The complete effect batch is still staged while inactive.
- Projectile rigidbodies remain `simulated = false` during staging.
- After the complete batch becomes active, every effect receives an explicit idempotent `BeginEmission()` call.
- Launch restores the immutable origin, applies rotation from the locked direction, enables simulation, and assigns velocity.
- Direct, explosive, and DoT projectiles retain their existing range-derived lifetime.
- Chain/Arc effects remain instantaneous and do not receive projectile physics; they retain a bounded one-second fallback cleanup when no presentation consumer handles them.
- Rollback immediately deactivates a partially launched batch before destruction.
- The fixture-backed Stage 1 weapon strip is disabled when the confirmed Hub loadout is adopted. The compact production HUD remains and shows the authority-derived enabled mount count and equipped names.

## Regression coverage authored

`ActivatedProjectile_TravelsWithItsColliderAwayFromMuzzle`

- verifies the accepted effect is configured and explicitly launched;
- verifies the Rigidbody2D is simulated and has non-zero locked-direction velocity;
- advances two fixed updates;
- verifies both projectile transform and trigger collider leave the original muzzle;
- verifies the collider remains colocated with the projectile.

`ConcurrentMountsExecuteTogetherFromDistinctPhysicalOrigins`

- retains exact per-mount origin and operation identity assertions;
- verifies both Blaster and Rocket instances are launched;
- advances two fixed updates;
- verifies both weapons travel forward while preserving their distinct lateral muzzle offsets;
- retains exact replay/no-second-emission coverage.

## Required Unity proof

Run with Unity `6000.3.19f1`:

```text
Unity -batchmode -nographics -projectPath . -runTests -testPlatform PlayMode -testFilter ShooterMover.Tests.PlayMode.Weapons.Live.InventoryWeaponRuntimePlayModeTests -testResults Temp/wpn-travel-ui-001-playmode.xml -logFile Temp/wpn-travel-ui-001-playmode.log
```

The command should be run without `-quit`, because `-runTests` exits Unity after the test run.

## Manual acceptance

1. Route Bootstrap → Main Menu → Character → Hub → Inventory and equip at least two different weapons.
2. Enter Level 1.
3. Confirm the old four-card fixture strip is absent.
4. Confirm the compact HUD reports the real enabled mount count and equipped names.
5. Fire from several world units away from the moving droid.
6. Confirm projectiles visibly leave each muzzle and travel toward the cursor.
7. Confirm projectile orientation follows travel direction.
8. Confirm the moving droid takes damage on ranged collision without the player standing beside it.
9. Confirm concurrent mounts originate from distinct physical offsets.
10. Confirm Shotgun spread, Rocket travel/explosion, Ricochet travel/bounce, and Arc instantaneous behavior remain distinct.
11. Restart with `R` and confirm outstanding effects clear and new shots travel normally.

## Verification status

Connector-side source and branch audit only. Unity compilation, PlayMode XML, and manual ranged-hit proof are not claimed from this environment. Keep the pull request draft until those checks pass.
