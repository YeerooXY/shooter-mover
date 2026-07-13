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
- Generated batch: `assembly/generated/task_batches/stage1-evidence-harness.json`
- Progress: 3 of 16 batches generated
- Next batch: `movement-thruster`
- Canonical backlog: not generated
- Collaboration assignments and claims: not finalized

The Unity Foundation batch contains `UF-001` through `UF-011`. The Contract Steward batch contains `CS-001` through `CS-012`. The Stage 1 Evidence Harness batch contains `EH-001` through `EH-010` and is owned by the Verification, Performance, and Release Builder.

The Evidence Harness tasks cover build/content/tuning identity, deterministic configuration and seed fixtures, bounded local diagnostics and run validity, a benchmark arena shell, a short-route shell, rapid reset/restart/session lifecycle, performance capture hooks, immutable evidence manifests/checksums, edit/play/Windows smoke entrypoints, and the human-review/invalid-session protocol.

## Validation boundary

The batch index, the Stage 1 Evidence Harness batch, and all 10 `EH-*` task records pass the current AI Assembly Line built-in task-batch validation rules. The batch contains exactly `EH-001` through `EH-010`; all sizes are `S` or `M`; dependencies point only to existing `UF-*`/`CS-*` tasks or earlier `EH-*` tasks; the EH dependency graph is acyclic; and its 41 declared owned paths have no duplicate owner.

The two serialized evidence scenes have one explicit owner each: `EH-004` owns `Stage1BenchmarkArena.unity`, and `EH-005` owns `Stage1ShortRouteShell.unity`. Artifact/build smoke work consumes `UF-010`; build identity consumes `CS-002`; input fixtures consume `CS-003`; diagnostics and validity consume `CS-012`; and the arena/route shells consume the Bootstrap/Foundation scene tasks.

Full dependency-graph validation is not yet a completion signal because thirteen later planned batches do not have generated batch files. Run and require the complete workspace validator after all indexed batches exist; do not describe the canonical backlog or full graph as validated before then.

## Scope and capacity blocker

The Foundation and Contract Steward estimates total 7.3 focused lead days. The Stage 1 Evidence Harness adds 3.6 focused lead days, bringing the recorded S1.0 planning total to 10.9 focused lead days against the accepted five-day cap.

Dispatch therefore requires a human decision to re-estimate, resequence, cut non-evidence breadth, or approve a bounded cap amendment. Do not hide the overrun by removing required contracts, controls, accessibility, diagnostics, reliability, save safety, or performance work.

`CS-011` still needs focused human review because it combines registry generation, drift validation, baseline generated outputs, and documentation. Split it if one focused executor cannot complete and verify it as one revertible change.

## Coordination rules

- The active Task Splitter is the sole writer for the batch index and deterministic handoff files during each guided continuation.
- Generate exactly one next planned batch after explicit human continuation.
- Reviewers may validate and report findings in parallel but must not race edits to the same index, batch, or handoff files.
- Each generated batch must update the index, `CURRENT_HANDOFF.json`, `NEW_CHAT_RESUME.md`, and this handoff together.
- Do not assign implementation tasks or mutate `collaboration_state.json` until all batches validate and the canonical backlog exists.
- Stage 2 implementation remains blocked behind the explicit Stage 1 gate dependency.

## Exact next action

Human-review `stage1-evidence-harness.json`, especially its scene ownership, invalid-session rules, smoke entrypoints, and 3.6-day estimate. After explicit continuation, generate only `assembly/generated/task_batches/movement-thruster.json`, validate its 12 expected tasks, and refresh all deterministic handoff files.

Do not generate gameplay code, finalize the backlog, assign agents, open the final task-split PR, or begin Dispatch yet.
