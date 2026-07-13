# Resume Shooter Mover Product Discovery

Continue the Shooter Mover Product Discovery from committed repository state.

## Required startup sequence

Before responding:

1. Read `AGENTS.md` completely.
2. Read `project_workspace.json`.
3. Read `assembly/context/CURRENT_HANDOFF.json`.
4. Verify repository `YeerooXY/shooter-mover`, branch `assembly/bootstrap-shooter-mover`, and the recorded commit state.
5. Read every file listed in `authoritative_artifacts`.
6. Read the complete Intake Interviewer prompt from `assembly/prompts/00-intake-interviewer.md`.
7. Continue from the exact `next_action` in `CURRENT_HANDOFF.json`.

## Current checkpoint

- Product Discovery is active after verified decision D-120.
- Decisions D-101 through D-110 were batch-persisted in:
  - `assembly/intake/batches/LIVE_DECISIONS_D101_D110.md`
  - `assembly/intake/batches/intake_session_D101_D110.json`
- Decisions D-111 through D-120 were batch-persisted in:
  - `assembly/intake/batches/LIVE_DECISIONS_D111_D120.md`
  - `assembly/intake/batches/intake_session_D111_D120.json`
- Ordinary locomotion uses quick acceleration and braking with bounded readable inertia.
- Boost exit carries momentum briefly and then decays smoothly into ordinary movement.
- Light enemies may be shoved during a boost; heavy enemies, elites, and bosses remain solid blockers or strong deflectors.
- Contact damage uses discrete hits with per-enemy repeat-hit grace. Crowds remain dangerous because different enemies can each hit the player.
- Repeated light-enemy impacts drain boost momentum; dense crowds can absorb the burst and trap careless players.
- Shoved enemies may suffer bounded stagger or impact damage when they collide with walls, hazards, or other units, but shooting remains the primary damage solution.
- All four weapons continue firing during boosts without a universal accuracy penalty.
- Boost speed scales closely with the mech build's movement speed plus selected clearly classified physical modifiers.
- Boost activation immediately replaces current velocity, enabling sharp arcade reversals and back-and-forth chains. D-112 still governs the brief momentum carry after the burst ends.
- The user requested batching future repository persistence every ten discovery questions; queue D-121 through D-130 and persist at D-130 unless this instruction changes.
- `unsaved_decisions` is zero at this checkpoint.
- No pull request is open yet.
- Decisions D-001 through D-039 remain an unverified recovered set.
- Decisions D-040 through D-063 are archived verbatim in `assembly/intake/archive/LIVE_DECISIONS_THROUGH_D063.md`.
- Decisions D-064 through D-100 are in `assembly/intake/LIVE_DECISIONS.md`.

## Non-negotiable behavior

- Do not reconstruct state from chat memory.
- Do not repeat already accepted questions.
- Do not present D-001 through D-039 as final until explicitly re-verified.
- Ask exactly one highest-impact Product Discovery question per turn.
- Use a concise A/B/C decision card with pros, cons, MVP risk, scaling/refactor risk where relevant, and one recommendation placed after all options.
- If the user gives a short clear choice, record it without extended praise or repetition.
- Honour the user-requested ten-question persistence batch: track accepted D-121 through D-130 in the active conversation, then commit the full batch before asking D-131. Persist earlier if a context handoff becomes necessary or the user requests it.
- Keep already committed checkpoints at `unsaved_decisions: 0`; while an explicit batch is in progress, clearly track the queued decision range and never claim queued decisions are committed.
- Preserve the complete offline campaign and permanent guest-play requirements.
- Treat advanced ricochet extensions as prototype-dependent and defer deep commitment until a usable core build exists.
- Treat exact boost count, recharge timing, distance, acceleration, steering, speed scaling, grace timing, and collision coefficients as prototype-and-playtest variables unless explicitly fixed later.
- Do not choose a stack until product identity, users, core experience, connectivity, participation, platform scope, and MVP boundary are sufficiently clear.

## Expected first action

Ask D-121 exactly as directed by `CURRENT_HANDOFF.json`: decide the baseline enemy attack and projectile-density philosophy.

Do not repeat loadout, ammo, four-weapon firing, power-bank, aiming, firing-cadence, weapon movement-penalty, thruster charge, thruster invulnerability, thruster direction, steering, wall-ricochet, boost recovery, base locomotion, boost-exit momentum, boost-enemy collision, contact grace, boost firing, boost speed, boost velocity replacement, difficulty, XP, puzzles, checkpoint travel, death/reset, account, monetization, cloud-save, or strongbox questions first; D-064 through D-120 already settled or explicitly reopened those boundaries.