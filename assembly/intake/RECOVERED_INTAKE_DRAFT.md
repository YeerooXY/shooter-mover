# Shooter Mover — Recovered Intake Draft

Status: recovered from the guided chat intake on 2026-07-12. This is a preservation snapshot, not yet the final schema-valid `project_intake.json`.

## Repository and project state

- Greenfield project.
- Product repository: `YeerooXY/shooter-mover`.
- Public repository, default branch `main`.
- Project concept: a top-down shooter-mover inspired by Red Storm and Robokill 2.
- MVP target: a complete vertical slice with 3 handcrafted campaign levels.

## Core vision

1. The player controls one customizable combat robot/mech in the MVP.
2. Deep chassis/robot customization is post-MVP; design for extensibility but implement one polished mech first.
3. Campaign structure is Robokill-style: a handcrafted campaign with interconnected levels.
4. Post-MVP scope includes more levels, procedural/automatic level generation for effectively unlimited play, and a legacy/prestige reset system with long-term bonuses.
5. The game must support an unusually broad difficulty range, from very casual to brutally difficult enough that tutorial content can be lethal.
6. Combat feel and tactical encounters are the MVP identity; deep customization is a later expansion.
7. The central design values are: combat first, skill over grinding, hard-but-fair challenge, handcrafted before procedural, readable failures, creative solutions, and replayability/mastery from the beginning.

## MVP boundary

- 3 handcrafted levels.
- Complete beginning/middle/end vertical slice.
- At least one handcrafted multi-phase boss.
- Predesigned weapons rather than a full modular weapon builder.
- Combat, enemy behavior, exploration, objectives, secrets, difficulty, checkpoints, progression, and basic shop/equipment systems sufficient to prove the full loop.
- PG presentation: mechanical damage, sparks, fragments, smoke, shields, explosions; no gore.

## Combat and encounter philosophy

1. Encounter size is mixed: usually medium firefights, sometimes large swarms.
2. The game should combine many simple/dumb robots with smaller numbers of smarter or specialized units.
3. The intelligence should emerge primarily from team composition and battlefield roles, not every unit acting like a genius.
4. "The encounter is the enemy": enemy roles should complement one another (e.g. swarm, heavy, sniper, shield, support, scout/commander).
5. Enemy telegraphing must be clear in the MVP through readable visuals/audio.
6. Post-MVP, enemies should also communicate and coordinate with each other more richly.
7. Enemies use perception rather than omniscience: vision, hearing, alerts, search, memory, and later deeper tactics.
8. Hiding is a temporary tactical tool, not a dominant strategy. The game should pressure excessive turtling.
9. Aggression should usually be rewarded; recklessness should be punished.
10. Combat should mix static rooms, patrols, ambushes, reinforcements, arena fights, defenses, escapes, and quiet/exploration spaces.
11. Campaign encounters should be deterministic enough for mastery and speedrunning.
12. Difficulty-specific handcrafted encounter variants are preferred over an adaptive campaign director.
13. A true adaptive/director system may be considered later for endless/procedural modes, not the main campaign.
14. Most of the time, the player should be shooting; weapon mechanics should create variety without long downtime.
15. Weapon impact feedback should include sparks, stagger, knockback where appropriate, armor fragments, shield effects, sound, smoke, and explosions.

## Player movement, aiming, and controls

1. MVP movement is arcade-responsive: precise, quick, and flow-oriented.
2. Visual/audio effects may sell mech weight without making controls sluggish.
3. One mech/chassis in the MVP; additional chassis with distinct movement styles are post-MVP.
4. Twin-stick style aiming: movement and aim are independent; the whole robot rotates as one body in the MVP.
5. Input must be device-agnostic through a shared gameplay action abstraction.
6. Planned input sources: keyboard/mouse, console/gamepad twin-stick, and mobile touch controls.
7. MVP includes a simple responsive dash.
8. Post-MVP, dash expands into advanced mobility such as multiple dash variants, blink, grapple, jet boost, or other movement-altering abilities.
9. Mobility is a mastery and speedrunning system, not only a dodge mechanic.
10. Levels should include intended routes plus optimized, hidden, risky, and movement-skill routes.
11. Players should be able to express creativity in traversal and combat rather than being forced into one correct solution.

## Weapons and equipment

1. MVP weapons are handcrafted/predesigned.
2. Post-MVP, weapons may gain deeper modular customization.
3. The player carries 4 weapons.
4. Weapon switching is instant.
5. Special equipment/ability slots may be considered post-MVP.
6. Ammo is infinite; the player should never be unable to shoot.
7. Weapons are balanced through fire rate, magazine capacity, reload, spread, recoil, accuracy, damage, range, heat, charge time, projectile speed, and similar handling characteristics.
8. Weapons must have strong identities and distinct battlefield roles, not just numerical DPS differences.
9. Weapon acquisition is hybrid: campaign unlocks, shops, random enemy drops, fixed drops from specific enemies/minibosses, and secret-area rewards.
10. Weapon rarity is side-grade/uniqueness oriented rather than simply higher power.
11. MVP augment system: approximately 5 augment types; a weapon can have at most 2 augments/slots.
12. Post-MVP may add more augments, deeper tiers, synergies, and/or more slots.
13. Weapon progression is hybrid: permanent upgrades are primary; temporary bonuses exist only sparingly.
14. Temporary rewards must remain optional bonuses, never mandatory consumable upkeep or pay-to-compete style boosters.

