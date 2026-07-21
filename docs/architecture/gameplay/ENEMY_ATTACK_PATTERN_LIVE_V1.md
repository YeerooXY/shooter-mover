# ENEMY-ATTACK-PATTERN-LIVE-001 — live scheduler boundary

## Ownership

`EnemyAttackPatternLiveSchedulerV1` is the production-facing implementation of
`IEnemyAttackPatternEffectPortV1`. The engine-neutral pattern authority continues to own sequence
construction, immutable committed aim, stable sequence/emission identities, fingerprints and
lifecycle cancellation facts.

The live scheduler owns only downstream delivery state:

- atomic acceptance of a complete immutable sequence;
- pending emission ordering;
- exact-once emission delivery;
- cancellation of not-yet-emitted work;
- immutable/debug delivery records.

It does not own enemy decisions, cooldowns, player health, projectile simulation, hit eligibility or
Run Session time.

## Authoritative time

Unity may call `Tick()` from `Update`, a coroutine or another presentation wake-up mechanism. `Tick`
never accumulates `Time.deltaTime`. It reads `IEnemyAttackPatternRunTimeV1.CurrentTimeSeconds`, which
must be backed by the active Run Session clock. The same port validates that the immutable execution
still belongs to the current run and lifecycle generation.

This keeps variable frame intervals presentation-only: a late frame emits every due fact once, sorted
by `ScheduledAtSeconds` and then `EmissionStableId`.

## Atomic dispatch and replay

Before queue mutation, every emission is passed to
`IEnemyAttackPatternEmissionRealizerV1.CanRealize`. One rejection rejects the complete sequence and
queues nothing. Accepted sequence IDs retain their canonical dispatch fingerprint:

- same ID and fingerprint: `ExactReplay`;
- same ID and another fingerprint: `ConflictingDuplicate`;
- wrong run or lifecycle: fail closed;
- first valid delivery: `Applied`.

Caller-owned collections are not retained. `EnemyAttackSequenceDispatchV1` already owns its immutable
sorted copy, and the scheduler copies that read-only sequence into its private pending list.

## Committed aim and projectile/melee realization

The scheduler delivers the original `EnemyAttackEffectEmissionV1` unchanged. Realizers must consume:

- `CommittedIntent.Origin` and `CommittedIntent.Direction`;
- committed target identity;
- projectile payload and schema-generated spread offset;
- sequence and emission identities;
- source participant and lifecycle generation;
- scheduled and active-window timestamps;
- resolved damage and damage channel.

Realizers must not retarget delayed shots or reroll scatter. Projectile realizers adapt these facts to
the existing reusable projectile runtime. Melee/pounce realizers use the same facts to open and close
contact windows. Candidate contacts then route through the existing Combat Hit Policy and accepted
damage through `PlayerActorAuthority`; Unity callbacks do not mutate health directly.

## Cancellation

Cancellation facts are replay-protected by cancellation identity and fingerprint. Listed pending
projectile and melee emission IDs are removed. Already emitted projectiles are not erased. If an
already-open melee window is explicitly cancelled, the realizer receives `CancelActiveWindow` so its
candidate-contact projection can close without direct combat mutation.

## Extension path

A future burst, shotgun, rocket, contact or pounce enemy should add or change its schema-v2 definition
and presentation binding. Shared scheduling code does not branch on enemy name, attack name, weapon
name, room or prefab.

## Current integration limit

This change establishes and tests the production-safe scheduler/realizer seam. The repository still
needs concrete Stage 1 typed composition bindings from Run Session time into
`IEnemyAttackPatternRunTimeV1`, plus projectile/melee realizers that delegate to the existing
projectile and Combat Hit Policy adapters. Production catalog migration must remain fail-closed until
those typed bindings are available; this change intentionally does not translate schema-v2 content
back into the historical one-call execution path or edit `Stage1VisibleSliceController.cs`.
