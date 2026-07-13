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

- Product Discovery is active after verified decision D-170.
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
- D-141 was persisted early for handoff safety in:
  - `assembly/intake/batches/LIVE_DECISIONS_D141_D141.md`
  - `assembly/intake/batches/intake_session_D141_D141.json`
- Decisions D-142 through D-150 were batch-persisted in:
  - `assembly/intake/batches/LIVE_DECISIONS_D142_D150.md`
  - `assembly/intake/batches/intake_session_D142_D150.json`
- Decisions D-151 through D-160 were batch-persisted in:
  - `assembly/intake/batches/LIVE_DECISIONS_D151_D160.md`
  - `assembly/intake/batches/intake_session_D151_D160.json`
- Decisions D-161 through D-170 were batch-persisted in:
  - `assembly/intake/batches/LIVE_DECISIONS_D161_D170.md`
  - `assembly/intake/batches/intake_session_D161_D170.json`
- The player pilot is mostly silent, with only rare concise authored lines. Recurring characters and factions carry most dialogue; the mech is primarily the player's combat and progression platform.
- The world uses a stylized action-science-fiction tone with sincere stakes, colorful readable technology, exaggerated weapons and loot, and restrained humor.
- The central hub is a fast menu interface, not an explorable base.
- Missions are handcrafted with bounded controlled variation. Comparable challenge runs may use fixed seeds or standardized conditions.
- The game is campaign-first with deep mission replay for records, difficulty clears, loot, challenges, achievements, alternate objectives, and build experimentation.
- Production begins with one genuinely good, replayable level. The internal MVP is not a public demo, Early Access release, or paid episode.
- The internal MVP is a thin but complete one-level vertical slice covering launch, movement, four-weapon combat, a compact enemy roster, rewards or loot, basic loadout or progression, completion, and replay.
- The runtime is fully 2D with dimensional-looking artwork. The viewpoint is shallow angled top-down while movement, aiming, navigation, and collision remain on one flat gameplay plane.
- The eventual setting may contain several distinct enemy factions, but the one-level MVP focuses entirely on rogue automated industrial machines.
- The first level is an automated weapons factory. Its direct mission objective is to shut down the production core.
- The first climax is a level-appropriate upgraded droid with somewhat more advanced shooting, not a giant multi-phase spectacle. Detailed enemy and boss design is deferred.
- The first level uses approximately four or five complementary ordinary enemy roles plus the upgraded droid.
- The internal MVP uses approximately six to eight authored base weapons, with four equipped simultaneously.
- Copies of the same MVP weapon behave identically. Stars, augments, enchantments, and randomized item statistics are deferred.
- The internal MVP includes a minimal real strongbox and randomized-shop loop using the base-weapon pool, without the mature economy's advanced systems.
- Art production uses a hybrid pipeline: offline 3D sources rendered into 2D sprites for mechs, enemies, weapons, and major machinery, combined with painted, procedural, or directly authored 2D environments, effects, shadows, interface work, and finishing passes.
- The internal MVP is led by one human developer, supported by AI agents and occasional specialist outsourcing. The workflow supports four or more parallel agents through isolated task branches and gameplay-domain ownership.
- The engine is Unity with C#. Gameplay uses a hybrid plain-C# domain core with Unity-facing adapters, ScriptableObject definitions, URP 2D lighting, additive scenes, modular prefabs, and layered automated validation plus playable proof.
- Windows implementation and tuning begin with keyboard and mouse, while Unity Input System actions remain device-independent for later gamepad, touch, and other platform inputs.
- The MVP includes a practical accessibility baseline: scalable interface and text, color-independent warnings, reduced-effect controls, aim assistance, input alternatives, and configurable camera feedback.
- Saves are versioned and local-first with atomic writes, validation, rolling backups, recovery, and manual export or import. Permanent offline play remains complete.
- Core audio is polished enough to judge combat, with English-first localization-ready text and no requirement for full voice acting.
- The Windows target is stable 60 FPS at 1080p on a mainstream gaming PC, with scalable quality settings and measurable effect budgets.
- Playtesting uses structured local diagnostics and exportable privacy-safe evidence bundles rather than mandatory online telemetry.
- The first factory level is an interconnected authored room map inspired broadly by the navigation feel of `Robokill` and `Red Storm`.
- Cleared rooms remain enemy-free during the run. Environmental machinery and hazards may remain dangerous and must be clearly telegraphed.
- Ordinary death respawns the player at the latest activated checkpoint. Full-mission restart remains a possible post-MVP rule for nightmare or sadistic modes and record challenges.
- Checkpoints are recognizable automatic teleports placed initially about every six or seven rooms, subject to playtesting. Their map state is visible and the model must remain adaptable to later co-op.
- The next decision is D-171: decide whether activated teleports are only respawn checkpoints, also permit fast travel, or provide broader services.
- The user requested persistence every ten discovery questions. Queue D-171 through D-180 and persist at D-180 unless this instruction changes or another context handoff is requested.
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
- Honour the ten-question persistence cadence: queue D-171 through D-180 and commit the complete batch before asking D-181. Persist earlier if another context handoff becomes necessary or the user requests it.
- Keep committed checkpoints at `unsaved_decisions: 0`; while a batch is in progress, clearly track queued decisions and never claim they are committed before a repository write.
- Continue prioritizing high-level product, campaign, level structure, content boundary, accessibility, production-pipeline, architecture-policy, proof, and MVP-delivery decisions before returning to micro-level combat tuning.
- Preserve the complete offline campaign and permanent guest-play requirements.
- Preserve Windows PC as the first target and Android as a later target rather than expanding the internal MVP to mobile now.
- Preserve the interconnected authored room map, permanent room clearing within a run, environmental hazard persistence, and frequent teleport checkpoint cadence.
- Treat exact room count, checkpoint spacing, combat timing, projectile health, boost coefficients, collision coefficients, weapon statistics, enemy attack details, and animation counts as prototype-and-playtest variables unless explicitly fixed.

## Expected first action

Ask D-171 exactly as directed by `CURRENT_HANDOFF.json`: determine what services an activated teleport provides during a mission while preserving its checkpoint role.

Do not begin with detailed enemy attacks, weapon statistics, boss patterns, collision coefficients, or animation-count questions. Continue at the higher level-structure, checkpoint, progression, accessibility, production, proof, and MVP-delivery level.
