# Resume Shooter Mover Product Discovery

Continue the Shooter Mover Product Discovery from committed repository state.

## Required startup sequence

Before responding:

1. Read `AGENTS.md` completely.
2. Read `project_workspace.json`.
3. Read `assembly/context/CURRENT_HANDOFF.json`.
4. Verify repository `YeerooXY/shooter-mover`, branch `assembly/bootstrap-shooter-mover`, and the recorded commit state.
5. Read every file listed in `authoritative_artifacts`.
6. Read the complete Intake Interviewer prompt from `assembly/prompts/00-intake-interviewer.md`. If the path remains absent, restore or read the current framework prompt from `YeerooXY/ai-assembly-line` before continuing.
7. Continue from the exact `next_action` in `CURRENT_HANDOFF.json`.

## Current checkpoint

- Product Discovery is active after verified decision D-210.
- Decisions D-101 through D-210 are persisted in authoritative batch-extension markdown and JSON files under `assembly/intake/batches/`.
- The latest completed batch is:
  - `assembly/intake/batches/LIVE_DECISIONS_D201_D210.md`
  - `assembly/intake/batches/intake_session_D201_D210.json`
- D-001 through D-039 remain recovered but unverified and must not be presented as final requirements until explicitly re-verified.
- D-040 through D-063 are archived verbatim.
- D-064 through D-100 remain in the primary live decision log.
- No pull request is open yet.

## Stable product direction

- The game is a Windows-first offline-capable top-down 2D shooter built in Unity with C#; Android and online co-op follow later.
- The runtime is fully 2D with dimensional-looking artwork and a shallow angled top-down view over one flat gameplay plane.
- The game is campaign-first with deep replay for difficulty clears, records, achievements, loot, challenges, alternate objectives, and build experimentation.
- The first complete internal vertical slice is one automated-weapons-factory level whose objective is to shut down the production core.
- The final Stage 2 slice targets approximately six to eight base weapons, four or five ordinary enemy roles, and an upgraded-droid climax.
- Four mounted weapons fire concurrently toward one shared aim point with independent cadence and separate power banks.
- The regenerating directional thruster is a signature game-feel pillar.
- Normal fire is unlimited. Power ammo is scarce but regularly usable and weapon-specific.
- Loot, strongboxes, shops, checkpoints, banking, deterministic reward grants, offline saves, difficulty rules, replay, accessibility, and performance requirements are extensively specified in the persisted decision batches.
- Co-op is post-MVP, targets up to four players through staged two-player validation, and uses relay-backed player-hosted latency-tolerant trusted local-first simulation with coarse shared room and campaign facts.

## Proof and delivery direction through D-210

- Development uses a two-stage proof gate.
- Stage 1 proves intrinsic movement, aiming, four-weapon combat, enemy readability, traversal, quick restart, and voluntary repeat-play desire with temporary content.
- Stage 2 turns the proven loop into one complete reliable factory level with real saves, checkpoints, loot risk, banking, a shop, strongboxes, completion, replay, scalable effects, and diagnostics.
- The developer must first replay voluntarily and pursue mastery; then a small target-player group must show genuine behavioral replay interest.
- Formal pass signals are declared before each test round. Behavior determines pass or fail; interviews and ratings explain causes.
- After a limited number of failed rounds, conduct a written pivot-or-stop review, permit one evidence-backed substantial pivot, and stop or shelve if the revised prototype still fails.
- Stage 1 begins with curated fixed four-weapon loadouts, followed only after promise by a tiny nonpersistent randomized reward wrapper.
- Stage 1 contains a rapidly resettable benchmark arena plus a short interconnected route.
- Stage 1 uses roughly five or six representative weapon archetypes and three ordinary enemy roles plus one elite.
- External tests use readability-complete temporary presentation: representative telegraphs, hit feedback, silhouettes, HUD, audio, reduced-effects controls, and performance instrumentation, while final art and polish remain deferred.
- Formal external builds must reliably support repeated complete prototype sessions. Technically ruined sessions are excluded from fun-gate evidence and recorded as reliability failures.

## Non-negotiable interview behavior

- Do not reconstruct state from chat memory.
- Do not repeat already accepted questions.
- Do not present D-001 through D-039 as final until explicitly re-verified.
- Ask exactly one highest-impact Product Discovery question per turn.
- Use a concise A/B/C decision card with pros, cons, MVP risk, scaling or refactor risk where relevant, and one recommendation placed after all options.
- If the user gives a short clear choice, record it without extended praise or repetition and immediately continue.
- Commit decisions in grouped batches rather than after every answer. D-201 through D-210 are fully persisted; begin the next batch at D-211 and persist through D-220 unless an earlier handoff is requested.
- Keep committed checkpoints at `unsaved_decisions: 0`; while a batch is active, track queued decisions and never claim they are committed before a repository write.
- Prioritize product, vertical-slice boundaries, proof, content-production pipeline, architecture policy, accessibility, testing, and delivery decisions before micro-level coefficients.
- Preserve complete offline campaign and permanent guest play.
- Preserve Windows as the first target and Android as later work.
- Preserve the one-level internal vertical slice rather than expanding the MVP into a public demo, Early Access release, or full campaign.
- Treat exact counts, timings, coefficients, and thresholds as prototype variables unless explicitly fixed.

## Expected next action

Ask D-211 exactly as directed by `CURRENT_HANDOFF.json`: define what the complete Stage 2 production slice must demonstrate before broader content production begins.
