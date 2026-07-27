# Inventory and Economy Audit — Current State and Roadmap

**Audit date:** 2026-07-27  
**Repository:** `YeerooXY/shooter-mover`  
**Scope:** inventory ownership, gear versus weapons, money, scrap, augments, and overclock modules/cores  
**Evidence level:** static source and architecture review only; no current-branch Unity compilation, automated test run, or manual gameplay acceptance was executed as part of this audit.

## Executive summary

Shooter Mover has strong low-level authorities, but they do not yet form one complete player-facing inventory and economy loop.

| Area | Core architecture | Production integration | Current conclusion |
|---|---|---|---|
| Canonical weapons | Strong | Partially integrated | Good ownership foundation; firing and upgrade paths are incomplete |
| Gear / armour | Older generic system | Persisted but poorly exposed | Functional storage, weak player-facing usability |
| Money | Strong typed wallet | Earning is connected; spending is not fully proven | Core ready, product loop unfinished |
| Scrap | Strong typed wallet | Strongbox earning is connected; Crafting binding is incomplete | Core ready, product loop unfinished |
| Augment upgrades | Mature generic-equipment service | Not reconciled with canonical weapons | Highest-risk integration gap |
| Overclock modules/cores | Assignment IDs only | No usable execution or ownership system | Product-planning stage, not a live feature |

The project is not blocked by a lack of systems. It is blocked by **systems from different architectural generations meeting at incomplete seams**.

The central split is:

```text
Weapons
    -> canonical WeaponHoldings authority
    -> canonical physical mount authority
    -> generic holding retained as compatibility/reward receipt

Armour and non-weapon gear
    -> generic PlayerHoldingsService
    -> generic exact-instance loadout projection

Money and scrap
    -> independent character-local ledger-backed wallets

Overclocks
    -> StableId references stored on a weapon
    -> no core inventory, installation transaction, or runtime effect policy
```

## Current authoritative ownership

| Concept | Current owner |
|---|---|
| Exact owned weapon | `ProductionWeaponHoldingsAuthorityV2` |
| Equipped weapon | `ProductionWeaponMountLoadoutAuthorityV2` |
| Weapon definition and mechanics | `WeaponDefinitionId` resolved through the weapon catalogue |
| Armour and non-weapon equipment | `PlayerHoldingsService` |
| Equipped armour | `ProductionInventoryLoadoutAuthorityV1` compatibility loadout |
| Generic weapon record | Compatibility/reward receipt, not canonical weapon ownership |
| Money balance | `MoneyWalletService` |
| Scrap balance | `ScrapWalletServiceV1` |
| Weapon augment assignment | `WeaponEquipmentInstance.AugmentAssignments` |
| Weapon overclock assignment | `WeaponEquipmentInstance.OverclockAssignments` |

Relevant production composition lives primarily in:

- `Assets/ShooterMover/Runtime/Application/Flow/Production/ProductionPlayerLoadoutRuntimeV1.cs`
- `Assets/ShooterMover/Runtime/Application/Flow/Production/ProductionCharacterRuntimeGraphV1.cs`
- `Assets/ShooterMover/Runtime/Application/Flow/Production/ProductionCharacterAuthorityAdaptersV1.cs`
- `Assets/ShooterMover/UI/ProductionFlow/ProductionHubLoadoutCompositionV1.cs`

## Findings

### 1. Weapons have a healthy canonical ownership model

The V2 weapon model is intentionally small:

- opaque exact instance identity;
- weapon definition identity;
- augment assignment IDs;
- overclock assignment IDs.

The canonical Inventory service selects and equips exact instances, rejects duplicate placement, respects class mount availability, and commits the physical mount authority before updating the older compatibility projection.

This is the correct direction: generic inventory data no longer remains the write authority for weapon ownership or equipped state.

#### Remaining weapon risks

- The canonical weapon no longer stores item level or quality. The compatibility projection may reconstruct a minimum-level/default-quality item or recover richer data from an old receipt. The product contract must state whether level and quality were intentionally removed from canonical weapon identity or are temporarily missing.
- The current playable slice has not yet demonstrated the complete route from exact equipped canonical weapon to firing with all augment/overclock effects.
- The Inventory screen can manage weapons, but the broader weapon lifecycle—upgrade, dismantle, replace, remove, and recover after interrupted operations—is not yet one canonical transaction family.

### 2. Gear is persisted but effectively second-class

The generic loadout still defines four armour slots:

- head;
- body;
- legs;
- feet.

However, armour validation currently checks the broad `Armor` category rather than a typed armour location. A valid armour item can therefore satisfy any armour slot unless additional rules exist elsewhere.

More importantly, the live canonical Inventory presentation is weapon-focused:

- Equipped Weapons;
- Owned Weapons;
- Selected Weapon.

Armour remains in generic holdings and persistence, but the current canonical player flow does not provide an equivalent first-class way to:

- browse owned armour;
- compare armour;
- select an armour slot;
- equip or unequip armour;
- inspect armour augments.

