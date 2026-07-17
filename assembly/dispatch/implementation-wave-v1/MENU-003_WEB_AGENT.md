# MENU-003 — Expanded menu route and artwork integration

## Objective

Bind the newly supplied main-menu, character, class, mode, level, map, shop, skills, crafting, results, settings, toolbar, and back-button artwork into the route flow.

## Dependencies

MENU-002, ASSET-INTAKE-002, and the route/profile authorities from the vertical-slice coordinator.

## Acceptance

- Main Menu → Character/Class → Hub → Shop/Skills/Crafting/Inventory/Play is navigable.
- Play → Solo/Multiplayer → Level Selection → Level 1/Level 2 is navigable.
- Back buttons and toolbar navigation are consistent.
- The art remains presentation-only and does not replace domain authorities.

## Validation

Unity compile, focused PlayMode route tests, and manual screenshots of every route transition.
