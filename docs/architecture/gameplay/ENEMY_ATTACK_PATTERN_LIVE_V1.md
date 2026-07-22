# ENEMY-ATTACK-PATTERN-LIVE-001 — production Unity integration

## Ownership boundary

`EnemyAttackPatternAuthorityV1` remains the engine-neutral authority for deterministic schema-v2
sequence construction, immutable committed aim, sequence/emission identity, fingerprints, replay and
lifecycle cancellation facts.

`EnemyAttackPatternLiveSchedulerV1` implements `IEnemyAttackPatternEffectPortV1` and owns only downstream
delivery state:

- atomic acceptance of one immutable sequence;
- deterministic pending-emission ordering;
- commit-after-ack physical delivery;
- replay-safe cancellation of pending effects and open melee windows;
- immutable delivery/debug records.

It does not own enemy health, player health, Run Session identity, authoritative time, room state,
mission results, pickup collection, conditions, status effects, XP, drops or permanent character state.

## One shared production Run Session

Stage 1 composes exactly one `RunSessionAuthorityV1` and one `RunSessionAggregateV1` in
`Stage1PlayableLoopCompositionV1.RunSession.cs`.

```text
Stage1PlayableLoopCompositionV1
└── shared RunSessionAggregateV1
    ├── accepted player runtime port
    ├── frozen inventory/weapon runtime port
    ├── canonical condition runtime
    ├── condition-owned status-effect projection
    ├── canonical active-ability lifecycle port
    ├── room runtime port
    ├── existing mission-result authority port
    ├── authoritative run tick
    └── feature consumers
        ├── schema-v2 enemy attack scheduling
        └── future physical pickup collection / run-local journals
```

The shared aggregate is started through `ProductionConditionBoundRunSessionStartSourceV1`, so the
condition and status-effect ports are the merged production authorities. The former feature-local
`Stage1StatusRunProjectionV1` and `Stage1ConditionRunProjectionV1` placeholders were deleted.

The Stage 1 adapters for player, weapon, room and mission result are created once for this shared graph.
They are not reconstructed by the enemy-attack partial.

`Stage1PlayableLoopCompositionV1.EnemyAttackPatterns.cs` is now a consumer only. It:

- calls `TryResolveSharedRunSession`;
- receives the existing aggregate through `RunSessionEnemyAttackPatternTimeV1`;
- validates the shared run ID and player lifecycle;
- never constructs `RunSessionAuthorityV1`;
- never calls `Start`;
- never builds runtime ports;
- never owns a simulation tick.

The internal `TryResolveSharedRunSession` seam is the integration point for downstream Stage 1 features.
A pickup/collection branch must consume this aggregate after rebase rather than starting another run.

## Authoritative in-run restart

An in-mission restart does not replace the authority or aggregate. The production input path calls
`RunSessionAggregateV1.Restart` through `RestartSharedRunSession`.

The restart command uses the existing run identity, current lifecycle generation, next generation,
current authoritative tick and `RunRestartPolicyV1.FullTransientReset()`. The aggregate then:

1. checks restart replay/conflict history;
2. validates run identity, active state, lifecycle and tick;
3. preflights every lifecycle runtime port;
4. restarts the accepted player, weapon, condition/status, ability and room ports;
5. increments lifecycle generation exactly once;
6. resets policy-selected transient state;
7. retains the aggregate object, authority, frozen character/loadout inputs and replay ledgers.

The player port delegates to the accepted player runtime. Its accepted restart projects movement, health,
enemy session state, input, camera and HUD reset through the retained Stage 1 presentation boundary.
The room port resets the production room runtime within the same aggregate-controlled transaction.

Enemy attack consumers are torn down before restart and recreated against the same aggregate at the new
lifecycle generation. Their disposable scheduler, collision and physical-effect state is not the Run
Session itself.

A completely new authority and aggregate are created only when Stage 1 enters a genuinely different run
identity or the composition is destroyed. A player lifecycle change outside `RunSessionAggregateV1.Restart`
fails closed rather than silently reconstructing mission truth.

This preserves:

- the same run ID and aggregate reference;
- frozen character, skill, loadout and equipment fingerprints;
- restart replay and conflict history;
- fact and local-mutation exactly-once ledgers;
- stale-generation rejection;
- one lifecycle ordering for future pickup collection and other run-local journals.

## Authoritative time

Unity `FixedUpdate` wakes the shared Stage 1 host but does not contribute combat time directly.

Each accepted simulation step calls `RunSessionAggregateV1.AdvanceConditionRuntime` with one explicit
monotonic tick. That operation advances the aggregate and its bound condition/status owner together.
Enemy attacks then read the already committed aggregate tick through
`RunSessionEnemyAttackPatternTimeV1`.

Consequently, enemy attacks, condition expiry, physical pickup collection and future run-local systems
observe the same:

- run identity;
- lifecycle generation;
- authoritative tick;
- terminal state.

No per-enemy or feature-local clock exists. Due emissions order by:

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

No caller-owned mutable collection is retained.

## Transactional downstream acknowledgement

`EnemyAttackPatternTransactionalRealizerV1` adapts the physical Unity realizer to immutable results:

- `Applied`;
- `ExactReplay`;
- `Rejected`;
- `ConflictingDuplicate`;
- `RetryableFailure`.

A due emission remains pending until realization returns `Applied` or `ExactReplay`. A throw or retryable
failure preserves the same emission identity, committed aim and schedule for a later retry.

