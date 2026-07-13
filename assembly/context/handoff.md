# Shooter Mover Task-Decomposition Handoff

## Current lifecycle phase

**Guided task decomposition is in progress.**

Requirements PR #1 and planning PR #2 are merged into `main`. PR #3 merged the first partial Unity Foundation batch. PR #4 recovered and merged the Contract Steward and Evidence Harness batches at `5bded6d0b9b133b3851bb1998ebb776e9356c3b5`. Both earlier task-split branches are permanently closed. No Unity or game implementation may begin until the complete task decomposition and canonical backlog are reviewed and merged.

Movement PR #6 is merged into `main` at `b9942630abd66663a037ad3e64ddde4c62b9b441`; its branch is permanently closed. The human-approved combat-four-mount batch is validated on fresh branch `ai/task-split-shooter-mover-v1-continuation-4` and is awaiting its draft continuation PR.

## Durable task state

- Batch index: `assembly/generated/task_batch_index.json`
- Planned batches: 16
- Predeclared task IDs: 186
- Generated batch: `assembly/generated/task_batches/unity-foundation.json`
- Generated batch: `assembly/generated/task_batches/shared-contracts-core.json`
- Generated batch: `assembly/generated/task_batches/stage1-evidence-harness.json`
- Generated batch: `assembly/generated/task_batches/movement-thruster.json`
- Generated batch: `assembly/generated/task_batches/combat-four-mount.json`
- Progress: 5 of 16 batches generated
- Next batch: `stage1-weapons`
- Canonical backlog: not generated
- Collaboration assignments and claims: not finalized

The Unity Foundation batch contains `UF-001` through `UF-011`. The Contract Steward batch contains `CS-001` through `CS-012`. The Stage 1 Evidence Harness batch contains `EH-001` through `EH-010`. The Movement and Thruster batch contains `MT-001` through `MT-012`. The Four-Mount Combat batch contains `CB-001` through `CB-011`. Movement and combat are owned by the Movement and Combat Builder.

The movement batch covers immutable tuning identity, deterministic base locomotion, thruster charge/regeneration, activation/steering/exit momentum, wall reflection, weighted enemy contact/grace rules, device-independent Input System mapping, Rigidbody2D application, Unity 2D contact translation, explicit actor lifecycle, read-only thruster status, and evidence-harness scenarios.

The combat batch covers immutable runtime profiles, independent mount state machines, independent power-bank expenditure/refill eligibility and normal-fire fallback, modular behavior-plan boundaries, shared aim resolution, four-mount coordination, bounded recoil-to-movement influence, device-independent combat input, Unity 2D execution adapters, engine-independent HUD status projection, and formal foundation evidence.

## Validation boundary

The current executable AI Assembly Line validator passes the updated batch index and all five generated batches. The index contains 16 unique batches and 186 unique predeclared IDs. The combat batch contains exactly `CB-001` through `CB-011`; every task is size `S` or `M`; and dependencies point only to generated earlier tasks or earlier `CB-*` tasks.

The generated 56-task dependency graph is acyclic and topologically ordered. Its 193 exact file/bounded-folder ownership claims have no exact or parent-folder overlap. `CB-008` exclusively owns `ShooterMoverCombat.inputactions` and consumes MT-007 device lifecycle conventions without editing `ShooterMoverMovement.inputactions`. `CB-011` reads but never edits EH-004's benchmark scene.

The review corrections are frozen into the batch: total direct spend is 4.50 days; CB-003 allows explicit authored refill but no passive power regeneration; CB-010 uses plain-C# Application projection rather than UnityAdapters; and CB-011 requires recorded human playable review for shared aim, independent firing, recoil interaction, color-independent readability, restart, and fault isolation.

The current validator still skips eleven intentionally planned/missing batches, so this is not completion validation for all 186 predeclared task IDs. Run and require the complete workspace validator after all indexed batches exist; do not describe the canonical backlog or full graph as complete before then.

## Scope and capacity blockers

The Foundation and Contract Steward estimates total 7.3 focused lead days. The Stage 1 Evidence Harness adds 3.6 focused lead days, bringing the recorded S1.0 planning total to 10.9 focused lead days against the accepted five-day cap.

Dispatch therefore requires a human decision to re-estimate, resequence, cut non-evidence breadth, or approve a bounded cap amendment. Do not hide the overrun by removing required contracts, controls, accessibility, diagnostics, reliability, save safety, or performance work.

The Movement and Thruster batch estimates 6.15 focused lead days against the accepted S1.1 eight-day cap, leaving 1.85 focused lead days for human review and bounded evidence-led iteration. Do not consume that reserve by silently expanding movement polish, presentation, combat, enemies, or generalized tooling.

The Four-Mount Combat batch estimates 4.50 focused lead days against the accepted S1.2 ten-day cap. Only 5.50 focused lead days remain for six representative weapon packages, combat HUD, temporary audiovisual/readability work, human review, and bounded iteration. The next weapon batch must remain sharply bounded and must not add generalized tooling or mature balance depth.

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

Open, review, and merge the combat-four-mount continuation PR. After it merges, start a fresh context from current `main`, present the proposed `WP-001` through `WP-012` titles, owner lane and exact dependencies to the human lead, and stop. After explicit continuation, create a fresh branch from current `main`, generate only `assembly/generated/task_batches/stage1-weapons.json`, validate it, refresh all deterministic handoff files, open a draft continuation PR, and stop.

Do not generate gameplay code, finalize the backlog, assign agents, or begin Dispatch yet.
