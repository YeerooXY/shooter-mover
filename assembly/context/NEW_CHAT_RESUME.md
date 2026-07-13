# Resume Shooter Mover Guided Task Split

Continue from the committed task-split branch in `YeerooXY/shooter-mover`.

## Required startup

1. Read `AGENTS.md`.
2. Read `project_workspace.json`.
3. Read `assembly/context/CURRENT_HANDOFF.json`.
4. Verify branch `ai/task-split-shooter-mover-v1` and its latest commit.
5. Verify planning PR #2 is merged into `main`.
6. Read the framework Task Splitter prompt and current task schemas.
7. Read `assembly/generated/task_batch_index.json`.
8. Read every generated batch whose index status is `generated`, `validated`, or `accepted`.

## Current durable state

- Planning PR #2 is merged at `320b7fee260743f0db250a8e14f46ddd8fdf7b24`.
- Task-split branch: `ai/task-split-shooter-mover-v1`.
- The index contains 16 agent-sized batches and 186 predeclared stable task IDs.
- Generated and schema-validated: `unity-foundation` with 11 tasks.
- Generated and schema-validated: `shared-contracts-core` with 12 tasks.
- Next batch: `stage1-evidence-harness` with 10 tasks.
- No Unity/game implementation, canonical backlog, collaboration assignment, or task-split pull request exists.
- Recorded blocker: Foundation plus Contract Steward estimates total 7.3 focused lead days against the accepted S1.0 five-day cap. Do not hide this by cutting contracts, evidence, accessibility, diagnostics, reliability, save safety, or performance.
- Full graph validation remains pending until every planned batch file exists; forward references to predeclared later IDs are not evidence of a complete validated backlog.

## Guided rule

Generate exactly one next planned agent batch per explicit continuation from the human lead. Keep each batch at 10–13 small tasks. After writing the batch:

1. validate the batch and every task against current AI Assembly Line contracts;
2. ensure expected IDs match the index;
3. ensure dependencies use existing or predeclared stable IDs;
4. update the index status;
5. update `CURRENT_HANDOFF.json` and this file;
6. report the agent name and its task list for review;
7. stop.

## Next action

Generate `assembly/generated/task_batches/stage1-evidence-harness.json` only after the human lead asks to continue. Do not jump ahead to movement or later agents.

Stage 2 tasks must remain blocked behind the explicit Stage 1 gate task dependency. Do not build `task_backlog.json`, finalize collaboration state, or open the task-split PR until all 16 batches are generated and validated.
