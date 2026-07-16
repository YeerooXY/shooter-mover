# Strongbox Opening V1

Status: BOX-001 runtime baseline, corrected SAS4-style equipment-roll model

## Authority boundaries

`StrongboxOpeningServiceV1` owns strongbox registration context, opening identity, the frozen generated outcome, retry stage, terminal opening fact, and its deterministic snapshot. It does not own balances, equipment definitions, equipment validation, or player inventory.

- GEN-001 resolves the reward profile and performs each concrete equipment roll.
- RAP-001 owns the immutable reward commitment, claim, child transactions, and roll-forward application.
- SCR-001 owns scrap balance truth.
- INV-001 owns the strongbox instance, generated equipment, generated strongboxes, and stackable holdings.

No scene, prefab, pickup, UI, or Unity object owns opening truth.

## Data-driven definitions

A `StrongboxDefinitionV1` is identified by a stable tier ID and contains the reward-profile and transaction-facing data:

- display order;
- reward-profile scaling values;
- minimum and maximum generated grant count;
- a strictly positive mandatory-scrap quantity range and scrap currency identity;
- a compatible equipment-generation-policy reference;
- a base `RewardProfileV1` consumed by GEN-001.

Equipment generation for a tier is bound separately through `StrongboxEquipmentGenerationDefinitionV1`. The binding contains:

- the same strongbox tier ID;
- one `StrongboxPowerBudgetPolicyV1`;
- one accepted shared `EquipmentGenerationPolicyV1`;
- the accepted EQP equipment catalog.

`StrongboxEquipmentGenerationDefinitionCatalogV1` is enum-free, rejects duplicate tier identities, uses canonical tier ordering, and has a stable fingerprint. The final production tier count and balance values remain content decisions.

## Owned instance context

Every registered `StrongboxInstanceContextV1` retains immutable opening inputs:

- strongbox instance ID;
- tier/definition ID;
- committed root seed;
- generator algorithm version;
- complete progression context, including player level;
- source context identity;
- collection/creation provenance identity;
- optional definition/content fingerprint captured when the box was created.

Re-registering the exact context is a no-change replay. Reusing an instance identity with different context is a conflict.

## Mandatory scrap

Mandatory scrap is intentionally additional to the SAS4-style equipment result. It is not awarded as an out-of-band wallet mutation. BOX derives a stable mandatory-scrap grant ID and appends the positive scrap policy as a guaranteed entry to a derived effective reward profile before invoking GEN.

Consequences:

1. Scrap is included in the immutable `RewardResultV1`.
2. The same box seed and context reproduce the same scrap quantity.
3. RAP applies scrap through the real SCR adapter together with the other frozen rewards.
4. A generated result without positive scrap is rejected before commitment.

## SAS4-style equipment generation

Every equipment unit in an equipment reward grant is an independent equipment slot. Slots are sampled **with replacement**. Selecting a weapon definition does not remove that definition from later slots and owning a weapon does not remove it from the pool.

Therefore this is valid:

```text
slot 0 -> weapon.ak47 / equipment-instance.a1
slot 1 -> weapon.ak47 / equipment-instance.a2
```

The definition may repeat. The immutable equipment instance identity may not.

For each equipment slot, `StrongboxEquipmentGenerationResolverV1` performs the following order:

1. calculate the box-adjusted mean item level;
2. roll a target item level on a bounded bell-shaped distribution;
3. filter the complete GEN candidate set to equipment that supports that target level;
4. use GEN to select the equipment definition and quality;
5. compare that selected item's rolled level with the box mean;
6. roll the inverse augment-slot power budget, capped by the selected definition's capacity;
7. use GEN again with the selected definition, quality, target level, and slot count fixed;
8. validate and freeze one unique immutable equipment instance.

The first GEN pass is a deterministic selection pass with zero augments. The final GEN pass cannot change the selected definition or quality. It applies the compensatory slot count and performs the real augment selection and instance validation.

Each slot starts from the complete compatible candidate set. No deduplication is performed by equipment definition ID.

## Level roll distribution

For V1:

```text
meanItemLevel = max(1, playerLevel + tierLevelBonus)
minimumItemLevel = max(1, meanItemLevel - 12)
maximumItemLevel = meanItemLevel + 12
```

`StrongboxPowerBudgetPolicyV1` uses a deterministic, fixed-point bell-curve approximation built from twelve centered uniform samples. The authored item-level standard deviation controls concentration around the mean, and the result is always clamped to the V1 `+/- 12` range.

The roll uses the named RNG substream `strongbox-rng.item-level-v1` and the equipment-slot ordinal. Adding or changing the scrap roll does not shift equipment-level results.

For GEN eligibility and weighting, BOX derives an immutable effective progression context whose character level is the box-adjusted mean. Region, difficulty, and progression tags remain unchanged. This means a tier bonus can admit appropriately stronger equipment without mutating the player's real progression state.

## Inverse augment power budget

After GEN has selected the item:

```text
differenceFromMean = selectedItemLevel - meanItemLevel
```

The expected augment slots interpolate inversely across the bounded level range:

- at `mean - 12`, expected slots equal the effective maximum;
- at the mean, expected slots are the midpoint of the effective range;
- at `mean + 12`, expected slots equal the authored minimum.