The preferred direction is a sibling gear service, not merging armour back into weapon authority:

```text
CanonicalGearInventoryServiceV2
    -> generic non-weapon holdings
    -> typed armour-slot compatibility
    -> exact gear-instance selection
    -> shared Inventory presentation
```

### 3. Money and scrap cores are stronger than their product integration

Both wallets are character-local, typed, ledger-backed, deterministic, and persisted through character save-component adapters.

They support:

- positive grants and bounded spends;
- insufficient-funds rejection;
- expected-sequence checks;
- exact duplicate no-change;
- conflicting duplicate rejection;
- checked overflow handling;
- deterministic snapshots;
- fail-closed imports.

Scrap also retains richer reason and provenance information than money.

Production strongbox reward application is connected to both wallets, so money and scrap can be earned through the existing reward route.

The weaker side is spending and presentation:

- the Hub does not display persistent money/scrap balances;
- the central production coordinator initially enters Shop and Crafting in disconnected mode;
- Inventory has a later dedicated composition root that reconnects it to the selected character graph;
- no equivalent current production binder was confirmed for Shop and Crafting during this audit.

This means the wallet authorities are ready, but the complete player loop is not yet proven:

```text
earn -> display balance -> spend -> receive item -> persist -> restart -> retain result
```

### 4. Generic augment upgrades are unsafe for canonical weapons

`AugmentUpgradeServiceV1` predates canonical weapon ownership. It:

1. reads an embedded `EquipmentInstance` from generic holdings;
2. spends money;
3. removes that generic holding;
4. creates a replacement generic equipment instance;
5. grants the replacement through reward application.

It does not own or update:

- canonical weapon holdings;
- canonical weapon assignments;
- canonical physical mount references;
- the relationship between old and replacement canonical weapon instances.

The service can remain suitable for generic armour, but it must not silently operate on canonical weapons.

Until a canonical weapon upgrade transaction exists, weapon augment upgrades should fail explicitly rather than mutating only a compatibility receipt.

### 5. Overclock modules/cores are not implemented yet

The weapon value has an immutable sorted list of overclock assignment IDs, and the Inventory UI can display those IDs.

That is only a persistence seam. There is no confirmed production authority for:

- owned core quantity or exact core instances;
- overclock definitions;
- acquisition provenance;
- installation or removal;
- capacity cost;
- signature choices;
- compatibility validation;
- runtime execution;
- refund, salvage, or recovery.

The retained gameplay projection currently fails closed when a weapon has any overclock assignment. Therefore non-empty overclock assignments must not enter player-facing production until an execution policy exists.

The current product-vision document correctly treats overclock cores as a later system after the basic loot, inventory, and crafting loops are stable.

## Recommended parallel-work capacity

### Recommended maximum: three active lanes

Shooter Mover can support **three simultaneous workstreams** in this area only when ownership is kept strict:

1. **one high-risk authority/runtime lane**;
2. **one separate UI or non-overlapping domain lane**;
3. **one design, testing, documentation, or production-binding lane**.

For code-heavy work that changes production authorities, persistence, or shared composition roots, the safer limit is **two simultaneous implementation lanes**.

More than three active feature PRs in this cluster is likely to cause collisions in:

- `ProductionCharacterRuntimeGraphV1`;
- save-component adapters;
- reward application composition;
- Inventory presentation contracts;
- Shop/Crafting composition;
- shared equipment catalogues;
- canonical versus compatibility projections.

### Safe parallel lanes after the first contract gate

| Lane | Work | Can run in parallel with | Must avoid |
|---|---|---|---|
| A | Canonical weapon augment transaction | B and C with strict file ownership | Gear UI, Shop/Crafting UI, unrelated catalogue rewrites |
| B | Gear Inventory and typed armour slots | A and C | Canonical weapon ownership and mount authority |
| C | Shop/Crafting production binding and Hub balances | A and B | Rewriting wallet internals or inventory ownership |
| D | Overclock product design and contracts only | A, B, and C | Runtime installation/execution before A is complete |
| E | Combined vertical validation | Nothing substantial | Must be the integration/reconciliation phase |

### Practical portfolio rule for the other audits

Across all current feature audits, count a feature as **high-collision** when it changes any of these:

- selected-character runtime graph;
- save adapters or schemas;
- reward application authority composition;
- shared production flow coordinator;
- canonical holdings or loadout authorities.

Run no more than **two high-collision features at once**. Additional parallel work should be isolated content, UI presentation over stable ports, tests, simulation, documentation, or design contracts.

## Five-step future plan

### Step 1 — Freeze unsafe paths and settle the contracts

**Goal:** prevent new content from entering unsupported states while making the next implementation decisions explicit.

Decide and document:

- whether canonical weapons intentionally have no item level/quality;
- immutable replacement versus same-identity mutation for weapon upgrades;
- whether augment assignment IDs identify definitions or exact module instances;
- whether overclock assignment is singular, slotted, or capacity-based;
- character-bound versus account-wide ownership for future cores.

