# Weapon Catalogue Progression Vision

Status: design authority for follow-up catalogue and strongbox tasks.

## Purpose

This document locks the intended relationship between weapon families, Marks, rarity, strongbox drops and crafting unlocks before the catalogue receives final combat values.

The immediate goal is to support small follow-up tasks that can populate a dummy weapon catalogue and exercise strongbox selection without prematurely balancing damage, crafting costs or final drop probabilities.

## Core catalogue invariants

1. **One weapon family has one permanent rarity.**
   - A family never changes from Common to Rare, Epic, Legendary or Artifact between Marks.
   - Rarity belongs to the family, not to the individual item roll.

2. **Every weapon family has an MK1, MK2 and MK3 variant.**
   - A favourite weapon identity or mechanic returns during progression.
   - Later Marks refine the same fantasy instead of becoming unrelated weapons.
   - MK3 means the perfected version of that family; it does not mean that every MK3 is level-100 meta equipment.

3. **Rarity does not define the period in which a family may appear.**
   - New Common, Rare and Epic families must continue to appear in late-game loot.
   - A level-100 strongbox must be able to drop Common equipment designed for the level-100 era.
   - Early Common families, such as Rattler, may naturally age out while later Common families replace them in the current loot pool.

4. **Rarity controls scarcity, budget, flexibility and power ceiling.**
   - It does not make an era-appropriate Common weapon unusable.
   - Legendary and Artifact families should offer stronger ceilings, more specialised mechanics or more favourable trade-offs.
   - The endgame should contain several credible choices across weapon categories rather than collapse into two universal meta weapons.

## Terminology

### Weapon family

The persistent named identity and core mechanic shared by MK1, MK2 and MK3.

Example: `Rattler` is a Common kinetic automatic-rifle family. Its three Marks remain accurate, fully automatic and recognisably Rattler weapons.

### Mark

The generation of a weapon family:

- MK1 introduces the family.
- MK2 returns and develops it.
- MK3 is its final form.

Marks do not change the family's rarity.

### Drop anchor level

A catalogue value describing the effective loot level around which a specific family Mark belongs.

The drop anchor:

- guides strongbox eligibility and weighting;
- is not necessarily a hard minimum player level;
- is not a guaranteed-drop level;
- may exceed the player level cap;
- is separate from the item's eventual rolled level and combat values.

A strongbox implementation may favour entries near its effective loot level while still applying box-tier, rarity and explicit eligibility rules.

### Craft unlock level

The player level at which the recipe for a specific family Mark becomes available.

The crafting feature itself may be available throughout progression. Individual recipes unlock around their intended progression anchor.

Drop anchor and craft unlock are separate fields because above-cap loot anchors must remain craftable at the level-100 player cap.

Recommended default relationship:

```text
CraftUnlockLevel ~= min(DropAnchorLevel, 100)
```

This is only a default. A family may unlock its recipe slightly before or after its nominal drop anchor when progression design requires it.

### Effective strongbox level

The level used by the strongbox selection process. It may occasionally exceed the player cap at endgame.

This permits exceptionally rare above-cap MK3 drops without introducing player levels above 100.

## Late-game anchor intent

The following are design envelopes, not final per-family values.

| Rarity | Typical MK1 anchor | Typical MK2 anchor | Typical MK3 anchor |
|---|---:|---:|---:|
| Common | Any progression era | Mid or late return | Up to 100 |
| Rare | Any progression era | Mid or late return | Up to 100 |
| Epic | Midgame onward | Late game | Roughly 84-100 |
| Legendary | Roughly 55-80 | Roughly 80-96 | Roughly 96-104 |
| Artifact | Roughly 68-76 | Roughly 88-96 | Roughly 106-115 |

The intended Artifact rhythm is approximately:

- MK1 around level 70;
- MK2 around level 90;
- MK3 around effective loot level 110.

Some Legendary MK3 variants may sit just above the normal cap, generally around effective loot level 101-104. Artifact MK3 variants occupy the more substantial above-cap chase.

## Crafting vision

- Crafting as a system is available during normal progression.
- Each weapon Mark has its own recipe unlock.
- Recipes generally unlock around their progression anchor.
- A Mark with a drop anchor above 100 unlocks for crafting at level 100 unless a later design explicitly says otherwise.
- Crafting is the deterministic route to a chosen family and Mark.
- Strongboxes are the probabilistic route and may produce above-cap designs through exceptional effective-level rolls.

Future crafting costs will use at least:

- money;
- scrap;
- weapon rarity;
- progression or anchor level.

The exact formulas, costs, recipe dependencies and material quantities are intentionally deferred.

## Strongbox vision

A strongbox should select from era-appropriate catalogue entries rather than merely rescale every historical weapon forever.

At level 100, the catalogue must contain:

- late Common families designed for level-100 content;
- late Rare and Epic families;
- normal-cap Legendary and Artifact variants;
- a small above-cap Legendary MK3 chase;
- a rarer, higher above-cap Artifact MK3 chase.

Above-cap drop anchors do not mean that ordinary level-100 loot is obsolete. They create additional scarcity for the strongest final Marks.

The strongbox system will later determine:

- effective box level;
- anchor-distance weighting;
- rarity weighting;
- allowed box tiers;
- above-cap roll probability;
- duplicate and ownership behaviour;
- item-level and quality rolls.

## Power and viability intent

- Early Common MK3 families may be designed to fade around levels 50-60.
- Later Common MK3 families may be designed specifically for level-100 strongboxes and content.
- A level-100 Common should be usable and era-appropriate, but should usually have a lower ceiling than comparable higher-rarity families.
- Some Epic MK3 families should remain fully viable at level 100.
- Legendary and Artifact families should provide category-leading options with meaningful differences in bossing, mobbing, speed, safety, control and spectacle.
- No rarity or Mark alone guarantees universal superiority.

## Initial confirmed family

| Family | Rarity | Category | Identity |
|---|---|---|---|
| Rattler | Common | Kinetic automatic rifle | Accurate, fully automatic, no-spread starter family |

Rattler MK1 begins with the current baseline of 4 rate of fire, 1 damage, 1 Pierce and no spread. Final scaling and later-Mark values are outside this document.

## Deferred decisions

This vision does not yet define:

- the complete family-name catalogue;
- exact per-family anchor levels;
- final weapon stats or scaling;
- crafting prices;
- strongbox probability curves;
- box-tier eligibility;
- augment slots or item-quality ranges;
- final category balance.

Those decisions should be introduced through focused follow-up tasks rather than bundled into the vision PR.
