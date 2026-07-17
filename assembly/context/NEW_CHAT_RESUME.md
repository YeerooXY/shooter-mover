# Resume Shooter Mover playable vertical-slice dispatch

Continue from committed state in `YeerooXY/shooter-mover`. Never write to a branch whose pull request has merged.

## Required startup

1. Read `AGENTS.md`, `project_workspace.json`, and `assembly/context/CURRENT_HANDOFF.json`.
2. Verify live `main`, open PR state, exact dependencies, ownership, and `LAUNCH_BASE_POLICY.md`.
3. Read `assembly/context/handoff.md` and `assembly/dispatch/vertical-slice-v1/README.md`.
4. Use one fresh branch/worktree per implementation packet.

## Verified preparation boundary

- Dispatch preparation baseline: `645cf24f30ee6c8762214a84060e59e35df67a05`.
- Asset intake: `0b1b654c1fb8cf8208904eb55041fde954cfb560`.
- Twenty implementation packets exist under `assembly/dispatch/vertical-slice-v1/`.
- Ownership collision audit reports zero overlaps.
- Unity was not run for dispatch preparation; no current-baseline pass is claimed.

## Immediate dependency-safe dispatch

- MENU-002 from MENU-001 head `f0430794fca20cc911561478767eddbecb476f1e`, targeting `agent/menu-001-main-menu-flow`.
- ROOM-001, LEVELDES-001, XP-002, DROP-001, and WEAPON-DATA-001 from exact `645cf24f30ee6c8762214a84060e59e35df67a05`.

## Gates

- Merge proof-complete MENU-001 before HUB-001.
- Merge HUB-001 before CHAR-001, INV-002, PLAY-001, LEVELSEL-002, and hub UI screens.
- Merge WEAPON-DATA-001 before SIM-002.
- RUN-001 is absent; do not dispatch DEV-001 or BOXUI-001 until it separately merges.
- PR #171 SKILL-001 is open/unproven; do not dispatch SKILLUI-001 until a skill authority merges with required proof.
- Deferred tasks use a dispatch-time current-main launch override; never use stale preparation baseline after dependencies merge.
- DEMO-005 starts last and alone owns Stage1VisibleSlice scene/controller.

## Exact next action

Review the coordinator PR and ownership matrix. After merge, dispatch only the six immediate tasks above. Keep implementation PRs draft until named XML result files exist and report passing tests.
