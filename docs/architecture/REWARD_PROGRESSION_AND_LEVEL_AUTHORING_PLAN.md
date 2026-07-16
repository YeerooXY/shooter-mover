# Reward, progression, and level-authoring implementation plan

# 1. Executive summary

The repository is ready for a **contract-first expansion**, not a broad rewrite. Its existing inward-only assembly structure, `StableId`, combat messages, enemy lifecycle, room projection, encounter lifecycle, registry tooling, restart behavior, and physical projectile boundaries are strong foundations. The new work should add a unified object-authoring, reward, equipment, economy, progression, and simulation vertical slice around those foundations.

The proposed implementation has seven governing rules:

1. **`DEMO-001` first consolidates the already-working playable systems.** It publishes one immediate baseline with the robot, movement, aiming, shooting, boosting, camera, turret, props, destruction, and restart before the larger economy depends on it.
2. **Stage 1 always has one serialized owner at a time.** `DEMO-001` owns the immediate consolidation; after it merges and releases those paths, `INT-001` becomes the sole final economy/environment integration owner.
3. **All randomness flows through one versioned deterministic generator.** Strongboxes, drops, shops, crafting randomness, tests, and the simulator use the same application service and seed derivation.
4. **Definitions provide defaults; placed instances expose explicit overrides.** A designer can guarantee a strongbox, suppress drops, change a variant, or override a collider without creating another script or one-off prefab.
5. **Rewards have explicit ownership and claim state.** Generation, pickup projection, claiming, and atomic application to wallets/inventories are separate, idempotent steps.
6. **Existing combat and lifecycle authorities stay intact.** Reward adapters subscribe to confirmed destruction or encounter-resolution facts; they do not add damage shortcuts or second health systems.
7. **The balancing simulator is a maintained product surface.** It receives its own editor assembly, report schema, regression tests, and documented workflow.

Current `main` is anchored at merge commit `37e3b4da6830df0813ac9ccd0388d0c2a9eb346c`, which merged PR #127 for turret tracking and wreck-collision options.  The committed handoff files are behind this repository state and still refer to PR #110 and commit `34d7901`; they should be treated as historical routing context until a coordinator reconciles them.

The user-reported robot integration remains local at commit `96d6ce9`. No corresponding remote branch was visible through the GitHub connector. It must therefore be audited locally and published through `DEMO-001`; no economy/package task should depend on an unpublished local commit.

## Decisions that materially affect balance

These must be approved or supplied as explicit tuning data rather than silently decided by an implementation agent:

* **BALANCE DECISION:** soft-unlock curve shape and the size of its early-access tail.
* **BALANCE DECISION:** obsolete-item decay and minimum late-game availability.
* **BALANCE DECISION:** quality growth, exceptional-roll probability, and tier-to-tier advantage.
* **BALANCE DECISION:** the actual values for all 11 initial strongbox tiers.
* **BALANCE DECISION:** scrap ranges, recipe costs, and expected boxes-to-craft.
* **BALANCE DECISION:** default crafting delay and whether delay variance is fixed or random.
* **BALANCE DECISION:** crafted-item augment limits and whether guarantees can be purchased with extra scrap.
* **BALANCE DECISION:** shop price curves, refresh frequency, and reroll cost.
* **BALANCE DECISION:** duplicate-item handling and salvage yield.
* **BALANCE DECISION:** whether reward claims survive death, quick restart, room reload, and mission restart.
* **BALANCE DECISION:** whether box scrap is based only on box tier or also on item quality and duplicate status.

---

# 2. Repository findings

## 2.1 Architectural foundations

The repository already enforces the intended hybrid architecture:

```text
Domain
  ↑
Contracts
  ↑
Application
  ↑
UnityAdapters / Content.Definitions
  ↑
Presentation
  ↑
Bootstrap / Tests
```

`Domain`, `Contracts`, and `Application` are Unity-free; content definitions may use Unity but point only inward; Bootstrap is the composition root.

The ownership document already requires:

* one active owner for every scene, prefab, ScriptableObject, and paired `.meta`;
* one owner for shared modules and central tables;
* isolated package subtrees;
* regeneration rather than hand-merging generated output;
* no concurrent scene edits.

This aligns well with the requested parallelization model.

## 2.2 Existing reusable contracts

The repository already contains:

| Foundation             | Finding                                                                                                                                            | Recommendation                                                                                                                    |
| ---------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------- | --------------------------------------------------------------------------------------------------------------------------------- |
| Stable identity        | `StableId` is immutable, Unity-free, deterministic, and intended for content, runs, events, transactions, and generated entries.                   | Reuse it for definitions, instances, reward operations, transactions, equipment, boxes, recipes, shops, and seeds.                |
| Content references     | Typed content references and generated registry formats exist through CS-009/CS-010/CS-011.                                                        | Add new content kinds or compatible descriptors through the existing registry lane.                                               |
| Combat boundary        | Confirmed hit messages, stable event identities, duplicate protection, and physical-contact semantics already exist.                               | Drop sources consume terminal destruction facts; they never infer damage from visual collisions.                                  |
| Room projection        | Durable room and mission facts are separate from loaded Unity projections. Cross-room communication uses IDs rather than direct object references. | Doors and room transitions should use typed conditions and room sockets.                                                          |
| Encounter lifecycle    | Generic participant entries avoid enemy-subtype switches; durable completion requires a matching mission event.                                    | Boss and ordinary enemy rewards should use the same source-resolution adapter.                                                    |
| Deterministic evidence | The repository already has canonical deterministic evidence configuration and seed fixtures.                                                       | Reuse the identity/fingerprint conventions, but add a dedicated runtime-quality PRNG rather than overloading an evidence fixture. |

The existing architecture already states that one immutable reward commitment should be created for a collected strongbox and that run-seeded shop stock remains stable across revisits and reloads.

## 2.3 Enemy quality audit

The five Stage 1 enemy packages are real, independently owned packages rather than empty placeholders:

* Pursuer Drone uses EN-002 state and EN-003 adapters, with explicit target and contact ports.
* Ram Droid uses the same shared health/contact boundaries with deterministic disposable-impact behavior.
* Mobile Blaster Droid is package-isolated and consumes shared weapon/projectile infrastructure.
* Blaster Turret has bounded cadence, physical projectiles, tracking, and configurable destroyed collision.
* Four-Blaster Elite uses one accepted health authority and deterministic, telegraphed spread.

The shared enemy adapter uses explicit injected authority, decision, target, and contact dependencies and has a restart-generation boundary.

The target adapter correctly applies confirmed hit messages, checks target identity, rejects duplicate event IDs, and delegates health mutation to EN-002.

**Conclusion:** do not rewrite enemy health, target acquisition ports, contact rules, projectile contact, or restart. Normalize only the authoring and composition defects listed below.

## 2.4 Proven normalization requirements

### A. Turret scene lookup

`BlasterTurretAuthoring2D.ResolveContext()` falls back to `FindFirstObjectByType<BlasterTurretSceneContext2D>`.

This conflicts with the requested architecture because the object depends on a global scene lookup as its ordinary integration path.

**Fix:** resolve an explicit serialized scope or the nearest parent `GameplaySceneScope2D`. A temporary legacy fallback may remain for one migration release, but it must emit validation debt and must not be used by new prefabs.

### B. Turret identity generation

Turret instance identity is calculated from:

* scene path;
* hierarchy object names;
* sibling indices.

This violates the requirements for stable placed identity, duplicate detection, and independence from scene/object names.

**Fix:** use a serialized authored `StableId` on the placed object. Prefab duplication should copy the ID initially, and an editor validator should immediately flag the duplicate and offer a deliberate “Generate new instance ID” action.

### C. Package-specific global registration

`BlasterTurretSceneContext2D.Configure()` searches the entire loaded scene for authored turret components.

**Fix:** replace global discovery with generic registration against the nearest scope. The scene scope should know about interfaces such as `ISceneRuntimeParticipant`, not concrete turret classes.

### D. Stage 1 and name-dependent prop integration

`Stage1DestructiblePropIntegration`:

* scans specific presentation and collider roots;
* treats names beginning with `Crate_` and `Explosive_` as gameplay types;
* searches for colliders using `visual.name + "_Collision"`;
* derives IDs from names and sibling indices.

This is the clearest architectural violation in the audited code.

**Fix:** replace it with a reusable authored component that explicitly references its variant definition, collider, presentation root, stable instance ID, and drop override. The final scene owner migrates existing objects and removes the Stage 1 helper only after parity tests pass.

### E. Prop definitions are instance-only

`DestructiblePropAuthoring2D` currently exposes direct HP, collider size, offset, and animation fields but no reusable object definition, variant list, or inherited reward profile.

**Fix:** make it consume an `ObjectFamilyDefinition` plus selected variant, then layer explicit overrides over the inherited resolved configuration.

### F. Useful prop events should be retained

`DestructibleProp2D` already publishes once-only `Destroyed` and `Restarted` events after confirmed damage.

These events are a good integration seam. A reward-source adapter should subscribe to the destruction fact rather than modifying prop damage code.

## 2.5 Missing systems

No production implementation was located on current `main` for:

* money wallet;
* scrap wallet;
* typed player holdings/inventory authority;
* reward commitment, claim, and atomic application lifecycle;
* reward/drop profiles;
* strongbox inventory/opening;
* equipment instances;
* armor definitions;
* augment generation;
* augment upgrade transactions;
* soft-unlock curves;
* explicit progression-context provider;
* shared equipment generator;
* shops;
* crafting;
* balancing simulator;
* general doors;
* general void/fall hazards;
* generic placed-object identity and override authoring.

These should be implemented as new bounded systems.

## 2.6 Repository and branch state

* Current `main`: `37e3b4da6830df0813ac9ccd0388d0c2a9eb346c`.
* No open pull requests were returned by the GitHub connector during this audit.
* The repository’s committed handoff is stale and should be reconciled in a separate coordinator PR, not by ordinary implementation agents.
* The robot sprite integration is user-reported local-only and cannot be assumed to exist in remote agent environments.
* Unity is pinned to `6000.3.19f1`.

---

# 3. Recommended architecture

## 3.1 Domain boundaries

```text
ShooterMover.Domain
├── Common
│   ├── StableId
│   └── Random
├── Authoring
│   └── resolved object/variant values
├── Economy
│   ├── Money
│   ├── Scrap
│   └── Ledger
├── Holdings
│   ├── Strongboxes
│   ├── Equipment
│   └── Miscellaneous
├── Equipment
│   ├── definitions
│   ├── instances
│   └── augments
├── Progression
│   ├── soft eligibility
│   ├── quality availability
│   └── crafting availability
└── Rewards
    ├── drop profiles
    ├── reward grants
    ├── commitment/claim lifecycle
    ├── generation traces
    └── strongbox rules
```

Plain C# owns all probability, transaction, progression, quality, and crafting decisions.

## 3.2 Contract boundaries

```text
ShooterMover.Contracts
├── Authoring
├── Economy
├── Equipment
├── Progression
└── Rewards
```

Contracts expose immutable requests, results, rejection statuses, traces, snapshots, and service ports. They must not contain Unity assets or runtime scene references.

## 3.3 Application services

```text
ShooterMover.Application
├── Holdings
│   ├── PlayerHoldingsService
│   └── EquipmentInventoryService
├── Economy
│   ├── MoneyWalletService
│   └── ScrapWalletService
├── Rewards
│   ├── RewardGenerationService
│   ├── DropResolutionService
│   ├── RewardCommitmentService
│   ├── RewardClaimService
│   └── RewardApplicationService
│   └── StrongboxOpeningService
├── Equipment
│   ├── EquipmentGenerationService
│   └── AugmentUpgradeService
├── Shops
│   └── ShopInventoryService
├── Crafting
│   └── CraftingService
└── Simulation
    └── BalanceSimulationService
```

