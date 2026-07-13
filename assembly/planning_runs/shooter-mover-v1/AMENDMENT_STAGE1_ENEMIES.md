# Stage 1 Enemy Planning Amendment

**Status:** selected by the human lead; authoritative when this amendment pull request merges
**Scope:** representative Stage 1 enemies, first boss and resulting Stage 2 roster boundary
**Architecture impact:** none

## Decision

Replace the previously planned three ordinary Stage 1 roles and Foreman Elite with four deliberately simple ordinary enemies and one easy first boss:

1. `enemy.pursuer-drone` — **Pursuer Drone**, a basic durable-enough drone that walks directly toward the player and deals ordinary melee/contact damage.
2. `enemy.ram-droid` — **Ram Droid**, a smaller, faster and lower-health pressure enemy that deals one bounded impact hit and destroys itself when it collides with the player.
3. `enemy.mobile-blaster-droid` — **Mobile Blaster Droid**, a moving ranged enemy that fires the accepted Blaster Machine Gun projectile profile with simple readable timing.
4. `enemy.blaster-turret` — **Blaster Turret**, a stationary ranged enemy that fires the same accepted blaster projectile profile with a clear line-of-fire telegraph.
5. `enemy.four-blaster-elite` — **Four-Blaster Elite**, the Stage 1 first boss: a simple, approachable elite with four blasters and mild bounded spread that makes shots harder, but still fair, to dodge.

These are working production names and stable IDs, not final marketing copy. “Ram Droid” is an original project identity; “creeper-style” described only its disposable collision-pressure role.

## First-boss boundary

The Four-Blaster Elite replaces the Foreman Elite. It is intentionally easy to implement and approachable for a first-time player:

- one readable health model;
- no phase transition;
- no denial pulse, mortar attack, reinforcement system, teleport, complex repositioning or authored bullet-hell barrage;
- four blaster origins using the accepted projectile behavior;
- mild, bounded and telegraphed spread;
- simple deterministic cadence with safe recovery windows;
- color-independent warning and reduced-effects readability;
- no final-boss presentation or reward authority.

The Stage 2 `enemy.prototype-overseer` remains the upgraded-droid climax and is not replaced or reduced by this amendment.

## Stage boundaries

Stage 1 now contains four ordinary roles and the Four-Blaster Elite. The complete Stage 2 slice still targets five ordinary roles plus the elite and Prototype Overseer. This amendment does not select the one remaining Stage 2 ordinary role. That identity requires an evidence-backed planning amendment after the Stage 1 gate and before Stage 2 combat-content tasks are generated or dispatched.

## Task-splitting effect

- Keep the predeclared `EN-001` through `EN-013` task-ID range and redesign those cards around the amended roster.
- Reuse accepted movement, contact, combat-message, blaster-projectile, encounter and evidence contracts; do not create a parallel enemy combat architecture.
- Keep enemy packages independently owned and keep the EH-005 short-route shell read-only.
- Do not generate `stage1-enemies-route.json` until this amendment PR is approved and merged.
- Preserve the S1.3 ten-day cap and reserve explicit human review for fairness, pacing, telegraphs and readability.

This amendment supersedes the Stage 1 roster/count selected in D-208 and the corresponding planning content selection; it does not rewrite the historical decision log.
