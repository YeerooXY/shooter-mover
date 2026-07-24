# WEAPON-RUNTIME-LIVE-001 — canonical live firing cutover

## Status

Production inventory-backed firing is routed through `WeaponFiringScheduler`. The previous
`WeaponExecutionCore` route is no longer constructed or invoked by the live inventory adapter,
player weapon composition root, or concurrent-mount composition.

This document describes the production authority boundary, the compatibility projection into the
existing runtime effect system, and the intentionally retained legacy tooling types.

## Canonical live route

```text
exact equipped EquipmentInstance
  -> EquipmentCatalog exact definition/runtime reference
  -> explicit WeaponBlueprintMappingPolicyRegistry entry
  -> WeaponCatalogBlueprintMapper
  -> immutable WeaponBlueprint
  -> EffectiveWeaponFactory
  -> immutable EffectiveWeapon
  -> WeaponFiringScheduler.Schedule(request, current WeaponFiringSessionState)
  -> WeaponFiringScheduler.AcceptedEmission
  -> AcceptedEmissionRuntimeAdapter
  -> temporary WeaponRuntimeFiringProfile compatibility projection
  -> existing WeaponBehaviorRegistry
  -> existing WeaponBehaviorContext
  -> existing immutable WeaponEffectBatch
  -> InventoryWeaponEffectBatch projection
  -> existing IInventoryWeaponEffectBatchSink
```

The route contains one cadence/replay/shot-sequence authority: `WeaponFiringScheduler` plus the
caller-owned immutable `WeaponFiringSessionState` returned by it.

## Exact effective-weapon resolution

`InventoryWeaponEffectiveResolver` resolves one exact equipment instance and fails closed when any
step cannot be represented exactly.

1. The requested `EquipmentInstanceId` is resolved through the existing player holdings/equipment
   lookup.
2. The resolved instance identity must exactly match the requested identity.
3. `EquipmentCatalog.ValidateInstance` validates the immutable instance, definition, quality, item
   level metadata, and installed augment identities.
4. The exact equipment definition supplies the existing runtime weapon definition reference.
5. The current `WeaponCatalog` must contain that exact definition and mark it `Live`.
6. One explicit `WeaponCatalogBlueprintMappingIntent`, keyed by `WeaponDefinitionId`, must exist in
   `WeaponBlueprintMappingPolicyRegistry`.
7. `WeaponCatalogBlueprintMapper` creates the immutable `WeaponBlueprint`; it is not allowed to
   guess missing semantics.
8. One `IWeaponAugmentModifierSetResolver` resolves the exact installed augment instances into the
   canonical modifier sets consumed by `EffectiveWeaponFactory`.
9. `EffectiveWeaponFactory` produces the immutable `EffectiveWeapon` without mutating the
   blueprint, equipment instance, or augment instances.

Item level remains identity/progression metadata and is not used for combat scaling.

The supplied `UnaugmentedWeaponModifierSetResolver` is intentionally narrow: it accepts equipment
with no installed augments and rejects augmented equipment until composition supplies the canonical
augment-to-modifier policy. Missing or preview-only catalog content, missing mapping policy, missing
augment policy, incompatible structure, or invalid modifiers are explicit rejections. No replacement
equipment, starter weapon, or fallback behavior is selected.

The current `main` catalog composition is intentionally empty after the architecture cleanup. This
cutover therefore supplies the one keyed policy authority and explicit production injection point; it
does not invent per-definition semantics or import the open PR #288 catalog branch.

## Trigger and cadence authority

Live requests carry one of the scheduler's explicit trigger transitions:

- `Pressed`
- `Held`
- `Released`

`InventoryWeaponRuntimeComposition.TryTrigger` delivers the same transition to every enabled
physical mount. The retained `TryFire` compatibility method delegates immediately to `Pressed`; it
owns no cooldown, replay, accepted-operation, shot sequence, or emission expansion logic.

Burst and pulse timing are expanded by `WeaponFiringScheduler`. The runtime adapter consumes each
accepted emission independently and never reconstructs a burst or pulse from catalog values.

## Firing-session state ownership

`InventoryWeaponRuntimeComposition` owns exactly one current immutable
`WeaponFiringSessionState` snapshot for its run/runtime lifetime.

- `WeaponFiringScheduler` remains configuration-only.
- The composition passes its current snapshot into `Schedule` through the execution adapter.
- The adapter returns an `InventoryWeaponExecutionTransition` containing the candidate `NextState`
  and an explicit publication flag.
- The composition is the only live boundary that assigns the next snapshot.
- Exact replays return the unchanged authoritative snapshot and are not assigned again.
- Before execution, the composition verifies that the request actor/lifecycle still equals the
  trusted current player actor/lifecycle.
- A verified actor or lifecycle replacement clears the complete snapshot before scheduling the new
  generation; a stale request is rejected and cannot clear current state.
- `Dispose` replaces the complete snapshot with `WeaponFiringSessionState.Empty`; state is not
  carried into another run/runtime composition.
- The scheduler's lifecycle-keyed tracks and bounded replay pruning remain unchanged.

