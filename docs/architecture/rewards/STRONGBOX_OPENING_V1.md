# Strongbox Opening V1

Status: BOX-001 runtime baseline

## Authority boundaries

`StrongboxOpeningServiceV1` owns only strongbox registration context, opening identity, the frozen generated outcome, retry stage, terminal opening fact, and its deterministic snapshot. It does not generate rewards itself and it does not own balances or player inventory.

- GEN-001 is invoked exactly once for a newly accepted opening identity.
- RAP-001 owns the immutable reward commitment, claim, child transactions, and roll-forward application.
- SCR-001 owns scrap balance truth.
- INV-001 owns the strongbox instance, generated equipment, generated strongboxes, and stackable holdings.

No scene, prefab, pickup, UI, or Unity object owns opening truth.

## Data-driven definitions

A `StrongboxDefinitionV1` is identified by a stable tier ID and contains:

- display order;
- generation/source-tier bias;
- quality and exceptional-roll bias;
- minimum and maximum generated grant count;
- a strictly positive mandatory-scrap quantity range and scrap currency identity;
- a compatible generation-policy reference;
- a base `RewardProfileV1` consumed by GEN-001;
- stable scaling-input identities for tier and exceptional bias.

Definitions are collected by `StrongboxDefinitionCatalogV1`. Catalog order is canonical (`displayOrder`, then tier stable ID), duplicate tier identities are rejected, and the complete ordered catalog has a stable SHA-256 fingerprint. There is no tier enum and no runtime switch over a fixed tier count. The final production catalog and balance values remain deferred.

## Owned instance context

Every registered `StrongboxInstanceContextV1` retains immutable opening inputs:

- strongbox instance ID;
- tier/definition ID;
- committed root seed;
- generator algorithm version;
- complete progression context;
- source context identity;
- collection/creation provenance identity;
- optional definition/content fingerprint captured when the box was created.

Re-registering the exact context is a no-change replay. Reusing an instance identity with different context is a conflict.

## Mandatory scrap and generation

Mandatory scrap is not awarded as an out-of-band wallet mutation. BOX-001 derives a stable mandatory-scrap grant ID and appends the positive scrap policy as a guaranteed entry to a derived effective reward profile. The service then invokes the shared GEN-001 reward generator once.

Consequences:

1. Scrap is included in the immutable `RewardResultV1`.
2. Scrap appears in both the contract reward trace and the complete GEN trace.
3. The same seed and context reproduce the same scrap quantity and other grants.
4. RAP applies scrap through the real SCR-001 adapter together with all other rewards.
5. A generated result without positive scrap is rejected before commitment.

Tier and exceptional values are passed through explicit `RewardGenerationScalingValueV1` inputs. BOX-001 does not sample, select, or duplicate GEN algorithms.

## Deterministic identities

For a new opening, BOX-001 derives stable identities from canonical inputs with SHA-256:

- opening/source operation: `boxop.<48 hex>`;
- reward commitment: `boxcommit.<48 hex>`;
- effective profile: `boxprofile.<48 hex>`;
- mandatory scrap grant: `boxscrap.<48 hex>`;
- opening transaction: `boxtx.<48 hex>`;
- claim: `boxclaim.<48 hex>`;
- strongbox consumption transaction and operation: `boxconsume.<48 hex>` and `boxconsumeop.<48 hex>`;
- generated child strongbox/equipment identities through the configured payload resolver.

No identity depends on time, random GUIDs, process counters, scene hierarchy, or object names.

## Opening state machine

```text
new
  -> Prepared (GEN result, traces, payloads, commit, claim and consume command frozen)
  -> RewardCommitted
  -> RewardClaimedPending  --retry same RAP claim/children--+
  -> RewardApplied                                      |
  -> Opened (INV strongbox removal confirmed) <---------+
```

Generator and payload failures retain a deterministic rejection record and leave the box owned. Predictable RAP preflight rejection leaves the frozen opening at `RewardCommitted`; an exact retry submits the same claim command. A post-preflight interruption leaves RAP in `Claimed`, and BOX calls `Retry` with the same commitment and claim identities.

After RAP reports `Applied`, BOX submits one deterministic INV `RemoveStrongbox` command. Consumption is deliberately after reward application because removing first would violate the requirement that pre-application failure leaves the box owned. If interruption occurs between RAP application and INV removal, replay does not regenerate or reapply rewards; it retries only the exact removal command. The opening is reported `Opened` only after INV confirms the original removal as Applied.

This is monotonic idempotent roll-forward, not compensation.

## Duplicate semantics

- A repeated opening identity with the exact command fingerprint resumes pending work or, after completion, returns `ExactDuplicateNoChange` with the original terminal `Opened` fact and frozen result/trace.
- Reusing an opening identity with different canonical command content returns `ConflictingDuplicate`.
- A duplicate terminal opening never calls GEN, RAP, SCR, or INV again.
- A retry never substitutes a new seed, generated reward, commitment, claim, child transaction, or consumption identity.

## Reward payloads and equipment

`DeterministicStrongboxGrantPayloadResolverV1` maps money, scrap, stackable holdings, and generated strongbox grants into RAP payloads. Equipment references require an `IStrongboxEquipmentPayloadResolverV1`. That resolver must supply immutable `EquipmentInstance` values generated from the compatible shared generation policy; RAP and INV validate and retain them. BOX-001 never invents equipment stats or mutates generated equipment.

## Snapshot and replay

`StrongboxOpeningSnapshotV1` retains:

- schema version and catalog fingerprint;
- monotonic count of terminal opened boxes;
- every immutable registered instance context;
- every opening command and stage;
- the exact generated result, contract trace, complete GEN trace, and generation fingerprint;
- RAP commit and claim commands;
- INV consumption command;
- original terminal opening fact;
- a canonical snapshot fingerprint.

Import validates schema, catalog identity, snapshot fingerprint, duplicate identities, context references, terminal record shape, and terminal-count consistency before replacing live BOX state.

RAP, SCR, MON, and INV snapshots remain owned by their authorities. A save-game composition must restore those authorities consistently before importing/resuming BOX state. If RAP is already `Claimed` or `Applied` while a restored BOX record is one stage behind, BOX inspects RAP truth and rolls forward without regenerating.

## Failure behavior

- Unknown instance: rejected without mutation.
- Registered but not owned instance: rejected without mutation.
- Unknown tier or captured content-fingerprint mismatch: rejected without mutation.
- Invalid zero-scrap policy: rejected at definition construction.
- GEN exception/failure, invalid reward count, or missing mandatory scrap: rejected before RAP and INV mutation.
- RAP preflight rejection: box remains owned and the frozen opening is retryable.
- RAP post-preflight interruption: same child plan is retried.
- INV consumption interruption: same removal command is retried; rewards are not reapplied.
- Snapshot validation failure: live BOX state is unchanged.