The simulator invokes these exact services. It may batch or aggregate results, but it cannot duplicate their generation logic.

## 3.4 Unity authoring boundaries

```text
ShooterMover.Content.Definitions
├── Objects
├── Rewards
├── Equipment
├── Augments
├── Strongboxes
├── Shops
└── Crafting

ShooterMover.UnityAdapters
├── Authoring
├── Rewards
├── Economy
├── Environment
└── Scenes

ShooterMover.Presentation
└── Rewards
```

ScriptableObjects are immutable authored configuration. Runtime wallet balances, box-open state, generated items, and source-claim state never live in shared assets.

## 3.5 Scene composition

Introduce a generic `GameplaySceneScope2D` on a room or integration root. It explicitly exposes:

* target registration;
* combat-hit registration;
* restart/session generation;
* reward-source resolution;
* pickup spawning;
* checkpoint lookup;
* room/encounter condition readers;
* optional wallet/application ports.

Placed objects bind through:

1. an explicit serialized scope override; or
2. the nearest parent scope.

They do not use global `Find*`, object names, scene names, or a Stage 1 controller.

## 3.6 Object and variant model

Use a small family/variant identity layer plus composable capability definitions. Do not create one universal asset containing every possible field.

```text
ObjectFamilyDefinition
- family_id
- display metadata
- default_variant_id
- ordered variants[]
- validation policy
```

Each variant selects only the capability modules that apply:

```text
ObjectVariantDefinition
- variant_id
- optional numeric object level
- presentation module
- collision module
- optional destructible module
- optional reward-source module
- optional movement module
- optional combat module
- optional lifecycle module
- optional environment-specific modules
```

Example composition:

```text
Level-2 reinforced crate
- PresentationDefinition
- CollisionDefinition
- DestructibleDefinition
- RewardSourceDefinition

Level-2 shooter turret
- PresentationDefinition
- CollisionDefinition
- DestructibleDefinition
- RewardSourceDefinition
- TurretCombatDefinition
- TrackingDefinition
- LifecycleDefinition
```

Crates never expose irrelevant firing fields, and doors never inherit irrelevant weapon fields. Shared modules remain reusable without becoming a giant universal Inspector.

Do not structurally limit the number of levels. `objectLevel` is useful for designer sorting and scaling, but the stable variant ID is authoritative.

## 3.7 Override model

Every authored instance resolves:

```text
resolved value =
    explicit instance override
    ?? selected variant value
    ?? family default
```

Inspector groups expose only relevant override toggles:

```text
[ ] Override health
[ ] Override collision
[ ] Override presentation
[ ] Override combat
[ ] Override rewards
[ ] Override lifecycle
```

Each enabled group shows:

* inherited value;
* overridden value;
* validation;
* “Reset to inherited”;
* a resolved preview.

Reward override modes are a single explicit enum, not a collection of interacting booleans.

## 3.8 Reward flow

```text
confirmed source resolution
        ↓
RewardGenerationContext + DropProfile + SourceOverride
        ↓
RewardGenerationService
        ↓
RewardGenerationResult + Trace
        ↓
RewardCommitment (Generated)
        ↓
optional pickup projection (Projected)
        ↓
claim admission (Claimed)
        ↓
atomic RewardApplicationService
        ↓
money wallet + scrap wallet + strongbox holdings
+ equipment inventory + miscellaneous holdings
        ↓
RewardCommitment (Applied)
```

The source adapter does not mutate wallets or inventories. It submits one idempotent reward operation. Every transition retains the same operation/grant IDs so restart, duplicate callbacks, pickup retries, and persistence retries cannot duplicate or lose rewards.

## 3.9 Strongbox flow

```text
owned strongbox instance
        ↓ OpenStrongboxCommand
StrongboxOpeningService
        ↓
validate unique box + opening transaction
        ↓
shared equipment generator
        ↓
equipment + side rewards + mandatory scrap
        ↓
immutable RewardCommitment
        ↓
atomic application / later persistence port
```

**Hard invariant:** every successful strongbox opening contains a positive scrap grant.

## 3.10 Restart and persistence assumptions

Recommended semantics:

* scene quick restart resets health, enemies, props, doors, hazards, projectiles, and transient pickup presentation;
* money, scrap, owned boxes, generated equipment, miscellaneous holdings, and applied reward operation IDs do **not** reset;
* a source already resolved in the current run cannot generate another reward merely because its GameObject was restarted;
* a generated but unclaimed pickup is re-projected after quick restart, or is atomically applied before restart according to the chosen pickup policy;
* a claimed reward can be safely retried until it reaches `Applied`;
* a full new-run command creates a new run-scoped reward ledger.

**BALANCE/PERSISTENCE DECISION:** confirm whether mission restart is allowed to reset unbanked rewards. Quick restart should not, because otherwise it creates trivial farming.

---

# 4. Shared contract proposal

## 4.1 Deterministic random contract

```csharp
public readonly struct GenerationSeed
{
    public ulong Value { get; }
    public int AlgorithmVersion { get; }
}

public interface IDeterministicRandomStream
{
    ulong NextUInt64();
    double NextUnitDouble();
    int NextInt(int inclusiveMinimum, int exclusiveMaximum);
    IDeterministicRandomStream Fork(StableId purposeId, long ordinal);
}
```

Requirements:

* frozen, documented integer algorithm;
* identical outputs across supported runtimes;
* named substreams for item eligibility, item selection, slots, augments, quality, scrap, and side rewards;
* adding a cosmetic roll must not shift equipment results;
* no `UnityEngine.Random`;
* no ambient `System.Random`;
* algorithm version included in result traces and fingerprints.

## 4.2 Reward generation context

```text
RewardGenerationContext
- operation_id
- run_id
- source_instance_id
- source_definition_id
- source_type
- character_level
- source_level
- region_level
- difficulty
- optional strongbox_tier_id
- tags
- generation_seed
- definition_fingerprint
```

## 4.3 Reward result

```text
RewardGenerationResult
- operation_id
- money_grants[]
- scrap_grants[]
- strongbox_grants[]
- equipment_instances[]
- miscellaneous_grants[]
- trace
```

The result is immutable and can be applied exactly once.

## 4.4 Explainable trace

```text
RewardGenerationTrace
- algorithm version
- root seed
- named substream seeds
- applied inherited profile
- applied override mode
- candidate eligibility records
- curve weights
- selected candidate/index
- quality availability range
- augment slot roll
- augment compatibility exclusions
- augment tier/level rolls
- guarantees applied
- quantity rolls
- final result fingerprint
```

Large bulk simulations may retain aggregate traces and only preserve complete traces for selected/outlier seeds.

## 4.5 Drop profile

```text
DropProfile
- profile_id
- guaranteed_entries[]
- independent_rolls[]
- exclusive_groups[]
- scaling_policy
- conditions/tags
```

An entry can yield:

* money;
* scrap;
* strongbox;
* equipment, when explicitly permitted;
* miscellaneous reward;
* future typed reward category.

Avoid a global switch statement by using a typed reward-specification registry or closed v1 reward specification hierarchy with versioned extension points.

## 4.6 Per-instance reward override

```text
RewardOverrideMode
- Inherit
- ForceNone
- ReplaceProfile
- AppendGuaranteed
- ForceMoney
- ForceStrongboxExactTier
- ForceStrongboxTierRange
- ForceMiscellaneous
```

The authoring object stores only data needed for its selected mode.

## 4.7 Wallet contracts

Money and scrap use one internal typed idempotent-ledger primitive while retaining separate public authorities and snapshots.

```text
WalletTransactionCommand
- transaction_id
- wallet_id
- currency_id
- signed_amount
- reason_id
- source_operation_id
- expected_sequence (optional)
```

```text
WalletTransactionResult
- Applied
- DuplicateNoChange
- InsufficientFunds
- InvalidAmount
- WrongCurrency
- SequenceConflict
```

Money and scrap have separate authorities and snapshots. They may reuse an internal idempotent-ledger primitive, but one wallet must never accept the other currency ID.

## 4.8 Player holdings and inventory

```text
PlayerHoldingsSnapshot
- owned_strongboxes[]
- equipment_instances[]
- miscellaneous_stacks[]
- sequence
- applied_operation_ids/fingerprint
```

```text
HoldingsMutationCommand
- transaction_id
- source_operation_id
- typed additions[]
- typed removals[]
- expected_sequence (optional)
```

Strongboxes, equipment, armor, premium ammunition, and future miscellaneous items require an explicit authority. Shops, crafting, box opening, and pickups must not maintain private ownership lists.

## 4.9 Reward commitment and claim lifecycle

```text
RewardCommitment
- commitment_id
- source_operation_id
- grants[]
- state
- generation_fingerprint
```

```text
RewardCommitmentState
- Generated
- Projected
- Claimed
- Applied
- Cancelled
```

Rules:

* transitions are monotonic and idempotent;
* `Claimed` may be retried until `Applied`;
* atomic application either updates every targeted authority or none;
* duplicate pickup callbacks return `DuplicateNoChange`;
* restart policy explicitly determines whether `Generated`/`Projected` commitments are re-projected, banked, or cancelled;
* reward truth never lives only in a pickup GameObject.

## 4.10 Equipment definition

```text
EquipmentDefinition
- equipment_id
- category
- family_id
- display metadata
- natural_discovery_level
- base stats
- eligible augment tags
- incompatible augment tags
- maximum configured slots
- strongbox eligibility
- shop eligibility
- crafting recipe reference
- presentation references
- content tags
```

Weapon definitions may reference existing weapon-package identities rather than duplicating weapon behavior.

## 4.11 Equipment instance

```text
EquipmentInstance
- instance_id
- definition_id
- generated_at_character_level
- source_type/source_id
- quality_score
- augment_slots
- augments[]
- generation_seed/fingerprint
- provenance
```

## 4.12 Augment definition and instance

```text
AugmentDefinition
- augment_id
- eligible categories/families/tags
- exclusion tags
- tier configuration
- maximum level
- scaling curve
- upgrade-cost curve
- presentation metadata
```

```text
AugmentInstance
- augment_id
- tier
- level
- resolved effect values
```

The initial three tiers and ten levels are catalog defaults, not enum or array hard caps.

## 4.13 Augment upgrade contract

```text
UpgradeAugmentCommand
- transaction_id
- equipment_instance_id
- augment_slot_id
- requested_target_level
- quoted_money_cost
- expected_inventory_sequence
- expected_wallet_sequence
```

```text
UpgradeAugmentResult
- Applied
- DuplicateNoChange
- EquipmentNotOwned
- AugmentNotFound
- MaximumLevelReached
- InvalidTargetLevel
- PriceChanged
- InsufficientFunds
- SequenceConflict
```

The upgrade service computes cost from the augment definition and current level, spends money once, and replaces the immutable equipment instance atomically.

## 4.14 Progression context and curves

Gameplay and simulation consume the same explicit progression context:

```text
ProgressionContext
- character_level
- region_level
- difficulty
- optional progression_tags
```

```text
IProgressionContextProvider.Read()
```

The simulator supplies a direct context. Gameplay may initially use an authored/session provider until a future XP system becomes authoritative.

Use normalized curve interfaces:

```text
IEligibilityCurve.Evaluate(characterLevel, naturalLevel, context)
IQualityAvailabilityCurve.Evaluate(characterLevel, naturalLevel, sourceBias)
ICraftingAvailabilityPolicy.Evaluate(characterLevel, recipe)
```

The natural eligibility model should support:

* configurable non-zero early tail;
* activation/peak region;
* continued availability;
* configurable old-item decay;
* minimum retention floor.

**BALANCE DECISION:** exact mathematical curve and parameters.