No second replay dictionary, accepted-operation ledger, cooldown state, or shot counter was added.

## Accepted-emission compatibility adapter

`AcceptedEmissionRuntimeAdapter` consumes only:

```text
EffectiveWeapon + WeaponFiringScheduler.AcceptedEmission
```

It does not accept provisional schedule-entry DTOs.

Before building a batch it requires `acceptedEmission.HasValidFingerprint(effectiveWeapon)` and
validates the exact:

- projectile emission kind;
- actor identity;
- participant identity;
- equipment instance identity;
- weapon definition identity;
- lifecycle generation;
- source operation lineage;
- scheduler-derived emission operation identity;
- authoritative shot sequence;
- projectile ordinal;
- muzzle origin and deterministic spread direction;
- modular payload values represented by the downstream effect type.

`AcceptedEmission.EmissionFireOperationId`, which is the accepted emission command's derived
`FireOperationId`, is passed through `WeaponBehaviorContext` and must appear in every emitted
`WeaponEffectIdentity`. `SourceFireOperationId` remains scheduler lineage only.

`AcceptedEmission.TicksUntilNextEmission` is used only as the temporary compatibility value for
`WeaponRuntimeFiringProfile.CooldownTicks`. The downstream profile does not decide whether or when
firing occurs.

Behavior selection is structural:

- exact regular-projectile semantics select the existing projectile behavior;
- exact supported rocket/explosion semantics select the existing explosive behavior;
- no weapon-definition-ID switch is used;
- no fallback behavior is used.

The existing `WeaponBehaviorRegistry`, `WeaponBehaviorContext`, `WeaponEffectBatch`, and effect sink
remain the only downstream authorities.

## Transaction and retry order

For a newly accepted schedule:

1. Read the composition's current immutable session state.
2. Schedule against that snapshot.
3. Do not publish `NextState`.
4. Validate the accepted schedule against the exact request and `EffectiveWeapon`.
5. Adapt every accepted emission into an immutable effect batch in scheduler order.
6. Do not submit anything if any adaptation fails.
7. Submit projected batches in scheduler emission order.
8. Treat sink `Accepted` and exact `AlreadyAccepted` as success.
9. Publish the scheduler's `NextState` only after every batch succeeds.

If adaptation or sink submission rejects or throws, the composition retains the previous scheduler
state and returns an explicit retryable integration failure. Retrying the exact operation schedules
the same deterministic emissions. Batches accepted before a later failure are recovered through the
existing sink's exact identity/fingerprint behavior.

The Unity sink keys accepted batches by actor, lifecycle generation, and the scheduler-derived
emission operation ID. It returns `AlreadyAccepted` only when the projected batch fingerprint is
identical; changed content is rejected as a conflicting duplicate.

A successful no-emission transition publishes `NextState` without creating an empty batch. An exact
retained transition replay leaves state unchanged. An exact retained accepted-emission replay
resubmits the same immutable batches and relies on exact sink idempotency without advancing operation
or shot sequence.

## Concurrent mounts

The existing physical mount model is preserved.

For each enabled mount, `InventoryWeaponRuntimeComposition` derives and preserves:

- the exact mounted `EquipmentInstanceId`;
- a deterministic operation ID from the caller operation ID plus stable mount identity;
- mount-specific lateral muzzle origin;
- mount-specific deterministic seed separation;
- the shared trigger transition delivered consistently to every mount.

Scheduler tracks are keyed by exact actor/equipment/lifecycle identity, so each mount has independent
cadence and shot continuity. One mount's cooldown does not block another mount. Mounts are not
collapsed into one equipment identity or one batch.

A non-cooldown integration failure from any mount takes precedence in the aggregate return. Retrying
the same trigger replays already successful mounts through exact scheduler/sink idempotency and lets
the failed mount complete; a successful first mount cannot mask an unsupported or sink-failed later
mount.

## Supported compatibility subset

The current downstream runtime projection supports only semantics that the retained effect batch and
Unity effect instances can represent exactly:

- semi-automatic, automatic, and scheduler-expanded burst cadence;
- scheduler-expanded pulse timing where each accepted pulse emission maps independently;
- single, authored spread, and pulse-spread projectile patterns;
- integer pierce values with no fractional additional-hit chance;
- unguided regular projectiles with the retained blocking/pierce termination shapes;
- rockets with the retained exact all-terminal-event explosion shape and minimum damage multiplier
  of one;
- existing projectile and explosive behavior registrations.

## Explicitly unsupported runtime projection

The live adapter rejects rather than downgrades:

- continuous fire/beam damage ticks;
- chain arcs;
- orb projectiles;
- homing or reacquisition;
- ricochet and post-bounce guidance pause;
- damage over time and persistent pools;
- twin-barrel semantics whose per-barrel origin cannot be represented exactly;
- random pattern deviation not represented by the retained behavior;
- fractional pierce;
- unsupported impact/termination/explosion trigger combinations;
- unregistered behavior;
- invalid or identity-mismatched behavior output.

