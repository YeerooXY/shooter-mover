# WEAPON-RUNTIME-LIVE-001 — canonical live firing cutover

## Status

The inventory-backed runtime now uses `WeaponFiringScheduler` as the sole live authority for trigger
state, cadence, burst and pulse expansion, cooldown timing, shot sequencing, scheduler-derived
emission operation identity, deterministic scheduling, replay admission, and conflicting duplicate
rejection.

The corrected live route is:

```text
exact equipped EquipmentInstance
  -> explicit modular blueprint mapping
  -> immutable EffectiveWeapon
  -> WeaponFiringScheduler
  -> scheduler-authorized AcceptedEmission values
  -> AcceptedEmissionRuntimeAdapter
  -> immutable caller-owned pending-delivery state
  -> due-time drain
  -> existing WeaponBehaviorRegistry projection
  -> immutable WeaponEffectBatch
  -> existing IInventoryWeaponEffectBatchSink
```

No scene, prefab, Stage 1, strongbox, simulator, package, project-setting, or unrelated gameplay
connection is part of this cutover.

## Authority boundary

`WeaponFiringScheduler` remains the only firing-admission authority. The downstream pending state:

- does not decide whether firing is allowed;
- does not calculate cadence or cooldown;
- does not expand bursts or pulses;
- does not assign shot sequences or operation IDs;
- does not select targets;
- does not recreate scheduler emissions;
- does not own random generation, behavior selection, projectile simulation, damage, or effects.

It only retains immutable scheduler-authorized emissions until their authored `ScheduledTick` is due
and the existing effect sink accepts the exact projected batch.

## Effective weapon resolution

`InventoryWeaponEffectiveResolver` resolves the exact requested equipment instance through existing
holdings and catalog authorities, explicit blueprint mapping policy, installed augment policy, and
`EffectiveWeaponFactory`.

Resolution fails closed when exact semantics are unavailable. It never substitutes a starter weapon,
related family, blaster, definition-ID fallback, inferred behavior, or item-level combat scaling.

## Pending-delivery state

`InventoryWeaponPendingDeliveryState` is immutable and replace-on-write. Every pending entry preserves:

- scheduler-authored `ScheduledTick`;
- `EmissionFireOperationId`;
- `SourceFireOperationId` lineage;
- accepted-emission fingerprint;
- effective-weapon fingerprint;
- actor identity;
- participant identity;
- equipment instance identity;
- weapon definition identity;
- lifecycle generation;
- cadence, trigger-group, burst, pulse, and emission ordinals;
- shot sequence;
- projectile ordinal and the complete immutable multi-projectile batch;
- exact `InventoryWeaponEffectProfile` required by the retained sink;
- immutable projected `WeaponEffectBatch`.

The adapter validates and adapts every accepted emission before pending admission. A future delivery
therefore cannot resolve a changed equipment instance, catalog entry, modular blueprint, augment set,
behavior registration, or effect profile.

Pending identity is actor + lifecycle + scheduler-derived emission operation ID. Exact identity and
fingerprint duplicates are idempotent. The same identity with changed immutable content is rejected
before sink mutation. Delivered fingerprints are retained only as downstream delivery receipts so an
exact scheduler replay cannot recreate an emission that already left the outbox. Those receipts are
not consulted by firing admission.

The state has a deterministic bounded capacity. Exhaustion rejects explicitly; it never discards an
older entry and never submits future work early.

## Atomic scheduler/outbox publication

For a newly accepted scheduler decision:

1. Resolve the exact `EffectiveWeapon`.
2. Call `WeaponFiringScheduler.Schedule` with the composition-owned session snapshot.
3. Validate the accepted schedule against the exact request and effective-weapon fingerprint.
4. Adapt every accepted emission into its immutable projected batch and inventory effect profile.
5. Admit the complete schedule into a candidate pending-delivery snapshot.
6. Under `InventoryWeaponRuntimeComposition.firingStateGate`, publish the scheduler `NextState` and
   pending-delivery snapshot together.
