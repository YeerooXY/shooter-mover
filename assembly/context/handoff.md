# Shooter Mover Task-Decomposition Handoff

## Current lifecycle phase

**Guided task decomposition is in progress.**

Requirements PR #1, planning PR #2, the partial Unity Foundation PR #3, and recovery PR #4 are merged into `main`. Their branches are permanently closed. The approved Movement and Thruster batch was generated on fresh branch `ai/task-split-shooter-mover-v1-continuation-2` for draft pull request #5.

No Unity or game implementation may begin until the complete task decomposition and canonical backlog are reviewed and merged.

## Durable task state

- Batch index: `assembly/generated/task_batch_index.json`
- Planned batches: 16
- Predeclared task IDs: 186
- Generated batch: `assembly/generated/task_batches/unity-foundation.json`
- Generated batch: `assembly/generated/task_batches/shared-contracts-core.json`
- Generated batch: `assembly/generated/task_batches/stage1-evidence-harness.json`
- Generated batch: `assembly/generated/task_batches/movement-thruster.json`
- Progress: 4 of 16 batches generated
- Next batch: `combat-four-mount`
- Canonical backlog: not generated
- Collaboration assignments and claims: not finalized

The Movement and Thruster batch contains exactly `MT-001` through `MT-012` and is owned by the Movement and Combat Builder across `core-domain` and `unity-input-physics`.

It decomposes the accepted S1.1 scope into a versioned tuning profile, deterministic base locomotion, charge/recharge bank, burst activation/steering/exit momentum, wall reflection, weighted contact/grace rules, device-independent Input System adapter, Rigidbody2D bridge, contact adapter, movement actor lifecycle, read-only thruster status, and evidence-harness scenarios.

The stale `MV-001` through `MV-012` predeclaration was reconciled to the handoff-approved `MT-001` through `MT-012` without changing task count, architecture or product requirements.

## Validation boundary

The current batch/index validation confirms:

- the index contains 16 unique batches and 186 unique predeclared IDs;
- `movement-thruster.json` contains exactly the 12 expected MT task IDs;
- every MT task is size S or M;
- dependencies point only to earlier generated UF/CS/EH tasks or earlier MT tasks;
- the MT dependency graph is acyclic and topologically ordered;
- all owned paths are exact and unique inside the batch;
- MT files do not claim Bootstrap, evidence-harness scenes, project settings, shared contracts, central generated registries, content packages, persistence or UI;
- `MT-007` exclusively owns `ShooterMoverMovement.inputactions`;
- `MT-012` loads the EH-004 arena read-only and does not edit `Stage1BenchmarkArena.unity`;
- no Unity implementation, canonical backlog, assignment or Dispatch work was created.

The current validator still skips twelve intentionally planned/missing batches, so this is not completion validation for all 186 predeclared IDs. Run and require the complete workspace validator after every indexed batch exists.

## Scope and capacity

The Foundation and Contract Steward estimates total 7.3 focused lead days. The Stage 1 Evidence Harness adds 3.6 focused lead days, bringing S1.0 to 10.9 focused lead days against the accepted five-day cap.

Dispatch therefore still requires a human decision to re-estimate, resequence, cut non-evidence breadth, or approve a bounded cap amendment. Do not hide the overrun by removing required contracts, controls, accessibility, diagnostics, reliability, save safety or performance work.

Movement and Thruster estimates 6.35 focused lead days against the accepted S1.1 cap of eight focused lead days. The remaining 1.65 days are review/integration reserve, not automatic scope for extra polish. Reaching either S1.1 cap still triggers the written milestone review.

`CS-011` still needs focused human review because it combines registry generation, drift validation, baseline generated outputs and documentation. Split it if one focused executor cannot complete and verify it as one revertible change.

## Coordination rules

- A merged branch is permanently closed. Every continuation starts from current `main` on a fresh branch after comparing branch and PR state.
- Before writing the next batch, the Task Splitter must present the proposed task IDs, titles, concise objectives, owner lanes, exact dependencies, estimated sizes and owned files/assets, then stop.
- Generate exactly one next planned batch only after explicit human continuation.
- Reviewers may validate and report findings in parallel but must not race edits to the same index, batch or handoff files.
- Each generated batch updates the index, `CURRENT_HANDOFF.json`, `NEW_CHAT_RESUME.md`, and this handoff together.
- Open one draft batch-continuation PR and stop for review. Never append to a merged PR branch.
- Do not assign implementation tasks or mutate `collaboration_state.json` until all batches validate and the canonical backlog exists.
- Stage 2 implementation remains blocked behind the explicit Stage 1 gate dependency.

## Exact next action

Review and merge draft pull request #5. In a fresh Task Splitter context after merge, propose `CB-001` through `CB-011` with titles, concise objectives, owner lanes, exact dependencies, estimated sizes and owned files/assets, then stop.

After explicit continuation, create a fresh branch from current `main`, generate only `assembly/generated/task_batches/combat-four-mount.json`, validate it, refresh all deterministic handoff files, open a draft continuation PR, and stop.

Do not generate gameplay code, finalize the backlog, assign agents, or begin Dispatch.
