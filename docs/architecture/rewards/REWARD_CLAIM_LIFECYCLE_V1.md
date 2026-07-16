# Reward Claim Lifecycle V1

Status: RAP-001 contract and implementation baseline
Owner: `ShooterMover.Application.Rewards.Application.RewardApplicationServiceV1`

## Authority ownership

RAP-001 is the only authority that owns durable truth between an immutable generated reward and its application to player value. It owns:

- the source-operation to commitment identity binding;
- the immutable generation fingerprint and canonical grant payloads;
- projection bookkeeping;
- the successful claim identity and claimant/destination binding;
- deterministic child transaction and operation identities;
- child-resolution facts, terminal application, cancellation, and replay;
- deterministic export/import of all coordinator facts.

RAP does **not** own balances or inventory. MON-001 remains the money authority, SCR-001 remains the scrap authority, and INV-001 remains the holdings authority. RAP composes them through `IRewardChildAuthorityV1` ports and the real adapters in `RewardApplicationAuthorityAdaptersV1.cs`. Presentation, scenes, pickups, shops, crafting and strongbox-opening surfaces never grant value directly.

## Lifecycle

```text
Generated ──project──> Projected
    │                     │
    ├────claim────────────┤
    │                     v
    └──────────────────> Claimed ──all children confirmed──> Applied
    │                     │
    └────cancel────────> Cancelled  └──retry unresolved children──┘

Applied and Cancelled are terminal.
```

The state is monotonic. A projection command may be recorded more than once under distinct projection identities while the state remains `Projected`; this supports presentation teardown and recreation without creating a reward. Exact duplicate commands return deterministic no-change results.

### Valid transitions

| Current | Command | Result |
|---|---|---|
| none | commit | `Generated` |
| Generated | project | `Projected` |
| Projected | project with a new projection identity | `Projected`, state unchanged |
| Generated or Projected | claim after complete preflight | `Claimed`, then `Applied` if every child confirms |
| Claimed | retry | stays `Claimed` or becomes `Applied` |
| Generated or Projected | cancel | `Cancelled` |

### Invalid transitions

- Projecting a Claimed, Applied or Cancelled commitment.
- Claiming a Cancelled commitment.
- Successfully claiming one commitment under a second claim identity.
- Cancelling a Claimed or Applied commitment.
- Retrying anything except the bound claim of a Claimed commitment.
- Moving from Applied or Cancelled to any earlier state.

RAP's V1 cancellation policy intentionally permits cancellation only before claim binding. A claim is a durable promise to finish or diagnose application, not a state that can be abandoned through cancellation.

## Commands and machine-readable results

The public command contracts are:

- `RewardCommitCommandV1`
- `RewardProjectCommandV1`
- `RewardClaimCommandV1`
- `RewardRetryClaimCommandV1`
- `RewardCancelCommandV1`

Normal domain control flow uses `RewardApplicationResultStatusV1`, including:

- `Generated`
- `Applied`
- `ExactDuplicateNoChange`
- `ConflictingDuplicate`
- `AlreadyAppliedNoChange`
- `Projected`
- `ClaimedPendingApplication`
- `Cancelled`
- `InvalidCommand`
- `UnknownCommitment`
- `InvalidStateTransition`
- `AuthorityMismatch`
- `ExpectedSequenceConflict`
- `InsufficientFunds`
- `CapacityRejected`
- `ChildAuthorityRejected`
- `SnapshotRejected`

Exception text is not used as ordinary domain control flow. An unexpected authority exception is converted to a deterministic pending-child diagnostic and never reported as success.

## Commitment contract

A commitment retains:

- the canonical `RewardOperationRequestV1`;
- the immutable `RewardResultV1`;
- the generator's canonical generation fingerprint;
- a one-to-one canonical payload for every generated grant;
- exact strongbox instance identities;
- exact immutable equipment instances;
- the source-operation and commitment bindings required for replay.

Payloads are sorted by grant StableId. Duplicate payload/grant identities, missing payloads, unsupported grant kinds, mismatched equipment definitions, duplicate unique instance identities, and non-canonical generation fingerprints are rejected at construction. The commitment fingerprint covers the complete operation, generated result, generation fingerprint and application payloads.

An explicit no-drop result is a valid commitment with no child grants. Claiming it produces an Applied terminal fact without mutating a child authority.

## Identity derivation

The following identities are preserved directly:

- source operation;
- commitment;
- claim;
- generated grant;
- claimant;
- destination authority;
- generation fingerprint.

Each child authority transaction and operation identity is derived from canonical input using SHA-256:

```text
transaction = raptx.<48 lowercase hex>
operation   = rapop.<48 lowercase hex>
```

The derivation inputs include commitment, claim, grant, unit ordinal and destination authority. These StableIds contain exactly one dot and are independent of clocks, random GUIDs, object names, hierarchy positions and process-local counters. A strongbox or equipment grant with quantity greater than one receives one permanent child transaction per retained unique instance.

## Duplicate and conflict semantics

