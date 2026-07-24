# WEAPON-GUIDANCE-001 — deterministic homing target policy

## Scope

This change adds a reusable, engine-independent guidance decision component for
`WeaponGuidanceSpec`. It does not move projectiles, resolve collisions, emit weapon effects,
or modify current Unity gameplay.

The same component can be used by regular projectiles, rockets, orbs, and future projectile
kinds. No projectile-specific runtime subclass is introduced.

## Runtime boundary

Callers provide:

- immutable `WeaponGuidanceState`;
- the authored `WeaponGuidanceSpec`;
- projectile position and elapsed step time;
- an `IWeaponGuidanceTargetSnapshotSource` containing immutable engine-neutral target snapshots.

The component returns:

- one immutable next state;
- one turn-rate-limited movement direction;
- the exact resolved target snapshot when tracking;
- an explicit status: unguided, activation wait, ricochet pause, no target, or tracking.

Unity `Transform`, `GameObject`, physics, scene lookup, and presentation references are outside
this boundary.

## Target identity and ordering

Every target reference contains both:

- existing `WeaponActorInstanceId`;
- existing `LifecycleGeneration`.

A later lifecycle generation is a different target even when the stable actor identity is reused.
Equal selection scores are resolved by canonical actor identity and then lifecycle generation, so
source-list order never decides a tie. Duplicate snapshots for the same identity and generation are
rejected explicitly.

## Selection policies

- `ClosestToAim` chooses the greatest directional alignment to the state's acquisition aim,
  then nearest distance, then deterministic target identity.
- `NearestInRange` chooses nearest squared distance, then deterministic target identity.
- `CurrentLockedTarget` resolves only the exact supplied lock. It never substitutes a respawned
  lifecycle or another actor.

A valid existing lock is retained before any new policy selection. When the lock is lost,
`WeaponReacquisitionMode.None` clears it. `ReuseTargetPolicy` evaluates the authored policy again.
For `CurrentLockedTarget`, the exact missing reference is retained so it may be resolved again later.

The current `WeaponGuidanceSpec` has one range value. Until a separate retention range is authored,
that range is used for both initial acquisition and continued lock validity.

## Activation, turn rate, and ricochet pause

Activation delay and ricochet pause are evaluated inside the step. If either boundary ends partway
through a step, only the remaining active time contributes to turning.

Steering uses the authored degrees-per-second turn rate and clamps the signed angular change for the
active portion of the step. The implementation never replaces the projectile direction with the
target direction without passing through that turn-rate limit.

`WeaponGuidanceState.PauseAfterRicochet` accepts the externally resolved reflected direction,
updates the acquisition aim to that trajectory, preserves the exact target lock, and pauses guidance
for an explicit duration.

## Intentionally deferred

- Unity or physics adapters;
- projectile movement integration;
- collision and ricochet resolution;
- effect emission;
- scene or Stage 1 adoption;
- focused deterministic geometry and target-selection tests.

Tests are intentionally deferred until the guidance contract reaches its final reviewed form.
