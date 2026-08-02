# Legacy armor loadout retirement

## Decision

The old generic fixed-slot armor loadout is retired before the new four-piece gear system is introduced.

The retired path previously treated every non-gun item in the broad `Armor` category as valid for the old head, body, legs, and feet slots. It was also coupled to the fixed gun-slot compatibility projection.

## Current boundary

- Canonical gun ownership remains `GunInventoryState`.
- Canonical equipped gun state remains `LoadoutState`.
- Generic holdings remain available for reward receipts and future non-gun gear ownership.
- The old `InventoryLoadoutState` API survives temporarily as a gun-slot-only compatibility shell for current Inventory callers.
- Every former armor slot is forced empty.
- New commands containing an armor binding reject with `retired-armor-loadout-slot-unsupported`.
- Existing valid legacy snapshots may still provide gun positions for one-time migration, but armor bindings are discarded.
- The existing exact-instance loadout save component remains temporarily as an empty schema placeholder so current account validation and old saves do not require a destructive schema reset.

## Next gear system

The replacement must introduce explicit typed slots for:

- helmet;
- body armor;
- leggings;
- boots.

It must not reuse the retired broad-category slot validation or make the gun compatibility projection authoritative for gear.

## Manual acceptance

1. Open the project in Unity `6000.3.19f1` and confirm zero compile/import errors.
2. Create a fresh character and confirm the retired exact-instance loadout component contains no non-gun bindings.
3. Load a controlled existing save with old armor bindings and confirm canonical gun mounts restore.
4. Save that character and confirm all retired armor bindings are removed.
5. Attempt to submit a non-gun fixed-slot binding and confirm `retired-armor-loadout-slot-unsupported`.
6. Equip, unequip, confirm, save, and reload canonical guns.

## Scope

This change does not add replacement gear content, gear stats, gear loot, gear art resolution, or gear Inventory UI.

## Validation boundary

This change was reviewed statically through the connected repository. Unity compilation, EditMode tests, PlayMode tests, and manual save migration were not executed in this environment.
