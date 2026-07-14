# Shooter Mover Stage 1 Streamlined Dispatch

## Accepted through UF-005

UF-001 through UF-004 remain accepted with their existing repository proof.
PR #20 merged after UF-005's four owned files passed repository-layout review,
positive validation, and adversarial ownership checks. Under the streamlined
rule in `AGENTS.md`, that proof-complete human merge accepts UF-005 without a
second handoff PR or duplicated task-run file.

## Way of working

- One ordinary implementation task uses one pull request.
- Nemo's merge after required automated and manual proof records acceptance.
- Agents use fresh branches/worktrees and edit only task-owned paths.
- Implementation PRs contain dependency, scope, validation, manual-proof,
  limitation, and rollback evidence.
- Ordinary agents do not edit central lifecycle bookkeeping.
- A coordinator batches handoff/collaboration/slot reconciliation once per
  development wave; that reconciliation is not a dependency gate.
- Explicit strong-review and milestone evidence requirements remain unchanged.

## Next parallel work

After PR #22 merges, launch from fresh current `main`:

- UF-006 — explicit bootstrap composition-root shell;
- CS-001 — StableId v1; and
- UF-011 — prototype-debt register.

Keep their branches and owned paths disjoint. Stage 2 remains blocked behind
`GATE-010`.
