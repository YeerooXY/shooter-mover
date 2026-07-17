# ACT-001 — Multiple activated abilities

## Objective

Create a reusable runtime for multiple equipped activated abilities. Keep it separate from `PowerModifier`, which remains empowered weapon fire.

## Interactive protocol

Phase 1: inspect input, player intents, movement, combat, skills, HUD, and effect contracts. Propose the initial slot count, input scheme, cooldown/charge/resource rules, global cooldown policy, targeting model, and one demo ability.

Stop and wait for approval. Phase 2: implement the approved foundation and one working ability.

## Acceptance

- Ability definitions use stable IDs and authorable icon, cooldown, charge, cost, duration, target, and effect references.
- Each equipped slot has independent cooldown/charge state.
- Activation has deterministic command/result statuses and duplicate-operation protection.
- Rejected activations do not consume resources.
- Focus loss safely releases inputs.
- The first demo ability is visible and usable in the combat scene.

## Validation

Focused EditMode tests for cooldowns, charges, costs, duplicates, and invalid targets; PlayMode proof with at least two slots visible and one ability activated.
