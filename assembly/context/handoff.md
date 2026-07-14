# Shooter Mover Stage 1 Handoff

## Current state

Stage 1 task splitting is complete. The active index contains nine validated batches and the canonical backlog contains 103 tasks. Dependency validation is acyclic and the recorded exact path claims are conflict-free.

The human product lead approved the 2026-07-14 capacity amendment:

- S1.0: 12 focused lead days / 3 calendar weeks;
- Stage 1 aggregate: 50 focused lead days / 12 calendar weeks;
- direct S1.0 estimate: 10.9 days;
- remaining S1.0 review and integration reserve: 1.1 days.

Later milestone caps and reserves are unchanged.

## Dispatch boundary

Draft PR #14 is the approval boundary. Until it merges, the backlog is proposed rather than accepted.

After merge, `UF-001` is the only initially ready task. It must be claimed explicitly and implemented on a fresh branch. Downstream tasks stay blocked until their exact dependencies and proof requirements are satisfied.

## Stage 2 boundary

The original seven Stage 2 batch ranges are preserved in `assembly/generated/deferred_full_mvp_task_batch_index.json`. They are not part of the canonical backlog and no Stage 2 task files exist.

`GATE-010` remains the sole Stage 2 unlock. A genuine signed advance decision with `stage2_unlocked=true`, followed by an evidence-backed planning and task amendment, is required before Stage 2 generation or implementation. This includes multiplayer or networking functionality.

## Exact next action

Review and merge draft PR #14. Then start a fresh Dispatch context from current `main`, claim `UF-001`, execute it as a bounded implementation change, and continue in dependency order. Run the Stage 1 evidence gate only when all gate prerequisites and real evidence exist.
