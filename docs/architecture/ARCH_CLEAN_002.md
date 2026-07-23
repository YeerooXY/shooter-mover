# ARCH-CLEAN-002 — retained demo-content cleanup

## Purpose

Remove content that existed only to keep the deleted Stage 1 / Visible Slice demo looking playable, while retaining reusable game architecture for future authored rooms, weapons, enemies, hazards, exits, and Results.

## Removed in this change

- the five hard-coded production starter weapon definitions;
- automatic starter grants for Blaster, Shotgun, Rocket Launcher, and Arc Gun;
- the hard-coded Ricochet reserve identity;
- `DemoCutover` and `Production vertical slice` catalog content;
- `flow-draft-slot-*` instance identities;
- the retained prototype crate, door, explosive prop, floor tile, and complete level-reference sprites with paired Unity metadata.

The existing catalog-facing class remains temporarily as an empty compatibility boundary so account, inventory, shop, strongbox, and Results code can compile without receiving fabricated equipment. The canonical weapon-catalog composition must replace this boundary rather than adding another starter catalog.

## Reusable systems intentionally retained

- room graph and room-runtime contracts;
- generic wall and collision geometry;
- generic door definitions, state, locking, and traversal contracts;
- generic hazard and void-region contracts;
- generic destructible-prop authority and prop-catalog contracts;
- terminal facts, physical pickups, run-local collection, and durable reward transfer;
- mission completion and final-exit authority;
- Results authority, Results scene routing, reward projection, strongbox-opening routing, and return-to-hub flow.

These systems are not Stage 1 content. Future room JSON and authored content should compose them using stable identities and definitions.

## Prohibited replacement patterns

Future work must not restore demo behavior through renamed equivalents:

- no fixed room numbers or Stage 1 coordinates in controllers;
- no GameObject-name or hierarchy-name decisions for crates, hazards, doors, walls, or exits;
- no hard-coded `final room` checks outside mission/room definitions;
- no scene-global discovery used as content registration;
- no C# factory that authors the production weapon catalog;
- no automatic starter equipment until an explicit onboarding/progression policy owns it;
- no Results screen values derived from a specific demo scene or enemy implementation.

## PR #288 integration rule

PR #288 may retain its canonical JSON import, typed projection, deterministic fingerprints, rarity normalization, strongbox weighting, and exact runtime-package lookup.

It must not retain or recreate `ProductionStarterWeaponCatalogV1` as a five-weapon compatibility facade. Production composition should consume the canonical projection directly, and initial character holdings/loadout should remain empty until a dedicated initial-equipment policy is authored.

## Validation boundary

This connector-only environment cannot run Unity compilation or EditMode/PlayMode tests. Required validation before merge:

1. Unity script import and compilation;
2. character creation with empty holdings/loadout;
3. Inventory and Shop empty-state smoke;
4. Results and Strongbox routes with no starter catalog present;
5. missing-GUID/missing-script scan after prototype sprite deletion;
6. rebase or rebuild PR #288 on this architecture boundary.
