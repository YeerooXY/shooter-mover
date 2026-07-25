# WEAPON-DATA-002A — Canonical authored weapon contracts

## Status

Implemented contract boundary for `WEAPON_DATA_002_AUTHORED_WEAPON_DELIVERY_MODEL.md`.

This change defines and validates canonical authored weapon content. It does **not** implement the complete weapon catalogue migration, strongbox raffle, shop/opening flow, account migration, or new Unity projectile components.

## Exact authority

`ShooterMover.Domain.Weapons.WeaponBlueprint` is the canonical immutable authored weapon definition.

New authored content is created through:

```csharp
WeaponBlueprint.CreateAuthored(
    identity,
    fireSettings,
    shotPattern,
    baseStats,
    delivery,
    presentation,
    dropMetadata)
```

The canonical groups are:

```text
WeaponBlueprint
├── Identity
├── FireSettings
├── ShotPattern
├── BaseStats
├── Delivery
├── Presentation
└── DropMetadata
```

The display name and family/category text are identity and presentation data only. They never select behaviour.

## Reused authorities

The implementation deliberately reuses the existing contracts for:

- `WeaponDefinitionId` and stable domain identities;
- `WeaponFireSettings` and `WeaponShotPattern`;
- `PierceValue` fixed-point tenths;
- `WeaponGuidanceSpec`;
- `WeaponImpactSpec` and `WeaponExplosionTriggerSpec`;
- `WeaponEffects`, explosion, DoT execution policy, and chain effects;
- `WeaponBehaviorId` and the existing behaviour registry;
- `EffectiveWeapon`, `EffectiveWeaponFactory`, and the existing modifier evaluator;
- `WeaponFiringScheduler`;
- generic projectile, guidance, impact, effect-batch, and deterministic-random authorities;
- `EquipmentInstance` and `AugmentInstance`;
- `GeneratedEquipmentAugmentSignatureV1` and its authority for rolled capacity/shared level.

No second scheduler, projectile hierarchy, guidance authority, impact authority, effect authority, random service, equipment model, augment model, or behaviour registry was added.

## Fire and shot semantics

Canonical authored fire modes are:

- semi-automatic;
- automatic;
- burst.

`RateOfFire` is firing cycles per second. It is retained as an alias over the scheduler's existing `ShotsPerSecond` value so there is no second cadence authority.

Canonical burst data is represented by `WeaponBurstSettings`:

- `ShotsPerBurst` — sequential shots inside one firing cycle;
- `IntervalBetweenShotsSeconds` — timing between those sequential shots.

Canonical authored content always has one shot group per firing cycle. The legacy `ShotsPerTrigger` field remains for the transitional catalogue/scheduler boundary, but canonical validation requires it to be one.

`WeaponShotPattern.Canonical(projectilesPerShot, spreadDegrees)` represents simultaneous attack instances. It contains no sequential timing.

Therefore:

- shotgun: one firing cycle, one shot, multiple projectiles;
- burst rifle: one firing cycle, multiple sequential shots, one projectile per shot;
- burst shotgun: one firing cycle, multiple sequential shots, multiple projectiles per shot;
- laser: normal fire mode/cadence plus one or more simultaneous traces.

The pre-existing `Continuous` fire mode remains only for older transitional definitions. Canonical authored lasers do not use it.

## Universal authored values

`WeaponBaseStats` owns only values that are meaningful across delivery types:

- direct damage;
- canonical damage category (`Physical`, `Thermal`, `Chemical`, `Energy`);
- optional typed `WeaponDamageOverTimeStats`;
- `PierceValue`;
- `RicochetValue`;
- movement penalty percentage;
- typed maximum attack distance;
- knockback, preserved from the current catalogue rather than discarded by migration.

Absent DoT is `null`. The compatibility `WeaponDamageSpec` still exposes legacy zero-valued projections for existing adapters, but canonical content stores DoT as an optional typed object.