## 4.15 Strongbox definition

```text
StrongboxDefinition
- tier_id
- display order
- discovery reach bias
- early-access multiplier
- reward count distribution
- category weights
- slot distribution
- augment-tier distribution
- augment-level budget
- exceptional multiplier
- minimum protection
- scrap distribution
- side-reward profile
- presentation reference
```

There is no `switch (tier)` and no array sized to 11.

## 4.16 Shop contract

```text
GenerateShopInventoryCommand
- shop_id
- run_id
- refresh_ordinal
- character/region/shop levels
- inventory size
- seed
- definition fingerprint
```

The same key produces the same inventory. Revisiting a shop does not reroll it.

## 4.17 Crafting contract

```text
CraftingRecipe
- recipe_id
- target equipment_id
- natural discovery level source
- crafting delay
- optional delay variance
- base scrap cost
- cost scaling
- crafted quality policy
```

Hard invariant:

```text
minimum crafting unlock >
ordinary natural discovery activation
```

This must be validated in data and tested.

## 4.18 Door contract

```text
DoorDefinition
- door_id
- opening mode
- condition expression
- one-way policy
- collider states
- sprite/animation hooks
- room transition reference
- restart policy
```

Condition expressions support `All`, `Any`, and typed leaves:

* Always;
* TriggerEntered;
* InteractionRequested;
* EncounterResolved;
* TargetDestroyed;
* CurrencyAvailable;
* KeyOwned;
* DirectionAllowed;
* RoomTransitionAuthorized.

## 4.19 Void/fall hazard contract

```text
VoidHazardDefinition
- player response
- enemy response
- projectile response
- prop response
- damage or instant-destroy policy
- respawn/checkpoint reference
- animation/audio hooks
- restart policy
```

Responses are explicit per category:

* Ignore;
* Damage;
* Destroy;
* Respawn;
* RemoveProjectile;
* KeepSupported.

---

# 5. Dependency graph

```text
Current main + local robot commit
        │
        └── DEMO-001
            immediate playable baseline
            (sole Stage 1 owner until merge)

ADR-001 ─┬─ AUD-001
         ├─ OBJ-001
         ├─ REW-001
         ├─ EQP-001
         ├─ RNG-001
         ├─ LED-001
         └─ PRG-001

LED-001 + REW-001 ─┬─ MON-001
                   └─ SCR-001

REW-001 + EQP-001 + RNG-001 + PRG-001 ── GEN-001
REW-001 + LED-001 + EQP-001 ───────────── INV-001
REW-001 + INV-001 + MON-001 + SCR-001 ── RAP-001

GEN-001 + SCR-001 + INV-001 + RAP-001 ── BOX-001
EQP-001 + RNG-001 + SCR-001 + INV-001
        + RAP-001 ─────────────────────── CRA-001
EQP-001 + MON-001 + INV-001 + RAP-001 ── AUG-001
GEN-001 + MON-001 + INV-001 + RAP-001 ── SHOP-001

OBJ-001 + REW-001 ─── SRC-001
OBJ-001 + SRC-001 ─── PROP-001
AUD-001 + OBJ-001 ─── NORM-001
ADR-001 + OBJ-001 ─┬─ DOOR-001
                   └─ VOID-001

GEN/BOX/SHOP/CRA/AUG/SRC/RAP ── SIM-001 ── STAT-001 ── BAL-001
RAP-001 + source packages ───── PICK-001

All accepted package work + BAL-001 + DEMO-001 baseline
        │
        └── INT-001
            final Stage 1 owner after DEMO-001 releases paths
```

Specific required ordering:

* equipment definitions before shared generation;
* soft-unlock/quality mathematics before generation;
* explicit progression context before gameplay or simulation supplies character level;
* shared ledger before money, scrap, holdings, shops, crafting, or upgrades;
* holdings and atomic reward application before boxes, shops, crafting, or pickups can grant durable ownership;
* generation before strongboxes and shops;
* strongbox opening before complete scrap-accumulation simulation;
* equipment definitions before crafting;
* equipment definitions, money, holdings, and reward application before augment upgrades;
* reward contracts before source authoring;
* object lifecycle assumptions before doors and void hazards;
* immediate playable consolidation before broad economy work obscures the current demo;
* all package work before the final single scene integration owner.

---

# 6. Parallel wave plan

## Wave 0 — architecture lock and read-only audit

Can begin immediately from `37e3b4da...`:

| Task     | Parallel? | Purpose                                                                                        |
| -------- | --------: | ---------------------------------------------------------------------------------------------- |
| ADR-001  |       Yes | Freeze ownership, lifecycle, identity, restart, reward-claim, and simulator-sharing decisions. |
| AUD-001  |       Yes | Read-only audit of enemies, props, scene glue, restart, identity, damage, and test quality.    |
| DEMO-001 |       Yes | Publish the existing complete playable baseline and robot while architecture work proceeds.    |

No broad implementation begins until ADR-001 is approved.

## Wave 1 — independent foundations

After ADR-001:

| Task    | Dependencies |
| ------- | ------------ |
| OBJ-001 | ADR-001      |
| REW-001 | ADR-001      |
| EQP-001 | ADR-001      |
| RNG-001 | ADR-001      |
| LED-001 | ADR-001      |
| PRG-001 | ADR-001      |

These use non-overlapping Domain, Contracts, Application, Definition, and test folders.

## Wave 2 — authorities, shared runtime, and independent environment packages

| Task     | Dependencies                       |
| -------- | ---------------------------------- |
| MON-001  | REW-001, LED-001                   |
| SCR-001  | REW-001, LED-001                   |
| INV-001  | REW-001, EQP-001, LED-001          |
| GEN-001  | REW-001, EQP-001, RNG-001, PRG-001 |
| SRC-001  | REW-001, OBJ-001                   |
| DOOR-001 | ADR-001, OBJ-001                   |
| VOID-001 | ADR-001, OBJ-001                   |
| NORM-001 | AUD-001, OBJ-001                   |

Doors and void hazards can proceed independently once ADR-001 documents:

* scene-scope acquisition;
* restart registration;
* stable placed identity;
* player/enemy/projectile/prop classification;
* checkpoint references;
* durable versus session-local state.

## Wave 3 — reward application and product systems

| Task     | Dependencies                                                 |
| -------- | ------------------------------------------------------------ |
| RAP-001  | REW-001, INV-001, MON-001, SCR-001                           |
| BOX-001  | GEN-001, SCR-001, INV-001, RAP-001                           |
| CRA-001  | EQP-001, RNG-001, SCR-001, INV-001, RAP-001                  |
| AUG-001  | EQP-001, MON-001, INV-001, RAP-001                           |
| SHOP-001 | GEN-001, MON-001, INV-001, RAP-001                           |
| PROP-001 | OBJ-001, SRC-001                                              |
| PICK-001 | RAP-001, SRC-001, BOX-001                                     |
| SIM-001  | GEN-001 initially; BOX/SHOP/CRA/AUG/SRC/RAP for complete modes |

`SIM-001` should merge as soon as it can simulate generator fixtures, then receive bounded follow-up extensions for boxes, shops, drops, and crafting. Do not postpone it until the end.

## Wave 4 — tools, balance data, and serial integration

| Task     | Dependencies                                                                    |
| -------- | ------------------------------------------------------------------------------- |
| VAL-001  | OBJ-001, SRC-001, DOOR-001, VOID-001, SIM-001                              |
| STAT-001 | GEN-001, BOX-001, SHOP-001, CRA-001, AUG-001, RAP-001                      |
| BAL-001  | SIM-001, STAT-001, explicit human balance decisions                        |
| INT-001  | DEMO-001 and all selected packages, NORM-001, PROP-001, VAL-001, BAL-001   |

Only one task edits the Stage 1 scene at a time: `DEMO-001` first, then `INT-001` after the demo branch is merged and ownership is explicitly released.

---

# 7. Task inventory

| ID       | Task                                                 | Exact main ownership family                                                                            |           Scene edit | Dispatch status          |
| -------- | ---------------------------------------------------- | ------------------------------------------------------------------------------------------------------ | -------------------: | ------------------------ |
| ADR-001  | Architecture and lifecycle ADR                       | `docs/architecture/rewards/**`, `docs/architecture/authoring/**`                                       |                   No | Ready                    |
| AUD-001  | Existing-system audit                                | `docs/audits/reward-object-readiness/**`                                                               |                   No | Ready                    |
| DEMO-001 | Immediate complete playable baseline                 | Stage 1 scene/controller/tests plus exact robot asset paths                                             | **Yes — first owner** | Ready                    |
| OBJ-001  | Placed identity, capabilities, variants, overrides    | `Runtime/{Domain,Contracts,UnityAdapters}/Authoring/**`, `Content/Definitions/Objects/**`              |                   No | After ADR                |
| REW-001  | Reward/economy contracts                             | `Runtime/Domain/Rewards/Model/**`, `Runtime/Contracts/{Rewards,Economy}/**`                            |                   No | After ADR                |
| EQP-001  | Equipment and augment definitions                    | `Runtime/{Domain,Contracts}/Equipment/**`, `Content/Definitions/Equipment/**`                          |                   No | After ADR                |
| RNG-001  | Deterministic random and progression curves          | `Runtime/Domain/Common/Random/**`, `Runtime/Domain/Progression/**`, `Runtime/Contracts/Progression/**` |                   No | After ADR                |
| LED-001  | Typed idempotent ledger primitive                    | `Runtime/Domain/Economy/Ledger/**`, focused tests                                                       |                   No | After ADR                |
| PRG-001  | Progression context provider contract                | `Runtime/{Domain,Contracts,Application}/Progression/Context/**`                                        |                   No | After ADR                |
| MON-001  | Money wallet authority                               | `Runtime/Domain/Economy/Money/**`, `Runtime/Application/Economy/Money/**`                              |                   No | After REW + LED          |
| SCR-001  | Scrap wallet authority                               | `Runtime/Domain/Economy/Scrap/**`, `Runtime/Application/Economy/Scrap/**`                              |                   No | After REW + LED          |
| INV-001  | Strongbox/equipment/misc holdings                    | `Runtime/{Domain,Contracts,Application}/Holdings/**`                                                    |                   No | Wave 2                   |
| RAP-001  | Reward commitment, claim, atomic application         | `Runtime/{Domain,Contracts,Application}/Rewards/Application/**`                                        |                   No | Wave 3                   |
| GEN-001  | Shared reward/equipment generator                    | `Runtime/{Domain,Application}/Rewards/Generation/**`                                                   |                   No | Wave 2                   |
| SRC-001  | Drop profiles and placed source overrides            | `Content/Definitions/Rewards/**`, `Runtime/UnityAdapters/Rewards/Sources/**`                           |                   No | Wave 2                   |
| BOX-001  | Strongbox opening runtime                            | `Runtime/{Domain,Application}/Rewards/Strongboxes/**`, strongbox schemas                               |                   No | Wave 3                   |
| CRA-001  | Crafting runtime                                     | `Runtime/{Domain,Application}/Crafting/**`, `Content/Definitions/Crafting/**`                          |                   No | Wave 3                   |
| AUG-001  | Money-funded augment upgrading                       | `Runtime/{Domain,Application}/Equipment/Upgrades/**`, focused tests                                    |                   No | Wave 3                   |
| DOOR-001 | Reusable doors                                       | `ContentPackages/Environment/Doors/**`                                                                 |                   No | Wave 2                   |
| VOID-001 | Void/fall hazards                                    | `ContentPackages/Environment/VoidHazards/**`                                                           |                   No | Wave 2                   |
| PROP-001 | Destructible-prop migration                          | Existing `ContentPackages/Props/DestructibleProps/**` and focused tests                                |                   No | Wave 3                   |
| NORM-001 | Enemy registration and turret identity normalization | Exact Blaster Turret authoring/context/prefab/test files                                               |                   No | Conditional after audit  |
| SHOP-001 | Shop inventory, prices, purchases                    | `Runtime/{Domain,Application}/Shops/**`, `Content/Definitions/Shops/**`                                |                   No | Wave 3                   |
| SIM-001  | First-class balancing simulator                      | `Assets/ShooterMover/Editor/BalanceSimulator/**`, editor assembly, simulator tests/docs                |                   No | Wave 3                   |
| PICK-001 | Reward pickup presentation                           | `UnityAdapters/Rewards/Pickups/**`, `Presentation/Rewards/**`, pickup package                          |                   No | Wave 3                   |
| VAL-001  | Designer inspectors and validators                   | `Assets/ShooterMover/Editor/Authoring/**`                                                              |                   No | Wave 3                   |
| STAT-001 | Statistical and deterministic verification           | Focused reward/economy simulation tests                                                                |                   No | Wave 3                   |
| BAL-001  | Initial catalogs and balance data                    | Exact catalog assets under rewards/equipment/strongboxes/shops/crafting                                |                   No | Human decisions required |
| INT-001  | Final reward/environment integration                 | Stage 1 scene, controller, integration tests                                                            | **Yes — final owner** | Last                     |

