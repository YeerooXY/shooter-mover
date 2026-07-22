# DROP-PERSIST-PROOF-001 — Collected run rewards to permanent character state

## Status

The implementation is integrated with merged `PICKUP-LIVE-001` / PR #279 and now includes:

- the exact collected-reward journal adapter;
- pre-End exact payload freezing;
- durable prepared-transfer custody;
- a durable acceptance boundary for Run End;
- one honest atomic RAP/BOX batch operation;
- exact receipt persistence and replay;
- restart recovery from the normal character save graph;
- an immutable Results projection and custody-addressed retry command.

Tests and Unity execution proof are intentionally not part of this iteration.

## Ownership

The shared `RunSessionAggregateV1` owns transient mission truth:

> this exact generated child was physically collected in this exact run lifecycle by this exact participant.

Permanent state remains owned by the existing selected-character authorities:

- `MoneyWalletService`;
- `ScrapWalletServiceV1`;
- `PlayerHoldingsService`;
- the character-scoped `RewardApplicationServiceV1`;
- `StrongboxOpeningServiceV1`;
- `CharacterCompositionCoordinatorV1` and the existing atomic account store.

The transfer layer adds no replacement wallet, inventory, BOX, RAP, character, mission-result, Run Session, or account authority.

Its durable state is proof and crash-recovery custody only:

- prepared-transfer custody;
- completed-transfer receipts.

## Source and eligibility

The only reward source is:

```text
RunSessionAggregateV1.ExportCollectedRunRewards()
```

Collision callbacks and UI never grant permanent state. They cannot prove accepted completion, exact lifecycle eligibility, selected-character consistency, complete-batch admissibility, or successful durable persistence.

Every journal row preserves:

- generated child / concrete reward instance ID;
- source grant and DROP operation IDs;
- reward kind, content definition, and quantity;
- terminal and triggering event IDs;
- run and source lifecycle facts;
- source entity, placement, definition, and participant attribution;
- generated batch and child fingerprints;
- room and world-spawn facts;
- collector identity;
- collection operation, order, authoritative tick, and record fingerprint.

## Exact payload freezing before End

Before completion can be accepted, the final-exit route freezes all transfer material:

- exact journal rows;
- exact concrete `EquipmentInstance` payloads, including level, quality, and augments;
- exact unopened `StrongboxInstanceContextV1` records;
- frozen generation seed, algorithm, progression context, and event/modifier fingerprint;
- selected-character identity, revision, and fingerprint;
- current money, scrap, holdings, RAP, BOX, and receipt fingerprints;
- current money, scrap, and holdings sequences;
- the exact intended End command identity and fingerprint.

Equipment is materialized during the same DROP/GEN execution that created its generated child identity. Results never regenerates it.

Strongbox custody contains the exact instance ID, tier, deterministic seed, generation context, original DROP operation, exact collection operation, and tier-definition fingerprint. Transfer never opens or rerolls the box.

A missing or conflicting equipment payload, BOX context, journal record, character expectation, or authority state rejects **before accepted End**.

## Durable custody states

The optional character save component is:

```text
save-component.collected-run-reward-prepared-transfers
collected-run-reward-prepared-transfers-explicit-v1
```

It has three monotonic states:

| State | Meaning | May grant permanent rewards? |
| --- | --- | --- |
| `AwaitingAcceptedEnd` | Exact journal and payload custody exists, but completion is not yet accepted | No |
| `Prepared` | The exact accepted mission result, transfer batch, and application plan are durably bound | Yes |
| `Persisted` | The exact durable transfer receipt is bound to the prepared record | Already completed |

State transitions are content-verified:

```text
AwaitingAcceptedEnd.AcceptEnd(...) == incoming Prepared record
Prepared.MarkPersisted(receipt fingerprint) == incoming Persisted record
```

A higher enum value cannot replace custody with different content.

