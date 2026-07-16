# Equipment and augments v1

## Status and scope

This document defines the EQP-001 engine-independent equipment and augment model.
It is the shared definition and immutable-instance boundary consumed by later
reward generation, holdings, strongbox, crafting, augment-upgrade, shop, and
balance-simulation tasks.

The implementation lives below:

- `Assets/ShooterMover/Runtime/Domain/Equipment/`;
- `Assets/ShooterMover/Runtime/Contracts/Equipment/`; and
- `Assets/ShooterMover/Content/Definitions/Equipment/`.

The model does not generate items, own equipment, equip loadouts, spend currency,
open strongboxes, craft, upgrade, price, persist, or execute weapon behavior.

The words **must**, **must not**, **required**, and **only** identify v1 contract
requirements. Changing an identity rule, canonical ordering, compatibility rule,
or immutable-instance meaning requires a reviewed versioned contract change.

## 1. Identity and category model

All durable identities use canonical `StableId` values from
[`STABLE_ID_V1.md`](STABLE_ID_V1.md).

The model distinguishes:

| Identity | Purpose |
|---|---|
| `equipment_definition_id` | One authored weapon, armor, or future equipment definition |
| `equipment_instance_id` | One immutable generated/owned equipment instance |
| `augment_definition_id` | One authored augment definition |
| `augment_instance_id` | One installed immutable augment instance |
| `category_id` | Open equipment category vocabulary |
| `family_id` | Definition-specific compatibility family |
| `quality_id` | Authored quality/tier label identity |
| `tag_id` | Extensible equipment compatibility tag |
| `exclusion_group_id` | Mutually incompatible augment group |

`equipment-category.weapon` and `equipment-category.armor` are the first accepted
categories. Category identity is data, not a closed enum, so later categories can
be added without changing the equipment instance shape.

Equipment definition IDs and augment definition IDs share one catalog identity
namespace boundary. A catalog rejects duplicate IDs within either set and rejects
an ID reused across both sets.

## 2. Equipment definitions

An immutable `EquipmentDefinition` contains:

```text
EquipmentDefinition
- definition_id
- category_id
- family_id
- display_name
- optional runtime_weapon_reference_id
- item_level_range [inclusive]
- maximum_augment_slots
- ordered quality_tiers[]
- ordered tags[]
```

### 2.1 Weapon references

Weapon equipment references an existing `weapon.*` identity only. The reference
connects generated equipment metadata to an accepted weapon package; it does not
copy or redefine firing, projectile, cadence, mount, ammunition, targeting, hit,
or behavior-module rules.

The five Stage 1 package identities supported by current content are:

```text
weapon.blaster-machine-gun
weapon.shotgun
weapon.rocket-launcher
weapon.arc-gun
weapon.ricochet-gun
```

A weapon category definition requires a `weapon.*` runtime reference. Armor and
other non-weapon categories must not carry a weapon runtime reference. Armor is
therefore valid without any dependency on weapon runtime implementation.

### 2.2 Levels, quality, and slots

Item levels use an authored positive inclusive range. The architecture has no
fixed maximum item level.

Quality/tier metadata is an arbitrary ordered set of `(quality_id, label, rank)`
entries. Ranks are positive and unique inside a definition. The architecture has
no three-tier limit.

`maximum_augment_slots` is a non-negative authored integer. Zero-slot equipment
is valid. One-slot and configured many-slot equipment use the same model; there
is no architectural slot-count ceiling.

## 3. Augment definitions

An immutable `AugmentDefinition` contains:

```text
AugmentDefinition
- definition_id
- family_id
- display_name
- compatibility
  - allowed category_ids[]
  - allowed family_ids[]
  - required tag_ids[]
  - excluded tag_ids[]
- exclusion_group_ids[]
- duplicate_policy
- tier_range [inclusive]
- level_range [inclusive]
```

An empty allowed-category or allowed-family collection means that dimension is
unrestricted. Every required tag must be present. No excluded tag may be present.
A tag cannot be both required and excluded.

Catalog validation rejects an augment definition that cannot match any equipment
definition in that catalog. This prevents a later generator from receiving a
known-impossible compatibility rule.

Tier and level ranges are positive, inclusive, and configurable. The architecture
has no three-tier or ten-level cap.

## 4. Duplicate and exclusion rules

Each augment definition declares one duplicate policy:

- `DisallowSameDefinition` — an equipment instance may contain at most one
  augment instance using that definition;
- `AllowSameDefinition` — multiple instances using that definition are permitted,
  subject to slot capacity and exclusion rules.

