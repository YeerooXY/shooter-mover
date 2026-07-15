# Stage 1 Weapon Packages v1

## Status and scope

This document defines the amended Stage 1 weapon-package authoring boundary for `WP-001`.
It consumes the existing Stable ID, Content Definitions, Generated Registry,
`WeaponRuntimeProfile`, independent power-bank, and modular weapon-behavior contracts.
It does not change those contracts and does not create a runtime weapon switch.

The baseline owns no concrete weapon package, tuning asset, final balance value, scene,
prefab, HUD, save data, reward rule, registry output, or generated catalog.

## Exact Stage 1 roster

Exactly these five weapon definition IDs are accepted:

| Stable ID | Working name | Required authoring topology | Default |
| --- | --- | --- | --- |
| `weapon.blaster-machine-gun` | Blaster Machine Gun | automatic projectile | yes |
| `weapon.shotgun` | Shotgun | bounded spread projectile | no |
| `weapon.rocket-launcher` | Rocket Launcher | one bounded area detonation | no |
| `weapon.arc-gun` | Arc Gun | primary hit plus at most three additional targets | no |
| `weapon.ricochet-gun` | Ricochet Gun | finite projectile with at most two 2D wall bounces | no |

Unknown IDs, missing IDs, duplicate IDs, a missing default, multiple defaults, or a
default other than Blaster Machine Gun invalidate the complete roster.

## Descriptor boundary

Each `Stage1WeaponPackageDescriptor` contains:

- one Content Definitions v1 weapon descriptor;
- the default-starting-weapon declaration;
- one normal-fire profile;
- one empowered-fire profile.

Each fire profile contains:

- one already-validated CB-001 `WeaponRuntimeProfile`;
- one explicit Stage 1 behavior topology;
- the consumable-ammunition declaration;
- a closed set of package-owned numeric behavior coefficients.

The content definition contributes package-owned inputs to the existing registry
contracts. `GetRegistryInputs()` is available only after the full five-package roster
validates. CS-011 remains the sole owner of generated registry files.

Every runtime behavior-module ID must be declared by an exact
`SharedModule` Content Reference v1 on the package definition. Extra, wrong-kind,
unsupported-version, or undeclared module references are rejected. This validates
authoring dependencies only. Runtime behavior is still resolved by explicit CB-004
module registration and is never selected by a central weapon-ID switch.

## Unlimited normal fire and independent power

Normal fire must declare `ConsumesConsumableAmmunition = false`, matching
`WeaponRuntimeProfile.NormalFireConsumesConsumable`.

Both normal and empowered runtime profiles must use the existing independent
per-mount power-bank policy. Empowered expenditure and depleted-bank fallback remain
owned by CB-003. A package descriptor does not add magazines, reserve ammunition,
shared ammunition, regeneration, or a second resource subsystem.

The CB-001 runtime-profile validator remains authoritative for numeric validity,
including positive empowered cost when an independent bank is configured. WP-001
does not duplicate those numeric bounds or choose final values.

## Numeric-only empowerment

Empowerment may change numeric values already present in the normal profile, including
accepted timing, resource, damage, projectile, spread, radius, range, or lifetime
coefficients. It may not change:

- the behavior family;
- the explicit topology values;
- the cycle mode;
- the ordered behavior-module IDs;
- the set of declared numeric coefficient kinds;
- the consumable-ammunition declaration.

The coefficient vocabulary is closed so a package cannot encode a new behavior flag,
second subsystem, target-selection rule, or randomized modifier as an arbitrary
number. Extending that vocabulary requires an explicit reviewed baseline amendment.

`RecoilInfluence` remains only an existing numeric declaration. This baseline grants
no player-movement authority and does not weaken the accepted CB-011 evidence boundary.

## Topology limits

The normal and empowered topology objects must be identical.

- **Blaster Machine Gun:** no chained targets, wall bounces, detonations, or fragmentation.
- **Shotgun:** no chained targets, wall bounces, detonations, or fragmentation. Bounded
  spread implementation belongs to the concrete package.
- **Rocket Launcher:** exactly one area detonation; no fragmentation, second detonation,
  chained target topology, or wall-bounce topology.
- **Arc Gun:** zero to three additional targets after the primary target; no wall
  bounces, detonations, or fragmentation.
- **Ricochet Gun:** zero to two 2D wall bounces; no additional-target chain,
  detonation, or fragmentation topology.

Changing a cap only in the empowered profile is both a cap violation and an empowered
topology change.

## Deterministic validation

`Stage1WeaponPackageValidator` returns immutable package and error collections in
canonical order. Input ordering cannot change the canonical result. Invalid results
cannot project registry inputs or expose an accepted default package.

Validation covers:

- exact roster identity and default identity;
- descriptor, content-definition, runtime-profile, and reference versions;
- provenance and release eligibility;
- unlimited normal fire and independent power-bank declarations;
- known behavior families and closed numeric coefficient kinds;
- duplicate, null, non-finite, newly added, or removed coefficients;
- empowered topology, cycle-mode, and module-order changes;
- Arc, Ricochet, and Rocket topology limits.

## Consumer checklist

A concrete WP-003 through WP-007 package must:

1. use its assigned stable weapon ID;
2. create one package-owned Content Definitions v1 descriptor;
3. declare every behavior module through exact Shared Module v1 references;
4. provide normal and empowered CB-001 runtime profiles;
5. keep topology, cycle mode, module order, and coefficient kinds identical;
6. mark only Blaster Machine Gun as the default;
7. leave normal fire free of consumable ammunition;
8. pass the complete five-package validator before registry generation.

## Non-goals and rollback

This baseline adds no concrete package, final balance, universal weapon switch,
generated output, serialized gameplay asset, combat-foundation edit, scene, UI,
enemy, mission, save, reward, networking, or backend behavior.

Before concrete package consumers land, rollback is the removal of the descriptor,
validator, tests, document, and their inseparable Unity metadata as one unit. No
registry regeneration, save migration, scene restoration, or combat-contract rollback
is required.
