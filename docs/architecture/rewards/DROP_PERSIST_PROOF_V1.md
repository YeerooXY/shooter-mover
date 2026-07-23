# DROP-PERSIST-PROOF-001 — Collected run rewards to permanent character state

## Status

The implementation is integrated with merged `PICKUP-LIVE-001` / PR #279 and includes:

- exact collected-reward journal transfer;
- exact equipment and unopened strongbox payload custody;
- durable `AwaitingAcceptedEnd -> Prepared -> Persisted` records;
- explicit persistence certainty;
- a typed durable Run End acceptance boundary;
- one honest whole-batch RAP/BOX operation;
- exact durable receipts and replay detection;
- bounded restart recovery with a persistent exact-retry surface;
- sticky terminal preparation failures with no automatic retry;
- flow-level fatal notices that are visible outside the Results scene;
- immutable Results projections for success, retryable rejection, conflict, terminal preparation failure, and durable uncertainty.

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

`RejectedBeforeReplacement` is produced only before `CharacterCompositionCoordinatorV1.PersistActive(...)` is invoked, such as an invalid context, invalid custody transition, or authority conflict.

Once `PersistActive(...)` has been invoked, the transfer layer never infers certainty from diagnostic text. A thrown callback, null result, rejected result, or exact component mismatch is classified as `DurableStateUncertain` unless the save protocol explicitly proves replacement did not occur.

There is no parsing of strings such as:

```text
active-readback-*
account-save-io-failure
character-save-store-threw
```

The generic transfer coordinator is conservative too: a persistence-port throw or null result becomes fatal durable uncertainty.

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

## Durable Run End classification

`RunSessionAggregateV1.EndWithDurableAcceptance(...)` has four typed outcomes:

```text
Accepted
RetryableBeforeDurability
TerminalPreparationFailure
DurableStateUncertain
```

The aggregate exposes the corresponding retained state:

```text
None
PendingExactRetry
TerminalPreparationFailure
DurableStateUncertain
```

### Before mission-result acceptance

Validation, journal freezing, payload resolution, or initial `AwaitingAcceptedEnd` setup can reject before the mission-result authority is invoked. Ordinary deterministic failures at this boundary may re-arm final exit because no terminal mission-result transaction exists.

Initial custody save uncertainty is different: replacement may already have occurred. Stage 1 freezes gameplay, publishes a non-retryable projection, and displays a flow-level fatal notice directly in Level 1. It does not depend on the Results overlay becoming visible.

### After mission-result acceptance

After the mission-result authority succeeds, the aggregate retains the exact immutable candidate:

- End command identity and fingerprint;
- mission-result payload;
- candidate Run Session receipt;
- frozen run-local state.

The candidate remains available through `PendingDurableEndCandidate` until durable acceptance succeeds. The aggregate also retains the typed state and diagnostic.

Only an explicitly proven `RetryableBeforeDurability` result may become `PendingExactRetry`. Retrying invokes the durable callback for the same retained candidate; it does not construct another mission result.

Deterministic failures become `TerminalPreparationFailure`, including:

- the accepted mission result no longer matching the frozen character;
- failure to construct the immutable transfer plan;
- invalid or conflicting Prepared custody transitions before save invocation;
- any other current production `RejectedBeforeReplacement` outcome.

A terminal preparation failure is sticky:

- gameplay remains frozen;
- the final-exit callback is not re-subscribed;
- automatic retry stops;
- ordinary End calls do not re-enter the mission-result or durability callback;
- the terminal candidate and diagnostic remain available;
- a non-retryable preparation projection is published;
- the accepted mission result is queued to Results;
- a flow-level notice remains visible until Results accepts the handoff.

A thrown, null, or uncertain durable callback becomes `DurableStateUncertain`. It is also sticky and non-retryable.

Only `Accepted` marks the Run Session `Ended` and records normal End replay.

## Retry policy

Retryability is a typed fact, not an inference from a generic safe rejection.

The current production persistence adapter does not claim a transient pre-replacement storage failure. Its pre-invocation rejections are deterministic context, transition, or authority failures, so Stage 1 maps them to `TerminalPreparationFailure`.

The contract retains `RetryableBeforeDurability` for a persistence implementation that can explicitly prove both:

1. active-file replacement did not occur;
2. the failure is transient and may heal without changing the immutable candidate.

Even typed transient retries are bounded to five automatic attempts with capped exponential backoff. Exhaustion converts the retained transaction into `TerminalPreparationFailure` and publishes a visible non-retryable result.

## Stage 1 terminal route