---

# 8. Draft agent prompts

## Prompt-base rule

Packets that can start immediately use:

```text
Repository: YeerooXY/shooter-mover
Base commit: 37e3b4da6830df0813ac9ccd0388d0c2a9eb346c
PR base: main
```

A future dependent task cannot truthfully name a commit that does not yet exist. Before dispatch, the orchestrator must replace `BASE_AFTER_DEPENDENCIES` with the exact current `main` SHA containing all named dependency merges. Agents must verify they are zero commits behind that base before editing.

Every packet inherits these restrictions:

```text
- Fresh branch and worktree.
- Do not edit assembly/generated/** or lifecycle handoff files.
- Do not edit ProjectSettings/** or Packages/**.
- Do not edit a scene unless the packet explicitly grants scene ownership.
- Do not add shared ancestor .meta files.
- Commit only owned paths and inseparable metadata.
- Keep the PR draft until required Unity proof is complete.
- PR body: task ID, base/dependencies, exact paths, tests, manual proof,
  limitations, rollback.
```

## ADR-001 — Reward/object authoring architecture lock

```text
Branch: agent/adr-001-reward-object-architecture
Base: 37e3b4da6830df0813ac9ccd0388d0c2a9eb346c

Objective:
Document and freeze the shared contracts, ownership, scene-scope, identity,
restart, reward-claim, and simulator/runtime-sharing assumptions required by
the reward, object, door, hazard, shop, and crafting work.

Owned paths:
- docs/architecture/rewards/REWARD_EQUIPMENT_ARCHITECTURE_V1.md
- docs/architecture/authoring/PLACED_OBJECT_LIFECYCLE_V1.md
- docs/architecture/authoring/STAGE1_INTEGRATION_OWNERSHIP.md
- inseparable leaf metadata only if Unity imports these docs

Forbidden:
- Assets/** runtime, prefabs, scenes, ScriptableObjects
- assembly/generated/**
- ProjectSettings/**
- Packages/**

Required decisions:
- DEMO-001 owns Stage1VisibleSlice only until its merge; ownership is then
  explicitly released and INT-001 becomes the sole final integration owner.
- explicit/parent scene scope; no global Find* primary path.
- authored placed StableId and duplicate validation.
- quick restart does not duplicate durable reward grants.
- reward commitment Generated/Projected/Claimed/Applied lifecycle.
- holdings authority for boxes, equipment, and miscellaneous items.
- one shared typed ledger primitive for money, scrap, and holdings semantics.
- explicit progression context provider.
- deterministic random algorithm version and named substreams.
- simulator invokes exact application services.
- doors and hazards consume typed lifecycle ports.

Tests:
- repository layout validation
- architecture/ownership consistency review

Acceptance:
All dependent packets can name one contract and one owner for every shared
concept; no unresolved overlapping serialized paths remain.

Non-goals:
No implementation and no balance values.
```

## AUD-001 — Existing enemy and scene-readiness audit

```text
Branch: agent/aud-001-enemy-scene-reward-readiness
Base: 37e3b4da6830df0813ac9ccd0388d0c2a9eb346c

Objective:
Perform a read-only audit of every existing enemy package, destructible prop,
identity source, scene context, restart path, health/damage boundary, physical
projectile path, and relevant tests.

Owned paths:
- docs/audits/reward-object-readiness/ENEMY_AND_SCENE_AUDIT.md

Read-only inputs:
- Runtime/Domain/Enemies/**
- Runtime/UnityAdapters/{Enemies,Combat,Physics}/**
- ContentPackages/Enemies/**
- ContentPackages/Props/**
- Stage1VisibleSlice scene/controller/tests
- related PR history and package docs

Required report:
For each package, record hardcoded scene/name dependencies, target acquisition,
identity behavior, duplicate placement, restart, destruction, reward readiness,
definition separation, test quality, and exact evidence paths.

Acceptance:
Every proposed normalization is backed by a concrete file/line or test gap.
Working systems are explicitly marked “retain.”

Non-goals:
No code, prefab, scene, asset, or test changes.
```

## DEMO-001 — Immediate complete playable Stage 1 baseline

```text
Branch: agent/demo-001-complete-playable-baseline
Base: current main plus an audited/reapplied robot commit 96d6ce9

Objective:
Publish one immediately playable Stage 1 containing the systems that already
exist: robot visual, movement, aiming, visible shooting, projectile rotation,
boosting, boost trail, stable camera, turret tracking/shooting/destruction,
destructible props, collisions, destruction animation hooks, and restart.

Exclusive owned paths while active:
- Assets/ShooterMover/Scenes/Prototypes/Stage1VisibleSlice.unity
- Assets/ShooterMover/TestSupport/VisibleSlice/Stage1VisibleSliceController.cs
- Assets/ShooterMover/Tests/PlayMode/VisibleSliceIntegration/**
- exact robot asset and metadata paths from commit 96d6ce9

Required behavior:
- no regression to current movement, shooting, boost, camera, turret, or props
- robot sprite visible above map tiles and below projectiles/HUD
- one coherent playtest scene, not another disconnected project
- clean fallback behavior when optional art is missing

Tests:
cold compile; focused Stage 1 integration suite; movement/shooting/boost smoke;
turret tracking and physical-hit timing; prop destruction; fifty restarts;
manual keyboard/mouse playtest.

Handoff:
After merge, release Stage 1 ownership. INT-001 must start from the merge commit
containing DEMO-001 and becomes the next sole owner.

Non-goals:
No reward economy, doors, void hazards, shops, strongboxes, or broad refactors.
```

## OBJ-001 — Placed identity, capabilities, variants, and overrides

```text
Branch: agent/obj-001-placed-object-variants
Base: BASE_AFTER_DEPENDENCIES
Dependencies: ADR-001

Objective:
Provide generic stable placed identity, unbounded object variants, composable
capability definitions, resolved inheritance, explicit instance overrides,
scene-scope registration ports, and restart-participant contracts.

Owned paths:
- Assets/ShooterMover/Runtime/Domain/Authoring/**
- Assets/ShooterMover/Runtime/Contracts/Authoring/**
- Assets/ShooterMover/Runtime/UnityAdapters/Authoring/**
- Assets/ShooterMover/Content/Definitions/Objects/**
- Assets/ShooterMover/Tests/EditMode/Authoring/**
- Assets/ShooterMover/Tests/PlayMode/Authoring/**
- docs/architecture/contracts/OBJECT_AUTHORING_V1.md

Required behavior:
- serialized authored instance StableId
- generated runtime identity only through explicit spawn input
- duplicate detection contract
- family definition with arbitrary variant count
- variants select only relevant capability modules
- no universal definition exposing combat fields on crates or weapon fields on doors
- explicit capability-specific override groups with inherited/resolved values
- nearest-parent or explicit scene-scope binding
- no global search or object-name behavior

Tests:
identity stability after rename/reparent, duplicate rejection, override reset,
variant resolution, restart registration, arbitrary hierarchy placement.

Non-goals:
No existing enemy/prop migration, custom inspectors, scenes, rewards, balance,
or giant all-purpose ScriptableObject.
Prefabs: no. Project settings/manifests: no.
```

## REW-001 — Reward and economy contracts v1

```text
Branch: agent/rew-001-reward-economy-contracts
Base: BASE_AFTER_DEPENDENCIES
Dependencies: ADR-001

Objective:
Define immutable reward results, drop profiles, source overrides, transaction
commands/results, strongbox opening envelopes, and explainable traces.

Owned paths:
- Assets/ShooterMover/Runtime/Domain/Rewards/Model/**
- Assets/ShooterMover/Runtime/Contracts/Rewards/**
- Assets/ShooterMover/Runtime/Contracts/Economy/**
- Assets/ShooterMover/Tests/EditMode/Rewards/Contracts/**
- docs/architecture/contracts/REWARDS_V1.md
- docs/architecture/contracts/ECONOMY_TRANSACTIONS_V1.md

Required behavior:
guaranteed entries, independent rolls, exclusive weighted groups, typed reward
specifications, quantities, scaling inputs, explicit override modes, immutable
operation IDs, source IDs, and duplicate-safe transaction vocabulary.

Acceptance:
Money-only, strongbox-only, misc-only, mixed, no-drop, append-guaranteed, and
replace-entirely can all be represented without package-specific switches.

Non-goals:
No random implementation, wallets, Unity assets, pickups, or balance.
```

## EQP-001 — Equipment and augment definitions v1

```text
Branch: agent/eqp-001-equipment-augment-definitions
Base: BASE_AFTER_DEPENDENCIES
Dependencies: ADR-001

Objective:
Define shared weapon/armor equipment metadata, generated equipment instances,
augment definitions/instances, compatibility, exclusions, slots, tiers, levels,
and validation.

Owned paths:
- Assets/ShooterMover/Runtime/Domain/Equipment/**
- Assets/ShooterMover/Runtime/Contracts/Equipment/**
- Assets/ShooterMover/Content/Definitions/Equipment/**
- Assets/ShooterMover/Tests/EditMode/Equipment/**
- docs/architecture/contracts/EQUIPMENT_AUGMENTS_V1.md

Required behavior:
no hardcoded three-tier/ten-level cap; zero-slot items; category/family/tag
compatibility; duplicate and exclusion rules; existing weapon IDs referenced
without duplicating weapon runtime behavior.

Tests:
impossible combinations, variable configured maxima, deterministic canonical
ordering/fingerprints, armor compatibility, existing five weapon IDs.

Non-goals:
No random generation, shop, box, crafting, inventory, scene, or final content.
ScriptableObject schemas allowed; production balance assets not allowed.
```

## RNG-001 — Deterministic randomness and soft progression

```text
Branch: agent/rng-001-deterministic-progression-curves
Base: BASE_AFTER_DEPENDENCIES
Dependencies: ADR-001

Objective:
Implement one versioned deterministic PRNG, named substreams, soft item
eligibility, quality availability, and crafting-unlock mathematics.

Owned paths:
- Assets/ShooterMover/Runtime/Domain/Common/Random/**
- Assets/ShooterMover/Runtime/Domain/Progression/**
- Assets/ShooterMover/Runtime/Contracts/Progression/**
- Assets/ShooterMover/Tests/EditMode/Progression/**
- docs/architecture/contracts/DETERMINISTIC_RANDOM_V1.md
- docs/architecture/contracts/PROGRESSION_CURVES_V1.md

Required behavior:
same seed/context/version produces identical samples; substreams isolate roll
families; early tail, activation region, continued availability, old-item decay,
retention floor, quality growth, source bias, and configurable crafting delay.

Tests:
frozen vectors, fork stability, zero/invalid parameter rejection, no hard level
gate, configured early-tail behavior, crafting unlock later than natural
activation.

Non-goals:
No balance parameter choices, Unity curves, generator, boxes, or shops.
```

