# WEAPON-DATA-002 — Authored weapon, delivery, and baked runtime model

## Status

Accepted design direction. Implementation is intentionally deferred to the next weapon-data task.

This document is the shared authority for how weapon content, owned weapon instances, augments, baked in-game weapon values, delivery instances, Pierce, Ricochet, and weapon presentation are expected to fit together.

## Relationship to WEAPON-ARCH-001

`WEAPON-ARCH-001_MODULAR_CONTRACTS.md` introduced parallel modular vocabulary while the flat catalog remained authoritative. This decision refines the target model before the catalog migration proceeds.

In particular, this decision establishes that:

- a bullet or other delivery runtime is not authored as a shotgun-, rifle-, or weapon-family-specific definition;
- a weapon name such as **Pulse Shotgun** does not imply pulse-spread or burst behavior;
- weapon content owns the designer-facing values that are baked before entering live gameplay;
- delivery runtimes receive final immutable numbers and execute movement, collision, impact, and termination;
- universally meaningful values are separated from fire-mode- and delivery-specific values;
- Pierce and Ricochet use deterministic fixed-point integer-plus-fraction budgets;
- rockets interpret Pierce as an explosion target budget rather than a travel-through budget;
- owned augment state stays on the exact equipment instance and is never installed into the canonical weapon definition;
- inventory side-profile art and mounted top-down art are both required presentation references.

Where the current parallel contracts disagree with this decision, they are migration scaffolding rather than the final target schema.

## 1. Core rule

A weapon decides **when and what to emit**.

A delivery instance decides **how that emitted attack behaves in the world**.

The weapon and its resolver provide the final values. The spawned bullet, orb, rocket, laser, or special delivery does not look up inventory state, augments, character skills, or canonical weapon content while travelling.

```text
canonical weapon definition
+ exact owned equipment instance
+ installed augment instances
+ character passives
+ active abilities
+ permitted run modifiers
= baked EffectiveWeapon

accepted firing cycle
+ baked EffectiveWeapon
= immutable delivery spawn payload(s)

spawn payload
+ reusable delivery runtime
= live bullet, orb, rocket, laser, or special attack instance
```

The delivery runtime must not know whether a normal projectile came from a shotgun, pistol, rifle, or autocannon.

## 2. Three data layers

### 2.1 Canonical weapon definition

Versioned static game content shared by server validation and clients.

It contains:

- stable identity and display data;
- fire mode and cadence;
- shot count and spread;
- canonical damage and handling values;
- one selected delivery type with its required settings;
- presentation references;
- drop/progression references.

It does not contain an account owner, item instance, installed augments, runtime cooldown, active buffs, or a Unity scene object.

### 2.2 Owned weapon instance

Server-authoritative account state for one exact item.

It contains:

- exact equipment instance ID;
- canonical weapon definition ID;
- item level and quality;
- generated augment slot capacity;
- generated shared augment level;
- installed augment instances;
- ownership/provenance and equipped-slot bindings through the existing account authorities.

It does not duplicate weapon damage, fire rate, delivery behavior, display text, or PNG paths.

### 2.3 Baked effective weapon

Immutable in-game combat data derived from the definition, owned instance, augments, and current supported modifiers.

It contains final values such as:

- effective rate of fire;
- effective damage and damage-over-time values;
- effective projectile speed and range;
- effective Pierce and Ricochet budgets;
- effective guidance and explosion values;
- the selected delivery kind and presentation references.

It is cached or rebuilt when an allowed modifier input changes. It is not durable account state.

## 3. Designer-facing weapon shape

The target authored shape is a discriminated weapon definition with universal sections and one delivery-specific section.

```yaml
weapon:
  id: weapon.example
  display_name: Example Weapon

  fire:
    mode: semi_auto
    rate_of_fire: 2.0
    burst: null

  shot:
    emissions_per_shot: 1
    spread_degrees: 0

  base_stats:
    damage: 20
    damage_type: energy
    damage_over_time: null
    pierce: 1.0
    ricochet: 0.0
    movement_penalty_percent: 5
    travel_distance:
      mode: limited
      distance: 30

  delivery:
    type: normal
    normal:
      bullet_speed: 25
      bullet_size: 0.18

  presentation_id: weapon-presentation.example
  drop_profile_id: drop-profile.example
```

