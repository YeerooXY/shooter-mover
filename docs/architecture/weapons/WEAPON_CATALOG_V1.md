# WEAPON-DATA-001 — JSON weapon catalog V1

## Purpose

This package imports the evolving `weapon_baseline_v01.json` planning shape into a typed, engine-independent catalog. The reviewed V0.1 intake contains 44 families and 121 definitions, but neither importer nor validator contains a family or definition count invariant.

The top-level source sections are `version`, `status`, `rules`, `inputs`, `archetypes`, `families`, and `definitions`.

## Ownership and boundary

Production code is isolated to:

- `Assets/ShooterMover/Runtime/Domain/Weapons/Catalog/**`
- `Assets/ShooterMover/Runtime/Application/Weapons/Catalog/**`

Focused proof is isolated to `Assets/ShooterMover/Tests/EditMode/Weapons/Catalog/**`.

The package does not implement firing, projectiles, hit resolution, status effects, rewards, inventory mutation, crafting, strongbox generation, scenes, prefabs, or project settings.

## Typed fields

The catalog preserves rules, rarity inputs, family planning data, and authored weapon definition fields: identities and marks; archetype, damage type, affinity; appearance and drop levels; rarity and weights; tails and acquisition/crafting fields; fire rate, projectile count, burst count and damage per projectile; spread, speed, range and pierce; explosions, DoT, pools and chains; knockback, power cost and healing; primary effect, notes and optional side-profile art references.

Weapon damage is authored directly. The catalog does not prescribe a global base DPS, level growth curve, archetype DPS factor, target DPS, or direct/area/DoT damage shares. Display DPS is derived from the authored fire rate, projectile count, burst count and damage per projectile.

Older JSON may still contain the retired power and target-DPS fields. The importer tolerates those fields but ignores their values, and canonical export omits them.

## Live and preview content

`Availability` is an optional extension on family and definition objects and accepts `Live` or `PreviewOnly`. Missing availability defaults to `Live`, preserving compatibility with the supplied baseline. A preview-only family makes all of its definitions effectively preview-only; a live family may contain an individually preview-only definition.

Consumers select `WeaponCatalogContentFilter.LiveOnly`, `PreviewOnly`, or `All`. Simulator/editor tooling may use `All`; live drop composition must use `LiveOnly`.

Optional art can be authored as `SideProfileArtReference` or `SideProfileArtReferences`. Supplying both is rejected.

## Determinism

Imported archetypes, families and definitions are normalized with ordinal identity ordering. Catalog fingerprints are SHA-256 values over a length-prefixed canonical representation of active catalog fields, so input collection order cannot change catalog identity. Retired power and target-DPS fields do not affect the fingerprint.

`WeaponCatalogJson.Export` emits byte-stable compact JSON plus one trailing LF. Importing that export produces the same fingerprint and exporting it again produces identical bytes.

## Validation

The validator rejects missing data, malformed or duplicate IDs, duplicate family/mark pairs, family/definition mismatches, unknown families/archetypes/damage types/rarities, invalid authored stat ranges, invalid availability/art references, and drift in rarity metadata or final drop weight.

Weapon validity does not depend on a calculated DPS budget. Direct damage, area damage and DoT may be tuned independently as authored weapon behavior.

Floating derived drop-weight values use a relative tolerance of `1e-6`.

## Focused EditMode proof

Fixture:

```text
ShooterMover.Tests.EditMode.Weapons.Catalog.WeaponCatalogJsonTests
```

Coverage includes authored field preservation, canonical round trips, order-independent fingerprints, live/preview filtering, duplicate rejection, unsupported archetypes, range errors, derived drop-weight drift and a generated 45-family/135-definition catalog proving historical counts are not frozen.

Expected XML path under Unity `6000.3.19f1`:

```text
artifacts/test-results/WEAPON-DATA-001-EditMode.xml
```

A passing Unity claim requires that XML to report a completed run with zero failures.
