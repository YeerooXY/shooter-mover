# Weapon and Shooting System Audit and Forward Plan

## Status

- **Document type:** architecture audit and future delivery plan
- **Repository baseline:** `main`, inspected on 2026-07-27
- **Change type:** documentation only
- **Implementation status:** no weapon runtime behaviour is changed by this document

## Purpose

This document records the current production truth for Shooter Mover's player weapon and shooting system and proposes the shortest maintainable route to visible, highly configurable weapon experimentation.

It is intended to support portfolio planning across the other active system audits: it identifies which weapon work must be serialized, which work can safely run in parallel, and when this feature can expand from one implementation lane into several independent lanes.

## Executive summary

The weapon system has a strong canonical backend but current `main` stops immediately before visible firing.

Current production code already provides:

- one canonical authored weapon catalogue;
- 18 provisional weapons across six families and three Marks per family;
- exact immutable weapon-instance ownership;
- class-authored physical weapon mounts;
- persistent equip, replace and unequip behaviour;
- exact equipped-weapon binding onto the spawned player;
- reusable scheduling for semi-automatic, automatic and burst fire;
- canonical spread and multi-projectile launch generation;
- projectile lifecycle, Pierce, Ricochet, guidance, impact, explosion and damage-over-time domain machinery.

Current `main` does **not** provide:

- a production player firing-input loop;
- a production `IInventoryWeaponEffectBatchSink`;
- a Unity adapter that consumes canonical launch effects and creates visible projectiles;
- a complete current-main projectile-to-enemy damage path;
- visible firing for any of the 18 catalogue weapons.

The next milestone should therefore not add more catalogue families. It should complete one generic canonical firing and projectile pipeline, then place a development-only weapon-combination lab on top of it.

## Current production architecture

### Canonical weapon content

`ProductionWeaponCatalogueV1` is the single authored production weapon-content authority. It produces:

- canonical `WeaponBlueprint` definitions;
- a compatibility `WeaponCatalog` projection;
- a compatibility `EquipmentCatalog` projection.

`ProductionWeaponCatalogProvider` exposes those shared projections. The flat catalogues remain necessary for existing strongbox, inventory, shop and simulator boundaries, but they are not intended to become a second authored combat-mechanics authority.

The current provisional catalogue contains:

| Family | Rarity | Representative mechanics |
|---|---|---|
| Rattler | Common | Physical Normal projectile; automatic, semi-automatic and burst variants |
| Ironwake | Common | Physical shotgun spread with 6, 8 and 10 projectiles |
| Voltspike | Rare | Energy homing projectile; semi-automatic, automatic and burst variants |
| Prismata | Epic | Chemical slow Orb delivery |
| Crownfall | Legendary | Thermal contact Rocket with explosion |
| Nullstar | Artifact | Chemical direct damage plus stacking and refreshing damage over time |

The catalogue is explicitly a provisional system-test matrix. Its purpose is to exercise mechanics and integration boundaries rather than represent final balance.

### Canonical ownership and equipped state

Weapon ownership is authoritative in:

```text
InstanceId
-> ProductionWeaponHoldingsAuthorityV2
-> WeaponEquipmentInstance
-> WeaponDefinitionId
-> ProductionWeaponCatalogueV1
```

Equipped state is authoritative in:

```text
WeaponMountLoadoutSnapshotV2
-> MountId -> InstanceId
```

The retained generic holdings and legacy loadout structures are compatibility boundaries:

- generic holdings retain non-weapon inventory and immutable reward receipts;
- the retained loadout remains an armour and route compatibility projection;
- V2 weapon ownership and mount state are the production truth.

Fresh-character onboarding creates one distinct exact starter instance for every active physical mount. Inventory opening does not grant or repair missing weapons.

### Spawned-player handoff

The current production route reaches the spawned player:

```text
canonical catalogue
-> exact weapon instance
-> canonical holdings
-> physical mount loadout
-> first active equipped weapon
-> ProductionCanonicalWeaponGameplayBindingV2
-> CanonicalPlayerWeaponSourceV2 on the spawned player
```

`CanonicalPlayerWeaponSourceV2` verifies the selected character, exact instance ownership and canonical definition identity. It has no fallback and cannot silently rebind to another weapon during the same player lifecycle.

This is the final connected production seam on current `main`.

### Reusable but disconnected execution machinery

The repository already contains reusable execution components for:

- exact equipment-to-effective-weapon resolution;
- trigger and cadence scheduling through `WeaponFiringScheduler`;
- semi-automatic, automatic and burst fire;
- deterministic spread;
- multiple projectiles per accepted emission;
- immutable canonical projectile launch requests;
- pending-delivery and replay handling;
- Normal, Orb and Rocket projectile profiles;
- guidance, Pierce, Ricochet, impact and explosion decisions.

