# Shooter Mover — Project DNA

Status: derived from accepted Product Discovery decisions D-040 through D-233. This document is a concise north star; `assembly/intake/project_intake.json`, `assembly/requirements/REQUIREMENTS.md`, and the verified decision logs remain authoritative.

## Product identity

- Product type: replay-focused, skill-based, fully 2D science-fiction top-down shooter.
- Project maturity: greenfield implementation with Product Discovery complete and the first internal vertical slice awaiting Planning.
- Product goal: prove an addictive directional-thruster movement system, four concurrently firing weapons, readable machine combat, and a satisfying earn-adjust-replay loop in one complete automated-weapons-factory mission.
- Why it is worth building: the project combines the immediate arcade appeal of responsive movement and abundant weapon fire with deterministic mastery, speedrunning, achievements, loot discovery, build experimentation, and long-term replay.

## Target users

### Primary users

- User group: players who enjoy responsive top-down action, weapon experimentation, records, difficult achievements, speedrunning, and replay mastery.
- Experience level: broad spectrum from casual newcomers to expert action players and extreme-mode grinders.
- Devices and environment: Windows PC first, primarily keyboard and mouse, with device-independent input architecture for gamepad and later Android.
- Typical session or workflow frequency: complete mission attempts, checkpoint-length play segments, repeated mastery runs, and short benchmark or challenge sessions.
- Problem or desired experience: a shooter whose movement and weapon array remain satisfying enough to replay voluntarily, while difficulty and accessibility can serve very different skill levels without making the rules arbitrary.
- Success from the user's perspective: movement feels immediately controllable and worth mastering; combat remains readable despite four weapons; rewards encourage experimentation without compulsory grind; failures feel understandable and fair.

### Secondary users

- User group: campaign-focused casual players, achievement hunters, record chasers, long-term loot grinders, build experimenters, and later cooperative groups.
- Different needs or constraints: forgiving recovery and assistance options, deterministic record rules, efficient reward processing, permanent offline access, and future cross-device or cooperative continuity without compromising the solo MVP.

## Core experience

- Primary workflow or gameplay loop: `launch mission → traverse and fight → collect and bank rewards → complete or fail → open and compare rewards → adjust loadout → replay for mastery, records, challenges, or better rewards`.
- Main user input: precise planar movement, one shared aim point, one fire action for all ready mounted weapons, and a movement-directed regenerating thruster burst.
- Main product response/output: responsive displacement, four independently timed weapon streams, readable enemy pressure, persistent mission-state changes, deterministic reward transactions, and clear performance or record feedback.
- Primary reason to return: improving execution, routes, times, scores, difficulty clears, achievements, secrets, loadout combinations, and reward outcomes while enjoying the movement and combat again.

## Participation and connectivity

- Participation model: single-player for the internal MVP; real-time cooperative campaign play is the first major post-MVP multiplayer direction.
- Connectivity model: complete offline core with optional online services later.
- Account or identity model: permanent guest play; optional accounts may later support cosmetics, receipt restoration, cloud backup, and device migration without gating gameplay.
- Persistence model: versioned local-first profiles and typed mission state using atomic snapshots, rolling backups, recovery, stable IDs, and a compact idempotent transaction journal.

## Platforms and environment

- MVP platform: Windows PC, stable 1920×1080 at 60 FPS on the declared primary mainstream gaming-PC target.
- Later platforms: Android after Windows stability; other desktop or console platforms only after product and production proof.
- Required hardware, services, or integrations: Unity LTS with C#, URP 2D, Unity Input System, ordinary local file storage, GitHub pull-request workflow, and no mandatory runtime network service.

## MVP boundary

### Must prove

- Signature chainable directional-thruster movement with responsive ordinary locomotion, predictable collision behavior, and basic deterministic wall ricochet.
- Four mounted weapons firing concurrently toward one aim point with independent cadence, heat, charge, recoil, recovery, and weapon-specific power banks.
- Readable mixed-machine encounters and deterministic difficulty rules across accessible through mastery-oriented play.
- Voluntary replay desire in Stage 1, followed by a reliable complete end-to-end factory mission and repeatable content-production proof in Stage 2.
- Safe local persistence, diagnostics, accessibility, performance, immutable formal test artifacts, and bounded milestone evidence.

