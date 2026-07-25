# WEAPON-RUNTIME-LIVE-001 — canonical live firing cutover

## Status

The inventory-backed runtime uses `WeaponFiringScheduler` as the sole live authority for trigger
transitions, cadence, cooldown timing, burst and pulse expansion, shot sequencing,
scheduler-derived emission operation identity, deterministic scheduling, replay admission, and
conflicting duplicate rejection.

The live route is:

```text
exact equipped EquipmentInstance
  -> explicit WeaponBlueprint mapping policy
  -> immutable EffectiveWeapon
  -> WeaponFiringScheduler
  -> scheduler-authorized AcceptedEmission
  -> AcceptedEmissionRuntimeAdapter
  -> immutable caller-owned pending-delivery state
  -> composition-owned due-time drain
  -> WeaponBehaviorRegistry
  -> immutable WeaponEffectBatch
  -> existing inventory effect sink
```

No scene, prefab, Stage 1, strongbox, simulator, package, project-setting, test, or unrelated gameplay
connection is part of this cutover.

## Authority boundary

`WeaponFiringScheduler` remains the only firing-admission authority. The pending state does not:

- decide whether firing is allowed;
- calculate cadence or cooldown;
- expand bursts or pulses;
- assign shot sequences or operation IDs;
- classify physical trigger edges;
- select targets;
- recreate scheduler emissions;
- own random generation, behavior selection, projectile simulation, damage, or effects.

It only retains immutable scheduler-authorized emissions until their exact `ScheduledTick` is due and
the existing effect sink accepts the projected batch.

The canonical live route does not construct or invoke:

- `WeaponExecutionCore`;
- `WeaponCatalogRuntimeProfileResolver`;
- `DefaultWeaponBehaviorSelector`.

## Effective weapon resolution

`InventoryWeaponEffectiveResolver` resolves the exact requested equipment instance through existing
holdings and catalog authorities, explicit blueprint mapping policy, installed augment policy, and
`EffectiveWeaponFactory`.

Resolution fails closed when exact semantics are unavailable. It never substitutes a starter weapon,
related family, blaster, definition-ID fallback, inferred behavior, or item-level combat scaling.

## Pending-delivery state

`InventoryWeaponPendingDeliveryState` is immutable and replace-on-write. Every pending entry preserves:

- scheduler-authored `ScheduledTick`;
- scheduler-derived `EmissionFireOperationId`;
- `SourceFireOperationId` as lineage only;
- accepted-emission and effective-weapon fingerprints;
- actor, participant, equipment, definition, and lifecycle identities;
- cadence, trigger-group, burst, pulse, and emission ordinals;
- shot sequence and projectile ordinal;
- the complete immutable projected `WeaponEffectBatch`;
- the exact retained inventory effect profile.

Pending identity is actor + lifecycle + scheduler-derived emission operation ID. Exact identity and
fingerprint duplicates are idempotent. Changed immutable content under the same identity rejects before
sink mutation.

### Capacity semantics

`Capacity` limits actual pending delivery work only. Delivered receipts do not consume pending capacity.
Therefore a long-running actor lifecycle does not acquire a permanent shot limit merely because old
emissions were delivered.

Pending capacity exhaustion rejects explicitly. It never:

- discards pending work;
- sends future work early;
- evicts receipts under pressure;
- rolls scheduler state back after an accepted schedule was safely queued.

### Delivered-receipt retention

Delivered receipts exist only so an exact retained scheduler replay cannot reconstruct an emission that
already left the outbox. Each receipt retains the stable actor/lifecycle/emission identity and exact
pending-entry fingerprint.

`PruneDeliveredReceipts(WeaponFiringSessionState)` derives the receipt keep-set only from retained
scheduler replay records containing accepted schedules. Receipts whose accepted-schedule replay records
were pruned by the canonical scheduler are removed deterministically. Pending entries are never removed
by receipt pruning.

Consequently:

- still-replayable delivered emissions retain exact receipts;
- conflicting immutable content still rejects while the scheduler replay is retained;
- once the scheduler forgets an operation, the outbox also stops acting as a replay authority;
- receipt retention is bounded by canonical scheduler replay retention rather than actor lifetime.

## Atomic scheduler/outbox publication

For a successful scheduler transition:

1. resolve the exact `EffectiveWeapon`;
2. call `WeaponFiringScheduler.Schedule` with the composition-owned session snapshot;
3. validate the accepted schedule and each accepted emission;
4. adapt every emission into its immutable projected batch;
5. admit the full schedule into a candidate pending snapshot;
6. prune delivered receipts against the new scheduler replay snapshot;
7. under `InventoryWeaponRuntimeComposition.firingStateGate`, publish the scheduler and pending
   snapshots together;
8. only after publication, drain entries already due.

If mapping, validation, adaptation, dedupe, pending admission, or receipt pruning fails, neither candidate
snapshot is published.

A later sink failure does not roll scheduler state or pending admission back. The accepted operation
remains represented and cannot be scheduled again as a new shot.

## Sole delivery route

The production effect-delivery route is only:

```text
InventoryWeaponRuntimeComposition
  -> firingStateGate
  -> resolve current actor and lifecycle
  -> inspect composition-owned pending state
  -> select exact retained first due entry
  -> internal adapter sink bridge
  -> mark delivered only after Accepted or exact AlreadyAccepted
```

`InventoryBackedWeaponExecutionAdapter.TryDeliverPending` is assembly-internal. The player composition
root no longer publicly exposes the concrete adapter; `Runtime` is the production firing surface.

No public production method can submit an arbitrary pending entry. The composition enforces:

- runtime is not disposed;
- current actor and lifecycle resolve successfully;
- a legitimate replacement lifecycle clears scheduler, pending, receipts, and edge state;
- the entry belongs to the active actor lifecycle;
- the entry is the deterministic first retained due entry;
- `entry.ScheduledTick <= currentSimulationTick`.

The actor and lifecycle are re-resolved immediately before each sink submission. A previous-lifecycle
entry cannot reach the sink.

## Due-time draining and sink failure

`Advance(simulationTick)` and `DrainDueEmissions(simulationTick)` drain independently of trigger input.
Future burst and pulse emissions remain pending until due.

Pending order is deterministic:

1. scheduled tick;
2. cadence ordinal;
3. trigger-group ordinal;
4. burst-shot ordinal;
5. pulse ordinal;
6. emission ordinal;
7. scheduler-derived emission operation identity.

For each due entry:

- sink `Accepted` removes the exact entry and records a delivery receipt;
- exact sink `AlreadyAccepted` also removes it;
- rejection, exception, or invalid response retains it;
- drain stops on the first failed entry;
- later due entries cannot overtake it;
- no future entry is submitted.

## Replay and partial delivery

Exact scheduler replay projects the retained accepted schedule again only to validate immutable content
against pending entries and delivered receipts:

- still-pending entries remain once;
- delivered entries are recognised through exact receipts;
- missing replay entries reject closed;
- changed content under an existing identity rejects;
- no successful emission is scheduled twice.

For partial delivery where emission 0 succeeds and emission 1 fails:

- emission 0 is removed and receipted;
- emission 1 and later entries remain pending;
- scheduler state remains advanced;
- retry begins at emission 1;
- later entries do not overtake it.

## Trigger-edge transactional publication

`UpdateTriggerInput` classifies physical input only:

```text
not held -> held      = Pressed
held -> held          = Held
held -> not held      = Released
not held -> not held  = no scheduler request; drain due work only
```

It first calculates a candidate edge decision without mutating shared edge state. Every enabled mount
then receives the same scheduler signal with deterministic per-mount operation ID, origin, and seed.

The candidate edge publishes only when every mount has either:

