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
- PR #15 merged and accepted `UF-001`.
- The editor baseline is Unity 6.3 LTS `6000.3.19f1`, changeset `7689f4515d75`.
- `UF-002` is submitted for review in PR #16 with only URP `17.3.0`, Input System `1.19.0`, and Test Framework `1.6.0` as direct dependencies.
- Its exact 21-entry graph is in `Packages/packages-lock.json`; its inventory and fingerprint are in `docs/toolchain/DEPENDENCY_LOCK.md`.
- The implementation task run is `assembly/generated/task_runs/UF-002-run-001.json`.
- No task is marked done before human review.
- Static graph, exact-version, source, inventory, excluded-SDK, and fingerprint checks pass.
- The first full Unity Hub project import is now authorized with the UF-002 lock and remains the required manual review proof.
- Stage 2 remains locked behind `GATE-010`.

## Exact next action

Finish installing Unity `6000.3.19f1`, open the project, and perform the documented Package Manager no-error/no-upgrade/no-lock-rewrite check. Then review and merge PR #16. After merge, mark UF-002 done; UF-003 and UF-004 become ready and may proceed in parallel from separate fresh branches.

Do not add gameplay, optional packages, networking, analytics, storefront, mobile, or Stage 2 functionality.
