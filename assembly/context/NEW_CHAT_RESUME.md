# Resume Shooter Mover Stage 1 Development

Continue from committed repository state in `YeerooXY/shooter-mover`. Never
write to a branch whose pull request has merged.

## Required startup

1. Read `AGENTS.md`, `project_workspace.json`, and
   `assembly/context/CURRENT_HANDOFF.json`.
2. Read every path listed in `authoritative_artifacts`.
3. Verify the recorded branch, pull-request state, and comparison with current
   `main` before any write.
4. Read the exact task object and its task-run proof.
5. Follow `CURRENT_HANDOFF.json` field `next_action` exactly.

## Durable state

- Stage 1 backlog PR #14 merged at
  `377fb612a413a6e2af4dff1674374f7ec9ff5710`; Stage 1 Dispatch is active.
- PR #15 accepted UF-001 and pins Unity 6.3 LTS `6000.3.19f1`.
- PRs #16 and #17 accepted UF-002 and its exact 21-package dependency graph.
- PR #19 accepted UF-003 after exact-editor/static proof and Nemo's empty-scene
  Renderer2D check passed.
- PR #18 merged at `22067939458d90b8adfb5d68473f6f179f21bd91`,
  accepting the UF-004 inward-only assembly skeleton after all static,
  negative-reference, exact-editor, and human Inspector checks passed.
- This fresh handoff branch records UF-004 as `done` and makes its downstream
  dependency unlock durable.
- PR #20 implements UF-005 on `nemo/uf-005-repository-layout`. It changes only
  the four owned files and passes positive and adversarial repository-layout
  validation.
- UF-006 may start from fresh current `main` after this handoff merges. It owns
  only `BootstrapCompositionRoot.cs` and `BOOTSTRAP_LIFECYCLE.md`.
- Unity imports have generated unrelated local files in older worktrees.
  Preserve them, never bulk-stage them, and commit only active-task paths.
- Stage 2 remains locked behind `GATE-010`.

## Exact next action

Review and merge the UF-004 acceptance handoff PR. Then merge ready PR #20 for
UF-005 and launch UF-006 from fresh current `main`. Do not reuse either merged
branch.

Do not add gameplay, optional packages, networking, analytics, storefront,
mobile, or Stage 2 functionality.
