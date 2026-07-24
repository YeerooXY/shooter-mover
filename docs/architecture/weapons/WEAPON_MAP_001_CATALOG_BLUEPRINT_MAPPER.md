# WEAPON-MAP-001 — catalog to modular blueprint mapper

## Dependency

This mapper depends on the immutable modular contracts merged through PR #297. It starts from merge commit `ef642a14955c38608569597409e76132c74b418b` on `main`.

## Authority boundary

`WeaponCatalog` and `WeaponDefinitionData` remain the authored source. The mapper resolves definitions and families through those existing authorities and creates `WeaponBlueprint` without changing the JSON schema, `WeaponRuntimeProfile`, runtime execution, Unity adapters, or production composition.

The blueprint's `DropMetadataReference` is the exact existing `DefinitionId`. Rarity, weights, appearance/drop levels, acquisition, crafting, planning shares, and other catalog-only metadata therefore remain owned by and resolvable from the current catalog rather than being copied into a second authority.

## Canonical mapping policy

`WeaponCatalogBlueprintMappingIntent` is definition-bound policy, not an unrestricted override bag. It carries the existing `WeaponDefinitionId` that it authoritatively describes, and the mapper rejects a missing or mismatched ID before constructing any modular contract.

Production composition must obtain mapping intent from one authoritative resolver or policy registry keyed by `WeaponDefinitionId`. Arbitrary production callers must not manufacture competing intents for the same definition. The mapper remains pure and validates identity; ownership of the single canonical policy source remains an application-composition responsibility.

## Exact mappings

- `DefinitionId` -> existing `WeaponDefinitionId` with the exact source string.
- display name and family -> exact source values.
- `FireRate` -> `WeaponFireSettings.ShotsPerSecond` for projectile modes.
- `BurstCount` -> `ShotsPerBurst`.
- `ProjectilesPerTrigger` -> `WeaponShotPattern.ProjectilesPerShot`.
- spread -> the exact source angle, with explicit intent deciding whether it means authored spread or randomness.
- speed, range and integer pierce -> projectile fields; integer pierce expands exactly to tenths.
- direct, area, DoT, duration and knockback -> `WeaponDamageSpec`.
- explosion radius, chain targets and chain range -> effect descriptions, combined only with explicit missing semantics.
- presentation -> one exact authored definition/family reference; multiple choices require explicit selection.

## Explicit semantic intent

The current schema does not encode all modular meanings. `WeaponCatalogBlueprintMappingIntent` supplies only those missing choices:

- the expected existing `WeaponDefinitionId`;
- semi-auto, automatic, burst or continuous interpretation;
- explicit trigger-level shot-group count (`ShotsPerTrigger`), which the legacy schema does not store;
- shot-pattern and spread meaning;
- burst and pulse timing;
- projectile kind and termination behavior;
- explicit conversion for legacy damage strings that are not already Physical, Thermal, Chemical or Energy;
- unguided/homing parameters;
- impact, ricochet and explosion triggers;
- explosion falloff, DoT tick/stack behavior and chain retained damage;
- presentation selection when more than one reference is authored.

The intent cannot override numeric combat values already stored by `WeaponDefinitionData`.

## Explicit failures

Mapping returns typed diagnostics instead of substituting defaults. It rejects, among other cases:

- missing or mismatched intent definition identity;
- unknown definitions, families or archetypes;
- unknown damage strings without an explicit category mapping;
- invalid burst/count/pattern combinations;
- continuous interpretation of the current projectile-mandatory schema, because that would discard speed/range/projectile damage;
- missing homing/impact semantics;
- explosion, DoT or chain data without their missing behavior semantics;
- persistent pool and healing data because PR #297 has no corresponding modular effect contract yet;
- missing, unauthored or ambiguous presentation references.

## Fractional pierce boundary

Catalog integer pierce maps losslessly through `PierceValue.FromLegacyInteger`. This task does not map blueprints into `WeaponRuntimeFiringProfile`. Any later runtime adapter must use `PierceValue.TryToLegacyInteger` and reject fractional values when the conversion would lose information.

## Deferred

- catalog schema changes;
- the authoritative production policy registry/resolver for all catalog definitions;
- persistent pools and healing effects;
- continuous range/presentation contracts;
- `EffectiveWeapon` and augments;
- runtime profile and behavior selection;
- Unity, scenes, prefabs, simulator, strongboxes and production composition.
