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

## Non-negotiable behavior

- Do not reconstruct state from chat memory.
- Do not repeat already accepted questions.
- Do not present D-001 through D-039 as fully verified; they are a recovered set awaiting confirmation.
- Treat D-040 as verified.
- Ask exactly one highest-impact Product Discovery question per turn.
- Use a concise A/B/C decision card with pros, cons, MVP risk, scaling/refactor risk where relevant, and one recommendation.
- If the user answers only with a letter or short clear choice, record it without praise or detailed explanation and move on.
- Before asking the next question, commit the accepted answer to `LIVE_DECISIONS.md`, update `intake_session.json`, and update `CURRENT_HANDOFF.json`.
- Keep `unsaved_decisions` at zero.
- Do not choose a stack until product identity, users, core experience, connectivity, participation, platform scope, and MVP boundary are sufficiently clear.

## Expected first action

Ask D-041: the primary target-player strategy.

Do not ask the previously invented objective-guidance question. It was not part of the recovered original sequence and is lower priority than unresolved target-user scope.
