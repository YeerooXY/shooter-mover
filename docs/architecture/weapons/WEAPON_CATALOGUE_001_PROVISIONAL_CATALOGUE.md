# WEAPON-CATALOGUE-001 — provisional authored catalogue

## Status

The production weapon source is:

```text
ProductionWeaponCatalogueV1
    -> WeaponCatalog
    -> EquipmentCatalog
```

`ProductionWeaponCatalogProvider` is a short access point for those two projections. It owns no content and does not seed inventory.

The deleted starter catalogue is not retained as an alias, fallback, hidden catalogue, or compatibility authority.

## Authored content

The provisional catalogue contains six families with MK1, MK2, and MK3 definitions: eighteen weapons in total.

| Family | Permanent rarity | Damage channel | System-test identity |
|---|---|---|---|
| Rattler | Common | Physical | automatic, semi-auto, then three-shot burst |
| Ironwake | Common | Physical | simultaneous shotgun spread |
| Voltspike | Rare | Energy | seeking projectile |
| Prismata | Epic | Chemical | orb delivery |
| Crownfall | Legendary | Thermal | contact rocket and area damage |
| Nullstar | Artifact | Chemical | direct hit plus stacking damage over time |

The non-Rattler family names and combat values remain provisional.

Each Mark is authored once as:

```text
ProductionWeaponFamilyV1
    -> ProductionWeaponMarkV1
    -> WeaponBlueprint
```

The blueprint owns fire mode, shot pattern, damage channel, delivery, guidance, impact, and effects. The flat `WeaponCatalog` and `EquipmentCatalog` are derived views used by current inventory, strongbox, simulator, presentation, and firing boundaries.

## Equipment projection

Each Mark produces one equipment definition:

```text
<family>.mkN
    -> equipment.weapon-<family>-mkN
```

The equipment definition points back to the exact weapon definition. Catalogue membership never grants ownership.

Every projected definition is validated for:

- unique weapon and equipment identity;
- family rarity consistency;
- damage-channel consistency;
- valid fire, burst, spread, delivery, guidance, explosion, and damage-over-time structure;
- a matching flat weapon projection;
- a matching equipment projection whose runtime reference points to the same weapon.

## Character onboarding

New characters enter through `ProductionWeaponOnboardingV1`.

The policy:

1. resolves the selected character's mount layout;
2. selects the authored Rattler MK1 starter definition;
3. creates one fresh exact `EquipmentInstance` per required mount;
4. grants each instance to the character holdings authority;
5. binds those exact owned IDs to the required slots;
6. validates the complete holdings/loadout state before it is published;
7. relies on the character save transaction to persist holdings and loadout together.

Supported layouts grant exactly:

| Profile | Required mounts | Starter instances |
|---|---:|---:|
| Striker | 2 | 2 |
| Combat Medic | 3 | 3 |
| Juggernaut | 4 | 4 |

Generated instance IDs are character-local and distinct. Route payloads may carry unbound navigation positions, but they never manufacture ownership.

## Retired save data

The pre-authored weapon set is deleted game content. It is not registered in either production catalogue and is not translated into a current weapon.

`RetiredWeaponSaveMigrationV1` is the isolated decode-and-delete boundary for old save IDs. For each affected character it:

- removes retired equipment holdings;
- clears bindings that no longer point to owned current equipment;
- removes generated-augment signatures tied to deleted instances;
- preserves valid current equipment, strongboxes, stacks, XP, currencies, scrap, skills, and unrelated components;
- runs normal onboarding for required empty mounts;
- creates fresh exact starter instances;
- returns a complete migrated account for atomic save before runtime restore.

Running the migration against an already migrated account produces no changes and grants no additional items.

## Connected systems

The selected-character graph restores holdings before validating exact loadout bindings. Level runtime adopts the same character authorities instead of rebuilding inventory.

The authored catalogue remains connected to:

- inventory/loadout presentation;
- strongbox equipment generation;
- durable mission rewards;
- simulator input;
- exact equipped-instance resolution;
- live firing.

Shop, crafting, armour, augments, selling, and general inventory tabs remain outside this cleanup.

## Inspection matrix

| Inspection target | Definition |
|---|---|
| confirmed starter | Rattler MK1 |
| semi-automatic trigger | Rattler MK2 |
| sequential burst | Rattler MK3 |
| simultaneous spread | Ironwake MK1-MK3 |
| seeking guidance | Voltspike MK1-MK3 |
| orb delivery | Prismata MK1-MK3 |
| contact explosion | Crownfall MK1-MK3 |
| damage-over-time stacking | Nullstar MK1-MK3 |

## Validation

Automated coverage now targets catalogue projection, 2/3/4-mount onboarding, exact identity, character separation, save migration, idempotence, restore, inventory opening, switching, strongbox grants, and live equipped-weapon resolution.

Unity compile, EditMode, PlayMode, and runtime execution results must be reported from the actual validation run; this document does not claim them by itself.
