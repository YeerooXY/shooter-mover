# WEAPON-PROJECTILE-001 — Projectile execution model

## Status

Draft architecture rebased onto current `main` after merged WEAPON-GUIDANCE-001 / PR #300.

This layer is engine-neutral and additive. It does not change Unity physics, scenes, live firing,
`WeaponEffectBatch`, damage routers, catalogs, or production composition.

## Shared authorities

### Target identity

All projectile launch, contact, lifecycle, emission, and fractional-pierce boundaries use the merged
`WeaponTargetReference` from `Domain.Weapons.Execution`.

The reference contains:

- `WeaponActorInstanceId`;
- `LifecycleGeneration`.

A respawned actor therefore cannot be treated as the target lifecycle that was hit or tracked earlier.

### Explosion reasons

`WeaponExplosionTriggerReason` is the one shared domain flags type for:

- enemy impact;
- wall impact;
- range expiry;
- termination.

Explosion reasons are independent from continuation. One decision can represent explosion plus
continuation, explosion plus termination, or no explosion. WEAPON-IMPACT-001 should consume this
same type rather than defining an application-local equivalent.

## Authority boundaries

### Movement

`ProjectileMovementModel` advances position and travelled distance only. It clamps exactly to the
authored range. Direction comes from the shared `WeaponGuidanceState`; movement does not own a
second direction or target-lock state.

### Guidance

`ProjectileLifecycleState` reuses `WeaponGuidanceState` from WEAPON-GUIDANCE-001. Future homing and
reacquisition remain guidance decisions rather than projectile-kind subclasses.

### Impact lifecycle

`ProjectileImpactResolver` owns projectile lifecycle accounting for:

- enemy contact;
- range expiry;
- explicit termination;
- fixed-point pierce continuation.

It does not own wall continuation, ricochet chance, reflected direction, bounce capacity, speed
retention, same-wall suppression, homing-pause selection, or wall explosion-reason selection.

Every handled wall contact transitions to `AwaitingWallImpactResolution` and returns
`RequiresWallImpactResolution`. A dedicated impact authority supplies one explicit
`ProjectileWallImpactResolution`:

- `SuccessfulBounce`, with resolved direction, speed, homing pause, and explosion reasons; or
- `BlockingImpact`, with explosion reasons.

A successful bounce may carry `WeaponExplosionTriggerReason.WallImpact` while flight continues.
The projectile layer preserves that reason and does not force it to `None`. A blocking result may
carry both `WallImpact` and `Termination`.

Only an explicit successful result resumes flight. `ContinueUntilRangeExpiry` never grants wall
penetration.

### Effect projection

`ProjectileEffectEmitter` projects only completed decisions. Pending wall decisions emit nothing.
A completed successful bounce can emit both a wall-impact description and one explosion description
while retaining an active lifecycle. The emitter does not replace or modify `WeaponEffectBatch`.

## Fixed-point pierce

`PierceValue` remains fixed-point in tenths and represents additional enemy-hit continuations after
the primary hit.

1. The primary enemy hit applies.
2. Guaranteed continuations are consumed first.
3. The fractional continuation roll occurs once after guaranteed continuations are exhausted.
4. A granted roll provides one final continuation.
5. Wall contacts never consume pierce.

The fractional roller is a narrow port. Its default adapter delegates to the existing
`DeterministicRandom` authority and defines no separate hash or generator. Its isolated decision key
retains projectile, fire-operation, equipment, participant, source lifecycle, and exact impacted
target lifecycle identity.

## Validated profile construction

`ProjectileExecutionProfile` has no public multi-contract constructor. It is created only through:

`ProjectileExecutionProfile.From(WeaponBlueprint blueprint)`

The execution profile is a projection of already validated weapon structure, not an easier secondary
authoring authority.

## Projectile kinds

Regular projectiles, rockets, and orbs share the same immutable lifecycle state. Rocket explosion
requests come from authored impact decisions. No projectile-kind runtime subclass is required.

## Prototype validation policy

No automated tests are added or modified in this PR. Validation is limited to static source review,
branch comparison, forbidden-reference scans, and later Unity compilation/in-game integration.
