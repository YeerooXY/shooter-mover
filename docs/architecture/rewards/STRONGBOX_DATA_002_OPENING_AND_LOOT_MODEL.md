# STRONGBOX-DATA-002 — authoritative opening and loot-roll model

## Status

This document defines the shared target architecture for strongbox acquisition, opening, equipment selection, item-level rolling, augment rolling, reveal presentation and simulation.

It is a design authority for the next implementation task. It does not change current strongbox balance, production opening behavior, shop behavior, scenes, UI, animation, inventory persistence or simulator execution.

The design must preserve one production loot authority. Mission rewards, shop purchases, opening animation, retries and the balance simulator must all consume the same strongbox-opening result rather than implementing separate random-selection logic.

---

## Core decision

A strongbox opening is resolved in this order:

```text
exact owned StrongboxInstance
    -> StrongboxTierDefinition
    -> StrongboxOpeningProfile
    -> character/profile level snapshot
    -> target-level roll
    -> eligible equipment pool
    -> deterministic normalized item raffle
    -> selected equipment definition
    -> exact item-level roll
    -> augment-capacity and shared-level roll
    -> exact EquipmentInstance plus generated augment signature
    -> immutable StrongboxOpeningReceipt
    -> reveal animation and UI projection
```

The strongbox tier owns its level targeting, rarity allocation and augment-base tables.

Each weapon or gear definition owns only its equipment-specific drop metadata, such as authored rarity, peak level, availability and base selection weight.

An owned strongbox does not copy its tier's odds. An owned equipment instance does not copy the tables that generated it.

---

## Existing authorities to preserve

The current repository already contains the main pieces of this model:

- `ProductionStrongboxCatalogV1` for the eleven production tier identities and broad tier balance;
- `ProductionStrongboxHybridLootCatalogV1` for per-tier target-level, rarity, augment-slot and augment-level balance;
- `StrongboxHybridLootPolicyV1` for deterministic target, definition-weight, item-level and augment-signature decisions;
- `StrongboxHybridEquipmentGenerationResolverV1` for production candidate selection and equipment construction;
- `GeneratedEquipmentAugmentSignatureAuthorityV1` for rolled capacity/shared-level metadata;
- `StrongboxOpeningServiceV1` and the reward-application pipeline for authoritative opening and grant behavior;
- the production-backed strongbox simulator gateway, which must continue to call the real opening route.

The implementation task may reorganize authored data into clearer files or names, but it must not create a second rarity table, item selector, augment roller, random authority or simulator-only loot formula.

---

## Responsibility split

### `StrongboxTierDefinition`

Game-facing identity and the stable reference to one opening profile.

```yaml
strongbox_tier:
  id: strongbox-tier.steel
  tier_number: 1
  display_name: Steel Strongbox
  presentation_id: strongbox-presentation.steel
  opening_profile_id: strongbox-opening.steel-v1
```

This data may also retain other tier-owned reward presentation or economy values, such as scrap rewards, when those are part of the same production strongbox definition.

It must not contain copied weapon or gear candidates.

### `StrongboxOpeningProfile`

The lightweight authored balance loaded when a box is opened.

```yaml
strongbox_opening_profile:
  id: strongbox-opening.steel-v1

  target_level:
    minimum_offset: -4
    most_likely_offset: -2
    maximum_offset: 1

  rarity_odds:
    common: 800000
    rare: 180000
    epic: 18000
    legendary: 1900
    artifact: 100
    scale: 1000000

  item_level:
    offset_outcomes:
      - { value: -4, weight: 1 }
      - { value: -3, weight: 12 }
      - { value: -2, weight: 111 }
      - { value: -1, weight: 726 }
      - { value: 0, weight: 1000 }
      - { value: 1, weight: 726 }
      - { value: 2, weight: 111 }
      - { value: 3, weight: 12 }
      - { value: 4, weight: 1 }

  augments:
    slot_outcomes:
      - { value: 0, weight: 65 }
      - { value: 1, weight: 28 }
      - { value: 2, weight: 6 }
      - { value: 3, weight: 1 }

    shared_level_outcomes:
      - { value: 1, weight: 45 }
      - { value: 2, weight: 30 }
      - { value: 3, weight: 15 }
      - { value: 4, weight: 7 }
      - { value: 5, weight: 3 }
```

