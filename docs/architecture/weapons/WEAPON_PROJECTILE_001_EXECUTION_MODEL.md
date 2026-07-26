# WEAPON-PROJECTILE-001 — Projectile execution model

## Status

Engine-neutral projectile execution is now connected to the canonical effective-weapon firing
composition by WEAPON-DATA-002A / PR #313.

This layer does not change Unity physics, scenes, prefabs, collision presentation, or damage
application. It now participates in production post-scheduler composition through immutable launch
payloads carried by the existing `WeaponEffectBatch` and inventory pending-delivery boundary.

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
continuation, explosion plus termination, or no explosion. The existing impact authority consumes
this same type rather than defining an application-local equivalent.

## Authority boundaries

### Movement

`ProjectileMovementModel` advances position and travelled distance only. It clamps exactly to the
baked projectile range. Direction comes from the shared `WeaponGuidanceState`; movement does not own
a second direction or target-lock state.

### Guidance

`ProjectileLifecycleState` reuses `WeaponGuidanceState` from WEAPON-GUIDANCE-001. Homing and
reacquisition remain guidance decisions rather than projectile-kind subclasses. Canonical launch
state is initialised from the final guidance specification retained by
`ProjectileExecutionProfile.From(EffectiveWeapon)`.

### Impact lifecycle

`ProjectileImpactResolver` owns projectile lifecycle accounting for:

- enemy contact;
- range expiry;
- explicit termination;
- fixed-point Pierce continuation.

It does not own wall continuation, Ricochet chance, reflected direction, bounce capacity, speed
retention, same-wall suppression, homing-pause selection, or wall explosion-reason selection.

Every handled wall contact transitions to `AwaitingWallImpactResolution` and returns
`RequiresWallImpactResolution`. The shared impact authority supplies one explicit
`ProjectileWallImpactResolution`:

- `SuccessfulBounce`, with resolved direction, speed, homing pause, and explosion reasons; or
- `BlockingImpact`, with explosion reasons.

A successful bounce may carry `WeaponExplosionTriggerReason.WallImpact` while flight continues. The
projectile layer preserves that reason and does not force it to `None`. A blocking result may carry
both `WallImpact` and `Termination`.

Only an explicit successful result resumes flight. `ContinueUntilRangeExpiry` never grants wall
penetration.

### Effect projection

`ProjectileEffectEmitter` projects only completed decisions. Pending wall decisions emit nothing. A
completed successful bounce can emit both a wall-impact description and one explosion description
while retaining an active lifecycle.

Every profile-based emission retains the exact `ProjectileExecutionProfile`, final damage/effects,
effect identity, event/impact ordinal, contact reason, and projectile-specific existing-random
stream. Canonical Rocket enemy contact suppresses the separate direct-impact emission only when the
validated Rocket profile produces an executable explosion emission.

`ProjectileExplosionResolutionAdapter` converts canonical Rocket explosion emissions into
`WeaponExplosionResolutionRequest.ForCanonicalRocket(emission.Profile, ...)`. Transitional positive
`AreaDamage` emissions retain the old unlimited-target request. Canonical non-Rocket explosion damage
is not guessed.

## Fixed-point Pierce

`PierceValue` remains fixed-point in tenths and represents additional enemy-hit continuations after
the primary hit.

1. The primary enemy hit applies.
2. Guaranteed continuations are consumed first.
3. The fractional continuation roll occurs once after guaranteed continuations are exhausted.
4. A granted roll provides one final continuation.
5. Wall contacts never consume Pierce.

The fractional roller delegates to the existing `DeterministicRandom` authority and defines no
separate hash or generator. Its isolated decision key retains projectile, fire-operation, equipment,
participant, source lifecycle, and exact impacted target lifecycle identity.

For canonical Rocket delivery, the projectile body stops on first blocking contact. Its final
`PierceValue` is retained for the deterministically ordered explosion-victim budget instead of
continuing the Rocket through enemies.

## Validated profile construction

`ProjectileExecutionProfile` has no public multi-contract constructor. It is created through two
explicit routes:

```text
ProjectileExecutionProfile.From(EffectiveWeapon)
    canonical Normal / Orb / Rocket production route
    copies final resolved execution values

ProjectileExecutionProfile.From(WeaponBlueprint)
    transitional catalogue projection only
    rejects canonical authored definitions
```

The canonical route retains final definition/equipment identity, delivery type, projectile speed and
range, maximum attack distance, fixed-point Pierce and Ricochet, guidance, impact, damage, effects,
and movement penalty. Spawned projectiles do not query inventory, augments, skills, catalogue, or
character state after launch.

The blueprint-only route requires `IsTransitionalCatalogProjection` and throws
`projectile-profile-canonical-blueprint-rejected` for canonical definitions. It is not an alternate
canonical authoring path.

## Production handoff

The live post-scheduler call remains:

```text
InventoryBackedWeaponExecutionAdapter.ProjectAcceptedSchedule
    -> AcceptedEmissionRuntimeAdapter.Adapt(effectiveWeapon, emission)
```

For canonical travelling delivery, the adapter calls `ProjectileExecutionProfile.From(effectiveWeapon)`,
constructs ordered `ProjectileLaunchRequest` values, creates their initial
`ProjectileLifecycleState`, and carries them as `CanonicalProjectileLaunchEffect` descriptions
through the existing inventory pending-delivery envelope. The legacy behaviour registry remains
transitional-only.

The Unity-side sink that turns `CanonicalProjectileLaunchEffect.InitialState` into an instantiated
Unity projectile presentation remains unimplemented in this PR.

## Projectile kinds

Regular projectiles, Rockets, and Orbs share the same immutable lifecycle state. No projectile-kind
runtime subclass is required.

- Normal retains direct impact, final Pierce, range, speed, and Ricochet.
- Orb retains travelling movement, final guidance/Pierce/range, and normal enemy-impact payload.
- Rocket stops on first blocking contact and resolves universal damage through its explosion.

## Prototype validation policy

No automated tests are added or modified in this PR. Validation is limited to static source review,
branch comparison, changed-file scope inspection, and later Unity compilation/in-game integration.
