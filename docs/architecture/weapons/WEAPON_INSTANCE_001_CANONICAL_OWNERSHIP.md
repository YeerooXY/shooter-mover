# WEAPON-INSTANCE-001 — Canonical Owned Weapon Identity

## Status

Accepted target model. This change introduces the canonical domain value and cuts newly generated starter identities over to the opaque format.

The existing generic `EquipmentInstance` persistence payload remains a compatibility boundary until the holdings schema is migrated with dual-read support. It must not be treated as the final weapon ownership model.

## Canonical owned weapon

Each exact owned weapon contains only:

```text
WeaponEquipmentInstance
├── InstanceId
├── WeaponDefinitionId
├── AugmentAssignments
└── OverclockAssignments
```

### `InstanceId`

`InstanceId` is the opaque, globally unique, persistent identity of this exact owned weapon.

New identities use:

```text
instance.<32 lowercase hexadecimal random characters>
```

The value must not encode:

- weapon name or family;
- Mark;
- class;
- mount or slot;
- owner;
- acquisition source;
- creation order;
- the number of previously owned copies.

Rejected identity styles include:

```text
rattler10
rattler-slot-2
aggressive-rattler-3
equipment-instance.onboarding-...
```

Old persisted identities are not silently rewritten because identity replacement would produce a different owned object. Migration may preserve a legacy opaque value while all newly generated identities use the canonical factory.

### `WeaponDefinitionId`

`WeaponDefinitionId` references the authoritative `WeaponBlueprint`.

Example:

```text
InstanceId: instance.8d72c6c438eb4cd29a4dc24765a61420
WeaponDefinitionId: rattler.mk1
```

The definition resolves:

- display name;
- weapon family;
- Mark;
- rarity;
- base mechanics and statistics;
- presentation;
- firing behaviour.

This authored data is not copied into the owned instance.

The compatibility `EquipmentDefinitionId` projection may remain at legacy API boundaries during migration, but it is not the authoritative weapon-type identity.

### `AugmentAssignments`

`AugmentAssignments` stores references to the augments installed on this exact weapon. Assignment references follow the augment ownership model and do not copy augment display or definition data.

Two weapons using the same `WeaponDefinitionId` may have different augment assignments.

### `OverclockAssignments`

`OverclockAssignments` stores references to the overclocks installed on this exact weapon.

Two weapons using the same `WeaponDefinitionId` may have different overclock assignments.

## Explicit exclusions

Do not add speculative instance metadata. The following fields are excluded unless a real mechanic later proves they belong to the exact persistent weapon object:

- acquisition source;
- obtained timestamp;
- generated item level;
- quality;
- loot-box tier;
- mission source;
- creation sequence;
- number of identical weapons owned;
- copied user-facing weapon name.

Acquisition receipts and replay-safety facts remain in reward, transaction, ledger, or persistence operations.

## Holdings

The target authoritative holdings shape is:

```text
InstanceId → WeaponEquipmentInstance
```

Two Rattler MK1 weapons remain separate because they have different instance IDs:

```text
ID A
├── definition: rattler.mk1
├── augments: [...]
└── overclocks: [...]

ID B
├── definition: rattler.mk1
├── augments: [...]
└── overclocks: [...]
```

`Rattler MK1 × 2` is permitted only as a derived UI grouping. It is never authoritative ownership state.

## Loadout

A weapon mount stores only:

```text
MountId → InstanceId
```

The mount does not copy weapon names, authored definition data, assignments, or combat statistics.

Runtime resolution follows:

```text
mount
→ InstanceId
→ character holdings
→ exact WeaponEquipmentInstance
→ WeaponDefinitionId
→ authoritative WeaponBlueprint
→ augment and overclock composition
→ effective live weapon
```

## Starter weapons

Each active starter mount receives one independently created exact weapon instance:

```text
Instance A
├── unique opaque ID
├── definition: rattler.mk1
├── augments: empty
└── overclocks: empty

Instance B
├── different unique opaque ID
├── definition: rattler.mk1
├── augments: empty
└── overclocks: empty
```

Unavailable or locked mounts receive no instance.

## Kept weapon creation

When a generated weapon is accepted:

1. create one opaque unique `InstanceId`;
2. retain its `WeaponDefinitionId`;
3. retain its augment assignments;
4. retain its overclock assignments;
5. add that exact instance to character holdings.

The resulting weapon does not retain whether it came from an enemy, strongbox, crafting, shop, starter grant, or mission reward.

## Persistence migration boundary

The current schema-v1 holdings ledger embeds the legacy generic `EquipmentInstance` in command receipts and current projections. Replacing that payload in place would invalidate existing fingerprints and replay records.

The production cutover therefore requires a dedicated schema migration:

1. introduce a schema-v2 weapon payload containing `WeaponEquipmentInstance`;
2. retain schema-v1 dual-read and deterministic conversion for existing saves;
3. keep original transaction receipts immutable;
4. write only schema-v2 canonical weapon instances after migration;
5. update holdings lookup, loadout validation, and live weapon resolution to consume `WeaponDefinitionId` directly;
6. remove the generic weapon `EquipmentInstance` compatibility path only after save migration has shipped and been verified.

Do not fake this migration by assigning default item level or quality to canonical weapons. Those values are not legitimate weapon-instance state.

## Guardrails delivered here

- `WeaponEquipmentInstance` exposes exactly the four canonical data properties.
- assignment collections are immutable, sorted, non-null, and duplicate-free.
- `OwnedEquipmentInstanceIdFactory` generates source-free random identities.
- production starter onboarding uses the opaque identity factory.
- tests prove same-definition weapons remain distinct and may carry different assignments.
- tests prove the Aggressive starter layout creates two distinct instances and leaves unavailable positions unbound.
