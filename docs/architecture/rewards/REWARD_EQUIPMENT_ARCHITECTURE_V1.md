# Reward and equipment architecture v1

## Status

This document is the ADR-001 architecture lock for reward generation, reward
application, money, scrap, holdings, equipment, progression context, shops,
crafting, strongboxes, upgrades, and balancing simulation.

It is normative for the Wave 1 and later task packets named in
[`REWARD_PROGRESSION_AND_LEVEL_AUTHORING_PLAN.md`](../REWARD_PROGRESSION_AND_LEVEL_AUTHORING_PLAN.md).
It defines technical ownership and invariants only. It does not implement a
system, create balance data, select persistence policy, or change Unity content.

The following accepted contracts remain authoritative and are consumed rather
than replaced:

- [StableId v1](../contracts/STABLE_ID_V1.md);
- [Combat Messages v1](../contracts/COMBAT_MESSAGES_V1.md);
- [Mission Messages v1](../contracts/MISSION_MESSAGES_V1.md);
- [Room Projection v1](../contracts/ROOM_PROJECTION_V1.md);
- [Encounter Lifecycle v1](../contracts/ENCOUNTER_LIFECYCLE_V1.md);
- [EH-006 session reset and restart](../../verification/evidence-harness/SESSION_RESET_AND_RESTART.md);
- [File ownership rules](../FILE_OWNERSHIP.md); and
- [Unity assembly dependencies](../ASSEMBLY_DEPENDENCIES.md).

The words **must**, **must not**, **required**, and **only** identify locked
architecture requirements. A later change to one of these requirements requires
a reviewed versioned architecture decision.

## 1. Authority map

Every shared concept has one named owner. Consumers may depend on its public
contracts, but may not create a second authority, private wallet, private
inventory, alternate lifecycle, alternate generator, or scene-local substitute.

| Shared concept | Sole owner task | Public responsibility |
|---|---|---|
| Reward/economy message model | `REW-001` | Immutable grants, drop specifications, source overrides, commands, results, and traces |
| Typed exact-once ledger primitive | `LED-001` | Engine-independent transaction identity, duplicate no-change, sequence checks, snapshots, and validated import |
| Money | `MON-001` | Public money authority and money snapshot only |
| Scrap | `SCR-001` | Public scrap authority and scrap snapshot only |
| Strongbox/equipment/miscellaneous holdings | `INV-001` | Public ownership authority for unique and stackable holdings |
| Reward commitment, claim, and atomic application | `RAP-001` | Commitment lifecycle and all-or-none application across public authorities |
| Equipment and augment definitions | `EQP-001` | Equipment/armor/augment metadata, compatibility, instances, and validation |
| Deterministic random algorithm and progression curves | `RNG-001` | Versioned integer PRNG, named substreams, eligibility, quality, and crafting-availability mathematics |
| Progression context | `PRG-001` | Explicit character/region/difficulty context and providers |
| Shared reward/equipment generation | `GEN-001` | The only runtime generation implementation used by every product surface |
| Reward source authoring | `SRC-001` | Drop profiles, placed-source overrides, and source-resolution adapter contracts |
| Strongbox opening | `BOX-001` | Exact-once opening of owned strongboxes through shared generation and application |
| Crafting | `CRA-001` | Recipe admission, scrap spend, and equipment grant transaction |
| Augment upgrading | `AUG-001` | Quote validation, money spend, and immutable equipment replacement |
| Shops | `SHOP-001` | Stable shop inventory identity, pricing input, and purchase transaction |
| Pickup presentation | `PICK-001` | Transient projection and claim submission only; never reward truth |
| Balance simulator | `SIM-001` | Editor/reporting shell invoking the exact application services |
| Production balance catalogs | `BAL-001` | Human-approved authored tuning data only |
| Stage 1 serialized integration | `INT-001`, after `DEMO-001` releases ownership | Final scene/controller references only; no new authority |

