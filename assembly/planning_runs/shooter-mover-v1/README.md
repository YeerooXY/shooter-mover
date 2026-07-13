# Shooter Mover Planning Run v1

**Run ID:** `shooter-mover-v1`  
**Lifecycle state:** planning review  
**Branch:** `ai/planning-shooter-mover-v1`  
**Requirements source:** merged `main`

## Purpose

Convert the accepted Product Discovery and requirements package into an implementation-ready architecture and repository design. This run defines shared contracts, module and file ownership, UI flows, content boundaries, verification gates, milestone caps, production policies, and agent lanes.

It deliberately does **not** create:

- `task_backlog.json`;
- task batches or stable task IDs;
- implementation branches;
- Unity source code or content assets;
- post-MVP Android, networking, service, or storefront systems.

## Outputs

Machine-readable canonical outputs:

- `assembly/generated/project_spec.json`
- `assembly/generated/repo_plan.json`
- `assembly/generated/agent_prompts.json`
- `assembly/generated/slots_db.json`
- `assembly/generated/planning_runs_index.json`

Human-readable planning trace:

- `ARCHITECTURE.md`
- `MILESTONES.md`
- `CONTENT_PLAN.md`
- `POLICIES.md`
- `TASK_SPLITTER_HANDOFF.md`
- `AMENDMENT_STAGE1_WEAPONS.md`

## Planning resolutions

This run resolves the non-blocking Planning questions by:

- selecting initial Stage 1 and Stage 2 milestone caps;
- freezing an initial formal replay-behavior gate;
- selecting representative weapons, enemies, factory topology, and upgraded-droid attacks;
- defining a provisional Windows hardware matrix;
- defining asset provenance, source backup, release-material recovery, and dependency policies;
- defining a prototype-shortcut register and Stage 2 debt exit gate.

Numeric feel and economy coefficients remain evidence-controlled prototype variables unless specifically frozen for a formal round.

## Stage 1 weapon amendment

The human lead selected an amendment after combat-foundation review. It becomes authoritative when its pull request merges. Stage 1 then proves five deliberately simple weapons, and empowered fire only improves authored numeric stats without changing each weapon's core behavior topology. See `AMENDMENT_STAGE1_WEAPONS.md`.

## Review boundary

The package is a draft until its pull request is reviewed and merged. After merge, a fresh Task Splitter context creates the canonical backlog in a separate PR.
