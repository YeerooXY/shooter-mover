# Resume Shooter Mover Reward/Progression Wave 1

Continue from committed state in `YeerooXY/shooter-mover`. Never write to a
branch whose pull request has merged.

## Required startup

1. Read `AGENTS.md`, `project_workspace.json`, and
   `assembly/context/CURRENT_HANDOFF.json`.
2. Verify current `main`, open PR state, and exact path ownership before writing.
3. Read the merged ADR-001 documents, AUD-001 audit, and
   `docs/architecture/REWARD_PROGRESSION_AND_LEVEL_AUTHORING_PLAN.md`.
4. Read the selected prompt under `assembly/dispatch/wave1/` completely.
5. Use a fresh branch and separate worktree for every task.

## Durable state

- Wave 0 is complete: PRs #130, #132, and #131 merged.
- Current verified `main` is
  `0e678a9333956aa29ba2e3598265c8e1a4122e72`.
- The complete playable Stage 1 demo, robot, rotating turret, physical shots,
  boost, props, collisions, and restart are merged.
- Wave 1 contains `OBJ-001`, `REW-001`, `EQP-001`, `RNG-001`, `LED-001`, and
  `PRG-001`.
- All six Wave 1 tasks may run in parallel on isolated branches.
- No Wave 1 task may edit Stage 1 or existing gameplay packages.
- `RNG-001` owns `Progression/Curves/**`; `PRG-001` owns
  `Progression/Context/**`.
- Stage 2 remains locked behind `GATE-010`.

## Exact next action

Dispatch all six prompt files under `assembly/dispatch/wave1/` to separate
GitHub web/coding agents. Every branch starts from exact base
`0e678a9333956aa29ba2e3598265c8e1a4122e72`.

Keep PRs draft until proof is complete. If Unity is unavailable to an agent,
the coordinator runs the pending focused tests before merge. Do not dispatch a
Wave 2 consumer until all dependencies named in the roadmap and handoff merge.
