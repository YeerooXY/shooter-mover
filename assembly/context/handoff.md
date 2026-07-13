# Shooter Mover Task-Decomposition Handoff

## Current lifecycle phase

**Guided task decomposition is in progress.**

Requirements PR #1 and planning PR #2 are merged into `main`. PR #3 also merged the first partial Unity Foundation batch, so its branch is permanently closed. Contract Steward and Evidence Harness work was recovered onto the fresh branch `ai/task-split-shooter-mover-v1-continuation-1`. No Unity or game implementation may begin until the complete task decomposition and canonical backlog are reviewed and merged.

The recovered state is open for human review in draft pull request #4. Do not append another batch to that branch after the PR merges.

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

The executable AI Assembly Line workspace validator passes the batch index, all three generated batches, all 33 generated task records, and the current dependency graph. The Evidence Harness contains exactly `EH-001` through `EH-010`; all sizes are `S` or `M`; and dependencies point only to existing `UF-*`/`CS-*` tasks or earlier `EH-*` tasks.

Ownership was narrowed for `UF-004`, `UF-005`, `UF-006`, `UF-008`, `UF-009`, and `CS-004`. The 33 currently generated tasks now have non-overlapping exact file or bounded-folder claims. The two serialized evidence scenes retain one explicit owner each: `EH-004` owns `Stage1BenchmarkArena.unity`, and `EH-005` owns `Stage1ShortRouteShell.unity`.

Artifact/build smoke work consumes `UF-010`; build identity consumes `CS-002`; input fixtures consume `CS-003`; diagnostics and validity consume `CS-012`; and the arena/route shells consume the Bootstrap/Foundation scene tasks.

The current validator skips thirteen intentionally planned/missing batches, so this is not completion validation for all 186 predeclared task IDs. Run and require the complete workspace validator after all indexed batches exist; do not describe the canonical backlog or full graph as complete before then.

## Scope and capacity blocker

The Foundation and Contract Steward estimates total 7.3 focused lead days. The Stage 1 Evidence Harness adds 3.6 focused lead days, bringing the recorded S1.0 planning total to 10.9 focused lead days against the accepted five-day cap.

Dispatch therefore requires a human decision to re-estimate, resequence, cut non-evidence breadth, or approve a bounded cap amendment. Do not hide the overrun by removing required contracts, controls, accessibility, diagnostics, reliability, save safety, or performance work.

`CS-011` still needs focused human review because it combines registry generation, drift validation, baseline generated outputs, and documentation. Split it if one focused executor cannot complete and verify it as one revertible change.

## Coordination rules

- A merged branch is permanently closed. Every later continuation starts from current `main` on a fresh branch after comparing branch and PR state.
- Before writing the next batch, the Task Splitter must present the proposed task IDs, titles, exact dependencies and owner lane to the human lead, then stop.
- Generate exactly one next planned batch only after explicit human continuation.
- Reviewers may validate and report findings in parallel but must not race edits to the same index, batch, or handoff files.
- Each generated batch must update the index, `CURRENT_HANDOFF.json`, `NEW_CHAT_RESUME.md`, and this handoff together.
- Commit each batch/index/handoff transition atomically, open one draft batch-continuation PR, and stop for review. Never append to a merged PR branch.
- Do not assign implementation tasks or mutate `collaboration_state.json` until all batches validate and the canonical backlog exists.
- Stage 2 implementation remains blocked behind the explicit Stage 1 gate dependency.

## Exact next action

Review and merge recovery PR #4. In a fresh context after merge, present the proposed `MT-001` through `MT-012` titles and exact dependencies to the human lead and stop. After explicit continuation, create a fresh branch from current `main`, generate only `assembly/generated/task_batches/movement-thruster.json`, validate it, refresh all deterministic handoff files, open a draft continuation PR, and stop.

Do not generate gameplay code, finalize the backlog, assign agents, or begin Dispatch yet.
