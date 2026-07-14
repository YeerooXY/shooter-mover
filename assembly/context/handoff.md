# Shooter Mover UF-004 Handoff

## Outcome

UF-004 is implemented and submitted for human review in draft PR #18. The branch is `agent/uf-004-inward-assembly-skeleton`, created from current `main` after PR #17 accepted UF-002.

The task adds the baseline compilation boundaries for:

- Domain;
- Contracts;
- Application;
- UnityAdapters;
- Content.Definitions;
- Presentation;
- Bootstrap;
- EditMode tests;
- PlayMode tests.

Domain, Contracts, and Application explicitly set `noEngineReferences: true`. The remaining product assemblies point inward, Bootstrap acts as the composition root, and product assemblies never reference tests.

The task also adds deterministic `.asmdef.meta` files, an executable graph validator, dependency documentation, and `assembly/generated/task_runs/UF-004-run-001.json`.

## Verification completed

- All nine assembly-definition files parse as JSON.
- The Python validator compiles.
- The clean graph passes exact-reference, Unity-free-layer, test-flag, inward-direction, missing-reference, and cycle checks.
- A temporary forbidden `ShooterMover.Domain -> ShooterMover.UnityAdapters` reference was detected, then removed.

## Review boundary

UF-004 remains `review`, not `done`. The remaining proof requires Nemo's local pinned Unity `6000.3.19f1` editor:

1. check out PR #18;
2. open the project and let script compilation finish;
3. confirm there are no compiler errors;
4. inspect the nine assembly definitions against `docs/architecture/ASSEMBLY_DEPENDENCIES.md`;
5. run `python tools/validation/validate_unity_assembly_graph.py` from the repository root.

Empty assembly folders are intentional. Do not add marker gameplay types merely to force visible compiled DLLs.

## Exact next action

If Unity compilation and graph inspection pass, review and merge PR #18. After merge, create a fresh handoff branch to record UF-004 as done. UF-005, UF-006, UF-009, and CS-001 may treat UF-004 as satisfied only after that acceptance is durable.

UF-003 remains separately ready and must stay on its own branch or worktree. Stage 2 remains blocked behind `GATE-010`.