## Progression, mastery, and replayability

1. Progression is hybrid: major upgrades between levels, meaningful discoveries/pickups within levels.
2. The game should not become a roguelike; the campaign and permanent progression remain central.
3. Mastery systems should be extensive but optional: speedrunning, no-damage clears, medals/ranks, secret completion, optional bosses/challenges, difficulty-specific rewards, completion statistics, and later legacy/prestige progression.
4. Casual players can finish the campaign without mastery systems; expert players can continue optimizing for a long time.
5. Skill should matter more than grinding.
6. Higher difficulty should change encounters, AI behavior, resource availability, warnings, checkpoints/recovery, boss behavior, and other rules—not just inflate HP/damage.
7. Difficulty-specific layouts/encounters should remain deterministic so each difficulty can be learned and mastered.
8. Target emotional experience depends on difficulty: casual power fantasy through high-difficulty constant adaptation/mastery.

## Levels, exploration, secrets, and objectives

1. MVP consists of a few large interconnected levels rather than many tiny linear missions.
2. Levels are mostly connected, with carefully used one-way sections for pacing/set pieces.
3. One-way transitions must be designed carefully alongside room reclamation after death so players cannot enter impossible or unfair states.
4. Levels should offer multiple valid routes: intended, hidden, risky, combat-heavy, safer, movement-skill, and optimized/speedrun paths where appropriate.
5. Unknown paths and secrets should reward curiosity and creativity.
6. For the 3-level MVP, many secrets are worth the modest extra work.
7. Secrets may include hidden rooms, secret weapons, lore, shortcuts, alternate paths/exits, optional minibosses, and other rewards.
8. Mixed mission objectives are required rather than only clearing enemies: destroy targets, retrieve/activate objects, defend, escape, side objectives, and similar variety.
9. Environment matters tactically in the MVP: cover, explosive objects, turrets/security, doors, switches, hazards, machinery, and destructible objects where practical.
10. Post-MVP may evolve toward richer physics, chain reactions, and dynamic destruction.
11. Levels should have quiet/breathing/exploration spaces as contrast to combat.

## Bosses

1. MVP bosses are handcrafted, multi-phase encounters.
2. Bosses may change patterns, expose weak points, lose components, alter arenas, or use reinforcements.
3. Post-MVP/later rounds may introduce modular or dynamic boss variants.
4. Boss preparation rooms are required.
5. Lower difficulties should clearly warn that the boss is in the next room; higher difficulties may rely more on environmental recognition.
6. Major bosses show health bars; miniboss/elite health display is contextual.

## Difficulty, healing, death, checkpoints, and saves

1. Difficulty is a dedicated planning subject and must be designed as a ruleset.
2. Death/checkpoint/save behavior is a dedicated planning subject.
3. Difficulty may change enemy composition, AI coordination, attack patterns, placements, reinforcements, healing, resource availability, warnings, and boss phases.
4. Player has shields plus health.
5. Shield can regenerate; health is primarily restored through pickups, shops, upgrades, and difficulty-dependent between-room recovery.
6. Between-room health/shield recovery depends on difficulty.
7. Health pickup availability/effectiveness also depends on difficulty.
8. At least one top difficulty should provide no recovery, or an extremely small value such as about 1% per room if testing proves zero is too harsh.
9. Checkpoints are fixed, handcrafted in-world teleport stations.
10. On death, the player respawns at the last activated teleport/checkpoint.
11. Enemies may reclaim previously cleared rooms after death.
12. Reclamation must create meaningful pressure/decisions rather than repetitive busywork.
13. Save system is hybrid: permanent progress at checkpoints plus a quit-anytime, single-use suspend save.
14. The suspend save is intended to support PC, console, and especially mobile session interruption without weakening challenge.

## Shops and acquisition direction already accepted

- Shops sell weapons and upgrades.
- Enemy robots may drop random weapons.
- Some specific enemies/minibosses have fixed weapon drops.
- Campaign and secret rewards also provide weapons.
- Exact currency, pricing, selling, shop frequency, repair costs, and economy balance remain open.

## Explicit post-MVP roadmap

- Additional mech chassis and movement styles.
- Deep robot/chassis configuration.
- Modular weapon building.
- More augments and augment slots.
- Special ability/equipment slots.
- Advanced mobility abilities and traversal.
- Rich enemy-to-enemy communication and deeper tactical simulation.
- Dynamic/modular bosses.
- Highly interactive/destructible environments.
- Procedural/automatic level generation and endless play.
- Combat director for procedural/endless modes, not the deterministic campaign.
- Legacy/prestige reset system.
- More levels and content.

## Open intake areas

The following were not yet fully decided when recovery occurred:

- Map/navigation UI.
- Exact level size and expected completion time.
- Puzzle complexity, keys/locks, fast travel, and environmental storytelling details.
- Economy and shop rules.
- Story, setting, factions, protagonist context, tone beyond PG, and narrative delivery.
- Exact MVP enemy count/roles and weapon list.
- Exact boss count and level-specific objective designs.
- Target platforms/order of release.
- Engine, programming language, architecture, tooling, testing, proof requirements, and team/agent parallelization.
- Accessibility options and control rebinding specifics.
- Monetization/business model.

## Recovery rule

Before resuming guided intake, compare this snapshot against the chat transcript and correct any omission or misinterpretation. Once confirmed, convert it into the schema-valid intake artifacts required by AI Assembly Line.
