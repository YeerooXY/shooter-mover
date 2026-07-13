# Shooter Mover Task-Decomposition Handoff

## Current lifecycle phase

**Guided task decomposition is in progress.**

Requirements PR #1 and planning PR #2 are merged into `main`. PR #3 merged the first partial Unity Foundation batch. PR #4 recovered and merged the Contract Steward and Evidence Harness batches at `5bded6d0b9b133b3851bb1998ebb776e9356c3b5`. Both earlier task-split branches are permanently closed. No Unity or game implementation may begin until the complete task decomposition and canonical backlog are reviewed and merged.

Task-split PRs through accessibility/reliability PR #12 are merged. PR #12 merged at `6dc427949e15d76a0764cc1598663e783b833b7c`; all corresponding branches are permanently closed. The validated final Stage 1 gate batch is on fresh branch `ai/task-split-shooter-mover-v1-continuation-8` in draft PR #13 for end-of-day human review.

## Durable task state

- Batch index: `assembly/generated/task_batch_index.json`
- Planned batches: 16
- Predeclared task IDs: 186
- Generated batch: `assembly/generated/task_batches/unity-foundation.json`
- Generated batch: `assembly/generated/task_batches/shared-contracts-core.json`
- Generated batch: `assembly/generated/task_batches/stage1-evidence-harness.json`
- Generated batch: `assembly/generated/task_batches/movement-thruster.json`
- Generated batch: `assembly/generated/task_batches/combat-four-mount.json`
- Generated batch: `assembly/generated/task_batches/stage1-weapons.json`
- Generated batch: `assembly/generated/task_batches/stage1-enemies-route.json`
- Generated batch: `assembly/generated/task_batches/stage1-accessibility-reliability.json`
- Generated batch: `assembly/generated/task_batches/stage1-gate.json`
- Progress: 9 of 16 batches generated
- Stage 1 decomposition: complete
- Next batch: `mission-state-continuity`
- Canonical backlog: not generated
- Collaboration assignments and claims: not finalized

The Unity Foundation batch contains `UF-001` through `UF-011`. The Contract Steward batch contains `CS-001` through `CS-012`. The Stage 1 Evidence Harness batch contains `EH-001` through `EH-010`. The Movement and Thruster batch contains `MT-001` through `MT-012`. The Four-Mount Combat batch contains `CB-001` through `CB-011`. The Stage 1 Weapon batch contains `WP-001` through `WP-012`. Movement, combat and weapon packages are owned by the Movement and Combat Builder.

The movement batch covers immutable tuning identity, deterministic base locomotion, thruster charge/regeneration, activation/steering/exit momentum, wall reflection, weighted enemy contact/grace rules, device-independent Input System mapping, Rigidbody2D application, Unity 2D contact translation, explicit actor lifecycle, read-only thruster status, and evidence-harness scenarios.

The combat batch covers immutable runtime profiles, independent mount state machines, independent power-bank expenditure/refill eligibility and normal-fire fallback, modular behavior-plan boundaries, shared aim resolution, four-mount coordination, bounded recoil-to-movement influence, device-independent combat input, Unity 2D execution adapters, engine-independent HUD status projection, and formal foundation evidence.

The weapon batch covers the five-package baseline, one bounded shared projectile primitive, separately owned Blaster Machine Gun, Shotgun, Rocket Launcher, Arc Gun and Ricochet Gun packages, deterministic loadout fixtures, registry-input validation, temporary accessible presentation, formal package evidence and an explicit human identity/readability gate. Empowerment is numeric-only; Arc remains capped at three additional targets and Ricochet at two wall bounces.

The accepted enemy amendment freezes Pursuer Drone, Ram Droid, Mobile Blaster Droid, Blaster Turret and an easy Four-Blaster Elite. The elite has one health model, four blaster origins, mild telegraphed spread and a simple deterministic cadence; it replaces Foreman Elite without changing Prototype Overseer's Stage 2 role. One remaining Stage 2 ordinary role is intentionally deferred until Stage 1 evidence.

The enemy/route batch defines a shared engine-independent foundation, five separately owned packages, registry-input validation, read-only benchmark and route encounter composition, formal evidence and an explicit human quality gate. EN-004 through EN-008 and then EN-010/EN-011 provide the parallel lanes.

