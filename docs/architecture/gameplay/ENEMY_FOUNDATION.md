# Enemy Foundation

## Added

- `EnemyDef` for reusable authored data.
- `EnemySpawn` for level-owned placement and overrides.
- `BodyDef` with travel, size, mass, and stable mounts.
- Typed `GunAttack`, `MeleeAttack`, `ChargeAttack`, and `ExplodeAttack`.
- Typed damage, burn, explosion, slow, and knockback references.
- Fixed modifier order.
- Separate deterministic variant and perk roll keys.
- Validation for duplicate IDs, missing mounts, tier/variant/perk overrides, and phase order.

## Gun boundary

`GunAttack` stores only a gun ID and `ShotPlan`.

`ShotPlan` owns mount selection and timing only. It has no damage, projectile count, spread, speed, range, guidance, explosion, or status values.

## Compatibility

This slice is additive. The current enemy catalog and runtime remain active until import and runtime adapters migrate to these contracts.

PR #413's enemy-only rocket and burn path is not used.

## Next

1. Add Enemy Creator package DTOs and validation.
2. Map current level placements to `EnemySpawn`.
3. Add shared `GunFireReq` execution.
4. Migrate built-in enemies.
5. Remove legacy enemy projectile and level-scaling data.