`emissions_per_shot` is the technical name for the designer concept commonly shown as **bullets per shot**. It counts simultaneous delivery instances, so it remains correct for bullets, orbs, rockets, and multiple laser traces.

## 4. Universal identity

Always present:

```yaml
id: weapon.pulse-shotgun
display_name: Pulse Shotgun
```

The display name has no mechanical meaning. Words such as `Pulse`, `Burst`, `Orb`, or `Rocket` must not silently select firing or delivery behavior.

## 5. Fire settings

Every weapon always has:

```yaml
fire:
  mode: semi_auto
  rate_of_fire: 1.4
```

Supported modes:

- `semi_auto`
- `automatic`
- `burst`

Laser weapons use the same modes and are still governed by rate of fire.

### 5.1 Rate-of-fire meaning

`rate_of_fire` means accepted firing cycles per second.

For semi-automatic and automatic weapons, one firing cycle creates one shot group.

For burst weapons, one firing cycle creates one complete burst. The burst has additional internal timing.

### 5.2 Semi-automatic

A new firing cycle requires a new trigger press after the weapon becomes ready.

```yaml
fire:
  mode: semi_auto
  rate_of_fire: 1.4
  burst: null
```

### 5.3 Automatic

Accepted firing cycles repeat while the trigger remains held and the cadence allows them.

```yaml
fire:
  mode: automatic
  rate_of_fire: 6.0
  burst: null
```

### 5.4 Burst

Burst-only data exists only when `mode` is `burst`.

```yaml
fire:
  mode: burst
  rate_of_fire: 1.2
  burst:
    shots_per_burst: 3
    time_between_burst_shots_seconds: 0.08
```

`shots_per_burst` is sequential. It is never the same value as `emissions_per_shot`.

A three-shot burst shotgun with eight emissions per shot performs three sequential firing events, each emitting eight simultaneous delivery instances.

The scheduler must reject a new burst while the current burst is still active. Rate-of-fire cadence and burst duration therefore cannot create overlapping bursts unless a future explicit weapon behavior permits it.

## 6. Shot settings

Always present:

```yaml
shot:
  emissions_per_shot: 8
  spread_degrees: 28
```

Rules:

- `emissions_per_shot` is at least one;
- `spread_degrees` may be zero;
- all emissions in one shot occur simultaneously unless the fire mode schedules another sequential shot;
- spread-direction calculation belongs to the weapon firing/emission layer;
- each resulting delivery instance receives its own resolved direction and identical baked stats unless an explicit pattern says otherwise.

This keeps shotgun spread separate from burst fire.

## 7. Universal base stats

The following values are always visible on a damaging weapon definition:

```yaml
base_stats:
  damage: 7.5
  damage_type: energy
  damage_over_time: null
  pierce: 1.0
  ricochet: 0.0
  movement_penalty_percent: 8
  travel_distance:
    mode: limited
    distance: 14
```

### 7.1 Damage

`damage` is the direct damage carried by each emitted delivery instance.

Eight emissions with `damage: 7.5` can therefore deal 60 direct damage if every emission hits a valid target.

### 7.2 Damage type

Initial supported values remain:

- `physical`
- `thermal`
- `chemical`
- `energy`

### 7.3 Damage over time

Damage over time is either absent or explicit:

```yaml
damage_over_time: null
```

```yaml
damage_over_time:
  damage_per_second: 6
  effect_length_seconds: 5
```

The authored names must not use an ambiguous `damage_number`. The unit is explicitly damage per second, and the runtime status-effect system may derive deterministic tick values from its tick cadence.

DoT stacking, refresh, and strongest-effect policy are separate status-effect decisions and are not encoded implicitly in this pair.

### 7.4 Movement penalty

`movement_penalty_percent` is the weapon's authored handling penalty. The effective-weapon resolver combines it with supported character and runtime modifiers before gameplay receives the final value.

### 7.5 Travel distance

Every delivery has a maximum reach, including lasers.

```yaml
travel_distance:
  mode: limited
  distance: 35
```

```yaml
travel_distance:
  mode: unlimited
```

No magic negative or zero value represents unlimited range.

## 8. Delivery type is a discriminated union

