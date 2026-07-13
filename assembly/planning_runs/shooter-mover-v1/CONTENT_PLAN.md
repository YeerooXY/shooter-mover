# Representative Content and Factory Plan

Names are working production identifiers, not final marketing copy. Counts and archetypes are frozen for task splitting; numeric balance remains prototype-controlled.

## 1. Stage 1 weapon set

Stage 1 uses six weapons so multiple four-slot combinations cover the accepted firing models.

| Stable ID | Working name | Primary rhythm | Purpose | Power archetype |
|---|---|---|---|---|
| `weapon.needle-autocannon` | Needle Autocannon | fast automatic fire with bounded spread/recoil growth | continuous reference weapon and audiovisual prioritization | rapid surge |
| `weapon.foundry-cannon` | Foundry Cannon | slow heavy projectile with recovery and authored push | slow cadence, convergence, recoil, boost counterplay | explosive fragmentation |
| `weapon.scatter-array` | Scatter Array | short burst or spread volley | range choice, crowd pressure, multi-projectile density | focused overcharge |
| `weapon.thermal-beam` | Thermal Beam | sustained beam with heat and recovery | continuous hit feedback, heat, movement trade-off, cover | high-intensity overcharge |
| `weapon.coil-lance` | Coil Lance | charge/release piercing line shot | visible charge, commitment, precision, heavy timing | piercing surge |
| `weapon.wasp-rack` | Wasp Rack | paced micro-missiles with bounded homing | utility/homing, physical threats, target priority | swarm surge |

Stage 1 fixed-loadout comparisons use reproducible four-weapon sets. Its tiny randomized wrapper chooses from these six but does not persist rewards.

## 2. Stage 2 weapon additions

| Stable ID | Working name | Primary rhythm | Purpose | Power archetype |
|---|---|---|---|---|
| `weapon.arc-projector` | Arc Projector | short-range pulses with bounded chaining | unusual multi-target synergy without modifiers | extended chaining |
| `weapon.slag-mortar` | Slag Mortar | lobbed area projectile with deliberate recovery | area denial, cover exception, route control | fragmentation field |

The complete slice therefore has exactly eight identical-copy base weapons; four are equipped simultaneously.

Keep four reusable empowered-fire archetypes: overcharge, fragmentation, piercing, and surge. A weapon may tune an archetype but does not receive an unrelated bespoke second design merely to use power ammo.

## 3. Enemy roster

### Stage 1 ordinary roles

| Stable ID | Working name | Role | Key pressure |
|---|---|---|---|
| `enemy.cutter-drone` | Cutter Drone | close-pressure pursuer, light | pursuit, contact danger, shove-through, crowd pathing |
| `enemy.sentry-gunner` | Sentry Gunner | ranged projectile, medium | aimed bursts, visible lock attacks, cover interaction |
| `enemy.fabricator-mortar` | Fabricator Mortar | positioning and area denial, medium | telegraphed lobbed hazards, displacement, destructible payload option |

### Stage 1 elite

`enemy.foreman-elite` — **Foreman Elite**

- tougher readable machine, not the final boss;
- combines a bounded gunner burst, denial pulse, and purposeful repositioning;
- introduces one authored barrage peak;
- remains a single encounter unit built from reusable modules.

### Stage 2 ordinary additions

| Stable ID | Working name | Role | Key pressure |
|---|---|---|---|
| `enemy.bulwark-loader` | Bulwark Loader | heavy blocker and formation anchor | solid boost collision, protected lanes, slow push, weak exposure |
| `enemy.interceptor-drone` | Interceptor Drone | mobile flanker and physical-threat launcher | bounded prediction, shootable rockets/drones, nearby-room pursuit |

The complete factory has five ordinary roles. Variety comes from composition, geometry, reinforcement timing, hazards, and difficulty overrides—not roster inflation.

## 4. Upgraded-droid climax

`enemy.prototype-overseer` — **Prototype Overseer**

Use one readable health bar and one escalation threshold; no multi-stage spectacle.

Attacks:

1. **Committed burst cannon** — tracks during wind-up, visibly locks, then fires a learnable burst.
2. **Rail-line shot** — narrow high-threat line with clear commitment and cover interaction.
3. **Micro-missile fan** — bounded physical projectiles that may be shot down.
4. **Production-floor barrage** — authored radial/lane peak, not permanent screen fill.
5. **Thruster reposition** — short deterministic relocation that changes firing angle without teleport ambiguity.

At the escalation threshold, cadence and combinations change within declared bounds; the arena, objective, health model, and core rules do not become a second production.

## 5. Factory topology

Target: 24 meaningful rooms, four zones, 18-room critical route, six optional rooms, four teleports, one shop-enabled teleport, and two secure-storage rooms.

### Zone A — Receiving and Intake

Critical rooms:

1. `factory.receiving-entry`
2. `factory.cargo-sort`
3. `factory.security-gate`
4. `factory.intake-line`
5. `factory.freight-junction`
6. `factory.teleport-a`

Optional:

- `factory.receiving-vault` — secure storage;
- `factory.scrap-inspection` — challenge/reward.

Objective: acquire production-network access and disable intake security.

### Zone B — Assembly Lines

Critical rooms:

7. `factory.frame-assembly`
8. `factory.conveyor-crossing`
9. `factory.weapon-mount-line`
10. `factory.assembly-control`
11. `factory.foreman-floor`
12. `factory.teleport-b-shop`

Optional:

- `factory.tooling-cache` — strongbox or refresh-token challenge;
- `factory.maintenance-bypass` — route shortcut.

Objective: halt the primary assembly line and defeat the Foreman Elite.

### Zone C — Test and Calibration

Critical rooms:

13. `factory.ballistics-range`
14. `factory.mobility-range`
15. `factory.thermal-calibration`
16. `factory.test-control`
17. `factory.power-transfer`
18. `factory.teleport-c`

Optional:

- `factory.test-vault` — second secure storage;
- `factory.prototype-cell` — high-pressure reward/refresh-token room.

Objective: disable the test network and sever one production-core conduit.

### Zone D — Core Control

Critical rooms:

19. `factory.core-access`
20. `factory.teleport-d`
21. `factory.coolant-routing`
22. `factory.core-antechamber`
23. `factory.overseer-arena`
24. `factory.production-core`

Objective chain: reach the core network, sever the final conduit, defeat the Prototype Overseer, shut down production, resolve completion, and return to the menu hub.

## 6. Topology rules

- Critical navigation remains understandable from explored-map state and light objective guidance.
- Optional rooms never hide mandatory completion items.
- Four teleports fit the six-room cadence; only `teleport-b-shop` hosts the slice shop.
- Banking remains separate from teleports.
- Shop purchases secure immediately; collected rewards remain provisional until banking or completion.
- Cleared rooms remain clear; persistent hazards remain legible.
- Ordinary encounters permit retreat; Foreman and Overseer use explicit lockdowns.
- Fast travel uses activated teleports only, outside combat, and cannot refresh content.
- Difficulty changes authored formations, reinforcements, warnings, recovery, and checkpoint pressure without hidden topology changes.

## 7. Representative final-art subset

Pass the following through the real pipeline during Stage 2:

- player mech;
- Cutter Drone;
- Foreman Elite or Prototype Overseer;
- Needle Autocannon;
- Foundry Cannon or Thermal Beam;
- one Assembly Line machine/environment set;
- representative normal maps, emissives, shadows, animation, damage, destruction, projectiles, and effects.

The rest may remain coherent readability-complete temporary art until the pipeline is proven.
