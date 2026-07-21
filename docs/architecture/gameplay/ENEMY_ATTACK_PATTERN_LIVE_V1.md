# ENEMY-ATTACK-PATTERN-LIVE-001 — production Unity integration

## Ownership boundary

`EnemyAttackPatternAuthorityV1` remains the engine-neutral authority for deterministic schema-v2
sequence construction, immutable committed aim, sequence/emission identity, fingerprints, replay and
lifecycle cancellation facts.

`EnemyAttackPatternLiveSchedulerV1` is the production implementation of
`IEnemyAttackPatternEffectPortV1`. It owns only downstream delivery state:

- atomic acceptance of one complete immutable sequence;
- deterministic pending-emission ordering;
- exact-once emission delivery;
- replay-safe cancellation of pending effects and open melee windows;
- immutable delivery/debug records.

It does not own enemy health, player health, Run Session identity, projectile collision, hit
eligibility, XP, drops, room state or permanent character state.

`EnemyCommittedAttackPatternExecutorV1` is the narrow outer transaction between one catalog attack
and the scheduler. Cooldown and outer replay are recorded only after the complete sequence has been
accepted downstream. A transient dispatch failure therefore retries the same operation, committed aim,
sequence and fingerprint without consuming cooldown.

## Authoritative Run Session time

`RunSessionAggregateV1.AdvanceTime` advances one explicit monotonic authoritative tick through
`AdvanceRunSessionTimeCommandV1`. It rejects wrong-run, stale/future-lifecycle, ended-run, tick
regression and conflicting operation reuse without mutation.

`RunSessionEnemyAttackPatternTimeV1` projects the accepted Run Session tick into seconds through an
explicit ticks-per-second scale. Unity `FixedUpdate` wakes the production host, but neither Unity frame
time nor a per-enemy wall clock becomes combat authority.

The Stage 1 host advances the Run Session clock once per accepted fixed simulation tick, then evaluates
committed attacks and asks the scheduler to emit every due fact. Variable wake-up intervals produce the
same ordering:

1. `ScheduledAtSeconds`;
2. `EmissionStableId`.

## Atomic sequence acceptance

Before queue mutation, every immutable emission is passed to
`IEnemyAttackPatternEmissionRealizerV1.CanRealize`. One rejection rejects the whole dispatch and queues
nothing. Accepted dispatch identities preserve their canonical fingerprint:

- first equivalent delivery: `Applied`;
- same identity and fingerprint: `ExactReplay`;
- same identity with another fingerprint: `ConflictingDuplicate`;
- wrong run/source lifecycle or unsupported payload: fail closed.

No caller-owned mutable collection is retained. The dispatch contract owns an immutable sorted copy and
the scheduler creates its own private pending list.

## Committed aim and physical realization

The scheduler forwards the original `EnemyAttackEffectEmissionV1` unchanged. Unity realization consumes
the facts frozen when the attack was committed:

- committed origin, direction and optional target identity;
- exact scheduled timestamp and melee active-window bounds;
- sequence, emission and source participant identities;
- source lifecycle generation;
- schema-generated pellet spread;
- projectile profile, speed, travel distance, collision radius and area payload;
- resolved damage and damage channel.

Delayed shots never retarget to the player's newer position. Scatter directions are not rerolled in
Unity.

### Projectiles and area effects

`EnemyAttackPatternUnityEmissionRealizerV1` reuses `BoundedProjectile2D` and
`CombatHit2DAdapter`. Projectile profile IDs resolve through the typed
`EnemyAttackPatternProjectilePrefabRegistryV1`; there is no enemy-name or weapon-name switch.

The realizer preserves finite speed/range/lifetime, collision radius, source ownership, operation
identity and committed direction. Instant area payloads evaluate explicitly registered targets in
stable target-identity order and pass their authored maximum-target capacity into Combat Hit Policy.

The retained bounded-projectile path currently terminates at the first physical impact, so authored
non-area projectile pierce greater than zero fails closed instead of pretending to support pierce.
Persistent area durations also fail closed; instantaneous rocket/explosion payloads are supported.

### Melee and pounce

`EnemyAttackPatternMeleeContact2D` reports trigger/collision candidates only. It never evaluates
eligibility or mutates health.

The realizer opens and closes timed melee windows using authoritative scheduled bounds. It captures the
target lifecycle at emission time, enforces the committed target where present, derives deterministic
per-target hit ordinals, and respects authored `hits_per_target`.

