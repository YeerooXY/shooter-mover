# WEAPON-ARCH-001 — Modular weapon contracts

## Scope

This change adds immutable, engine-independent vocabulary for the future modular weapon architecture. It does **not** replace the current catalog importer, equipment model, firing scheduler, behavior registry, deterministic effect batch, Unity projectile presentation, scenes, strongboxes, simulator, production composition, or weapon catalog content.

The new contracts are deliberately parallel to the active authorities so migration can happen in reviewed steps rather than through a second live runtime.

## 1. Current authorities

### `Runtime/Domain/Combat/WeaponRuntimeProfile.cs`

`WeaponRuntimeProfile` is an immutable legacy runtime-tuning aggregate. It currently owns cadence, burst timing, post-cycle recovery, heat/charge/power-bank fields, recoil influence, behavior module IDs, presentation priority, canonical serialization, fingerprinting, and deterministic identity. It remains supported as a migration source, but it is not the target weapon definition model.

### `Runtime/Domain/Weapons/Catalog/WeaponCatalogModel.*`

The JSON-backed catalog is the current authored weapon-data authority. `WeaponDefinitionData` combines identity, family, drop/progression metadata, rarity, art references, combat statistics, power/crafting data, and effect parameters. `WeaponCatalogRules`, `WeaponCatalogInputs`, archetypes, families, validation, fingerprinting, and filtering remain unchanged.

### `Runtime/Application/Weapons/Catalog/*`

The application catalog layer remains the JSON DTO/import/export boundary. It still maps string damage values and the current flat catalog fields into `WeaponCatalog`. WEAPON-ARCH-001 does not redirect import into `WeaponBlueprint` yet.

### `Runtime/Domain/Equipment/EquipmentModel.cs`

`EquipmentDefinition`, `EquipmentInstance`, `AugmentDefinition`, and `AugmentInstance` remain the inventory/equipment authorities. Equipment instances own item level, quality, and installed augment instances. None of those values are installed into or used to mutate a canonical `WeaponBlueprint`.

### `Runtime/Application/Weapons/Execution/WeaponExecutionContracts.cs`

`WeaponBehavior`, `WeaponBehaviorRegistry`, execution commands, behavior context, and effect sinks remain the application execution contracts. No second behavior registry or weapon-specific runtime class hierarchy is introduced.

### `Runtime/Domain/Weapons/Execution/WeaponExecutionModel.cs`

`WeaponDefinitionId`, `EquipmentInstanceId`, `WeaponRuntimeFiringProfile`, and execution identities remain unchanged. The new blueprint reuses the existing `WeaponDefinitionId`; it does not create a duplicate identity type. The integer `WeaponRuntimeFiringProfile.Pierce` remains supported until a later mapper can explicitly reject or resolve fractional pierce.

### `Runtime/Domain/Weapons/Execution/WeaponEffectBatch.cs`

`WeaponEffectBatch` remains the deterministic, immutable execution boundary consumed by application sinks and Unity adapters. The new `WeaponEffects` descriptions are definition-time data only and do not replace emitted execution effects.

### `Runtime/UnityAdapters/Weapons/Live/*`

The current Unity adapters continue to consume `WeaponEffectBatch`/`IWeaponEffectDescription`, stage atomic batches, create projectile GameObjects, and perform Unity-specific travel and presentation. The new domain contracts contain no `UnityEngine` references and are not discovered from scenes.

### `Runtime/Application/Flow/Production/ProductionStarterWeaponCatalogV1.cs`

The production starter catalog remains an intentionally empty transitional boundary. WEAPON-ARCH-001 does not add starter weapons, production definitions, grants, or composition changes.

## 2. New modular contract ownership

### `WeaponBlueprint`

Owns only canonical definition data:

- existing stable `WeaponDefinitionId`;
- display name;
- weapon family string used by current catalog migration;
- `WeaponFireSettings`;
- `WeaponShotPattern`;
- optional `WeaponProjectileSpec`;
- `WeaponGuidanceSpec`;
- `WeaponImpactSpec`;
- `WeaponDamageSpec`;
- `WeaponEffects`;
- a drop-metadata reference;
- a presentation/art reference.

It does not own runtime cooldown state, trigger state, projectile instances, inventory identity, item level, quality, installed augments, heat, charge, ammo, power banks, or scene objects.

### `WeaponFireSettings`

Separates semi-automatic, automatic, burst, and continuous cadence. Projectile modes author `ShotsPerSecond`, `ShotsPerTrigger`, `ShotsPerBurst`, the interval between burst shots, and the interval after the burst. Continuous mode requires `DamageTicksPerSecond` and requires all projectile cadence fields to remain zero.

