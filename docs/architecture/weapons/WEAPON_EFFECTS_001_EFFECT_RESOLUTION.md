# WEAPON-EFFECTS-001 — deterministic effect resolution

## Scope

This change adds reusable, engine-independent target-level resolution for explosion damage,
damage over time, and chain arcs. It emits immutable decisions and does not apply damage,
mutate status state, move projectiles, or interact with Unity.

The change is stacked on WEAPON-GUIDANCE-001 because that branch owns the shared
`WeaponTargetReference` in `Domain.Weapons.Execution`.

## Existing authorities

`WeaponEffectBatch` remains the immutable fire-level execution transaction accepted by existing
sinks. Effect resolution begins from the existing `WeaponEffectIdentity` through
`WeaponEffectSourceContext` and does not introduce another batch, behavior registry, or damage
authority.

Damage values remain authored by `WeaponDamageSpec`. Explosion, DoT, and chain behavior remain
authored by `WeaponExplosionEffect`, `WeaponDamageOverTimeEffect`, and `WeaponChainArcEffect`.

## Shared target identity

Guidance, explosion, DoT, chain, and later projectile/impact integration use the same exact
`WeaponTargetReference`, composed from `WeaponActorInstanceId` and `LifecycleGeneration`.

Effect-specific snapshots retain `IsEligible`; guidance-specific snapshots retain `IsTargetable`.
Those policies remain separate while target equality and lifecycle semantics have one authority.

## Explosion resolution

Explosion resolution snapshots the target source exactly once for one impact. It removes duplicate
target identities, filters eligibility, radius, and optional line of sight, then orders decisions by
distance and shared target identity. Linear radial falloff retains the authored minimum multiplier at
the radius edge.

## Damage-over-time resolution

`IWeaponEffectApplicationHistory` is mandatory. A request cannot be constructed with null history.
`WeaponEffectApplicationHistory.Empty` is the explicit immutable first-resolution value.

The per-target application key retains fire operation and shot sequence but excludes projectile and
impact ordinals. Pellets from one shotgun shot therefore share one DoT application identity, while a
later shot may apply normally. Duplicate suppression is an invariant rather than an optional caller
convention.

Stack count, remaining duration, refresh behavior, and capacity are resolved from immutable input
snapshots. Adding a stack does not refresh existing duration unless `RefreshesDuration` is authored.

## Chain resolution

Chain resolution is iterative and bounded by `MaximumTargets`; it never recurses. Candidates are
snapshotted once, duplicate identities are removed, already-used targets remain excluded, and each
jump uses deterministic distance/identity ordering plus optional line of sight from the previous jump.
Damage is multiplied by `RetainedDamagePerJump` after every successful jump.

Callers must intentionally choose one request factory:

- `FromPoint(...)` for a chain beginning from a non-target position;
- `FromEnemyImpact(...)` for a chain beginning on an enemy.

`FromEnemyImpact(...)` requires the exact impact target and automatically inserts it into the used
set, preventing the first chain jump from immediately reacquiring the original enemy.

## Prototype boundary

No NUnit or Unity test files are included under the current prototype policy. No scene, Stage 1,
prefab, catalog, current firing behavior, production composition, or Unity adapter is modified.

Unity compilation and standalone C# compilation were unavailable in the connector-only environment,
so no compilation or test-pass claim is made.
