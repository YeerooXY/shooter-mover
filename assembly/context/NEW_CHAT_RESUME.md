# Resume Shooter Mover Reward/Progression Wave 2

Continue from committed state in `YeerooXY/shooter-mover`. Never write to a
branch whose pull request has merged.

## Required startup

1. Read `AGENTS.md`, `project_workspace.json`, and
   `assembly/context/CURRENT_HANDOFF.json`.
2. Verify current `main`, open PR state, exact dependencies, and path ownership.
3. Read `assembly/context/handoff.md`,
   `assembly/dispatch/wave2/VALIDATION.md`, and the selected Wave 2 prompt.
4. Use a fresh branch and separate worktree for every task.

## Durable state

- Wave 0 and Wave 1 are complete.
- Verified Wave 2 dispatch base is
  `6d04451883127dcf597c4f6fec199aeaec2a7f9e`.
- Eight Wave 2 tasks are ready: `MON-001`, `SCR-001`, `INV-001`, `GEN-001`,
  `SRC-001`, `DOOR-001`, `VOID-001`, and `NORM-001`.
- All eight may run in parallel on isolated exact-base branches.
- No Wave 2 task may edit Stage 1 serialized files.
- `INT-001` remains the sole final Stage 1 serialized owner.
- Stage 2 remains locked behind `GATE-010`.

## Exact next action

Dispatch each prompt under `assembly/dispatch/wave2/` to a separate agent.
Keep PRs draft until proof is complete and do not opportunistically repair the
three unrelated PlayMode baseline failures recorded in `VALIDATION.md`.