`ShotsPerBurst` is intentionally independent from `WeaponShotPattern.ProjectilesPerShot`. A burst shotgun can therefore author three sequential shots per burst and eight projectiles per shot without conflating the two dimensions.

### `WeaponShotPattern`

Describes single, spread, pulse spread, twin barrel, volley, beam, and spray emission. It owns projectile count per shot, authored spread, random angular variation, pulse count, and pulse interval. Pattern-specific validation rejects missing projectile counts and invalid pulse/spread combinations.

### `WeaponProjectileSpec` and `PierceValue`

`WeaponProjectileSpec` describes regular projectiles, rockets, and orbs with speed, range, fixed-point pierce, and termination behavior.

`PierceValue` stores tenths. It exposes guaranteed hits and fractional additional-hit chance. `FromLegacyInteger` is exact. `TryToLegacyInteger` fails when conversion would discard a fractional chance, creating an explicit migration boundary instead of rounding silently.

### `WeaponGuidanceSpec`

Separates unguided and homing delivery. Homing data includes acquisition range, turn rate, activation delay, target policy, and reacquisition policy. Blueprint validation rejects homing without a projectile.

### `WeaponImpactSpec`

Describes supported enemy-impact, wall-impact, range-expiry, and termination events. Optional ricochet data requires wall-impact handling. Optional explosion triggers must be a subset of the impact events the weapon handles.

### `WeaponDamageSpec`

Owns typed `Physical`, `Thermal`, `Chemical`, or `Energy` damage plus direct, area, damage-over-time, and knockback magnitudes. `WeaponDamageCategoryConversion` performs exact case-sensitive conversion from the current catalog strings. Unknown or null strings return `false` from `TryFromCatalogValue` and throw from `FromCatalogValue`; they are never reinterpreted as another damage category.

### `WeaponEffects`

Owns optional reusable definition-time descriptions for explosion, damage over time, and chain arc. These descriptions contain no Unity behavior. Blueprint validation rejects explosion trigger/area-damage data without an explosion effect and damage-over-time magnitude/duration without a damage-over-time effect.

## 3. Field-by-field mapping from `WeaponRuntimeProfile`

| `WeaponRuntimeProfile` field | Modular destination | Migration rule |
|---|---|---|
| `ProfileVersion` | Migration envelope, not `WeaponBlueprint` | Retain in the legacy serializer until a blueprint serialization version is designed. |
| `ProfileId` | Lookup input toward `WeaponDefinitionId` | Do not copy blindly. Resolve the canonical weapon definition through the catalog/equipment mapping. |
| `CadenceSeconds` | `WeaponFireSettings.ShotsPerSecond` | For projectile modes, convert explicitly as `1 / CadenceSeconds` after validating a positive finite interval. Never map it to continuous damage ticks. |
| `BurstShotCount` | `WeaponFireSettings.ShotsPerBurst` | Preserve sequential burst-shot count. Do not map it to `ProjectilesPerShot`. |
| `BurstShotIntervalSeconds` | `WeaponFireSettings.IntervalBetweenBurstShotsSeconds` | Direct timing mapping for burst mode. |
| `RecoverySeconds` | `WeaponFireSettings.IntervalAfterBurstSeconds` or later scheduler policy | Use as post-burst interval when the legacy profile is genuinely burst fire. Non-burst recovery needs an explicit mapper decision rather than implicit reuse. |
| `CycleMode` | Deferred | Heat and charge resource behavior is intentionally outside WEAPON-ARCH-001. |
| `HeatCapacityUnits` | Deferred | No heat system in the new contracts yet. |
| `HeatPerShotUnits` | Deferred | No heat system in the new contracts yet. |
| `HeatRecoveryUnitsPerSecond` | Deferred | No heat system in the new contracts yet. |
| `ChargeSeconds` | Deferred | No charge system in the new contracts yet. |
| `HasIndependentPowerBank` | Deferred | No power-bank/resource system in the new contracts yet. |
| `PowerBankCapacityUnits` | Deferred | No power-bank/resource system in the new contracts yet. |
| `EmpoweredCostUnits` | Deferred | No empowered-fire/resource system in the new contracts yet. |
| `RecoilInfluence` | Deferred movement/effective-profile input | Not added to the canonical combat contracts in this task. It must not be silently dropped by the eventual mapper. |
| `BehaviorModuleIds` | Existing `WeaponBehaviorRegistry` selection/mapping | Keep the registry authority. A later mapper may select existing behaviors from modular data; do not duplicate behavior types. |
| `PresentationPriority` | Transitional presentation policy | The blueprint owns a presentation reference, not Unity priority behavior. Preserve priority in the legacy path until presentation migration is defined. |
| `Fingerprint` | Legacy compatibility | Continue using the existing profile fingerprint while the legacy profile is authoritative. |
| `DeterministicIdentity` | Legacy compatibility | Do not substitute it for `WeaponDefinitionId`; retain only for legacy profile identity/replay compatibility. |
| canonical text | Legacy compatibility | Blueprint serialization/canonical fingerprinting is intentionally deferred. |

