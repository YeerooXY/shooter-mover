# STATUS-EFFECT-RUNTIME-001 — Generic temporary effects

## Scope

This foundation adds one engine-neutral authority for run-local buffs, debuffs,
stacks, refreshes, replacement, ignore behavior, deterministic expiry, dispel,
checkpoint restore, and lifecycle reset.

It is additive. The merged numerical modifier and fact-window condition files are
not edited.

## Ownership

- `StatusEffectDefinitionV1` describes versioned content.
- `StatusEffectCatalogV1` validates and fingerprints definitions.
- `StatusEffectAuthorityV1` owns active stacks, expiry, lifecycle generation, and
  operation replay truth.
- `RuntimeModifierSnapshotV1` remains the only numerical modifier language.
- `FactWindowConditionAuthorityV1` remains the fact-window authority.
- `FactWindowStatusEffectBridgeV1` converts an accepted generic condition
  activation into an ordinary status-effect application command.
- Combat, UI, skills, equipment, and Unity collision callbacks do not mutate
  active status-effect state directly.

## Stacking policy

Stacking is shared by effect definition across all sources:

- `Add`: add one independently attributed stack until `maximumStacks`; further
  applications are accepted no-change and ignored until a stack expires or is
  dispelled.
- `Refresh`: one logical stack; a later source refreshes duration and becomes the
  active source while the stack identity remains stable.
- `Replace`: remove the current logical stack and install a new source-owned
  stack.
- `Ignore`: keep the first live stack and ignore later applications.

`Refresh`, `Replace`, and `Ignore` definitions must author exactly one maximum
stack. This makes multiple-source behavior explicit rather than dependent on
call order or hidden controller rules.

## Time and expiry

All time is an injected integer simulation tick carried by commands. Domain code
does not read wall-clock or Unity time.

Every accepted command requires a non-decreasing tick. Before a valid apply,
advance, or dispel is committed, stacks with
`expiresAtTickExclusive <= simulationTick` are removed deterministically.
Expired stacks disappear from both active snapshots and modifier projections.

## Modifier projection

Each live stack projects the definition's existing
`RuntimeModifierDefinitionV1` contributions into one immutable
`RuntimeModifierSnapshotV1`.

Projected source IDs include:

- effect definition ID;
- exact stack ID;
- exact application source ID;
- authored modifier-template source ID.

Flat and percentage contributions therefore sum per live stack, while
multiplicative contributions compound per live stack according to the merged
modifier evaluator. No second modifier language exists.

## Replay and conflict behavior

All command kinds share one operation-ID ledger:

- exact replay returns the original immutable result object without repeating
  mutation;
- reuse with different command facts returns `ConflictingDuplicate`;
- subject, lifecycle, stale-tick, or unknown-definition rejection is recorded and
  replays identically;
- a conflicting reuse never replaces the original replay record.

## Checkpoints

`StatusEffectAuthoritySnapshotV1` contains:

- current immutable state;
- catalog fingerprint;
- exact active stacks and source identities;
- current modifier projection;
- complete operation replay records and original results.

Restore fails closed when the catalog, definition fingerprint, stack policy,
maximum stack count, expiry state, modifier projection, subject, or replay
provenance is inconsistent.

This is a run checkpoint contract. Active effects are not permanent character
truth.

## Lifecycle restart

An accepted restart command must increment lifecycle generation exactly once.
It clears every active run-local effect and modifier projection while leaving
skills, equipment, account modifiers, and other permanent authorities untouched.
Old-generation commands reject.

## Killing-spree proof

A killing spree requires no `KillingSpreeController`:

1. `FactWindowConditionAuthorityV1` observes generic `fact.enemy-killed` facts.
2. Its data-defined condition emits `RuntimeConditionActivationFactV1`.
3. `FactWindowStatusEffectBridgeV1` resolves the condition ID through a binding.
4. The bridge creates an ordinary `ApplyStatusEffectCommandV1`.
5. `StatusEffectAuthorityV1` applies the data-defined damage modifier for its
   authored duration.

Another fact-window effect uses another binding and definition, not another
authority branch.

## Non-goals

- no damage-over-time execution or periodic damage scheduler;
- no Unity `MonoBehaviour`, HUD, VFX, networking, or persistence into permanent
  character saves;
- no skill-specific, enemy-specific, room-specific, or weapon-specific status
  controller;
- no rewrite of `RuntimeModifierFoundationV1.cs` or
  `FactWindowConditionAuthorityV1.cs`.

A future generic deterministic periodic-effect capability may consume active
status-effect snapshots and emit explicit replay-safe damage commands, but that
is intentionally outside version 1.
