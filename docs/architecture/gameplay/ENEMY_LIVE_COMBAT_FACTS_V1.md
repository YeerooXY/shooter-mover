# ENEMY-LIVE-001 — Generic enemy combat and reward facts

## Scope

`ShooterMover.EnemyCombatRuntime` is the engine-neutral live-combat composition for reusable enemy definitions. It does not introduce another health authority, weapon algorithm, XP authority, drop generator, room authority, or Unity controller hierarchy.

## Ownership

- `EnemyActorState` / `EnemyActorStepper` remain authoritative for enemy health, damage, destruction, and idempotent terminal notifications.
- `EnemyDecisionPolicy` remains authoritative for target selection, detection/range/vision/LOS/attack-arc gates, movement intent, and locked attack intent.
- `WeaponExecutionCore` remains authoritative for ranged weapon execution, cooldown replay, effect batches, and conflicting fire-operation IDs.
- `PlayerActorAuthority` remains authoritative for player health, damage replay, and player death.
- XP and drop systems consume `EnemyCombatDeathFact`; the enemy runtime never awards XP or generates a drop.
- Room-clear consumers read `EnemyRuntimeProjection.BlocksRoomClear`; the enemy runtime does not complete a room.
- Unity presentation reads lifecycle. `EnemyTerminalCollider2DAdapter` disables physical hitboxes after destruction while leaving sprite presentation untouched.

## Definition facts

Every `EnemyCombatDefinition` carries stable definition identity, maximum health, level, detection radius, vision arc, attack arc, minimum/preferred/maximum attack range, cooldown ticks, damage, damage channel, XP value, faction, room-clear role, sprite/presentation reference, drop profile, movement profile, attack identity, ready/cooldown phases, and ranged-weapon or melee-pounce capability kind.

## Ranged flow

1. External perception gathers candidate positions and line-of-sight facts.
2. `EnemyPerceptionBuilder` derives immutable detection and vision facts.
3. `EnemyCombatActorRuntime` supplies the definition projection and calls `EnemyDecisionPolicy`.
4. An attack exists only when target, range, vision, LOS, attack arc, and cadence are valid.
5. `WeaponCoreEnemyRangedAttackExecutor` converts the accepted locked intent into `WeaponFireCommand`.
6. Runtime composition supplies the concrete equipment-instance ID and execution occurs only through `WeaponExecutionCore`.
7. Projectile/contact presentation reports a locked-intent impact back to `ApplyLockedAttackImpact`.
8. `PlayerActorEnemyDamageRouter` delegates the resulting `DamageReceiverCommand` to `PlayerActorAuthority`.

No enemy-name or weapon-name switch is used.

## Melee pounce flow

A melee definition requires no weapon executor. A valid decision creates `EnemyPounceCommitment`, freezing origin, direction, and target point. Contact is accepted only for the committed target. The first impact event is routed to `PlayerActorAuthority`; exact replay is idempotent and a different second impact for the same pounce is rejected.

## Death and reward facts

Incoming enemy damage is translated to `EnemyActorCommand.Damage`. Exact duplicate damage returns duplicate without another death fact; a conflicting duplicate is rejected. The first lethal transition emits one `EnemyCombatDeathFact` containing killer/source entity identity, optional source run-participant identity, enemy identity and lifecycle generation, enemy definition and level, XP value, drop profile, faction and room-clear role, and presentation reference.

These are immutable downstream inputs only. No XP, item, currency, inventory, or strongbox mutation occurs here.

## Example: Mobile Blaster Droid

```text
definition: enemy.mobile-blaster-droid
health: 16
level: 1
detection / vision / attack arc: 20 / 360 / 90
attack range: 0..12 (preferred 5)
cooldown: 6 ticks
damage: 10 kinetic
XP: 20
faction: faction.enemy
room-clear: required
presentation: presentation.moving-droid-sprite
drop profile: drop-profile.mobile-blaster-droid
attack: weapon.blaster-machine-gun
capability: ranged weapon
```

The existing prefab keeps its current `SpriteRenderer` and sprite reference. It adds only the generic terminal-collider projection for the existing circle hitbox.

## Example: Pouncer

```text
definition: enemy.pouncer
health: 12
level: 1
detection / vision / attack arc: 12 / 180 / 70
attack range: 0..2.5 (preferred 1.5)
cooldown: 30 ticks
damage: 8 contact
XP: 15
faction: faction.enemy
room-clear: required
presentation: presentation.pouncer-sprite
drop profile: drop-profile.pouncer
attack: attack.enemy-pounce
capability: melee pounce (no weapon)
```

## Exact Unity proof commands

Focused EditMode:

```bash
"<UNITY_EDITOR>" -batchmode -nographics -projectPath . \
  -runTests -testPlatform EditMode \
  -testFilter ShooterMover.Tests.EditMode.EnemyCombatRuntime.EnemyCombatRuntimeTests \
  -testResults artifacts/enemy-live-001-editmode.xml \
  -logFile artifacts/enemy-live-001-editmode.log -quit
```

Focused PlayMode:

```bash
"<UNITY_EDITOR>" -batchmode -nographics -projectPath . \
  -runTests -testPlatform PlayMode \
  -testFilter ShooterMover.Tests.PlayMode.Enemies.EnemyTerminalCollider2DAdapterTests \
  -testResults artifacts/enemy-live-001-playmode.xml \
  -logFile artifacts/enemy-live-001-playmode.log -quit
```

Full compilation / test discovery:

```bash
"<UNITY_EDITOR>" -batchmode -nographics -projectPath . \
  -runTests -testPlatform EditMode \
  -testResults artifacts/enemy-live-001-all-editmode.xml \
  -logFile artifacts/enemy-live-001-all-editmode.log -quit
```

The implementation branch intentionally does not edit `Stage1VisibleSliceController.cs`.
