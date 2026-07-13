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

- Product Discovery is active after verified decision D-100.
- Decisions D-091 through D-100 were batch-persisted at the user-requested D-100 boundary.
- D-093 established unlimited frictionless normal fire: shooting is a permanent core ability and cannot be lost through ordinary-ammo depletion.
- D-095 established that all four mounted weapons fire concurrently under one trigger, each with independent cadence and recovery.
- D-096 corrected the power model: holding the modifier empowers all four weapons simultaneously, and each weapon consumes from its own separate power-ammo bank. Whether power ammo may eventually be purchased is reopened and remains unresolved.
- D-097 uses one manual aim point toward which all four mounts converge; optional aim assistance or auto-targeting is post-MVP.
- D-100 selected a regenerating directional thruster burst as a signature addictive movement pillar and a vertical-slice acceptance criterion.
- The user requested batching future repository persistence every ten discovery questions; queue D-101 through D-110 and persist at D-110 unless this instruction changes.
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
- Honour the user-requested ten-question persistence batch: track accepted D-101 through D-110 in the active conversation, then commit the full batch before asking D-111. Persist earlier if a context handoff becomes necessary or the user requests it.
- Keep already committed checkpoints at `unsaved_decisions: 0`; while an explicit batch is in progress, clearly track the queued decision range and never claim queued decisions are committed.
- Preserve the complete offline campaign and permanent guest-play requirements.
- Do not choose a stack until product identity, users, core experience, connectivity, participation, platform scope, and MVP boundary are sufficiently clear.

## Expected first action

Ask D-101 exactly as directed by `CURRENT_HANDOFF.json`: decide the thruster burst charge and regeneration model.

Do not repeat loadout, normal-ammo, four-weapon firing, power-ammo, aiming, firing cadence, movement-penalty, recovery, difficulty, XP, puzzles, checkpoint travel, death/reset, account, monetization, cloud-save, or strongbox questions first; D-064 through D-100 already settled or explicitly reopened those boundaries.