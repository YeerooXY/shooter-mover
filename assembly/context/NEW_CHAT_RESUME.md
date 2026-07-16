# Resume Shooter Mover Reward/Progression Wave 0

Continue from committed state in `YeerooXY/shooter-mover`. Never write to a
branch whose pull request has merged.

## Required startup

1. Read `AGENTS.md`, `project_workspace.json`, and
   `assembly/context/CURRENT_HANDOFF.json`.
2. Verify current `main` and exact path ownership before writing.
3. Read
   `docs/architecture/REWARD_PROGRESSION_AND_LEVEL_AUTHORING_PLAN.md`.
4. Read the selected prompt under `assembly/dispatch/wave0/` completely.
5. Use a fresh branch and separate worktree for every task.

## Durable state

- PR #110 is merged; instructions to review or merge it are obsolete.
- PR #127 is merged and provides turret tracking plus configurable destroyed
  collision.
- PR #128 is merged at `56a8483` and establishes the current reward,
  progression, economy, level-authoring, simulator, and integration roadmap.
- Wave 0 contains `ADR-001`, `AUD-001`, and `DEMO-001`.
- Wave 1 cannot begin until `ADR-001` merges.
- `DEMO-001` alone owns Stage 1 serialized integration paths during Wave 0.
- Local robot integration commit
  `96d6ce9791f4eee860a385e6c7613f972491a4f6` is not available to GitHub-only
  web agents.
- Stage 2 remains locked behind `GATE-010`.

## Exact next action

Dispatch:

- `ADR-001_WEB_AGENT.md` to one GitHub web agent;
- `AUD-001_WEB_AGENT.md` to a second GitHub web agent;
- `DEMO-001_LOCAL_AGENT.md` to one local/path-capable agent.

All three start from exact base
`56a84838558fdfe67fb97254d832b2dd7cd5c018`.

Do not let `ADR-001` or `AUD-001` edit Unity assets or scenes. Do not let a
GitHub-only agent attempt `DEMO-001`. Do not dispatch Wave 1 before `ADR-001`
merges.