Exactly one delivery type is active:

- `normal`
- `orb`
- `rocket`
- `laser`
- `special`

The selected delivery type determines which extra settings are valid and which runtime algorithm executes the baked payload.

A delivery type is not a weapon family. There is no `ShotgunBullet`, `RifleBullet`, or `ChemicalShotgunBullet` runtime class.

## 9. Shared travelling-delivery values

Normal projectiles, orbs, and rockets travel over time and therefore require a speed.

```yaml
bullet_speed: 25
```

Laser deliveries do not author a fake speed of zero. Their trace resolves according to laser behavior and the universal travel distance.

Optional guidance may be supported by a travelling delivery without making a weapon-specific subclass:

```yaml
seeking:
  enabled: true
  acquisition_distance: 18
  turn_speed_degrees_per_second: 150
  activation_delay_seconds: 0.15
  target_selection: closest_to_aim
  can_find_new_target: true
  homing_pause_after_ricochet_seconds: 0
```

When seeking is disabled, the remaining seeking fields are absent rather than filled with meaningless zeroes.

## 10. Normal delivery

```yaml
delivery:
  type: normal
  normal:
    bullet_speed: 32
    bullet_size: 0.18
```

Normal delivery behavior:

- travels along its resolved direction;
- checks eligible world and enemy collisions;
- applies the baked payload on enemy contact;
- uses Pierce to decide how many enemies it may affect;
- uses Ricochet on eligible world-object contacts;
- terminates when its hit budget, bounce opportunity, blocking collision, or travel distance requires termination.

The runtime does not know whether this normal projectile came from a shotgun or another weapon.

## 11. Orb delivery

```yaml
delivery:
  type: orb
  orb:
    bullet_speed: 10
    bullet_radius: 0.42
    explosion_radius: 1.8
    seeking:
      enabled: true
      acquisition_distance: 18
      turn_speed_degrees_per_second: 150
      activation_delay_seconds: 0.15
      target_selection: closest_to_aim
      can_find_new_target: true
      homing_pause_after_ricochet_seconds: 0
```

Orb contact behavior:

- an orb does not use the rocket's detonate-on-any-contact rule;
- world-object contact is resolved through its eligible blocking/Ricochet rules rather than immediate detonation;
- enemy contact resolves the orb payload;
- an authored `explosion_radius` applies when that enemy-contact payload resolves, not merely because the orb touched a wall;
- Pierce controls whether the orb may affect another eligible enemy after a prior enemy contact;
- with the standard value `pierce: 1.0`, the orb resolves on the first enemy and terminates.

An orb may be seeking or unguided. Seeking is a data feature, not a separate `HomingOrbProjectile` subclass.

## 12. Rocket delivery

```yaml
delivery:
  type: rocket
  rocket:
    bullet_speed: 18
    bullet_size: 0.28
    explosion_radius: 2.5
```

Rocket contact behavior:

- the first eligible enemy, wall, or blocking-object contact detonates the rocket immediately;
- detonation terminates the rocket;
- Pierce does not allow the rocket body to travel through enemies;
- Pierce defines how many deterministically ordered enemies inside the explosion radius receive the payload;
- standard rockets require `ricochet: 0.0` because immediate contact detonation and bouncing are contradictory.

A future exceptional bouncing explosive must use an explicit approved special behavior rather than weakening the standard rocket rule.

## 13. Laser delivery

```yaml
delivery:
  type: laser
  laser:
    width: 0.12
```

Laser behavior:

- still uses fire mode and rate of fire;
- uses universal direct damage, damage type, DoT, Pierce, Ricochet, spread, movement penalty, and travel distance where supported;
- does not require bullet speed or bullet size;
- resolves one or more traces according to `emissions_per_shot` and spread;
- Pierce controls how many ordered enemy intersections receive the payload;
- Ricochet may produce additional reflected traces when the laser behavior supports eligible reflective surfaces.

A semi-automatic laser fires once per accepted trigger edge. An automatic laser repeats accepted traces according to rate of fire.

## 14. Special delivery

Special delivery is an escape hatch for genuinely exceptional mechanics, not arbitrary executable data.

```yaml
delivery:
  type: special
  special:
    behavior_id: special-behavior.slowing-wave
    parameters:
      radius: 4
      slowdown_percent: 30
      effect_length_seconds: 3
```

