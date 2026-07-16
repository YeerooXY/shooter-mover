# PRG-001 — Progression context provider

Use this prompt with one isolated GitHub web/coding agent.

```text
Repository: YeerooXY/shooter-mover
Task: PRG-001
Branch: agent/prg-001-progression-context
Exact base commit: 0e678a9333956aa29ba2e3598265c8e1a4122e72
PR base: main
Dependencies already merged: ADR-001 through PR #132

Create a fresh branch from the exact base and open a draft PR.

Objective

Define the one explicit immutable character/region/difficulty progression
context and provider boundary used later by generation, drops, strongboxes,
shops, crafting, upgrades, gameplay, tests, and simulation. Supply simple
authored/session and direct providers suitable before an XP system exists.

Read completely before writing

- AGENTS.md
- project_workspace.json
- assembly/context/CURRENT_HANDOFF.json
- docs/architecture/rewards/REWARD_EQUIPMENT_ARCHITECTURE_V1.md
- docs/architecture/contracts/STABLE_ID_V1.md
- docs/architecture/ASSEMBLY_DEPENDENCIES.md
- docs/architecture/FILE_OWNERSHIP.md
- the PRG-001 section of
  docs/architecture/REWARD_PROGRESSION_AND_LEVEL_AUTHORING_PLAN.md

Exclusive owned paths

- Assets/ShooterMover/Runtime/Domain/Progression/Context/**
- Assets/ShooterMover/Runtime/Contracts/Progression/Context/**
- Assets/ShooterMover/Runtime/Application/Progression/Context/**
- Assets/ShooterMover/Tests/EditMode/Progression/Context/**
- docs/architecture/contracts/PROGRESSION_CONTEXT_V1.md
- inseparable leaf metadata inside those exact subtrees

Critical ownership exclusion

RNG-001 owns random and `Progression/Curves/**` paths. Do not implement
eligibility curves, quality mathematics, crafting delays, PRNGs, or substreams.

Forbidden

- XP gain, leveling authority, player stats, save backend, UI, scenes, Unity
  player lookup, static singleton, balance curves, generator/product logic
- shared asmdefs, assembly/context/**, assembly/generated/**, assembly/dispatch/**
- ProjectSettings/** and Packages/**

Required behavior

1. Immutable ProgressionContext containing at least:
   - character level;
   - region level;
   - difficulty identity/value;
   - optional canonical progression tags.
2. Validation is explicit and engine-independent.
3. Do not impose a game-wide maximum level.
4. Tags are canonical, duplicate-free, and deterministically ordered.
5. Snapshot/equality/fingerprint behavior is deterministic and culture-free.
6. IProgressionContextProvider exposes read-only current context without global
   discovery.
7. Application provides:
   - a fixed/direct provider suitable for simulator/tests;
   - a mutable session provider that accepts explicit validated replacement and
     exposes immutable snapshots/change facts;
   - no XP or automatic level calculation.
8. Invalid replacement leaves the previous session context unchanged.
9. Consumers can receive the context directly or through the provider port.
10. No context value is inferred from scene objects, HUD text, player names,
    static fields, clock, or random state.

Required proof

- valid low and very high levels
- invalid negative/out-of-domain values reject
- tags deduplicate/order canonically or reject according to the documented rule
- equality/fingerprint independent of input tag order
- direct provider returns the exact immutable context
- session provider replacement increments deterministic sequence/change fact
- invalid replacement does not mutate current state
- exact duplicate replacement has documented no-change behavior
- no Unity/global lookup and no RNG curve dependency

Validation

- Add focused EditMode tests.
- Run repository layout and assembly graph validation.
- Run focused Unity EditMode tests and cold compile when available.
- If Unity is unavailable, leave the PR draft and state the exact pending proof.

Acceptance

GEN-001 and all later product/simulation services can consume one context model
without discovering level from gameplay presentation or inventing a simulator-
only substitute.

PR body

Record task ID, exact base/dependency, changed paths, public API summary, tests,
pending proof, limitations, and rollback.

Non-goals

No XP, leveling UI, persistence, player lookup, balance curves, random service,
generator, scene, or production progression data.
```