`PlayerInventoryWeaponRuntimeCompositionRoot.CreateCanonical(...)` can compose the inventory-backed runtime, but it requires a caller-owned `IInventoryWeaponEffectBatchSink`. Current `main` provides no production scene sink and no player input controller that advances this runtime.

## What is genuinely working

The word "working" must be separated into three levels.

### Authored and structurally represented

All 18 current catalogue definitions are authored through the canonical model and exercise the intended system matrix.

### Implemented in reusable domain/application code

The reusable code supports or models:

- semi-automatic, automatic and burst cadence;
- single and deterministic spread patterns;
- multiple projectile launch effects;
- Normal, Orb and Rocket delivery;
- homing guidance state;
- Pierce and fixed-point Ricochet;
- contact and range impact policies;
- canonical Rocket explosion resolution;
- damage-over-time data and effect contracts;
- deterministic identities and replay-safe state.

### Visible in current playable `main`

No player weapon is currently confirmed as visibly firing in current `main`.

Rattler, Ironwake, Voltspike, Prismata, Crownfall and Nullstar all reach catalogue, ownership, loadout and spawned-player binding, but the production route stops before trigger processing and visual effect delivery.

## Legacy and duplicate-system findings

### Retired starter catalogue

The old fixed starter catalogue and fixed demo-instance approach have been removed from current production. The current catalogue and exact-instance onboarding are the intended authority.

### Stage 1 weapon implementation

The earlier Stage 1 weapon execution source is not present on current `main`. It is not a second live production weapon authority.

### Historical first-combat-room projectile slice

A more recent combat integration branch previously contained:

- `ProductionPlayablePlayerWeaponControllerV1`;
- `ProductionNormalProjectileEffectSink2D`.

That work is useful reference material for input wiring, replay handling, source-collider suppression, enemy damage command creation and terminal-impact retry. It should not be merged wholesale because its presentation sink was intentionally narrow and rejected several mechanics needed by the current catalogue, including multi-projectile spread, homing, Rocket explosions, area damage and damage over time.

The correct future implementation should reuse the canonical contracts and useful failure-handling ideas without preserving the single-projectile or Normal-only assumptions.

## Important architectural correction

Live combat should resolve the exact canonical `WeaponBlueprint` directly from the exact weapon instance's `WeaponDefinitionId`.

The intended execution route is:

```text
WeaponEquipmentInstance
-> WeaponDefinitionId
-> ProductionWeaponCatalogueV1.TryGetBlueprint(...)
-> EffectiveWeaponFactory
-> WeaponFiringScheduler
-> canonical projectile launch effects
-> Unity effect sink
```

The flat `WeaponCatalog` projection is intentionally lossy. It is suitable for compatibility consumers and summaries, but it cannot safely reconstruct every canonical homing, Rocket impact, explosion and damage-over-time semantic.

No future firing composition should create a second mechanics authority by rebuilding production weapons from flat scalar catalogue values.

## Target capability

The target is a modular weapon platform where combinations are data, not subclasses.

For example, a shotgun rocket launcher should be represented as:

```text
fire mode: semi-automatic
shot pattern: eight projectiles with deterministic spread
delivery: Rocket
impact: detonate on enemy, wall or range expiry
effect: thermal explosion
```

It should not require a dedicated `ShotgunRocketLauncher` class.

The same generic runtime should also permit combinations such as:

- automatic micro-rockets;
- burst rocket spread;
- homing shotgun pellets;
- slow explosive Orbs;
- poison projectiles with stacking damage over time;
- high-Pierce spread weapons;
- later, Ricochet and chain combinations where their execution policies are supported.

# Five-step future plan

## Step 1 — Canonical firing cutover

**Goal:** establish one authoritative exact-instance-to-executable-weapon route.

Deliverables:

- direct canonical blueprint resolution from `WeaponDefinitionId`;
- production composition from `CanonicalPlayerWeaponSourceV2` or the exact canonical loadout runtime;
- one input/controller boundary that feeds aim and trigger state into the existing scheduler;
- explicit rejection of lossy flat-catalogue combat reconstruction;
- focused tests proving exact instance and definition identity are preserved;
- no fallback weapon creation.

This is the foundation step and should have one primary owner. Other weapon-runtime implementation should not branch independently before this contract is fixed.

## Step 2 — Generic projectile and explosion vertical slice

**Goal:** complete the first visible canonical combat route using one generic sink.

Initial supported matrix:

- Normal projectiles;
- multiple projectiles per shot;
- deterministic spread;
- Rocket delivery;
- direct enemy damage;
- wall and range termination;
- contact explosions;
- presentation trails and impacts.

This should make Rattler, Ironwake and Crownfall visibly usable and should demonstrate a shotgun-plus-Rocket combination without weapon-specific runtime classes.

