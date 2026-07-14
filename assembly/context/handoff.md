# Shooter Mover UF-004 Handoff

## Outcome

UF-004 is implemented and submitted for human review in draft PR #18. The branch is `agent/uf-004-inward-assembly-skeleton` and now includes current `main` after PR #19 accepted UF-003.

Nemo manually confirmed UF-003's empty-scene URP 2D renderer before merging PR #19; the automated scan confirmed all six quality profiles share the pipeline and contain no gameplay-authority fields. UF-003 is accepted; the active review boundary is UF-004 only.

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
- Unity `6000.3.19f1` imported all nine asmdefs on the combined UF-003/UF-004 tree with exit code 0 and no assembly/compiler errors.

## Review boundary

UF-004 remains `review`, not `done`. Exact-editor import passes; the remaining proof is Nemo's Inspector graph review:

1. check out PR #18;
2. inspect the nine assembly definitions against `docs/architecture/ASSEMBLY_DEPENDENCIES.md`;
3. optionally rerun `python tools/validation/validate_unity_assembly_graph.py` from the repository root.

Empty assembly folders are intentional. Do not add marker gameplay types merely to force visible compiled DLLs.

## Exact next action

If the Inspector graph matches the documentation, mark PR #18 ready and merge it. After merge, create a fresh handoff branch to record UF-004 as done. UF-005, UF-006, UF-009, and CS-001 may treat UF-004 as satisfied only after that acceptance is durable.

Stage 2 remains blocked behind `GATE-010`.
