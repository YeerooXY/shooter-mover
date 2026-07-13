# Shooter Mover Task-Decomposition Handoff

## Current lifecycle phase

**Guided task decomposition is in progress.**

Requirements PR #1 and planning PR #2 are merged into `main`. The durable working branch is `ai/task-split-shooter-mover-v1`. No Unity or game implementation may begin until the complete task-decomposition pull request is reviewed and merged.

## Durable task state

- Batch index: `assembly/generated/task_batch_index.json`
- Planned batches: 16
- Predeclared task IDs: 186
- Generated batch: `assembly/generated/task_batches/unity-foundation.json`
- Generated batch: `assembly/generated/task_batches/shared-contracts-core.json`
- Progress: 2 of 16 batches generated
- Next batch: `stage1-evidence-harness`
- Canonical backlog: not generated
- Collaboration assignments and claims: not finalized

The Unity Foundation batch contains `UF-001` through `UF-011`. The Contract Steward batch contains `CS-001` through `CS-012`. Shared contracts precede their consumers, and generated registry outputs have one explicit owner.

## Validation boundary

The batch index, both batch files, and all 23 generated task records pass their current JSON schema checks. Expected IDs match the index, and generated dependencies point to Foundation tasks or earlier Contract Steward tasks.

Full dependency-graph validation is not yet a completion signal because later planned IDs such as `EH-001` do not have generated batch files yet. Run and require the complete validator after all indexed batches exist; do not describe the canonical backlog or full graph as validated before then.

## Scope and capacity blocker

The Foundation and Contract Steward estimates currently total 7.3 focused lead days against the accepted five-day S1.0 cap, before the evidence-harness batch is estimated.

Dispatch therefore requires a human decision to re-estimate, resequence, cut non-evidence breadth, or approve a bounded cap amendment. Do not hide the overrun by removing required contracts, controls, accessibility, diagnostics, reliability, save safety, or performance work.

`CS-011` needs focused human review because it combines registry generation, drift validation, baseline generated outputs, and documentation. Split it if one focused executor cannot complete and verify it as one revertible change.

## Coordination rules

- The active Task Splitter is the sole writer for the batch index and deterministic handoff files during each guided continuation.
- Generate exactly one next planned batch after explicit human continuation.
- Reviewers may validate and report findings in parallel but must not race edits to the same index, batch, or handoff files.
- Each generated batch must update the index, `CURRENT_HANDOFF.json`, `NEW_CHAT_RESUME.md`, and this handoff together.
- Do not assign implementation tasks or mutate `collaboration_state.json` until all batches validate and the canonical backlog exists.
- Stage 2 implementation remains blocked behind the explicit Stage 1 gate dependency.

## Exact next action

Human-review `shared-contracts-core.json`, especially `CS-011` and the S1.0 estimate. After explicit continuation, generate only `assembly/generated/task_batches/stage1-evidence-harness.json`, validate its 10 expected tasks, and refresh all deterministic handoff files.

Do not generate gameplay code, finalize the backlog, assign agents, open the final task-split PR, or begin Dispatch yet.
