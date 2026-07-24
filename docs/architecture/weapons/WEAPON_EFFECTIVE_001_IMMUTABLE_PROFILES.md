# WEAPON-EFFECTIVE-001 — Immutable effective weapon profiles

## Scope

This change adds an immutable derived `EffectiveWeapon` profile after WEAPON-ARCH-001. It combines one canonical `WeaponBlueprint`, one validated existing `EquipmentInstance`, and the installed existing `AugmentInstance` values without changing current Unity firing behavior, scenes, Stage 1 content, the simulator, strongboxes, weapon catalog JSON, or production composition.

The implementation does not introduce heat, charge, ammo, magazines, power banks, item-level combat scaling, a second equipment model, a second augment model, a second ID system, or a second weapon behavior registry.

## Authorities retained

- `WeaponBlueprint` remains the canonical authored modular weapon definition.
- `EquipmentCatalog` remains the equipment and augment definition registry and validates the supplied `EquipmentInstance`.
- `EquipmentInstance`, `AugmentInstance`, and `AugmentDefinition` remain the inventory and installed-augment authorities.
- Existing `WeaponDefinitionId`, `EquipmentInstanceId`, and `StableId` types are reused.
- `WeaponBehaviorRegistry`, `WeaponEffectBatch`, the current firing scheduler, and Unity live adapters remain unchanged.

`WeaponAugmentModifierSet` is a per-installed-instance immutable value. It carries already-resolved numeric effects together with the existing `AugmentDefinition` and `AugmentInstance`; it is not stored or queried as a registry. `EffectiveWeaponFactory` rejects any set whose definition or instance differs from the existing `EquipmentCatalog` and `EquipmentInstance` authorities.

## Effective profile ownership

`EffectiveWeapon` preserves:

- the source `WeaponBlueprint` reference;
- the existing `EquipmentInstanceId` value;
- the existing equipment definition `StableId`;
- item level as separate metadata;
- quality identity;
- an immutable snapshot of installed `AugmentInstance` values;
- immutable effective fire, pattern, projectile, guidance, impact, damage, and effect contracts.

Item level is copied to `EffectiveWeapon.ItemLevel` and is never read by the combat-stat evaluator.

## Modifier order

For every numeric stat, all installed augment modifiers are evaluated in this exact order:

1. authored value from `WeaponBlueprint`;
2. sum of flat additions;
3. sum of additive percentages, applied once as `value * (1 + summedPercentage)`;
4. product of multipliers;
5. one optional explicit override;
6. final finite-value validation, integer conversion where required, and clamping.

Additive percentages use decimal fractions: `0.10` means plus ten percent. Multiple explicit overrides for the same stat are rejected rather than depending on hidden ordering.

Integer values use deterministic midpoint rounding away from zero after all modifier stages. Non-negative values clamp at zero. Angular spread/randomness values clamp to `0..360`. Retained-speed and retained-damage ratios clamp to `0..1`. Values whose authored contract requires positivity are validated after clamping and rejected if they cannot remain valid.

## Supported numeric augment targets

- direct and area damage;
- projectile or continuous rate of fire;
- spread and angular randomness;
- projectile speed, range, and fixed-point pierce tenths;
- explosion radius;
- damage-over-time magnitude, duration, tick rate, and stack count;
- homing acquisition range, turn rate, and activation delay;
- ricochet count, retained speed, and random angle;
- chain target count, acquisition range, and retained damage per jump.

## Structural compatibility

Augments change values only. They do not add, remove, or replace weapon structure.

The factory explicitly rejects:

- projectile speed, range, or pierce changes on a weapon without a projectile;
- explosion radius or area-damage changes on a weapon without authored explosion structure;
- DoT changes on a weapon without authored DoT damage and effect structure;
- homing changes on an unguided weapon;
- ricochet changes on a weapon without authored ricochet structure;
- chain changes on a weapon without authored chain structure;
- angular spread changes on single-shot or beam pattern kinds.

Fire mode, shot-pattern kind, projectile kind, guidance mode, impact-trigger presence, explosion presence, ricochet presence, chain presence, and behavior selection are not modifier targets.

## Validation boundary

Before evaluating combat values, `EffectiveWeaponFactory`:

1. validates the equipment instance through the existing `EquipmentCatalog`;
2. resolves the existing equipment definition and requires the weapon category;
3. requires the equipment runtime weapon reference to match the supplied `WeaponBlueprint.DefinitionId` exactly;
4. requires exactly one modifier set for every installed augment instance, including an empty set for an augment with no weapon-stat effect;
5. rejects extra, duplicate, stale, or mismatched augment instance/definition payloads;
6. reconstructs the immutable WEAPON-ARCH-001 contracts so their existing validation remains active;
7. performs final cross-structure validation.

No source definition or instance is modified during evaluation.

## Deferred

- mapping current `WeaponDefinitionData` into `WeaponBlueprint`;
- authoring or persistence format for augment modifier payloads;
- item-level combat scaling;
- heat, charge, ammo, magazines, cooldown resources, and power banks;
- mapping `EffectiveWeapon` into current `WeaponRuntimeFiringProfile` or behavior selection;
- Unity firing, projectile, scene, strongbox, simulator, or production adoption;
- fractional-pierce execution behavior.