Unlimited distance is represented by `WeaponAttackDistance.Unlimited()`, not a magic negative or zero value. The current travelling-projectile compatibility projection still requires finite range and fails closed until that runtime migration is implemented.

## Pierce

Pierce remains fixed-point tenths:

```text
10 -> 1.0 -> one guaranteed affected enemy
12 -> 1.2 -> one guaranteed enemy plus one 20% additional-enemy chance
20 -> 2.0 -> two guaranteed enemies
27 -> 2.7 -> two guaranteed enemies plus one 70% additional-enemy chance
```

The runtime must consume the integer capacity directly and roll the fractional remainder at most once through the existing deterministic random authority. It must not repeatedly subtract floating-point values.

Delivery interpretation is explicit:

- Normal/Orb: enemy hit capacity for the travelling instance;
- Laser: deterministically ordered intersected-enemy capacity;
- Rocket: deterministically ordered enemies inside the explosion that receive damage.

Rocket Pierce never lets the rocket body pass through enemies. Contact still detonates immediately.

## Ricochet

`RicochetValue` is the canonical fixed-point tenths budget:

```text
0  -> no bounce
2  -> 20% chance for one final bounce
10 -> one guaranteed bounce
12 -> one guaranteed bounce plus one 20% final-bounce chance
20 -> two guaranteed bounces
```

`WeaponRicochetSpec` now supports a canonical constructor carrying the exact fixed-point budget plus reusable bounce physics and post-bounce homing pause.

The previous independent `maximumRicochets + bounceChance` constructor remains transitional and is explicitly not reinterpreted as the canonical budget. Canonical validation rejects an impact ricochet contract unless its fixed-point budget exactly matches `WeaponBaseStats.Ricochet`.

Runtime consumption remains:

1. consume one guaranteed whole bounce while at least one remains;
2. when only a fractional remainder remains, roll once through the existing deterministic random authority;
3. success performs one final bounce and exhausts the budget;
4. failure exhausts the budget without bouncing.

World collisions do not consume Pierce.

The existing effective evaluator preserves the exact fixed-point budget while rebuilding modified impact physics. The legacy `RicochetMaximumRicochets` augment target is rejected for canonical fixed-point definitions because rewriting a maximum count would destroy the guaranteed-plus-one-fraction semantics. Retained-speed and angle modifiers remain reusable.

## Typed delivery union

`WeaponDeliverySpec` is a discriminated union with exactly one selected settings group:

- `Normal` -> `WeaponNormalDeliverySettings`;
- `Orb` -> `WeaponOrbDeliverySettings`;
- `Rocket` -> `WeaponRocketDeliverySettings`;
- `Laser` -> `WeaponLaserDeliverySettings`;
- `Special` -> `WeaponSpecialDeliverySettings`.

All delivery variants reuse `WeaponGuidanceSpec`, `WeaponImpactSpec`, and `WeaponEffects`. No weapon-specific projectile subclasses were introduced.

### Normal

Normal settings contain projectile speed and radius. The compatibility projection creates the existing generic `RegularProjectile` spec.

### Orb

Orb settings contain projectile speed and radius and may use existing gradual homing. Canonical validation rejects rocket-style wall-contact explosion triggers for Orb delivery.

An Orb can terminate on enemy impact, range expiry, or another explicit impact/termination rule. It does not explode merely because it touches a wall.

### Rocket

Rocket settings contain projectile speed and radius. Rocket validation requires:

- enemy-impact handling;
- wall-impact handling;
- explosion trigger on both enemy and wall contact;
- a valid reusable explosion effect with positive radius.

The generic impact authority remains responsible for evaluating an eligible wall ricochet before applying contact explosion behaviour. No rocket subclass was added.

### Laser

Laser settings contain width only. There is no projectile-speed field or fake speed value.

Laser still uses:

- canonical fire mode and rate;
- simultaneous trace count and spread;
- damage, damage type and optional DoT;
- Pierce and Ricochet;
- movement penalty and maximum distance.

