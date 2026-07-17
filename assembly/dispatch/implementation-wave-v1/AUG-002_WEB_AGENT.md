# AUG-002 — Interactive session for real augment content and effects

## Objective

Turn the existing immutable augment and upgrade authorities into gameplay-visible content. Create an initial approved augment catalog and project installed augment values into live weapon, armor, movement, and ability stats.

## Interactive protocol

Phase 1: inspect EQP-001, AUG-001, equipment generation, weapon profiles, movement tuning, ability contracts, and inventory. Propose augment families, tiers, levels, stacking/exclusion rules, values, and the exact stat projection boundary.

Stop and wait for explicit approval. Phase 2: implement the approved definitions, projection service, and one or two demo-visible effects.

## Dependencies

AUG-001, the approved weapon-system plan, and the equipment/inventory authorities.

## Acceptance

- Definitions remain authorable; no weapon-name switch statements.
- Upgrading an augment changes the replacement instance deterministically.
- Installed augments produce a canonical effective-stat snapshot.
- Weapon and player runtime consume that snapshot without mutating immutable equipment.
- Compatibility, duplicate, exclusion, tier, and level rules remain enforced.

## Validation

Focused EditMode tests for projection and stacking, upgrade retry tests, and a PlayMode proof showing an installed augment changing a live weapon or player behavior.
