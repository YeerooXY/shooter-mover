# Shop behaviour, stock and unlocks

## Status

Product-planning document only. The level ladder below is a concrete working proposal so the system can be discussed; it is not final balance.

## Confirmed direction

- The Hub contains a dedicated shop with a visible shopkeeper presentation.
- The shop concept has space for six weapon offers and six gear offers.
- A character may equip up to four weapons and four armour pieces: headpiece, mech chest armour, mech leggings and mech boots.
- Shop purchases create or transfer exact item instances; the shop does not grant abstract ownership flags.
- Inventory and shop presentation should make it obvious what is owned, equipped, affordable and locked.
- Later shop rotation should be deterministic enough to test and replay, with a way to pin an offer under consideration.

## Shop purpose

The shop provides controlled access to usable equipment and fills gaps left by random loot. It should not consistently outperform playing missions or opening strongboxes.

The shop is valuable because the player can deliberately buy:

- a known weapon family;
- a needed armour slot;
- a basic augment;
- a crafting blueprint or material bundle;
- a convenience item whose effect is fully disclosed.

## Initial layout proposal

### Weapon section — six offers

A typical rotation may contain:

- two affordable common weapons near the player's level band;
- one less common weapon family;
- one higher-price specialised weapon;
- one blueprint or crafted-quality weapon offer;
- one rare wildcard or pinned slot.

### Gear section — six offers

A typical rotation may contain:

- one offer for each of the four armour slots;
- one alternative armour build item;
- one augment, material or blueprint offer after those systems unlock.

The shop can use the same twelve visible cells even when later categories are locked. Locked cells should explain the unlock condition rather than pretending stock is empty.

## Exact purchase flow

```text
select offer
-> inspect exact item and price
-> compare against equipped item where relevant
-> confirm purchase
-> debit money exactly once
-> add exact instance to selected character holdings
-> persist transaction
-> mark offer sold or replace it according to shop policy
```

Repeated confirmation or reconnect must not create duplicate items or charge twice.

## Working level unlock ladder

This is a starting design for discussion around an initial level cap near 65.

| Level | Working unlock |
|---:|---|
| 1 | Shop available with common MK1 weapons and basic starter equipment. |
| 5 | Sell owned exact equipment from inventory/shop interfaces. |
| 10 | Full four-slot armour offers begin appearing; Rare offers can appear at low frequency. |
| 15 | Basic weapon and gear blueprints become purchasable; basic crafting unlocks. |
| 20 | Basic augment offers and augment-installation services unlock. |
| 25 | MK2 weapon offers and more specialised status/delivery families enter the pool. |
| 30 | Advanced crafting recipes and targeted material bundles unlock. |
| 35 | Epic offers can appear; one shop offer may be pinned across rotations. |
| 40 | Higher difficulty and mode rewards can inject special shop stock. |
| 45 | Advanced augments and limited augment extraction/removal services unlock. |
| 50 | Legendary offers become possible at low frequency; expensive targeted blueprints appear. |
| 55 | Endgame material bundles and overclock-preparation services appear. |
| 60 | Overclock cores or signature-overclock recipes may enter special stock after their system is implemented. |
| 65 | Initial-cap milestone shop with capstone blueprints, challenge stock and prestige cosmetics/convenience rewards. |

When the cap expands toward 100, later tiers should add content and specialisation rather than invalidating everything bought before level 65.

## Offer eligibility

A shop offer may consider:

- account or character level;
- completed campaign milestones;
- unlocked blueprints;
- current difficulty clears;
- weapon-family discovery;
- mode-specific reputation or milestone flags;
- initial release cap.

Eligibility should be data-defined. The shop controller should not contain a switch statement for every weapon or level.

## Rotation proposal

A predictable model is preferable to a purely opaque timer.

Possible working model:

- stock is derived from an account seed and rotation index;
- rotation advances after a defined number of completed missions or at a known daily boundary;
- the player can see when the next rotation occurs;
- one offer can be pinned after the relevant unlock;
- buying an offer marks that exact offer sold for the current rotation;
- closing/reopening or changing characters does not reroll stock.

The exact cadence is open. Offline players should not be punished by missing mandatory power items.

## Pinning

### Working proposal

- One pinned offer remains across ordinary rotations.
- Pinning is free when first unlocked, or costs a modest amount of money—not premium currency.
- The pinned item's price and exact identity remain unchanged.
- A purchased pin slot becomes empty; it does not duplicate the purchased item.
- Additional pin capacity, if ever added, should be convenience rather than combat advantage.

## Pricing principles

- Common gear should be affordable enough to repair an unlucky progression gap.
- Specialised weapons cost more because of targeting, not because the shop sells superior hidden stats.
- Prices scale by content tier and level band, but early prices should not outrun early earnings.
- Selling an item always returns less money than buying the same category.
- Shop arbitrage between buying, selling and dismantling must not generate infinite currency.
- Exact prices should be visible before confirmation.

## Strongboxes and the shop

### Working direction

The ordinary shop should focus on equipment, blueprints, augments and materials. Selling strongboxes directly should be treated cautiously because it can undermine mission rewards and turn money into unrestricted loot rerolls.

Possible later exceptions:

- one limited beginner box;
- event or challenge boxes;
- a guaranteed low-tier box with a strict rotation limit;
- keys or services only if boxes later require them.

No paid-real-money loot-box design is implied by this document.

## Shared versus character-specific stock

Preferred working direction:

- the rotation is account-wide and stable;
- purchased exact equipment is granted to the currently selected character;
- the UI clearly displays the receiving character before purchase;
- character switching does not reroll the offers.

A future shared stash can change transfer convenience without changing shop determinism.

## Open decisions

- Exact level numbers in the unlock ladder.
- Whether the shop is available immediately or introduced by an early Hub milestone.
- Exact rotation cadence: missions, real time or a hybrid.
- Whether sold offers remain empty or are replaced at a premium.
- Whether each character sees the same stock at adjusted level bands.
- Whether armour quality and weapon quality use identical offer rules.
- Whether blueprints are bought once account-wide.
- Whether the shop can sell unopened strongboxes at all.
- Whether cosmetics are mixed into this shop or placed in a separate vendor.
