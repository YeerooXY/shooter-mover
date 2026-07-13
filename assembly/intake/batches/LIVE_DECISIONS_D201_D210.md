# Shooter Mover — Product Discovery Batch D-201 through D-210

Status: authoritative verified extension to `assembly/intake/LIVE_DECISIONS.md`.

## D-201 — Two-stage vertical-slice proof gate

- Status: accepted
- Choice: C — core-fun gate followed by a thin end-to-end production gate
- Accepted requirement: Stage 1 proves movement, aiming, simultaneous four-weapon combat, enemy readability, room traversal, quick restart, and voluntary repeat-run desire with temporary content. Stage 2 turns the proven loop into one complete automated-weapons-factory level with reliable saves, checkpoints, loot risk, a shop, strongboxes, completion, replay, scalable effects, and diagnostic evidence.
- Expansion rule: Broader content production begins only after both the replay-desire signal and a stable extensible production loop are demonstrated.
- Evidence rule: Define pass signals and kill criteria before testing rather than moving the goalposts afterward.
- Tuning rule: Exact thresholds and coefficients remain prototype and playtest variables.

## D-202 — Staged developer and external validation

- Status: accepted
- Choice: C — developer proof first, followed by target-player proof
- Accepted requirement: The developer must first find the prototype compelling enough to replay voluntarily and pursue mastery, cleaner execution, alternate loadouts, or better times. A small external target-player group must then demonstrate genuine repeat-play interest despite temporary visuals and limited progression.
- Gate rule: Failure at either stage keeps the project in prototype iteration rather than advancing to full vertical-slice production.
- Test-planning rule: Tester counts and exact replay thresholds are declared before each formal round.

## D-203 — Predeclared behavioral fun gate with explanatory feedback

- Status: accepted
- Choice: C — behavior determines the result; interviews explain it
- Accepted requirement: Actual replay behavior determines whether the external core-fun gate passes, while ratings and interviews explain why.
- Positive signals: Observe voluntary restarts, alternate loadout trials, mastery or record chasing, and interest in returning for another session.
- Motivation rule: Replay must arise from enjoyment, experimentation, or mastery rather than merely helping the developer test.
- Frustration rule: Major recurring frustration must not consistently overpower the desire to continue.
- Integrity rule: Thresholds may change between formal rounds with written justification, but never retroactively to declare a weak result successful.

## D-204 — Predeclared pivot-or-stop review

- Status: accepted
- Choice: C — limited failed rounds followed by diagnosis, one justified pivot, then stop or shelve
- Accepted requirement: After a limited predeclared number of failed formal rounds, likely two or three, pause for a written diagnosis of movement, aiming, four-weapon readability, enemy design, route pacing, onboarding, and replay incentives.
- Pivot rule: Permit one substantial evidence-backed pivot with its own fresh predeclared gate.
- Stop rule: Stop or shelve the project if the revised prototype still fails.
- Production rule: Do not enter full vertical-slice production while the result remains ambiguous.

## D-205 — Controlled two-pass reward prototype

- Status: accepted
- Choice: C — curated fixed-loadout benchmark followed by a tiny nonpersistent randomized-reward pass
- Accepted requirement: First prove intrinsic movement and combat quality with curated fixed four-weapon loadouts. After that baseline is readable and promising, add a deliberately small nonpersistent randomized weapon or reward wrapper.
- Test-purpose rule: The second pass tests whether discovery and experimentation strengthen replay desire without hiding weak combat behind extrinsic rewards.
- Deferred systems: Mature economy, permanent inventory, advanced rarity, progression, real strongbox and shop persistence, and save integration remain Stage 2 work.
- Reproducibility rule: Test conditions and loadout identities remain reproducible for comparisons.

## D-206 — Benchmark arena plus short interconnected route

- Status: accepted
- Choice: C — use both a sterile arena and a compact navigation route
- Accepted requirement: Maintain one rapidly resettable arena for controlled mechanical comparisons and one short interconnected route with several rooms, corridors, hazards, and traversal choices.
- Evidence split: The arena isolates tuning changes; the route tests navigation, pacing, route mastery, and encounter transitions.
- Reuse rule: Both environments reuse the same temporary enemies, weapons, and art.
- Scope rule: Neither prototype environment must become the final factory level.

## D-207 — Five or six representative Stage 1 weapons

- Status: accepted
- Choice: C — representative mechanically distinct archetype roster
- Accepted requirement: Build roughly five or six prototype weapons that support several meaningful four-weapon combinations.
- Coverage rule: Represent fast automatic fire, heavy slow projectiles, spread or burst behavior, beam/charge/heat behavior, homing or utility behavior, and one deliberately unusual synergy case where practical.
- Fidelity rule: Temporary art and audio are acceptable, but cadence, handling, power-ammo behavior, movement interaction, and readability must be representative.
- Scope rule: The complete authored six-to-eight-weapon MVP roster is not required before the core-fun gate.

## D-208 — Three ordinary enemy roles plus one elite

- Status: accepted
- Choice: C — representative mixed-encounter roster
- Accepted requirement: Stage 1 includes a close-pressure pursuer, a ranged projectile enemy, a positioning or area-denial enemy, and one tougher elite that combines pressure without becoming the full upgraded-droid climax.
- Test-purpose rule: Mixed encounters must exercise target prioritization, thruster use, projectile readability, crowd control, weapon synergy, and encounter composition.
- Difficulty rule: Difficulty variation must not rely only on health inflation.
- Scope rule: Remaining factory roles and the upgraded-droid climax arrive in Stage 2.

## D-209 — Readability-complete temporary presentation

- Status: accepted
- Choice: C — coherent temporary assets with representative gameplay communication
- Accepted requirement: Formal external Stage 1 testing uses coherent placeholder or prototype art and audio, while gameplay communication is representative enough to judge the actual loop fairly.
- Visual-readability rule: Player, enemies, projectiles, hazards, pickups, attack warnings, hit confirmation, aiming, four-weapon firing, readiness, and HUD resources must be clearly distinguishable.
- Audio rule: Weapons, threats, damage, pickups, and movement receive distinct temporary audio sufficient for comprehension and game-feel evaluation.
- Accessibility rule: Reduced-effects and configurable camera-feedback controls are present.
- Performance rule: Representative effect density and performance instrumentation are included.
- Deferred polish: Final sprites, detailed environments, music, cinematics, and cosmetic finishing remain deferred.

## D-210 — Playable-session reliability before formal external testing

- Status: accepted
- Choice: B — repeated complete prototype sessions must be technically reliable
- Accepted requirement: A formal external-test build must reliably support repeated complete prototype runs without known progression-blocking defects or routine crashes during the expected session.
- Interaction rule: Restart, loadout changes, controls, and settings persistence must work consistently.
- Performance rule: Representative combat should remain near the target frame rate, with major frame spikes recorded.
- Diagnostics rule: Logs capture crashes, major performance failures, input problems, and important run events.
- Evidence-integrity rule: A session ruined by a technical failure is excluded from the fun-gate result and recorded separately as a reliability failure.
- Tolerance rule: Minor visual defects and non-critical edge cases are acceptable; near-release stability is not required.

## Batch persistence

- Persisted through: D-210
- Unsaved decisions after checkpoint: 0
- Next direction: define the Stage 2 production gate before broader level and content production begins