7. Only after publication, drain entries that are already due.

If validation, adaptation, dedupe, or capacity admission fails, neither scheduler state nor pending
state is published.

Once the complete accepted schedule is safely retained, scheduler state is not rolled back because of
a later sink failure. The operation cannot be forgotten and scheduled again.

## Due-time draining

`InventoryWeaponRuntimeComposition.Advance(simulationTick)` and its
`DrainDueEmissions(simulationTick)` alias drain pending work independently of trigger input.

Only entries with `ScheduledTick <= simulationTick` are submitted. Ordering is deterministic:

1. scheduled tick;
2. cadence ordinal;
3. trigger-group ordinal;
4. burst-shot ordinal;
5. pulse ordinal;
6. emission ordinal;
7. scheduler-derived emission operation ID.

For each due entry:

- sink `Accepted` removes the exact entry and records its delivery fingerprint;
- exact sink `AlreadyAccepted` also removes it;
- sink rejection, exception, or an invalid response retains it;
- draining stops at the first failed entry so later emissions cannot overtake it;
- no entry is removed before confirmed sink acceptance;
- no future entry is submitted.

This lets scheduler-expanded burst and pulse sequences continue after release, during idle input, for
semi-automatic weapons, and after a temporary sink failure.

## Replay and partial delivery

Exact scheduler replay returns the scheduler's retained schedule. The adapter projects it again only
to validate exact immutable content against the outbox:

- still-pending entries are recognised as exact duplicates;
- already delivered entries are recognised through exact delivery receipts;
- no pending entry is appended twice;
- no delivered entry is recreated;
- a missing replay entry is rejected rather than silently reconstructed;
- changed content for an existing emission identity is a conflicting duplicate.

For a partial delivery where emission 0 is accepted, emission 1 fails, and emission 2 is not attempted:

- emission 0 is removed and recorded as delivered;
- emissions 1 and 2 remain pending;
- scheduler state remains advanced;
- retry starts at emission 1;
- emission 0 is not scheduled as a new shot.

The existing sink remains the final exact identity/fingerprint idempotency authority.

## Trigger input

The runtime exposes two explicit input surfaces:

- `UpdateTriggerInput(isHeld, operationId, ...)` owns only physical input-edge memory;
- `TryTrigger(..., WeaponTriggerSignal)` accepts a caller-classified explicit scheduler transition.

`UpdateTriggerInput` classifies:

```text
not held -> held      = Pressed
held -> held          = Held
held -> not held      = Released
not held -> not held  = no scheduler request; drain due work only
```

It does not calculate cadence and does not emulate automatic fire with repeated synthetic presses.
Each real scheduler request requires a deterministic operation ID. An exact retry reuses the same ID
and exact input facts; changed facts under the same ID reject as a conflicting duplicate.

The same trigger signal is delivered to every enabled mount. Stable per-mount operation IDs, muzzle
offsets, and deterministic seed separation remain derived from the caller operation and stable mount
identity.

`TryFire` and the default-`Pressed` intent helpers remain only as obsolete one-shot compatibility
surfaces. They do not own cadence and are not the live held-fire API.

The repository currently has no production Unity gameplay/input owner after the architecture cleanup.
This PR therefore exposes the honest input-facing API and per-tick `Advance` operation but does not
claim an in-scene input hook. No scene or Stage 1 route was restored to manufacture one.

## Stateless adapter entry points

The stateless adapter overload no longer schedules from `WeaponFiringSessionState.Empty`; it fails
closed with `weapon-live-firing-state-required`.

The scheduler-state-only overload also fails closed with
`weapon-live-pending-delivery-state-required`.

Live production execution goes through `InventoryWeaponRuntimeComposition`, which owns both immutable
state snapshots under one lock.

## Result semantics

`InventoryWeaponExecutionResult` no longer treats a successful no-emission transition as a fake
zero-effect shot.

It reports separately:

- per-mount scheduler outcomes;
- accepted or replayed schedules;
- accepted or replayed no-emission transitions;
- waiting-for-cadence and release status;
- zero, one, or many batches delivered by the current call;
- accepted versus exact-already-accepted sink deliveries;
- pending count;
- retryable delivery failure;
- scheduler or integration rejection.

`EffectBatch` is populated only when exactly one batch was delivered. `DeliveredBatches` is the
canonical zero-or-many surface. Shot-sequence presence is explicit; sequence zero is not used to
represent a successful transition without a shot.

A call may both accept a trigger transition and drain due work. Per-mount scheduling outcomes are
retained beside the delivery summary so an immediate batch cannot hide a release, waiting state, or
replayed transition.

## Concurrent mounts

Each enabled mount preserves its exact equipment identity and independent scheduler track. One
mount's cadence cannot block another mount, and one mount's pending entry cannot be confused with
another's.

All mounts are scheduled before the global due drain, so cross-mount delivery order follows the same
pending ordering rules rather than caller loop order alone.

If one mount fails after another mount has safely published a schedule, retrying the aggregate
operation replays/dedupes the successful mount and continues the failed mount without scheduling the
successful emission twice. Per-mount scheduler outcomes and all delivered batches remain visible in
the aggregate result.

## Lifecycle and disposal

The composition verifies the trusted current actor and lifecycle before activating a replacement.
A stale request rejects before it can clear current state.

A verified actor or lifecycle replacement clears:

- scheduler session state;
- pending deliveries and delivery receipts;
- input-edge state.

`Dispose` clears those states plus active actor/lifecycle references. The disposed composition cannot
deliver an old pending emission.

## Supported exact compatibility subset

The retained behavior registry and effect-batch boundary support only exact projections of:

- semi-automatic projectile firing;
- automatic projectile firing;
- scheduler-expanded burst timing;
- scheduler-expanded pulse timing;
- single projectile patterns;
- authored spread patterns;
- pulse-spread patterns;
- integer pierce;
- unguided regular projectiles;
- the retained exact rocket/explosion shape;
- existing projectile and explosive behaviors.

The adapter rejects without fallback:

- continuous beams or damage ticks;
- chain arcs;
- orb projectile semantics;
- homing or reacquisition;
- ricochet and post-bounce pause;
- damage over time or persistent pools;
- twin-barrel origin semantics not represented downstream;
- random pattern deviation not represented downstream;
- fractional pierce;
- lossy impact or explosion-trigger combinations;
- missing behavior registration;
- invalid behavior output;
- identity-mismatched behavior output.

## Legacy authority reachability

The canonical live route does not construct or invoke:

- `WeaponExecutionCore`;
- `WeaponCatalogRuntimeProfileResolver`;
- `DefaultWeaponBehaviorSelector`.

They remain obsolete for existing non-production tooling/regression consumers and are not retired in
this focused repair. The older heat, charge, power-bank, recoil, profile serialization, behavior
module, and old mount-stepper ecosystem also remains a separate cleanup task.

`WeaponRuntimeFiringProfile` remains only as a compatibility DTO consumed by the retained behavior
registry. Its cooldown value is projected from a scheduler-authorized emission and never admits fire.

## Validation scope

This repair was validated by direct source tracing of:

- scheduler accepted schedule and emission fingerprints;
- adapter schedule validation and exact effect identity checks;
- pending admission, dedupe, capacity, and deterministic ordering;
- scheduler/outbox publication under the composition lock;
- due-only drain and first-failure stop behavior;
- exact replay before and after partial delivery;
- no-emission transition publication and replay;
- per-mount operation, origin, seed, scheduling, and result aggregation;
- lifecycle replacement, stale request rejection, and disposal;
- the player inventory weapon composition root;
- absence of live references to the three legacy firing authorities in the repaired route.

The repository code-search index was unavailable, so empty search results were not used as proof of
absence. Known composition roots and relevant files were inspected directly.

No automated tests were added under the prototype policy. Unity compilation, CI, automated tests, and
in-game validation must be reported only if they actually run elsewhere; this document does not claim
them.
