# Resume Shooter Mover Stage 1

Continue from committed repository state in `YeerooXY/shooter-mover`. Never write to a branch whose pull request has merged.

## Required startup

1. Read `AGENTS.md`, `project_workspace.json`, and `assembly/context/CURRENT_HANDOFF.json`.
2. Read every path listed in `authoritative_artifacts`.
3. Verify the recorded branch, pull-request state, and comparison with current `main` before any write.
4. If PR #14 is merged, switch to current `main` and create a fresh implementation branch.
5. Follow exact task dependencies and use one bounded implementation PR per claimed task or deliberately reviewed task group.

## Durable state

- PR #13 is merged at `bcf86cd41cd01bb8c3ca4f137c223049fc23e5cf`.
- Stage 1 task decomposition is complete: 9 validated batches, 103 tasks, an acyclic dependency graph, and conflict-free exact path ownership.
- The canonical Stage 1 backlog is `assembly/generated/task_backlog.json`.
- `UF-001` is the only initially ready implementation task. All other tasks remain blocked until their dependencies complete.
- No implementation task is auto-claimed.
- The approved Stage 1 cap is 50 focused lead days or 12 calendar weeks.
- S1.0 is 12 focused lead days or 3 calendar weeks; 10.9 direct days leave 1.1 days of review and integration reserve.
- Later milestone caps and reserves remain unchanged.
- Seven Stage 2 batch ranges are preserved in `assembly/generated/deferred_full_mvp_task_batch_index.json`.
- No Stage 2 task files exist. Stage 2, including any multiplayer/networking work, remains blocked behind `GATE-010` and a later evidence-backed amendment.
- The Stage 1 gate tasks describe future evidence work; they have not been executed and no outcome has been fabricated.

## Exact next action

Follow `CURRENT_HANDOFF.json` field `next_action` exactly.

Review and merge draft PR #14. After merge, begin Dispatch from a fresh branch based on current `main`: explicitly claim and execute `UF-001`, produce its required proof, and open a bounded implementation PR. Continue only through satisfied dependencies.

Do not start Stage 2 or execute the Stage 1 evidence gate early.
