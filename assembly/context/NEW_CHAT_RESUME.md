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
- PR #16 merged the UF-002 package baseline with only URP `17.3.0`, Input System `1.19.0`, and Test Framework `1.6.0` as direct dependencies.
- Its exact 21-entry graph is in `Packages/packages-lock.json`; its inventory and fingerprint are in `docs/toolchain/DEPENDENCY_LOCK.md`.
- The implementation task run is `assembly/generated/task_runs/UF-002-run-001.json`.
- UF-002 is human-accepted and recorded as done in PR #17.
- Static graph, exact-version, source, inventory, excluded-SDK, and canonical fingerprint checks pass.
- Unity `6000.3.19f1` completed the first import: all 21 packages registered in 19.57 seconds, no blocking package/compiler error appeared, and Git reported no package-file rewrite.
- PR #17 merged, so UF-003 and UF-004 are unblocked and may proceed independently.
- UF-003 is submitted for review in PR #19. It pins linear color, the new Input System backend, zero 2D gravity, and a Unity-authored URP 2D pipeline shared by all six rendering-only quality profiles.
- UF-003 exact-editor setup and clean-import checks exited 0; serialized GUID checks and the forbidden 3D-physics scan pass.
- UF-004 has a clean separate worktree at `../shooter-mover-uf004` on branch `nemo/uf-004-assembly-skeleton`; it still needs its explicit task claim and implementation.
- The first import generated untracked Unity files. Preserve them, never bulk-stage them, and commit only paths owned by the active task.
- Stage 2 remains locked behind `GATE-010`.

## Exact next action

Review PR #19 in Unity `6000.3.19f1`. Open an empty scene and confirm URP 2D is active; switch quality profiles and confirm only visual-cost settings differ; merge if accepted. UF-004 may proceed independently in its prepared worktree and must claim only its assembly-skeleton task paths. Do not bulk-stage first-import Unity files in either worktree.

Do not add gameplay, optional packages, networking, analytics, storefront, mobile, or Stage 2 functionality.
