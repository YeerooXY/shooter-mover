# DROP-PERSIST-PROOF-001 — Collected run rewards to permanent character state

## Status

The implementation is integrated with merged `PICKUP-LIVE-001` / PR #279 and includes:

- exact collected-reward journal transfer;
- exact equipment and unopened strongbox payload custody;
- durable `AwaitingAcceptedEnd -> Prepared -> Persisted` records;
- a typed durable Run End acceptance boundary;
- one honest whole-batch RAP/BOX operation;
- exact durable receipts and replay detection;
- bounded restart recovery with a persistent exact-retry surface;
- immutable Results projections for success, retryable rejection, conflict, and fatal uncertainty.

Tests and Unity execution proof are intentionally deferred to the next iteration.

## Ownership

The shared `RunSessionAggregateV1` remains authoritative for transient mission truth:

> this exact generated child was physically collected in this exact run lifecycle by this exact participant.

Permanent state remains owned by the existing selected-character authorities:

- `MoneyWalletService`;
- `ScrapWalletServiceV1`;
- `PlayerHoldingsService`;
- the character-scoped `RewardApplicationServiceV1`;
- `StrongboxOpeningServiceV1`;
- `CharacterCompositionCoordinatorV1` and the existing atomic account store.

The transfer layer introduces no replacement wallet, inventory, RAP, BOX, mission-result, Run Session, character, or account-save authority.

Its durable state owns only:

- crash-recovery custody for an accepted transfer;
- completed-transfer receipts and overlap history.

## Source and eligibility

The only reward source is:

```text
RunSessionAggregateV1.ExportCollectedRunRewards()
```

Collision callbacks and UI cannot grant permanent rewards.

Every transferred journal record retains the exact:

- generated child / concrete reward instance ID;
- source grant and DROP operation IDs;
- reward kind, content definition, and quantity;
- terminal and triggering event IDs;
- run and source lifecycle facts;
- source entity, placement, definition, and participant attribution;
- generated batch and reward fingerprints;
- room and physical spawn facts;
- collector identity;
- collection operation, order, authoritative tick, and record fingerprint.

## Exact pre-End payload freezing

Before invoking the mission-result authority, the final-exit route freezes:

- the complete collected journal;
- exact concrete `EquipmentInstance` payloads, including level, quality, and augments;
- exact unopened `StrongboxInstanceContextV1` records;
- generation seed, algorithm, progression context, and event/modifier fingerprint;
- selected character ID, revision, and fingerprint;
- current money, scrap, holdings, RAP, BOX, and receipt fingerprints;
- current money, scrap, and holdings sequences;
- the intended End command identity and fingerprint.

Equipment is materialized during the same DROP/GEN execution that produced its generated child identity. Results never regenerates equipment.

Strongbox custody includes the exact instance ID, tier, deterministic seed, progression context, original DROP operation, exact collection operation, and tier-definition fingerprint. Transfer never opens or rerolls a box.

Missing or conflicting journal, equipment, BOX, character, or authority facts reject before the mission-result authority is invoked.

## Durable custody

The optional character component is:

```text
save-component.collected-run-reward-prepared-transfers
collected-run-reward-prepared-transfers-explicit-v1
```

Its states are:

| State | Meaning | Eligible for permanent application |
| --- | --- | --- |
| `AwaitingAcceptedEnd` | Exact journal and payload custody exists, but no accepted mission result is bound | No |
| `Prepared` | Exact accepted mission result, batch, and whole-plan fingerprint are durably bound | Yes |
| `Persisted` | Matching durable receipt is bound | Already complete |

State transitions are content-verified:

```text
existing.AwaitingAcceptedEnd.AcceptEnd(...) == incoming Prepared
existing.Prepared.MarkPersisted(receipt fingerprint) == incoming Persisted
```

A higher enum value cannot replace a custody record with different content.

The component uses an explicit deterministic binary/Base64 codec. It stores no reflection metadata or CLR type names.

## Explicit persistence certainty

`CollectedRunRewardTransferPersistenceStatusV1` distinguishes:

```text
RejectedBeforeReplacement
PreparedAndVerified / PersistedAndVerified / AlreadyPersisted
DurableStateUncertain
```

`RejectedBeforeReplacement` is produced only before `CharacterCompositionCoordinatorV1.PersistActive(...)` is invoked, such as invalid transfer context or an invalid in-memory custody transition.

Once `PersistActive(...)` has been invoked, the transfer layer never infers certainty from diagnostic text. A thrown callback, null result, rejected result, or exact component mismatch is classified as `DurableStateUncertain` unless the save protocol explicitly proves replacement did not occur.

