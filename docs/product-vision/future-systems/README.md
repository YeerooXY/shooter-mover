# Sas5–Robokill future systems design memory

## Purpose

This folder is the long-lived product-design memory for systems that are intentionally **not being implemented yet**.

It exists so that ideas discussed during development are not lost, silently rewritten by later architecture work, or mistaken for finished requirements. When implementation reaches one of these systems, the relevant document should be reviewed with the product owner and converted into a bounded implementation task.

## Design-memory routing convention

When the product owner says that they want to **think, brainstorm, refine or design an idea without implementing it yet**, treat the discussion as product-design memory for this folder.

This applies even when the request is informal, for example:

- “I have a skill idea for Juggernaut.”
- “Let us think about a new weapon.”
- “I have an enemy or boss idea.”
- “I want to design a level, room, prop or environmental hazard.”
- “How should this shop, crafting, difficulty, multiplayer or endless-mode idea work?”
- “Do not implement this yet; I only want to develop the idea.”

The expected workflow is:

1. Read this index and the most relevant existing design-memory document.
2. Help the product owner explore the idea before committing to a solution.
3. Identify whether each conclusion is a **Confirmed direction**, **Working proposal** or **Open decision**.
4. Record the developed idea in the relevant Markdown document on a documentation branch or PR.
5. Create a new document in this folder when no existing document is a natural home.
6. Do not modify runtime code, scenes, prefabs, tests or production content unless the product owner separately requests implementation.
7. Preserve superseded ideas with a short note when useful instead of silently erasing the design history.

### Default routing map

| Idea mentioned | Primary design-memory document |
|---|---|
| character skill, class passive, active ability, Juggernaut/Medic/Striker idea | [SKILLS_AND_CLASSES.md](SKILLS_AND_CLASSES.md) |
| weapon augment, modifier, socket, capacity or overclock | [AUGMENTS_AND_OVERCLOCK.md](AUGMENTS_AND_OVERCLOCK.md) |
| enemy, elite, boss, attack pattern, telegraph or encounter combination | [ENEMY_ARCHETYPES.md](ENEMY_ARCHETYPES.md) |
| weapon name, family, firing mechanic, projectile or damage identity | [WEAPON_IDEAS.md](WEAPON_IDEAS.md) |
| crafting recipe, salvage, currency, resource source or economic sink | [CRAFTING_AND_ECONOMY.md](CRAFTING_AND_ECONOMY.md) |
| vendor, shop stock, rotation, purchase or unlock level | [SHOP_AND_UNLOCKS.md](SHOP_AND_UNLOCKS.md) |
| XP curve, level cap, loot box, reward tier or long-term progression | [PROGRESSION_AND_LOOT.md](PROGRESSION_AND_LOOT.md) |
| difficulty tier, modifier, challenge rule or reward scaling | [DIFFICULTY_MODES.md](DIFFICULTY_MODES.md) |
| co-op, raid, lobby, matchmaking, revive, PvP or multiplayer loot | [MULTIPLAYER_MODES.md](MULTIPLAYER_MODES.md) |
| survival, endless descent, holdout, boss rush, extraction or challenge seed | [ENDLESS_MODES.md](ENDLESS_MODES.md) |
| level, room, biome, prop, hazard, interactable or environmental storytelling | create or use a suitable level/world-design document in this folder and link it from this index |

Ideas may affect several systems. Start with the primary document, then update linked documents only when the cross-system decision is important enough to preserve.

### Example

If the product owner says:

> I have a skill idea for Juggernaut.

Interpret it as:

```text
Sas5–Robokill product-design discussion
-> open SKILLS_AND_CLASSES.md
-> review the existing Juggernaut identity and guardrails
-> develop the idea conversationally
-> classify conclusions by certainty
-> write the resulting design memory into that document
-> no implementation unless separately requested
```

## Status language

Every document uses three levels of certainty:

- **Confirmed direction** — a product decision already stated or repeatedly reinforced.
- **Working proposal** — a concrete design that makes the idea discussable, but is not locked.
- **Open decision** — deliberately unresolved and must be decided before implementation.

A working proposal must never be treated as permission to implement the whole document without a dedicated task.

## Project-wide confirmed direction

- The immediate priority is a visible, repeatable game loop rather than accumulating disconnected systems.
- The player selects a persistent character with character-local holdings and an exact loadout.
- The three current class directions are Striker/Assault, Combat Medic/Healer and Juggernaut/Defensive.
- Weapon capacity is limited to four physical positions. Class policy decides how many are currently available.
- The initial armour layout is four pieces: headpiece, mech chest armour, mech leggings and mech boots.
- Strongbox results should feel satisfying without forcing players into reroll exploits.
- Exact item identity matters: rewards, equipment, selling and loadouts operate on concrete instances.
- New content should be data-defined wherever practical. Adding a weapon, enemy or level should not require identity-specific branches in production controllers.
- Early progression should move quickly, then slow down toward endgame.
- Launching with a level cap around 65 and later expanding toward 100 is a preferred release strategy, not yet a locked schedule.
- The first complete product loop remains:

```text
enter level
-> fight enemies
-> obtain strongbox
-> complete mission
-> open strongbox
-> keep, sell or later dismantle the item
-> equip an exact weapon
-> replay and use it
```

## Documents

| Document | Scope |
|---|---|
| [NEXT_VISIBLE_DELIVERY_WORKFLOW.md](NEXT_VISIBLE_DELIVERY_WORKFLOW.md) | Small visible implementation milestones, four-way parallel splits, integration branches and acceptance gates. |
| [SKILLS_AND_CLASSES.md](SKILLS_AND_CLASSES.md) | Class identities, active skills, passive boards and the future Assault third mount. |
| [AUGMENTS_AND_OVERCLOCK.md](AUGMENTS_AND_OVERCLOCK.md) | Weapon augments, augment capacity, overclock cores and anti-reroll principles. |
| [ENEMY_ARCHETYPES.md](ENEMY_ARCHETYPES.md) | Concrete enemy roles, behaviours, telegraphs and extensibility rules. |
| [WEAPON_IDEAS.md](WEAPON_IDEAS.md) | Existing authored families plus the named weapon concepts already discussed. |
| [CRAFTING_AND_ECONOMY.md](CRAFTING_AND_ECONOMY.md) | Money, scrap, crafting, salvage, sinks and deterministic acquisition. |
| [SHOP_AND_UNLOCKS.md](SHOP_AND_UNLOCKS.md) | Shop inventory, rotations, item categories and a working unlock ladder. |
| [PROGRESSION_AND_LOOT.md](PROGRESSION_AND_LOOT.md) | Level pacing, release caps, strongbox philosophy and long-term retention. |
| [DIFFICULTY_MODES.md](DIFFICULTY_MODES.md) | Difficulty tiers, meaningful modifiers and reward scaling. |
| [MULTIPLAYER_MODES.md](MULTIPLAYER_MODES.md) | Co-op, raids, matchmaking, loot ownership and later competitive possibilities. |
| [ENDLESS_MODES.md](ENDLESS_MODES.md) | Survival, endless descent, defence, boss rush and extraction-style risk. |

## How to use this memory

Before implementing one of these systems:

1. Read [NEXT_VISIBLE_DELIVERY_WORKFLOW.md](NEXT_VISIBLE_DELIVERY_WORKFLOW.md) and the relevant design-memory document.
2. Separate confirmed direction from working proposals.
3. Resolve the open decisions needed for the smallest visible slice.
4. Write one bounded task with explicit out-of-scope sections.
5. Update the design-memory document when the product decision changes.
6. Record superseded ideas instead of deleting their history without explanation.

## Non-goal

These documents do not define code architecture, save schemas, network protocols or final balance values. They describe the desired player-facing system and the constraints future technical designs must satisfy.