The effective maximum is the lower of the strongbox policy maximum and the selected equipment definition's maximum slot capacity. A second deterministic bell-shaped roll is applied around the expected slot budget and clamped to the effective range. The roll uses the isolated `strongbox-rng.augment-slots-v1` substream.

This produces the intended compensation:

- under-leveled equipment is heavily weighted toward high augment capacity;
- over-leveled equipment is heavily weighted toward low augment capacity;
- equipment near the mean receives a middle power budget.

A typical production policy may use `0..10` slots with a narrow slot deviation, producing approximately `8..10` slots for substantially under-leveled results and `0..2` for substantially over-leveled results. These are authored balance values, not hardcoded runtime caps.

## Shared GEN composition

BOX does not implement a parallel equipment selector, quality selector, or augment compatibility system. It derives temporary GEN policies from the accepted shared policy:

- the selection policy retains every candidate supporting the target level, fixes item level, and requests zero augments;
- the final policy retains only the selected definition and quality, fixes item level and compensatory slot count, and keeps the accepted augment candidates and compatibility rules.

Impossible target levels or slot counts fail before RAP mutation and leave the box owned.

## Deterministic identities and streams

For a new opening, BOX derives stable identities from canonical inputs with SHA-256:

- opening/source operation: `boxop.<48 hex>`;
- reward commitment: `boxcommit.<48 hex>`;
- effective profile: `boxprofile.<48 hex>`;
- mandatory scrap grant: `boxscrap.<48 hex>`;
- opening transaction: `boxtx.<48 hex>`;
- claim: `boxclaim.<48 hex>`;
- strongbox consumption transaction and operation: `boxconsume.<48 hex>` and `boxconsumeop.<48 hex>`;
- selection operation and temporary selection instance per grant and slot;
- final equipment instance per grant and slot: `boxequipment.<48 hex>`;
- final equipment operation per grant, slot, and power-budget roll: `boxequipmentop.<48 hex>`;
- temporary fixed-level/fixed-slot generation policies.

Named RNG streams isolate item level, item selection, augment-slot compensation, final equipment generation, and scrap. No identity or gameplay result depends on time, random GUIDs, process counters, scene hierarchy, or object names.

## Opening state machine

```text
new
  -> Prepared (reward result, equipment payloads, commit, claim and consume command frozen)
  -> RewardCommitted
  -> RewardClaimedPending  --retry same RAP claim/children--+
  -> RewardApplied                                      |
  -> Opened (INV strongbox removal confirmed) <---------+
```

Generator and payload failures leave the box owned. Predictable RAP preflight rejection leaves the frozen opening retryable. A post-preflight interruption leaves RAP in `Claimed`, and BOX calls `Retry` with the same commitment and claim identities.

After RAP reports `Applied`, BOX submits one deterministic INV `RemoveStrongbox` command. If interruption occurs between reward application and box removal, replay retries only the same removal command. It does not reroll equipment, add scrap again, or reapply rewards.

## Duplicate semantics

Two kinds of duplicate must remain separate:

- **Duplicate equipment definition:** valid. Independent slots or independent boxes may produce the same weapon definition, each with a unique equipment instance ID.
- **Duplicate opening transaction:** idempotent. Replaying the same opening resumes pending work or returns the original terminal result without additional value.

A second opening identity for the same physical box is rejected once the first opening is frozen. Reusing an opening identity with different canonical command content is also rejected.

## Reward payloads and equipment

`DeterministicStrongboxGrantPayloadResolverV1` maps money, scrap, stackable holdings, generated strongboxes, and equipment into RAP payloads. `StrongboxEquipmentGenerationResolverV1` is the concrete BOX-to-GEN equipment implementation. RAP and INV retain the exact immutable generated instances; BOX does not mutate their stats afterward.

## Snapshot and replay

`StrongboxOpeningSnapshotV1` retains the registered box context, opening command and stage, frozen reward result and reward-generation trace, resolved immutable payloads, RAP commands, INV consumption command, terminal opening fact, and a canonical snapshot fingerprint.

The resolved equipment instances are frozen inside the payload before RAP. Restoring and retrying an opening therefore cannot reroll its weapon definition, item level, quality, augment slots, augments, or instance identities.

RAP, SCR, MON, and INV snapshots remain owned by their authorities. A save-game composition must restore those authorities consistently before importing or resuming BOX state.

## Failure behavior

- Unknown instance: rejected without mutation.
- Registered but not owned instance: rejected without mutation.
- Unknown tier or captured content-fingerprint mismatch: rejected without mutation.
- Invalid zero-scrap policy: rejected at definition construction.
- Missing tier equipment binding or mismatched GEN policy identity: rejected before mutation.
- No equipment candidate supports the rolled target level: rejected before mutation.
- GEN equipment failure, selection drift, item-level drift, or augment-slot drift: rejected before mutation.
- RAP preflight rejection: box remains owned and the frozen opening is retryable.
- RAP post-preflight interruption: same child plan is retried.
- INV consumption interruption: same removal command is retried; rewards are not reapplied.
- Snapshot validation failure: live BOX state is unchanged.