Rules:

- `behavior_id` selects an approved runtime strategy;
- each strategy owns a validated parameter schema;
- unknown behavior IDs or parameters fail content validation;
- JSON or other content must never contain code, reflection targets, Unity object references, or scene discovery instructions.

## 15. Pierce value

Pierce is authored as a non-negative value with at most one decimal digit and compiled to fixed-point tenths.

For standard damaging attacks, content should normally use at least `1.0` because `1.0` means one guaranteed enemy can be affected.

```text
integer part    = guaranteed enemies affected
fractional part = chance to affect one additional enemy
```

Examples:

| Pierce | Meaning |
|---:|---|
| `1.0` | Affect one enemy and stop. |
| `1.2` | Affect one enemy, then have a 20% chance to affect one additional enemy. |
| `2.0` | Affect two enemies. |
| `2.7` | Affect two enemies, then have a 70% chance to affect one additional enemy. |

The fractional chance is evaluated at most once. A successful fractional roll grants exactly one final target; it does not create repeated fractional rolls.

### 15.1 Travelling projectile and orb interpretation

For normal projectiles and orbs, Pierce is the enemy-contact budget.

Example for `pierce: 2.2`:

```text
enemy 1 -> guaranteed payload, continue
ordinary remaining guaranteed hits: 1

enemy 2 -> guaranteed payload
ordinary guaranteed hits exhausted -> roll 20%

roll fails    -> terminate
roll succeeds -> continue to one final enemy, apply payload, then terminate
```

### 15.2 Laser interpretation

Laser intersections are ordered along the trace. Pierce selects the first guaranteed targets and possibly one additional target using the same fixed-point rule.

### 15.3 Rocket interpretation

A rocket first gathers eligible enemies inside its explosion radius, then sorts them deterministically:

1. distance from explosion centre;
2. stable target identity;
3. lifecycle generation when identity alone is insufficient.

Pierce selects victims from that ordered list.

For `pierce: 2.2`, two explosion victims are guaranteed and a deterministic 20% roll decides whether the third receives the payload.

## 16. Ricochet value

Ricochet uses the same fixed-point integer-plus-fraction format as Pierce, but counts successful bounces from eligible objects or surfaces.

```text
integer part    = guaranteed bounces
fractional part = chance for one additional bounce
```

Examples:

| Ricochet | Meaning |
|---:|---|
| `0.0` | Cannot bounce. |
| `0.2` | 20% chance to bounce on the first eligible object contact. |
| `1.0` | One guaranteed bounce. |
| `1.2` | One guaranteed bounce, then a 20% chance for one additional bounce. |
| `2.0` | Two guaranteed bounces. |

Exact `1.2` execution:

```text
initial ricochet value: 1.2

eligible object contact 1:
value is at least 1.0
-> bounce is guaranteed
-> subtract 1.0
-> remaining value is 0.2

eligible object contact 2:
value is below 1.0 and above 0.0
-> roll 20%

failure:
-> do not bounce
-> terminate according to delivery collision rules

success:
-> bounce
-> set remaining Ricochet to 0.0

next eligible object contact:
-> no bounce remains
-> terminate according to delivery collision rules
```

The fractional chance is never rolled repeatedly. After a successful fractional bounce, the remaining value is zero.

The baked runtime representation should avoid floating-point subtraction:

```yaml
ricochet_budget:
  guaranteed_bounces: 1
  extra_bounce_chance_tenths: 2
```

Pierce should use the same fixed-point approach.

## 17. Deterministic chance and ordering

Pierce and Ricochet fractional outcomes must be deterministic for authoritative simulation and replay.

The random decision must derive from stable inputs such as:

- run/simulation seed;
- source equipment instance identity;
- firing-cycle/emission identity;
- collision or candidate index;
- target identity and lifecycle generation where relevant.

The implementation must not call an uncontrolled Unity random source.

Equal-distance target candidates require stable identity ordering.

## 18. Baked EffectiveWeapon

The effective-weapon resolver applies all supported modifiers before a live attack is spawned.

```text
canonical base values
+ installed augment modifiers
+ character passive modifiers
+ active ability modifiers
+ supported run modifiers
= final immutable values
```

