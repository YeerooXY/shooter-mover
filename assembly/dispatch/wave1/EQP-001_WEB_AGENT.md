# EQP-001 — Equipment and augment definitions v1

Use this prompt with one isolated GitHub web/coding agent.

```text
Repository: YeerooXY/shooter-mover
Task: EQP-001
Branch: agent/eqp-001-equipment-augment-definitions
Exact base commit: 0e678a9333956aa29ba2e3598265c8e1a4122e72
PR base: main
Dependencies already merged: ADR-001 through PR #132

Create a fresh branch from the exact base and open a draft PR.

Objective

Define the shared engine-independent equipment/augment model and Unity-authored
definition schemas for weapons and armor. Support configurable item levels,
quality/tier labels, augment slots, augment levels, compatibility, exclusions,
and immutable generated instances without hardcoding the proposed defaults of
three tiers or ten levels.

Read completely before writing

- AGENTS.md
- project_workspace.json
- assembly/context/CURRENT_HANDOFF.json
- docs/architecture/rewards/REWARD_EQUIPMENT_ARCHITECTURE_V1.md
- docs/architecture/contracts/STABLE_ID_V1.md
- docs/architecture/ASSEMBLY_DEPENDENCIES.md
- docs/architecture/FILE_OWNERSHIP.md
- existing Stage 1 weapon package IDs/registry tests, read-only
- the EQP-001 section of
  docs/architecture/REWARD_PROGRESSION_AND_LEVEL_AUTHORING_PLAN.md

Exclusive owned paths

- Assets/ShooterMover/Runtime/Domain/Equipment/**
- Assets/ShooterMover/Runtime/Contracts/Equipment/**
- Assets/ShooterMover/Content/Definitions/Equipment/**
- Assets/ShooterMover/Tests/EditMode/Equipment/**
- docs/architecture/contracts/EQUIPMENT_AUGMENTS_V1.md
- inseparable leaf metadata inside those exact subtrees

Forbidden

- existing weapon package/runtime/registry files
- random generation, holdings/inventory, shops, strongboxes, crafting, upgrades
- production balance catalogs or final production .asset instances
- scenes, prefabs, Stage 1, shared asmdefs
- assembly/context/**, assembly/generated/**, assembly/dispatch/**
- ProjectSettings/** and Packages/**

Required model

1. Stable equipment definition identities and immutable equipment instance
   identities.
2. At least weapon and armor categories without preventing future categories.
3. Definition metadata may reference existing weapon IDs, but must not duplicate
   firing, projectile, cadence, mount, or weapon-package behavior.
4. Configurable item level/rank domain with no architectural hard cap.
5. Configurable quality/tier metadata with no architectural three-tier cap.
6. Configurable augment-slot maximum, including valid zero-slot equipment.
7. Augment definitions with:
   - stable identity;
   - category/family/tag compatibility;
   - exclusion/incompatibility groups;
   - duplicate policy;
   - configurable tier and level ranges;
   - deterministic canonical ordering.
8. Immutable augment instances and immutable generated equipment instances.
9. Validation must reject impossible combinations before later generation or
   holdings code consumes them.
10. Content.Definitions may expose ScriptableObject schemas and conversion into
    immutable values, but mutable generated equipment never lives in assets.

Existing weapon references to prove

- weapon.blaster-machine-gun
- weapon.shotgun
- weapon.rocket-launcher
- weapon.arc-gun
- weapon.ricochet-gun

Required proof

- all five existing weapon IDs can be referenced without package duplication
- armor definitions are valid independently of weapon runtime
- zero, one, and configurable many augment slots
- configured maxima above three tiers and ten levels work
- incompatible category/family/tag combinations reject
- exclusion groups reject impossible pairs
- duplicate augment policy is enforced
- canonical ordering/fingerprint is stable across input order
- immutable instance replacement can represent a future upgrade without
  mutating the original instance
- malformed StableIds, duplicate IDs, invalid ranges, and impossible slot
  contents reject deterministically

Validation

- Add focused EditMode tests.
- Run repository layout and assembly graph validation.
- Run focused Unity EditMode tests and cold compile when available.
- If Unity is unavailable, leave the PR draft and state the exact pending proof.

Acceptance

GEN-001, INV-001, BOX-001, CRA-001, AUG-001, SHOP-001, and SIM-001 can consume
one equipment model. No runtime weapon behavior is rewritten.

PR body

Record task ID, exact base/dependency, changed paths, schema/API summary, tests,
pending proof, limitations, and rollback.

Non-goals

No item generation, inventory, equipping UI, loadout behavior, shop, strongbox,
crafting, upgrade transaction, scene, or production balance data.
```
