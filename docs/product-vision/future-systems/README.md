# Sas5–Robokill future systems design memory

## Purpose

This folder is the long-lived product-design memory for systems that are intentionally **not being implemented yet**.

It exists so that ideas discussed during development are not lost, silently rewritten by later architecture work, or mistaken for finished requirements. When implementation reaches one of these systems, the relevant document should be reviewed with the product owner and converted into a bounded implementation task.

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

1. Read the relevant document and any linked system documents.
2. Separate confirmed direction from working proposals.
3. Resolve the open decisions needed for the smallest visible slice.
4. Write one bounded task with explicit out-of-scope sections.
5. Update the design-memory document when the product decision changes.
6. Record superseded ideas instead of deleting their history without explanation.

## Non-goal

These documents do not define code architecture, save schemas, network protocols or final balance values. They describe the desired player-facing system and the constraints future technical designs must satisfy.
