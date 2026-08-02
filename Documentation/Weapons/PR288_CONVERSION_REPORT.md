# PR #288 Weapon Conversion Report

Historical source: `d30030776909a42fed4633c49817c29b8c2eddf2:Assets/ShooterMover/Resources/WeaponCatalog/weapon_baseline_v01.json`

- Source families: **44**
- Source definitions: **121**
- Generated families: **11**
- Generated definitions: **33**
- Runtime-behavior-pending families: **7**
- Schema-blocked families: **16**
- Manual-review families: **10**

## Explicit mappings

- Damage: `Kinetic -> physical`, `Energized -> energy`, `Thermal -> thermal`, `Chemical -> chemical`.
- `Photonic` and `Omni-Phase` are not normalized to Energy; they remain review/pending.
- Rarity: `Common -> common`, `Uncommon -> common`, `Rare -> rare`, `Epic -> epic`, `Legendary -> legendary`, `Mythic -> artifact`, `Artifact -> artifact`.
- Because current production rarity is family-owned, generated families use the highest normalized rarity among their Marks.
- Historical PR #288 selection weights are migration evidence only. Generated JSON does not author a weight; current peak-level and rarity logic owns live distribution.
- Historical bullet definitions do not author collision radius; generated bullet Marks use the current-project `0.1` convention and record that approximation below.
- Families with fewer than three Marks are not padded or duplicated.
- Launchers and explosive Orbs remain blocked because current canonical authored data has one executable damage value, while PR #288 authored separate direct and area damage.
- Burst rifles remain under review because PR #288 does not author the required intra-burst interval.

## Family results