The codec is explicit deterministic binary encoded as Base64. It stores no CLR type names and uses no reflection or canonical-text reparsing.

## Durable Run End acceptance

`RunSessionAggregateV1.EndWithDurableAcceptance(...)` separates producing an immutable mission result from accepting the run as ended.

The sequence is:

1. validate the exact End command;
2. obtain the immutable mission-result authority result;
3. construct the candidate Run Session end receipt;
4. build the exact transfer batch and atomic application plan;
5. persist and verify the `Prepared` custody record;
6. only then mark the Run Session `Ended` and record End replay.

If the durable callback rejects, the aggregate remains active and the final-exit route is re-armed. Retrying the same command obtains the mission-result authority's exact replay and retries the same durable acceptance.

Therefore there is no accepted-End window in which the exact plan exists only in process memory.

## Atomic application model

The production model is deliberately whole-batch:

```text
PreflightAtomicBatch(plan)
CaptureCompensation()
ApplyAtomicBatch(plan)
RecordReceipt()
PersistAppliedAndVerify()
```

It does **not** create one apparent child call per reward and then execute the entire RAP commitment on the first child.

`ApplyAtomicBatch(plan)` performs exactly one logical permanent application:

- one RAP commit for the complete generated reward set;
- one RAP claim for the selected character;
- registration of every exact unopened BOX context;
- one result containing all applied reward child IDs for auditing.

The reward IDs remain first-class receipt facts, but they are not misrepresented as separate permanent transactions.

## Complete preflight

Before the live graph changes, the atomic authority verifies:

1. no matching operation conflict or cross-receipt reward overlap;
2. the selected character and custody state;
3. every frozen authority fingerprint;
4. frozen money, scrap, and holdings sequences;
5. every exact BOX context and definition fingerprint;
6. a full RAP commit and claim on cloned money, scrap, holdings, and RAP snapshots.

Only after this whole-plan dry run succeeds is compensation captured.

## Compensation

The live compensation snapshot contains:

- money;
- scrap;
- holdings;
- RAP commitments, claims, and child history;
- BOX contexts and opening state;
- transfer receipts;
- prepared-transfer custody.

A mutation, receipt, or safely rejected save failure restores all of those snapshots. Confirmed restoration produces a retryable rejection. Failed restoration produces `FatalCompensationFailure`.

Compensation is used only when the durable account file is confirmed not to have accepted the final grant state.

## Atomic persistence verification

The existing atomic account store already follows this protocol:

```text
write temporary candidate
→ decode temporary candidate
→ validate temporary candidate
→ replace active file
→ read and validate active file
```

`CollectedRunRewardPersistenceExpectationV1` installs exact selected-character component fingerprint expectations into `KnownSaveComponentVersionGuardV1` for the duration of `PersistActive(...)`.

This makes prepared-custody and final receipt verification part of:

- temporary candidate validation **before replacement**;
- active-file read-back validation before save success is returned.

The final save expects both exact components:

```text
save-component.collected-run-reward-prepared-transfers
save-component.collected-run-reward-transfer-receipts
```

Verification is no longer performed only after `PersistActive(...)` has already committed and marked the runtime character.

## Durable uncertainty rule

Some failures prove the active file was not replaced, such as temporary-candidate validation rejection. Those failures may restore live authority snapshots and permit exact retry.

Failures that can occur after replacement are different:

- active read-back failure;
- account save I/O failure whose replacement stage is unknown;
- exception during the final save call;
- exact active component mismatch after reported success.

These return:

```text
DurableStateUncertain
→ FatalCompensationFailure
→ exact retry disabled
→ live compensation intentionally not attempted
```

Rolling live state back in this condition would risk creating a disk-with-rewards / memory-without-rewards split. The implementation therefore fails closed instead of manufacturing a retryable Schrödinger reward.

## Receipt and exactly-once replay

The completed-transfer component is:

```text
save-component.collected-run-reward-transfer-receipts
collected-run-reward-transfer-receipts-explicit-v1
```

