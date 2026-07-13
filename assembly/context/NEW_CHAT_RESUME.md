# Resume Shooter Mover Planning Review and Task-Split Handoff

Continue from committed repository state in `YeerooXY/shooter-mover`.

## Required startup sequence

1. Read `AGENTS.md` completely.
2. Read `project_workspace.json`.
3. Read `assembly/context/CURRENT_HANDOFF.json`.
4. Verify the active repository, branch, pull request, head commit, and merge state.
5. Read every path listed in `authoritative_artifacts`.
6. Read the complete active-role prompt at `YeerooXY/ai-assembly-line/prompts/00-planning-agent.md` while the planning PR is open.
7. Follow the exact `next_action` in `CURRENT_HANDOFF.json`.

## Lifecycle checkpoint

- Requirements pull request #1 is merged into `main`.
- Product Discovery remains closed with verified decisions D-040 through D-233.
- D-001 through D-039 remain excluded recovery material.
- D-234 was never selected; the planning package defines a prototype-debt register and Stage 2 exit policy without treating D-234 as accepted Product Discovery.
- Planning run `shooter-mover-v1` lives on `ai/planning-shooter-mover-v1` in draft pull request #2.
- Pull requests remain the approval boundary; planning artifacts are drafts until PR #2 merges.

## Planning package

Canonical machine-readable outputs:

- `assembly/generated/project_spec.json`
- `assembly/generated/repo_plan.json`
- `assembly/generated/agent_prompts.json`
- `assembly/generated/slots_db.json`
- `assembly/generated/planning_runs_index.json`

Human-readable planning trace:

- `assembly/planning_runs/shooter-mover-v1/ARCHITECTURE.md`
- `assembly/planning_runs/shooter-mover-v1/MILESTONES.md`
- `assembly/planning_runs/shooter-mover-v1/CONTENT_PLAN.md`
- `assembly/planning_runs/shooter-mover-v1/POLICIES.md`
- `assembly/planning_runs/shooter-mover-v1/TASK_SPLITTER_HANDOFF.md`

The four required JSON outputs are mirrored under `assembly/planning_runs/shooter-mover-v1/outputs/`.

## Stable planning direction

- One Unity repository with an engine-independent plain-C# domain core and explicit Unity adapters.
- `MissionRunState` is authoritative; loaded rooms and UI are projections that submit validated commands.
- Typed ScriptableObject definitions use stable IDs, generated registries, deterministic review snapshots, and isolated content packages.
- Persistence uses atomic versioned snapshots, rolling backups, and a compact idempotent journal for important transitions.
- Stage 1 has a 43-focused-lead-day or 10-calendar-week hard review cap and proves intrinsic replay desire.
- Stage 2 has a 77-focused-lead-day or 20-calendar-week hard review cap and proves the complete factory plus production pipeline.
- The planned slice contains eight base weapons, five ordinary machine roles, a Foreman elite, a Prototype Overseer climax, and a 24-room four-zone factory.
- Asset provenance, 3-2-1 source recovery, dependency locking, prototype-debt exits, immutable formal artifacts, and a provisional Windows hardware matrix are defined.
- Android, online co-op, remote services, full campaign content, mature item modifiers, and public-release systems remain postponed.

## Review focus

Review PR #2 for blocking inconsistency in:

- authority and module boundaries;
- milestone caps and behavioral pass criteria;
- representative weapon and enemy roster;
- 24-room factory topology;
- hardware and performance targets;
- provenance, backup, dependency, and prototype-debt policy;
- lane ownership and Task Splitter handoff.

Do not reject the plan merely because exact tuning coefficients remain prototype variables. Do not add a canonical task backlog to the planning PR.

## Exact next action

If PR #2 is open, review it and repair only blocking planning issues on `ai/planning-shooter-mover-v1`. Merge only with explicit human approval.

If PR #2 is already merged into `main`, switch to the framework Task Splitter role at `YeerooXY/ai-assembly-line/prompts/06-task-splitter.md`. Create the canonical task batch index, task batches, task backlog, and updated collaboration state in a new branch and separate pull request. Do not start implementation before the task-split PR merges.
