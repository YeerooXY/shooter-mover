# DROP-PERSIST-PROOF-001 — Collected run rewards to permanent character state

## Implementation status

The implementation branch was launched from:

```text
777c3f93810d74184831af4753582b757fe12f69
```

`PICKUP-LIVE-001` / PR #279 was later merged into `main` at:

```text
bccf88e857b67c57b162ebb7b0e7152f36966f31
```

The branch was reconciled with that merge through a two-parent merge commit before
production integration continued. The implementation now includes the non-test runtime,
persistence, and Results route. Tests and Unity execution proof are intentionally deferred.

## Authority boundary

The shared `RunSessionAggregateV1` remains the authority for transient mission truth:

> this exact generated child was physically collected in this exact run lifecycle by
> this exact participant.

Permanent state remains owned by the existing selected-character authorities:

- `MoneyWalletService`;
- `ScrapWalletServiceV1`;
- `PlayerHoldingsService`;
- the character-scoped `RewardApplicationServiceV1`;
- `StrongboxOpeningServiceV1`;
- `CharacterCompositionCoordinatorV1` and the atomic account store.

The transfer code owns none of those states. Its only durable state is downstream
application history in the transfer-receipt save component.

Collision callbacks and pickup presentation never grant permanent rewards. Results owns
only an immutable projection and an exact retry command.

## Source of truth and eligibility

The only reward source is:

```text
RunSessionAggregateV1.ExportCollectedRunRewards()
```

The final-exit route freezes that journal and the terminal-drop generation context before
ending the run. Transfer planning is allowed only after `RunSessionAggregateV1.End(...)`
returns an accepted receipt whose mission result is `Completed`.

The plan validates:

- exact run ID;
- accepted lifecycle generation;
- accepted mission-result payload and fingerprint;
- selected character instance ID;
- expected character revision and fingerprint frozen into the run;
- every collected journal record belongs to that run and lifecycle.

A rejected or incomplete mission cannot create a transfer plan.

## Immutable batch

`CollectedRunRewardTransferBatchV1` contains:

- one stable transfer operation ID;
- exact run and accepted lifecycle identity;
- derived stable mission-result identity plus the accepted result fingerprint;
- selected character identity, expected revision, and expected fingerprint;
- every exact collected-reward record;
- a deterministic, order-independent fingerprint.

Each item preserves the journal's:

- generated reward child / concrete instance ID;
- source grant and DROP operation IDs;
- reward kind, content definition, and quantity;
- terminal and triggering event IDs;
- source entity, placement, lifecycle, definition, and participant attribution;
- generated batch and reward fingerprints;
- room and physical spawn facts;
- collector identity, collection operation, order, tick, and record fingerprint.

Input enumeration order does not affect the batch fingerprint. Collection order remains
part of each exact record.

## Exact application plan

`CollectedRunRewardApplicationPlanV1` pairs the journal batch with:

- the exact RAP commit command;
- the exact RAP claim command;
- concrete application payload fingerprints;
- unopened strongbox contexts;
- one deterministic plan fingerprint.

The plan fingerprint is recorded in the durable receipt's authority-fingerprint map. An
operation that reappears with the same journal batch but a different hidden equipment or
strongbox payload is therefore a conflict, not an exact replay.

### Money and scrap

Money and scrap quantities come directly from the collected journal. They are not
recomputed from reward profiles, kill counts, visible cards, or mutable progression.

### Equipment

Equipment is materialized during the same DROP/GEN execution that created the generated
child identity. `RetainingTerminalDropRewardGenerationExecutor` delegates to the existing
reward generator, then creates and retains the exact concrete `EquipmentInstance` under:

- the exact generated child ID;
- the referenced equipment definition;
- the frozen progression context;
- the frozen root seed and algorithm version;
- a deterministic equipment-generation operation and seed.

Results never regenerates equipment. The transfer plan can only resolve the retained
instance and verifies its instance ID and definition ID before RAP receives it. Level,
quality, augments, and the complete immutable equipment payload are therefore transferred
as one exact instance. Missing or conflicting retained payloads fail closed.

The permanent holdings record uses the generated child as the concrete grant/instance
identity and the transfer operation as the downstream application source. The complete
original DROP and collection provenance remains frozen in the transfer batch fingerprint
and receipt proof rather than being duplicated into the narrower holdings provenance
model.

### Strongboxes

The generated child ID becomes the exact unopened strongbox instance ID. The transfer plan
creates a deterministic `StrongboxInstanceContextV1` from the frozen generation context,
exact generated reward fingerprint, original DROP operation, collection operation, tier,
and current tier-definition fingerprint.

RAP adds the exact box identity to holdings. The existing BOX authority then registers the
matching unopened context. Transfer never opens the box, consumes it, or invokes BOX reward
generation.

## Shared RAP ownership

Strongbox opening and collected-run transfer use the same character-scoped
`RewardApplicationServiceV1`. The existing BOX composition binds that RAP instance into a
reference-only production registry; no second reward-application history is introduced.

The registry also exposes the character-scoped transfer-receipt authority through the
normal save-adapter composition.

## Complete preflight

Before the live graph changes, the transfer performs:

1. durable operation-receipt lookup;
2. application-plan fingerprint comparison;
3. cross-operation and partial reward-overlap checks;
4. character ID, revision, and fingerprint checks;
5. strongbox-context collision checks;
6. a full RAP dry run against cloned money, scrap, holdings, and RAP snapshots;
7. exact RAP commit and claim preflight on that discarded clone.

Only after the whole dry run succeeds are live compensation snapshots captured.