### Minimum complete content

- One interconnected automated-weapons-factory mission with differentiated zones, optional branches, teleport checkpoints, selected teleport shops, dedicated secure-storage rooms, and a production-core shutdown objective.
- Approximately six to eight identical-copy base weapons, with roughly five or six representative weapons available during the earlier Stage 1 proof.
- Approximately four or five ordinary rogue-machine roles plus a readable upgraded-droid climax; Stage 1 begins with three ordinary roles and one elite.
- A menu hub, mission launch, loadout and basic progression interfaces, completion, records, reward review, and immediate replay.
- Deterministic sealed strongboxes, gameplay currency, a small stable-per-run randomized shop, optional mission-bound shop-refresh tokens, safe duplicate handling, banking, and atomic reward opening.
- Representative final-pipeline art for the player, an ordinary enemy, the elite or upgraded droid, at least two weapons, and a major factory machine or environment set.

### Explicitly postponed

- Android implementation and touch controls.
- Online co-op, relay, matchmaking, host migration, shared progression, and leaderboards.
- Full campaign production, additional factions, large bosses, procedural or endless modes, and prestige systems.
- Mature randomized item stars, augments, enchantments, broad persistent rerolls, reservations, dismantling depth, inventory automation, and modular weapon construction.
- Public demo, Early Access, storefront integration, monetization infrastructure, mandatory accounts, remote telemetry, and cloud services.

### Non-goals

- Runtime 3D, a custom engine, DOTS-first architecture, or broad speculative frameworks.
- Pay-to-win, real-money strongboxes, paid progression, mandatory connectivity, invasive DRM, kernel anti-cheat, or administrator privileges for normal play.
- Replacing authored campaign mastery with primarily procedural roguelite generation.
- Treating content count, visual polish, or generalized architecture as more important than evidence-critical game feel, readability, reliability, accessibility, saves, and performance.

## Guiding product principles

- Combat feel, readable failure, and encounter quality before content count.
- The thruster is a defining mechanic, not late polish.
- Four-weapon combat should feel abundant and expressive; ordinary fire never runs out.
- Difficulty changes authored rules and pressure while remaining deterministic and learnable.
- Skill and experimentation outrank compulsory grind.
- Handcrafted repeatable missions come before procedural breadth.
- Offline ownership, privacy, safe saves, and recoverability are product features.
- Evidence gates decide expansion; sunk cost does not.
- Human and AI contributions use the same scoped ownership, validation, review, and rollback standards.

## Technology direction

- Selected stack or constraints: pinned Unity LTS, C#, URP 2D, Unity Input System, additive scenes, modular prefabs and room packages, typed ScriptableObject definitions, generated registries and review snapshots, plain-C# domain logic, and thin Unity-facing adapters.
- Why it fits the accepted product: it supports a fully 2D Windows-first action game, normal-mapped dimensional sprites, focused automated testing, later Android export, and parallel work through explicit domain and serialized-asset ownership.
- Known tradeoffs: Unity serialized assets require careful ownership and merge sequencing; dynamic lighting and four-weapon effects require strict budgets; package and editor upgrades require isolated validation; the hybrid domain/engine boundary must avoid duplicate authoritative state.

## Acceptance signals

- The developer voluntarily replays Stage 1 and pursues cleaner execution, alternate loadouts, routes, or records.
- A predeclared small target-player test shows genuine repeat-play behavior rather than testing obligation.
- The Stage 2 factory mission repeatedly completes without routine crashes, blockers, lost rewards, duplicate grants, or unrecoverable save failures.
- Representative heavy combat sustains the declared 1080p/60 target and remains readable on reduced visual settings.
- A new room or encounter, enemy, and weapon are added through documented pipelines without foundational rewrites.
- An isolated AI agent or collaborator reproduces a representative content addition with ordinary review and automated checks.
- A written production-readiness review justifies expansion beyond the first level using fun, reliability, throughput, art-pipeline, performance, scope, and cost evidence.