There is no parsing of strings such as:

```text
active-readback-*
account-save-io-failure
character-save-store-threw
```

The generic transfer coordinator is conservative as well: a persistence-port throw or null result becomes fatal durable uncertainty, even if a future persistence adapter does not implement the production wrapper correctly.

## Atomic store verification

The existing atomic account store follows:

```text
write temporary candidate
-> decode temporary candidate
-> validate temporary candidate
-> replace active file
-> read and validate active file
```

`CollectedRunRewardPersistenceExpectationV1` installs exact selected-character component fingerprints into the normal account validator for the duration of `PersistActive(...)`.

Prepared custody and final receipt verification therefore occurs:

1. during temporary-candidate validation before replacement;
2. during active-file read-back before success is reported.

The final save expects exact fingerprints for both:

```text
save-component.collected-run-reward-prepared-transfers
save-component.collected-run-reward-transfer-receipts
```

## Durable uncertainty policy

A durable-uncertain result means the active file may already contain the candidate state.

The required outcome is:

```text
DurableStateUncertain
-> FatalCompensationFailure
-> no live compensation
-> no exact retry command
```

Rolling live authorities back in this state could create disk-with-rewards / memory-without-rewards divergence. The implementation therefore fails closed.

Only a typed `RejectedBeforeReplacement` outcome may enter the ordinary compensation-and-retry path.

## Durable Run End state machine

`RunSessionAggregateV1.EndWithDurableAcceptance(...)` has a typed acceptance result:

```text
Accepted
SafelyRejectedBeforeDurability
DurableStateUncertain
```

The aggregate also exposes:

```text
None
PendingExactRetry
DurableStateUncertain
```

### Before mission-result acceptance

Validation, journal freezing, payload resolution, or `AwaitingAcceptedEnd` setup can reject before the mission-result authority is invoked. These failures may re-arm the final-exit callback because no terminal mission-result transaction exists.

### After mission-result acceptance

After the mission-result authority succeeds, the aggregate retains the exact immutable candidate:

- End command identity and fingerprint;
- mission-result payload;
- candidate Run Session receipt;
- frozen run-local state.

A safely rejected `Prepared` save does **not** reopen ordinary gameplay and does not call the mission-result authority again. The exact candidate becomes `PendingExactRetry` and is retried with capped exponential backoff.

A thrown/null/uncertain durable callback changes the aggregate to `DurableStateUncertain`. Ordinary End retries are rejected, gameplay remains frozen, and a fatal non-retryable transfer projection is published.

Only `Accepted` marks the aggregate `Ended` and records normal End replay.

## Stage 1 terminal route

The final-exit route behaves as follows:

| Boundary | Outcome |
| --- | --- |
| Failure before terminal mission-result creation | re-arm final exit |
| Terminal candidate exists and durability is safely rejected | freeze gameplay and retry the same exact candidate |
| Terminal durability is uncertain | freeze gameplay, no ordinary retry, publish fatal recovery Results |
| Durable acceptance succeeds | apply or replay the exact transfer, then enter Results |

While an exact terminal retry is pending, player movement, room progression, weapon execution, and effect emission are disabled. The final-exit callback is not re-subscribed.

The retry delay is capped; it retries the same retained transaction rather than constructing a new mission result or transfer plan.

## Whole-batch permanent application

The production contract is:

```text
Preflight(plan)
CaptureCompensation()
ApplyAtomicBatch(plan)
RecordReceipt()
PersistAppliedAndVerify()
```

`ApplyAtomicBatch(plan)` performs:

- one RAP commit for the complete generated reward set;
- one RAP claim for the selected character;
- all exact unopened strongbox context registrations;
- one immutable result containing every applied reward ID for auditing.

Reward IDs remain first-class receipt facts. They are not represented as fake per-child permanent transactions.

## Complete preflight and compensation

Before live mutation, the transfer verifies:

1. no operation conflict or cross-receipt reward overlap;
2. selected-character and custody identity;
3. every frozen authority fingerprint;
4. frozen money, scrap, and holdings sequences;
5. every exact BOX context and definition fingerprint;
6. a full RAP commit and claim against cloned authority snapshots.

Compensation includes:

- money;
- scrap;
- holdings;
- RAP commitment, claim, and child history;
- BOX state;
- receipt history;
- prepared custody.

Compensation is allowed only before durable state becomes uncertain. Failed compensation is fatal.