The detailed serialized ownership sequence is locked in
[`STAGE1_INTEGRATION_OWNERSHIP.md`](../authoring/STAGE1_INTEGRATION_OWNERSHIP.md).
Placed identity, scene scope, capabilities, doors, and hazards are locked in
[`PLACED_OBJECT_LIFECYCLE_V1.md`](../authoring/PLACED_OBJECT_LIFECYCLE_V1.md).

## 2. Layering and assembly direction

The existing inward-only assembly direction remains unchanged:

```text
Domain
  ↑
Contracts
  ↑
Application
  ↑                  ↑
UnityAdapters   Content.Definitions
  ↑                  │
Presentation         │
  └──────────┬───────┘
             ↑
          Bootstrap
```

The reward architecture follows these boundaries:

- Domain owns immutable values, state transitions, deterministic algorithms, and
  validation without `UnityEngine`.
- Contracts owns immutable requests, results, rejection codes, snapshots, and
  service ports without scene or asset references.
- Application owns public authorities and orchestration.
- Content.Definitions owns immutable authored configuration only.
- UnityAdapters translates destruction, pickup, door, hazard, and scene-scope
  callbacks into typed application requests.
- Presentation renders immutable snapshots and transient projections; it does not
  own balances, holdings, claim state, shop stock, or generated items.
- Bootstrap composes concrete services.

Mutable balances, generated equipment, strongbox ownership, source claims, reward
commitments, applied operation IDs, and shop runtime identity must never live in
shared ScriptableObjects.

## 3. Identity model

All durable and cross-boundary identities use canonical `StableId` values. Unity
instance IDs, hierarchy paths, scene names, object names, sibling indices, frame
numbers, and object references are not durable identities.

The model distinguishes at least these identities:

| Identity | Purpose |
|---|---|
| `run_id` | Names one run-scoped reward/source/shop namespace |
| `source_instance_id` | Authored placed source identity or explicit spawned identity |
| `source_operation_id` | Names one logical source resolution within a run |
| `commitment_id` | Names one immutable generated reward commitment |
| `grant_id` | Names one grant entry within a commitment |
| `transaction_id` | Idempotency identity for one authority mutation or aggregate application |
| `equipment_instance_id` | Unique owned equipment or armor instance |
| `strongbox_instance_id` | Unique owned box instance and opening identity |
| `shop_inventory_id` | Stable run/shop/refresh inventory identity |
| `recipe_id` | Stable authored crafting recipe identity |
| `algorithm_version` | Version of the deterministic generation algorithm |

A retry must reuse the original logical ID. Reusing an ID with changed payload,
context, result fingerprint, or target authority is a conflicting duplicate and
must fail closed.

Grant identities remain stable from generation through projection, claim, and
application. A pickup projection may have a transient projection identity, but it
must carry the durable commitment, operation, and grant identities it represents.

## 4. Typed ledger primitive and separate public authorities

### 4.1 Shared primitive

Money, scrap, and holdings must compose one typed idempotent-ledger primitive
owned by `LED-001`. The primitive supplies common transaction semantics:

- immutable transaction ID;
- exact duplicate returns `DuplicateNoChange`;
- conflicting duplicate is rejected;
- optional expected-sequence admission;
- validation before mutation;
- deterministic canonical snapshot ordering and fingerprinting;
- validated import/export boundary; and
- no engine, scene, UI, save-file, or product-specific dependency.

Sharing the primitive does not merge public authorities.

### 4.2 Money and scrap remain separate

`MON-001` and `SCR-001` expose separate public services, commands, result enums,
snapshots, sequences, and change facts. A money authority must reject scrap
currency IDs and a scrap authority must reject money currency IDs. Neither may
inspect or mutate the other's balance.

A consumer may submit a typed request through the owning service. It must not
reach into the shared ledger, mutate a snapshot, keep a mirrored balance, or
apply a compensating private balance after failure.

### 4.3 Holdings authority

`INV-001` is the explicit ownership authority for:

- owned strongboxes;
- generated weapon equipment;
- armor equipment;
- premium ammunition;
- unique future equipment instances; and
- stackable miscellaneous items.

