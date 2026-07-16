# Balance Simulator V1 (SIM-001)

## Purpose

SIM-001 is a maintained Unity Editor balance surface for deterministic single-open inspection and bulk simulation. Open it from **Tools > Shooter Mover > Balance Simulator**.

The simulator deliberately does not contain an alternative reward algorithm. It composes the production deterministic generator and the existing strongbox, shop-pricing, crafting-unlock, and augment-upgrade policies, then aggregates immutable observations.

## Inputs

- character level;
- strongbox tier and strongbox level;
- shop level;
- deterministic unsigned 64-bit seed;
- number of simulations;
- single-open or batch mode;
- optional starting money and scrap balances for affordability projections.

Single-open mode always executes exactly one deterministic iteration. Batch mode derives an isolated named substream seed for every iteration through `DeterministicRandom` version 1.

## Outputs

The report includes:

- reward-type distribution;
- weapon and armor definition frequencies;
- duplicate-definition count and frequency;
- total and unique equipment-instance counts;
- item-level and quality distributions;
- augment count, tier, and level distributions;
- money and scrap deltas plus required shop/crafting/upgrade totals;
- soft-eligible candidates and crafting unlock range;
- rejected or impossible rolls grouped by system and stable code;
- a deterministic report fingerprint.

Definition frequency and instance identity are intentionally separate. Two strongboxes may select the same weapon definition, while every generated equipment object retains a different `EquipmentInstance.InstanceId`.

## Runtime reuse boundary

`BalanceSimulationServiceV1` only derives iteration substreams and aggregates observations. `RuntimeBalanceScenarioV1` is the reference editor composition and calls:

- `RewardGenerationServiceV1.GenerateEquipment` for generated equipment;
- `StrongboxEquipmentGenerationResolverV1` and `StrongboxPowerBudgetPolicyV1` for strongbox equipment;
- `ShopPricingPolicyV1` for shop prices;
- `CraftingRecipeV1.ResolveUnlockLevel` and the recipe's generator policy for crafting;
- `AugmentUpgradeCostPolicyV1.TryCalculateCost` for upgrade affordability.

A project-specific composition can implement `IBalanceSimulationRuntimeV1` and feed authored catalogs and policies into the same aggregation/report layer. This keeps production definitions outside editor code and prevents simulator-only probability behavior.

## Determinism and tests

EditMode tests cover:

- exact report equality for equal inputs;
- single-open iteration count;
- separate instance IDs for duplicate strongbox definitions;
- batch duplicate-definition aggregation without collapsing instances;
- soft-level requirement visibility;
- deterministic rejected/impossible-roll aggregation.

Large generated reports are displayed in the EditorWindow and are not committed.

## Scope

SIM-001 adds no scene, controller, prefab, or ProjectSettings changes. All Unity code is isolated in an Editor-only assembly under `Assets/ShooterMover/Editor/BalanceSimulator`.
