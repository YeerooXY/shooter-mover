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
- Batch index: `assembly/generated/task_batch_index.json`.
- The index contains 16 agent-sized batches and 186 predeclared stable task IDs.
- Each agent batch contains 10–13 small, reviewable tasks.
- Generated and validated: `assembly/generated/task_batches/unity-foundation.json` with 11 tasks.
- Next batch: `shared-contracts-core`.
- No Unity/game implementation, canonical backlog, collaboration assignment, or task-split pull request has been created.

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

Generate `assembly/generated/task_batches/shared-contracts-core.json` only after the human lead asks to continue. Do not jump ahead to later agents.

Stage 2 tasks must remain blocked behind the explicit Stage 1 gate task dependency. Do not build `task_backlog.json`, finalize collaboration state, or open the task-split PR until all 16 batches are generated and validated.