The holdings authority uses the shared exact-once transaction semantics while
retaining holdings-specific validation and public snapshots. Shops, crafting,
strongbox opening, pickups, UI, scenes, and equipment presentation must not keep
private ownership lists.

Equipping/loadout state is not silently included in this authority. Whether the
first holdings increment also owns equipped slots remains an unresolved product
decision recorded below.

## 5. Reward commitment lifecycle

### 5.1 States

One immutable `RewardCommitment` owns reward truth after generation:

```text
Generated -> Projected -> Claimed -> Applied
     |            |
     +------------+-------> Cancelled

Generated ----------------> Claimed
```

The direct `Generated -> Claimed` path supports rewards that intentionally have
no physical pickup projection.

The states mean:

| State | Meaning |
|---|---|
| `Generated` | The immutable grants, context, seed/version, and fingerprint exist; no presentation is required yet |
| `Projected` | One or more transient views represent the commitment; presentation remains disposable |
| `Claimed` | Admission succeeded and the exact immutable application may be retried |
| `Applied` | Every targeted public authority accepted the same aggregate operation exactly once |
| `Cancelled` | An explicit policy ended an unclaimed commitment without application |

### 5.2 Monotonicity and idempotence

Transitions must be monotonic. Exact repeats return no-change. Reusing a
transition identity with changed data is rejected. `Applied` and `Cancelled` are
terminal.

`Claimed` is intentionally retryable. A crash, scene unload, restart, or
application failure must not turn a claim into a second grant or lose its
immutable result.

Reward truth must not exist only in a pickup `GameObject`, effect, HUD entry,
source component, enemy component, room projection, or local scene controller.

### 5.3 Cancellation

`Cancelled` is not a generic cleanup shortcut. It is permitted only when:

1. the commitment is `Generated` or `Projected`;
2. an explicit versioned cancellation policy admits the transition;
3. a stable cancellation command/reason identity is recorded; and
4. the transition preserves the original commitment and generation fingerprint.

A `Claimed` or `Applied` commitment must never be cancelled. Destroying a pickup,
unloading a scene, restarting an attempt, despawning a source, or losing a Unity
reference does not by itself cancel a commitment.

The exact mission-failure and banking policies that may request cancellation are
human decisions. `RAP-001` owns the policy port and transition mechanics; it does
not choose the product policy.

## 6. Atomic mixed-reward application

A mixed commitment may target money, scrap, strongbox/equipment holdings, and
miscellaneous holdings. Application is all-or-none and retry-safe.

`RAP-001` owns one aggregate application transaction with these invariants:

1. The immutable commitment and all target commands are derived before mutation.
2. Every target authority validates identity, type, sequence, capacity, and
   preconditions before any externally visible mutation.
3. If any target rejects validation, no target snapshot changes and the
   commitment remains `Claimed`.
4. A successful commit makes every target mutation and the commitment's
   `Applied` transition observable as one logical operation.
5. Retrying the same aggregate operation returns the already-applied result with
   no additional balance or item change.
6. Reusing any member transaction ID or the aggregate operation ID with changed
   payload is a conflict.

The shared ledger primitive does not itself own cross-authority orchestration.
`RAP-001` is the sole coordinator. A future persistence backend must preserve the
same atomic boundary through an atomic journal/transaction or deterministic
recovery; it may not expose a permanently partial applied state.

Product systems use this boundary:

- a strongbox opening removes one owned box and applies generated equipment,
  mandatory positive scrap, and side rewards once;
- a shop purchase spends money and grants the purchased equipment once;
- crafting spends scrap and grants one result once;
- an augment upgrade spends money and replaces one immutable owned equipment
  instance once; and
- a claimed pickup applies its commitment once.

## 7. Source resolution and duplicate prevention

Reward generation begins only from an accepted typed source fact. Examples are:

- a confirmed terminal enemy destruction fact;
- a once-only destructible-prop destruction fact; or
- a matching durable encounter/mission completion fact when that source policy
  intentionally waits for durable completion.

Reward adapters consume the accepted combat, enemy, encounter, and mission
boundaries. They must not infer destruction from animation, collision visuals,
missing GameObjects, object names, or health copied into presentation code.

Each logical source resolution has one `source_operation_id`. The run-scoped
source-claim ledger records at least generated, claimed, cancelled, and applied
operation identities as required by the final `RAP-001` contract. Repeated
callbacks, duplicate physics contacts, duplicate destruction observers, quick
restart, room reload, and retry must converge on the existing operation.

A source that has resolved in the current run cannot generate another reward
merely because its runtime projection was rebuilt.

## 8. Progression context

Gameplay and simulation consume the same explicit immutable context:

```text
ProgressionContext
- character_level
- region_level
- difficulty
- optional progression_tags
```

`PRG-001` owns `IProgressionContextProvider` and validation.

- Gameplay receives an authored/session provider until a future XP authority is
  accepted.
- Simulation supplies a direct context.
- Generators, drops, shops, crafting, upgrades, and tests receive context through
  parameters or the provider port.
- No system may discover character level through a global player lookup, scene
  search, object name, HUD text, static singleton, or simulator-only shortcut.

The context shape is locked; the numerical meaning and balance curves remain
owned by their later tasks and human-reviewed data.

## 9. Deterministic randomness

`RNG-001` owns one documented, versioned integer algorithm. All reward,
equipment, strongbox, shop, crafting randomness, tests, and simulation use it.
`UnityEngine.Random`, ambient `System.Random`, time-based seeds, frame counts, and
unordered collection iteration are forbidden generation inputs.

A generation seed includes the root value and algorithm version. Named substreams
are derived from stable purpose IDs and ordinals. At minimum, the generation
contract must isolate:

- eligibility;
- candidate selection;
- quantities;
- quality;
- slot count;
- augment selection;
- augment tier;
- augment level;
- scrap;
- side rewards;
- shop inventory/price randomness when approved; and
- crafting randomness when approved.

Adding a cosmetic roll or trace field must not shift equipment, scrap, shop, or
crafting results. Tracing observes generated samples and never consumes extra
randomness.

Every generated result and simulator report carries the algorithm version,
root seed, named-substream trace or trace fingerprint, progression context, and
content-definition fingerprint.

Changing the algorithm or substream derivation requires a version bump, frozen
vector updates, and an explicit migration/compatibility decision. It is not a
balance-only asset edit.

## 10. Shared generator and product services

`GEN-001` is the single reward/equipment generator. Drop resolution, strongbox
opening, shops, random crafting, tests, and simulation call it rather than
copying eligibility, weighting, quality, slot, augment, or quantity logic.

Definitions describe data and policy inputs. They do not contain executable
scene behavior or mutable generated state.

Product services remain separate application authorities:

- `BOX-001` validates ownership and exact-once opening, then calls generation and
  reward application;
- `SHOP-001` owns stable stock identity and purchase admission, then calls shared
  generation/application services;
- `CRA-001` owns recipe admission and its scrap/equipment transaction;
- `AUG-001` owns upgrade quotes and its money/equipment transaction; and
- `PICK-001` owns presentation and claim submission only.

No product service may apply balances or holdings directly around `RAP-001`.

## 11. Simulator/runtime parity

The simulator is a maintained product surface, not an alternate model.
`SIM-001` owns an Editor input/reporting shell that invokes the exact runtime
application services:

```text
BalanceSimulatorWindow
        -> BalanceSimulationService
        -> RewardGenerationService / StrongboxOpeningService /
           ShopInventoryService / CraftingService /
           AugmentUpgradeService / RewardApplicationService
```

The simulator may batch calls and aggregate outputs. It must not reimplement
eligibility, random sampling, prices, application, claim transitions, wallet
rules, holdings rules, or crafting/upgrade transactions.

