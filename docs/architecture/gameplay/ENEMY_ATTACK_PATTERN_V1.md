# ENEMY-ATTACK-PATTERN-001 — deterministic enemy attack patterns

## Purpose

Enemy attacks may be represented by one of two mutually exclusive canonical models:

- `shooting_pattern` plus `projectile_payload`;
- `melee_pattern`.

Schema-v1 catalog input remains the production compatibility format. Schema-v2 is supported and validated by focused fixtures, but the authored production catalog is intentionally not migrated until a concrete Unity scheduled-effect adapter is available.

## Pattern ownership

A shooting sequence owns shot count, interval, projectiles per shot, spread, aim policy, wind-up, recovery, and interruption policy. The projectile payload owns profile, speed, travel distance, collision radius, pierce, and optional area payload.

A melee sequence owns wind-up, active window, strike count and interval, contact radius, lunge distance, aim commitment, recovery, per-target hit limit, terminal-on-impact behavior, and interruption policy.

Contact and pounce are data configurations of the same reusable model rather than separate enemy controllers.

## V1 policy contract

Schema-v2 V1 accepts only behavior the runtime currently realizes without mutable target tracking or an impact ledger:

- shooting aim: `lock-at-sequence-start`;
- melee aim: `lock-at-wind-up`;
- melee terminal behavior: `continue-sequence`;
- interruption: `cancel-pending-on-lifecycle-end` or `complete-committed-sequence`.

Reserved tracking, per-strike locking, and terminal-on-impact end policies reject during JSON import with `enemy-catalog-attack-policy-unsupported-v1`. The runtime authority also fails closed if unsupported models are constructed directly.

## Atomic live dispatch boundary

For canonical schema-v2 descriptors, the engine-neutral live flow is:

```text
accepted EnemyAttackExecutionRequestV1
        ↓
EnemyAttackPatternAuthorityV1.Start
        ↓
immutable EnemyAttackSequenceV1 + emission facts
        ↓
EnemyAttackSequenceDispatchV1
        ↓
IEnemyAttackPatternEffectPortV1.Dispatch
```

Descriptors contained by a schema-v1 catalog are tagged as compatibility content when the catalog is constructed. They bypass pattern authority and retain the historical single `IEnemyAttackEffectPortV1.Emit(execution)` call. This prevents schema-v1 pounce, turret, and contact timing projections from creating scheduled identities that the old adapter cannot own or cancel.

The downstream port receives the complete sequence in one immutable batch:

```csharp
EnemyAttackPatternDispatchResultV1 Dispatch(
    EnemyAttackSequenceDispatchV1 sequence);

EnemyAttackPatternDispatchResultV1 Cancel(
    EnemyAttackSequenceCancellationFactV1 cancellation);
```

A scheduled consumer must prevalidate the complete batch before committing any queued projectile or melee window. It keys accepted delivery by the sequence or cancellation stable ID and canonical fingerprint:

- first equivalent delivery returns `Applied`;
- repeated equivalent delivery returns `ExactReplay`;
- the same stable ID with another fingerprint returns `ConflictingDuplicate`;
- rejected or failed prevalidation leaves no partial sequence in the consumer.

The runtime catches downstream exceptions and converts them into a rejected dispatch result. This does not make an internally non-atomic adapter safe; atomic prevalidation and commit are requirements of the port contract.

## Outer runtime transaction order

The enemy runtime deliberately commits in this order:

1. validate decision, aim, attack definition, and execution;
2. obtain the deterministic pattern sequence from `EnemyAttackPatternAuthorityV1`;
3. dispatch the complete immutable sequence batch;
4. only after downstream `Applied` or `ExactReplay`, record the accepted execution;
5. apply cooldown;
6. record the outer attack replay result.

If dispatch rejects or throws, steps 4–6 do not occur. The pattern authority retains the original immutable sequence, so retrying the same attack operation obtains its exact sequence replay and redelivers the same batch. A successful retry commits accepted execution, cooldown, and outer replay exactly once.

## Lifecycle cancellation

