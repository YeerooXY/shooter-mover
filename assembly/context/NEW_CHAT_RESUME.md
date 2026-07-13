# Resume Shooter Mover Guided Task Split

Continue from committed repository state in `YeerooXY/shooter-mover`. Never continue writing to a branch whose pull request has merged.

## Required startup

1. Read `AGENTS.md`.
2. Read `project_workspace.json`.
3. Read `assembly/context/CURRENT_HANDOFF.json`.
4. Read every path listed in `authoritative_artifacts` and follow the recorded `next_action` exactly.
5. Verify the recorded branch, its pull-request state, and its comparison with current `main` before any write.
6. Verify planning PR #2 is merged into `main`.
7. Read the framework Task Splitter prompt and current task schemas.
8. Read `assembly/generated/task_batch_index.json`.
9. Read every generated batch whose index status is `generated`, `validated`, or `accepted`.

## Current durable state

- Planning PR #2 is merged at `320b7fee260743f0db250a8e14f46ddd8fdf7b24`.
- Recovery PR #4 is merged at `5bded6d0b9b133b3851bb1998ebb776e9356c3b5`; its branch is permanently closed.
- Active continuation branch: `ai/task-split-shooter-mover-v1-continuation-3`; draft PR #6 is open for review.
- The index contains 16 agent-sized batches and 186 predeclared stable task IDs.
- Generated and schema-validated: `unity-foundation` with 11 tasks.
- Generated and schema-validated: `shared-contracts-core` with 12 tasks.
- Generated and schema-validated: `stage1-evidence-harness` with 10 tasks.
- Generated and schema-validated: `movement-thruster` with 12 tasks using `MT-001` through `MT-012`.
- Progress: 4 of 16 batches generated and validated.
- Next batch: `combat-four-mount` with 11 tasks.
- No Unity/game implementation, canonical backlog, or collaboration assignment exists.
- Recorded S1.0 blocker: Foundation, Contract Steward, and Evidence Harness estimates total 10.9 focused lead days against the accepted five-day cap. Do not hide this by cutting contracts, evidence, accessibility, diagnostics, reliability, save safety, or performance.
- S1.1 movement-thruster tasks estimate 6.15 focused lead days against the accepted eight-day cap, leaving 1.85 days for human review and bounded evidence-led iteration.
- Full graph validation remains pending until every planned batch file exists; forward references to predeclared later IDs are not evidence of a complete validated backlog.

## Guided rule

For every remaining batch, use this two-turn review gate:

1. Proposal turn: present the proposed stable task IDs, titles, owner lane and exact dependencies to the human lead, then stop without writing repository files.
2. Creation turn: only after explicit human continuation, create a fresh branch from current `main`, generate exactly that one batch, update the index and all deterministic handoffs atomically, validate, open one draft continuation PR, report it, and stop.

Keep each batch at 10–13 small tasks. Never append commits to a merged branch. After writing an approved batch:

1. validate the batch and every task against current AI Assembly Line contracts;
2. ensure expected IDs match the index;
3. ensure dependencies use existing or predeclared stable IDs;
4. update the index status;
5. update `CURRENT_HANDOFF.json`, this file, and `assembly/context/handoff.md`;
6. open a draft continuation PR against `main`;
7. report the agent name, task list, dependencies, validation and PR;
8. stop.

## Next action

Review and merge draft pull request #6. After it is merged, start a fresh Task Splitter context from current `main`. Its first response must propose `CB-001` through `CB-011` with titles, owner lanes and exact dependencies and must not write files. After explicit human continuation, it may generate only `assembly/generated/task_batches/combat-four-mount.json` on a fresh branch and open its draft continuation PR. Do not jump ahead to weapons, enemies, or later agents.

Stage 2 tasks must remain blocked behind the explicit Stage 1 gate task dependency. Do not build `task_backlog.json`, finalize collaboration state, assign implementation work, or begin Dispatch until all 16 batches are generated and validated.