## Current catalog to modular mapping boundary

The current flat catalog remains authoritative. A later application mapper should perform explicit mapping, including:

- `DefinitionId`, `DisplayName`, `FamilyId` -> blueprint identity/name/family;
- rarity, weights, tails, acquisition, top-box, crafting, first/peak levels -> separate drop metadata referenced by the blueprint;
- side-profile art references -> presentation reference selection;
- `FireRate`, `BurstCount` -> `WeaponFireSettings` after mode interpretation;
- `ProjectilesPerTrigger` -> `WeaponShotPattern.ProjectilesPerShot` only after confirming current semantic intent;
- `SpreadDegrees` -> `WeaponShotPattern`;
- `ProjectileSpeed`, `Range`, integer `Pierce` -> `WeaponProjectileSpec`, using `PierceValue.FromLegacyInteger`;
- `DamageType` -> `WeaponDamageCategoryConversion` with unknown-string rejection;
- direct, area, DoT, radius, chain, and knockback values -> `WeaponDamageSpec` plus matching `WeaponEffects` and impact triggers;
- `PowerCost`, healing, progression calculations, and other resource fields -> deferred, never silently folded into combat fields.

## 4. Temporarily supported systems

The following remain active and unchanged:

- `WeaponRuntimeProfile` canonical parsing/fingerprinting;
- JSON `WeaponCatalog` import/export/validation;
- flat `WeaponDefinitionData` values;
- `WeaponRuntimeFiringProfile`, including integer pierce and string damage type;
- `WeaponBehaviorRegistry` and built-in behaviors;
- `EquipmentDefinition`, `EquipmentInstance`, `AugmentDefinition`, and `AugmentInstance`;
- `WeaponEffectBatch` and current effect descriptions;
- Unity live projectile/effect adapters;
- empty production starter catalog composition.

## 5. Intentionally deferred

- catalog JSON schema migration to modular contracts;
- complete weapon catalog conversion;
- `EffectiveWeapon` construction;
- augment stat application and conflict resolution;
- item-level behavior (item level must not scale canonical combat statistics);
- heat, charge, ammo, magazines, cooldown resources, and power banks;
- recoil/movement aggregation;
- weapon-specific Unity behavior;
- scene discovery, reflection, service locators, or production composition changes;
- simulator, strongbox, drop-table, and rarity migrations;
- canonical blueprint serialization/fingerprinting;
- fractional-pierce execution and Unity impact behavior.

## 6. Migration path toward `EffectiveWeapon`

1. Keep `WeaponBlueprint` immutable and catalog-owned.
2. Add an application mapper from validated `WeaponDefinitionData` to `WeaponBlueprint`. Every ambiguous current field must produce an explicit mapping decision or validation failure.
3. Add a temporary adapter from `WeaponBlueprint` to the existing `WeaponRuntimeFiringProfile`/behavior selection boundary. Fractional pierce must fail until execution contracts support it.
4. Introduce `EffectiveWeapon` in a later task as an immutable derived result of:
   - one canonical `WeaponBlueprint`;
   - one existing `EquipmentInstance`;
   - validated existing augment definitions/instances;
   - explicit runtime-context inputs that are allowed to affect combat.
5. Never mutate or install augments into `WeaponBlueprint`. Cache or rebuild `EffectiveWeapon` when equipment changes.
6. Keep item level, rarity, and drop metadata outside combat-stat scaling unless a later architecture decision explicitly introduces a separate supported rule.
7. Migrate execution behavior to consume `EffectiveWeapon`, then remove legacy flat/runtime profile ownership only after every live adapter and deterministic replay path has moved.

## 7. `WeaponEffectBatch` remains the deterministic execution boundary

`WeaponBlueprint`, `WeaponEffects`, and future `EffectiveWeapon` are definition/evaluation data. They do not directly spawn Unity objects or apply damage.

The execution path must continue to produce one immutable `WeaponEffectBatch` with stable effect identities and deterministic ordering. Application sinks retain acceptance/idempotency control, and Unity adapters remain consumers of that batch. This preserves the current transaction-like boundary while allowing the upstream weapon definition to become modular.

A future mapper/behavior implementation may translate modular descriptions into the existing direct projectile, explosive projectile, damage-over-time projectile, and chain-arc execution effects. It must not bypass `WeaponEffectBatch`, discover scenes, or create a second live effect authority.