| Boundary | Outcome |
| --- | --- |
| Deterministic failure before mission-result creation | re-arm final exit |
| Initial custody save durability uncertain | freeze; flow-level fatal notice; no retry |
| Explicit transient failure after terminal candidate creation | freeze; retry same candidate, at most five attempts |
| Character/plan/Prepared deterministic conflict | freeze; terminal non-retryable projection; queue Results |
| Terminal durability uncertain | freeze; fatal non-retryable projection; queue Results when candidate exists |
| Durable acceptance succeeds | apply or replay exact transfer, then enter Results |

While any terminal transaction is unresolved, player movement, room progression, weapon execution, and effect emission are disabled.

## Flow-level terminal notice

`ProductionCollectedRunRewardTerminalNoticeV1` is attached to the persistent production flow object.

It renders independently of the active scene and contains:

- the exact custody ID;
- terminal or uncertainty diagnostic;
- explicit non-retryable guidance;
- confirmation that the retained terminal transaction remains available for diagnostics.

It owns no reward payload and exposes no retry button.

For terminal failures with an accepted mission result, the notice remains visible while the Results transition is pending and removes itself once Results is active. For initial pre-End uncertainty, where no accepted mission result exists to route, the notice remains visible in the current scene.

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

Compensation includes money, scrap, holdings, RAP history, BOX state, receipt history, and prepared custody.

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
| `Prepared` custody without receipt | rebuild same whole plan |
| Matching `Persisted` custody and receipt | complete |

## Restart recovery

`ProductionCollectedRunRewardRecoveryV2` scans only durable `Prepared` custody. `AwaitingAcceptedEnd` is never eligible for permanent grants.

For each recoverable custody record it rebuilds the exact plan, verifies its fingerprint, invokes the normal coordinator, and classifies success, recoverable failure, or fatal uncertainty.

Recoverable failures receive up to five automatic attempts with capped exponential backoff from one to thirty seconds. Transactions remain serialized to one permanent-state attempt per probe.

After retries are exhausted, a persistent flow-level recovery notice remains visible in Bootstrap, Hub, or Results with an exact custody retry action. Fatal or durable-uncertain outcomes stop automatic retries and do not expose a retry button.

Restart recovery is separate from the in-run terminal state machine: a deterministic terminal preparation failure never enters restart recovery because no valid durable `Prepared` plan exists.

## Results guarantee

After a terminal candidate exists, no path merely writes a diagnostic and resumes gameplay.

Every terminal outcome is represented by immutable state:

- applied;
- exact replay;
- explicitly retryable transient rejection;
- conflicting duplicate;
- terminal preparation failure;
- fatal durable uncertainty.

The Stage 1 composition retains the immutable mission result and summary until the production flow accepts the Results handoff.

The Results projection exposes custody, operation, batch and plan identity, transfer and persistence status, applied reward IDs, receipt and state fingerprints, revisions, diagnostics, and retry eligibility.

Terminal preparation and durability-uncertain projections set retry eligibility to false.

## Production sequence

```mermaid
sequenceDiagram
    participant Run as RunSessionAggregateV1
    participant Result as Mission result authority
    participant Custody as Prepared custody
    participant Notice as Flow-level notice
    participant RAP as Character RAP / BOX
    participant Save as Atomic account store
    participant Results as Results

    Run->>Custody: persist AwaitingAcceptedEnd
    alt initial save uncertain
        Custody-->>Run: DurableStateUncertain
        Run->>Run: freeze gameplay
        Run-->>Notice: visible fatal notice in current scene
    else initial custody verified
        Run->>Result: create immutable terminal result
        Result-->>Run: accepted terminal candidate
        Run->>Custody: construct and persist Prepared plan
        alt character or plan conflict
            Custody-->>Run: TerminalPreparationFailure
            Run->>Run: retain candidate; stop retries
            Run-->>Notice: non-retryable diagnostic
            Run-->>Results: queued immutable result
        else explicitly transient before durability
            Custody-->>Run: RetryableBeforeDurability
            Run->>Run: retry same candidate, bounded to five
        else durability uncertain
            Custody-->>Run: DurableStateUncertain
            Run->>Run: retain candidate; stop retries
            Run-->>Notice: fatal diagnostic
            Run-->>Results: queued immutable result
        else Prepared verified
            Custody-->>Run: Accepted
            Run->>Run: mark Ended
            Run->>RAP: preflight and apply whole batch
            Run->>Save: persist rewards + custody + receipt
            Save-->>Run: committed and verified
            Run-->>Results: immutable projection
        end
    end
```

## Verification boundary

No tests were added or run in this implementation iteration. Unity compilation, EditMode/PlayMode proof, fault-injection execution, crash-restart proof, and manual scene-route proof are not claimed.
