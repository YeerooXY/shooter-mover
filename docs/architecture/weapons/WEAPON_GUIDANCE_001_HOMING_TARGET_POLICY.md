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

A valid existing lock is retained before any new policy selection. The current
`WeaponGuidanceSpec` has one range value. Until a separate retention range is authored, that range
is used for both initial acquisition and continued lock validity.

## Acquisition lifecycle

`WeaponGuidanceState` records acquisition history explicitly:

- `NotAcquired`: no target has ever been acquired; the authored selection policy may run;
- `Tracking`: an exact identity and lifecycle generation is currently locked;
- `WaitingForReacquisition`: a previous lock was lost and `ReuseTargetPolicy` may run again;
- `LostWithoutReacquisition`: a previous lock was lost under `None`; this is terminal and the
  projectile continues without selecting another target.

Losing a target under `WeaponReacquisitionMode.None` therefore cannot become indistinguishable
from a projectile that has never acquired a target. Later simulation steps remain in
`LostWithoutReacquisition` and do not invoke target selection.

For `CurrentLockedTarget` with `ReuseTargetPolicy`, the exact missing identity and lifecycle
reference is retained while waiting. It may resolve that same target again, but it never substitutes
another actor or lifecycle generation.

## Activation, turn rate, and ricochet pause

Activation delay and ricochet pause are evaluated inside the step. If either boundary ends partway
through a step, only the remaining active time contributes to turning.

Steering uses the authored degrees-per-second turn rate and clamps the signed angular change for the
active portion of the step. The implementation never replaces the projectile direction with the
target direction without passing through that turn-rate limit.

`WeaponGuidanceState.PauseAfterRicochet` accepts the externally resolved reflected direction,
updates the acquisition aim to that trajectory, preserves the exact target lock and acquisition
lifecycle, and pauses guidance for an explicit duration. A ricochet therefore cannot reset the
one-acquisition limit.

## Intentionally deferred

- Unity or physics adapters;
- projectile movement integration;
- collision and ricochet resolution;
- effect emission;
- scene or Stage 1 adoption;
- focused deterministic geometry and target-selection tests;
- overflow hardening for extreme finite coordinates outside expected game-scale values.

Tests are intentionally deferred until the guidance contract reaches its final reviewed form.
