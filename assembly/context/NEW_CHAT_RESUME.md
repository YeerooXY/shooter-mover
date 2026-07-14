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
- PR #17 recorded the successful first exact-editor import and accepted `UF-002` as done.
- PR #19 merged after exact-editor/static quality proof and Nemo's empty-scene renderer check passed, accepting `UF-003`.
- `UF-003` pins linear color, the new Input System backend, zero 2D gravity, and a Unity-authored URP 2D pipeline shared by all six rendering-only quality profiles.
- `UF-004` is claimed by `web-ai` and submitted in draft PR #18 on `agent/uf-004-inward-assembly-skeleton`.
- PR #18 adds nine assembly definitions and their metadata, `tools/validation/validate_unity_assembly_graph.py`, `docs/architecture/ASSEMBLY_DEPENDENCIES.md`, and `UF-004-run-001.json`.
- Static validation passes for the required graph, exact direct references, Unity-free inner layers, test flags, inward direction, missing internal references, and cycles.
- A deliberate temporary `ShooterMover.Domain -> ShooterMover.UnityAdapters` reference was rejected and removed.
- PR #18 has been merged locally with current `main` to reconcile the parallel UF-003 workflow state.
- Unity `6000.3.19f1` imported all nine UF-004 asmdefs on the combined tree with exit code 0 and no assembly/compiler errors. The expected empty-assembly notices are not errors.
- The first import generated unrelated untracked Unity files. Preserve them, never bulk-stage them, and commit only paths owned by the active task.
- Stage 2 remains locked behind `GATE-010`.

## Exact next action

Inspect the nine assembly definitions in PR #18 against `docs/architecture/ASSEMBLY_DEPENDENCIES.md`. The validator and exact-editor import already pass. If the Inspector graph matches the documented direct references, mark PR #18 ready and merge it. After merge, use a fresh handoff branch to record UF-004 as done; only then may UF-005, UF-006, UF-009, and CS-001 treat UF-004 as satisfied.

Do not add gameplay, optional packages, networking, analytics, storefront, mobile, or Stage 2 functionality.