## LED-001 — Typed idempotent ledger primitive

```text
Branch: agent/led-001-idempotent-ledger
Base: BASE_AFTER_DEPENDENCIES
Dependencies: ADR-001

Objective:
Implement one engine-independent typed ledger primitive for exact-once additions,
spends, sequence checks, snapshots, and import/export. Money, scrap, and holdings
reuse it without sharing public authorities or accepting each other's entry types.

Owned paths:
- Assets/ShooterMover/Runtime/Domain/Economy/Ledger/**
- Assets/ShooterMover/Tests/EditMode/Economy/Ledger/**
- docs/architecture/contracts/IDEMPOTENT_LEDGER_V1.md

Required behavior:
transaction-ID duplicate no-change; bounded debit policy; optional expected
sequence; immutable snapshots; deterministic canonical ordering/fingerprint;
validated snapshot import.

Tests:
duplicate credit/debit, rejected debit, sequence conflict, snapshot round trip,
corrupt import rejection, large ledger behavior.

Non-goals:
No money/scrap semantics, Unity adapters, UI, persistence backend, or scenes.
```

## PRG-001 — Progression context provider

```text
Branch: agent/prg-001-progression-context
Base: BASE_AFTER_DEPENDENCIES
Dependencies: ADR-001

Objective:
Define the explicit character/region/difficulty progression context consumed by
generation, shops, drops, crafting, upgrades, gameplay, and simulation.

Owned paths:
- Assets/ShooterMover/Runtime/Domain/Progression/Context/**
- Assets/ShooterMover/Runtime/Contracts/Progression/Context/**
- Assets/ShooterMover/Runtime/Application/Progression/Context/**
- Assets/ShooterMover/Tests/EditMode/Progression/Context/**

Required behavior:
immutable context; validation; authored/session provider suitable before XP exists;
direct simulator provider; no global player lookup.

Non-goals:
No XP gain, leveling UI, save backend, balance curves, or scenes.
```

## MON-001 — Money wallet

```text
Branch: agent/mon-001-money-wallet
Base: BASE_AFTER_DEPENDENCIES
Dependencies: REW-001, LED-001

Objective:
Implement an engine-independent money authority with add, spend, rejection,
sequence snapshots, and idempotent transaction handling by composing LED-001.

Owned paths:
- Assets/ShooterMover/Runtime/Domain/Economy/Money/**
- Assets/ShooterMover/Runtime/Application/Economy/Money/**
- Assets/ShooterMover/Tests/EditMode/Economy/Money/**

Required behavior:
positive grants, bounded spends, insufficient-funds rejection, duplicate
transaction no-change, deterministic snapshots, UI-ready immutable change
events, persistence-ready import/export contract.

Tests:
duplicate add/spend, insufficient funds, wrong currency, invalid amounts,
sequence conflict, snapshot round trip.

Non-goals:
No UI, pickups, shops, persistence files, scrap, or scenes.
```

## SCR-001 — Scrap wallet

```text
Branch: agent/scr-001-scrap-wallet
Base: BASE_AFTER_DEPENDENCIES
Dependencies: REW-001, LED-001

Objective:
Implement a separate scrap authority with idempotent grants/spends and
salvage-ready reason/provenance fields by composing LED-001.

Owned paths:
- Assets/ShooterMover/Runtime/Domain/Economy/Scrap/**
- Assets/ShooterMover/Runtime/Application/Economy/Scrap/**
- Assets/ShooterMover/Tests/EditMode/Economy/Scrap/**

Required behavior:
same transaction semantics as money while rejecting money currency IDs;
strongbox-opening source reason; future salvage reason; immutable snapshots.

Non-goals:
No salvage calculation, crafting, strongbox generation, UI, or scenes.
```

## INV-001 — Player holdings and equipment inventory

```text
Branch: agent/inv-001-player-holdings
Base: BASE_AFTER_DEPENDENCIES
Dependencies: REW-001, EQP-001, LED-001

Objective:
Implement the durable authority for owned strongboxes, generated weapons/armor,
premium ammunition, and future miscellaneous item stacks.

Owned paths:
- Assets/ShooterMover/Runtime/Domain/Holdings/**
- Assets/ShooterMover/Runtime/Contracts/Holdings/**
- Assets/ShooterMover/Runtime/Application/Holdings/**
- Assets/ShooterMover/Tests/EditMode/Holdings/**
- docs/architecture/contracts/PLAYER_HOLDINGS_V1.md

Required behavior:
typed additions/removals; unique equipment instances; stackable misc items;
strongbox ownership; exact-once mutations; immutable snapshots; provenance;
persistence-ready import/export.

Tests:
duplicate grant/remove, missing item, unique instance collision, stack bounds,
snapshot round trip, wrong reward type, sequence conflict.

Non-goals:
No equipment UI, equipping behavior, reward rolling, wallet balances, scene, or
save backend.
```

## RAP-001 — Reward commitment, claim, and atomic application

```text
Branch: agent/rap-001-reward-application
Base: BASE_AFTER_DEPENDENCIES
Dependencies: REW-001, INV-001, MON-001, SCR-001

Objective:
Own the lifecycle from immutable generated reward through projection, claim, and
atomic application to money, scrap, strongbox/equipment, and misc authorities.

Owned paths:
- Assets/ShooterMover/Runtime/Domain/Rewards/Application/**
- Assets/ShooterMover/Runtime/Contracts/Rewards/Application/**
- Assets/ShooterMover/Runtime/Application/Rewards/Application/**
- Assets/ShooterMover/Tests/EditMode/Rewards/Application/**
- docs/architecture/rewards/REWARD_CLAIM_LIFECYCLE_V1.md

Required behavior:
Generated/Projected/Claimed/Applied/Cancelled states; monotonic idempotent
transitions; one operation/grant identity throughout; retry-safe Claimed state;
all-or-none authority application; explicit restart/persistence policy.

Tests:
duplicate source callback, duplicate pickup callback, failure during application,
retry after failure, restart reprojection, already-applied no-change, mixed reward
atomicity.

Non-goals:
No pickup visuals, generation logic, balance values, persistence backend, or scene.
```

## GEN-001 — Shared deterministic reward/equipment generator

```text
Branch: agent/gen-001-shared-reward-generator
Base: BASE_AFTER_DEPENDENCIES
Dependencies: REW-001, EQP-001, RNG-001, PRG-001

Objective:
Implement the single deterministic generator used by drops, strongboxes, shops,
random crafting, tests, and simulation.

Owned paths:
- Assets/ShooterMover/Runtime/Domain/Rewards/Generation/**
- Assets/ShooterMover/Runtime/Application/Rewards/Generation/**
- Assets/ShooterMover/Tests/EditMode/Rewards/Generation/**
- docs/architecture/rewards/GENERATOR_TRACE_FORMAT.md

Required behavior:
candidate eligibility; weighted selection; quality availability; slot count;
augment selection/tier/level; guarantees; source biases; named substreams;
complete explainable trace; stable result fingerprint.

Tests:
same inputs exact equality, trace equality, substream isolation, compatibility
filtering, no eligible candidate result, impossible augment prevention,
deterministic quantity/weighted group behavior.

Non-goals:
No Unity editor UI, production catalogs, wallet mutation, pickup spawning,
strongbox ownership, shop persistence, or scene edits.
```

## SRC-001 — Reward definitions and source authoring

```text
Branch: agent/src-001-drop-source-authoring
Base: BASE_AFTER_DEPENDENCIES
Dependencies: REW-001, OBJ-001

Objective:
Create reusable Unity reward/drop definitions and a placed source component with
clear inherited defaults, explicit override modes, validation, and resolved
preview data.

Owned paths:
- Assets/ShooterMover/Content/Definitions/Rewards/**
- Assets/ShooterMover/Runtime/UnityAdapters/Rewards/Sources/**
- Assets/ShooterMover/Tests/EditMode/Rewards/Authoring/**
- Assets/ShooterMover/Tests/PlayMode/Rewards/Sources/**
- docs/authoring/REWARD_SOURCE_WORKFLOW.md

Required behavior:
inherit, none, replace, append guaranteed, money, exact box tier, box tier range,
and miscellaneous override modes; once-only source operation identity; restart
must not duplicate a claimed operation.

Non-goals:
No existing enemy/prop file edits, pickups, wallets, scene, or final balance.
ScriptableObjects: allowed in owned path. Prefabs: no.
```

## BOX-001 — Strongbox runtime

```text
Branch: agent/box-001-strongbox-runtime
Base: BASE_AFTER_DEPENDENCIES
Dependencies: GEN-001, SCR-001, INV-001, RAP-001

Objective:
Implement data-driven strongbox definitions and opening admission using owned box
instances from INV-001, shared generation, RAP-001 commitments, and mandatory
scrap awards.

Owned paths:
- Assets/ShooterMover/Runtime/Domain/Rewards/Strongboxes/**
- Assets/ShooterMover/Runtime/Application/Rewards/Strongboxes/**
- Assets/ShooterMover/Content/Definitions/Strongboxes/**
- Assets/ShooterMover/Tests/EditMode/Rewards/Strongboxes/**
- docs/architecture/rewards/STRONGBOX_OPENING_V1.md

Required behavior:
arbitrary tier count; tier biases, not bespoke code; exact-once open; exact
generator reuse; positive scrap on every successful open; duplicate open no
additional reward; trace/fingerprint retention.

Tests:
mandatory scrap invariant, duplicate opening, tier lookup, deterministic output,
invalid tier, commitment exactness, exceptional source bias support.

Non-goals:
No final 11-tier values, pickup visuals, inventory UI, persistence backend, or
scene. BAL-001 owns production tuning assets.
```

## CRA-001 — Crafting

```text
Branch: agent/cra-001-targeted-crafting
Base: BASE_AFTER_DEPENDENCIES
Dependencies: EQP-001, RNG-001, SCR-001, INV-001, RAP-001;
GEN-001 if random quality is enabled

Objective:
Implement recipe definitions, delayed availability, scrap affordability,
transaction-safe crafting, and crafted-quality policies.

Owned paths:
- Assets/ShooterMover/Runtime/Domain/Crafting/**
- Assets/ShooterMover/Runtime/Application/Crafting/**
- Assets/ShooterMover/Content/Definitions/Crafting/**
- Assets/ShooterMover/Tests/EditMode/Crafting/**
- docs/architecture/rewards/CRAFTING_V1.md

Required behavior:
unlock derived from natural level plus configurable positive delay; weapon-
specific costs; fixed/random quality policy; slot/tier/level guarantees and caps;
one scrap spend and one equipment grant per successful atomic craft.

Tests:
crafting cannot unlock at/before ordinary discovery activation, insufficient
scrap, duplicate transaction, deterministic random craft, cap enforcement.

Non-goals:
No balance values, salvage, refinement/reroll UI, scenes, or direct wallet mutation.
```

## AUG-001 — Money-funded augment upgrading

