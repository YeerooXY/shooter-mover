# Shooter Mover Task-Decomposition Handoff

## Current lifecycle phase

**Guided task decomposition is in progress.**

Requirements PR #1 and planning PR #2 are merged into `main`. PR #3 merged the first partial Unity Foundation batch. PR #4 recovered and merged the Contract Steward and Evidence Harness batches at `5bded6d0b9b133b3851bb1998ebb776e9356c3b5`. Both earlier task-split branches are permanently closed. No Unity or game implementation may begin until the complete task decomposition and canonical backlog are reviewed and merged.

The human-approved movement-thruster batch is committed on the fresh continuation branch `ai/task-split-shooter-mover-v1-continuation-3` for draft pull-request review.

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

The Unity Foundation batch contains `UF-001` through `UF-011`. The Contract Steward batch contains `CS-001` through `CS-012`. The Stage 1 Evidence Harness batch contains `EH-001` through `EH-010`. The Movement and Thruster batch contains `MT-001` through `MT-012` and is owned by the Movement and Combat Builder.

The movement batch covers immutable tuning identity, deterministic base locomotion, thruster charge/regeneration, activation/steering/exit momentum, wall reflection, weighted enemy contact/grace rules, device-independent Input System mapping, Rigidbody2D application, Unity 2D contact translation, explicit actor lifecycle, read-only thruster status, and evidence-harness scenarios.

## Validation boundary

The current AI Assembly Line built-in validators pass the updated batch index and the movement-thruster batch. The index contains 16 unique batches and 186 unique predeclared IDs. The movement batch contains exactly `MT-001` through `MT-012`; every task is size `S` or `M`; and dependencies point only to existing `UF-*`/`CS-*`/`EH-*` tasks or earlier `MT-*` tasks.

The movement dependency graph is acyclic and topologically ordered. Its 36 declared owned files/assets are unique within the batch and avoid every previously exclusive serialized owner. `MT-007` exclusively owns `ShooterMoverMovement.inputactions`; `MT-012` loads but never edits `EH-004`'s `Stage1BenchmarkArena.unity`. No movement task edits `EH-005`'s short-route scene, Bootstrap-owned files, shared contracts, generated registries, Packages, ProjectSettings, ContentPackages, or accepted planning/requirements.

The current validator still skips twelve intentionally planned/missing batches, so this is not completion validation for all 186 predeclared task IDs. Run and require the complete workspace validator after all indexed batches exist; do not describe the canonical backlog or full graph as complete before then.

## Scope and capacity blockers

The Foundation and Contract Steward estimates total 7.3 focused lead days. The Stage 1 Evidence Harness adds 3.6 focused lead days, bringing the recorded S1.0 planning total to 10.9 focused lead days against the accepted five-day cap.

Dispatch therefore requires a human decision to re-estimate, resequence, cut non-evidence breadth, or approve a bounded cap amendment. Do not hide the overrun by removing required contracts, controls, accessibility, diagnostics, reliability, save safety, or performance work.

The Movement and Thruster batch estimates 6.15 focused lead days against the accepted S1.1 eight-day cap, leaving 1.85 focused lead days for human review and bounded evidence-led iteration. Do not consume that reserve by silently expanding movement polish, presentation, combat, enemies, or generalized tooling.

`CS-011` still needs focused human review because it combines registry generation, drift validation, baseline generated outputs, and documentation. Split it if one focused executor cannot complete and verify it as one revertible change.

## Coordination rules

- A merged branch is permanently closed. Every later continuation starts from current `main` on a fresh branch after comparing branch and PR state.
- Before writing the next batch, the Task Splitter must present the proposed task IDs, titles, exact dependencies and owner lanes to the human lead, then stop.
- Generate exactly one next planned batch only after explicit human continuation.
- Reviewers may validate and report findings in parallel but must not race edits to the same index, batch, or handoff files.
- Each generated batch must update the index, `CURRENT_HANDOFF.json`, `NEW_CHAT_RESUME.md`, and this handoff together.
- Commit each batch/index/handoff transition atomically, open one draft batch-continuation PR, and stop for review. Never append to a merged PR branch.
- Do not assign implementation tasks or mutate `collaboration_state.json` until all batches validate and the canonical backlog exists.
- Stage 2 implementation remains blocked behind the explicit Stage 1 gate dependency.

## Exact next action

Open and review the draft movement-thruster continuation pull request. After it merges, start a fresh context from current `main`, present the proposed `CB-001` through `CB-011` titles, owner lanes and exact dependencies to the human lead, and stop. After explicit continuation, create a fresh branch from current `main`, generate only `assembly/generated/task_batches/combat-four-mount.json`, validate it, refresh all deterministic handoff files, open a draft continuation PR, and stop.

Do not generate gameplay code, finalize the backlog, assign agents, or begin Dispatch yet.
