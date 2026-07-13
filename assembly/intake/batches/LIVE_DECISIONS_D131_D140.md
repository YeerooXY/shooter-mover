# Shooter Mover — Product Discovery Batch D-131 through D-140

Status: authoritative verified extension to `assembly/intake/LIVE_DECISIONS.md`.

## D-131 — Mostly silent player pilot; mech as player tool

- Status: accepted
- Choice: Custom — a mostly silent protagonist with only a few authored lines
- Accepted requirement: The player character is present enough to anchor the campaign but is not a highly vocal or dominant personality. Silence is the default, with only rare concise lines at important moments.
- Cast rule: Recurring authored characters, factions, mission briefings, and the surrounding world carry most dialogue, personality, exposition, and emotional weight.
- Mech rule: The mech is primarily the player's combat platform, loadout, and progression vehicle rather than a speaking mascot or heavily characterized companion.
- Production rule: This direction should reduce localization, voice-production, co-op duplication, and replay-pacing burden while preserving room for light player identity and cosmetic expression.

## D-132 — Stylized action-science-fiction tone

- Status: accepted
- Choice: B — sincere stakes with colorful, exaggerated, game-readable technology
- Accepted requirement: The world may contain real danger, conflict, and meaningful consequences while freely supporting flashy weapons, distinctive factions, memorable bosses, strong loot-tier visuals, and occasional restrained humor.
- Tone rule: Avoid both relentlessly grey military bleakness and full parody. Spectacle should feel native to the world rather than apologetic or ironic.
- Visual cue: The user's generated loot-box concepts support a bold, dimensional, rarity-readable science-fiction presentation suitable for this tone.

## D-133 — Pure menu-based central hub

- Status: accepted
- Choice: A — direct interface hub rather than an explorable headquarters
- Accepted requirement: Mission selection, armory, upgrades, shops, challenges, story access, and launch flow are reached through a fast menu-driven hub.
- Replay rule: Returning to another mission or loadout should require minimal friction, supporting repeated attempts, records, and later Android use.
- Worldbuilding rule: The menu hub may visually evolve with campaign progress and characters without requiring the player to walk between stations.

## D-134 — Handcrafted missions with controlled variation

- Status: accepted
- Choice: B — authored layouts and major encounters with bounded variable elements
- Accepted requirement: Core maps, important rooms, routes, secrets, objectives, and major encounters are deliberately authored.
- Variation rule: Selected enemy formations, reinforcement choices, loot positions, optional rooms, modifiers, or secondary conditions may vary without erasing level identity.
- Competitive rule: Ranked, speedrun, or directly comparable challenges may use fixed seeds or standardized conditions.
- Scope rule: Procedural generation is not the primary level-production strategy for the MVP.

## D-135 — Campaign-first structure with deep mission replay

- Status: accepted
- Choice: B — complete a coherent campaign while making cleared missions worth revisiting
- Accepted requirement: The authored campaign is the main progression journey. Completed missions remain replayable for improved times, scores, loot, difficulty clears, achievements, challenges, build experimentation, and alternate objectives.
- Grind rule: Replay rewards should encourage mastery and experimentation rather than force repetitive farming for basic campaign viability.
- Identity rule: The project is not fundamentally a run-reset roguelite.

## D-136 — Phased campaign scope beginning with one strong level

- Status: accepted
- Choice: Custom refinement of A — compact long-term campaign, but one complete level is the first production target
- Accepted requirement: Do not begin by producing six to ten missions in parallel. First create one genuinely good, replayable level and prove the shared systems and content workflow.
- Expansion rule: Build the next level only after the first level and its supporting systems are sufficiently complete and reusable. A two- or three-mission internal milestone may follow.
- Long-term boundary: If the core proves successful, a fuller compact release may grow toward roughly six to ten substantial missions rather than a large content-heavy launch.

## D-137 — Internal MVP before any public release strategy

- Status: accepted
- Choice: Custom — the first build is an internal development milestone, not a public demo, paid episode, or Early Access launch
- Accepted requirement: Public packaging, pricing, demo strategy, and progress transfer are deferred until the game is demonstrably fun and buildable.
- Development rule: The immediate goal is one complete level that validates movement, four-weapon combat, enemies, rewards, replay, and the production pipeline.
- Sequencing rule: A second level is created after the surrounding systems are in place, not merely to inflate early content count.

## D-138 — Thin but complete one-level vertical slice

- Status: accepted
- Choice: B — minimal implementations of the entire intended player loop
- Accepted requirement: The internal MVP supports mission launch, movement, four simultaneous weapons, a small enemy roster, mission completion, rewards or loot, basic loadout or progression screens, and immediate replay.
- Integration rule: Supporting systems may be deliberately small, but should be real and connected rather than mock screens or disconnected mechanic sandboxes.
- Validation rule: The slice must test whether completing, earning, adjusting the build, and replaying form an enjoyable loop—not only whether shooting works in a test room.

## D-139 — Fully 2D runtime with dimensional-looking artwork

- Status: accepted
- Choice: A — genuine 2D gameplay and rendering pipeline
- Accepted requirement: Rooms, collision, characters, effects, projectiles, and gameplay simulation operate in a two-dimensional runtime.
- Art rule: Perspective, painted shading, shadows, layering, animation, and optionally sprites rendered from offline 3D models may create a chunky dimensional appearance.
- Platform rule: Preserve predictable collision, strong performance, and a straightforward later Android path. Runtime 3D is not required for the intended visual identity.

## D-140 — Shallow angled top-down viewpoint

- Status: accepted
- Choice: B — visible character and wall sides while gameplay remains on one flat plane
- Accepted requirement: Use a shallow angled top-down presentation in the spirit of the dimensional-looking reference games rather than a perfectly vertical or strongly isometric camera.
- Readability rule: Foreground walls, tall props, shadows, and sprite layering must not obscure important enemies, loot, aiming information, or projectiles.
- Control rule: Movement, aiming, navigation, and collision remain mechanically planar and must not inherit the alignment problems of a strong isometric grid.

## Batch persistence

- Persisted through: D-140
- Unsaved decisions after checkpoint: 0
- Next batch boundary: D-150
