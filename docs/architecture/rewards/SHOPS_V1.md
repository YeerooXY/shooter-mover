# SHOPS_V1 — Deterministic procedural shop runtime

## Scope

`SHOP-001` owns engine-independent shop inventory and purchase truth. It does not
own scenes, shop UI, teleport presentation, or production balance values.

The runtime composes the existing authorities:

- **GEN-001** creates immutable equipment instances.
- **MON-001** performs the money spend and deterministic refund.
- **INV-001** owns granted equipment instances.
- **RAP-001** commits, preflights, applies, and retries the equipment grant.

No shop code writes another authority's private state.

## Definition contract

`ShopDefinitionV1` is immutable and fingerprinted. It contains:

- a stable shop identity;
- configurable inventory size;
- eligible equipment categories;
- required and excluded equipment tags;
- an `EquipmentGenerationPolicyV1` consumed through GEN-001;
- a progression-context snapshot policy;
- an integer pricing policy;
- disabled or explicit run-bound refresh policy;
- maximum accepted refresh count for one run;
- base lock capacity and an optional runtime extension port;
- definition schema version and deterministic algorithm version.

`ShopDefinitionAsset` is an authoring adapter. It contains no shipped production
prices or shop assets; authors must provide project-specific values.

## Inventory identity and seed derivation

For refresh ordinal `r`, the inventory root seed is the first 64 bits, interpreted
big-endian, of SHA-256 over this canonical text:

```text
schema=shop-inventory-seed-v1
run_id=<run StableId>
shop_id=<shop StableId>
refresh_ordinal=<non-negative integer>
algorithm_version=<positive integer>
```

The root seed therefore depends only on run ID, shop ID, refresh ordinal, and
algorithm version. GEN-001 receives this root seed for every slot; deterministic
slot operation and instance identities provide distinct named substreams.

Stock-entry, equipment-instance, generation-operation, purchase, commitment,
claim, spend, and refund IDs are SHA-256-derived `StableId` values. Reconstructing
the same run/shop/ordinal/context/definition/catalog inputs yields the same
inventory, equipment, prices, traces, and stock-entry identities.

The inventory fingerprint includes definition fingerprint, frozen progression
context fingerprint, seed, entry identity, equipment fingerprint, price, and GEN
result fingerprint. Sold/pending state is deliberately excluded: a purchase
command retains the inventory identity it observed and receives `SoldOut`, not a
misleading stale-inventory result, after another accepted purchase.

## Stability and lifecycle

`Open` creates ordinal-zero inventory only when no run/shop record exists. Later
opens return the retained inventory unchanged, even when the caller represents a
revisit, death, restart, or a later progression context.

`ShopRuntimeSnapshotV1` persists immutable equipment, prices, sold/pending flags,
refresh ordinal, frozen contexts, seed, and fingerprints. Import validates both
the snapshot fingerprint and every inventory fingerprint. The first post-import
`Open` binds the snapshot to the matching definition and a catalog that validates
all retained equipment; it does not regenerate stock.

Progression context is either:

- frozen at the first open for every refresh; or
- resampled only when an explicit refresh is accepted.

A revisit never resamples progression context.

## Pricing

`ShopPricingPolicyV1` is deterministic integer arithmetic:

```text
price = max(minimum_price,
            base_price
          + per_item_level * item_level
          + per_quality_rank * quality_rank
          + per_augment * augment_count
          + per_augment_tier * sum(augment tiers)
          + per_augment_level * sum(augment levels))
```

All coefficients are non-negative, the minimum is positive, and checked overflow
rejects inventory generation. No floating-point pricing or production values are
embedded in runtime code.

## Purchase transaction semantics

A purchase command includes one transaction ID, run/shop/stock identities, the
observed inventory fingerprint, claimant, and exact expected price.

1. The shop validates identity, inventory fingerprint, stock state, exact price,
   and available money before authority mutation.
2. It reserves the stock entry for that purchase transaction.
3. It creates and commits a deterministic equipment-only RAP commitment.
4. MON spends the exact price once with the observed money sequence.
5. RAP claims the exact immutable equipment instance through INV.
6. The entry becomes sold out only after RAP confirms application.

If RAP preflight or claim rejects before application, the shop issues a
deterministic MON refund and releases the entry. Thus a failed equipment grant
does not silently consume money. If RAP reports a post-preflight pending child,
the entry remains reserved and the exact purchase replay invokes RAP retry. The
spend transaction ID is reused, so retry cannot spend twice.

Terminal exact duplicates return `ExactDuplicateNoChange` plus the original
terminal status and fact fields. Reusing the transaction ID with a different
command fingerprint returns `ConflictingDuplicate`.

Insufficient money, unknown entries, stale inventory fingerprints, price
mismatches, sold entries, and conflicting duplicates do not mutate money,
holdings, or inventory availability.

## Refresh semantics

Refresh is never caused by open/revisit. It requires an explicit fingerprinted
command and is accepted only when:

- the definition enables explicit run-bound refresh;
- the current inventory fingerprint matches;
- the maximum accepted refresh count has not been reached;
- every locked entry exists and is available;
- lock count is within base capacity plus the optional extension provider;
- GEN can create every unlocked replacement and pricing succeeds.

Accepted refresh increments the ordinal exactly once. Rejected refresh leaves the
ordinal, seed, entries, prices, sold state, and fingerprint unchanged. Refresh
transaction identities are replay-safe and conflicting duplicates are rejected.

The lock-capacity extension is a read-only policy port. SHOP-001 does not create a
persistent reroll currency or grant free refreshes. A future scarce run-only token
authority can admit refreshes and supply extra lock capacity outside this contract.

## Validation expectations

The focused EditMode suite covers deterministic inventory and prices, run and
refresh identity, revisit/reload stability, configurable size, category/tag
filtering, exact-once money and equipment, sold-out behavior, duplicate/conflict
semantics, insufficient funds, RAP retry, stale/unknown input, refresh limits,
rejected refresh retention, and real GEN/MON/INV/RAP composition.

Repository-level static checks, duplicate GUID audit, cold Unity compilation,
focused shop tests, and the complete EditMode suite remain required before the
draft pull request is promoted.
