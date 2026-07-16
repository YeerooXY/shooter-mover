# Shooter Mover Reward/Progression Wave 0 Handoff

## Current boundary

PR #110 is merged and its historical visible-slice task materialization is no
longer the active routing boundary.

PR #128 is merged at commit
`56a84838558fdfe67fb97254d832b2dd7cd5c018`. Its authoritative roadmap is:

`docs/architecture/REWARD_PROGRESSION_AND_LEVEL_AUTHORING_PLAN.md`

The repository is now in Wave 0 dispatch for the next architecture phase.

## Verified current state

- Current verified `main` is `56a84838558fdfe67fb97254d832b2dd7cd5c018`.
- PR #110 merged at `0364d9d8f04f6d7f14fc7e17046064e507099bd0`.
- PR #127 merged the turret tracking and destroyed-wreck collision options.
- PR #128 merged the reward/progression/level-authoring roadmap.
- The existing runtime already contains movement, aiming, shooting, visible
  physical projectiles, boosting, camera support, turret behavior, destructible
  props, destruction-animation configuration, and restart behavior.
- The prepared robot integration exists locally at
  `96d6ce9791f4eee860a385e6c7613f972491a4f6`.
- That robot commit is based on `37e3b4d`, changes only the robot asset,
  Stage 1 scene/controller, and focused Stage 1 integration tests, and passed
  the eight focused Stage 1 PlayMode tests before handoff.

## Wave 0 execution shape

Three tasks may begin in parallel from exact base
`56a84838558fdfe67fb97254d832b2dd7cd5c018`:

1. `ADR-001` — freeze architecture, lifecycle, scene ownership, identity,
   reward claim, inventory, ledger, progression-context, and simulator-sharing
   decisions.
2. `AUD-001` — perform a read-only evidence-backed audit of existing enemies,
   props, scene integration, identity, restart, damage, and reward readiness.
3. `DEMO-001` — publish one immediate playable Stage 1 baseline containing the
   robot, movement, shooting, boosting, camera, turret, props, collisions,
   destruction hooks, and restart.

`ADR-001` and `AUD-001` are safe for separate GitHub web agents.

`DEMO-001` must use a local/path-capable agent because its required robot commit
is not published on the remote. Do not ask a GitHub-only agent to invent or
recreate the unavailable local diff blindly.

## Serialized ownership

- `DEMO-001` is the only Wave 0 task allowed to edit the Stage 1 scene,
  Stage 1 controller, focused integration tests, and robot asset paths.
- `ADR-001` owns only its exact architecture documents.
- `AUD-001` owns only its exact audit document and reads implementation paths
  without modifying them.
- After `DEMO-001` merges, it releases Stage 1 ownership.
- The later `INT-001` task becomes the sole final Stage 1 integration owner.

## Dispatch artifacts

Use these prompts without silently broadening their scopes:

- `assembly/dispatch/wave0/ADR-001_WEB_AGENT.md`
- `assembly/dispatch/wave0/AUD-001_WEB_AGENT.md`
- `assembly/dispatch/wave0/DEMO-001_LOCAL_AGENT.md`

Every task uses a fresh branch/worktree and must record its exact base commit in
the PR description.

## Merge and continuation rule

- `ADR-001`, `AUD-001`, and `DEMO-001` are independently mergeable.
- Wave 1 is blocked until `ADR-001` merges.
- After `ADR-001` merges, replace every `BASE_AFTER_DEPENDENCIES` placeholder
  in Wave 1 packets with the exact current `main` SHA.
- Do not dispatch broad reward/economy implementation directly from the roadmap
  before that architecture lock.
- Stage 2 remains locked behind `GATE-010`.

## Exact next action

Dispatch the three Wave 0 prompts to three isolated agents:

- two GitHub web agents for `ADR-001` and `AUD-001`;
- one local/path-capable agent for `DEMO-001`.

Review and merge `ADR-001` before opening Wave 1.
