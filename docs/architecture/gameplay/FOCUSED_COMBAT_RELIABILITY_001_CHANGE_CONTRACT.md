# Damage and death refactor

## Player-visible result

- Projectiles move, expire, pierce, ricochet when supported, and disappear according to projectile rules only.
- A projectile collision sends one hit to the struck object and does not wait for that object to die, drop loot, award XP, or update the room.
- Enemies and destructible props use the same collision-facing hit component while keeping their existing health and death logic.
- A dead enemy becomes inactive immediately, so it no longer receives Unity updates, physics work, or rendering work.
- Combat-room doors remain locked until every required enemy is dead.

## Responsibilities

### Projectile

`ProductionCanonicalNormalProjectile2D` owns:

- movement and remaining range;
- pierce state;
- collision resolution;
- projectile completion and destruction.

It does not retain a target, a pending damage command, or retry state after impact.

### Damageable object

`DamageableTarget2D` is the small shared Unity boundary. It validates the target identity and forwards the hit to the object that was struck.

The target owns what the hit means:

- `RoomEnemyActor2D` forwards it to the existing enemy health/death gameplay state;
- `DestructibleProp2D` forwards it to the existing prop health/destruction gameplay state.

### Enemy death

Health reaching zero is the death point. The enemy then performs its own death actions:

- stop attacks;
- stop collision and presentation work;
- report the kill to the room;
- award XP;
- request drops;
- update kill statistics.

These actions are never delegated back to the projectile. A non-fatal death-action failure is recorded and reported, while the remaining death actions are still attempted.

## Long-room performance rule

Dead enemy GameObjects are inactive. Therefore, after many cumulative kills, dead enemies do not add more `Update`, `FixedUpdate`, rendering, or Physics2D callbacks. Their lightweight room/runtime records remain until the room is unloaded or rebuilt.

This removes the known growing-work problem, but actual frame time and memory after 1,000 kills still require a Unity Profiler run before the PR can be marked ready.

## Other retained fixes

- Every supported current-room enemy must bind an `EnemyAttack2D` publisher; unsupported or incomplete bindings fail the room build instead of silently creating a partial combat room.
- Both directions of each authored combat-room door use `room-complete` and the rooms use `all-enemies` completion.
- Weapon mount selection is unchanged.

## Validation status

Completed:

- static full-file review of the changed production path;
- direct-caller review;
- source/generated encounter JSON comparison;
- branch comparison against the recorded base and current `main`.

Not executed in this environment:

- Unity compilation;
- EditMode or PlayMode tests;
- manual gameplay acceptance;
- 1,000-kill Unity Profiler capture.

The pull request must remain draft until those checks are performed.

## Manual acceptance

1. Enter the authored combat-loop level.
2. Verify both enemies in the two-enemy room can fire and damage the player.
3. Kill one enemy and confirm both doors remain locked.
4. Kill the final enemy and confirm both doors open, with XP/drop/kill effects occurring.
5. Fire a piercing projectile through enemies and confirm it never pauses waiting for enemy death work.
6. Shoot a destructible prop and confirm its existing destruction behavior runs.
7. Restart or re-enter and confirm defeated enemy objects are correctly rebuilt/reactivated.
8. Run 1,000 cumulative kills while profiling scripts, Physics2D, active projectiles, hierarchy count, frame time, and memory.