Successful physical realization has its own replay ledger. If Unity created an effect but scheduler
bookkeeping did not commit, retry returns an exact downstream replay rather than producing a duplicate
projectile or melee window.

Active-window cancellation follows the same rule. Scheduler bookkeeping is removed only after every
referenced physical close acknowledges success. Completed closes replay exactly while unfinished closes
remain retryable.

## Committed aim and physical realization

The Unity realizer consumes the immutable facts frozen at attack commitment:

- origin, direction, target and source participant;
- sequence/emission identity and source lifecycle;
- scheduled timestamp and active-window bounds;
- schema-authored pellet spread;
- projectile profile, speed, range/lifetime and collision radius;
- area payload, resolved damage and damage channel.

Delayed attacks never retarget and spread is never rerolled in Unity.

### Projectiles and area effects

`EnemyAttackPatternUnityEmissionRealizerV1` reuses `BoundedProjectile2D` and
`CombatHit2DAdapter`. Projectile profiles resolve through the typed
`EnemyAttackPatternProjectilePrefabRegistryV1`; no enemy-name or weapon-name switch is used.

Instant area payloads evaluate explicitly registered targets in stable identity order. Unsupported
physical projectile pierce and persistent area durations fail closed.

### Melee and pounce

`EnemyAttackPatternMeleeContact2D` reports candidates only. It does not decide eligibility or mutate
health.

Timed melee windows preserve committed bounds, target lifecycle, deterministic per-target hit ordinal
and authored `hits_per_target`. `RigidbodyEnemyAttackPatternPounceMotion2D` derives motion from committed
origin, direction, lunge distance and shared authoritative time rather than accumulated Unity delta.

## Combat Hit Policy and player damage

`EnemyAttackPatternHitRouterV1` delegates final eligibility to the existing `CombatHitPolicyV1`. Only an
accepted policy result becomes an existing `PlayerDamageRequest`; `PlayerActorAuthority` remains the
only player health/death authority.

Hit replay preserves semantic outcome:

- applied hit → `AppliedExactReplay`, accepted;
- policy rejection → same rejection, not accepted;
- deterministic damage-authority rejection → same rejection, not accepted;
- conflicting identity reuse → `ConflictingDuplicate`;
- temporary context or damage-authority unavailability → non-memoized `RetryableFailure`.

Melee hit counters advance only when `IsAccepted` is true, so rejected replays cannot consume authored
hit capacity.

## Cancellation and lifecycle end

Cancellation validates that every pending or active effect belongs to the same source entity and
lifecycle before mutation. Accepted cancellation:

- removes listed future projectiles and melee strikes;
- closes listed active melee/pounce windows;
- prevents late spawning;
- leaves already emitted projectiles intact;
- replays exactly;
- rejects conflicting cancellation identity reuse.

A terminal enemy immediately fails the shared lifecycle gate, suppressing later emissions. The generic
production controller then consumes the schema-v2 lifecycle cancellation path to close pending/open work.

## Production catalog cutover

The production enemy catalog is schema v2. The live production definitions migrated are:

- `enemy.mobile-blaster-droid`;
- `enemy.blaster-turret`.

The build-safe Resources projection is validated fail closed. Retained moving-droid and turret packages
continue owning health, movement and presentation. Their historical projectile adapters are retired only
after the shared run and new attack composition succeed.

## Composition and teardown

The integration is additive to `Stage1PlayableLoopCompositionV1` and does not edit
`Stage1VisibleSliceController.cs` or create a second bootstrap.

On lifecycle restart, only feature-consumer state is disposed: emitted enemy effects, melee windows,
collision relays, scheduler queues and attack source bindings. The shared authority and aggregate remain
alive and commit the new generation through their existing restart transaction.

Full shared-run teardown occurs only when the component is disabled/destroyed or Stage 1 begins a truly
different run identity. That teardown disposes feature consumers before releasing the aggregate reference.

## Verification coverage

Focused EditMode coverage includes:

- atomic dispatch and exact/conflicting replay;
- deterministic burst/scatter ordering;
- fail-once realization and cancellation retry;
- lifecycle cancellation and melee-window bookkeeping;
- applied and rejected hit replay semantics;
- transient context/damage retries;
- authored melee hit limits and one canonical player death;
- an architecture guard proving the enemy partial contains no Run Session start, authority, runtime-port
  factory or private tick, while the shared production host contains exactly one authority construction;
- production source proof that restart input calls `RestartSharedRunSession` and cannot call the retained
  player restart, room restart or `BeginRun` bypasses;
- aggregate regression proving the same object, run identity and frozen input fingerprint survive restart,
  lifecycle increments once, stale facts reject, exact restart replay is stable, conflicting reuse rejects,
  pre-restart operation identities remain reserved and fresh generation work remains accepted.

The open pickup branch must add collection-record-specific assertions after rebasing onto this shared-run
restart seam. This PR proves the aggregate lifetime and exactly-once boundary without importing #279's
parallel task-owned types.

## Current limitations and verification boundary

- Non-area physical projectile pierce is rejected by the retained bounded-projectile path.
- Persistent area fields are not implemented; instantaneous area payloads are supported.
- The retained package wind-up animation may remain presentation-only after historical projectile
  execution is retired.
- The current Stage 1 condition definition is explicit neutral baseline content; lifecycle, replay,
  windows and status effects still use the real merged production authorities.
- This connected environment has no Unity executable or C# compiler. Unity compilation, XML results and
  PlayMode/manual proof are not claimed here.
