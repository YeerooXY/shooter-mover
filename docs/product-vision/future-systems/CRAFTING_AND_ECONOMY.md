# Crafting and economy direction

## Status

Product-planning document only. Exact costs, drop rates and unlock levels are intentionally not final.

## Confirmed direction

- The initial economy should remain understandable.
- Money is the general purchasing and service currency.
- Scrap is the first material currency for crafting and equipment work.
- Overclock cores are a later high-end material, not part of the minimum first loop.
- Crafting presentation should separate weapons and gear.
- Selling and dismantling are different choices: selling returns money, while dismantling returns crafting material.
- Transactions must operate on exact equipment instances and must not duplicate rewards when repeated.
- The first production Keep/Sell loop should work before adding deep crafting complexity.

## Currency roles

### Money

Primary uses:

- buying exact shop items;
- paying ordinary crafting fees;
- installing, removing or changing augments;
- respec and convenience services where appropriate;
- pinning or refreshing shop offers if that feature survives review.

Primary sources:

- mission completion;
- selling exact equipment;
- mode milestones;
- account progression rewards.

Money should remain useful at endgame through recurring but fair sinks.

### Scrap

Primary uses:

- crafting known weapon or armour definitions;
- improving augment capacity within authored limits;
- installing mechanical modifications;
- repairing or recalibrating specialised equipment if such systems are added.

Primary sources:

- dismantling exact equipment;
- mission rewards;
- endless-mode milestones;
- duplicate or unwanted augment conversion.

Scrap should represent material progress, not a second version of money.

### Overclock cores

Later-game use only:

- capacity overclocks;
- signature overclocks;
- rare endgame crafting recipes;
- possibly unlocking the final step of a favourite weapon build.

Cores should not be needed to make ordinary weapons usable.

## Item disposition choices

After an item is acquired, the player should eventually have three clear actions:

| Action | Result |
|---|---|
| Keep | Retain the exact item in inventory. |
| Sell | Remove the exact item and receive a known money value. |
| Dismantle | Remove the exact item and receive known scrap/material value. |

The first implemented loop may support only Keep and Sell. Dismantle can arrive with crafting.

Each action must be previewed and exactly-once. Reopening a screen cannot sell or dismantle the same instance twice.

## Crafting philosophy

Crafting should provide **targeted progress**, not another blind lottery.

A player chooses a known recipe, sees the exact base result and pays known resources. Randomness, where used at all, should affect bounded secondary properties and should never replace the selected weapon family with an unrelated item.

## Working weapon-crafting flow

```text
unlock blueprint
-> choose weapon family and mark
-> preview base definition, quality and cost
-> choose optional supported material/augment input
-> confirm exact transaction
-> create one exact equipment instance
-> persist item and currency changes atomically
```

A crafted item should resolve through the same canonical definition and live execution path as a strongbox or shop item.

## Working armour-crafting flow

```text
choose slot: head / chest / legs / boots
-> choose known armour blueprint
-> preview defence identity and supported modifiers
-> pay money + scrap
-> create exact armour instance
```

Armour should not be added until the four-slot armour loop is ready to be visible and useful.

## Blueprint acquisition ideas

Blueprints may unlock through:

- character/account level milestones;
- first discovery of a weapon family;
- completing a specific level or boss;
- dismantling a sufficient number of related items;
- shop purchase;
- endless or difficulty-mode milestones.

Blueprint ownership is preferably account-wide so that discovering content does not need to be repeated on every character. Exact crafted items remain character-owned unless a later shared stash is designed.

## Recipe tiers

A working structure:

| Recipe tier | Purpose |
|---|---|
| Basic | common launch weapons, simple gear and introductory augments |
| Advanced | later marks, rare families and specialised status weapons |
| Elite | legendary/endgame definitions, signature augments and overclock preparation |
| Masterwork | carefully gated capstone recipes, not routine progression |

Recipe tiers should not automatically imply random rarity rerolls.

## Economy source and sink map

| System | Gives | Consumes |
|---|---|---|
| missions | money, boxes, occasional scrap | time and mode entry requirements |
| selling | money | exact item |
| dismantling | scrap/material | exact item |
| shop | exact item | money |
| crafting | exact item | money + scrap + blueprint |
| augment installation | modified exact item | money/scrap and augment capacity |
| overclock | capstone modification | core + money/scrap |
| respec | build flexibility | preferably low money cost |

## Anti-inflation principles

- Do not make every reward primarily money.
- Do not let endgame players accumulate currencies with no meaningful use.
- Repeated sinks should provide agency or convenience, not punish normal play.
- Avoid destructive upgrade failure that deletes rare items or cores.
- Prices should scale slower than the player's earning power at low levels so early progression feels active.
- Endgame costs may be substantial, but should correspond to visible progress and reasonable playtime.

## Duplicate handling

Duplicate items are not inherently bad because exact instances can carry different augments or builds. However, unwanted duplicates need useful exits:

- immediate Sell after reveal;
- later Dismantle for scrap;
- blueprint research progress;
- augment extraction where present;
- collection or mastery credit where appropriate.

Duplicate protection should reduce frustration without making the loot pool collapse after a few hours.

## Crafting station presentation

The existing direction is a screen that clearly switches between:

- Weapons;
- Gear.

Later sub-tabs may include Augments and Overclocking, but these should not clutter the first crafting release.

The screen should show:

- recipe identity and art;
- unlock requirement;
- exact resource cost;
- current owned resources;
- resulting item definition;
- any chosen modifiers;
- a before/after preview when upgrading an owned exact item.

## Open decisions

- Whether money and scrap are character-local or account-wide.
- Exact sell and dismantle value formulas.
- Whether crafting quality is fixed by recipe or influenced by optional materials.
- Whether blueprint discovery comes from drops, milestones or both.
- Whether shop-bought blueprints can replace level-gated unlocks.
- Whether dismantling can recover installed augments.
- Whether armour and weapons use the same scrap type.
- Whether any third ordinary material currency is needed before overclock cores.
- How costs are adjusted when the level cap expands from the initial release cap toward 100.