`RigidbodyEnemyAttackPatternPounceMotion2D` realizes lunge/ram motion from committed origin, direction,
lunge distance and authoritative active-window time. It never accumulates Unity delta time.

## Combat Hit Policy and player damage

`EnemyAttackPatternHitRouterV1` owns only session-local policy/history and hit-event replay. It builds
immutable source/effect/target snapshots and delegates final eligibility to the existing
`CombatHitPolicyV1` with `enemy-normal` policy.

Only an accepted policy result is translated through `CombatHitDamageCommandAdapterV1` and forwarded as
an existing `PlayerDamageRequest`. `Level1PlayerRuntimeSceneAdapterV1` and `PlayerActorAuthority` remain
the only player-health/death authorities.

Preserved behavior includes:

- source participant and faction attribution;
- friendly-fire/self-hit decisions;
- target lifecycle validation;
- exact hit replay and conflicting duplicate rejection;
- authored per-target hit counts and area capacity;
- death emission exactly once through the existing player authority;
- no direct health mutation from Unity collision callbacks or enemy controllers.

## Cancellation and lifecycle end

Cancellation facts are replay-protected by cancellation identity and fingerprint. Before mutation, the
scheduler validates that every referenced pending or active effect belongs to the same source entity and
lifecycle.

Accepted cancellation:

- removes all listed not-yet-emitted projectiles and melee strikes;
- closes listed active melee/pounce windows;
- prevents late projectile spawning;
- leaves already emitted projectiles intact;
- treats exact cancellation replay as idempotent;
- rejects conflicting cancellation identity reuse.

Retained Stage 1 enemy health authorities expose active/lifecycle state. A lethal transition makes the
source non-current immediately, so the scheduler cannot emit later work. The generic production
controller then consumes the schema-v2 lifecycle cancellation path on the next production simulation
wake-up and closes pending/open windows.

## Production catalog cutover

The authoritative enemy catalog is now schema v2. The current live production definitions migrated are:

- `enemy.mobile-blaster-droid`;
- `enemy.blaster-turret`.

The remaining catalog fixtures were also converted so production does not maintain a mixed-schema
catalog. Existing gameplay cadence is represented as explicit wind-up plus recovery timing.

Stage 1 imports the schema-v2 catalog from a build-safe Resources projection and rejects any malformed,
unsupported or non-v2 production catalog. Production schema-v2 attacks never translate back to the
historical one-call attack effect path.

The retained moving-droid and turret packages continue to own their accepted movement, health and
presentation. Their historical projectile execution adapters are retired only after the new Run Session,
catalog, scheduler and realizer composition has succeeded. They cannot spawn duplicate gameplay
projectiles afterward.

## Composition and teardown

The integration is an additive partial of `Stage1PlayableLoopCompositionV1`; it does not add another
bootstrap or edit `Stage1VisibleSliceController.cs`.

Production composition uses typed references already owned by Stage 1:

- selected account-backed `ProductionCharacterRuntimeGraphV1`;
- existing Level 1 player runtime;
- existing inventory weapon effect emitter;
- existing room runtime;
- existing mission-result authority;
- retained moving-droid and turret health/movement/presentation surfaces.

Teardown disposes emitted projectiles, closes melee/pounce windows, unregisters collision relays, clears
policy history and removes scheduler/source references. Re-entry or restart rebuilds one fresh run-local
integration and leaves no orphan scheduler or duplicate subscription.

## Adding future content

A future burst, shotgun, scatter, rocket, contact or pounce enemy primarily requires:

1. a schema-v2 enemy attack definition;
2. a registered projectile presentation profile when applicable;
3. a typed source/target presentation binding;
4. a pounce motion port only when the definition authors a lunge;
5. focused content and runtime tests.

Shared scheduler, projectile, melee, policy and player-damage classes do not branch on enemy name,
attack name, room, weapon name or prefab hierarchy.

## Current limitations and verification boundary

- Non-area physical projectile pierce is rejected because the retained `BoundedProjectile2D` path does
  not support continuing after impact.
- Persistent area fields are not implemented; instantaneous area payloads are supported.
- The retained package wind-up animation may still run as presentation-only compatibility, but its old
  projectile adapter is retired and cannot produce gameplay effects.
- This connected environment has no Unity executable or C# compiler. Unity compilation, XML results and
  PlayMode visual proof must be produced in a licensed Unity environment; none are claimed here.
