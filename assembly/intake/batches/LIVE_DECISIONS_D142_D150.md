# Shooter Mover — Product Discovery Batch D-142 through D-150

Status: authoritative verified extension to `assembly/intake/LIVE_DECISIONS.md`.

## D-142 — Rogue industrial machines as the first opposing faction

- Status: accepted
- Choice: A — automated industrial and rogue-machine faction
- Accepted requirement: The one-level internal MVP fights a cohesive force of automated factory units, security robots, maintenance machines, industrial drones, and related rogue machinery.
- Readability rule: Enemy silhouettes, movement roles, weapons, warning states, and weight classes must remain clearly distinct from the player mech and from one another.
- Production rule: Prefer modular mechanical construction, shared components, and reusable effects or animation patterns so the first faction can be produced and iterated efficiently.
- Expansion rule: Corporate, mercenary, biological, alien, and other factions remain possible later, but no assets or systems for them are required before the first faction and level are enjoyable.

## D-143 — Automated weapons factory as the first level setting

- Status: accepted
- Choice: B — automated weapons factory
- Accepted requirement: The first complete level takes place inside an automated weapons-production complex containing assembly lines, testing spaces, storage, security systems, industrial machinery, and distinct production zones.
- Cohesion rule: The location must directly justify the rogue-machine faction, technological loot, escalating defenses, and the mission's production-shutdown objective.
- Variety rule: Interior areas must use clearly differentiated visual and gameplay identities so the level does not become one repetitive factory corridor.
- Reuse rule: Factory machinery, props, turrets, doors, conveyors, hazards, and modular construction pieces should form a reusable industrial environment kit.

## D-144 — Shut down the production core

- Status: accepted
- Choice: A — shut down the factory's production core
- Accepted requirement: The first mission sends the player through the weapons factory to disable its production systems and ultimately shut down or destroy the central production core controlling the facility.
- Structure rule: Intermediate objectives may introduce distinct factory zones and progressively weaken or expose the core without requiring an elaborate cinematic narrative.
- Scope rule: Keep the mission premise direct and appropriate for the first level; do not require an extraction sequence, complex branching finale, or prototype-launch storyline for the internal MVP.

## D-145 — Level-one upgraded droid climax

- Status: accepted
- Choice: custom — a modest upgraded droid with somewhat more advanced shooting
- Accepted requirement: The first level ends with a level-appropriate upgraded droid rather than a giant, multi-stage, factory-wide spectacle.
- Combat rule: The upgraded droid is primarily distinguished by more advanced or combined shooting behaviour than ordinary enemies while remaining understandable as the first campaign climax.
- Scope rule: Do not require a complex multi-phase boss, large bespoke arena simulation, or extensive unique mechanics for level one.
- Inspiration note: Later enemy and boss design may draw broad inspiration from the readable mechanical encounters of `Robokill` and `Red Storm`, plus selected boss ideas from `SAS 4`, but exact designs are deferred to later detailed content work.

## D-146 — Four or five complementary enemy roles plus the upgraded droid

- Status: accepted
- Choice: B — four or five ordinary roles plus the level-one upgraded droid
- Accepted requirement: The first level uses a compact roster of approximately four or five complementary rogue-machine roles, followed by the upgraded droid climax.
- Encounter rule: Roles should combine into meaningfully different formations so encounter variety comes from composition, positioning, and environment rather than from producing a large number of enemies immediately.
- Production rule: Keep the roster small enough to animate, telegraph, balance, and test thoroughly within one shared mechanical asset pipeline.
- Deferred detail: Exact enemy concepts, statistics, attacks, and role assignments are decided later.

## D-147 — Six to eight authored MVP weapons

- Status: accepted
- Choice: B — six to eight authored weapons
- Accepted requirement: The one-level internal MVP contains approximately six to eight handcrafted base weapons. The player equips four simultaneously and may obtain or choose among the alternatives.
- Validation rule: The pool must be large enough to test loadout decisions, four-weapon combinations, loot rewards, shop choices, and replay-driven build adjustment.
- Scope rule: Additional weapons may be defined after the core arsenal, firing feel, and content pipeline prove successful.
- Deferred detail: Exact weapon list, family progression, balance values, and later complex archetypes remain future design work.

## D-148 — Identical base-weapon copies for the internal MVP

- Status: accepted
- Choice: A — base weapons only, without randomized item modifiers
- Accepted requirement: Every MVP copy of the same base weapon behaves identically. Randomized damage, fire rate, cooling, projectile-speed, handling, star, augment, and enchantment rolls are excluded from the first internal slice.
- Validation rule: Combat and loadout testing should reveal whether the underlying authored weapons are enjoyable without lucky item rolls obscuring their quality.
- Architecture rule: The item and save model may preserve a clean future extension path for stars, augments, enchantments, and per-instance statistics, but those systems are not implemented merely for the MVP.

## D-149 — Minimal real strongbox and randomized-shop loop

- Status: accepted
- Choice: B — minimal real strongbox and shop foundation
- Accepted requirement: The first level awards sealed strongboxes that resolve to one of the six-to-eight base weapons, and a small shop offers a randomized subset of those weapons for gameplay currency.
- Loop rule: The internal MVP must validate the connected `play → earn → open → compare → equip → replay` experience rather than using only deterministic unlocks or mock reward screens.
- Scope rule: Exclude stars, augments, enchantments, shop rerolls, dismantling, reservations, multiple elaborate strongbox tiers, and the broader mature economy from the immediate slice.
- Duplicate rule: Because copies are identical under D-148, duplicate handling may use the smallest real non-destructive solution necessary for the internal loop and can be expanded later.

## D-150 — Hybrid offline-3D and painted-2D art pipeline

- Status: accepted
- Choice: C — hybrid production pipeline
- Accepted requirement: Produce mechs, enemies, weapons, and major machinery from simple offline 3D models and animations rendered into two-dimensional sprites, while environments, projectiles, explosions, shadows, damage overlays, interface work, and finishing passes may use painted, procedural, or directly authored 2D techniques.
- Runtime rule: The shipped game remains fully two-dimensional; offline 3D is an asset-production method, not a runtime-rendering requirement.
- Consistency rule: Establish common perspective, lighting, scale, outline, palette, shadow, texture, and post-processing rules so rendered and painted assets appear to belong to one visual world.
- Scaling rule: The pipeline should support efficient directional animation, mounted weapons, enemy variants, damage states, future mech cosmetics, and additional machine content without requiring every visual element to use the same source method.

## Batch persistence

- Persisted through: D-150
- Unsaved decisions after checkpoint: 0
- Next batch boundary: D-160
