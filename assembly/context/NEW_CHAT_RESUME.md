# Resume Shooter Mover Stage 1 Development

Continue from committed repository state in `YeerooXY/shooter-mover`. Never write to a branch whose pull request has merged.

## Required startup

1. Read `AGENTS.md`, `project_workspace.json`, and `assembly/context/CURRENT_HANDOFF.json`.
2. Read every path listed in `authoritative_artifacts`.
3. Verify the recorded branch, pull-request state, and comparison with current `main` before any write.
4. Read the exact task object and its task-run proof.
5. Follow `CURRENT_HANDOFF.json` field `next_action` exactly.

## Durable state

- Stage 1 backlog PR #14 merged at `377fb612a413a6e2af4dff1674374f7ec9ff5710`.
- Stage 1 Dispatch is active.
- `UF-001` was the only initially ready task and is now submitted for review in PR #15.
- The proposed baseline is Unity 6.3 LTS `6000.3.19f1`, changeset `7689f4515d75`.
- The task changes only `ProjectSettings/ProjectVersion.txt` and `docs/toolchain/UNITY_BASELINE.md`, plus workflow proof and handoff state.
- The implementation task run is `assembly/generated/task_runs/UF-001-run-001.json`.
- No task is marked done before human review.
- The first full Unity Hub project import is paired with UF-002 so a deterministic package manifest and lock exist before Unity resolves packages or generates project state.
- Stage 2 remains locked behind `GATE-010`.

## Exact next action

Review and merge PR #15. After merge, start a fresh branch from current `main`, record UF-001 as done, claim UF-002, and pin the approved Unity package manifest and lock. Perform the first full editor import and no-migration check with the locked package set.

Do not add gameplay, optional packages, networking, analytics, storefront, mobile, or Stage 2 functionality.
