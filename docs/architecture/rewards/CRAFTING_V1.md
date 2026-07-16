# CRAFTING_V1

Status: CRA-001 runtime contract.

## Purpose

Targeted crafting converts a positive scrap cost into exactly one immutable equipment instance. Recipes are authored data; the runtime contains no weapon names, level-specific item switches, production balance catalog, UI, scene, pickup, salvage, or dismantling behavior.

Crafting is intentionally later than ordinary discovery. A recipe carries the progression source identity, natural discovery level, ordinary discovery activation level, positive base delay, and optional non-negative bounded additional delay.

## Unlock formula

For recipe `r` and deterministic root seed `s`:

```text
additional_delay(r, s) = deterministic integer in
    [minimum_additional_delay, maximum_additional_delay]

crafting_unlock_level(r, s) =
    natural_discovery_level
    + crafting_delay_levels
    + additional_delay(r, s)
```

`crafting_delay_levels` must be strictly positive. The minimum possible crafting unlock must also be strictly greater than `ordinary_discovery_activation_level`. Thus an item with natural discovery level 50, base delay 5, and additional delay range 0–2 becomes craftable at level 55–57 without hardcoding the item or those levels in runtime code.

The delay roll uses the repository deterministic random contract, the recipe identity, the recipe generator algorithm version, and the caller-supplied root seed. Time, GUIDs, Unity object identity, and process-local hash order are not inputs.

## Recipe model

`CraftingRecipeV1` is immutable and canonical. It contains:

- schema/version and stable recipe identity;
- target equipment definition identity;
- natural discovery source identity, natural level, and ordinary activation level;
- positive crafting delay and optional bounded additional delay;
- positive scrap cost;
- fixed or deterministic weighted-random quality policy;
- item-level range;
- minimum/maximum augment slots and maximum augment tier/level;
- optional weighted augment definition identities;
- deterministic generator policy identity, algorithm version, activation parameters, and obsolescence parameters;
- canonical text and `sha256:` fingerprint.

Recipe option collections are identity-sorted before fingerprinting. Authoring order therefore cannot change the snapshot or result.

`CraftingRecipeDefinitionAssetV1` is the Unity authoring adapter. It converts serialized StableId strings and numeric policy fields into the engine-independent recipe. This task ships no production recipe assets.

## Validation order

`CraftingServiceV1.Craft` validates before creating a RAP commitment:

1. command and recipe identity;
2. target equipment definition existence;
3. deterministic unlock and progression eligibility;
4. current scrap affordability;
5. recipe quality, item-level, slot, augment, tier, and level compatibility with the authoritative equipment catalog;
6. GEN-001 output against the authoritative equipment catalog.

These failures do not change SCR-001, INV-001, or RAP-001 state.

## Deterministic generation

CRA-001 builds a task-local constrained projection of the authoritative equipment catalog:

- exactly the target equipment definition is eligible;
- item-level and slot ranges are intersected with recipe and definition caps;
- augment tier and level maxima are clamped to recipe caps;
- fixed quality supplies exactly one quality candidate;
- randomized quality supplies the recipe's canonical weighted candidates;
- augment candidates come only from recipe data.

The constrained catalog and policy are passed to `RewardGenerationServiceV1.GenerateEquipment`. CRA-001 does not duplicate GEN-001 selection, quality, slot, augment, tier, level, instance validation, or trace logic.

All derived operation, commitment, claim, grant, child transaction, equipment instance, and augment instance identities are deterministic. Reusing a craft transaction identity with the same canonical command regenerates the same plan. Reusing it with different content reaches RAP-001 as a conflicting duplicate.

## Atomic application

The generated plan contains two RAP grants:

1. a positive Scrap-kind quantity representing the recipe cost;
2. one EquipmentReference grant carrying the exact generated equipment instance.

RAP-001 retains the complete immutable plan, preflights every child authority before the first apply, and stores deterministic child transaction IDs for retry.

`CraftingScrapSpendRewardChildAuthorityV1` is the CRA-owned semantic bridge from RAP's positive Scrap child quantity to SCR-001's typed `ScrapMutationKindV1.Spend` command with:

- `ScrapIdentityV1.CraftingSpendReason`;
- `ScrapIdentityV1.CraftingSourceKind`;
- RAP's deterministic child transaction and operation identities;
- the original source operation and claimant provenance;
- optional expected sequence.

Equipment uses the existing `PlayerHoldingsRewardChildAuthorityV1`. No wallet, holdings, generator, or RAP implementation is rewritten.

An exact replay is no-change at RAP, SCR, and holdings. If an interruption occurs after one child applies, the next identical craft call detects the retained claimed commitment and invokes RAP retry. The same child transaction IDs and same equipment instance/fingerprint are reused.

## Statuses

CRA-001 reports explicit statuses for success, exact replay, conflicting duplicate, unknown recipe, unknown target equipment, unavailable progression, insufficient scrap, recipe/catalog incompatibility, generation rejection, retry-required RAP state, RAP rejection, and invalid command.

## Limitations

- Crafting admission uses character level from `ProgressionContext`; region, difficulty, and tags remain available to future recipe eligibility expansion but are not additional hard gates in V1.
- RAP-001's shared grant vocabulary has no debit grant kind. The crafting-owned scrap adapter therefore interprets the positive Scrap child as a cost and emits the authoritative SCR-001 spend command. Shared RAP contracts are intentionally unchanged.
- Affordability is checked before RAP commitment and checked again by RAP child preflight. As with the other in-memory authorities, callers must serialize competing mutations or use expected sequences to avoid a concurrent balance change between those checks.
- This task provides runtime and authoring types, not production balance assets, UI, scenes, pickups, salvage, or dismantling.
