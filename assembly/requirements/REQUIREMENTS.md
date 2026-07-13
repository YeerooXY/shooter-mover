# Shooter Mover Requirements

**Status:** Requirements package ready for human review. Planning starts only after this branch is merged.

## 1. Authority and scope boundary

- Verified accepted decisions: **D-040 through D-233**
- Recovered D-001 through D-039: **excluded and not requirements**
- D-234: presented but not selected; hand it to Planning as a non-blocking policy decision
- Detailed decision logs under `assembly/intake/` remain the authoritative history
- The verified one-level internal MVP supersedes the recovered three-level MVP concept

## 2. Product goal

Create a Windows-first, offline-capable, fully 2D top-down shooter with dimensional-looking science-fiction art, addictive directional-thruster movement, four simultaneously firing weapons, handcrafted interconnected missions, broad difficulty tailoring, and deep replay for mastery, records, achievements, loot, and build experimentation.

The first production goal is one complete replayable automated-weapons-factory level and a repeatable content pipeline—not a public demo, Early Access release, or full campaign.

## 3. Target players and principles

Target players range from casual newcomers to expert action players, achievement hunters, speedrunners, record chasers, long-term grinders, and build experimenters.

Product principles:

- combat feel, readable failure, and encounter quality before content count;
- signature thruster movement as a first-class pillar;
- four-weapon combat with unlimited ordinary fire;
- handcrafted deterministic campaign mastery before procedural content;
- hard-but-fair difficulty rules rather than simple health and damage scaling;
- skill and experimentation before compulsory grind;
- complete offline and permanent guest play;
- evidence before expansion;
- Git and pull requests as durable state and approval boundaries.

## 4. Internal MVP

### Must include

- One handcrafted interconnected automated-weapons-factory mission with differentiated zones, optional branches, secure-storage banking rooms, teleport checkpoints, and a production-core shutdown objective.
- A readable upgraded-droid climax appropriate for mission one.
- A menu hub, mission launch, completion, rewards, basic progression/loadout screens, records, and immediate replay.
- Arcade-responsive planar movement and a regenerating directional thruster for dodging, pursuit, routing, traversal, and speedrunning.
- Manual shared-point aiming with four mounted weapons firing concurrently and tracking independent cadence, heat, charge, recovery, recoil, and power-ammo state.
- Roughly six to eight handcrafted base weapons, four equipped simultaneously, with identical base copies in the internal slice.
- Roughly four or five complementary ordinary rogue-machine roles plus the upgraded droid.
- Unlimited ordinary fire and scarce but regularly usable weapon-specific power-ammo banks with automatic normal-fire fallback.
- A minimal real `play → earn → bank → open → compare → equip → replay` economy using deterministic sealed strongboxes, a small randomized shop, currency, safe duplicate handling, and capacity-safe reward review.
- Fog-of-war room mapping with light guidance and no automatic secret revelation.
- Deterministic learnable difficulty rules that tailor recovery, encounters, warnings, checkpoint pressure, and mastery conditions.
- Versioned local-first saves with stable IDs, atomic writes, backups, recovery, manual import/export, and safe solo suspension outside combat.
- Practical accessibility, readable effects/HUD/audio, privacy-safe diagnostics, and stable 1080p/60 performance on the primary target.
- A proven pipeline for adding a representative room or encounter, enemy, weapon, and final-art asset without foundational rewrites.

### Explicitly postponed

- Android implementation, touch controls, and mobile release;
- online co-op, relay services, matchmaking, host migration, shared progression, and leaderboards;
- public demo, Early Access, full campaign, and release-scale storefront work;
- extra factions, chassis, advanced mobility, large spectacle bosses, procedural/endless play, and prestige;
- randomized item stars, augments, enchantments, mature strongbox tiers, broad shop rerolls, dismantling, reservations, and inventory automation;
- modular weapon building, deep chassis customization, and special equipment slots;
- remote telemetry, mandatory accounts/cloud, always-online play, custom launcher, invasive DRM, or kernel anti-cheat;
- full voice acting, release-scale localization, and final accessibility breadth;
- exact content lists and numeric balance until prototypes provide evidence.

## 5. Core gameplay

### Movement and aiming

- Runtime gameplay, collision, projectiles, and simulation are fully 2D on one plane.
- Presentation uses a shallow angled top-down camera.
- Movement is precise and arcade-responsive; audiovisuals may sell weight without sluggish controls.
- The directional thruster is a signature regenerating burst and must receive first-class prototyping and tuning.
- One cursor or aim point controls the complete weapon array.
- Mount geometry requires close-range convergence safeguards.