`CancelAttackPatterns` first obtains the canonical terminal cancellation fact from the pattern authority and then delivers that fact through the same replay-safe downstream boundary.

An exact retry of the cancellation command redelivers the same fact. The consumer returns `Applied` after a prior failed attempt or `ExactReplay` after prior acceptance. Therefore a transient cancellation failure cannot permanently strand the scheduler without an authoritative retry route.

With `cancel-pending-on-lifecycle-end`, shots or strikes after the authoritative terminal time are cancelled. Emissions at or before the cancellation boundary remain valid. With `complete-committed-sequence`, the committed sequence is retained while the actor still becomes terminal for new starts.

## Automatic enemy-terminal composition

The authoritative damage/death path now invokes attack-pattern cancellation automatically before publishing terminal collision, room-terminal, XP, drop, and kill-stat facts.

`ApplyDamage(command, occurredAtSeconds)` supplies the authoritative terminal time. The compatibility overload uses time zero and conservatively cancels every pending scheduled emission.

If the first cancellation delivery fails:

- actor death and the immutable death fact remain canonical;
- terminal consequence publication is held pending;
- the damage operation is not entered into the completed replay ledger;
- exact retry of the same damage operation retries the same cancellation fact;
- successful retry publishes terminal consequences and records damage replay once.

Tests prove that a three-shot burst started at `10.0` and terminalized at `10.1` executes only the `10.1` projectile; the `10.3` and `10.5` projectiles never execute.

## Legacy compatibility

A descriptor from a schema-v1 catalog retains the historical one-call `IEnemyAttackEffectPortV1.Emit(execution)` boundary and never enters pattern authority. This is an explicit deferred-cutover path, not an attempt to reinterpret schema-v1 wind-up or commitment fields as scheduled facts.

For untagged canonical descriptors, the old port is accepted only for a genuinely equivalent single immediate emission. Timed, multi-shot, multi-projectile, spread, or active-window attacks fail closed unless the port implements the atomic pattern interface.

The legacy callback cannot provide the same replay guarantees if an implementation throws after performing its side effect. It remains a compatibility seam preserving existing production behavior, not the production target for schema-v2 content.

## Production cutover status

The repository does not yet contain a concrete Unity adapter that atomically queues `EnemyAttackSequenceDispatchV1`, advances by `ScheduledAtSeconds`, realizes melee active windows, and consumes cancellation facts.

Accordingly:

- schema-v2 importer/model/runtime support remains in this task;
- schema-v2 replacement content is exercised through focused fixtures;
- the production `enemy_catalog_v1.json` remains schema v1 and executes through the old one-call boundary;
- schema-v1 execution does not create pattern sequences or scheduled cancellation IDs;
- production schema-v2 migration is deferred to the adapter/cutover task.

This prevents authored wind-up, burst, spread, or melee-window content from becoming live through an adapter that cannot represent it safely.

## Fingerprinting

Each emission fingerprint binds:

- the complete execution fingerprint;
- the canonical sequence fingerprint;
- exact sequence and emission identity;
- scheduled and active-window timing;
- projectile or strike identity;
- projectile spread offset.

The nested execution and sequence fingerprints bind descriptor and payload facts, committed target and direction, source participant, run, room runtime, room definition, placement, lifecycle, damage, cooldown, and execution kind.

## Proof fixtures

Focused EditMode tests cover:

- single, burst, rapid, shotgun, explosive, contact, and pounce scheduling;
- immutable atomic sequence dispatch;
- exception during second-emission prevalidation with zero partial queue state;
- exact retry after failed dispatch without duplicate accepted-execution insertion or cooldown mutation;
- cancellation failure followed by exact redelivery and one accepted cancellation;
- automatic cancellation from authoritative lethal damage;
- automatic death-path cancellation retry;
- already-due projectile survival and pending projectile suppression;
- fail-closed legacy fallback;
- material emission fingerprint changes and conflicting duplicate rejection;
- schema-v2 policy validation through fixtures;
- production catalog remaining schema v1 pending real adapter cutover.
