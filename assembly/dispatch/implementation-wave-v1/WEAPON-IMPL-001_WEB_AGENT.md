# WEAPON-IMPL-001 — Implement approved weapon families

## Objective

Implement the approved weapon-system plan as reusable behavior modules and authorable MK1/MK2/MK3 content for the agreed weapon families: blaster, shotgun, rocket launcher, flamethrower, sniper, fast rocket launcher, pulse shotgun, pulse rocket launcher, ricochet, fast sniper, heavy gatling, and chain weapon.

## Interactive protocol

Phase 1: inspect the completed weapon planning artifact and current four-mount/projectile/effect contracts. Present the shared pipeline, family-specific modules, data schema, balance table, and first demo subset.

Stop and wait for approval. Phase 2: implement the approved common runtime and the agreed first weapons. Later families must be additive.

## Acceptance

- Weapon definition identity remains separate from equipment instance identity.
- MK1/MK2/MK3 variants are data-driven.
- No giant family switch replaces reusable behavior modules.
- Existing four mounts remain functional.
- At least blaster, shotgun, flamethrower, and one launcher work in the demo.
- Projectile speed, range, spread, pierce, area damage, and damage-over-time are testable values.

## Validation

Focused EditMode behavior tests and PlayMode proof for the approved first subset, including visible projectiles and at least one status/effect behavior.
