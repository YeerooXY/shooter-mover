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

- Product Discovery is active after verified decision D-110.
- Decisions D-101 through D-110 were batch-persisted at the user-requested D-110 boundary in:
  - `assembly/intake/batches/LIVE_DECISIONS_D101_D110.md`
  - `assembly/intake/batches/intake_session_D101_D110.json`
- The signature thruster currently uses a small bank of independently regenerating, chainable charges. Exact count and timings remain playtest variables.
- A tiny startup forgiveness window is preferred over full invulnerability.
- Boost direction follows movement input independently from aim, with limited mid-burst steering.
- Wall collisions use predictable ricochets led by geometry and slightly shaped by movement input.
- Basic deterministic ricochet belongs in the early playable build.
- Ricochet momentum extensions are only a later prototype experiment after the ordinary game loop is usable; they are not a first-playable commitment.
- Each spent boost charge begins regenerating immediately on its own timer.
- The baseline charge count remains stable for ordinary builds, with tightly controlled access to at most one additional charge later.
- The user requested batching future repository persistence every ten discovery questions; queue D-111 through D-120 and persist at D-120 unless this instruction changes.
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
- Honour the user-requested ten-question persistence batch: track accepted D-111 through D-120 in the active conversation, then commit the full batch before asking D-121. Persist earlier if a context handoff becomes necessary or the user requests it.
- Keep already committed checkpoints at `unsaved_decisions: 0`; while an explicit batch is in progress, clearly track the queued decision range and never claim queued decisions are committed.
- Preserve the complete offline campaign and permanent guest-play requirements.
- Treat advanced ricochet extensions as prototype-dependent and defer deep commitment until a usable core build exists.
- Do not choose a stack until product identity, users, core experience, connectivity, participation, platform scope, and MVP boundary are sufficiently clear.

## Expected first action

Ask D-111 exactly as directed by `CURRENT_HANDOFF.json`: decide the base locomotion acceleration and inertia model between thruster bursts.

Do not repeat loadout, ammo, four-weapon firing, power-bank, aiming, firing-cadence, weapon movement-penalty, thruster charge, thruster invulnerability, thruster direction, steering, wall-ricochet, recovery, difficulty, XP, puzzles, checkpoint travel, death/reset, account, monetization, cloud-save, or strongbox questions first; D-064 through D-110 already settled or explicitly reopened those boundaries.