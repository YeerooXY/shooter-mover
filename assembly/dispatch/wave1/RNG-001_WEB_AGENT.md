# RNG-001 — Deterministic randomness and soft progression

Use this prompt with one isolated GitHub web/coding agent.

```text
Repository: YeerooXY/shooter-mover
Task: RNG-001
Branch: agent/rng-001-deterministic-progression-curves
Exact base commit: 0e678a9333956aa29ba2e3598265c8e1a4122e72
PR base: main
Dependencies already merged: ADR-001 through PR #132

Create a fresh branch from the exact base and open a draft PR.

Objective

Implement one versioned deterministic integer PRNG with isolated named
substreams, plus pure soft-progression mathematics for item eligibility,
quality availability, old-item retention/decay, source bias, and delayed
crafting availability. Equal inputs must produce equal output in gameplay,
tests, and the future simulator.

Read completely before writing

- AGENTS.md
- project_workspace.json
- assembly/context/CURRENT_HANDOFF.json
- docs/architecture/rewards/REWARD_EQUIPMENT_ARCHITECTURE_V1.md
- docs/architecture/contracts/STABLE_ID_V1.md
- docs/architecture/ASSEMBLY_DEPENDENCIES.md
- docs/architecture/FILE_OWNERSHIP.md
- the RNG-001 section of
  docs/architecture/REWARD_PROGRESSION_AND_LEVEL_AUTHORING_PLAN.md

Exclusive owned paths

- Assets/ShooterMover/Runtime/Domain/Common/Random/**
- Assets/ShooterMover/Runtime/Domain/Progression/Curves/**
- Assets/ShooterMover/Runtime/Contracts/Progression/Curves/**
- Assets/ShooterMover/Tests/EditMode/Progression/Random/**
- Assets/ShooterMover/Tests/EditMode/Progression/Curves/**
- docs/architecture/contracts/DETERMINISTIC_RANDOM_V1.md
- docs/architecture/contracts/PROGRESSION_CURVES_V1.md
- inseparable leaf metadata inside those exact subtrees

Critical ownership exclusion

PRG-001 exclusively owns every `Progression/Context/**` path. Do not create,
edit, move, or reference a competing context/provider implementation.

Forbidden

- UnityEngine.Random, ambient System.Random, time/frame-based seeds
- generation, strongbox, shop, crafting, upgrade, wallet, holdings, or scene code
- production balance values or Unity AnimationCurve assets
- shared asmdefs, assembly/context/**, assembly/generated/**, assembly/dispatch/**
- ProjectSettings/** and Packages/**

Required random behavior

1. Document and implement algorithm version 1 with explicit unsigned integer
   overflow semantics. Prefer a compact, well-specified integer algorithm such
   as SplitMix64 and record frozen vectors.
2. Every random state is explicit and immutable/value-oriented where practical.
3. Bounded integer sampling avoids modulo bias.
4. Unit interval/fixed probability sampling has documented exact semantics.
5. Named substreams derive from root seed, algorithm version, stable purpose ID,
   and ordinal without consuming the parent stream.
6. Adding an unrelated/cosmetic substream cannot shift eligibility, quality,
   augment, scrap, shop, or crafting streams.
7. Trace/fingerprint observation consumes no additional random values.
8. Invalid ranges and unsupported algorithm versions fail closed.

Required curve behavior

Pure parameterized functions must support:

- a non-zero configurable early tail before nominal activation;
- a smooth activation region rather than a hard unlock;
- continued availability above activation;
- decay for older items;
- a configurable minimum retention floor;
- quality/tier availability growth;
- source/box bias;
- a crafting availability delay above natural item activation;
- no built-in maximum character level, item level, tier count, or augment level.

Do not choose production tuning. Tests may use named fixture parameters.

Required proof

- frozen v1 random vectors
- identical seed/version/substream inputs produce identical sequences
- different named substreams are isolated
- adding/reading another stream does not shift existing streams
- bounded sampling stays inside range and covers representative values
- invalid ranges/versions reject
- canonical trace/fingerprint is deterministic
- item has a configurable but non-zero chance below nominal activation
- activation transitions smoothly
- old items retain the configured floor
- high source bias changes weight without creating a hard gate
- crafting availability occurs later than natural availability
- very high levels remain valid
- no dependency on Unity, PRG-001 Context, or future generator code

Validation

- Add focused EditMode tests.
- Run repository layout and assembly graph validation.
- Run focused Unity EditMode tests and cold compile when available.
- If Unity is unavailable, leave the PR draft and state the exact pending proof.

Acceptance

GEN-001 and later simulation/product services can consume one deterministic
algorithm and one soft-progression curve family without copying mathematics.

PR body

Record task ID, exact base/dependency, changed paths, algorithm specification,
frozen vectors, tests, pending proof, limitations, and rollback/versioning note.

Non-goals

No production balance, generator, item catalog, strongbox, shop, crafting,
upgrade, Unity curve, scene, or progression-context provider.
```
