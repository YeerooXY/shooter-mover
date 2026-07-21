# CONDITION-LIVE-001 — accepted runtime facts to conditional effects

## Launch boundary

- Repository: `YeerooXY/shooter-mover`
- Exact launch `main` SHA: `aa6dd3ceb228588a7303e2cf01304c5404acc943`
- Branch: `agent/condition-live-001-kill-fact-effects`
- Dependencies verified merged: `STATUS-EFFECT-RUNTIME-001` / PR #262 and `ENEMY-FACTORY-001` / PR #264.

## Ownership

`ConditionRuntimeAuthorityV1` owns only transient, run-local delivery replay, accepted-fact audit, per-participant fact windows, condition activation, temporary status effects, and expiry projection.

It delegates mutable truth to the existing authorities:

- `FactWindowConditionAuthorityV1` owns observed windows and activation decisions;
- `StatusEffectAuthorityV1` owns temporary effects, stacking/refresh policy, expiry, and modifier projection;
- `RuntimeModifierSnapshotV1` remains the numerical modifier language;
- `EnemyDeathFactV1` remains the immutable enemy-terminal fact emitted by `ENEMY-FACTORY-001`.

The integration does not mutate or duplicate enemy health/death, player health, XP, drops, kill statistics, ranked skills, equipment, account state, or mission results.

## Accepted-fact boundary

`AcceptedGameplayFactDeliveryV1` is the narrow ingress contract. It carries the accepted source object together with attribution that the source authority intentionally does not own:

- delivery operation ID;
- run ID and run lifecycle generation;
- source actor and source actor lifecycle generation;
- attributed participant and permanent character identity;
- authoritative run tick.

`EnemyDeathConditionFactAdapterV1` validates the delivery against the immutable enemy death fact and projects one rich `ConditionObservedGameplayFactV1`. The projection preserves:

- exact death/source fact ID and triggering damage/contact fact ID;
- source fact type and projected observed fact type;
- run ID and run lifecycle generation;
- source actor, attributed killer participant, and source character;
- target enemy actor and target run participant;
- source and target actor lifecycle generations;
- authoritative tick.

The adapter then creates the existing compact `RuntimeObservedFactV1` used by `FactWindowConditionAuthorityV1`.

## Extension model

`AcceptedGameplayFactAdapterRegistryV1` registers adapters by source CLR fact type. The authority contains no fact-type switch. Adding a future accepted fact requires:

1. one immutable source fact;
2. one independent `IAcceptedGameplayFactAdapterV1` registration;
3. ordinary `FactWindowConditionDefinitionV1`, status-effect definition, and binding data;
4. focused tests.

Unknown source types fail closed with `condition-fact-type-unsupported`.

The objective-collection fixture in the focused suite proves a second unrelated fact type can use the same public APIs without changing the condition authority, status-effect authority, or enemy-kill fixture.

## Reference enemy-kill burst fixture

The focused fixture is ordinary shared data, not a dedicated controller:

| Field | Reference value | Configurable through |
|---|---:|---|
| observed fact | `gameplay.enemy-killed` | `observedFactTypeId` |
| required count | `3` | `requiredFactCount` |
| observation window | `10` ticks | `observationWindowTicks` |
| active duration | `8` ticks | `activeDurationTicks` |
| outgoing damage | `1.5x` | `outgoingDamageMultiplier` |
| condition ID | `condition.enemy-kill-burst` | `conditionDefinitionId` |
| status-effect ID | `status-effect.enemy-kill-burst` | `statusEffectDefinitionId` |
| repeat behavior | `Ignore` by default | existing status-effect stacking policy |

`FactWindowEffectFixtureV1` builds the existing fact-window definition, status-effect catalog entry, condition-to-effect binding, and `DerivedStatTargetIdsV1.OutgoingDamageMultiplier` (`combat.damage-multiplier`) contribution. No enemy type, skill type, or named spree branch exists in runtime code.

## Replay and conflict rules

- First valid delivery operation and source fact: applied once.
- Same delivery operation with identical facts: exact duplicate, no mutation.
- Same source fact through another delivery operation with identical facts: exact duplicate, no mutation.
- Reused delivery operation with changed facts: conflicting duplicate, no mutation.
- Reused source fact ID with changed immutable attribution: conflicting duplicate, no mutation.
- Participants and runs have independent authorities and ledgers.
- Unsupported facts and stale run/lifecycle facts reject before fact-window mutation.

## Run lifecycle

`IConditionRunClockV1` supplies the authoritative tick. `IConditionRunLifecycleV1` supplies the current run identity and lifecycle generation. `Reconstruct` requires an exact expected-current run and a next definition matching the lifecycle port.

Accepted reconstruction replaces every per-participant fact-window and status-effect authority and clears:

- observed-window state;
- temporary activation/effect state;
- accepted source-fact audit;
- delivery replay state;
- tick-advance replay state.

Permanent skill-allocation fingerprints, permanent character IDs, stable actor IDs, and authored runtime definitions are immutable inputs supplied again by composition; this authority never mutates them.

Old run IDs, old run lifecycle generations, and stale source actor lifecycle generations reject after reconstruction. Target enemy lifecycle generation is preserved from the already accepted `EnemyDeathFactV1`; freshness of that source fact remains enforced by the enemy authority before delivery.

## Expiry

Composition calls `Advance(operationId)` at an authoritative tick. The authority delegates to `StatusEffectAuthorityV1.Advance`; expired stacks disappear from its `RuntimeModifierSnapshotV1` projection at that tick. Exact advance replay returns the original immutable snapshot, while conflicting operation reuse fails closed.

## Expected RUN-SESSION-001 port

The future run aggregate only needs to provide:

- one `IConditionRunClockV1` implementation;
- one `IConditionRunLifecycleV1` implementation;
- current participant/character/lifecycle definitions at construction and reconstruction;
- accepted immutable gameplay facts to the registered ingress boundary;
- deterministic calls to `Advance` as the run tick moves.

No dependency on an unmerged run-session implementation is present.

## Explicit exclusions

No `KillingSpreeController`, enemy-type switch, polling kill counter, duplicate modifier/status authority, production run-session composition edit, or `Stage1VisibleSliceController.cs` edit is introduced.

## Representative deterministic fingerprints

The focused suite locks these SHA-256 values for the reference `3 kills / 10 ticks / 8 ticks / 1.5x / Ignore` fixture and first accepted enemy death:

- condition/effect runtime definition: `dbd51e08f25fcd54d271dff5567071d60d9007505b2ee7a90f88a055eda8f6e0`;
- enemy-death adapter registry: `bc7d35c8ed47a43969b83b3ddbf22c4251c99348d38678b1876651f5fe622330`;
- first rich observed enemy-killed fact: `6756f952c38dc7b97b37d3b2a9d5a0d536bad876e9ad9621889da8932e3c9bf8`.

`DeterministicReplay_ProducesEquivalentSnapshotsAndFingerprints` additionally compares two independent authorities after the same accepted-fact sequence and proves exact replay leaves the immutable gameplay snapshot fingerprint unchanged.
