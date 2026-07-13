# Shooter Mover Planning Handoff

## Current lifecycle phase

**Planning and architecture review.**

Requirements pull request #1 is merged. Planning run `shooter-mover-v1` is committed on `ai/planning-shooter-mover-v1` and open as draft pull request #2.

The planning artifacts remain proposals until PR #2 is reviewed and merged.

## Source of truth

Requirements foundation:

- `assembly/intake/project_intake.json`
- `assembly/intake/PROJECT_DNA.md`
- `assembly/requirements/REQUIREMENTS.md`
- `assembly/requirements/REQUIREMENTS_REVIEW_CLARIFICATIONS.md`
- verified decision logs under `assembly/intake/`

Planning package:

- `assembly/generated/project_spec.json`
- `assembly/generated/repo_plan.json`
- `assembly/generated/agent_prompts.json`
- `assembly/generated/slots_db.json`
- `assembly/generated/planning_runs_index.json`
- planning trace under `assembly/planning_runs/shooter-mover-v1/`

## Architecture summary

- One Unity product repository.
- Engine-independent plain-C# domain core for important rules and durable state.
- Explicit application services and Unity adapters for input, 2D physics, scenes, rendering, audio, and platform integration.
- One authoritative `MissionRunState`; rooms and UI are projections rather than state owners.
- Typed ScriptableObject definitions, stable IDs, generated registries, deterministic review snapshots, and isolated content packages.
- Atomic versioned snapshots plus a compact idempotent recovery journal.
- Local-only MVP services for saves, content lookup, diagnostics, and artifact identity; no remote backend.

## Proof plan

Stage 1 proves intrinsic movement/combat quality and voluntary replay desire through a benchmark arena, short route, six representative weapons, three ordinary enemies, one elite, technical reliability, developer behavior, and a formal 6–10-player external round.

Stage 2 proves the complete factory mission and production system through saves/recovery, checkpoints, banking, loot risk, one stable-per-run shop, mission-only refresh tokens, deterministic strongboxes, completion/replay, accessibility, diagnostics, performance, art-pipeline proof, and isolated content reproduction.

Hard review caps:

- Stage 1: 43 focused lead days or 10 calendar weeks.
- Stage 2: 77 focused lead days or 20 calendar weeks.

## Representative content

- Eight base weapons: autocannon, heavy cannon, scatter array, thermal beam, coil lance, micro-missile rack, arc projector, and slag mortar.
- Five ordinary machine roles: close pursuer, ranged gunner, area-denial mortar, heavy blocker, and mobile interceptor.
- One Foreman elite.
- One Prototype Overseer upgraded-droid climax.
- Twenty-four meaningful rooms across Receiving, Assembly, Test, and Core zones.
- Four teleports, one shop-enabled teleport, two secure-storage rooms, and six optional rooms.

Working names and stable IDs are planning identifiers, not final marketing copy.

## Operational policies

The plan defines:

- Blender/Krita-centered offline art workflow with exact versions pinned before production use;
- release-bound asset provenance records;
- 3-2-1 source-asset recovery and milestone restore drills;
- secrets/signing material outside the repository;
- a prototype-shortcut register and non-negotiable Stage 2 debt exit conditions;
- pinned dependencies and controlled upgrades;
- provisional primary and minimum Windows hardware profiles;
- immutable build identity and performance evidence.

## Validation completed

- `project_spec.json` validates against the framework project-spec contract.
- `repo_plan.json` validates against the repository-plan contract.
- `agent_prompts.json` validates against the agent-prompt contract.
- every slot record validates against the slot contract.
- canonical outputs are mirrored in the planning-run outputs directory.
- no task backlog, task batch, stable task ID, or implementation file was created.

## Human approval action

Review draft pull request #2. Focus on architecture, milestone caps, evidence criteria, content selection, topology, hardware targets, policies, and lane ownership.

Merge only with explicit human approval.

## Next stage after merge

Start a fresh **Task Splitter** context from merged `main` using `assembly/planning_runs/shooter-mover-v1/TASK_SPLITTER_HANDOFF.md` and the framework Task Splitter prompt.

The Task Splitter must create the canonical backlog in a separate task-split pull request. Implementation and Dispatch remain blocked until that pull request is reviewed and merged.

## Blocking issues

No unresolved product-discovery question blocks planning review.

The lifecycle gates are:

1. merge planning PR #2;
2. create and merge a separate task-split PR;
3. only then begin implementation waves.