Each receipt records:

- transfer operation and batch fingerprint;
- run and lifecycle identity;
- accepted mission-result identity and fingerprint;
- selected character;
- every applied reward child ID;
- resulting permanent reward-authority fingerprints;
- exact atomic application-plan fingerprint;
- receipt fingerprint.

Replay rules:

| Durable state | Outcome |
| --- | --- |
| Matching operation, batch, and plan | `ExactReplay`, no mutation |
| Same operation with different batch or plan | conflicting duplicate |
| Any reward ID belongs to another receipt | partial/cross-operation overlap rejection |
| Prepared custody exists without receipt | rebuild and retry exact atomic plan |
| Matching Persisted custody and receipt | completed |

## Restart recovery

Results does not retain an execution delegate, equipment object graph, or authoritative batch in static memory.

The retry command addresses:

- custody ID;
- transfer operation ID;
- batch fingerprint;
- atomic plan fingerprint.

After restart, the normal character restore path reconstructs:

- prepared custody;
- exact equipment payloads;
- exact BOX contexts;
- transfer receipts;
- RAP and permanent authorities.

`ProductionCollectedRunRewardRecoveryV2` scans only `Prepared` records. It never grants from `AwaitingAcceptedEnd`, because that state does not prove accepted completion.

For a `Prepared` record it rebuilds the exact RAP commands and payloads from durable custody, verifies the plan fingerprint, and executes the same atomic coordinator. The durable receipt—not the Results screen or an in-memory retry flag—remains the exactly-once source of truth.

## Results guarantee

Before accepted End, any rejection re-arms the final-exit callback.

After accepted End, every outcome queues Results:

- applied;
- exact replay;
- retryable rejection;
- conflicting duplicate;
- preparation inconsistency;
- fatal durable-state uncertainty.

The Stage 1 composition retains the immutable mission result and summary until the production flow accepts the Results transition. An accepted completed run cannot disappear into a diagnostic-only return path.

`ProductionCollectedRunRewardResultsOverlay` displays:

- custody, operation, batch, and plan identity;
- transfer and persistence status;
- applied reward IDs;
- receipt fingerprint;
- account and character revisions/fingerprints;
- diagnostics and compensation diagnostics;
- whether an exact retry is permitted.

The UI issues only the typed custody-addressed retry command. It cannot mutate permanent authorities.

## Production sequence

```mermaid
sequenceDiagram
    participant DROP as DROP / GEN
    participant Pickup as Pickup authority
    participant Run as RunSessionAggregateV1
    participant Custody as Prepared custody component
    participant RAP as Existing character RAP
    participant BOX as Existing BOX authority
    participant Receipt as Transfer receipt component
    participant Save as Existing atomic account store
    participant Results as Results projection

    DROP->>DROP: retain exact equipment payload
    DROP->>Pickup: admit generated children
    Pickup->>Run: append accepted collection records
    Run->>Custody: persist AwaitingAcceptedEnd custody
    Run->>Run: build candidate completed mission result
    Run->>Custody: persist exact Prepared batch and plan
    Custody-->>Run: temporary + active read-back verified
    Run->>Run: accept End
    Run->>RAP: dry-run whole plan on cloned authorities
    Run->>RAP: commit and claim atomic batch once
    Run->>BOX: register exact unopened contexts
    Run->>Receipt: record exact receipt
    Run->>Custody: mark matching record Persisted
    Run->>Save: persist rewards + custody + receipt
    Save-->>Run: exact component validation and read-back
    Run-->>Results: immutable transfer projection
```

## Changed boundaries

Production changes are confined to the existing application, production-adapter, persistence, Run Session, terminal-drop, and Results composition seams. `Stage1VisibleSliceController.cs` is not modified.

No tests are added or run in this implementation iteration. Unity compilation, focused test proof, crash-interruption execution proof, and manual route proof are not claimed.