The numbers above illustrate the intended shape and the discussed Tier 1 percentages. They are not a balance migration instruction and must not silently replace the current production tables.

The central tier catalog maps every tier to exactly one versioned opening profile. Profiles may live in separate small files so opening code can resolve one tier without loading UI or scene data.

### `EquipmentDropDefinition`

Drop metadata belonging to one weapon or gear definition.

```yaml
equipment_drop:
  equipment_definition_id: equipment.weapon.pulse-shotgun
  rarity: common
  peak_drop_level: 8
  base_weight: 1.4
  available: true
  top_box_only: false
```

This must remain separate from combat data and from exact owned-instance state.

The minimum required values are:

- exact equipment definition identity;
- authored rarity;
- base selection weight;
- level-affinity anchor or peak level;
- availability;
- explicit exceptional eligibility gates such as `top_box_only`, where retained.

Weapons and gear must participate through the same equipment-candidate boundary. The simulator may not invent wearable behavior while production remains weapon-only.

### `StrongboxInstance`

The exact unopened box owned by the player.

```yaml
strongbox_instance:
  instance_id: strongbox-instance.abc123
  tier_id: strongbox-tier.steel
  state: unopened
  acquisition_operation_id: shop-purchase.456
```

It stores identity, tier, ownership/provenance and lifecycle state.

It does not store:

- rarity odds;
- level-offset tables;
- candidate weights;
- augment tables;
- preselected loot.

### Generated equipment state

After a successful opening, the durable result remains split across the existing equipment and generated-signature authorities:

```yaml
equipment_instance:
  instance_id: equipment-instance.xyz789
  definition_id: equipment.weapon.pulse-shotgun
  item_level: 8
  quality_id: equipment-quality.common
  installed_augments: []
```

```yaml
generated_augment_signature:
  equipment_instance_id: equipment-instance.xyz789
  source_strongbox_instance_id: strongbox-instance.abc123
  capacity: 2
  shared_augment_level: 5
  opening_profile_id: strongbox-opening.steel-v1
```

The exact copy must preserve its rolled item level, quality, augment capacity and shared augment level after future balance changes.

It does not preserve every probability or candidate weight used during generation as normal inventory data.

---

## Authoritative opening algorithm

### 1. Validate the opening command

The command identifies:

- exact strongbox instance;
- exact character/profile whose level applies;
- stable operation identity;
- expected account revision or equivalent concurrency boundary;
- algorithm/content version where required for deterministic replay.

The service rejects boxes that are missing, already consumed by another operation, owned by another account or incompatible with the requested character context.

An exact retry of a completed operation returns the same opening receipt. It does not reroll.

### 2. Resolve the tier and profile

Resolve:

```text
StrongboxInstance.TierId
    -> StrongboxTierDefinition
    -> StrongboxOpeningProfile
```

The tier and opening-profile fingerprint/version become part of the opening receipt and deterministic random purpose derivation.

### 3. Snapshot the current profile level

The opening uses the current canonical character/profile level at the moment the authoritative command is accepted.

The snapshot is immutable for that opening. Animation duration, client lag or later character progression cannot modify the result.

### 4. Roll the target level

The tier profile supplies a minimum, most-likely and maximum offset.

```text
profile level 10
+ rolled offset -2
= target level 8
```

The distribution is deterministic and biased toward the authored most-likely offset rather than treating every value equally unless the profile explicitly defines equal weights.

Early boxes may target lower levels. Higher boxes may target equal or higher levels.