## Durable receipt and replay

The receipt component is:

```text
save-component.collected-run-reward-transfer-receipts
collected-run-reward-transfer-receipts-explicit-v1
```

Each receipt records:

- transfer operation and batch fingerprint;
- run and lifecycle identity;
- accepted mission-result identity and fingerprint;
- selected character;
- every applied reward ID;
- resulting reward-authority fingerprints;
- whole-plan fingerprint;
- receipt fingerprint.

Replay rules:

| Durable evidence | Outcome |
| --- | --- |
| Matching operation, batch, and plan | `ExactReplay`, no mutation |
| Same operation with different batch or plan | conflicting duplicate |
| Reward ID belongs to another receipt | partial/cross-operation overlap rejection |
| `Prepared` custody without receipt | rebuild the same whole plan |
| Matching `Persisted` custody and receipt | complete |

## Restart recovery

`ProductionCollectedRunRewardRecoveryV2` scans only durable `Prepared` custody. `AwaitingAcceptedEnd` is never eligible for permanent grants.

For each recoverable custody record it:

1. rebuilds the exact plan from restored equipment and BOX payloads;
2. verifies the stored plan fingerprint;
3. invokes the normal atomic coordinator;
4. classifies success, retryable failure, or fatal uncertainty.

Recoverable failures receive up to five automatic attempts with capped exponential backoff from one to thirty seconds. Transactions remain serialized to one permanent-state attempt per probe.

After retries are exhausted, a persistent flow-level recovery notice remains visible in Bootstrap, Hub, or Results. It contains the exact custody identity and an explicit `RETRY EXACT RECOVERY NOW` action.

The manual action addresses durable custody and plan fingerprints; it owns no payload and cannot substitute a new reward list.

Fatal or durable-uncertain outcomes:

- stop automatic retries;
- render a persistent fatal notice;
- do not expose a retry button.

A recoverable failure therefore cannot disappear into a console warning or require another application restart.

## Results guarantee

After a terminal candidate exists, no path merely sets a diagnostic and resumes gameplay.

Every terminal outcome is represented by immutable state:

- applied;
- exact replay;
- retryable rejection;
- conflicting duplicate;
- preparation inconsistency;
- fatal durable uncertainty.

The Stage 1 composition retains the immutable mission result and summary until the production flow accepts the Results handoff.

The Results projection exposes:

- custody, operation, batch, and plan identity;
- transfer and persistence status;
- applied reward IDs;
- receipt and resulting-state fingerprints;
- account and character revisions/fingerprints;
- diagnostics and compensation diagnostics;
- exact retry eligibility.

A null transfer-service result after accepted End is itself projected as durable uncertainty with retry disabled.

## Production sequence

```mermaid
sequenceDiagram
    participant DROP as DROP / GEN
    participant Pickup as Pickup authority
    participant Run as RunSessionAggregateV1
    participant Custody as Prepared custody
    participant RAP as Character RAP
    participant BOX as BOX authority
    participant Receipt as Transfer receipt
    participant Save as Atomic account store
    participant Recovery as Recovery UI
    participant Results as Results

    DROP->>DROP: retain exact equipment payload
    DROP->>Pickup: admit generated children
    Pickup->>Run: append exact collection records
    Run->>Custody: persist AwaitingAcceptedEnd
    Run->>Run: obtain immutable terminal mission result
    Run->>Custody: build and persist exact Prepared plan
    alt Prepared verified
        Custody-->>Run: Accepted
        Run->>Run: mark Ended
        Run->>RAP: dry-run and apply whole batch
        Run->>BOX: register exact unopened contexts
        Run->>Receipt: record exact receipt
        Run->>Save: persist rewards + Persisted custody + receipt
        Save-->>Run: committed and verified
        Run-->>Results: immutable projection
    else Safely rejected before replacement
        Custody-->>Run: PendingExactRetry
        Run->>Run: retain exact candidate and freeze gameplay
        Run->>Custody: retry same terminal transaction
    else Durable state uncertain
        Custody-->>Run: DurableStateUncertain
        Run->>Run: freeze; disable ordinary retry
        Run-->>Results: fatal non-retryable projection
    end

    Recovery->>Custody: scan durable Prepared records
    Recovery->>RAP: bounded exact recovery attempts
    Recovery-->>Recovery: persistent manual action or fatal notice
```

## Verification boundary

No tests were added or run in this implementation iteration. Unity compilation, EditMode/PlayMode proof, fault-injection execution, crash-restart proof, and manual scene-route proof are not claimed.