Example shape:

```yaml
effective_weapon:
  source_equipment_instance_id: equipment-instance.orb-launcher-001
  weapon_definition_id: weapon.seeking-chemical-orb-launcher

  fire:
    mode: semi_auto
    rate_of_fire: 0.90
    burst: null

  shot:
    emissions_per_shot: 1
    spread_degrees: 0

  base_stats:
    damage: 23
    damage_type: chemical
    damage_over_time:
      damage_per_second: 8.4
      effect_length_seconds: 5
    pierce_budget:
      guaranteed_targets: 1
      extra_target_chance_tenths: 0
    ricochet_budget:
      guaranteed_bounces: 0
      extra_bounce_chance_tenths: 0
    movement_penalty_percent: 14
    travel_distance:
      mode: limited
      distance: 35

  delivery:
    type: orb
    orb:
      bullet_speed: 10
      bullet_radius: 0.42
      explosion_radius: 1.8
      seeking:
        enabled: true
        acquisition_distance: 18
        turn_speed_degrees_per_second: 150
        activation_delay_seconds: 0.15
        target_selection: closest_to_aim
        can_find_new_target: true
        homing_pause_after_ricochet_seconds: 0
```

The effective weapon does not remember that Deadly, Overclocked, or a DoT augment produced those values. It carries the finished numbers required by firing and delivery execution.

## 19. Delivery spawn payload

When the scheduler accepts a firing cycle, the weapon creates one immutable spawn payload per emission.

```yaml
delivery_spawn:
  emission_id: deterministic-emission-id
  source_actor_id: player-instance.001
  source_equipment_instance_id: equipment-instance.pulse-shotgun-001
  source_weapon_definition_id: weapon.pulse-shotgun

  origin: resolved-muzzle-position
  direction: resolved-spread-direction

  damage: 7.5
  damage_type: energy
  damage_over_time: null

  pierce_budget:
    guaranteed_targets: 1
    extra_target_chance_tenths: 0

  ricochet_budget:
    guaranteed_bounces: 0
    extra_bounce_chance_tenths: 0

  travel_distance:
    mode: limited
    distance: 14

  delivery:
    type: normal
    bullet_speed: 32
    bullet_size: 0.18
```

The delivery instance may consume its own hit and bounce budgets as it executes. It must not mutate the baked weapon or the owned equipment instance.

## 20. Augment ownership

Augments are durable owned-instance data, not canonical weapon-definition data.

```yaml
owned_weapon:
  instance_id: equipment-instance.orb-launcher-001
  weapon_definition_id: weapon.seeking-chemical-orb-launcher
  item_level: 68
  quality_id: equipment-quality.legendary

  augment_capacity: 3
  shared_augment_level: 11

  installed_augments:
    - instance_id: augment-instance.deadly-001
      augment_definition_id: augment.deadly
      level: 11
      tier: 1

    - instance_id: augment-instance.overclocked-001
      augment_definition_id: augment.overclocked
      level: 11
      tier: 1

    - instance_id: augment-instance.dot-001
      augment_definition_id: augment.dot-damage
      level: 11
      tier: 1
```

Empty slots are represented by absence from `installed_augments`.

A weapon with two level-five slots and no player-selected augmenting therefore stores:

```yaml
augment_capacity: 2
shared_augment_level: 5
installed_augments: []
```

Each augment instance records the exact installed augment identity and player-owned upgrade state required by the account model. The effective resolver looks up the canonical augment definition and applies its level-dependent modifiers.

## 21. Presentation references

A weapon needs separate art for inventory/results presentation and live robot mounting.

```yaml
weapon_presentation:
  id: weapon-presentation.pulse-shotgun

  inventory:
    side_profile_sprite_id: sprite.weapon.pulse-shotgun.side

  mounted:
    top_down_sprite_id: sprite.weapon.pulse-shotgun.top
    pivot_x: 0.28
    pivot_y: 0.50
    scale: 1.0
    rotation_offset_degrees: 0
    muzzle_local_x: 0.82
    muzzle_local_y: 0.00
```

Rules:

- inventory and Results cards use the side-profile sprite;
- the robot uses the mounted top-down sprite;
- the robot owns weapon mount sockets;
- the weapon presentation owns alignment, scale, rotation offset, and muzzle offset relative to the socket;
- delivery presentation owns projectile sprites, trails, impacts, explosions, or laser effects;
- PNG data and Unity asset paths are not stored on every owned item or sent as account state.

## 22. Server and account boundary

The server/account state stores:

- exact equipment instance ID;
- weapon definition ID;
- item level and quality;
- augment capacity and shared augment level;
- installed augment instances and their levels;
- ownership/provenance;
- exact equipped-slot bindings;
- account and component revisions/fingerprints through existing authorities.

The server validates commands against a versioned canonical content catalog.

The account does not duplicate:

- fire rate;
- base damage;
- projectile speed;
- Pierce or Ricochet formulas;
- delivery settings;
- display names;
- side-profile or mounted PNGs;
- Unity Resources paths.

A future remote account adapter should submit operation ID, expected revision, and exact identity-based commands, then receive the new revision and snapshot/component delta.

## 23. Inventory and Results projections

Inventory and Results UI are disposable projections built by resolving exact owned instances against canonical definitions, augment definitions, and presentation catalogs.

A weapon card should be able to show:

- exact equipment instance identity internally;
- display name;
- quality;
- item level;
- augment capacity;
- shared augment level;
- installed augment names and levels;
- side-profile sprite;
- optional new/equipped/comparison state.

Results transfer records retain exact reward identity and provenance. The Results screen must resolve those identities into player-facing cards rather than displaying raw stable IDs.

## 24. Validation matrix

| Field or rule | Always | Normal | Orb | Rocket | Laser | Special |
|---|:---:|:---:|:---:|:---:|:---:|:---:|
| ID and display name | yes | — | — | — | — | — |
| Fire mode and rate of fire | yes | — | — | — | — | — |
| Emissions per shot and spread | yes | — | — | — | — | — |
| Damage and damage type | yes | — | — | — | — | — |
| Nullable DoT | yes | — | — | — | — | — |
| Pierce and Ricochet values | yes | interpreted | interpreted | explosion budget / Ricochet zero | interpreted | strategy-defined support |
| Movement penalty | yes | — | — | — | — | — |
| Limited or unlimited travel distance | yes | — | — | — | — | — |
| Bullet speed | no | required | required | required | forbidden | strategy schema |
| Bullet size/radius | no | size required | radius required | size required | forbidden | strategy schema |
| Explosion radius | no | forbidden | supported | required | forbidden | strategy schema |
| Laser width | no | forbidden | forbidden | forbidden | required | strategy schema |
| Seeking | no | supported | supported | supported | forbidden unless later designed | strategy schema |
| Detonate on any contact | no | no | no | always | no | strategy schema |
| Standard Ricochet | no | supported | supported | forbidden | supported if reflective | strategy schema |

Additional validation:

- burst fields are present only for burst mode;
- burst shot count is at least two;
- non-burst modes have no burst block;
- one-decimal Pierce/Ricochet authoring is converted exactly to fixed-point tenths;
- values with greater precision are rejected rather than rounded;
- standard damaging attacks should normally require Pierce of at least `1.0`;
- standard rockets reject non-zero Ricochet;
- unknown delivery types, special behavior IDs, damage types, or presentation references fail import;
- `Pulse` in a display name never selects pulse-spread behavior.

## 25. Example: semi-automatic Pulse Shotgun

`Pulse` describes its energy technology and presentation. It is not burst fire and does not emit sequential pulses.

```yaml
weapon:
  id: weapon.pulse-shotgun
  display_name: Pulse Shotgun

  fire:
    mode: semi_auto
    rate_of_fire: 1.4
    burst: null

  shot:
    emissions_per_shot: 8
    spread_degrees: 28

  base_stats:
    damage: 7.5
    damage_type: energy
    damage_over_time: null
    pierce: 1.0
    ricochet: 0.0
    movement_penalty_percent: 8
    travel_distance:
      mode: limited
      distance: 14

  delivery:
    type: normal
    normal:
      bullet_speed: 32
      bullet_size: 0.18

  presentation_id: weapon-presentation.pulse-shotgun
  drop_profile_id: drop-profile.pulse-shotgun
```