```text
Branch: agent/aug-001-augment-upgrades
Base: BASE_AFTER_DEPENDENCIES
Dependencies: EQP-001, MON-001, INV-001, RAP-001

Objective:
Implement money-funded augment level upgrades for owned equipment using authored
upgrade-cost curves and immutable equipment replacement.

Owned paths:
- Assets/ShooterMover/Runtime/Domain/Equipment/Upgrades/**
- Assets/ShooterMover/Runtime/Application/Equipment/Upgrades/**
- Assets/ShooterMover/Tests/EditMode/Equipment/Upgrades/**
- docs/architecture/rewards/AUGMENT_UPGRADES_V1.md

Required behavior:
validate ownership/slot/current level; compute current quoted cost; reject stale
quote, insufficient money, invalid jumps, and maximum-level upgrades; spend money
once and replace the equipment instance atomically.

Tests:
levels beyond the default ten when configured; maximum level; insufficient funds;
duplicate transaction; stale price; missing item/augment; snapshot sequence
conflict; exact money and equipment result.

Non-goals:
No upgrade UI, augment rerolling, tier/star upgrading unless explicitly approved,
balance values, or scenes.
```

## DOOR-001 — Reusable door package

```text
Branch: agent/door-001-reusable-doors
Base: BASE_AFTER_DEPENDENCIES
Dependencies: ADR-001, OBJ-001, accepted room/encounter contracts

Objective:
Create scene-independent reusable doors with typed visible conditions,
open/closed collision and presentation, animation hooks, one-way and transition
support, restart restoration, and validation.

Owned paths:
- Assets/ShooterMover/ContentPackages/Environment/Doors/**
- Assets/ShooterMover/Tests/EditMode/Environment/Doors/**
- Assets/ShooterMover/Tests/PlayMode/Environment/Doors/**
- docs/authoring/DOORS.md

Required behavior:
always, trigger, interact, encounter resolved, target destroyed, future wallet/
key condition ports, one-way, room transition, all/any composition, no Stage 1
controller dependency.

Tests:
condition evaluation, impossible configuration, collider states, restart,
arbitrary hierarchy placement, missing transition/socket validation.

Non-goals:
No Stage 1 scene placement, wallet implementation, key inventory, mission mutation,
or project settings. Generic prefab allowed inside owned package.
```

## VOID-001 — Void/fall hazards

```text
Branch: agent/void-001-fall-hazards
Base: BASE_AFTER_DEPENDENCIES
Dependencies: ADR-001, OBJ-001, accepted combat/restart contracts

Objective:
Create configurable, visible-in-editor void regions with per-category responses,
damage/death/respawn policy, checkpoint reference, presentation hooks, and
restart safety.

Owned paths:
- Assets/ShooterMover/ContentPackages/Environment/VoidHazards/**
- Assets/ShooterMover/Tests/EditMode/Environment/VoidHazards/**
- Assets/ShooterMover/Tests/PlayMode/Environment/VoidHazards/**
- docs/authoring/VOID_HAZARDS.md

Required behavior:
separate player/enemy/projectile/prop handling; accepted combat boundary for
damage; explicit projectile removal; optional enemy fall; checkpoint port; no
scene/map assumptions.

Tests:
filters, damage vs instant death, respawn, projectile removal, supported prop,
restart, missing checkpoint validation.

Non-goals:
No scene edit, new player-health authority, direct damage shortcut, or final art.
Generic prefab allowed.
```

## PROP-001 — Destructible-prop authoring migration

```text
Branch: agent/prop-001-definition-driven-destructibles
Base: BASE_AFTER_DEPENDENCIES
Dependencies: OBJ-001, SRC-001

Objective:
Migrate destructible props from Stage1/name-driven integration to reusable
definition/variant authoring while retaining accepted confirmed-hit authority,
destruction events, animation, collision, and restart behavior.

Owned paths:
- Assets/ShooterMover/ContentPackages/Props/DestructibleProps/**
- Assets/ShooterMover/Tests/EditMode/Props/**
- Assets/ShooterMover/Tests/PlayMode/Props/**
- docs/authoring/DESTRUCTIBLE_PROPS.md

Required behavior:
explicit collider/presentation references; authored StableId; variant definition;
drop profile/default and instance override; no name-prefix or sibling-index logic;
legacy migration seam only when necessary.

Tests:
parity with current destruction/restart, duplicate hit, arbitrary names,
duplicate identity validation, level-1/level-2 resolution, reward event once.

Forbidden:
Stage1VisibleSlice scene/controller/integration tests.

Non-goals:
No scene migration, enemy changes, wallet application, or pickup visuals.
```

## NORM-001 — Turret identity and registration normalization

```text
Branch: agent/norm-001-turret-registration
Base: BASE_AFTER_DEPENDENCIES
Dependencies: AUD-001, OBJ-001

Objective:
Remove global scene discovery and scene/hierarchy-name-derived identity from the
Blaster Turret while preserving tracking, cadence, physical projectiles, damage,
destroyed collision, presentation, and restart.

Owned files:
- ContentPackages/Enemies/BlasterTurret/BlasterTurretAuthoring2D.cs
- ContentPackages/Enemies/BlasterTurret/BlasterTurretSceneContext2D.cs
- ContentPackages/Enemies/BlasterTurret/BlasterTurret.prefab
- inseparable metadata for those files
- focused BlasterTurret package tests
- package documentation

Required behavior:
authored placed ID; explicit/nearest-parent generic scope; self-registration;
duplicate rejection; no FindFirstObjectByType/FindObjectsByType ordinary path.

Forbidden:
Stage1VisibleSlice scene/controller/integration tests; other enemy packages.

Acceptance:
existing turret behavior tests pass plus rename/reparent/duplicate-placement tests.

Non-goals:
No reward profile, balance, new attack, or scene migration.
```

## SHOP-001 — Procedural shops

```text
Branch: agent/shop-001-procedural-shops
Base: BASE_AFTER_DEPENDENCIES
Dependencies: GEN-001, MON-001, INV-001, RAP-001

Objective:
Implement deterministic shop inventory generation, pricing, stable refresh
identity, and transaction-safe purchase contracts using the shared generator.

Owned paths:
- Assets/ShooterMover/Runtime/Domain/Shops/**
- Assets/ShooterMover/Runtime/Application/Shops/**
- Assets/ShooterMover/Content/Definitions/Shops/**
- Assets/ShooterMover/Tests/EditMode/Shops/**
- docs/architecture/rewards/SHOPS_V1.md

Required behavior:
stable run/shop/refresh seed; configurable size; weapon/armor compatibility;
pricing inputs; insufficient-funds rejection; exact-once purchase; no refresh on
revisit/death/reload; atomic money spend plus equipment grant.

Non-goals:
No production balance values, shop UI, scene, persistence backend, or alternative
item generator.
```

## SIM-001 — First-class balancing simulator

```text
Branch: agent/sim-001-balance-simulator
Base: BASE_AFTER_DEPENDENCIES
Dependencies: GEN-001; extend after BOX-001, SHOP-001, CRA-001, AUG-001,
SRC-001, RAP-001

Objective:
Build a maintained Unity Editor balancing product that invokes exact runtime
application services for single-seed inspection and bulk simulation.

Owned paths:
- Assets/ShooterMover/Editor/ShooterMover.Editor.asmdef
- Assets/ShooterMover/Editor/BalanceSimulator/**
- Assets/ShooterMover/Tests/EditMode/BalanceSimulator/**
- docs/tools/BALANCE_SIMULATOR.md
- docs/architecture/ASSEMBLY_DEPENDENCIES.md
- tools/validation/validate_unity_assembly_graph.py
  only for the reviewed Editor assembly addition

Required modes:
one/many boxes, one/many shops, drop profile, scrap accumulation, crafting
availability/affordability, augment-upgrade affordability, reward-claim lifecycle,
result-by-seed.

Required outputs:
frequencies, slots, tiers, levels, quality budgets, money, scrap, percentiles,
early access, exceptional rolls, boxes-to-recipe, outlier seeds, traces,
definition fingerprint, CSV/JSON export.

Acceptance:
same definitions/context/seed match direct runtime service results exactly.
Large generated reports are not committed.

Non-goals:
No duplicate generator, production UI, scene, or silent balance approval.
```

## PICK-001 — Reward pickup presentation

```text
Branch: agent/pick-001-reward-pickups
Base: BASE_AFTER_DEPENDENCIES
Dependencies: RAP-001, SRC-001, BOX-001

Objective:
Project committed reward grants as collectable money, box, scrap/misc
presentation, then submit claims to RAP-001 without owning reward truth.

Owned paths:
- Assets/ShooterMover/Runtime/UnityAdapters/Rewards/Pickups/**
- Assets/ShooterMover/Runtime/Presentation/Rewards/**
- Assets/ShooterMover/ContentPackages/Environment/RewardPickups/**
- Assets/ShooterMover/Tests/PlayMode/Rewards/Pickups/**
- docs/authoring/REWARD_PICKUPS.md

Required behavior:
explicit operation/grant IDs, duplicate collection no-change, missing service
fail-closed, restart-safe reprojection, Claimed retry behavior, reduced-effects
presentation fallback.

Non-goals:
No scene placement, reward rolling, wallet authority, equipment inventory UI,
or final art.
```

## VAL-001 — Level-designer authoring tools

```text
Branch: agent/val-001-level-authoring-tools
Base: BASE_AFTER_DEPENDENCIES
Dependencies: OBJ-001, SRC-001, DOOR-001, VOID-001, SIM-001 editor assembly

Objective:
Provide focused inspectors, foldouts, inherited/resolved previews, duplicate-ID
validation, drop previews, bounds gizmos, and project-wide validation.

Owned paths:
- Assets/ShooterMover/Editor/Authoring/**
- Assets/ShooterMover/Tests/EditMode/AuthoringTools/**
- docs/authoring/LEVEL_DESIGNER_WORKFLOW.md

Required behavior:
show only applicable fields; reset to inherited; duplicate StableId scan;
missing reference and impossible-condition diagnostics; resolved drop summary;
deterministic preview seed.

Non-goals:
No runtime authority, scene edits, balance values, or generator implementation.
```

## STAT-001 — Statistical verification

```text
Branch: agent/stat-001-reward-statistics
Base: BASE_AFTER_DEPENDENCIES
Dependencies: GEN-001, BOX-001, SHOP-001, CRA-001, AUG-001, RAP-001

Objective:
Add deterministic invariants, broad distribution sanity tests, tier comparisons,
and simulator/runtime parity tests without freezing accidental exact percentages.

Owned paths:
- Assets/ShooterMover/Tests/EditMode/Rewards/Statistics/**
- Assets/ShooterMover/Tests/EditMode/Shops/Statistics/**
- Assets/ShooterMover/Tests/EditMode/Crafting/Statistics/**
- docs/verification/rewards/STATISTICAL_TEST_POLICY.md

Required behavior:
fixed-seed reproducibility; mandatory box scrap; crafting delay invariant;
configured soft early tail; no impossible augment; broad percentile/range checks;
upgrade-affordability ranges; reward-application idempotence; runtime/simulator
parity; report outlier seeds on failure.

Non-goals:
No production tuning changes or scene edits.
```

## BAL-001 — Initial balance catalogs

```text
Branch: agent/bal-001-initial-reward-catalogs
Base: BASE_AFTER_DEPENDENCIES
Dependencies: SIM-001, STAT-001, recorded human balance decisions

Objective:
Author the initial 11 strongbox tiers, equipment progression parameters, augment
defaults, shop policies, scrap awards, and crafting recipes as reviewed content
data.

Owned paths:
- exact new .asset files under Content/Definitions/{Equipment,Rewards,Strongboxes,Shops}
- exact new .asset files under Content/Definitions/Crafting
- one balance rationale/report under docs/balance/**
- inseparable metadata

Required evidence:
simulator reports at representative character levels and box tiers; expected
boxes-to-craft; early-access rates; exceptional-roll rates; tier comparisons;
old-item share; shop affordability.

Acceptance:
all material balance numbers are traceable to an approved decision or clearly
recorded experiment. No hidden constants in runtime code.

Non-goals:
No runtime code, scene, prefab, or unilateral balance decisions.
```

## INT-001 — Final Stage 1 reward/environment integration owner

