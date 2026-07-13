# Stage 1 Weapon Planning Amendment

**Status:** selected by the human lead; authoritative when this amendment pull request merges
**Scope:** representative Stage 1 weapon content only
**Architecture impact:** none

## Decision

Replace the previously planned six-weapon Stage 1 roster with exactly five first-proof weapons:

1. `weapon.blaster-machine-gun` — **Blaster Machine Gun**, the default starting weapon and straightforward automatic-fire reference.
2. `weapon.shotgun` — **Shotgun**, a close-range multi-projectile spread weapon.
3. `weapon.rocket-launcher` — **Rocket Launcher**, a paced projectile with a simple bounded area-of-effect detonation.
4. `weapon.arc-gun` — **Arc Gun**, an original chain-lightning weapon that may jump to at most three additional nearby targets after the primary target.
5. `weapon.ricochet-gun` — **Ricochet Gun**, a long-lived projectile weapon whose shots may bounce from walls at most twice before expiring.

These are working production names and stable IDs, not final marketing copy. The Arc Gun borrows only the broad idea of nearby-target chaining; it must use original naming, presentation, tuning, code, and assets.

## Empowered-fire boundary

The accepted unlimited-normal-fire and independent per-mount power-bank architecture remains unchanged. Holding the power modifier may apply an authored numeric empowered profile to each eligible weapon. For this first proof, empowerment may tune existing values such as damage, cadence or recovery, projectile speed, spread, area radius, or other already-modeled numeric coefficients.

Empowerment must not add a second bespoke behavior, new target-selection topology, randomized modifiers, or a new shared subsystem. In particular:

- Arc Gun remains capped at three additional chained targets in both normal and empowered fire.
- Ricochet Gun remains capped at two wall bounces in both normal and empowered fire.
- Rocket Launcher empowerment may tune its existing explosion values but may not add fragmentation or a second detonation system.
- The empowered state, expenditure, depletion, and fallback still require clear HUD and audiovisual feedback even though the gameplay change is numeric.

## Stage boundaries

Stage 1 fixed-loadout and randomized-wrapper evidence draws from these five weapons. Four remain equipped simultaneously.

The complete Stage 2 slice still targets eight identical-copy base weapons under the accepted economy boundary. This amendment does not select the remaining three weapon identities. They require an evidence-backed planning amendment after the Stage 1 gate and before Stage 2 combat-content tasks are generated or dispatched.

## Task-splitting effect

- Keep the predeclared `WP-001` through `WP-012` task-ID range; redesign those twelve cards around five packages, shared bounded behavior modules, presentation/readability, and evidence.
- Do not generate `stage1-weapons.json` until this amendment PR is approved and merged.
- Do not alter the merged combat architecture or implement game code in this amendment.
- Preserve the S1.2 ten-day cap and its current 5.50 focused-lead-day remaining reserve.
