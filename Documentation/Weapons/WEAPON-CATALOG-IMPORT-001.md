
# WEAPON-CATALOG-IMPORT-001

## Scope

This task imports the migration snapshot as one canonical catalog shared by production
and the authoritative strongbox simulator. The snapshot contains exactly 121 weapon
definitions in 44 authored families. It is intentionally not the final per-weapon
runtime-behavior authoring structure; later work can compose reusable firing strategies
with individual weapon-definition data without changing these authored values.

## Production loading

The build-included source is the Unity `TextAsset` at:

`Assets/ShooterMover/Resources/WeaponCatalog/weapon_baseline_v01.json`

The production account composition reads it through
`ResourcesWeaponCatalogSourceV1` at Resources key
`WeaponCatalog/weapon_baseline_v01`, then passes the resulting
`CanonicalWeaponCatalogProjectionV1` as a typed dependency to the character runtime
graph factory. Application and Domain code remain engine-neutral. Repository-relative
filesystem loading exists only in the Editor/test source adapter.

## Authored combat statistics

Weapon combat statistics are exact authored values belonging to the weapon definition.
Character level, equipment item level, and strongbox level do not multiply or otherwise
rewrite damage, rate of fire, projectile speed, range, spread, projectile count,
explosion radius, damage-over-time values, or any other firing statistic. Equipment
item level remains progression and presentation metadata only.

## Rarity normalization and equipment quality

| Source rarity | Normalized rarity | Equipment quality |
|---|---|---|
| Common | Common | `equipment-quality.common` |
| Uncommon | Common | `equipment-quality.common` |
| Rare | Rare | `equipment-quality.rare` |
| Epic | Epic | `equipment-quality.epic` |
| Legendary | Legendary | `equipment-quality.legendary` |
| Mythic | MythicArtifact | `equipment-quality.mythic-artifact` |
| Artifact | MythicArtifact | `equipment-quality.mythic-artifact` |

Each projected weapon definition has exactly one authored quality. Strongboxes select a
weapon definition; they do not reroll its rarity.

## Definition drop weighting

Availability and `TopBoxOnly` are the hard gates. Level acquisition is a deterministic
fixed-point soft curve:

- `PeakDropLevel` is the maximum-weight point.
- `EarlyTail` shapes the approach to the peak and the additional tail below
  `FirstAppearance`.
- `LateTail` shapes the persistent post-peak tail.
- `FinalBaseWeight` scales the definition.
- normalized rarity and strongbox tier enter through the tier rarity multiplier.
- every otherwise eligible definition receives at least one fixed-point weight unit.

This makes distant drops extraordinarily rare without silently converting valid tails
to zero. Identical catalog fingerprints, box identity, seed, and context replay the same
exact equipment instance.

## Runtime packages

The content projection contains all 121 definitions and no implementation-status list.
`ProductionWeaponRuntimePackageRegistryV1` currently registers the five exact packages:

- `blaster.mk1`
- `shotgun.mk1`
- `rocket_launcher.mk1`
- `chain_weapon.mk1`
- `ricochet_weapon.mk1`

A missing exact package fails closed with
`weapon-runtime-behavior-pending:<exact-definition-id>` and never substitutes another
weapon.

## Augments

Fresh strongbox equipment contains zero installed `AugmentInstance` values. Existing
signature authority may retain rolled slot capacity and shared augment-level metadata;
those values are not installed augments.

## Validation status

The PR remains draft. Unity compilation and focused tests require the unrelated current
`main` assembly-direction blocker to be repaired first:
`PersonalRewardGenerationResultV1` is in Domain while depending on Contracts
`RewardGrantV1`. This task does not broaden into that reward refactor.