```text
Branch: agent/int-001-stage1-reward-object-integration
Base: BASE_AFTER_DEPENDENCIES
Dependencies:
DEMO-001 merge commit, all selected runtime/package tasks, RAP-001, AUG-001,
BAL-001 where production data is required.

Objective:
Extend the already-playable DEMO-001 baseline with the generic scene scope,
migrated props/turret, one door/hazard demonstration, holdings, money/scrap,
bounded reward demonstrations, and augment-upgrade proof without introducing
new authority.

Exclusive owned paths:
- Assets/ShooterMover/Scenes/Prototypes/Stage1VisibleSlice.unity
- Assets/ShooterMover/Scenes/Prototypes/Stage1VisibleSlice.unity.meta
- Assets/ShooterMover/TestSupport/VisibleSlice/Stage1VisibleSliceController.cs
- paired metadata
- Assets/ShooterMover/Tests/PlayMode/VisibleSliceIntegration/**

Forbidden:
No other task may edit these paths while INT-001 is active.

Required behavior:
one generic scene scope; no name/global lookup path; inherited defaults visible;
one turret reward override demonstration; one money-only safe; restart without
duplicate or lost rewards; one claimed pickup applied atomically; one strongbox
opening with scrap; one augment money upgrade; existing
movement/shooting/boost/camera/robot parity.

Tests:
cold Unity compile; focused integration suite; fifty restarts; duplicate reward
check; projectile cleanup; identity scan; manual keyboard/mouse and controller
play; reduced-effects check.

Acceptance:
one playable scene, no duplicated authority, no unresolved ID/reference warnings,
and all package tests remain green.
```

---

# 9. Integration and merge order

## 9.1 Merge sequence

1. Parallel immediate group:

   * `ADR-001`
   * `AUD-001`
   * `DEMO-001`
2. Parallel foundation group after ADR approval:

   * `OBJ-001`
   * `REW-001`
   * `EQP-001`
   * `RNG-001`
   * `LED-001`
   * `PRG-001`
3. Parallel authority/generator group:

   * `MON-001`
   * `SCR-001`
   * `INV-001`
   * `GEN-001`
4. `RAP-001`
5. Parallel package/runtime group:

   * `SRC-001`
   * `BOX-001`
   * `CRA-001`
   * `AUG-001`
   * `DOOR-001`
   * `VOID-001`
   * `NORM-001`
6. `PROP-001`
7. Parallel product group:

   * `SHOP-001`
   * first complete `SIM-001`
   * `PICK-001`
8. `VAL-001`
9. `STAT-001`
10. `BAL-001`
11. `INT-001`

## 9.2 Rebase and retarget policy

* Every dependent PR starts from the exact current `main` after its prerequisites merge.
* Do not keep long-lived stacked branches unless the orchestrator explicitly records the stack.
* If a dependent PR was started early, rebase or recreate it from current `main` before proof.
* The PR description records the exact merge commits for every dependency.
* A merged branch is never reused, matching repository policy.

## 9.3 Integration glue ownership

* Generic composition contracts: `OBJ-001`.
* Reward commitment and atomic application: `RAP-001`.
* Existing package migration: `PROP-001` and `NORM-001`.
* Stage 1 glue and serialized references: **`DEMO-001` first**, then ownership is released; **`INT-001` only** during final integration.
* Lifecycle/generated handoff reconciliation: separate coordinator PR, not any feature branch.

## 9.4 `.meta` and generated asset handling

* The task creating or moving an asset owns its `.meta`.
* Do not add ancestor folder metadata unless it is explicitly owned.
* Do not regenerate central registries from package branches unless a designated registry task is created.
* Resolve generated-output conflicts in source inputs, regenerate once, and review the complete diff.
* Do not commit `Library`, `Temp`, `Logs`, `Obj`, `UserSettings`, simulator output, or unrelated import changes.

## 9.5 Robot and playable-baseline integration

Before `DEMO-001`:

```text
git status
git show --stat 96d6ce9
git show --name-status 96d6ce9
git merge-base 96d6ce9 main
```

The orchestrator freezes the exact robot-owned paths in the packet. `DEMO-001` then either:

* cherry-picks the clean commit;
* reapplies only its exact assets/references;
* or first publishes it as a separate dependency PR.

No economy/package task assumes the robot commit exists. `INT-001` later depends
on the merged DEMO-001 baseline rather than re-auditing or reapplying local art.

---

# 10. Test and simulation strategy

## 10.1 Test pyramid

### Plain C# EditMode tests

Cover:

* StableId and operation identity;
* PRNG frozen vectors;
* substream isolation;
* soft-unlock eligibility;
* quality availability;
* augment compatibility;
* drop-profile resolution;
* typed ledger transactions;
* wallet transactions;
* holdings mutations;
* reward commitment transitions and atomic application;
* box opening;
* shop inventory;
* crafting;
* augment upgrading;
* progression-context validation;
* deterministic traces;
* canonical fingerprints.

### Unity EditMode authoring tests

Cover:

* ScriptableObject validation;
* arbitrary variant count;
* inherited/resolved override preview;
* duplicate placed identity;
* missing references;
* impossible door conditions;
* invalid hazard responses;
* content-catalog consistency.

### Unity PlayMode tests

Cover:

* confirmed destruction produces one source-resolution request;
* duplicate destruction or callback produces no extra reward;
* quick restart restores presentation but does not repeat claimed rewards;
* projected pickups survive/re-project according to policy;
* pickup collection claims once and applies atomically;
* a failed claimed reward can be retried without duplication;
* doors restore collider/sprite state;
* hazards filter player/enemy/projectile/prop correctly;
* renamed/reparented objects still function;
* duplicated prefabs are caught by validation;
* no scene-name or object-name integration dependency.

## 10.2 Deterministic invariants

The following should be hard test invariants:

1. Same definitions, context, algorithm version, and seed produce exactly the same result and trace.
2. Simulator and runtime service results are byte-for-byte equivalent after canonical serialization.
3. A successful strongbox opening always grants `scrap > 0`.
4. The same box instance cannot be opened twice.
5. The same wallet transaction cannot mutate a balance twice.
6. The same holdings transaction cannot grant or remove an item twice.
7. A mixed reward commitment is either fully applied or not applied.
8. A `Claimed` commitment can be retried until `Applied`.
9. Crafting’s earliest valid level is later than the configured ordinary natural-discovery activation.
10. Invalid augment combinations can never appear.
11. An augment upgrade spends money once and replaces one owned equipment instance.
12. A source’s normal rolls and appended guarantee are both preserved in `AppendGuaranteed`.
13. `ReplaceProfile` suppresses inherited rolls.
14. `ForceNone` yields no grants.
15. Quick restart does not refresh shops or regenerate already-created rewards.
16. Projectiles retain physical-contact semantics.

## 10.3 Statistical tests

Do not test accidental exact percentages unless explicitly frozen as balance requirements. Test:

* broad configured confidence ranges;
* approximate monotonic improvement between tiers where intended;
* non-zero but bounded early-access probability;
* maximum-quality roll rarity;
* old-item population not exceeding a configured ceiling;
* new-item population not being crowded below a configured floor;
* scrap mean/percentiles;
* boxes-to-craft percentiles;
* shop affordability;
* augment-upgrade affordability by level;
* low-tier economy contribution;
* exceptional-result frequency.

Failures should print the seed and complete trace.

## 10.4 Simulator modes

The first-class simulator should support:

| Mode               | Inputs                         | Outputs                                   |
| ------------------ | ------------------------------ | ----------------------------------------- |
| One strongbox      | level, tier, seed              | exact item, augments, scrap, full trace   |
| Bulk boxes         | levels/tiers, count, seed root | distributions, percentiles, outlier seeds |
| One shop           | level, shop config, seed       | inventory, prices, trace                  |
| Bulk shops         | count and level range          | item/quality/price distributions          |
| Drop profile       | source profile/context         | reward-family frequencies                 |
| Scrap accumulation | box/drop schedule              | total scrap and recipe affordability      |
| Crafting analysis  | target recipe/level path       | unlock and affordability estimates        |
| Augment upgrades   | item/augment, money curve      | upgrade cost and affordability path       |
| Claim lifecycle    | mixed reward, failure point    | state transitions and applied holdings    |
| Seed inspection    | operation context and seed     | fully reproducible trace                  |

## 10.5 Simulator architecture

```text
BalanceSimulatorWindow
        ↓
BalanceSimulationService
        ↓
RewardGenerationService / StrongboxOpeningService /
ShopInventoryService / CraftingService / AugmentUpgradeService /
RewardApplicationService
        ↓
same Domain code used by gameplay
```

The EditorWindow is only an input/reporting shell.

## 10.6 Reports

For every simulation, store or export:

* content fingerprint;
* algorithm version;
* root seed;
* simulation count;
* input parameters;
* mean, median, minimum, maximum;
* P1/P5/P25/P75/P95/P99;
* item frequency;
* equipment category split;
* early-access rate;
* slot/tier/level distributions;
* total augment-level budget;
* exceptional-roll rate;
* money and scrap distributions;
* expected boxes-to-recipe;
* outlier seeds.

Generated reports should normally remain local or CI artifacts, not committed source.

## 10.7 Balance gates

Before production catalog approval, answer with simulator evidence:

* At natural level 17, how often does the item appear?
* What is the chance of an exceptional version?
* Does tier 8 improve meaningfully over tier 7?
* How many boxes are needed for P50 and P90 crafting affordability?
* What percentage of players can find the item before crafting unlock?
* Are low-tier boxes producing too much long-term economic value?
* Are older items crowding newer ones?
* Does shop purchasing outpace money generation?

Every answer is a **balance decision**, not a coding decision.

---

# 11. Level-designer workflow examples

## 11.1 Placing ten identical crates

1. Drag `DestructibleObject2D.prefab` into the room ten times.
2. Select `CrateFamilyDefinition`.
3. Leave variant as `Default / Level 1`.
4. Leave all override groups disabled.
5. Each placement receives or is assigned a unique authored instance ID.
6. The validation window reports:

   * 10 valid instances;
   * one inherited crate variant;
   * one inherited drop profile;
   * no duplicate IDs.

No new prefab or ScriptableObject is needed for each crate.

## 11.2 Creating a reinforced level-2 crate

1. Select one crate.
2. Under **Variant**, choose `crate.reinforced-level-2`.
3. The resolved preview shows:

   * 24 HP;
   * level-2 collider;
   * level-2 sprite;
   * destruction animation;
   * inherited level-2 drop profile.
4. Optionally enable **Override collision** for one unusual placement.

The designer does not edit runtime code.

## 11.3 Placing a level-2 turret

1. Drag the generic turret prefab into the room.
2. Assign `ShooterTurretFamilyDefinition`.
3. Select `turret.shooter-level-2`.
4. Choose home facing and tracking cone.
5. Leave rewards inherited.
6. Validation confirms a unique instance ID and a reachable scene scope.

## 11.4 Making one ordinary turret guarantee a strongbox

1. Select the turret.
2. Open **Rewards**.
3. Enable **Override drops**.
4. Choose `Append Guaranteed`.
5. Add `Strongbox`, then select:

   * exact tier; or
   * configured tier range.
6. The preview shows:

   * inherited normal rolls retained;
   * guaranteed box added;
   * deterministic preview seed and result.

Choosing `Replace Profile` instead would intentionally remove normal rolls.

## 11.5 Creating a safe that drops money only

1. Place a safe prefab.
2. Assign `SafeFamilyDefinition`.
3. Its default reward profile is `reward-profile.safe-money-only`.
4. Enter the money quantity/range in the shared profile asset.
5. No instance override is necessary unless this safe is special.

## 11.6 Creating a boss that drops strongboxes only

