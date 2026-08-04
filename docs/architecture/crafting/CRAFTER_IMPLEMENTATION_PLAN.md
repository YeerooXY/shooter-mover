# Crafter implementation plan

Status: approved current direction as of 2026-08-04.

This document is the source of truth for the new player-facing Crafter. It supersedes the former CRA-001 crafting direction for future implementation. The old `docs/architecture/rewards/CRAFTING_V1.md` document is retained only as historical context and must not be used to restore its former random generation, replay, policy, or delayed-discovery machinery.

## Goal

Build a small, data-driven Crafter where the player:

1. selects `MK I`, `MK II`, or `MK III`;
2. selects a category such as `Shotguns`;
3. sees only items explicitly listed in that category's crafting JSON;
4. opens an item to see its canonical art, description, and stats plus its crafting requirements;
5. crafts one exact owned item through the existing character, inventory, and resource systems.

Strongboxes remain the random-loot path. The Crafter provides deterministic, player-chosen progress.

## Locked design decisions

- `MK I`, `MK II`, and `MK III` are the real game concepts. There is no Standard/Red/Black translation layer.
- Crafting categories are curated in crafting JSON. Runtime code does not infer a category from projectile count, spread, fire mode, folder name, or other weapon behavior.
- An item is craftable only when it appears in crafting content. No separate `craftable` flag is required.
- Weapon and armor definitions remain the sole owners of names, descriptions, art, stats, and gameplay behavior.
- Crafting content owns category placement, available Marks, unlock level, costs, and crafted-instance defaults.
- Crafted weapons are level 10 with three augment slots.
- Crafted armor is level 10 with two augment slots.
- A missing Mark recipe means that item is absent for that Mark.
- A present but level-locked recipe remains visible and explains when it unlocks.
- Each successful craft creates a new unique owned-item instance.
- Crafting must not multiply or rewrite canonical weapon or armor stats.

## Game-facing names

Inside `ShooterMover.Domain.Crafting`:

```text
AllItems
Category
CraftableItem
Recipe
Cost
Mark
```

Application names:

```text
ItemsLoader
Crafter
```

UI names:

```text
CrafterMenu
ItemCard
ItemDetails
```

The namespace supplies the crafting context, so names such as `CraftingRecipe`, `CraftingCategoryDefinition`, `CrafterCatalog`, `CraftingService`, `Policy`, and `Runtime` are deliberately avoided.

## Data relationship

```text
AllItems
└── Category
    └── CraftableItem
        └── Recipe
            ├── Mark
            └── Cost
```

### AllItems

Owns the complete loaded crafting content and its visible categories.

### Category

Represents one visible Crafter category such as Shotguns, Pistols, Helmets, or Boots. It owns:

- category ID and display name;
- display order;
- default crafted level;
- default augment-slot count;
- explicitly listed craftable items.

### CraftableItem

Groups the recipes for one item family shown as one card in the Crafter.

### Recipe

Represents one exact craft action for one exact Mark. It owns:

- the canonical item-definition ID;
- Mark;
- unlock level;
- required costs.

### Cost

Represents one required resource and amount. It must reuse an existing resource rather than create a separate crafting wallet.

### Mark

The only first-version values are:

```text
Mk1 → MK I
Mk2 → MK II
Mk3 → MK III
```

## Content layout

```text
Content/Crafting/
├── Weapons/
│   ├── assault-rifles.json
│   ├── shotguns.json
│   ├── pistols.json
│   ├── smgs.json
│   ├── sniper-rifles.json
│   ├── launchers.json
│   └── special-weapons.json
└── Armor/
    ├── headpieces.json
    ├── body-armor.json
    ├── legs.json
    └── boots.json
```

Only add a category file when it contains at least one intended item. Do not create empty placeholder files.

## Example JSON

```json
{
  "id": "shotguns",
  "name": "Shotguns",
  "order": 30,
  "createdLevel": 10,
  "augmentSlots": 3,
  "items": [
    {
      "id": "rattler",
      "order": 10,
      "recipes": [
        {
          "itemId": "rattler.mk1",
          "mark": "mk1",
          "unlockLevel": 5,
          "costs": [
            {
              "resourceId": "scrap",
              "amount": 50
            }
          ]
        }
      ]
    }
  ]
}
```

No weapon damage, fire rate, projectile behavior, artwork, or description is copied into this file.

## Loading direction

Crafting content follows the existing authored-content pattern:

```text
Content/Crafting/*.json
        ↓
validation/export
        ↓
build-included generated payload
        ↓
ItemsLoader
        ↓
AllItems
```

Domain and Application code must not read the repository filesystem at runtime.

## Validation direction

Later loading work rejects:

- duplicate category IDs;
- duplicate item IDs inside a category;
- duplicate Mark recipes for one item;
- duplicate resource costs in one recipe;
- missing canonical item definitions;
- Mark values that disagree with the resolved item definition;
- unknown resources;
- zero or negative costs;
- negative unlock levels;
- unknown Mark values;
- weapon/armor category mismatches;
- unsupported crafted level or augment-slot counts.

Invalid content fails clearly. It must not silently disappear from the Crafter.

## Craft action direction

The eventual `Crafter` performs one atomic game action:

1. resolve the selected character;
2. resolve the exact recipe and canonical item definition;
3. verify unlock level, resources, and inventory capacity;
4. create a unique owned-item instance;
5. apply level 10 and the category's augment-slot count;
6. spend the required resources;
7. add the item to the existing inventory/holdings;
8. save once;
9. return the created item to the menu.

All validation happens before mutation. The player must never receive partial outcomes such as spent resources without an item or an item without its cost.

## UI direction

The first screen contains:

- current money and scrap;
- top Mark selector: MK I / MK II / MK III;
- category selector;
- filtered item grid;
- selected-item details;
- unlock and cost requirements;
- exact crafted result;
- one Craft button with a readable disabled reason;
- Back navigation.

Missing recipes are hidden for the selected Mark. Locked recipes remain visible and inspectable.

## Review-sized PR sequence

Automated tests are deliberately deferred for now. Each PR still includes a small manual acceptance checklist.

### PR 1 — Crafting content model

Estimated handwritten size: 200–300 lines.

- add `AllItems`, `Category`, `CraftableItem`, `Recipe`, `Cost`, and `Mark`;
- add one tiny Shotguns JSON example;
- no loader, canonical item resolution, inventory mutation, UI, or tests.

Review question: are the concepts and names correct and small?

### PR 2 — Load and validate crafting content

Estimated size: 300–450 lines.

- add `ItemsLoader`;
- construct immutable game data from the generated payload;
- reject malformed, duplicate, or unsupported content;
- preserve authored display order.

Review question: does authored JSON become clean, deterministic game data?

### PR 3 — Connect recipes to canonical weapons

Estimated size: 200–350 lines.

- resolve exact recipe item IDs through the existing weapon authority;
- verify Mark and item kind;
- prove no second weapon source or copied combat stats exist.

Review question: does crafting reuse the canonical weapon definitions correctly?

### PR 4 — Add crafting availability

Estimated size: 200–350 lines.

- add read-only `Crafter` availability checks;
- check character level, resource balances, inventory capacity, recipe, and item existence;
- return direct player-facing failure reasons;
- perform no mutation.

Review question: can the game explain exactly why crafting is allowed or blocked?

### PR 5 — Craft and save the item

Estimated size: 350–600 lines.

- create a unique owned instance;
- apply level and augment slots;
- spend resources once;
- add the item once;
- persist once;
- keep failure atomic.

Split into creation and persistence PRs if this exceeds roughly 600 meaningful lines.

Review question: does one craft produce one correctly owned item and spend its cost exactly once?

### PR 6 — Add the real Crafter destination

Estimated size: 120–220 lines.

- add the screen shell;
- show resources, loading/error state, and Back;
- connect Hub → Crafter → Hub without circular UI dependencies.

### PR 7 — Add browsing

Estimated size: 300–500 lines.

- add Mark selection;
- add category selection;
- add filtered item cards and locked presentation;
- remain read-only.

### PR 8 — Add details and Craft button

Estimated size: 350–550 lines.

- show canonical item information and recipe requirements;
- call the already implemented `Crafter`;
- refresh resources and show success/failure feedback.

### PR 9+ — Add real content and polish

Keep category expansion and balancing separate from implementation:

- shotgun recipes;
- assault-rifle recipes;
- pistol recipes;
- specialist weapons;
- armor crafting;
- balance changes;
- final art, sound, and navigation polish.

## First-version exclusions

Do not add these during the initial Crafter implementation:

- dismantling;
- augment installation or rerolling;
- item upgrading;
- recipe discovery drops;
- crafting timers;
- random crafting results;
- a crafting-only item source;
- a crafting-only resource wallet;
- runtime weapon-stat scaling;
- one runtime class per craftable item;
- restored CRA-001 replay, policy, random-generation, activation-curve, or obsolescence machinery.

## Manual acceptance approach

Until automated tests are resumed, every PR must state the smallest manual checks needed for that slice. For the complete loop, the final smoke check is:

1. load a real character;
2. open the Crafter from the Hub;
3. switch Marks and categories;
4. inspect a locked and an unlocked recipe;
5. craft one item;
6. confirm resources decrease once;
7. confirm one unique item appears in Inventory;
8. restart and confirm both changes persist;
9. craft the same recipe again and confirm a second independent instance is created.
