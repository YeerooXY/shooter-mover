# WEAPON-CATALOGUE-001 — provisional anchored production catalogue

## Status

Implemented on branch:

`agent/weapon-catalogue-001-dummy-strongbox-catalogue`

Launch `main` SHA:

`4fd7392666e4dd66499f36ab59cdc1e835373262`

This implementation creates the smallest production catalogue needed to exercise permanent
family rarity, MK1-MK3 progression, drop anchors, craft unlock metadata, equipment projection,
production strongbox participation, and authoritative simulator input.

It does not approve final combat balance.

## Single authority

`ProductionWeaponCatalogueV1` is the only authored source for this provisional dataset.

Each Mark is authored once as:

```text
ProductionWeaponFamilyV1
    -> ProductionWeaponMarkV1
    -> canonical WeaponBlueprint
```

The catalogue then derives:

```text
canonical WeaponBlueprints
    -> flat WeaponCatalog compatibility projection
    -> EquipmentCatalog compatibility projection
```

The flat catalogue is retained only because the current production strongbox resolver,
inventory cards, shop, and simulator consume `WeaponCatalog` plus `EquipmentCatalog`.
It is not separately authored data.

`ProductionStarterWeaponCatalogV1` no longer owns weapon content. It delegates to
`ProductionWeaponCatalogueV1`. Its starter-grant lists remain empty, because catalogue
membership must not fabricate an owned copy of every weapon.

The unregistered `CanonicalWeaponDefinitionSamples` content was deleted rather than left
beside the production catalogue.

## Catalogue matrix

| Family | Permanent rarity | MK1 drop / craft | MK2 drop / craft | MK3 drop / craft |
|---|---|---:|---:|---:|
| Rattler | Common | 1 / 1 | 25 / 25 | 50 / 50 |
| Ironwake | Common | 60 / 60 | 80 / 80 | 100 / 100 |
| Voltspike | Rare | 58 / 58 | 79 / 79 | 100 / 100 |
| Prismata | Epic | 64 / 64 | 84 / 84 | 99 / 99 |
| Crownfall | Legendary | 68 / 68 | 88 / 88 | 103 / 100 |
| Nullstar | Artifact | 70 / 70 | 90 / 90 | 110 / 100 |

The non-Rattler names are provisional content names.

Every family contains exactly MK1, MK2, and MK3. Family rarity is supplied once and the
family constructor rejects a Mark whose canonical drop metadata carries another rarity.

## Combat values

Rattler MK1 preserves the confirmed starter identity:

- Physical automatic projectile weapon;
- rate of fire: `4`;
- direct damage: `1`;
- Pierce: `1`;
- spread: `0`.

All other entries deliberately reuse the same simple valid projectile profile:

- automatic rate of fire `4`;
- one projectile;
- direct damage `1`;
- Pierce `1`;
- no spread;
- projectile speed `20`;
- range `25`;
- no guidance, Ricochet, explosion, DoT, chain, or special behavior.

Those values are marked as provisional in both the canonical Mark metadata and the flat
catalogue notes. They are not approved family mechanics or balance.

## Strongbox and equipment projection

Each Mark creates one exact equipment definition:

```text
<family>.mkN
    -> equipment.weapon-<family>-mkN
```

The equipment record points back to the exact weapon definition. Each equipment definition
contains only its family rarity quality, preventing the strongbox quality step from rewriting
the selected weapon's permanent rarity.

All definitions are live catalogue candidates with:

- base selection weight `1`;
- explicit minimum strongbox tier `1`;
- no `top_box_only` restriction;
- drop anchor projected as the legacy `PeakDropLevel`;
- craft unlock projected into the retained crafting-route metadata.

Final rarity odds, tier gates, anchor-distance weighting, above-cap roll probability,
duplicate protection, and augment balance remain separate follow-up work.

## Production wiring

The existing production loadout runtime already obtains both catalogues through:

```text
ProductionStarterWeaponCatalogV1.BuildWeaponCatalog()
ProductionStarterWeaponCatalogV1.BuildEquipmentCatalog()
```

Those methods now return the single `ProductionWeaponCatalogueV1` projection.

Consequently the selected-character strongbox composition and production-backed simulator
receive the same eighteen weapon/equipment definitions without a second catalogue loader.

The legacy properties named `InitialEquipmentDefinitionStableIds` and
`AllEquipmentDefinitionStableIds` remain empty. They are starter-grant inputs, not catalogue
enumeration APIs. `CatalogueEquipmentDefinitionStableIds` exposes the actual catalogue
membership without causing inventory seeding.

## Fail-closed validation

Static catalogue construction rejects:

- a family without exactly MK1, MK2, and MK3;
- duplicate definition or equipment identities;
- per-Mark rarity disagreement;
- a drop anchor that differs from canonical drop metadata;
- invalid canonical weapon structure;
- an invalid legacy flat-catalogue projection;
- an invalid equipment catalogue;
- a canonical definition missing from the flat strongbox projection;
- a canonical definition missing its exact equipment projection;
- an equipment runtime reference that does not point back to the same definition.

## Manual inspection matrix

The authored anchor set provides these intended inspection points:

| Effective loot level | Entries centered at or near the inspection level |
|---:|---|
| 50 | Rattler MK3 and earlier Rattler Marks |
| 70 | Nullstar MK1; Crownfall MK1; late-family MK1 entries |
| 90 | Nullstar MK2; Crownfall MK2; late-family MK2 entries |
| 100 | Ironwake MK3, Voltspike MK3, Prismata MK3 |
| 103 | Crownfall MK3 |
| 110 | Nullstar MK3 |

The current strongbox policy uses soft weighting rather than hard anchor eligibility, so
"near" means favoured by the existing production policy, not guaranteed or exclusively
available.

## Validation performed

- inspected the merged canonical weapon validator and matched its authored projectile shape;
- inspected the retained flat-catalogue validator and preserved its family/definition ID and
  derived DPS invariants;
- traced production loadout construction to the delegated weapon and equipment catalogues;
- caught and repaired an integration hazard where exposing all catalogue IDs through the
  legacy starter list would have attempted to fabricate every definition as starter inventory;
- rebased the branch onto current `main` after unrelated room-runtime work advanced it.

## Validation not performed

- no automated tests were added or run under the current prototype policy;
- Unity compilation was not available and is not claimed;
- no PlayMode, scene, strongbox opening, simulator batch, or in-game firing run was performed;
- placeholder presentation references intentionally fall back until exact art is registered.
