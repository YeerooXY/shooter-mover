# Retired CRA-001 crafting direction

Status: historical only.

The former CRA-001 crafting design has been superseded by the current [Crafter implementation plan](../crafting/CRAFTER_IMPLEMENTATION_PLAN.md).

Do not restore the former delayed-discovery, activation-curve, obsolescence, weighted-random quality, deterministic replay, policy, or generated-random-item machinery when implementing the new Crafter.

The current direction is intentionally smaller:

- crafting categories and recipes are curated in crafting JSON;
- the player selects an exact item and Mark;
- canonical weapon and armor definitions remain the only source of item stats and behavior;
- crafting creates one exact owned item at the authored crafted level and augment-slot count;
- existing character resources and inventory are reused;
- Strongboxes remain the random-loot system.

The complete former document remains available in Git history for archaeology, but it is not a current implementation contract.