There must be one target-level authority. Existing broad tier offsets and hybrid-policy target deltas must not remain as two independently meaningful equipment-target calculations.

### 5. Build the eligible equipment pool

Load all canonical equipment drop definitions and retain candidates that satisfy the production rules:

- definition exists in the canonical equipment catalog;
- definition is available/live;
- supported equipment category;
- valid authored rarity;
- explicit tier restriction such as `top_box_only`;
- valid positive base weight;
- valid level-affinity data;
- any future explicit content gates.

Candidate ordering is always stable by equipment definition identity before random selection.

### 6. Calculate each candidate's raw item weight

For candidate `i`:

```text
raw_item_weight(i)
    = authored_base_weight(i)
    x level_affinity(target_level, peak_drop_level(i))
```

The level-affinity function is one versioned production policy. It must never be reimplemented in the simulator, shop or reveal UI.

A candidate with invalid, non-finite or non-positive resolved raw weight is excluded with a deterministic diagnostic.

### 7. Apply the tier's rarity allocation

The strongbox profile defines the intended rarity allocation for the opening.

To preserve those authored odds while still performing one item raffle:

1. group eligible candidates by rarity;
2. total the raw item weights within each non-empty rarity bucket;
3. allocate that rarity's ticket share across its candidates proportionally to their raw item weights;
4. combine all candidate tickets into one stable final pool;
5. perform one deterministic weighted selection.

For rarity `r` and candidate `i` in that rarity:

```text
final_ticket_share(i)
    = strongbox_rarity_share(r)
    x raw_item_weight(i)
    / sum_raw_item_weight(r)
```

Implementation must use deterministic fixed-point/integer arithmetic, stable ordering and an explicit remainder-allocation rule. Floating-point iteration order must not change the winner.

This produces the discussed raffle shape:

```text
common weapon 1      14,000 tickets
common weapon 2      12,000 tickets
common gear 1        10,000 tickets
epic weapon 1           300 tickets
artifact weapon 2        20 tickets
```

The values are the final results of tier rarity allocation, item base weight and target-level affinity, not separately authored duplicate odds.

#### Empty rarity bucket

When an authored rarity has no eligible candidates, the implementation must not silently fail or leave unreachable ticket space.

The draft policy is:

- remove empty rarity buckets;
- renormalize the configured rarity shares across the remaining non-empty buckets;
- record both configured and effective rarity allocation in the opening receipt/simulator observation;
- reject only when no eligible candidate exists at all.

Any different policy must be explicit, versioned and shared by production and simulator.

### 8. Select one equipment definition

Roll one deterministic integer threshold across the complete final ticket total and resolve the first cumulative range containing it.

The winner is an equipment definition, not an equipment instance yet.

### 9. Roll the exact item level

After the definition is selected, the opening policy rolls the concrete item level for that copy.

The item-level decision may use:

- the rolled target level;
- the selected definition's peak/anchor level;
- the tier's authored item-level offset outcomes;
- the equipment definition's permitted item-level range.

The existing target/definition blend must either be retained exactly or replaced through an explicit versioned migration. It must not change accidentally while reorganizing files.

The final item level is clamped only to the selected equipment definition's canonical supported range.

### 10. Roll augment capacity and shared level

Only after the item and item level are known, resolve the augment signature.

The current conceptual input is:

```text
opening profile's base slot table
+ opening profile's base shared-level table
+ profile-level versus item-level bias
+ selected rarity's augment bias
+ equipment slot limits
= exact capacity and shared augment level
```

The current bias shape is retained unless deliberately versioned:

```text
augment bias
    = profile level
    - rolled item level
    + rarity augment-bias value
```

The bias adjusts the authored outcome weights. It does not install augments.

Freshly generated equipment always starts with:

```yaml
installed_augments: []
```

The player later chooses compatible augment types for the available slots.

### 11. Commit atomically

A successful opening atomically:

- marks or consumes the exact strongbox instance;
- creates the exact equipment instance;
- stages and commits its generated augment signature;
- applies all other mandatory box rewards through the existing reward pipeline;
- advances account revision/state;
- stores an immutable opening receipt.

No visible animation is required for the grant to be durable.

### 12. Return the immutable opening receipt

```yaml
strongbox_opening_receipt:
  operation_id: strongbox-open.123
  strongbox_instance_id: strongbox-instance.abc123
  tier_id: strongbox-tier.steel
  opening_profile_id: strongbox-opening.steel-v1
  profile_level_snapshot: 10

  target_level:
    offset: -2
    value: 8

  selection:
    configured_rarity_odds_fingerprint: ...
    effective_rarity_odds_fingerprint: ...
    selected_definition_id: equipment.weapon.pulse-shotgun
    selected_rarity: common

  result:
    equipment_instance_id: equipment-instance.xyz789
    item_level: 8
    quality_id: equipment-quality.common
    augment_capacity: 2
    shared_augment_level: 5

  receipt_fingerprint: ...
```

The durable receipt may retain compact fingerprints and selected decision data for replay/audit without copying the complete candidate pool into the player's inventory record.

---

## Acquisition flows

### Mission reward

```text
mission reward
    -> grant exact StrongboxInstance
    -> inventory/strongbox collection
    -> player opens later through canonical opening command
```

Receiving the box does not preselect its item.

### Shop purchase

```text
shop purchase
    -> debit canonical currency
    -> grant exact StrongboxInstance
```

A shop may offer `Buy` or `Buy and open`.

`Buy and open` is two authoritative operations:

```text
purchase box
    -> receive exact StrongboxInstance
    -> call the normal strongbox opening command
```

The shop owns no separate loot generator.

### Other acquisition sources

Achievements, events, compensation, gifts and future systems grant the same `StrongboxInstance` shape and use the same opening command.

---

## Opening animation and reveal UI

The reveal animation is a presentation consumer, never a loot authority.

Correct flow:

```text
player requests open
    -> authoritative opening commits
    -> immutable receipt returned
    -> animation plays from receipt
    -> item card reveals exact committed result
```

The reveal projection resolves:

- box-tier sprite/animation;
- selected equipment side-profile art;
- display name;
- rarity presentation;
- exact item level;
- augment capacity;
- shared augment level;
- any additional granted rewards.

The animation performs no random rolls and cannot alter the result.

If the application closes after the grant but before the animation finishes, reopening the screen reads the same receipt and resumes or skips to the same reveal.

This prevents reroll exploits caused by animation cancellation, network retry, scene reload or process restart.

---

## Inventory and account boundaries

### Store on the exact strongbox instance

- strongbox instance identity;
- tier identity;
- ownership/acquisition provenance;
- unopened/opened/consumed lifecycle state;
- completed opening operation/receipt reference where needed.

### Store on the exact equipment result

- equipment instance identity;
- equipment definition identity;
- final item level;
- final quality identity;
- installed augment instances.

### Store in generated augment metadata

- exact equipment instance identity;
- source strongbox identity;
- opening/generation policy identity and version;
- rolled augment capacity;
- rolled shared augment level;
- generation fingerprint/provenance.

### Do not copy into inventory state

- tier rarity tables;
- target-level distributions;
- item-level offset tables;
- augment outcome tables;
- all candidate weights;
- equipment peak levels;
- base drop weights;
- PNG paths or animation logic.

---

## Simulator contract

The simulator calls the complete production opening path with isolated disposable holdings.

Input:

```yaml
strongbox_simulation_request:
  tier_id: strongbox-tier.steel
  profile_level: 10
  sample_count: 100000
  root_seed: 123456
```

It must not accept hand-authored rarity, level or augment tables that override production.

Each observation should expose enough immutable decision data to aggregate:

