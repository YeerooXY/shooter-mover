# Representative Content and Factory Plan

Names are working production identifiers, not final marketing copy. Counts and archetypes are frozen for task splitting as amended by `AMENDMENT_STAGE1_WEAPONS.md` and `AMENDMENT_STAGE1_ENEMIES.md`; numeric balance remains prototype-controlled.

## 1. Stage 1 weapon set

Stage 1 uses five deliberately simple weapons so multiple four-slot combinations can test identity and readability without exhausting the S1.2 review reserve.

| Stable ID | Working name | Primary rhythm | Purpose | Empowered profile |
|---|---|---|---|---|
| `weapon.blaster-machine-gun` | Blaster Machine Gun | straightforward automatic projectile fire | default starting weapon and continuous reference | numeric base-stat uplift only |
| `weapon.shotgun` | Shotgun | close-range multi-projectile spread | range choice and crowd pressure | numeric base-stat uplift only |
| `weapon.rocket-launcher` | Rocket Launcher | paced projectile with bounded area detonation | simple area damage and deliberate recovery | numeric base-stat uplift only |
| `weapon.arc-gun` | Arc Gun | primary hit chaining to at most three additional nearby targets | bounded multi-target synergy | numeric base-stat uplift; chain cap unchanged |
| `weapon.ricochet-gun` | Ricochet Gun | long-lived projectile with at most two wall bounces | geometry play and bank-shot identity | numeric base-stat uplift; bounce cap unchanged |

Stage 1 fixed-loadout comparisons use reproducible four-weapon sets. Its tiny randomized wrapper chooses from these five but does not persist rewards.

## 2. Stage 2 weapon additions

The complete slice still targets exactly eight identical-copy base weapons; four are equipped simultaneously. The remaining three weapon identities are intentionally deferred until Stage 1 evidence exists. They require a planning amendment before Stage 2 combat-content tasks are generated or dispatched.

For the Stage 1 proof, empowered fire only tunes existing numeric coefficients. It does not add unrelated bespoke behavior, increase the Arc Gun's three-additional-target cap, increase the Ricochet Gun's two-bounce cap, or introduce mature modifier systems.

## 3. Enemy roster

### Stage 1 ordinary roles

| Stable ID | Working name | Role | Key pressure |
|---|---|---|---|
| `enemy.pursuer-drone` | Pursuer Drone | basic melee/contact pursuer | direct approach, ordinary contact damage, simple crowd pressure |
| `enemy.ram-droid` | Ram Droid | small fast disposable contact attacker | low health, fast approach, one bounded impact hit, self-destruction on player collision |
| `enemy.mobile-blaster-droid` | Mobile Blaster Droid | moving ranged projectile attacker | simple movement, readable cadence, accepted blaster projectile profile |
| `enemy.blaster-turret` | Blaster Turret | stationary ranged projectile attacker | fixed position, clear line of fire, accepted blaster projectile profile |

### Stage 1 elite

`enemy.four-blaster-elite` — **Four-Blaster Elite**

- easy, readable first boss rather than the final upgraded-droid climax;
- uses four blaster origins with mild bounded spread and simple deterministic cadence;
- provides clear wind-up/recovery windows and no phase transition;
- has no denial pulse, mortar, reinforcement, teleport, complex repositioning or bullet-hell barrage;
- remains a single encounter unit built from accepted reusable blaster and enemy modules.

### Stage 2 ordinary addition

The complete factory still targets five ordinary roles. The one remaining Stage 2 ordinary role is intentionally deferred until Stage 1 evidence exists and requires a planning amendment before Stage 2 combat-content generation or dispatch. Variety comes from composition, geometry, reinforcement timing, hazards, and difficulty overrides—not roster inflation.

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

Objective: halt the primary assembly line and defeat the Four-Blaster Elite.

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
- Ordinary encounters permit retreat; Four-Blaster Elite and Overseer use explicit lockdowns.
- Fast travel uses activated teleports only, outside combat, and cannot refresh content.
- Difficulty changes authored formations, reinforcements, warnings, recovery, and checkpoint pressure without hidden topology changes.

## 7. Representative final-art subset

Pass the following through the real pipeline during Stage 2:

- player mech;
- Pursuer Drone;
- Four-Blaster Elite or Prototype Overseer;
- Blaster Machine Gun;
- Rocket Launcher or Arc Gun;
- one Assembly Line machine/environment set;
- representative normal maps, emissives, shadows, animation, damage, destruction, projectiles, and effects.

The rest may remain coherent readability-complete temporary art until the pipeline is proven.