The live RAP commit and claim apply the complete batch on the first canonical transfer
child. Later child calls are exact in-transaction replays; the coordinator still records
every exact journal child ID in the receipt. This preserves one all-or-nothing RAP
commitment while retaining the coordinator's per-record audit ordering.

## Compensation boundary

The compensation snapshot contains:

- money;
- scrap;
- holdings;
- RAP commitments, claims, and child history;
- BOX contexts/opening state;
- transfer receipts.

A child rejection, receipt rejection, save rejection, read-back mismatch, or exception
restores all of those authorities. Confirmed restoration returns a retryable rejection.
Incomplete restoration returns `FatalCompensationFailure` with both the original and
restoration diagnostics and cannot be retried automatically.

## Durable save and replay

The optional character component is:

```text
save-component.collected-run-reward-transfer-receipts
collected-run-reward-transfer-receipts-explicit-v1
```

It uses an explicit deterministic codec and stores no reflection or CLR type names. It
indexes:

- transfer operation IDs;
- batch, run, lifecycle, mission-result, and character identity;
- every permanently transferred reward child ID;
- resulting permanent-authority fingerprints;
- the application-plan fingerprint;
- receipt fingerprints and receipt-authority revision.

The component is part of the ordinary selected-character save-adapter list and is restored
with the rest of the character graph. Old characters may omit it and begin with an empty
receipt authority.

`ProductionCollectedRunRewardTransferPersistenceAdapter` calls the existing
`CharacterCompositionCoordinatorV1.PersistActive(...)`. Success requires:

- the atomic account store to accept the complete exported character;
- the active graph to be marked with the persisted character revision/fingerprint;
- the exact receipt still to be present;
- the persisted character to contain the receipt component.

No transfer-only account file or second save protocol exists.

## Replay outcomes

| Durable state | Outcome |
| --- | --- |
| No receipt and all preflight succeeds | Apply, persist, verify |
| Matching operation, batch, and plan | `ExactReplay`, no child mutation |
| Same operation with different batch | Conflicting duplicate |
| Same operation/batch with different concrete plan | Conflicting duplicate |
| Any child ID belongs to another receipt | Partial-overlap rejection |
| Mutation/save failure with confirmed restoration | Retryable rejection |
| Compensation cannot be confirmed | Fatal compensation failure |

The durable receipt, not a Results-local flag, is the permanent exactly-once source of
truth.

## Results route

The Stage 1 production composition replaces only its own final-exit subscription. It does
not modify `Stage1VisibleSliceController.cs`.

After accepted run completion and transfer execution, Results receives
`CollectedRunRewardTransferResultsProjectionV1`, containing:

- transfer status;
- operation and batch identity;
- run, lifecycle, and character identity;
- applied reward child IDs;
- receipt and resulting-state fingerprints;
- account and character revisions/fingerprints;
- persistence status and diagnostics;
- whether an exact retry is permitted.

`ProductionCollectedRunRewardResultsOverlay` displays that projection. Its retry button
constructs `RetryCollectedRunRewardTransferCommandV1` with the exact operation and batch
fingerprint. The bridge reruns the already-frozen application plan and cannot accept a
replacement payload, reroll, or edited reward list.

## Production sequence

```mermaid
sequenceDiagram
    participant DROP as DROP / GEN
    participant Pickup as Physical pickup authority
    participant Run as Shared RunSessionAggregateV1
    participant Transfer as Transfer service
    participant RAP as Existing character RAP
    participant BOX as Existing BOX authority
    participant Save as Existing character/account save
    participant Results as Results projection

    DROP->>DROP: retain exact equipment payloads
    DROP->>Pickup: admit exact generated children
    Pickup->>Run: append accepted collection records
    Run->>Run: accept Completed End command
    Run-->>Transfer: accepted result + exact journal
    Transfer->>RAP: dry-run full commit and claim on clones
    Transfer->>Transfer: capture full compensation
    Transfer->>RAP: commit and claim exact batch
    Transfer->>BOX: register exact unopened contexts
    Transfer->>Transfer: record durable receipt
    Transfer->>Save: persist active character atomically
    Save-->>Transfer: revision/fingerprint + receipt read-back
    Transfer-->>Results: immutable transfer projection
```

## Changed production boundaries

The implementation touches:

```text
Assets/ShooterMover/Runtime/Application/Rewards/CollectedRunTransfers/**
Assets/ShooterMover/Runtime/Application/Persistence/Components/KnownSaveComponentVersionGuardV1.cs
Assets/ShooterMover/Runtime/Application/Flow/Production/ProductionCharacterAuthorityAdaptersV1.cs
Assets/ShooterMover/Runtime/Application/Flow/Production/ProductionCharacterStrongboxCompositionV1.cs
Assets/ShooterMover/Runtime/TerminalDropBinding/TerminalDropBindingCompositionV1.cs
Assets/ShooterMover/Production/Stage1/Stage1RunPickupBootstrap2D.cs
Assets/ShooterMover/Production/Stage1/Stage1PlayableLoopCompositionV1.CollectedRunTransfer.cs
Assets/ShooterMover/Production/Stage1/RetainedTerminalDropEquipmentPayloads.cs
Assets/ShooterMover/UI/ProductionFlow/ProductionCollectedRunRewardResultsOverlay.cs
docs/architecture/rewards/DROP_PERSIST_PROOF_V1.md
```

It does not add a wallet, scrap, holdings, inventory, strongbox, mission-result, Run
Session, RAP, or account-save authority. It does not modify
`Stage1VisibleSliceController.cs`.

## Verification status

This implementation iteration intentionally does not add tests and does not run tests.
Unity compilation, EditMode/PlayMode XML, crash-interruption proof, and manual route proof
are not claimed here and remain the next verification phase.