- Repeating the exact commit/source callback returns the original commitment without sequence or value change.
- Reusing a source-operation or commitment identity with different canonical content is `ConflictingDuplicate`.
- Repeating the exact projection identity is a no-change replay.
- Reusing a projection identity with different content is a conflict.
- Repeating the bound claim command is a no-change replay. It does not secretly perform retry work.
- Reusing a claim identity with different content is a conflict.
- Retry is explicit through `RewardRetryClaimCommandV1`.
- Claiming an Applied commitment returns the existing terminal fact and never creates child work.
- Child authority exact duplicates are considered resolved only when the authority's original fact was Applied.

## Atomic application strategy

RAP builds the entire child plan before mutation. It then invokes batch preflight on money, scrap and holdings.

Preflight validates, as applicable:

- destination authority identities;
- supported grant kinds and currency identities;
- fixed expected sequences;
- existing transaction identity and payload compatibility;
- arithmetic overflow;
- balances/capacities;
- holdings stack type history;
- unique instance collisions;
- immutable equipment validation;
- every deterministic child command.

Only if every child is either accepted or already confirmed Applied does RAP bind the claim and begin child application. Therefore a predictable rejection cannot apply money first and discover a holdings-capacity failure later.

MON, SCR and INV do not expose a shared distributed transaction primitive. V1 therefore provides caller-observable atomicity with durable roll-forward recovery:

1. RAP reports `Applied` only after all children confirm their Applied fact.
2. If an authority call fails after successful preflight, RAP retains `Claimed` and reports `ClaimedPendingApplication`.
3. Successfully applied children retain their original permanent transaction identities.
4. Retry preflights the same plan, treats exact Applied child transactions as resolved, and calls only unresolved children.
5. No inverse/compensating currency or inventory transaction is invented.

A transient post-preflight interruption can leave underlying authorities ahead of the coordinator. That state is intentionally recoverable and is never presented as a completed reward until every child is confirmed.

## Restart semantics

Scene restart has no authority over RAP truth.

- Generated and Projected commitments remain claimable.
- Projection objects may disappear and be recreated under a new projection identity.
- Claimed commitments retain their exact child plan and may be retried.
- Applied commitments never apply again.
- Repeated source callbacks resolve through the retained source-operation binding.
- Cancelled commitments remain terminal.

No MonoBehaviour owns commitment state, and RAP performs no scene lookup or singleton discovery.

## Snapshot and import

`RewardApplicationSnapshotV1` contains:

- schema version;
- RAP authority StableId;
- monotonic RAP sequence;
- commitments in canonical commitment-ID order;
- complete commit, projection, claim, child-resolution and cancellation facts;
- commitment fingerprints;
- one canonical snapshot fingerprint.

Import validates the entire candidate graph before replacing current state. It rejects:

- null data;
- unsupported schema versions;
- authority mismatch;
- invalid or mismatched fingerprints;
- duplicate commitment/source/projection/claim/cancellation identities;
- state/record shape inconsistencies;
- child plans that cannot be deterministically rebuilt from the commitment and claim;
- an Applied commitment with a pending child;
- a Claimed commitment with no pending child.

Failure is non-mutating. Import restores coordinator truth only; MON, SCR and INV snapshots remain the responsibility of their respective persistence composition. A save-game layer must import those authority snapshots consistently with the RAP snapshot before gameplay resumes.

## Failure behavior

- Complete preflight rejection leaves money, scrap and holdings untouched.
- Post-preflight failure leaves the commitment Claimed and retryable.
- Child IDs are never replaced during retry.
- An exact duplicate of an originally rejected child operation remains rejected; RAP does not misreport it as Applied.
- Corrupt snapshots do not alter live coordinator state.
- RAP never swallows an exception and reports success.

## Future consumers

- **Pickup/presentation:** commit once, project any number of harmless representations, submit one claim, and render only RAP results.
- **Strongbox opening:** consume owned strongbox state through INV, generate a new immutable reward, then commit/claim through RAP.
- **Shop:** use the economy/holdings authorities for purchase cost and use RAP for any reward bundle delivered by a completed purchase contract.
- **Crafting/salvage:** create deterministic output reward commitments after the crafting authority has accepted its input transaction.
- **Augments/equipment:** pass generated immutable `EquipmentInstance` payloads into the commitment; RAP does not regenerate or mutate them.

These systems must not bypass RAP by calling money, scrap or holdings directly for reward delivery.

## Known limitations and deferred work

- Filesystem/cloud/save-slot persistence is explicitly deferred. RAP supplies deterministic snapshot contracts only.
- V1 does not implement a distributed lock across MON, SCR and INV. Concurrent external mutations can invalidate explicit expected sequences; the caller receives a machine-readable conflict and the commitment remains diagnosable.
- Rollback/compensation is not invented. Recovery is idempotent roll-forward.
- Presentation lifetime and pickup collision behavior are outside RAP.
- Reward generation, drop balance, shop pricing, crafting recipes and strongbox-opening policy remain separate authorities.