One accepted trigger edge emits eight simultaneous normal delivery instances with different spread directions. Each carries 7.5 Energy damage and can affect one enemy.

Example owned state:

```yaml
owned_weapon:
  instance_id: equipment-instance.pulse-shotgun-001
  weapon_definition_id: weapon.pulse-shotgun
  item_level: 24
  quality_id: equipment-quality.rare
  augment_capacity: 2
  shared_augment_level: 5
  installed_augments: []
```

## 26. Example: seeking Chemical DoT Orb Launcher

```yaml
weapon:
  id: weapon.seeking-chemical-orb-launcher
  display_name: Seeking Chemical Orb Launcher

  fire:
    mode: semi_auto
    rate_of_fire: 0.75
    burst: null

  shot:
    emissions_per_shot: 1
    spread_degrees: 0

  base_stats:
    damage: 18
    damage_type: chemical
    damage_over_time:
      damage_per_second: 6
      effect_length_seconds: 5
    pierce: 1.0
    ricochet: 0.0
    movement_penalty_percent: 14
    travel_distance:
      mode: limited
      distance: 35

  delivery:
    type: orb
    orb:
      bullet_speed: 10
      bullet_radius: 0.42
      explosion_radius: 1.8
      seeking:
        enabled: true
        acquisition_distance: 18
        turn_speed_degrees_per_second: 150
        activation_delay_seconds: 0.15
        target_selection: closest_to_aim
        can_find_new_target: true
        homing_pause_after_ricochet_seconds: 0

  presentation_id: weapon-presentation.seeking-chemical-orb-launcher
  drop_profile_id: drop-profile.seeking-chemical-orb-launcher
```

Example owned state:

```yaml
owned_weapon:
  instance_id: equipment-instance.orb-launcher-001
  weapon_definition_id: weapon.seeking-chemical-orb-launcher
  item_level: 68
  quality_id: equipment-quality.legendary
  augment_capacity: 3
  shared_augment_level: 11
  installed_augments:
    - instance_id: augment-instance.deadly-001
      augment_definition_id: augment.deadly
      level: 11
      tier: 1
    - instance_id: augment-instance.overclocked-001
      augment_definition_id: augment.overclocked
      level: 11
      tier: 1
    - instance_id: augment-instance.dot-001
      augment_definition_id: augment.dot-damage
      level: 11
      tier: 1
```

The in-game orb receives already-resolved direct damage, DoT, rate of fire, movement, guidance, Pierce, Ricochet, range, and delivery values. It does not inspect those three augment instances while travelling.

## 27. Implementation consequences for the next task

The next implementation task should treat this document as the target and perform the migration in small reviewed steps:

1. introduce or reshape immutable authoring contracts around universal weapon data and a delivery discriminated union;
2. replace the current Ricochet maximum-count/chance split with the shared fixed-point budget semantics defined here;
3. preserve or generalize the existing fixed-point Pierce value while adding delivery-specific execution interpretation;
4. remove mechanical meaning from display/family names such as `Pulse`;
5. add versioned serialization/import validation for the new shape;
6. produce a baked `EffectiveWeapon` from canonical content plus exact owned augment state and supported modifiers;
7. create immutable delivery spawn payloads from accepted firing cycles;
8. implement reusable normal, orb, rocket, laser, and approved special delivery strategies without weapon-specific projectile subclasses;
9. add both side-profile and mounted top-down weapon presentation references plus delivery presentation references;
10. update Inventory and Results projections to resolve exact item identities into complete cards;
11. add a remote account transport adapter later without changing the domain account-command model;
12. migrate production weapon content, strongbox simulation, and balancing tools to the same canonical identities only after the new schema is authoritative.

## 28. Intentionally unresolved

This decision does not choose:

- final balance numbers;
- DoT stacking/refresh policy;
- armour/resistance formulas;
- item-level combat scaling;
- ammo, heat, charge, magazines, or power-bank systems;
- exact special-delivery behavior schemas beyond the validated strategy rule;
- final JSON filenames or catalog partitioning;
- final Unity prefab and asset-bundle layout;
- whether every orb uses a non-zero explosion radius;
- future exceptional rockets that intentionally violate standard contact detonation.

Those decisions must be explicit follow-on work and must not be inferred from absent or zero-valued fields.