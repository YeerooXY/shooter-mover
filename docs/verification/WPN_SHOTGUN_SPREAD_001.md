# WPN-SHOTGUN-SPREAD-001 — Separate deterministic multi-projectile trajectories

## Launch boundary

- Repository: `YeerooXY/shooter-mover`
- Branch: `agent/wpn-shotgun-spread-001`
- Target: `main`
- Exact launch SHA: `8b4d4e401827735d10082f6f3b04a5d1b2103920`
- Launch state: current main immediately after merged PR #244
- Unity baseline: `6000.3.19f1`

## Observed symptom

The production starter Shotgun appeared to fire one projectile rather than a pellet fan.
The definition was already configured for seven projectiles and a 24-degree spread, and
the Unity sink already instantiated every effect. The seven pellet objects were real,
but they occupied the same trajectory and visually collapsed into one projectile.

## Root cause

`WeaponDeterministicSpread` built one FNV-1a input per projectile and appended the
projectile ordinal at the end. For adjacent ordinals such as `0` through `6`, most of the
resulting hash variation remained in low bits.

The conversion from a 64-bit hash to a double intentionally discarded eleven low bits
to produce a 53-bit unit value. Consequently, the part of the hash that distinguished
adjacent pellet ordinals was discarded. All seven pellets sampled the same spread angle.

Existing coverage asserted only:

- seven immutable effects existed;
- exact retries were deterministic;
- the batch fingerprint changed between shots.

It never asserted that pellet directions or physical positions were distinct.

## Repair

The reusable deterministic spread service now has a projectile-count-aware overload.

For every deterministic sample it:

1. hashes the immutable seed, operation, equipment, shot, count, and ordinal facts;
2. applies a 64-bit avalanche before taking the upper 53 bits;
3. for a multi-projectile batch, assigns every ordinal one ordered lane across the
   configured cone;
4. applies only small bounded deterministic jitter inside that lane;
5. clamps every result to the configured spread boundary.

A seven-pellet, 24-degree Shotgun now produces seven ordered trajectories from roughly
`-12` to `+12` degrees, with more than three degrees of separation between adjacent
centre-lines.

The old public single-projectile overload remains available. Single-projectile weapons
continue to receive one deterministic offset inside their configured spread. Projectile,
explosive, and damage-over-time behavior implementations all use the count-aware path
when their profile emits multiple effects.

## Determinism and authority

The fix does not add random runtime state. Direction remains a pure function of immutable
fire facts. Therefore:

- an exact rejected retry rebuilds a byte-equivalent effect batch;
- an accepted replay remains idempotent;
- conflicting command facts remain conflicting duplicates;
- exact equipment-instance identity remains part of spread derivation;
- cooldown and shot-sequence ownership remain unchanged.

## Regression coverage

### EditMode — `WeaponDeterministicSpreadTests`

`ShotgunPellets_FormDistinctOrderedFanAcrossConfiguredSpread` proves:

- seven effects are emitted;
- all seven directions are distinct;
- every angle remains inside `[-12, +12]` degrees;
- the first and last pellets reach the outer cone;
- directions remain ordered by ordinal;
- adjacent lanes remain separated by more than three degrees.

`SameShotgunCommand_RebuildsByteEquivalentDistinctFan` proves:

- rejected exact retries produce the same batch fingerprint;
- the repeated batch still contains seven distinct directions.

### PlayMode — `ShotgunLaunchesSevenPhysicalPelletsOnDistinctTrajectories`

The live inventory-backed Unity path proves:

- the Shotgun execution result contains seven effects;
- the emitter launches seven physical projectile objects;
- all seven `TravelDirection` values are distinct;
- all seven projectile positions separate after fixed updates;
- the physical fan crosses both sides of the centre-line.

## Changed files

- `Assets/ShooterMover/Runtime/Application/Weapons/Execution/WeaponBehaviors.cs`
- `Assets/ShooterMover/Tests/EditMode/Weapons/Execution/WeaponDeterministicSpreadTests.cs`
- `Assets/ShooterMover/Tests/EditMode/Weapons/Execution/WeaponDeterministicSpreadTests.cs.meta`
- `Assets/ShooterMover/Tests/PlayMode/Weapons/Live/InventoryWeaponShotgunSpreadPlayModeTests.cs`
- `Assets/ShooterMover/Tests/PlayMode/Weapons/Live/InventoryWeaponShotgunSpreadPlayModeTests.cs.meta`
- `docs/verification/WPN_SHOTGUN_SPREAD_001.md`

No weapon definition values, equipment/loadout authority, scene, prefab, presentation
asset, player authority, enemy authority, damage routing, reward, XP, room, or mission
result code is changed.

## Required Unity proof

Unity is unavailable in the connected authoring environment, so no compilation or
passing test result is claimed.

```text
Unity -batchmode -nographics -projectPath . -runTests -testPlatform EditMode -testFilter ShooterMover.Tests.EditMode.Weapons.Execution.WeaponExecutionCoreTests -testResults Temp/wpn-shotgun-spread-001-editmode.xml -logFile Temp/wpn-shotgun-spread-001-editmode.log
```

```text
Unity -batchmode -nographics -projectPath . -runTests -testPlatform PlayMode -testFilter ShooterMover.Tests.PlayMode.Weapons.Live.InventoryWeaponRuntimePlayModeTests -testResults Temp/wpn-shotgun-spread-001-playmode.xml -logFile Temp/wpn-shotgun-spread-001-playmode.log
```

Do not add `-quit`; `-runTests` exits Unity after the test run.

## Manual acceptance

1. Equip the production Shotgun.
2. Enter Level 1 and aim at empty floor several world units away.
3. Fire once and confirm seven yellow pellets separate into a readable fan.
4. Confirm the fan is centred around the mount-to-cursor direction.
5. Confirm pellets reach both sides of the centre-line.
6. Confirm individual pellets may hit different targets or different parts of a wide
   target group.
7. Confirm repeated fire produces deterministic but non-stacked pellet lanes.
8. Confirm Blaster, Rocket, Arc, Ricochet, cooldown, replay, and restart behavior remain
   functional.

Draft only. Do not merge automatically.
