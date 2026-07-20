# ENEMY-DATA-001 — Versioned enemy definition catalog

## Purpose

`EnemyCatalogV1` is the engine-neutral content boundary between authored enemy data and the later placement-to-runtime factory. It describes enemy content; it does not instantiate actors, mutate health, execute attacks, award experience, or roll drops.

The exact task launch point is `af83d72e80d216dbe78678754d6a66189967127f` on `main`. The implementation branch is `agent/enemy-data-001-definition-catalog`.

## Ownership boundary

- `EnemyDefinitionV1` owns immutable authored facts.
- `EnemyCatalogValidatorV1` validates syntax, numerical bounds, compatible attack parameter shapes, and all registry references.
- `EnemyCatalogJsonImporterV1` converts JSON into typed definitions and returns path-specific diagnostics without a partial catalog.
- Existing enemy actor and decision authorities continue to own live health, lifecycle, target selection, committed intent, and attack admission.
- Existing weapon execution, XP, and drop systems remain downstream owners. The catalog stores only stable capability/profile references and numerical attack content.
- `ENEMY-FACTORY-001` will resolve one room placement plus one catalog definition into an independent live actor. This task deliberately performs no such composition.

## Schema and versions

The root document contains `schema_version`, canonical `content_version`, and one or more `definitions`. Schema version 1 is currently supported; unsupported schemas reject.

Each accepted catalog exposes schema/content versions, definitions sorted by canonical enemy ID, and a SHA-256 fingerprint over a canonical order-independent representation. Each definition also exposes its own fingerprint. Definition order, attack order, and special-capability order do not affect fingerprints; semantic value changes do.

## Definition fields

An enemy definition contains:

- stable enemy and presentation IDs;
- base health and bounded level scaling;
- faction;
- detection radius and vision arc;
- a separate attack arc plus minimum, preferred, and maximum attack ranges;
- movement and decision policy IDs;
- one or more attack descriptors;
- experience and drop profile IDs;
- room-clear role;
- zero or more reusable special-capability IDs.

Attack descriptors contain stable attack/capability IDs, cooldown, damage, damage channel, and optional typed parameter blocks:

- `projectile`: profile, count, speed, maximum travel distance, collision radius, spread, and pierce;
- `area`: radius, duration, and bounded maximum targets;
- `melee`: contact radius, pounce distance, wind-up, and commitment duration.

## Registry resolution and fail-closed behavior

`IEnemyCatalogRegistryV1` resolves movement policies, decision policies, attack capabilities, special capabilities, presentations, damage channels, experience profiles, and drop profiles. Unknown references reject the whole import.

Attack registrations declare required and allowed parameter flags. A projectile cannot silently load as a pounce, and a projectile-area capability cannot omit either required block.

This keeps the catalog open to new IDs without enemy-type switches. A new enemy using registered mechanics requires definition data, a registered presentation asset/reference, a room placement, and focused content coverage—not a new production enemy class.

## Validation policy

Validation rejects malformed IDs; unsupported schemas; empty, excessive, null, or duplicate definitions; missing presentations; invalid health/scaling; invalid ranges/arcs; unknown registry references; duplicate attacks; non-positive cooldown/damage; incompatible parameter blocks; malformed projectile/area/melee values; and unsupported room-clear roles.

Vision and attack arcs are deliberately independent fields and both participate in the canonical fingerprint.

## Fixture content

`enemy_catalog_v1.json` proves four reusable compositions:

1. Mobile Blaster Droid — mobile ranged projectile.
2. Ram Pouncer — pursuing melee/pounce with locked commitment.
3. Blaster Turret — stationary projectile-plus-area attack with rotating aim.
4. Pursuer Drone — pursuing contact attack and optional room-clear role.

The fixture is content proof only. It is not loaded into Stage 1 by this task.

## Focused verification

Run from a Unity `6000.3.19f1` checkout:

```text
D:\6000.3.19f1\Editor\Unity.exe -batchmode -nographics -projectPath . -runTests -testPlatform EditMode -testFilter ShooterMover.Tests.EditMode.Enemies.EnemyCatalogJsonImporterV1Tests -testResults artifacts/enemy-data-001-editmode.xml -logFile artifacts/enemy-data-001-editmode.log
```

The suite covers all four fixture styles, duplicate IDs, malformed ranges, unknown movement/attack capabilities, independent vision/attack arcs, order-independent fingerprints, missing presentations, incompatible attack blocks, malformed IDs, and a new definition-only enemy using existing mechanics.

## Known limitations

- No live actor construction or Stage 1 composition change is included.
- No Unity presentation resolver is implemented here; later composition supplies the registry.
- XP and drop profile IDs are references only. Definitions never award XP, roll drops, or mutate inventory.
- Fixture values are structural examples, not final production balance.