The effective evaluator now distinguishes travelling deliveries from simultaneous non-projectile emissions, so canonical Laser definitions can resolve to `EffectiveWeapon` without inventing projectile structure. Current projectile-only range/Pierce augment targets still fail closed for Laser until delivery-neutral modifier targets are introduced.

The current Unity runtime adapter has not been expanded to execute Laser delivery in this task.

### Special

Special delivery contains:

- one existing `WeaponBehaviorId` registry reference;
- a sorted unique set of validated typed parameters (`Number`, `Integer`, `Boolean`, or stable `Identity`).

It cannot carry code, reflection targets, Unity references, or an arbitrary unvalidated dictionary. Unknown behaviour IDs still fail at the existing behaviour-registry boundary.

Special is not a substitute for reusable guidance, impact, damage, explosion, DoT, chain, or ricochet contracts.

## Presentation

`WeaponPresentation` has separate required references for:

- inventory/shop/results side-profile art;
- mounted top-down art;
- delivery/projectile/beam art.

Trail, impact, and explosion references are optional and separate.

Presentation references never select combat mechanics. Missing required art fails construction and cannot silently substitute another weapon's behaviour.

## Drop metadata and `TopBoxOnly`

`WeaponDropMetadata` contains:

- exact equipment definition identity;
- authored rarity identity;
- availability (`Live`, `PreviewOnly`, `Disabled`);
- peak drop level;
- positive base selection weight;
- explicit `WeaponStrongboxEligibility`.

Eligibility is either:

```text
MinimumTier = N
```

meaning Tier `N` and every later tier, or an explicit sorted unique allowed-tier list for genuinely named-tier-exclusive content.

There is no `TopBoxOnly` field in the canonical model. Canonical content never infers exclusivity from whichever strongbox happens to be highest today.

The flat `WeaponDefinitionData.TopBoxOnly` field and the current strongbox selection check remain transitional because changing live strongbox behaviour and migrating the full catalogue are out of scope. The migration boundary must map each old `TopBoxOnly` definition to an explicit stable tier rule before it can become canonical; it must not derive that rule from the current catalogue maximum.

Strongbox rarity percentages, target-level distributions, item-level tables, and augment-roll tables remain in the strongbox tier/profile authorities.

## Equipment and augment ownership

Canonical weapon definitions contain no installed augments, augment capacity, or shared augment level.

`CanonicalWeaponDefinitionSamples` includes two ownership examples using the existing contracts:

- Pulse Shotgun equipment with two rolled slots and shared augment level five, with no augments preinstalled;
- Chemical Orb equipment with three rolled slots and shared augment level eleven, with exact Deadly, Overclocked, and chemical-DoT augment instances installed.

The slot count and shared level are stored in `GeneratedEquipmentAugmentSignatureV1`. Installed augments are stored in the exact `EquipmentInstance`. Neither is stored in `WeaponBlueprint`.

Fresh dropped weapons remain expected to use `Array.Empty<AugmentInstance>()`; this task does not change strongbox generation.

## Effective weapon boundary

The existing route remains:

```text
canonical WeaponBlueprint
+ exact EquipmentInstance
+ installed AugmentInstances
+ supported modifier sets
-> immutable EffectiveWeapon
-> WeaponFiringScheduler
-> accepted emissions
-> runtime delivery adapter
-> WeaponEffectBatch
```

`EffectiveWeapon` now exposes canonical delivery, presentation, drop metadata, movement penalty, final projected range, and final Pierce without querying the catalogue after construction.

The existing evaluator remains the modifier authority. It now validates structure by delivery type rather than assuming every simultaneous attack instance is a travelling projectile. Unsupported structural augment changes continue to fail closed; no absent projectile, explosion, DoT, homing, chain, or canonical ricochet structure is silently created by an augment.

A spawned attack still receives the resolved immutable profile and must not query inventory, augment definitions, character skills, passive abilities, buffs, or the original weapon catalogue.

