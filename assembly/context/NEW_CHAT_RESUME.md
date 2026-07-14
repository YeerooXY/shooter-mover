# Resume Shooter Mover Stage 1 Development

Continue from committed state in `YeerooXY/shooter-mover`. Never write to a
branch whose pull request has merged.

## Required startup

1. Read `AGENTS.md`, `project_workspace.json`, and
   `assembly/context/CURRENT_HANDOFF.json`.
2. Read the exact task card and its dependencies.
3. Verify current `main`, relevant merged pull requests, and path ownership
   before writing.
4. Use a fresh branch and separate worktree for each task.

## Durable state

- Stage 1 Dispatch is active with 103 canonical tasks and a 50-focused-day cap.
- UF-001 through UF-004 are accepted through merged PRs #15–#19 and #21.
- PR #20 merged at `63e488b17f1899fdad89645a167a97a6d24c3bb7`
  after UF-005's positive and adversarial repository-layout validation passed.
- This amendment records UF-005 as accepted and streamlines ordinary Stage 1
  work: a proof-complete implementation PR merged by Nemo is the acceptance
  boundary; no second per-task handoff PR is required.
- Ordinary proof stays in the implementation PR. Central handoff,
  collaboration, and slot bookkeeping is reconciled once per development wave
  and is not a dependency gate.
- Explicit repository evidence remains mandatory when required by a task,
  especially milestone gates, persistence/migration, shared serialized assets,
  build/release artifacts, and strong-review boundaries.
- Stage 2 remains locked behind `GATE-010`.

## Exact next action

Review and merge the Stage 1 way-of-working simplification PR. Then launch
UF-006, CS-001, and UF-011 on separate fresh branches from current `main`.
Implementation agents must change only their owned paths, put all required
proof in their PRs, and leave central lifecycle bookkeeping untouched.

Do not add networking, mobile, analytics, storefront, or Stage 2 work.