## Validation boundary

The current executable AI Assembly Line validator passes the updated batch index and all six generated batches. The index contains 16 unique batches and 186 unique predeclared IDs. The weapon batch contains exactly `WP-001` through `WP-012`; every task is size `S` or `M`; and dependencies point only to generated earlier tasks or earlier `WP-*` tasks.

The prior six-batch 68-task dependency graph was acyclic and topologically ordered. Its 227 exact file/bounded-folder ownership claims had no exact or parent-folder overlap. WP-003 through WP-007 own separate package folders and may run in parallel. WP-009 validates temporary registry outputs without editing CS-011-owned generated files. WP-011 reads but never edits EH-004's benchmark scene.

With the enemy/route batch, the executable validator passes 81 tasks in topological order. The 261 exact path claims have no duplicates. EN-009 never edits generated registry outputs, and EN-010/EN-011 never edit the evidence-owned scenes. Direct S1.3 spend is 5.75 of 10 days, preserving 4.25 days for review and bounded follow-up.

With the accessibility/reliability batch, the executable validator passes 93 tasks in topological order and 300 exact path claims have no duplicates. AR-001 isolates the settings profile, AR-005 isolates settings-only atomic persistence, AR-011 assembles immutable evidence and AR-012 has no approval authority. The batch spends 1.70 remaining S1.3 days and 2.90 S1.4 days, leaving 2.55 and 2.10 days respectively.

With the gate batch, the executable validator passes 103 tasks in topological order and 322 exact path claims have no duplicates. GATE-010 is the sole Stage 2 unlock and can complete only for a signed advance decision with `stage2_unlocked=true`. No sessions, human decisions or implementation were performed. Planned gate spend is 1.90 S1.4 days and 3.40 S1.5 days, preserving 0.20 and 1.60 days respectively.

The weapon batch records 4.60 focused lead days. Combined with combat-four-mount, S1.2 direct spend is 9.10 of 10 days, preserving 0.90 day for review reserve and bounded approved follow-up. WP-012 requires recorded human playable review before downstream work may claim weapon identity, audiovisual feedback or readability acceptance.

The current validator still skips ten intentionally planned/missing batches, so this is not completion validation for all 186 predeclared task IDs. Run and require the complete workspace validator after all indexed batches exist; do not describe the canonical backlog or full graph as complete before then.

## Scope and capacity blockers

The Foundation and Contract Steward estimates total 7.3 focused lead days. The Stage 1 Evidence Harness adds 3.6 focused lead days, bringing the recorded S1.0 planning total to 10.9 focused lead days against the accepted five-day cap.

Dispatch therefore requires a human decision to re-estimate, resequence, cut non-evidence breadth, or approve a bounded cap amendment. Do not hide the overrun by removing required contracts, controls, accessibility, diagnostics, reliability, save safety, or performance work.

The Movement and Thruster batch estimates 6.15 focused lead days against the accepted S1.1 eight-day cap, leaving 1.85 focused lead days for human review and bounded evidence-led iteration. Do not consume that reserve by silently expanding movement polish, presentation, combat, enemies, or generalized tooling.

The Four-Mount Combat and Stage 1 Weapon batches estimate 9.10 focused lead days against the accepted S1.2 ten-day cap. Only 0.90 day remains for review reserve and bounded approved follow-up. Dispatch must not add generalized tooling, mature balance depth, extra weapons or unrelated polish.

The three remaining Stage 2 weapon identities are intentionally deferred until Stage 1 evidence. A later planning amendment is required before Stage 2 combat-content generation or dispatch.

The one remaining Stage 2 ordinary enemy identity is also deferred until Stage 1 evidence and a later planning amendment.

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

Review and merge the Stage 1 gate task-split draft PR. Next session, start fresh from current `main`, present proposed `MS-001` through `MS-013` titles, owner lanes and exact dependencies for mission-state-continuity, and stop. Generate that one batch only after explicit human continuation. Stage 2 implementation remains blocked until GATE-010 is genuinely approved.

Do not generate gameplay code, finalize the backlog, assign agents, or begin Dispatch yet.
