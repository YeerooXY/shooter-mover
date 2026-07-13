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

- Product Discovery is active after verified decision D-160.
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
- The internal MVP is led by one human developer, supported by AI agents and occasional specialist outsourcing. The human retains final authority and integration responsibility.
- The workflow should support four or more parallel AI agents, but only through narrowly scoped, independently owned work.
- Agents use isolated task branches with interface-first contracts, explicit dependencies, acceptance criteria, owned modules or serialized assets, and reproducible proof.
- The codebase is divided primarily into gameplay-domain modules, with small governed shared contracts.
- The final engine choice is Unity with C#. The earlier provisional Godot decision is superseded.
- Unity gameplay uses a hybrid domain-core architecture: plain testable C# for important rules and state, with Unity components handling engine-facing presentation and lifecycle concerns.
- Static content definitions use typed ScriptableObjects, while mutable runtime state remains separate.
- Rendering uses URP 2D lighting with normal-mapped sprites, while gameplay remains readable without depending on dynamic lighting.
- Project composition uses additive scenes and modular prefabs, with explicit initialization and temporary ownership of serialized assets.
- Every agent task requires proportional layered validation: plain-C# tests where applicable, targeted Unity integration tests, build or project validation, plus a playable proof or reproducible verification procedure.
- The next decision is D-161: establish the Windows-first MVP input, controller, and rebinding baseline before deeper accessibility and implementation-policy decisions.
- The user requested persistence every ten discovery questions. Queue D-161 through D-170 and persist at D-170 unless this instruction changes or another context handoff is requested.
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
- Honour the ten-question persistence cadence: queue D-161 through D-170 and commit the complete batch before asking D-171. Persist earlier if another context handoff becomes necessary or the user requests it.
- Keep committed checkpoints at `unsaved_decisions: 0`; while a batch is in progress, clearly track queued decisions and never claim they are committed before a repository write.
- Continue prioritizing high-level product, campaign, content-boundary, accessibility, production-pipeline, architecture-policy, and MVP-delivery decisions before returning to micro-level combat tuning.
- Preserve the complete offline campaign and permanent guest-play requirements.
- Preserve Windows PC as the first target and Android as a later target rather than expanding the internal MVP to mobile now.
- Treat advanced ricochet extensions as prototype-dependent and defer deep commitment until a usable core build exists.
- Treat exact combat timing, projectile health, boost coefficients, collision coefficients, weapon statistics, enemy attack details, and animation counts as later prototype-and-playtest variables unless explicitly fixed.

## Expected first action

Ask D-161 exactly as directed by `CURRENT_HANDOFF.json`: establish whether the Windows-first internal MVP supports keyboard and mouse only, keyboard and mouse plus gamepad with rebindable actions, or those inputs plus an early touch-control abstraction for future Android.

Do not begin with detailed enemy attacks, weapon statistics, boss patterns, collision coefficients, or animation-count questions. Continue at the higher input, accessibility, production, architecture-policy, proof, and MVP-delivery level.