1. Select the boss definition or boss variant.
2. Set its inherited reward profile to `reward-profile.boss-strongboxes-only`.
3. Add guaranteed or weighted strongbox tier entries.
4. Do not add money or misc entries.
5. The profile validator displays “Strongbox only.”

The encounter/boss code remains unchanged.

## 11.7 Adding a new strongbox tier

1. Open `StrongboxCatalog`.
2. Add an entry with a unique stable tier ID.
3. Set display order and generation biases.
4. Assign presentation.
5. Run catalog validation and simulator comparisons.
6. No runtime switch or enum edit is required.

**BALANCE DECISION:** values for reach, quality, scrap, and exceptional multiplier.

## 11.8 Adding a weapon at natural level 50

1. Create an equipment definition referencing the existing weapon package ID.
2. Set:

   * category `Weapon`;
   * family;
   * natural discovery level `50`;
   * eligible augment tags;
   * maximum configured slots;
   * strongbox/shop eligibility.
3. Add it to the equipment catalog.
4. Validate content reference and compatibility.
5. Simulate levels below, near, and above 50.

The item has a soft early tail rather than a strict `level >= 50` gate.

## 11.9 Making that weapon craftable around level 56

1. Create a recipe referencing the weapon.
2. Use natural discovery level from the equipment definition.
3. Set crafting delay to approximately `+6`.

**BALANCE DECISION:** whether this is exactly +6 or a configurable +5 to +7 range.

4. Set scrap cost and crafted-quality policy.
5. Simulate:

   * chance of natural discovery before level 56;
   * boxes required for affordability;
   * crafted versus excellent-box quality.

## 11.10 Adding a new augment

1. Create an augment definition.
2. Assign:

   * stable ID;
   * eligible category/family/tags;
   * exclusion tags;
   * configured tier range;
   * maximum level;
   * effect scaling;
   * upgrade-cost curve.
3. Add it to the augment catalog.
4. Run compatibility validation and generator simulation.

No generator switch statement is changed.

## 11.11 Upgrading an augment

1. The player selects an owned equipment instance and augment slot.
2. `AugmentUpgradeService` reads the authored upgrade-cost curve.
3. The UI receives a quote for the next level.
4. Confirming submits one transaction containing the expected wallet and holdings sequences.
5. Money is spent and the immutable equipment instance is replaced atomically.
6. Duplicate confirmation cannot spend money or upgrade twice.

The default content may stop at augment level 10, but another augment can configure a different maximum without changing runtime code.

## 11.12 Creating a locked door

1. Place `Door2D.prefab`.
2. Assign a door definition.
3. Set initial state `Locked`.
4. Add a visible condition:

   * key owned;
   * encounter resolved;
   * target destroyed;
   * or later currency payment.
5. Configure collider and sprite/animation states.
6. Assign a transition socket only when it is a room-transition door.
7. Validate impossible or missing conditions.

No Stage 1 controller edit is needed.

## 11.13 Placing a void/fall region

1. Place `VoidHazard2D.prefab`.
2. Resize its visible gizmo bounds.
3. Configure:

   * player: respawn or instant death;
   * enemies: optional destroy/fall;
   * projectiles: remove;
   * props: remain supported or fall.
4. Assign checkpoint/respawn reference.
5. Add optional fall animation/audio.
6. Validate that a respawn action has a valid destination.

No map name or scene name is referenced.

---

# 12. Risks and unresolved design decisions

| Risk                                                 | Consequence                                              | Mitigation                                                                                                             |
| ---------------------------------------------------- | -------------------------------------------------------- | ---------------------------------------------------------------------------------------------------------------------- |
| Parallel agents invent incompatible definitions      | Boxes, shops, crafting, and drops cannot share data      | Merge ADR/contract tasks first; one owner per shared contract; consumers cannot fork it.                               |
| Multiple scene owners                                | Serialized conflicts and broken references               | `DEMO-001` owns Stage 1 first; ownership is explicitly released before `INT-001` becomes final owner.                  |
| Randomness cannot be reproduced                      | Bugs and balance outliers cannot be investigated         | Versioned integer PRNG, named substreams, frozen vectors, seed and trace in every result.                              |
| Simulator diverges from runtime                      | Balance reports become misleading                        | Simulator invokes exact Application services and parity tests compare canonical results.                               |
| Crafting invalidates strongboxes                     | Loot loses excitement                                    | Configurable positive delay, crafted-quality policy, and boxes-to-craft/early-access simulation gates.                 |
| Strict unlock gates replace soft curves              | Progression feels mechanical and contradicts design      | No `characterLevel >= unlockLevel` eligibility rule; tests require configured early tail and continued availability.   |
| Low-tier rewards scale too strongly                  | Players farm trivial content indefinitely                | Report long-term currency/scrap contribution and impose reviewed scaling ceilings.                                     |
| Old equipment crowds out new equipment               | New unlocks feel invisible                               | Configurable decay plus retention floor; simulator reports item share by “age.”                                        |
| New items dominate immediately                       | Progression becomes abrupt                               | Soft activation curve and limited ordinary quality near natural level.                                                 |
| Exceptional boxes become meaningless                 | Tier excitement disappears                               | Separate exceptional multiplier and report high-quality tail probabilities.                                            |
| Exceptional boxes are too strong                     | Progression skips become common                          | Human-approved probability ceiling and outlier-seed inspection.                                                        |
| Per-instance overrides are difficult to inspect      | Designers cannot tell what an object will do             | One explicit override mode, inherited/resolved preview, validation, reset action.                                      |
| ScriptableObject explosion                           | Content becomes hard to navigate                         | Family catalogs with variant lists; inline ordinary values; references only for genuinely reusable complex policy.     |
| Giant universal ScriptableObjects                    | Merge conflicts and unusable inspectors                  | Split catalogs by bounded subsystem and single serialized owner.                                                       |
| Identity collision after prefab duplication          | Rewards or lifecycle events deduplicate the wrong object | Serialized instance IDs, project/scene duplicate scanner, deliberate regeneration action.                              |
| Name-derived identity survives migration             | Rename/reparent changes runtime identity                 | Remove hierarchy hashing from production authoring and test rename/reparent stability.                                 |
| Wallet duplicate transactions                        | Infinite currency or double spending                     | Transaction-ID ledger, duplicate no-change status, persistence-ready snapshots.                                        |
| Wallets and holdings duplicate ledger behavior       | Divergent transaction semantics and repeated bugs        | Land `LED-001` first; money, scrap, and holdings compose the same tested primitive.                                     |
| Generated rewards have no durable owner              | Boxes/equipment/misc items disappear or live in UI code  | `INV-001` owns durable strongbox, equipment, and miscellaneous holdings.                                                |
| Pickup truth exists only in a GameObject              | Restart loses rewards or enables duplicate claims        | `RAP-001` owns Generated/Projected/Claimed/Applied state; pickups are projections only.                                 |
| Mixed rewards partially apply                         | Money is granted while equipment or scrap is lost        | Atomic reward application with retry-safe claim state and all-or-none tests.                                            |
| Character level comes from ad hoc scene state         | Runtime and simulator produce different eligibility      | `PRG-001` supplies one explicit progression context contract.                                                           |
| Augment costs exist without an upgrade authority      | UI or shops mutate equipment and money directly          | `AUG-001` owns quoting, wallet spend, immutable replacement, and duplicate protection.                                  |
| Restart mutates progression incorrectly              | Farming or lost rewards                                  | Explicit transient versus run/durable state table; claimed source IDs survive quick restart.                           |
| Reward drops occur twice through multiple observers  | Duplicate loot                                           | One source operation ID; reward service idempotence; destruction callback count tests.                                 |
| Strongbox opens without scrap                        | Bad-luck protection fails                                | Hard opening invariant and property test: every successful opening has positive scrap.                                 |
| Simulator reports become too large                   | Tool performance and repository pollution                | Aggregate by default, full traces only for selected seeds/outliers, reports kept as local/CI artifacts.                |
| Trace logging changes random results                 | Adding diagnostics perturbs generation                   | Named substreams; traces observe samples but do not consume extra randomness.                                          |
| PRNG algorithm changes accidentally                  | Existing seeds produce different items                   | Algorithm version, frozen vectors, explicit migration/version bump.                                                    |
| Serialized managed-reference conditions break        | Door/drop authoring becomes fragile                      | Prefer explicit serializable tagged data structures and validator-supported versions.                                  |
| Unity `.meta` conflicts                              | Broken asset references                                  | Exact serialized owner, leaf metadata only, no mixed-worktree `git add -A`.                                            |
| Existing enemy normalization is larger than expected | Scope ballooning and regressions                         | AUD-001 first; normalize only evidenced violations; separate package-specific PRs where needed.                        |
| Prop migration breaks Stage 1 parity                 | Visible demo regresses                                   | Preserve DEMO-001 behavior; migrate scene only in INT-001; parity tests before deleting helper.                        |
| Local robot commit conflicts with current main       | Integration loses art or scene changes                   | Audit exact paths/merge base before dispatch; DEMO-001 owns resolution and publishes the baseline.                    |
| Handoff files remain stale                           | Agents use outdated task state                           | Separate coordinator reconciliation PR; implementation packets specify current exact base and dependencies.            |
| Money and scrap persistence semantics remain unclear | Restart/save behavior is inconsistent                    | Record explicit lifecycle matrix before wallet integration.                                                            |
| Salvage assumptions leak into v1                     | Premature economy coupling                               | Preserve provenance and transaction extension points, but defer salvage values and UI.                                 |

## Unresolved decisions requiring human approval

1. **Reward persistence:** which rewards are provisional, banked, or retained after mission failure?
2. **Source claim lifetime:** run-wide, room-projection-wide, or mission-save-wide?
3. **Soft-unlock curve:** logistic, Gaussian-like activation, piecewise smooth, or authored sampled curve?
4. **Old-item floor:** minimum late-game probability per item or per family?
5. **Quality model:** scalar budget, multi-axis quality, or both?
6. **Strongbox tier relation:** strict stochastic improvement or deliberately overlapping identities?
7. **Exceptional roll target:** expected frequency by tier and character level.
8. **Scrap formula:** box-only, box plus item quality, or box plus duplicate/salvage status?
9. **Crafting delay:** fixed per recipe, family default plus override, or sampled variance?
10. **Crafted quality:** dependable baseline, optional paid guarantees, and absolute/soft caps.
11. **Shop refresh:** mission-only token, money reroll, time/event refresh, or no manual refresh initially?
12. **Pricing:** progression-adjusted absolute prices versus expected earnings multiples.
13. **Duplicate equipment:** permit duplicates, convert automatically, or retain for later salvage?
14. **Armor timing:** implement architecture immediately but defer production armor content, or ship one early armor definition?
15. **Void semantics:** whether ordinary enemies falling should grant drops, count as kills, or count as environmental withdrawal.
16. **Door payment semantics:** preview-only port now or full money-spend integration in the first door increment.
17. **Boss reward resolution:** on boss destruction, encounter completion, or durable room-clear acceptance. The safest durable choice is the accepted mission/encounter completion fact, but this affects perceived drop timing.
18. **Initial Stage 1 integration breadth:** demonstration-only money/box drops or a complete usable progression loop.
19. **Pickup commitment policy:** should generated physical drops be re-projected after quick restart, automatically banked, or lost only under an explicit mission-failure rule?
20. **Augment upgrades:** next-level-only upgrades initially, or allow multi-level purchases with a quoted cumulative price?
21. **Equipment loadout authority:** should equipping weapons/armor be part of the first holdings increment or remain a separate future system?

The architecture should not advance to `BAL-001` or broad Stage 1 integration until these balance and lifecycle decisions are recorded in durable repository artifacts. `DEMO-001` is intentionally exempt because it only publishes the already-existing playable behavior.
