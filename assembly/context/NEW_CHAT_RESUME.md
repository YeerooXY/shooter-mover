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

- Product Discovery is active after verified decision D-230.
- Decisions D-101 through D-230 are persisted in authoritative batch-extension markdown and JSON files under `assembly/intake/batches/`.
- The latest completed batch is:
  - `assembly/intake/batches/LIVE_DECISIONS_D221_D230.md`
  - `assembly/intake/batches/intake_session_D221_D230.json`
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

## Proof and production direction through D-220

- Development uses a two-stage proof gate.
- Stage 1 proves intrinsic movement, aiming, four-weapon combat, enemy readability, traversal, quick restart, and voluntary repeat-play desire with temporary content.
- Stage 2 turns the proven loop into one complete reliable factory level and must also prove repeatable addition of a room or encounter, enemy, weapon, and representative final-pipeline art without foundational rewrites.
- The developer validates voluntary mastery pursuit first; then a small target-player group must show genuine behavioral replay interest.
- Formal pass signals and kill criteria are declared before test rounds. A limited failed-round sequence triggers written diagnosis, one evidence-backed substantial pivot, then stop or shelve if the revised prototype still fails.
- Content uses typed `ScriptableObject` definitions and reusable modules, stable IDs, deterministic review snapshots, generated registries, isolated feature packages, and layered automated plus playable validation.
- Meaningful rooms are independently testable packages assembled by a lightweight authored level graph.
- Authoritative mission state lives in a typed plain-C# model. Persistence uses atomic versioned snapshots plus a short idempotent transactional journal.

## Delivery and repository direction through D-230

- CI uses escalating gates: fast branch validation, targeted integration smoke tests and Windows builds, then scheduled or milestone full regression, performance, save-recovery, migration, clean-machine, offline-launch, and accessibility validation.
- Formal test and release builds are immutable identified artifacts promoted through channels rather than rebuilt between stages.
- Diagnostics remain local and privacy-safe by default, with explicit tester export and rotating one-click support bundles.
- Save evolution uses ordered idempotent migrations, immutable backups, atomic commit, protected rollback, and safe handling of removed content.
- Developer commands use one capability-gated auditable framework. Public builds exclude progression-altering commands; legitimate practice, accessibility, and speedrun tools remain separate player-facing features.
- Unity and third-party dependencies are pinned. Upgrades use isolated tasks, comprehensive validation, and rollback.
- Windows support uses a primary 60 FPS at 1080p target, a declared minimum floor with scalable visual effects, and a small representative hardware matrix.
- Test builds use immutable portable packages. Public release uses a storefront or conventional installer with optional updates, complete offline play, no campaign account requirement, and save-safe update and uninstall behavior.
- The application follows least-privilege offline security without mandatory DRM, kernel anti-cheat, or online activation.
- Human and AI work use short-lived scoped branches with protected integration, explicit ownership, layered CI, and one revertable change per branch.

## Non-negotiable interview behavior

- Do not reconstruct state from chat memory.
- Do not repeat already accepted questions.
- Do not present D-001 through D-039 as final until explicitly re-verified.
- Ask exactly one highest-impact Product Discovery question per turn.
- Use a concise A/B/C decision card with pros, cons, MVP risk, scaling or refactor risk where relevant, and one recommendation placed after all options.
- If the user gives a short clear choice, record it without extended praise or repetition and immediately continue.
- Commit decisions in grouped batches rather than after every answer. D-221 through D-230 are fully persisted; begin the next batch at D-231 and persist through D-240 unless an earlier handoff is requested.
- Keep committed checkpoints at `unsaved_decisions: 0`; while a batch is active, track queued decisions and never claim they are committed before a repository write.
- Prioritize product, vertical-slice boundaries, proof, content-production pipeline, architecture policy, accessibility, testing, delivery, and explicit scope controls before micro-level coefficients.
- Preserve complete offline campaign and permanent guest play.
- Preserve Windows as the first target and Android as later work.
- Preserve the one-level internal vertical slice rather than expanding the MVP into a public demo, Early Access release, or full campaign.
- Treat exact counts, timings, coefficients, calendar durations, and thresholds as planning or prototype variables unless explicitly fixed.

## Expected next action

Ask D-231 exactly as directed by `CURRENT_HANDOFF.json`: define milestone budgets and timebox review rules that prevent Stage 1 and Stage 2 from expanding indefinitely while still allowing a narrowly justified extension.
