# WEAPON-CATALOGUE-001 — Dummy Anchored Strongbox Catalogue

Status: follow-up implementation task specification.

## Goal

Create the smallest production-safe weapon catalogue dataset needed to exercise strongbox selection using weapon names, permanent family rarity and per-Mark progression anchors.

This task is intentionally about catalogue identity and loot placement. It is not a weapon-balance task.

## Dependency

Implement against the canonical authored weapon-definition and drop-metadata contracts from WEAPON-DATA-002A, or their merged equivalent.

Do not recreate a parallel weapon model if those contracts are available.

## Required catalogue fields

Each catalogue family must provide:

- stable family ID;
- display name;
- weapon category;
- one permanent family rarity;
- MK1, MK2 and MK3 entries.

Each Mark entry must provide:

- Mark;
- drop anchor level;
- craft unlock level;
- explicit strongbox eligibility required by the existing drop contract;
- placeholder authored combat values sufficient to construct and inspect the definition.

The family rarity must have one source of truth shared by all three Marks. Per-Mark data must not be able to silently contradict it.

## Anchor semantics

`DropAnchorLevel` describes where the Mark belongs in effective strongbox-level space.

`CraftUnlockLevel` describes when its recipe becomes available to the player.

Default the craft unlock to:

```text
min(DropAnchorLevel, 100)
```

The two values must remain independently authored so later progression tuning can intentionally separate them.

## Minimum coverage dataset

Add enough provisional families to cover all of these cases:

1. **Confirmed early Common family**
   - Rattler MK1, MK2 and MK3.
   - Rattler MK1 keeps the known starter identity: kinetic automatic rifle, 4 rate of fire, 1 damage, 1 Pierce and no spread.
   - Later Rattler values may remain placeholders in this task.

2. **Late Common family**
   - Its MK3 drop anchor is approximately level 100.
   - It proves that a level-100 strongbox can return Common loot designed for the current era.

3. **Late Rare family**
   - Its MK3 drop anchor is approximately level 100.

4. **Late Epic family**
   - Its MK3 drop anchor is approximately level 98-100.

5. **Slightly above-cap Legendary family**
   - MK3 drop anchor is approximately level 101-104.
   - MK3 craft unlock is level 100.

6. **Artifact family**
   - MK1 drop anchor is approximately level 70.
   - MK2 drop anchor is approximately level 90.
   - MK3 drop anchor is approximately level 110.
   - MK3 craft unlock is level 100.

The non-Rattler family names are provisional catalogue content and should be chosen as part of the task. Do not infer mechanics from names.

## Dummy combat values

Placeholder values may be used for families whose mechanics and balance have not yet been designed.

Requirements:

- clearly label placeholder definitions as dummy or provisional;
- use valid canonical definitions rather than bypassing validation;
- keep placeholder values simple and internally consistent;
- do not present placeholder damage, cadence, Pierce, spread or delivery values as approved balance;
- do not add weapon-specific runtime subclasses merely to make the dummy catalogue compile.

Where possible, use one deliberately boring valid projectile profile for placeholder entries while preserving only the catalogue metadata being tested.

## Strongbox integration boundary

Expose the dummy catalogue through the existing strongbox/catalogue input boundary so manual selection runs can inspect eligible entries at different effective loot levels.

The task must demonstrate that the selector can distinguish at least these cases:

| Effective loot level | Expected catalogue behaviour |
|---:|---|
| 50 | Early and mid-progression Marks are available according to existing eligibility rules |
| 70 | Artifact MK1 may enter the eligible set |
| 90 | Artifact MK2 may enter the eligible set |
| 100 | Late Common, Rare and Epic MK3 entries are present |
| 102-104 | Slightly above-cap Legendary MK3 may enter the eligible set |
| 110 | Artifact MK3 may enter the eligible set |

This task does not need to finalise strongbox rarity odds or anchor-distance weighting. It only needs enough real data and boundary wiring to exercise those systems in a later task or existing debug route.

## Crafting boundary

Record craft unlock metadata only.

Do not implement in this task:

- money costs;
- scrap costs;
- rarity multipliers;
- anchor-level cost formulas;
- recipe dependencies;
- crafting UI;
- actual crafting execution.

Those belong to a separate crafting-economy task.

## Non-goals

- Final weapon names beyond the small provisional dataset.
- Final damage or enemy-health scaling.
- Final weapon mechanics.
- Final rarity probabilities.
- Final strongbox tier rules.
- Final above-cap roll probabilities.
- Augment or quality-roll balance.
- Scene changes.
- Automated tests during the current prototype phase.

## Manual validation

Document the catalogue entries produced by the task and manually inspect selector eligibility at effective loot levels 50, 70, 90, 100, 102-104 and 110.

Confirm specifically that:

- all three Marks in one family share one rarity;
- every included family has MK1, MK2 and MK3;
- level-100 selection contains at least one late Common MK3;
- an above-cap Legendary MK3 is not normally eligible below its anchor policy;
- Artifact MK1 and MK2 appear before level 100;
- Artifact MK3 can use a drop anchor near 110 while its craft unlock remains 100;
- placeholder stats are visibly identified and cannot be mistaken for final balance.

## Follow-up tasks enabled

After this dataset exists, separate tasks can define:

- anchor-distance selection weighting;
- box-tier and rarity probabilities;
- above-cap effective-level roll chances;
- full weapon family names and categories;
- final MK1-MK3 mechanics and values;
- crafting money and scrap formulas;
- level-power and enemy-health scaling.
