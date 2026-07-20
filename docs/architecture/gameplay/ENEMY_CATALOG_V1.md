# ENEMY-DATA-001 — Versioned enemy definition catalog

## Purpose

`EnemyCatalogV1` is the engine-neutral content boundary between authored enemy data and the later placement-to-runtime factory. It describes enemy content; it does not instantiate actors, mutate health, execute attacks, award experience, or roll drops.

The exact task launch point is `af83d72e80d216dbe78678754d6a66189967127f` on `main`. The implementation branch is `agent/enemy-data-001-definition-catalog` and the draft review is PR #257.

## Ownership boundary

- `EnemyDefinitionV1` owns immutable authored facts shared by all of one enemy definition's attacks, including health, faction identity, detection radius, and vision arc.
- `EnemyAttackCapabilityDescriptorV1` owns complete immutable eligibility and execution-input facts for one attack: deterministic priority, attack arc, minimum/preferred/maximum range, cooldown, damage, damage channel, and typed projectile/area/melee parameters.
- `EnemyCatalogValidatorV1` validates syntax, numerical bounds, per-attack geometry, compatible parameter shapes, and registry references.
- `EnemyCatalogJsonImporterV1` converts JSON into typed definitions and returns path-specific diagnostics without a partial catalog.
- Existing enemy actor and decision authorities continue to own live health, lifecycle, target selection, committed intent, and attack admission.
- Existing weapon/projectile execution, XP, and drop systems remain downstream owners. The catalog stores stable profile references and content facts only.
- `ENEMY-FACTORY-001` will resolve one room placement plus one catalog definition into an independent live actor. This task deliberately performs no such composition.

## Schema and canonical ordering

The root document contains `schema_version`, canonical `content_version`, and one or more `definitions`. Schema version 1 is currently supported; unsupported schemas reject.

Each accepted catalog exposes schema/content versions, definitions sorted by canonical enemy ID, and a SHA-256 fingerprint over a canonical representation. Each definition also exposes its own fingerprint.

Attack array position is not authoritative. Every attack provides a unique non-negative `selection_priority`; lower values are evaluated first, with attack ID as the deterministic canonical tie-break used for serialization/fingerprinting. Duplicate priorities reject. After successful validation, the catalog exposes attacks in canonical priority/ID order. Definition order, authored attack-array order, and special-capability order do not affect fingerprints; semantic value changes do.

## Definition fields

An enemy definition contains:

- stable enemy and presentation IDs;
- base health and bounded level scaling;
- a canonical faction StableId;
- shared detection radius and vision arc;
- movement and decision policy IDs;
- one or more attack descriptors;
- experience and drop profile IDs;
- room-clear role;
- zero or more reusable special-capability IDs.

There is no definition-wide attack geometry. This prevents two competing sources of truth when an enemy has multiple attacks.

Every attack descriptor contains:

- stable attack and capability IDs;
- unique deterministic `selection_priority`;
- attack arc in `(0, 360]`;
- finite, ordered minimum/preferred/maximum ranges within the definition's detection radius;
- cooldown, damage, and damage-channel ID;
- optional typed parameter blocks:
  - `projectile`: registered profile ID, count, speed, maximum travel distance, collision radius, spread, and pierce;
  - `area`: radius, duration, and bounded maximum targets;
  - `melee`: contact radius, pounce distance, wind-up, and commitment duration.

Projectile travel distance must support that attack's own maximum range. Melee/contact reach is `contact_radius + pounce_distance` and must support that attack's own maximum range.

## Registry resolution and fail-closed behavior

`IEnemyCatalogRegistryV1` resolves:

- movement policies;
- decision policies;
- attack capabilities;
- special capabilities;
- presentations;
- projectile profiles;
- damage channels;
- experience profiles;
- drop profiles.

Unknown references reject the whole import. Projectile profile failures use `enemy-catalog-projectile-profile-unknown` at the exact `definitions[i].attacks[j].projectile.profile` path. A malformed projectile profile StableId fails during mapping at that same field path. Attacks without a projectile parameter block do not require a projectile profile.

Attack registrations declare required and allowed parameter flags. A projectile cannot silently load as a pounce, and a projectile-area capability cannot omit either required block.

### Faction boundary

The enemy/combat foundations inspected for this task do not expose one canonical faction registry. Faction values therefore remain intentionally open canonical StableIds and are not resolved through a new catalog-owned registry. This avoids inventing a second faction authority. A later canonical faction registry may be adapted into validation without changing the authored faction identity.

## Validation policy

Validation rejects malformed IDs; unsupported schemas; empty, excessive, null, or duplicate definitions; missing presentations; invalid health/scaling; invalid detection or vision facts; unknown registry references; duplicate attack IDs; duplicate/out-of-range selection priorities; invalid per-attack arcs/ranges; maximum ranges outside detection; insufficient projectile travel; incompatible melee reach; non-positive cooldown/damage; incompatible parameter blocks; malformed projectile/area/melee values; and unsupported room-clear roles.

Vision arc remains definition-wide perception. Attack arc remains per-attack eligibility. Both participate independently in canonical fingerprints.

## Fixture content

`enemy_catalog_v1.json` proves five reusable compositions:

1. Mobile Blaster Droid — mobile ranged projectile.
2. Ram Pouncer — pursuing melee/pounce with locked commitment.
3. Blaster Turret — stationary projectile-plus-area attack with rotating aim.
4. Pursuer Drone — pursuing contact attack and optional room-clear role.
5. Hybrid Sentinel — one close contact attack and one ranged projectile attack with different priorities, arcs, and ranges.

The hybrid fixture intentionally authors the higher-priority ranged entry before the lower-priority contact entry. Successful import exposes the canonical priority order and produces the same fingerprint when those JSON entries are reversed.

The fixture is content proof only. It is not loaded into Stage 1 by this task.

## Focused verification

Run from a Unity `6000.3.19f1` checkout:

```text
D:\6000.3.19f1\Editor\Unity.exe -batchmode -nographics -projectPath . -runTests -testPlatform EditMode -testFilter ShooterMover.Tests.EditMode.Enemies.EnemyCatalogJsonImporterV1Tests -testResults artifacts/enemy-data-001-editmode.xml -logFile artifacts/enemy-data-001-editmode.log
```

The focused suite covers known/unknown/malformed projectile profiles, non-projectile attacks without projectile registration, mixed melee/ranged definitions, per-attack arc/range/travel/reach validation, duplicate priorities, authored attack-order fingerprint independence, definition-order independence, fail-closed behavior, and the original ranged/pounce/turret/pursuit fixtures.

## Known limitations

- No live actor construction or Stage 1 composition change is included.
- No projectile execution or presentation logic is implemented here.
- No Unity presentation resolver is implemented here; later composition supplies the registry.
- XP and drop profile IDs are references only. Definitions never award XP, roll drops, or mutate inventory.
- Fixture values are structural examples, not final production balance.