Add fail-closed guards for:

- canonical weapon use through the old generic augment-upgrade service;
- non-empty overclock assignments entering live rewards or installation;
- unresolved canonical weapon definitions during destructive operations.

**Parallelism:** this is the short shared gate before the main implementation lanes split.

### Step 2 — Implement the canonical weapon augment transaction

**Goal:** make one exact equipped weapon upgrade safely and recoverably.

The transaction must coordinate:

- money spend;
- canonical weapon replacement or assignment mutation;
- physical mount references;
- compatibility/reward receipt state;
- persistence and retry records.

Required hostile cases:

- insufficient money;
- stale quote;
- missing weapon;
- mounted weapon replacement;
- duplicate confirmation;
- conflicting duplicate;
- interruption after each authority mutation;
- restart and retry;
- cleanup failure after commit.

**Ownership:** weapon domain, weapon holdings, weapon mounts, upgrade application, reward compatibility, and focused persistence additions.

### Step 3 — Restore gear as a first-class Inventory feature

**Goal:** let the player browse, inspect, equip, and unequip armour without weakening canonical weapon ownership.

Implement:

- typed armour-location compatibility;
- exact gear cards and details;
- gear-slot selection in the Inventory screen;
- preserved weapon-only canonical authority;
- save/restart proof with mixed weapon and armour loadout.

**Parallelism:** can run alongside Step 2 if it does not modify canonical weapon holdings or the weapon mount authority. Shared Inventory UI changes should be coordinated through a narrow projection contract or merged after the service layers are complete.

### Step 4 — Connect the visible economy loop

**Goal:** make the existing wallets visibly useful in the canonical Hub flow.

Implement production composition for:

- money and scrap balances in the Hub;
- connected Shop using the selected character wallet and holdings;
- connected Crafting using the selected character scrap wallet, holdings, progression, generation, and reward application;
- persistence immediately after accepted purchase/craft operations;
- reconnect/restart recovery for pending retryable operations.

Add richer money provenance before many more spend/grant sources are introduced.

**Parallelism:** can run alongside Steps 2 and 3 if it remains a composition/presentation change and does not rewrite wallet or inventory authorities.

### Step 5 — Introduce overclocks only through one end-to-end vertical slice

**Goal:** prove one real overclock core from acquisition to gameplay before broadening the system.

Start with one tightly bounded rule, for example:

- one exact core item;
- one compatible weapon family;
- one deterministic installation action;
- one authored benefit and drawback;
- one runtime execution policy;
- one persistence/restart path;
- one removal or salvage decision.

Do not begin broad overclock content before Steps 2 and 4 are stable. Overclock installation depends on a trusted canonical weapon mutation/replacement route and a connected economy/inventory loop.

Finish with one combined acceptance path:

```text
open strongbox
-> receive money/scrap/equipment
-> inspect inventory
-> equip gear and weapon
-> buy or craft
-> upgrade exact weapon
-> save and restart
-> load the same state
-> fire the exact equipped weapon with the expected effect
```

## Suggested PR sequence

```text
PR 1: Inventory/economy safety guards and contract decisions

Parallel after PR 1:
    PR 2A: Canonical weapon augment transaction foundation
    PR 2B: Gear slot typing and gear inventory service
    PR 2C: Shop/Crafting/Hub production composition

Then:
    PR 3A: Weapon upgrade persistence, retry, and mounted replacement
    PR 3B: Combined Inventory presentation
    PR 3C: Economy persistence and reconnect proof

Finally:
    PR 4: One overclock vertical slice
    PR 5: Combined hostile integration and manual acceptance
```

Keep each PR within one primary architectural responsibility. Avoid combining wallet changes, weapon ownership changes, gear UI, and overclock execution in one branch.

## Acceptance gates

A feature should not be considered complete because its domain service exists. It is complete when the real production path proves:

1. one authoritative owner;
2. no fallback or compatibility projection becomes a write authority;
3. persistence stores and restores the authoritative model;
4. retry cannot duplicate spend or equipment;
5. partial failure has an intentional recovery path;
6. the canonical Hub or gameplay workflow reaches the implementation;
7. Unity compilation and focused tests pass on the combined branch;
8. one manual acceptance workflow is recorded.

## Validation status for this audit

- Static source review: completed.
- Architectural ownership trace: completed.
- Current-branch compilation: not executed.
- Automated tests: not executed.
- Unity EditMode/PlayMode tests: not executed.
- Manual gameplay acceptance: not executed.
- Performance testing: not applicable to this documentation audit.

The repository had no attached status checks on the inspected latest `main` commit at audit time. Previous PR-level test records are useful historical evidence but do not prove the current combined source.

## Immediate recommendation

Begin with **Step 1**, then run **Steps 2, 3, and 4 as the three parallel lanes** with strict ownership. Treat Step 2 as the highest-risk lane and give it exclusive ownership over canonical weapon mutation and mount replacement. Keep overclock implementation out of production until those lanes converge and the combined economy/inventory path has passed restart and manual acceptance.