### Four concurrent weapons

- One fire action activates every ready mounted weapon.
- Each weapon follows its own cadence, heat, charge, burst, recoil, recovery, and downtime.
- Normal shooting never runs out and has no consumable ammo, refill action, or universal reload tax.
- Use a small clear family of firing models rather than one universal restriction or bespoke logic for every weapon.
- Selected heavy weapons may affect movement, but combined penalties are capped.

### Power fire

- Holding the power modifier attempts to empower all four weapons.
- Each weapon has an independent power bank and authored cost.
- Empty banks immediately fall back to normal fire while other weapons stay empowered.
- Power ammo is optional for completion, clearly communicated, scarce, and usable several meaningful times per checkpoint section.
- The internal slice should use a compact set of polished empowerment archetypes.

## 6. Mission, navigation, and state

- The factory uses independently testable modular authored rooms assembled through a lightweight level graph.
- The critical route remains understandable; branches reward exploration, secrets, strongboxes, challenges, shortcuts, or currency.
- Cleared ordinary rooms remain clear; persistent hazards may remain if legible.
- Teleport checkpoints initially target roughly one per six or seven rooms, adjusted through playtesting.
- Activated teleports support safe same-level fast travel; only a selected subset hosts shops.
- Ordinary death returns to the latest checkpoint.
- Explored rooms, defeated enemies, completed objectives, and permanently opened routes remain durable.
- Temporary post-checkpoint resources, loose pickups, deployed objects, and unsecured loot may roll back.
- Secure-storage rooms bank eligible carried loot; shop purchases secure immediately.
- Solo quit suspension is allowed only in safe out-of-combat conditions and is not a rewind slot.

The authoritative state is a typed engine-independent `MissionRunState` keyed by stable IDs. Unity rooms project that state and request validated transitions rather than owning durable truth.

## 7. Rewards and progression

### Internal-slice economy

Implement only enough real economy to validate the connected replay loop:

- deterministic sealed strongboxes resolving to the base-weapon pool;
- a small stable-per-run randomized shop;
- gameplay currency;
- authored secure-storage banking;
- atomic single-grant reward opening;
- capacity-safe review and smallest viable duplicate handling.

Do not implement the mature economy merely because its future direction is known.

### Reward integrity

Strongbox pickup commits a unique ID, seed, source, progression and difficulty snapshot, collection order, relevant modifiers, and loot-table/content version.

Opening must be atomic and reload-proof. Updating, levelling, changing difficulty, delaying, crashing, or reloading cannot reroll, duplicate, or erase the result.

### Longer-term accepted direction

Later systems may add progression-bounded random strongboxes, broad shops, limited rerolls, rarity-scaled prices, selling/dismantling, recurring weapon families, authored successors, player levels, milestone choices, and gameplay-resource respecs. Skill and difficult later content must remain more valuable than trivial farming.

## 8. Difficulty, accessibility, and replay

- Difficulty is selected at mission start and fixed for the attempt.
- Ordinary accessible-to-expert rulesets are available immediately; transformative extreme modes may unlock later.
- Rulesets may alter authored encounters, attacks, recovery, warnings, checkpoint pressure, and mastery conditions while remaining deterministic.
- Accessible modes may be intentionally forgiving; extreme modes may approach zero recovery.
- Earlier content may remain a power fantasy in ordinary modes; explicit scaled replay modes may keep it challenging later.
- Reclaimed or repeated state cannot generate infinite XP, recovery, ammo, refresh resources, or loot.

Accessibility baseline:

- scalable text and HUD;
- color-independent warnings;
- reduced flashing/effects;
- configurable shake and camera feedback;
- aim assistance;
- hold/toggle alternatives;
- audio and display controls;
- input rebinding;
- readable reduced-quality presentation.

Replay supports improved times, scores, secrets, difficulty clears, achievements, alternate objectives, build experiments, no-damage attempts, route optimization, and future verified record categories.

## 9. Presentation and content pipeline

### Art and audio

- The shipped runtime remains fully 2D.
- Mechs, enemies, weapons, and machinery may use offline 3D models rendered to sprites.
- Environments, projectiles, explosions, shadows, UI, damage overlays, and finishing may use authored or procedural 2D methods.
- Document perspective, scale, pivots, palette, lighting, normal maps, shadows, sorting, collision, animation, and import rules.
- URP 2D supplies normal-mapped sprites, restrained lighting, emissives, warning lights, muzzle flashes, projectiles, explosions, and scalable shadows.
- Gameplay readability cannot depend on dynamic lights.
- Important movement, thruster, weapon, impact, enemy, warning, factory, UI, and reward events require convincing audio.
- English text uses stable localization keys; full voice acting is unnecessary.