For equal definitions, context, algorithm version, seed, and command identity, a
simulator invocation and direct runtime service invocation must produce canonically
equal results and traces. Generated large reports remain local or CI artifacts
unless a review task explicitly owns a compact evidence file.

## 12. Restart, reload, and new-run invariants

The canonical lifecycle matrix is in
[`STAGE1_INTEGRATION_OWNERSHIP.md`](../authoring/STAGE1_INTEGRATION_OWNERSHIP.md).
The reward-specific locked invariants are:

- quick restart and room reload do not roll back applied money, scrap, holdings,
  or applied operation IDs;
- quick restart and room reload do not regenerate an already-created reward or
  refresh a stable shop inventory;
- `Generated` and `Projected` commitments remain authoritative without their
  presentation and are re-projected or handled by an explicit policy;
- `Claimed` commitments remain retryable until `Applied`;
- source-claim identity survives any same-run restart or reload;
- mission restart uses an explicit reward policy and cannot silently duplicate,
  regenerate, or partially roll back operations; and
- a new run creates a new run-scoped source-claim and shop identity namespace,
  while profile-level durable balances/holdings follow the separately approved
  persistence model.

If the product wants an "unbanked" balance that can be lost, that value must be
represented as a distinct provisional authority or commitment state before
`Applied`. An already `Applied` durable transaction must not be retroactively
removed by deleting scene objects or clearing a transient ledger.

## 13. Validation obligations

Dependent tasks must add focused proof for their owned boundaries. Across the
system, the following are hard architecture invariants:

1. duplicate source callback produces no additional commitment;
2. duplicate pickup callback produces no additional application;
3. mixed reward application is fully applied or unchanged;
4. a failed `Claimed` application can be retried exactly;
5. quick restart and room reload do not refresh shops or regenerate rewards;
6. money and scrap reject each other's typed entries;
7. holdings rejects duplicate unique instance IDs and duplicate transactions;
8. the same strongbox instance cannot open twice;
9. simulator and runtime service results are canonically equal;
10. deterministic substreams remain isolated;
11. gameplay and simulator use the same progression-context shape; and
12. no Unity-facing component becomes a wallet, inventory, claim, or generator
    authority.

## 14. Unresolved human decisions

The following remain deliberately unresolved. They do not block Wave 1 contract
boundaries because the architecture exposes explicit policy/data inputs instead
of choosing values silently.

- Which `Generated`/`Projected` rewards are retained, re-projected, banked, or
  cancelled after mission failure or mission restart.
- Whether source claims persist only for a run or also through a later mission
  save/resume format.
- The banking model and whether provisional currencies/items exist before
  `Applied`.
- Soft-unlock, old-item decay, quality, exceptional-roll, and retention curves.
- All strongbox tier values, scrap amounts, prices, reroll costs, recipe costs,
  crafting delays, augment costs, and production probabilities.
- Duplicate equipment and future salvage behavior.
- Shop refresh policy and manual reroll availability.
- Whether holdings v1 also owns equipped/loadout slots.
- Whether boss reward timing follows destruction, encounter completion, or the
  matching durable mission room-clear event.
- Whether enemy destruction by a void hazard is reward-bearing, kill-bearing, or
  environmental withdrawal.
- Which cancellation reasons are enabled by the first mission policy.
- Whether augment upgrades are next-level-only or may quote multi-level
  purchases.

These decisions must be recorded in a durable reviewed artifact before a task
that needs their values or behavior proceeds. No implementation agent may infer
them from examples, current scene behavior, simulator defaults, or chat history.

## 15. Non-goals

This architecture lock does not:

- implement runtime or editor code;
- create assets, prefabs, scenes, or metadata;
- choose production balance values;
- define a save backend or migration format;
- add XP, leveling, loadout UI, inventory UI, shop UI, or pickup art;
- replace accepted combat, enemy, encounter, mission, room, or restart contracts;
- authorize a second wallet, inventory, generator, lifecycle, scene scope, or
  progression source; or
- grant any task ownership outside its exact packet paths.