Future focused adapters should project continuous, chain, orb, guidance, ricochet, and DoT decisions
from their existing canonical domain components into new or extended immutable effect descriptions.
They must not add a second scheduler, projectile, guidance, impact, effect, or damage authority.

## Legacy authority reachability

### `WeaponExecutionCore`

Retained temporarily and marked obsolete for existing EditMode tooling/regression fixtures. Its own
partial implementation files still contain the old private cooldown, replay, accepted-operation, and
shot-sequence state, but no live Unity/player composition constructs or invokes it after this cutover.

Verified non-production consumers include the existing `WeaponExecutionCoreTests` partial fixture and
`WeaponExecutionCoreLiveRegressionTests`. These tests describe the superseded execution route and
should be retired or rewritten in a separate focused cleanup rather than mixed into the live cutover.

### `WeaponCatalogRuntimeProfileResolver`

Retained temporarily and marked obsolete because the legacy core/tooling fixtures still require its
flat-catalog projection. It is not reachable from `InventoryBackedWeaponExecutionAdapter`,
`InventoryWeaponRuntimeComposition`, or `PlayerInventoryWeaponRuntimeCompositionRoot`.

### `DefaultWeaponBehaviorSelector`

Retained temporarily and marked obsolete for legacy resolver/tooling consumers. Production live
behavior selection comes from the mapped modular/effective structure in
`AcceptedEmissionRuntimeAdapter`. The source-compatible live adapter constructor accepts the old
selector interface only to avoid breaking retained callers and ignores it; with no explicit mapping
registry that constructor fails closed before scheduling.

### `WeaponRuntimeFiringProfile`

Retained as the downstream compatibility DTO expected by `WeaponBehaviorContext` and built-in
behaviors. Its `CooldownTicks` field is now a projection of the accepted emission's
`TicksUntilNextEmission`; it is not an admission or cadence authority.

### `WeaponRuntimeProfile` (`Domain/Combat`)

The older profile containing cadence, burst, heat, charge, optional power-bank, recoil, behavior
module, serialization, and presentation fields remains separate from live firing.

The repository code-search index was unavailable during this connector-only audit. The following
direct runtime and test consumers were therefore individually verified through their retained files
and origin PRs rather than inferred from an empty search result:

| Reference | Classification | Reachable from canonical live firing? |
|---|---|---|
| `WeaponRuntimeProfile.cs` | transitional domain model/serialization | No |
| `WeaponRuntimeProfileValidator.cs` | transitional domain validation | No |
| `WeaponMountState.cs` | old independent mount-state prototype; `Initial(profile)` | No |
| `WeaponMountStepper.cs` | old heat/charge/cadence state-machine prototype | No |
| `WeaponPowerBankState.cs` | old independent power-bank factory from profile | No |
| `WeaponPowerBankPolicy.cs` | old power-bank policy over the profile-derived state | No |
| `IWeaponBehaviorModule.cs` / `WeaponBehaviorInput` | old behavior input stores the profile | No |
| `WeaponBehaviorPipeline.cs` | old module ordering reads profile module IDs | No |
| `WeaponFireExecutionPlan.cs` | old plan retains `WeaponBehaviorInput` transitively | No |
| `WeaponRuntimeProfileTests.cs` | EditMode tests | No |
| `WeaponMountStepperTests.cs` | EditMode tests | No |
| `WeaponPowerBankPolicyTests.cs` | EditMode tests | No |
| `WeaponBehaviorPipelineTests.cs` | EditMode tests | No |
| modular-weapon and old combat architecture documentation | migration documentation | No |

The old subsystem requires a separate focused retirement task because deletion also requires deciding
the fate of heat, charge, power-bank, recoil, profile serialization, behavior modules, execution
plans, and the old mount stepper. None of those mechanics are reconnected to
`WeaponFiringScheduler` by this cutover.

## Deleted and retained authorities

No legacy implementation files are deleted in this PR because the legacy core/resolver/selector still
have genuine non-production regression consumers. They are explicitly marked obsolete, disconnected
from live composition, and documented for later retirement.

The following transitional types remain intentionally:

- `WeaponRuntimeFiringProfile`: downstream behavior compatibility only;
- `WeaponExecutionResult` and `InventoryWeaponExecutionResult`: compatibility result surfaces;
- `IWeaponBehaviorSelector`: retained signature compatibility only, not used for live selection;
- `WeaponExecutionCore`, `WeaponCatalogRuntimeProfileResolver`, and
  `DefaultWeaponBehaviorSelector`: obsolete test/tooling authorities only.

## Authority invariants preserved

The cutover does not modify or weaken scheduler replay records, operation sequencing, global shot
sequencing, replay pruning, cumulative-history fingerprints, retention metadata, transition receipts,
or conservative replay expiry. `WeaponFiringReplayRecord` and
`WeaponFiringSessionState.WithTransition` remain the canonical bounded replay and pruning authority.

No parallel firing, replay, cooldown, random, projectile, guidance, impact, effect, damage, behavior
registry, or effect-batch authority was introduced. No Stage 1 route, scene/prefab edit, starter
blaster fallback, item-level combat scaling, heat, charge, ammo, magazine, or power-bank behavior was
introduced.