- target-level offsets and target levels;
- configured and effective rarity allocation;
- selected equipment definitions;
- selected equipment categories;
- selected authored rarities;
- raw/final weight diagnostics where intentionally available;
- final item levels;
- augment bias;
- augment capacity;
- shared augment level;
- exceptional outcomes;
- rejection diagnostics.

Reports should include global and per-equipment distributions without retaining every generated instance after aggregation.

The simulator must show configured versus observed rarity distribution so balance drift is visible immediately.

When gear becomes production-supported, the same production candidate boundary makes it appear in simulation automatically. The simulator must not implement a gear-only fallback.

---

## Determinism and replay rules

- All candidate identities are stably ordered before weighting and selection.
- Every random decision uses the existing deterministic random authority and a distinct stable purpose ID/substream.
- Target level, definition selection, item level, augment slots and augment level are separate deterministic decisions.
- Exact operation replay returns the original receipt.
- Reusing an operation identity with different input is rejected as a conflict.
- Fixed-point/integer weight calculation is preferred for final item tickets.
- Any normalization remainder is distributed by one documented stable rule.
- Policy/catalog/profile fingerprints participate in deterministic identity.
- UI timing, animation frames and client iteration order never affect loot.

---

## Validation rules

A tier/profile catalog is invalid when:

- a tier has no opening profile;
- profile identity is duplicated;
- target offsets do not satisfy minimum <= most likely <= maximum;
- rarity odds are missing, negative or total zero;
- configured rarity scale does not reconcile;
- item-level outcomes are empty or non-positive in total weight;
- augment slot outcomes are empty or invalid;
- augment-level outcomes are missing when positive slot outcomes exist;
- a candidate rarity is not recognized by the profile;
- duplicate equipment identities enter the candidate pool;
- final ticket totals overflow;
- no eligible candidate exists;
- a generated result exceeds canonical equipment or augment limits;
- an opening receipt cannot validate against its recorded fingerprints.

Unsupported data fails explicitly. It must not be repaired by hidden fallback weights or substituted equipment.

---

## Relationship to weapon data

`WEAPON-DATA-002` defines what a weapon is and how it fires.

This strongbox model consumes only stable equipment/drop metadata:

```text
weapon combat definition
    != weapon drop metadata
    != exact equipment instance
```

A weapon's item level is not part of its authored combat definition and does not rescale baked weapon combat values unless a separate explicit future progression system introduces that rule.

The strongbox system selects an equipment definition and creates an exact owned copy. It does not mutate the weapon definition.

---

## Implementation sequence

1. Freeze this design and reconcile it against the current two central tier/hybrid catalogs.
2. Establish one explicit `StrongboxTierDefinition -> StrongboxOpeningProfile` mapping without changing current balance.
3. Define versioned game-friendly authored contracts for target level, rarity odds, item-level outcomes and augment outcomes.
4. Define the canonical equipment drop projection shared by weapons and gear.
5. Replace floating candidate selection with deterministic fixed-point normalized ticket allocation where required by exact rarity odds.
6. Preserve the existing item-level and augment-bias behavior through explicit adapters/fingerprints before any balance change.
7. Add durable exact `StrongboxInstance` lifecycle and idempotent opening receipt boundaries where incomplete.
8. Route mission, shop and future acquisition sources through the same strongbox-instance grant.
9. Route all reveal animations through committed opening receipts.
10. Update the simulator to report target, configured/effective rarity, definition, item-level and augment distributions from the same production route.
11. Add gear only through the production equipment-candidate catalog, never through simulator-only logic.
12. Validate in-game opening, crash/retry recovery, shop buy/open flow and simulator parity during the prototype phase.

Each implementation step should be committed separately and should not bundle unrelated gameplay or scene work.

---

## Explicit non-goals for this design PR

- no balance-number changes;
- no strongbox tier renaming;
- no shop implementation;
- no opening animation implementation;
- no new weapon or gear content;
- no inventory migration;
- no scene or prefab changes;
- no alternate simulator formula;
- no automated tests during the current prototype phase unless requested separately.
