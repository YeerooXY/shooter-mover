# GEN-001 - Shared deterministic reward/equipment generator

Use this prompt with one isolated GitHub web/coding agent.

```text
Repository: YeerooXY/shooter-mover
Task: GEN-001
Branch: agent/gen-001-shared-reward-generator
Exact base commit: 6d04451883127dcf597c4f6fec199aeaec2a7f9e
PR base: main
Dependencies merged:
- REW-001 through 06012ea116c1b8bd1f087a5f9275079d5fd882bd
- EQP-001 through 0bac603dc5921ab1da1b89895f725a0b97261fae
- RNG-001 through 46cccb17c057b07a6d408b9aabe286228a921915
- PRG-001 through 6d04451883127dcf597c4f6fec199aeaec2a7f9e

Create a fresh exact-base branch and open a draft PR.

Objective

Implement the one deterministic generator used later by drops, strongboxes,
shops, random crafting, tests, and simulation. Do not create product-specific
generators.

Read AGENTS.md, CURRENT_HANDOFF.json, wave2/VALIDATION.md, reward architecture,
REWARDS_V1, EQUIPMENT_AUGMENTS_V1, DETERMINISTIC_RANDOM_V1,
PROGRESSION_CURVES_V1, PROGRESSION_CONTEXT_V1, and the GEN-001 roadmap section.

Exclusive owned paths

- Assets/ShooterMover/Runtime/Domain/Rewards/Generation/**
- Assets/ShooterMover/Runtime/Application/Rewards/Generation/**
- Assets/ShooterMover/Tests/EditMode/Rewards/Generation/**
- docs/architecture/rewards/GENERATOR_TRACE_FORMAT.md
- inseparable metadata inside those exact subtrees

Required behavior

- pure candidate eligibility from explicit definitions and progression context
- weighted selection, quantity, quality, slot count, augment selection/tier/level
- guarantees, source biases, independent rolls, exclusive groups, explicit no-drop
- consume RNG-001 only through named stable substreams
- adding trace/cosmetic fields cannot shift gameplay results
- prevent incompatible/impossible augments through EQP-001 validation
- deterministic no-eligible/impossible-policy result, never an infinite retry
- complete explainable trace and stable result/content/context fingerprint
- no hard cap of three tiers, ten augment levels, or eleven box tiers

Required proof

- equal inputs produce equal result and trace
- input-order independence and frozen representative vectors
- substream isolation
- eligibility/compatibility filtering and impossible augment prevention
- guaranteed, independent, weighted-exclusive, quantity, and no-drop behavior
- low/high progression contexts and no eligible candidate

Forbidden

No Unity Random/System.Random/time seeds, Unity UI/assets, production catalogs,
wallet/holdings mutation, pickups, boxes, shops, crafting, scenes, or alternate
simulator generator.
```