| Family | Source archetype | Source damage | Marks | Mapped type | Mapped damage | Status | Runtime behavior required | Approximation | Generated path | Validation | Notes |
|---|---|---:|---:|---|---|---|---|---|---|---|---|
| Shotgun | Spread | Kinetic | 3 | shotgun | physical | READY_AFTER_SMALL_MAPPING | existing travelling projectile execution | per-Mark rarity normalized to family common; bullet collision radius uses documented 0.1 current-project convention | Content/Weapons/shotgun/shotgun | validator + compiler passed | Reliable baseline spread weapon. |
| Burst Rifle | BurstRifle | Kinetic | 3 | — | physical | MANUAL_DESIGN_REVIEW | authored intra-burst interval | none | — | not generated | General-purpose burst family. |
| Sniper | Precision | Kinetic | 3 | normal-firearm | physical | READY_AFTER_SMALL_MAPPING | existing travelling projectile execution | per-Mark rarity normalized to family rare; bullet collision radius uses documented 0.1 current-project convention | Content/Weapons/normal-firearm/sniper | validator + compiler passed | High per-shot damage. |
| Grenade Launcher | Launcher | Kinetic | 3 | — | physical | SCHEMA_BLOCKED | separate direct and area-damage values | none | — | not generated | Arcing presentation may be added in Unity. |
| Fast Sniper | FastPrecision | Kinetic | 3 | normal-firearm | physical | READY_AFTER_SMALL_MAPPING | existing travelling projectile execution | per-Mark rarity normalized to family rare; bullet collision radius uses documented 0.1 current-project convention | Content/Weapons/normal-firearm/fast_sniper | validator + compiler passed | Faster alternative to Sniper. |
| Heavy Gatling Gun | HeavyLMG | Kinetic | 3 | normal-firearm | physical | READY_AFTER_SMALL_MAPPING | existing travelling projectile execution | per-Mark rarity normalized to family rare; bullet collision radius uses documented 0.1 current-project convention | Content/Weapons/normal-firearm/heavy_gatling | validator + compiler passed | No spin-up, heat or recoil. |
| Railgun | Precision | Kinetic | 2 | normal-firearm | physical | SCHEMA_BLOCKED | one-to-three Mark family support | none | — | not generated | Late MK1 after simpler families already reached MK3. |
| Shockwave Cannon | Gravity | Kinetic | 1 | — | physical | SCHEMA_BLOCKED | one-to-three Mark family support | none | — | not generated | Late common-ish specialist family. |
| Leviathan Heavy LMG | HeavyLMG | Kinetic | 3 | normal-firearm | physical | MANUAL_DESIGN_REVIEW | explicit top Strongbox tier policy | none | — | not generated | Apex power anchors are explicitly overridden. |
| Blaster | AutoRifle | Energized | 3 | normal-firearm | energy | READY_AFTER_SMALL_MAPPING | existing travelling projectile execution | per-Mark rarity normalized to family common; bullet collision radius uses documented 0.1 current-project convention | Content/Weapons/normal-firearm/blaster | validator + compiler passed | Early baseline energized family. |
| Arc Rifle | AutoRifle | Energized | 3 | normal-firearm | energy | READY_AFTER_SMALL_MAPPING | existing travelling projectile execution | per-Mark rarity normalized to family rare; bullet collision radius uses documented 0.1 current-project convention | Content/Weapons/normal-firearm/arc_rifle | validator + compiler passed | Simple energized automatic weapon. |
| Pulse Shotgun | Spread | Energized | 3 | shotgun | energy | READY_AFTER_SMALL_MAPPING | existing travelling projectile execution | per-Mark rarity normalized to family rare; bullet collision radius uses documented 0.1 current-project convention | Content/Weapons/shotgun/pulse_shotgun | validator + compiler passed | Elemental shotgun cousin. |
| Plasma Orb Launcher | Orb | Energized | 3 | — | energy | SCHEMA_BLOCKED | separate direct and area-damage values | none | — | not generated | Lane-control projectile. |
| Chain Weapon | Chain | Energized | 3 | — | energy | RUNTIME_BEHAVIOR_PENDING | live Unity chain-hit application | none | — | not generated | Team-friendly crowd clearer. |
| Pulse Rocket Launcher | FastLauncher | Energized | 3 | — | energy | SCHEMA_BLOCKED | separate direct and area-damage values | none | — | not generated | High-level Common MK3 remains viable. |
| Gravity Cannon | Gravity | Energized | 3 | — | energy | RUNTIME_BEHAVIOR_PENDING | charged displacement delivery strategy | none | — | not generated | Utility-heavy rare family. |
| Energized Autocannon | AutoRifle | Energized | 2 | normal-firearm | energy | SCHEMA_BLOCKED | one-to-three Mark family support | none | — | not generated | Intentional 'meh, this again' high-tier Common. |
| Flamethrower | ContinuousCone | Thermal | 3 | — | thermal | RUNTIME_BEHAVIOR_PENDING | continuous cone/tick delivery strategy | none | — | not generated | Immediate ticks; no heat resource. |
| Rocket Launcher | Launcher | Thermal | 3 | — | thermal | SCHEMA_BLOCKED | separate direct and area-damage values | none | — | not generated | Classic launcher progression. |
| Inferno Scattergun | Spread | Thermal | 3 | shotgun | thermal | READY_AFTER_SMALL_MAPPING | existing travelling projectile execution | per-Mark rarity normalized to family rare; bullet collision radius uses documented 0.1 current-project convention | Content/Weapons/shotgun/inferno_scattergun | validator + compiler passed | Variant does not imply trash. |
| Fast Rocket Launcher | FastLauncher | Thermal | 3 | — | thermal | SCHEMA_BLOCKED | separate direct and area-damage values | none | — | not generated | Faster, smaller launcher cousin. |
| Cluster Launcher | ClusterLauncher | Thermal | 3 | — | thermal | RUNTIME_BEHAVIOR_PENDING | child-projectile/cluster explosion strategy | none | — | not generated | Crowd-control specialist. |
| Homing Missile Launcher | Launcher | Thermal | 3 | — | thermal | SCHEMA_BLOCKED | separate direct and area-damage values | none | — | not generated | Tracking belongs to trajectory strategy. |
| Thermite Cannon | Mortar | Thermal | 3 | — | thermal | RUNTIME_BEHAVIOR_PENDING | arcing delivery plus persistent damage-zone strategy | none | — | not generated | High-level Common MK3 remains useful. |
| Thermal Carbine | AutoRifle | Thermal | 2 | normal-firearm | thermal | SCHEMA_BLOCKED | one-to-three Mark family support | none | — | not generated | High-tier filler that is fully viable. |
| Bio-Needler | AutoRifle | Chemical | 3 | normal-firearm | chemical | READY_AFTER_SMALL_MAPPING | existing travelling projectile execution | per-Mark rarity normalized to family common; bullet collision radius uses documented 0.1 current-project convention | Content/Weapons/normal-firearm/bio_needler | validator + compiler passed | Chemical baseline. |
| Cryo Cannon | AutoRifle | Chemical | 3 | normal-firearm | chemical | MANUAL_DESIGN_REVIEW | authored slowing effect values | none | — | not generated | Uses Chemical resistance category. |
| Corrosive Scattergun | Spread | Chemical | 3 | shotgun | chemical | READY_AFTER_SMALL_MAPPING | existing travelling projectile execution | per-Mark rarity normalized to family rare; bullet collision radius uses documented 0.1 current-project convention | Content/Weapons/shotgun/corrosive_scattergun | validator + compiler passed | Lower impact, stronger corrosion identity. |
| Mine Layer | MineLayer | Chemical | 3 | — | chemical | RUNTIME_BEHAVIOR_PENDING | mine placement and proximity strategy | none | — | not generated | Delayed damage and area control. |
| Corrosive Sprayer | Sprayer | Chemical | 3 | — | chemical | RUNTIME_BEHAVIOR_PENDING | continuous sprayer delivery strategy | none | — | not generated | Persistent damage-focused. |
| Acid Rifle | AutoRifle | Chemical | 3 | normal-firearm | chemical | READY_AFTER_SMALL_MAPPING | existing travelling projectile execution | per-Mark rarity normalized to family rare; bullet collision radius uses documented 0.1 current-project convention | Content/Weapons/normal-firearm/acid_rifle | validator + compiler passed | Straightforward late chemical option. |
| Toxic Mortar | Mortar | Chemical | 2 | — | chemical | SCHEMA_BLOCKED | one-to-three Mark family support | none | — | not generated | Late specialist. |
| Chem-Burst Carbine | BurstRifle | Chemical | 2 | — | chemical | SCHEMA_BLOCKED | one-to-three Mark family support | none | — | not generated | Intentional viable high-tier Common. |
| Reclaimer Acid Cannon | AcidPoolCannon | Chemical | 3 | — | chemical | MANUAL_DESIGN_REVIEW | explicit top Strongbox tier policy | none | — | not generated | Apex power anchors are explicitly overridden. |
| Photon Repeater | AutoRifle | Photonic | 3 | normal-firearm | — | MANUAL_DESIGN_REVIEW | intentional Photonic damage category | none | — | not generated | Early photonic baseline. |
| Prism Shotgun | Spread | Photonic | 3 | shotgun | — | MANUAL_DESIGN_REVIEW | intentional Photonic damage category | none | — | not generated | Photonic spread cousin. |
| Ricochet Weapon | Ricochet | Photonic | 3 | — | — | MANUAL_DESIGN_REVIEW | intentional Photonic damage category | none | — | not generated | Behavior remains fixed across marks. |
| Photon Burst Rifle | BurstRifle | Photonic | 3 | — | — | MANUAL_DESIGN_REVIEW | intentional Photonic damage category | none | — | not generated | High-speed burst cousin. |
| Beam Cannon | Beam | Photonic | 3 | — | — | MANUAL_DESIGN_REVIEW | intentional Photonic damage category | none | — | not generated | No charge or heat management. |
| Solar Lance | Precision | Photonic | 2 | normal-firearm | — | SCHEMA_BLOCKED | one-to-three Mark family support | none | — | not generated | Premium precision family. |
| Nova Projector | Orb | Photonic | 2 | — | — | SCHEMA_BLOCKED | one-to-three Mark family support | none | — | not generated | Area-control light family. |
| Light Drone Launcher | Drone | Photonic | 2 | — | — | SCHEMA_BLOCKED | one-to-three Mark family support | none | — | not generated | Late first-generation specialist. |
| Photonic Autocannon | AutoRifle | Photonic | 2 | normal-firearm | — | SCHEMA_BLOCKED | one-to-three Mark family support | none | — | not generated | Intentional high-tier Common. |
| Omni-Phase Assault System | OmniPhase | Omni-Phase | 3 | — | — | MANUAL_DESIGN_REVIEW | intentional Omni-Phase damage category | none | — | not generated | Only planned multi-damage-type weapon. |

## Validation boundary

The Node folder validator/compiler proves deterministic source shape only. Playability additionally requires the generated definition to reach `GunCatalogProvider.GunCatalog`, the equipment projection, Strongbox candidates, and a live Unity delivery strategy. Unity validation must be reported separately and is not implied by this document.
