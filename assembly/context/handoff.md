# Shooter Mover UF-004 Acceptance Handoff

## Outcome

UF-004 is accepted. PR #18 merged to `main` at
`22067939458d90b8adfb5d68473f6f179f21bd91` after every required proof passed:

- the nine assembly definitions match the documented inward-only graph;
- Domain, Contracts, and Application remain UnityEngine-free;
- the graph validator passes and rejects a deliberate forbidden outward
  reference;
- Unity `6000.3.19f1` imported all nine assembly definitions with no assembly or
  compiler errors; and
- Nemo confirmed the complete dependency graph in the Unity Inspector.

`assembly/generated/task_runs/UF-004-run-001.json` and the collaboration state
now record UF-004 as `done`. Empty assembly folders remain intentional; do not
add marker gameplay types merely to force visible compiled DLLs.

## Unlocked work

Once this acceptance handoff merges, UF-005, UF-006, UF-009, and CS-001 may
treat UF-004 as satisfied, subject to their remaining dependencies.

UF-005 is already implemented and validated in PR #20. That PR changes only
its four owned paths and passes positive repository-layout validation plus
negative checks for missing roots, unknown generated outputs, conflicting
ownership, Windows case variants, and unsupported wildcards.

UF-006 may then start from fresh current `main` and owns only:

- `Assets/ShooterMover/Runtime/Bootstrap/BootstrapCompositionRoot.cs`; and
- `Assets/ShooterMover/Runtime/Bootstrap/BOOTSTRAP_LIFECYCLE.md`.

## Exact next action

Review and merge this UF-004 acceptance handoff PR. Then merge ready PR #20 for
UF-005 and launch UF-006 from fresh current `main`. Never reuse a merged branch,
and do not bulk-stage unrelated Unity-generated files.

Stage 2 remains blocked behind `GATE-010`.
