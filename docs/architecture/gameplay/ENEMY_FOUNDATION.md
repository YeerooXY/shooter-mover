# Enemy Foundation

## Contracts

- `EnemyDef` owns reusable authored data.
- `EnemySpawn` owns level placement and overrides.
- `BodyDef` owns travel, size, mass, and stable mounts.
- Attacks are typed: gun, melee, charge, and explode.
- Effects are typed: damage, burn, explosion, slow, and knockback.
- Modifier order is fixed.
- Variant and perk rolls use separate deterministic stream keys.

## Packages

- `EnemyPkg` contains one enemy definition.
- `EnemyPkgJson` imports and exports schema `1` JSON.
- `EnemyPkgCheck` validates canonical references and incompatible attacks.
- `EnemyPkgCompiler` combines packages into a sorted `EnemyDefCatalog`.
- Duplicate enemy IDs fail before catalog publication.

Validated references:

- guns
- views
- movement styles
- AI styles
- effects
- perks
- modifiers
- XP profiles
- loot profiles

## Enemy Maker

`tools/enemy-maker` edits one package at a time.

It loads a catalog snapshot, opens existing packages, validates the model, previews JSON, and exports `<enemy-id>.enemy.json`.

The tool does not expose gun-owned damage, projectile count, spread, speed, range, guidance, explosion, or status values.

## Gun boundary

`GunAttack` stores only a gun ID and `ShotPlan`.

`ShotPlan` owns mount choice and timing only.

## Compatibility

This work is additive. The current enemy catalog and live runtime remain active until migration adapters are added.

PR #413's enemy-only rocket and burn path is not used.

## Remaining

1. Map Level Maker placements to `EnemySpawn`.
2. Add shared `GunFireReq` execution.
3. Migrate built-in enemies.
4. Remove legacy enemy projectile and level-scaling data.

## Validation

No automated tests were added or run in this implementation-first pass. Unity compilation and browser smoke testing remain pending.
