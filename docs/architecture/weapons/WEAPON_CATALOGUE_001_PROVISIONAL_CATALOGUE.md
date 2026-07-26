# WEAPON-CATALOGUE-001 — provisional anchored system-test catalogue

## Status

Implemented on branch:

`agent/weapon-catalogue-001-dummy-strongbox-catalogue`

Launch `main` SHA:

`4fd7392666e4dd66499f36ab59cdc1e835373262`

This implementation creates the smallest production catalogue needed to exercise permanent
family rarity, MK1-MK3 progression, drop anchors, craft unlock metadata, equipment projection,
production strongbox participation, authoritative simulator input, and representative canonical
weapon systems.

It does not approve final combat balance or final family identities.

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

The canonical blueprint owns fire mode, shot pattern, damage channel, delivery, guidance,
impact, and effects. The flat catalogue is retained because the current production strongbox
resolver, inventory cards, shop, and simulator consume `WeaponCatalog` plus `EquipmentCatalog`.
It is not a second authored combat definition.

`ProductionStarterWeaponCatalogV1` no longer owns weapon content. It delegates to
`ProductionWeaponCatalogueV1`. Its starter-grant lists remain empty, because catalogue
membership must not fabricate an owned copy of every weapon.

The unregistered `CanonicalWeaponDefinitionSamples` content was deleted rather than left
beside the production catalogue.

## Catalogue matrix

| Family | Permanent rarity | Damage channel | System-test identity | MK1 drop / craft | MK2 drop / craft | MK3 drop / craft |
|---|---|---|---|---:|---:|---:|
| Rattler | Common | Physical | normal projectile; automatic, semi-auto, then three-shot burst | 1 / 1 | 25 / 25 | 50 / 50 |
| Ironwake | Common | Physical | semi-auto shotgun spread; 6, 8, then 10 pellets | 60 / 60 | 80 / 80 | 100 / 100 |
| Voltspike | Rare | Energy | seeking normal projectile; semi-auto, automatic, then burst | 58 / 58 | 79 / 79 | 100 / 100 |
| Prismata | Epic | Chemical | slow orb delivery; semi-auto, automatic, then burst | 64 / 64 | 84 / 84 | 99 / 99 |
| Crownfall | Legendary | Thermal | semi-auto contact rocket with increasing explosion radius | 68 / 68 | 88 / 88 | 103 / 100 |
| Nullstar | Artifact | Chemical | direct hit plus stacking, refreshing damage over time; semi-auto, automatic, then burst | 70 / 70 | 90 / 90 | 110 / 100 |

The non-Rattler names are provisional content names.

Every family contains exactly MK1, MK2, and MK3. Family rarity is supplied once and the
family constructor rejects a Mark whose canonical drop metadata carries another rarity.
The flat compatibility projection also rejects a family whose Marks disagree on damage channel.

## Behaviour coverage

Together the eighteen definitions explicitly cover the requested integration cases:

- normal travelling projectile;
- rocket delivery;
- orb delivery;
- semi-automatic fire;
- automatic fire;
- sequential burst fire;
- simultaneous shotgun spread;
- homing/seeking guidance;
- contact-triggered explosion;
- damage over time with tick rate, stack limit, and duration refresh.

The damage model coverage is:

- **Physical direct damage** — Rattler;
- **Physical multi-projectile direct damage and knockback** — Ironwake;
- **Energy guided direct damage** — Voltspike;
- **Chemical orb direct damage** — Prismata;
- **Thermal direct and area damage** — Crownfall;
- **Chemical direct damage plus DoT** — Nullstar.

## Provisional combat normalization

Rattler MK1 preserves the confirmed starter identity:

- Physical automatic projectile weapon;
- rate of fire: `4`;
- direct damage: `1`;
- Pierce: `1`;
- spread: `0`.

The other profiles are intentionally provisional. Their direct-hit values are arranged around
a simple baseline of roughly `4` direct DPS before explosion or DoT contribution:

```text
direct damage
    × firing cycles per second
    × simultaneous projectiles per shot
    × sequential shots per burst
```

This is not final balance. It exists so delivery, fire-mode, guidance, spread, and damage-channel
behaviour can be compared without every test being dominated by a wildly different direct-hit
baseline.

Crownfall's area damage and Nullstar's damage over time add output beyond that direct baseline.
Those effects are deliberately visible for integration testing and are not balance claims.

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

The flat catalogue now carries each family's real damage-channel label and basic cadence,
projectile-count, spread, projectile-speed, range, and knockback projection. Typed guidance,
explosion, and DoT policy remain authoritative on the canonical `WeaponBlueprint`; the old flat
schema is not promoted into a second mechanics authority.

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
- per-Mark damage-channel disagreement inside a family;
- a drop anchor that differs from canonical drop metadata;
- invalid canonical fire-mode, burst, spread, guidance, delivery, rocket, explosion, or DoT structure;
- an invalid legacy flat-catalogue projection;
- a non-travelling definition that cannot be represented by the current flat projection;
- an invalid equipment catalogue;
- a canonical definition missing from the flat strongbox projection;
- a canonical definition missing its exact equipment projection;
- an equipment runtime reference that does not point back to the same definition.

## Manual inspection matrix

| Inspection target | Suggested definition |
|---|---|
| confirmed normal automatic starter | Rattler MK1 |
| semi-automatic trigger behaviour | Rattler MK2 |
| sequential three-shot burst | Rattler MK3 |
| simultaneous shotgun spread | Ironwake MK1-MK3 |
| seeking guidance | Voltspike MK1-MK3 |
| orb projectile delivery | Prismata MK1-MK3 |
| contact explosion and thermal area damage | Crownfall MK1-MK3 |
| chemical DoT stacking and refresh | Nullstar MK1-MK3 |

The authored anchor set still provides these strongbox inspection points:

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

- inspected the canonical fire, shot-pattern, delivery, guidance, explosion, DoT, and damage-channel contracts;
- matched each authored profile to the merged `WeaponBlueprint` validator requirements;
- preserved Rattler MK1's confirmed starter values;
- projected all four supported damage channels into the retained flat catalogue;
- included sequential burst count in the flat derived direct-DPS invariant;
- traced production loadout construction to the delegated weapon and equipment catalogues;
- kept catalogue membership separate from starter inventory seeding;
- kept the branch limited to runtime catalogue/flow code and architecture documentation.

## Validation not performed

- no automated tests were added or run under the current prototype policy;
- Unity compilation was not available and is not claimed;
- no PlayMode, scene, strongbox opening, simulator batch, or in-game firing run was performed;
- the missing canonical-to-Unity execution bridge remains outside this catalogue task;
- placeholder presentation references intentionally fall back until exact art is registered.
