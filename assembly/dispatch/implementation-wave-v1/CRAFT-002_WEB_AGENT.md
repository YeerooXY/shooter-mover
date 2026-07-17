# CRAFT-002 — Scrap to inventory and equipment flow

## Objective

Connect the existing deterministic crafting runtime to the visible crafting screen, inventory holdings, and loadout/equip flow.

## Dependencies

CRA-001, SCR-001, INV-001, RAP-001, equipment definitions, and the crafting artwork from the vertical-slice batch.

## Acceptance

- Player selects an authored recipe and sees cost, level gate, output range, and possible augments.
- Successful craft spends scrap exactly once.
- Exactly one new equipment instance is added to inventory.
- Exact retry is a no-op and conflicting transaction identity is rejected.
- Crafted equipment can be equipped through the existing loadout path.
- Strongbox results and crafted items remain separate equipment instances.

## Validation

Focused EditMode transaction tests, PlayMode craft-to-inventory proof, and manual proof of scrap before/after, new instance identity, and equip state.
