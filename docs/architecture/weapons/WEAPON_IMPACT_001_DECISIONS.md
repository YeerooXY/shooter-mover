# WEAPON-IMPACT-001 — Impact and ricochet decisions

## Status

Draft architecture stacked on WEAPON-PROJECTILE-001 / PR #301, which is itself rebased onto current
`main` after merged WEAPON-GUIDANCE-001.

This component is engine-neutral. It introduces no Unity collision components, scenes, Stage 1
behavior, random service, effect-batch authority, or weapon-specific projectile subclass.

## Shared authorities

### Explosion reasons

Impact decisions consume the domain-level `WeaponExplosionTriggerReason` introduced by
WEAPON-PROJECTILE-001. This PR defines no application-local explosion-reason enum.

Explosion and continuation remain independent:

- explosion plus continuation;
- explosion plus termination;
- continuation without explosion;
- termination without explosion.

A successful ricochet can therefore carry `WallImpact` while continuing. A failed or exhausted
bounce can carry `WallImpact | Termination` while terminating.

### Randomness

Ricochet chance and optional angle variation use the existing immutable `DeterministicRandom` stream.
No specialized random algorithm or service is introduced.

## Impact request

`WeaponImpactRequest` retains:

- `WeaponEffectIdentity`;
- impact ordinal;
- simulation step;
- impact event kind;
- authored `WeaponImpactSpec`;
- incoming direction and speed;
- wall normal;
- logical `WeaponWallContactId`;
- immutable ricochet runtime state.

No Unity object crosses this boundary.

## Wall behavior

Wall decisions support:

- deterministic bounce chance;
- reflected direction;
- optional bounded angle variation;
- maximum successful bounces;
- retained speed;
- post-bounce homing pause;
- failed/exhausted fallback;
- duplicate logical wall-contact suppression within one simulation step.

Only a successful bounce consumes a successful-bounce opportunity. Wall impacts never consume pierce.

## Duplicate contacts

Duplicate suppression uses:

`simulation step + WeaponWallContactId`

Impact ordinal remains ordered event identity, not wall-contact identity. Repeated callbacks for one
logical contact consume no extra randomness, bounce opportunity, or explosion request. Distinct wall
contacts during the same step remain independent.

## Projectile integration

PR #301 pauses every handled wall contact and waits for this authority. An adapter maps:

- `Ricochet` plus `Continue` to `ProjectileWallImpactResolution.SuccessfulBounce(...)`, preserving
  `ExplosionReasons`;
- terminating wall decisions to `ProjectileWallImpactResolution.BlockingImpact(...)`, preserving
  all shared reasons.

The projectile layer must not recompute or clear these flags.

## Prototype policy

No automated tests are added or modified. Unity compilation and in-game integration remain pending.
No build or runtime-pass claim is made.
