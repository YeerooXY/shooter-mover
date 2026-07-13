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

- Product Discovery has resumed and is paused after verified decision D-068.
- D-068 selected cleared-room persistence by default with deterministic, difficulty-scaled multi-room reclamation on stronger rulesets.
- `unsaved_decisions` must be zero before continuing.
- No pull request is open yet.
- Decisions D-001 through D-039 remain an unverified recovered set.
- Decisions D-040 through D-063 are archived verbatim in `assembly/intake/archive/LIVE_DECISIONS_THROUGH_D063.md`.
- Decisions D-064 through D-068 are in `assembly/intake/LIVE_DECISIONS.md`.

## Non-negotiable behavior

- Do not reconstruct state from chat memory.
- Do not repeat already accepted questions.
- Do not present D-001 through D-039 as final until explicitly re-verified.
- Ask exactly one highest-impact Product Discovery question per turn.
- Use a concise A/B/C decision card with pros, cons, MVP risk, scaling/refactor risk where relevant, and one recommendation placed after all options.
- If the user gives a short clear choice, record it without extended praise or repetition.
- Before asking the next question, commit the accepted answer to `LIVE_DECISIONS.md`, update `intake_session.json`, and update `CURRENT_HANDOFF.json`.
- Keep `unsaved_decisions` at zero.
- Preserve the complete offline campaign and permanent guest-play requirements.
- Do not choose a stack until product identity, users, core experience, connectivity, participation, platform scope, and MVP boundary are sufficiently clear.

## Expected first action

Ask D-069 exactly as directed by `CURRENT_HANDOFF.json`: decide activated-checkpoint fast-travel behavior.

Do not repeat the death/reset question or return to account, monetization, cloud-save, or strongbox decisions first; D-064 through D-068 already settled those boundaries.