After Step 1 freezes the controller/sink contract, Step 2 can be split into two coordinated lanes:

1. **controller and scheduler lane** — input, simulation ticks, lifecycle and pending delivery;
2. **projectile and damage lane** — Unity presentation, contacts, canonical impact, explosions and enemy damage.

## Step 3 — Development Weapon Chaos Lab

**Goal:** make weapon experimentation fast, visible and isolated from production progression.

Deliverables:

- dedicated development scene or mode;
- dense reusable damage dummies or enemy waves;
- runtime selectors for fire mode, rate, projectile count, spread, delivery, guidance, impact, effect, speed, range, size, damage and knockback;
- a small set of named chaos presets;
- projectile, hit and damage diagnostics;
- arena reset and quick weapon reload;
- ephemeral exact weapon instances that are not persisted or added to strongboxes.

Once the Step 2 sink interface is stable, most Chaos Lab scene, UI and presentation work can run in parallel with completion of the vertical slice.

Successful experiments can later be promoted into authored MK1-MK3 production families.

## Step 4 — Advanced delivery and persistent effects

**Goal:** activate the remaining current catalogue mechanics.

Two largely independent lanes can run in parallel after the baseline projectile sink is stable:

1. **guidance and Orb lane** — target acquisition, homing updates, reacquisition and Orb presentation for Voltspike and Prismata;
2. **damage-over-time lane** — application, stacking, refresh, lifecycle and presentation for Nullstar.

Each lane should extend the same canonical projectile/effect path rather than introduce family-specific controllers or sinks.

## Step 5 — Multi-mount integration and production hardening

**Goal:** turn the successful sandbox pipeline into the durable production weapon platform.

Deliverables:

- explicit policy for multiple active physical mounts;
- selected-mount switching, synchronized firing or another deliberate class-level firing policy;
- pooling and performance budgets for dense projectile counts;
- presentation lookup and fallback policy;
- player-defeat and scene-unload cleanup;
- replay, duplicate-contact and terminal-impact retry coverage;
- current catalogue acceptance matrix;
- Unity compilation, EditMode, PlayMode and manual gameplay evidence;
- documented promotion path from Chaos Lab preset to production weapon family.

This is a convergence and hardening step. It should be integrated after the preceding feature lanes have stable contracts rather than developed as another independent parallel branch.

# Concurrency recommendation

## Weapon-system capacity by phase

| Phase | Safe weapon workstreams | Notes |
|---|---:|---|
| Before Step 1 completes | **1 primary + 1 low-risk preparation lane** | Keep the authoritative resolver/controller contract under one owner. A second lane may prepare art, test-scene layout or acceptance fixtures without changing runtime contracts. |
| After Step 1 contract freeze | **2 implementation lanes** | Controller/scheduler and projectile/damage can proceed in parallel against an explicit sink contract. |
| After Step 2 sink stabilizes | **3 weapon-related lanes** | Chaos Lab UI/scene, guidance/Orb and damage-over-time can be separated, provided they share the same canonical sink and contact contracts. |
| Step 5 integration | **1 integration owner plus focused support** | Converge branches, performance, multi-mount policy and acceptance evidence through one integration boundary. |

## Portfolio-level implication

At the beginning, the weapon system should consume **one main feature slot** in the overall project portfolio. It is not yet safe to assign several independent agents to different weapon mechanics because they would all touch the missing canonical firing and sink boundary.

After Step 1 and the Step 2 interface split, the weapon programme can safely consume **two concurrent implementation slots**. Once the generic projectile sink is stable, it can temporarily expand to **three parallel weapon lanes** without creating separate mechanics authorities.

Therefore the recommended planning assumption alongside the other audits is:

- reserve **one slot now** for the weapon foundation;
- allow **two slots** after the canonical firing contract lands;
- allow **up to three slots** only after the generic projectile sink and contact model are stable;
- return to **one integration slot** for production hardening.

# Guardrails

Future weapon work should preserve these rules:

1. `WeaponBlueprint` remains the authored combat authority.
2. Exact `WeaponEquipmentInstance` identity remains intact through firing and damage.
3. `WeaponFiringScheduler` remains the cadence and burst authority.
4. Unity adapters consume baked canonical values and do not rebuild weapon facts.
5. Shotgun, Rocket, homing, Orb and damage-over-time are composable mechanics, not owner-specific class hierarchies.
6. Flat catalogue projections remain compatibility views, not combat-authoring sources.
7. Development-only Chaos Lab weapons remain outside persistence, economy, strongboxes and production progression.
8. No feature is described as working in-game until Unity compilation and runtime evidence exist.

## Recommended next task

The next implementation task should be **Step 1 — Canonical firing cutover**, with an explicit small contract for the future generic projectile sink. That creates the narrowest stable integration point for the other audits and unlocks safe parallel weapon development immediately afterward.
