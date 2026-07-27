# WEAPON-INSTANCE-001 — Canonical Weapon Instance Foundation, Phase 1

## Status

This phase introduces the future canonical owned-weapon value, opaque instance-ID generation, and explicit physical mount availability.

It does **not** make `WeaponEquipmentInstance` authoritative in the current production holdings, persistence, loadout, Inventory, or live weapon-resolution paths.

The existing schema-v1 production route remains:

```text
opaque InstanceId
→ legacy EquipmentInstance
→ legacy item level / quality / augment payload
→ schema-v1 holdings and replay receipts
```

The target route remains future work:

```text
InstanceId
→ WeaponEquipmentInstance
→ WeaponDefinitionId
→ augment / overclock assignments
→ authoritative WeaponBlueprint
```

This PR is therefore a reusable domain foundation, not completion of the Inventory milestone or Track B.

## Canonical future owned weapon

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
- number of previously owned copies.

Old persisted identities are not silently rewritten. Identity replacement would create a different owned object.

### `WeaponDefinitionId`

`WeaponDefinitionId` references the authoritative `WeaponBlueprint`.

Example:

```text
InstanceId: instance.8d72c6c438eb4cd29a4dc24765a61420
WeaponDefinitionId: rattler.mk1
```

The definition resolves display name, family, Mark, rarity, authored mechanics, statistics, presentation, and firing behaviour. Those facts are not copied into the canonical owned instance.

### Assignment collections

`AugmentAssignments` and `OverclockAssignments` currently retain stable references only. The collections are copied, sorted, non-null, immutable, and duplicate-free.

This representation is provisional until augment and overclock ownership contracts are finalized. If the final mechanic requires positional facts such as:

```text
augment slot → augment instance
overclock socket → overclock instance
```

then a typed assignment object must replace the bare stable-ID list. Do not infer slot or socket identity from list order.

## Explicit exclusions

Do not add speculative instance metadata merely to preserve the legacy generic equipment shape. Excluded fields include:

- acquisition source;
- obtained timestamp;
- generated item level;
- quality;
- loot-box tier;
- mission source;
- creation sequence;
- number of identical weapons owned;
- copied weapon name or authored combat statistics.

Acquisition receipts and replay-safety facts belong to reward, transaction, ledger, or persistence operations.

## Class-correct physical mounts

A class layout is not a universal four-slot weapon array.

Every physical mount has explicit availability:

```text
Active
LockedBySkill
```

Aggressive is currently:

```text
Aggressive
├── Outer Left: Active
├── Center: LockedBySkill
└── Outer Right: Active
```

Therefore:

- physical mount count: `3`;
- active mount count: `2`;
- locked-by-skill mount count: `1`;
- starter instance count: `2`;
- locked mount instance ID: `null`.

There is no fourth Aggressive physical weapon mount.

The current four-record route/loadout payload remains only a legacy persistence and input bridge. Empty records outside the class layout are not physical mounts and must not be counted or presented as such.

## Starter boundary delivered in Phase 1

The current production starter grant still constructs the legacy generic `EquipmentInstance`. Phase 1 changes only its generated identity and the class mount projection:

- each active starter mount receives one independent opaque ID;
- a skill-locked mount receives no starter instance;
- locked mounts remain unbound and disabled;
- the legacy schema-v1 payload remains unchanged for save compatibility.

The onboarding test in this phase proves opaque identity allocation and class-correct active/locked mount projection. It does **not** prove production construction or resolution of `WeaponEquipmentInstance`.

## Required schema-v2 production cutover

The schema-v1 holdings ledger embeds legacy `EquipmentInstance` payloads in current projections and immutable replay receipts. Replacing them in place would invalidate fingerprints and transaction history.

The authoritative cutover requires a separate migration phase:

1. add a schema-v2 weapon payload containing `WeaponEquipmentInstance`;
2. retain deterministic schema-v1 dual-read conversion;
3. preserve existing transaction receipts and replay facts unchanged;
4. write only schema-v2 canonical weapon instances after migration;
5. migrate holdings lookup to `InstanceId → WeaponEquipmentInstance`;
6. resolve loadouts and live weapons from `WeaponDefinitionId` directly;
7. remove generic weapon `EquipmentInstance` compatibility only after migration verification.

Do not fake this cutover by inserting default item level or quality into the canonical weapon object.

## Inventory milestone not delivered here

Phase 1 does not implement:

- owned-weapon cards;
- exact-instance Inventory listing;
- equip or unequip;
- occupied-slot replacement;
- class-correct visible Inventory slots;
- locked-slot presentation or skill unlock;
- persistence of Inventory equipment changes beyond existing schema-v1 behaviour;
- gameplay rebinding after an Inventory change;
- schema-v2 holdings;
- loadout lookup through `WeaponEquipmentInstance`.

Those items remain Track B work.

## Guardrails delivered here

- future `WeaponEquipmentInstance` exposes only the four canonical data properties;
- assignment collection contracts are directly tested;
- new owned-equipment IDs are opaque and source-free;
- starter onboarding uses opaque IDs while retaining schema-v1 payload compatibility;
- Aggressive exposes three physical mounts: two active and one skill-locked;
- the locked mount is unbound and receives no starter instance;
- no claim is made that the canonical value is production-authoritative.
