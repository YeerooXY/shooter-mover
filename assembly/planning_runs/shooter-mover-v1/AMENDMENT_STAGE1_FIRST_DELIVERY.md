# Amendment: Stage 1-First Delivery and Capacity

- **Accepted:** 2026-07-14
- **Approver:** Human product lead (Nemo)
- **Planning run:** `shooter-mover-v1`

## Decision

Shooter Mover will complete and validate Stage 1 before Stage 2 task generation or implementation begins.

The nine validated Stage 1 batches become the canonical implementation backlog. The seven planned Stage 2 batch ranges remain durable long-range planning, but are excluded from the active batch index and canonical backlog until the Stage 1 evidence gate passes.

## Capacity amendment

The accepted caps are amended as follows:

| Scope | Previous cap | Amended cap |
|---|---:|---:|
| S1.0 Foundation and evidence harness | 5 lead days / 1 week | 12 lead days / 3 weeks |
| Stage 1 aggregate | 43 lead days / 10 weeks | 50 lead days / 12 weeks |

S1.0 has 10.9 focused lead days of direct task estimates. The amended 12-day cap leaves 1.1 days for review and integration. The caps and reserves for S1.1 through S1.5 remain unchanged.

This is a bounded capacity correction, not new scope. Evidence-critical contracts, validation, accessibility, diagnostics, reliability, performance, and human review remain intact.

## Canonical and deferred artifacts

- Active Stage 1 index: `assembly/generated/task_batch_index.json`
- Canonical Stage 1 backlog: `assembly/generated/task_backlog.json`
- Deferred full-MVP index: `assembly/generated/deferred_full_mvp_task_batch_index.json`

The deferred artifact preserves the prior 16-batch decomposition, including seven Stage 2 ranges and 83 predeclared Stage 2 IDs. It is planning reference only and must not be passed to Dispatch.

## Gate and sequencing

1. Review and merge the Stage 1 backlog pull request.
2. Execute Stage 1 tasks in exact dependency order, beginning with `UF-001`.
3. Produce the required automated, playable, performance, reliability, and external evidence.
4. Execute `GATE-010` only from genuine evidence.
5. If the signed decision advances, create a fresh Stage 2 planning amendment before generating Stage 2 tasks.

Multiplayer, networking, services, and other Stage 2 scope remain out of Stage 1.
