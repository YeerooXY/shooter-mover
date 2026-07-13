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

- Product Discovery is active after verified decision D-141.
- Decisions D-101 through D-110 were batch-persisted in:
  - `assembly/intake/batches/LIVE_DECISIONS_D101_D110.md`
  - `assembly/intake/batches/intake_session_D101_D110.json`
- Decisions D-111 through D-120 were batch-persisted in:
  - `assembly/intake/batches/LIVE_DECISIONS_D111_D120.md`
  - `assembly/intake/batches/intake_session_D111_D120.json`
- Decisions D-121 through D-130 were batch-persisted in:
  - `assembly/intake/batches/LIVE_DECISIONS_D121_D130.md`
  - `assembly/intake/batches/intake_session_D121_D130.json`
- Decisions D-131 through D-140 were batch-persisted in:
  - `assembly/intake/batches/LIVE_DECISIONS_D131_D140.md`
  - `assembly/intake/batches/intake_session_D131_D140.json`
- D-141 was persisted early because the user explicitly requested a context handoff:
  - `assembly/intake/batches/LIVE_DECISIONS_D141_D141.md`
  - `assembly/intake/batches/intake_session_D141_D141.json`
- The player pilot is mostly silent, with only rare concise authored lines. Recurring characters and factions carry most dialogue; the mech is primarily the player's combat and progression platform rather than a heavily characterized mascot.
- The world uses a stylized action-science-fiction tone: sincere stakes, colorful readable technology, exaggerated weapons and loot, and restrained humor without full parody.
- The central hub is a fast menu interface, not an explorable base.
- Missions are handcrafted with bounded controlled variation. Comparable challenge runs may use fixed seeds or standardized conditions.
- The game is campaign-first with deep mission replay for records, difficulty clears, loot, challenges, achievements, alternate objectives, and build experimentation.
- Production begins with one genuinely good, replayable level. A second level follows only after shared systems and workflow are in place; a compact six-to-ten-mission campaign is a later possible expansion, not the immediate commitment.
- The first milestone is internal, not a public demo, Early Access release, or paid episode.
- The internal MVP is a thin but complete one-level vertical slice covering launch, movement, four-weapon combat, a small enemy roster, rewards or loot, basic loadout or progression, completion, and replay.
- The runtime is fully 2D with dimensional-looking artwork. The viewpoint is shallow angled top-down while movement, aiming, navigation, and collision remain on one flat gameplay plane.
- The eventual setting may contain several distinct enemy factions, but the one-level MVP must use one cohesive opposing faction only. Do not build content pipelines or assets for future factions before the first faction and level are complete and enjoyable.
- The next decision is D-142: select the specific opposing faction used by the one-level internal MVP.
- The user requested batching repository persistence every ten discovery questions. D-141 is already safely persisted because of this handoff; continue queuing D-142 through D-150 and persist at D-150 unless the instruction changes.
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
- Honour the ten-question persistence cadence: D-141 has already been persisted for handoff safety; queue D-142 through D-150 and commit the remaining batch before asking D-151. Persist earlier if another context handoff becomes necessary or the user requests it.
- Keep committed checkpoints at `unsaved_decisions: 0`; while a batch is in progress, clearly track queued decisions and never claim they are committed before a repository write.
- Continue prioritizing high-level world identity, campaign content, MVP boundaries, platform, participation, and production-pipeline decisions before returning to micro-level combat tuning.
- Preserve the complete offline campaign and permanent guest-play requirements.
- Treat advanced ricochet extensions as prototype-dependent and defer deep commitment until a usable core build exists.
- Treat exact combat timing, projectile health, boost coefficients, collision coefficients, and similar numeric details as later prototype-and-playtest variables unless explicitly fixed.
- Do not choose a stack until product identity, users, core experience, connectivity, participation, platform scope, and MVP boundary are sufficiently clear.

## Expected first action

Ask D-142 exactly as directed by `CURRENT_HANDOFF.json`: choose the specific enemy faction used in the one-level internal MVP while keeping the broader multi-faction future setting out of immediate production scope.

Do not begin with detailed projectile, collision, AI-timing, weapon-stat, or animation-count questions. Continue at the higher product, world, content-boundary, and MVP-production level.