### Representative final-pipeline proof

Most Stage 2 content may remain coherent temporary art, but the intended pipeline must be proven on:

- the player mech;
- one ordinary enemy;
- the elite or upgraded droid;
- at least two distinct weapons;
- a major factory machine/environment set;
- representative animation, lighting, normal maps, emissives, damage, destruction, and effects.

After the lead establishes the process, an isolated AI agent or collaborator must reproduce a representative addition without foundational help.

## 10. Technical architecture

Selected stack:

- Unity with C#;
- pinned Unity LTS and package lock;
- URP 2D;
- Unity Input System;
- additive scenes and modular prefabs;
- typed ScriptableObject definitions;
- plain-C# domain logic with thin Unity-facing adapters;
- generated registries and deterministic review snapshots.

Rules:

- Runtime mutable state does not live in shared ScriptableObjects.
- Content uses stable readable IDs and typed definitions.
- Ordinary variations use reusable behavior modules; genuinely novel mechanics may add isolated tested code extensions.
- Generated files are not manually edited.
- Cross-scene behavior uses explicit services, installers, registries, commands, and events—not scene searches or lifecycle luck.
- Do not introduce DOTS, a custom engine, runtime 3D, or broad packages without measured need.

Persistence uses atomic versioned snapshots plus a compact idempotent transactional journal for checkpoints, banking, unique rewards, route/objective changes, completion, and suspension.

## 11. Saves, diagnostics, security, and delivery

### Save and migration policy

- Validate and atomically replace versioned local saves.
- Keep rolling backups and manual import/export.
- Newer builds back up, validate, migrate, and atomically commit supported older saves.
- Migrations are ordered, idempotent, and interruption-tested.
- Older builds refuse to modify newer schemas.
- Rollback uses cloned or compatible backups, never destructive downgrade.
- Removed content uses tombstones, replacements, refunds, or explicit compensation.

### Diagnostics and debug tools

- Bounded structured logs remain local by default and exclude unrelated personal information.
- Testers explicitly export diagnostic bundles.
- Developer builds expose deterministic setup, inspection, performance, and fault-injection commands.
- Formal test builds expose only required commands and log their use.
- Altered sessions are excluded from normal fun, achievement, record, and challenge evidence.
- Public builds exclude progression-altering commands and retain safe support diagnostics.

### Windows trust and distribution

- Formal test builds are immutable checksummed portable artifacts and support side-by-side versions.
- A later public release may use a conventional installer or storefront with optional updates and complete offline launch.
- Saves live outside the installation directory and survive updates and ordinary uninstall.
- Normal play requires no administrator rights.
- The client contains no credentials, signing keys, or service secrets.
- Imported saves, journals, archives, and support bundles are validated defensively.
- Avoid mandatory DRM, online activation, and kernel anti-cheat.
- Modified profiles may lose verified competitive eligibility without losing campaign access.

## 12. Performance target

The primary target is stable **60 FPS at 1920×1080** during representative heavy combat with intended four-weapon effects, enemy/projectile density, lighting, shadows, particles, destruction, audio, UI, and diagnostics.

Track explicit CPU, GPU, allocation, memory, loading, atlas, particle, enemy, projectile, light, and audio budgets.

A declared minimum floor may reduce visuals while preserving complete readable gameplay, input, collision, simulation, saves, and progression. Higher-end options may add cosmetic quality but no gameplay advantage.

Validate through stress scenes and real hardware covering the primary target, minimum floor, another GPU/driver family, and clean Windows installations.

## 13. Business and online boundaries

The long-term business direction is free-to-play with direct cosmetic-only purchases.

- All gameplay, levels, bosses, weapons, difficulties, progression, challenges, and gameplay updates remain free.
- Real money cannot buy strongboxes, weapons, power, progression, loot odds, currency, materials, rerolls, capacity, or competitive advantages.
- Gameplay strongboxes are earned, not purchased.
- Accounts remain optional and never gate campaign or guest play.
- Optional linkage may later support cosmetics, receipt restoration, cloud backup, and device migration.
- Divergent cloud/local profiles are selected as complete lineages; never item-merge them.
- Cosmetics may not obscure telegraphs, hazards, hitboxes, enemies, or projectiles.

The internal slice does not need monetization or account infrastructure.

## 14. Team and workflow