- published its scheduler/pending snapshot pair; or
- produced an exact canonical replay.

If a mapping, adaptation, or pending-admission failure occurs before one mount is safely represented,
the previous physical edge state remains unchanged. Mounts that already published replay/dedupe on the
same-operation retry, while the failed mount can retry without losing the physical edge.

Once every mount is safely represented, edge state publishes before due delivery. A later sink failure
does not roll the edge back; retry drains retained pending work rather than scheduling a new shot.

Idle no-request input may initialise the non-held edge state and drain due work.

`TryTrigger(..., WeaponTriggerSignal)` remains the lower-level caller-classified scheduler surface.
`TryFire` and default-`Pressed` helpers remain obsolete one-shot compatibility surfaces only.

## Complete per-mount outcomes

`InventoryWeaponExecutionResult.MountOutcomes` is the canonical ordered record of every enabled mount
attempt. Each `InventoryWeaponMountExecutionOutcome` includes:

- stable mount identity when available;
- mount ordinal;
- exact equipment instance identity;
- top-level outcome kind;
- `WeaponExecutionStatus`;
- scheduler status when a scheduler decision exists;
- exact rejection code;
- exact-replay flag;
- scheduled emission count;
- whether the scheduler/pending pair published;
- retryability;
- source classification: successful scheduling, scheduler rejection, or integration rejection.

Outcomes remain in deterministic enabled-mount order. Equal statuses or diagnostics are never merged.
Later mount failures are not discarded.

`SchedulingOutcomes` remains only as a successful-scheduling compatibility projection. New callers should
use `MountOutcomes`.

The aggregate top-level result uses deterministic precedence:

1. retryable delivery failure;
2. mount integration failure;
3. mount scheduler rejection;
4. successful due delivery;
5. accepted new schedule;
6. accepted replay schedule;
7. successful no-emission transition;
8. no due delivery.

Per-mount scheduling/integration outcomes remain separate from zero-or-many delivered batches. A mount
failure cannot erase batches already delivered safely during the same call, and delivery cannot hide
mount failures.

## Lifecycle and disposal

A stale externally supplied request rejects before it can clear current state.

A verified actor or lifecycle replacement clears:

- scheduler session state;
- pending deliveries;
- delivered receipts;
- trigger-edge state.

`Dispose` clears those states plus active actor/lifecycle references. A disposed composition cannot
deliver an old entry.

## Supported exact compatibility subset

Supported exactly:

- semi-automatic projectile firing;
- automatic projectile firing;
- scheduler-expanded burst timing;
- scheduler-expanded pulse timing;
- single projectile patterns;
- authored spread patterns;
- pulse-spread patterns;
- integer pierce;
- unguided regular projectiles;
- retained exact rocket/explosion shape;
- existing projectile and explosive behaviors.

Rejected without fallback:

- continuous beams or damage ticks;
- chain arcs;
- orb projectiles;
- homing or reacquisition;
- ricochet and post-bounce homing pause;
- damage over time or persistent pools;
- unrepresentable twin-barrel origins;
- unrepresentable random deviation;
- fractional pierce;
- lossy impact combinations;
- lossy explosion triggers;
- missing behavior registration;
- invalid behavior output;
- identity-mismatched behavior output.

## Validation scope

Manual source tracing covered:

- long-running receipt pruning beyond scheduler replay-retention capacity;
- replay before and after scheduler replay pruning;
- future burst/pulse due-time enforcement;
- stop-on-first sink failure and retained pending order;
- transactional trigger-edge retry before and after pending publication;
- four-mount mixed success, integration rejection, unsupported effects, and no-emission transitions;
- lifecycle replacement, stale request rejection, and disposal;
- exact effect operation identity and source-operation lineage;
- absence of the three legacy firing authorities from the repaired live route.

No automated tests were added under the prototype policy. This document does not claim Unity compilation,
standalone compilation, CI, automated tests, or in-game validation unless those are reported separately
as actually executed.