## Transitional catalogue boundary

The current flat `WeaponDefinitionData` and `WeaponCatalogJsonImporter` remain temporary inputs.

`WeaponCatalogBlueprintMapper` remains the one explicit loss-conscious mapping boundary:

- the existing `Map(...)` path preserves the current live route and produces a blueprint marked `IsTransitionalCatalogProjection == true`;
- the new `MapAuthored(...)` path accepts the existing definition plus explicit missing semantics and produces the canonical grouped authority through `WeaponBlueprint.TryCreateAuthored(...)`.

`MapAuthored(...)` never infers delivery from weapon names and never converts `TopBoxOnly` from the catalogue's moving highest tier. It requires explicit delivery size/width, fixed-point Ricochet, movement penalty, presentation, equipment/rarity identities, availability, and stable strongbox eligibility.

The mapper preserves current direct damage, damage category, DoT, Pierce, range, fire/shot numerics, peak level, weight, and knockback. It fails closed when migration would otherwise discard independent legacy area-damage magnitude, persistent-pool data, healing, mismatched projectile kinds/termination policies, fake Laser speed, or another unsupported structure.

New directly authored content must use `CreateAuthored`. A full JSON schema/catalogue migration is intentionally deferred.

## Representative definitions

`CanonicalWeaponDefinitionSamples` provides development-only, unregistered examples for:

1. Pulse Shotgun — semi-automatic, eight simultaneous Energy projectiles, Normal delivery;
2. Seeking Chemical DoT Orb Launcher — semi-automatic, Chemical direct damage plus explicit DoT, gradual homing and optional reacquisition, Orb delivery;
3. Contact Rocket Launcher — immediate enemy/wall contact explosion and Pierce as explosion-target capacity;
4. Automatic Energy Laser — normal automatic cadence, width, no projectile speed.

They are not Stage 1 replacements and are not added to production, shops, strongboxes, scenes, prefabs, or persistence.

## Validation boundary

`WeaponBlueprint.TryCreateAuthored` returns ordered stable `WeaponDefinitionIssue` diagnostics. `CreateAuthored` throws `WeaponDefinitionValidationException` containing the same ordered diagnostics.

Stable issue codes cover:

- missing identity;
- missing/invalid fire settings and rate;
- burst missing burst data;
- non-burst carrying burst data;
- invalid simultaneous projectile count;
- invalid spread;
- missing/invalid damage and optional DoT;
- invalid Pierce/Ricochet fixed-point values;
- missing/incompatible delivery groups;
- laser/projectile-data conflicts;
- rocket missing valid contact explosion behaviour;
- invalid guidance;
- missing presentation;
- missing/invalid drop metadata and tier restriction;
- conflicting Special behaviour selection;
- unsupported structural augment changes;
- transitional runtime-projection limitations.

`MapAuthored(...)` translates raw catalogue and migration failures into stable `WeaponBlueprintMappingIssueCode` values, including explicit diagnostics for missing authored mapping data, invalid delivery, fake Laser speed, invalid fixed-point Ricochet, invalid tier rules, unresolved `TopBoxOnly`, unsupported area damage, and canonical construction rejection.

Leaf constructors also fail closed for invalid finite ranges, speed/radius/width, behaviour IDs, duplicate special parameters, duplicate/invalid tier lists, and missing required references. Invalid content is never silently reinterpreted.

## Explicit non-goals

This implementation does not:

- migrate the 121-definition flat catalogue;
- replace the JSON import schema;
- alter strongbox selection/opening logic;
- move tier probability or augment tables into weapon content;
- add shop, inventory, results, or opening UI;
- add ammo, magazines, heat, charge, spin-up, or power-bank systems;
- redesign the firing scheduler;
- add Unity projectile/orb/rocket/laser components;
- modify scenes, prefabs, current Stage 1 content, simulator execution, or account persistence;
- add automated tests during the prototype phase.