- One human lead owns product direction, final review, integration, scope, code, and assets.
- Four or more AI agents may work concurrently when independently ownable tasks exist.
- Specialist art, audio, music, or animation may be outsourced selectively.
- Organize work around bounded domains such as movement, weapons, enemies, rooms, rewards, menus, and narrow shared foundations.
- Each short-lived branch declares scope, owned files/assets, dependencies, interfaces, acceptance criteria, and proof.
- Avoid concurrent edits to the same scene, prefab, ScriptableObject, or module without explicit sequencing.
- Human and AI contributions use the same gates.
- One task branch should be one revertable change.
- Generated files are rebuilt, not hand conflict-resolved.

## 15. Validation and release engineering

### Layered CI

Ordinary branches:

- compile;
- run fast deterministic edit-mode and affected-system tests;
- validate content schemas, stable IDs, references, imports, registries, and ownership.

Integration:

- relevant play-mode smoke tests;
- Windows player smoke for launch, menu, input, room load, combat, save, resume, and clean exit.

Milestones:

- full regression;
- performance and memory stress;
- save corruption, interruption, journal, backup, migration, and recovery;
- clean-machine install and offline launch;
- settings/accessibility persistence;
- reproducible release-candidate artifacts.

Flaky tests are quarantined and repaired rather than rerun until lucky.

### Immutable artifacts and dependencies

Formal artifacts record build/content identity, source commit, Unity version, dependency/generated-content fingerprints, tests, symbols, checksums, and save-schema compatibility.

Promote the exact same artifact through validation channels. Any change creates a new artifact. Retain current and previous known-good builds plus migration and save-safe rollback support.

Pin dependencies, record source/version/purpose/license, disable automatic upgrades, and validate upgrades in isolated branches with rollback. Defer analytics, storefront, networking, and mobile SDKs until their workstreams begin.

## 16. Product proof gates

### Stage 1 — intrinsic game-feel proof

Prove movement, aiming, thruster feel, four-weapon combat, enemy readability, traversal, quick restart, and voluntary repeat play using:

- a rapidly resettable benchmark arena;
- a short interconnected route;
- roughly five or six representative weapons;
- three ordinary enemy roles plus one elite;
- readability-complete temporary visuals, HUD, telegraphs, audio, reduced-effects controls, and instrumentation.

The developer must voluntarily replay and pursue mastery. Then a small target-player group tests repeated complete sessions under predeclared behavioral criteria.

Behavior determines pass/fail; interviews explain causes. Technically ruined sessions are reliability failures and excluded from fun evidence.

After limited failed rounds, write a diagnosis, allow at most one substantial evidence-backed pivot, and stop or shelve if the revised prototype still fails.

### Stage 2 — complete factory and production proof

Deliver the complete reliable factory mission with saves, checkpoints, suspension, banking, shop, strongboxes, completion, replay, accessibility, diagnostics, performance, representative final art, and repeatable room/enemy/weapon pipelines.

A representative new room or encounter, enemy, and weapon must be addable without foundational rewrites. An independent agent or collaborator must reproduce a representative pipeline addition.

### Milestone control

Split both stages into evidence-producing milestones with predeclared effort or calendar caps.

At a cap:

1. pause;
2. review evidence and cause;
3. cut breadth before evidence-critical validation, accessibility, saves, reliability, or performance;
4. resequence dependencies;
5. approve only specific bounded extensions;
6. pivot or stop when evidence no longer justifies continuation.

Before a milestone or test, freeze the product question, evidence, outcomes, scope, method, population, and approvers. Material changes create a new version or round; old results retain their original rules.

## 17. Non-blocking Planning questions

Planning may proceed while explicitly resolving:

- exact milestone budgets and formal behavior thresholds;
- exact representative weapons, enemies, room graph, branches, and upgraded-droid attacks;
- numerical economy, reward, recovery, difficulty, and power-ammo coefficients;
- exact minimum supported Windows hardware;
- asset tools, license/provenance records, and source-asset backup policy;
- repository and release-material disaster recovery;
- prototype-shortcut register and Stage 2 debt exit conditions.

D-001 through D-039 remains excluded and creates no blocker.

## 18. Requirements acceptance

This package is ready for Planning when:

- `assembly/intake/project_intake.json` validates;
- this document agrees with the intake JSON on the one-level internal MVP;
- verified decisions D-040 through D-233 remain available;
- D-001 through D-039 is clearly excluded;
- the repository contains a clean handoff;
- the human reviews and merges the requirements/bootstrap pull request.

Planning must create the product and repository design in a separate planning pull request and must not create the canonical task backlog.