Exclusion groups operate across definitions and duplicates. Any two installed
augment instances whose definitions share an exclusion-group ID are incompatible.
The pair is rejected before later inventory or upgrade code can accept it.

Unique augment instance IDs remain mandatory even when a definition allows
multiple copies.

## 5. Immutable generated instances

Generated mutable state must not live in ScriptableObjects.

An immutable `EquipmentInstance` contains:

```text
EquipmentInstance
- equipment_instance_id
- equipment_definition_id
- item_level
- quality_id
- canonical ordered augment_instances[]
- fingerprint
```

An immutable `AugmentInstance` contains:

```text
AugmentInstance
- augment_instance_id
- augment_definition_id
- tier
- level
```

A future augment upgrade is represented by creating a replacement augment value
and a replacement equipment instance. The original equipment and augment values
remain unchanged. Keeping the same equipment instance ID for an accepted
replacement transaction is a holdings/application policy owned by later tasks;
EQP-001 only provides the immutable replacement representation.

## 6. Canonical ordering and fingerprints

Definitions and instances copy input collections into read-only collections and
sort them canonically:

- equipment definitions by definition ID;
- augment definitions by definition ID;
- equipment tags and compatibility IDs by `StableId` ordinal order;
- quality entries by rank, then quality ID;
- augment instances by instance ID, then canonical value.

Catalog and equipment-instance fingerprints are deterministic FNV-1a 64-bit
lowercase hexadecimal values over their canonical text. A fingerprint is a drift
and equality aid, not a collision-free durable identity. Persisted and transaction
boundaries must continue to carry the full StableIds and immutable payload.

Equivalent valid inputs produce identical canonical text and fingerprints even
when their source collection order differs.

## 7. Validation boundary

`EquipmentCatalog.Build` validates definitions before exposing a catalog.
`EquipmentCatalog.ValidateInstance` validates an immutable instance against one
accepted catalog. Validation returns deterministic canonically ordered issue
codes and makes no mutation.

The boundary rejects at least:

- missing, malformed, duplicate, or cross-type definition identities;
- missing categories or families;
- invalid item, augment-tier, or augment-level ranges;
- duplicate quality IDs or ranks;
- negative augment-slot maxima;
- invalid weapon/non-weapon runtime references;
- null or duplicate tag, compatibility, or exclusion values;
- impossible augment compatibility;
- unknown equipment, augment, or quality identities;
- item levels, augment tiers, or augment levels outside authored ranges;
- slot contents above capacity;
- null or duplicate augment instance identities;
- category, family, required-tag, or excluded-tag incompatibility;
- forbidden duplicate augment definitions; and
- exclusion-group conflicts.

Later generation, holdings, shop, crafting, and upgrade services must consume an
accepted catalog and must not silently repair an invalid definition or instance.

## 8. Unity authoring schemas

`EquipmentDefinitionAsset`, `AugmentDefinitionAsset`, and
`EquipmentCatalogAsset` are authored configuration schemas in
`ShooterMover.Content.Definitions`.

Their conversion methods:

1. parse every textual identity through `StableId` without normalization;
2. return explicit conversion errors for malformed serialized input;
3. construct engine-independent immutable domain values; and
4. invoke catalog validation before exposing an accepted catalog.

These assets may contain authored defaults and references. They must never store
generated equipment instances, augment instances, ownership, upgrade progress,
wallet balances, claim state, shop stock, or transaction state.

EQP-001 intentionally creates no production `.asset` catalog or balance values.
BAL-001 owns later human-approved production tuning assets.

## 9. Contract ports

`IEquipmentCatalogProvider` exposes one validated immutable catalog to consumers.
`IEquipmentInstanceValidator` accepts an immutable validation request and returns
a deterministic response carrying catalog and instance fingerprints plus issues.
`EquipmentCatalogSnapshot` is a canonical identity projection for diagnostics and
future version envelopes; it is not an inventory or persistence implementation.

## 10. Non-goals

Equipment and augments v1 does not:

- generate random equipment or augments;
- choose production item levels, qualities, probabilities, or slot distributions;
- own inventory, holdings, equipping, loadouts, or UI;
- open strongboxes, run shops, craft, salvage, reroll, or transact upgrades;
- spend money or scrap;
- rewrite weapon packages or dispatch runtime weapon behavior;
- create production assets, prefabs, scenes, or Stage 1 integration;
- define save files, migrations, or networking; or
- authorize mutable generated state in shared assets.
