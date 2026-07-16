# Stage 1 integration ownership

## Status

This document is the ADR-001 serialized-ownership and lifecycle lock for the
Stage 1 reward, equipment, object-authoring, door, hazard, shop, crafting,
simulator, and final integration program.

It is normative for every roadmap task that creates a package consumed by Stage
1 and for the two tasks allowed to edit the Stage 1 serialized integration paths.
It does not implement or edit Unity content.

This document consumes:

- [Reward and equipment architecture v1](../rewards/REWARD_EQUIPMENT_ARCHITECTURE_V1.md);
- [Placed object lifecycle v1](PLACED_OBJECT_LIFECYCLE_V1.md);
- [Reward, progression, and level-authoring plan](../REWARD_PROGRESSION_AND_LEVEL_AUTHORING_PLAN.md);
- [File ownership rules](../FILE_OWNERSHIP.md);
- [Unity assembly dependencies](../ASSEMBLY_DEPENDENCIES.md);
- [StableId v1](../contracts/STABLE_ID_V1.md);
- [Mission Messages v1](../contracts/MISSION_MESSAGES_V1.md);
- [Room Projection v1](../contracts/ROOM_PROJECTION_V1.md);
- [Encounter Lifecycle v1](../contracts/ENCOUNTER_LIFECYCLE_V1.md); and
- [EH-006 session reset and restart](../../verification/evidence-harness/SESSION_RESET_AND_RESTART.md).

The words **must**, **must not**, **required**, and **only** identify locked
architecture requirements.

## 1. One serialized owner at a time

Stage 1 has exactly one serialized owner at a time.

```text
DEMO-001 active
    -> DEMO-001 proof-complete merge
    -> explicit ownership release
    -> no feature task edits the released paths
    -> INT-001 starts from current main containing DEMO-001 and all dependencies
    -> INT-001 is the sole final owner
```

There is no concurrent or implied ownership between these phases. Finding an
unowned reference, merge conflict, missing component, or integration defect does
not grant another task permission to edit a serialized path.

### 1.1 DEMO-001 first owner

While active, `DEMO-001` exclusively owns the immediate playable-baseline paths
named by its packet, including:

- `Assets/ShooterMover/Scenes/Prototypes/Stage1VisibleSlice.unity`;
- its inseparable scene metadata when named by the packet;
- `Assets/ShooterMover/TestSupport/VisibleSlice/Stage1VisibleSliceController.cs`;
- its inseparable metadata;
- `Assets/ShooterMover/Tests/PlayMode/VisibleSliceIntegration/**`; and
- the exact robot asset/reference paths frozen in the `DEMO-001` packet.

`DEMO-001` publishes the already-working playable baseline. It does not add the
reward economy, doors, hazards, shops, strongboxes, or broad object-authoring
migration.

No package, architecture, wallet, holdings, reward, door, hazard, simulator, or
balance task may edit these paths while `DEMO-001` owns them.

### 1.2 Release after merge

Ownership is released only after the `DEMO-001` pull request:

1. contains its exact dependency and base record;
2. is proof-complete;
3. is intentionally merged by the human lead; and
4. leaves its branch permanently closed under repository policy.

A draft, closed-unmerged, failed, superseded, or unproven `DEMO-001` pull request
does not release ownership and does not satisfy `INT-001`.

Release means later tasks may consume the merged baseline read-only. It does not
mean every feature task may edit the scene. During the gap before `INT-001`, the
released Stage 1 serialized paths have no feature owner and must remain unchanged
unless the human lead creates a separately approved serial repair task.

### 1.3 INT-001 sole final owner

`INT-001` begins only from an exact current `main` commit containing:

- the merged `DEMO-001` baseline;
- all selected accepted runtime and package dependencies;
- `RAP-001` and the required money, scrap, and holdings authorities;
- normalized/migrated packages selected for integration;
- `VAL-001` validation support;
- `BAL-001` only where production tuning data is required; and
- every other dependency explicitly named in the dispatched `INT-001` packet.

While active, `INT-001` is the sole owner of:

- `Assets/ShooterMover/Scenes/Prototypes/Stage1VisibleSlice.unity`;
- `Assets/ShooterMover/Scenes/Prototypes/Stage1VisibleSlice.unity.meta`;
- `Assets/ShooterMover/TestSupport/VisibleSlice/Stage1VisibleSliceController.cs`;
- its paired metadata; and
- `Assets/ShooterMover/Tests/PlayMode/VisibleSliceIntegration/**`.

No other task may edit these paths while `INT-001` is active. `INT-001` integrates
accepted public contracts and package assets; it must not create a second wallet,
holdings authority, reward lifecycle, generator, progression context, scene
scope, checkpoint authority, combat authority, or mission authority.

## 2. Serialized-path ownership matrix

The exact task packet remains the narrowest authority. This matrix freezes the
non-overlapping ownership families needed by the roadmap; it does not enlarge any
packet.

| Task | Sole ownership family | Stage 1 serialized edit |
|---|---|---:|
| `ADR-001` | The three architecture documents named by this task | No |
| `AUD-001` | Read-only audit document subtree | No |
| `DEMO-001` | Stage 1 scene/controller/integration tests plus exact robot paths | **Yes, first** |
| `OBJ-001` | Generic Domain/Contracts/UnityAdapters authoring and object definitions | No |
| `REW-001` | Reward model and reward/economy contracts | No |
| `EQP-001` | Equipment/augment model, contracts, and definitions | No |
| `RNG-001` | Deterministic random and progression-curve implementation | No |
| `LED-001` | Shared typed ledger primitive | No |
| `PRG-001` | Progression-context contracts and providers | No |
| `MON-001` | Money authority | No |
| `SCR-001` | Scrap authority | No |
| `INV-001` | Strongbox/equipment/miscellaneous holdings authority | No |
| `RAP-001` | Reward commitment, claim, and atomic application | No |
| `GEN-001` | Shared deterministic reward/equipment generator | No |
| `SRC-001` | Reward/drop definitions and placed source adapters | No |
| `BOX-001` | Strongbox runtime and strongbox definition subtree | No |
| `CRA-001` | Crafting runtime and definition subtree | No |
| `AUG-001` | Augment upgrade runtime | No |
| `DOOR-001` | Reusable door package and focused tests/docs | No |
| `VOID-001` | Reusable void-hazard package and focused tests/docs | No |
| `PROP-001` | Existing destructible-prop package and focused tests/docs | No |
| `NORM-001` | Exact Blaster Turret authoring/context/prefab/test paths | No |
| `SHOP-001` | Shop runtime and definition subtree | No |
| `SIM-001` | Balance simulator Editor subtree and its reviewed assembly/tool additions | No |
| `PICK-001` | Pickup UnityAdapters, Presentation, package, and focused tests/docs | No |
| `VAL-001` | Authoring inspectors and validators | No |
| `STAT-001` | Statistical verification tests/docs | No |
| `BAL-001` | Exact human-approved production catalog assets and rationale | No |
| `INT-001` | Stage 1 scene/controller/integration tests | **Yes, final** |

Package tasks may create package-local prefabs or ScriptableObjects only when their
packet permits them. They never acquire ownership of Stage 1 merely because their
package will later be placed there.

`SIM-001` may edit `docs/architecture/ASSEMBLY_DEPENDENCIES.md` and the assembly
validator only for its separately reviewed Editor assembly addition, as specified
by its packet. That exception does not overlap ADR-001 or grant simulator
ownership of runtime contracts.

Central generated registries and `assembly/generated/**` remain owned by their
existing designated generator/workflow owners. No roadmap feature task manually
merges or opportunistically regenerates them.

## 3. Shared concept ownership review

The following names are the single source of truth for dependent packets:

| Shared concept | Named owner | Forbidden substitute |
|---|---|---|
| Placed identity/capability/scene-scope contracts | `OBJ-001` | Package-local universal object base or global scene lookup |
| Reward/economy contract vocabulary | `REW-001` | Shop-, box-, pickup-, or source-specific reward DTO hierarchy |
| Exact-once transaction primitive | `LED-001` | Separate duplicate handling in each wallet/inventory |
| Money | `MON-001` | UI balance, shop balance, scene wallet, or combined untyped currency bag |
| Scrap | `SCR-001` | Crafting-owned scrap count or money wallet accepting scrap |
| Holdings | `INV-001` | Private box lists, equipment lists, ammo lists, or pickup-owned inventory |
| Reward lifecycle/application | `RAP-001` | Pickup truth, source-local claim flag, or direct multi-authority mutation |
| Equipment/augment definitions | `EQP-001` | Strongbox- or shop-specific equipment schema |
| Deterministic random/curves | `RNG-001` | `UnityEngine.Random`, ambient `System.Random`, or simulator PRNG |
| Progression context | `PRG-001` | Scene player lookup, HUD level, or simulator-only level model |
| Reward/equipment generator | `GEN-001` | Separate box, shop, drop, crafting, test, or simulator generator |
| Reward source authoring | `SRC-001` | Enemy/prop code directly mutating wallets or holdings |
| Stage 1 scope/reference wiring | `INT-001`, after `DEMO-001` | Package task editing scene/controller |
| Durable mission facts | Existing mission authority/contracts | Door, encounter, room, or scene controller storing permanent clear state |
| Combat/destruction facts | Existing combat/enemy authorities | Reward, hazard, or presentation code inferring damage/death |
| Room projection | Existing room projection contracts | Unity scene object treated as durable room state |

Wave 1 tasks must depend on these owners. They must not unblock themselves by
inventing a second contract in a convenient local subtree.

## 4. Integration dependency and handoff rules

### 4.1 Package completion

A package is available to `INT-001` only after its proof-complete pull request is
merged. A draft branch, unmerged asset, local-only commit, or remembered chat
decision is not an integration dependency.

Each package handoff must record:

- exact dependency merge commits;
- exact changed paths;
- public contracts consumed;
- validation/test results;
- remaining manual proof;
- known limitations; and
- rollback scope.

### 4.2 Read-only consumption

`INT-001` consumes accepted package prefabs, definitions, contracts, and services
read-only unless its packet explicitly grants an exact defect-fix path. Integration
problems in another package are fixed by that package owner or a separately
approved repair task; they are not silently patched inside the scene branch.

### 4.3 Composition only

Stage 1 composition may:

- add one generic scene scope;
- serialize explicit service/scope references;
- place accepted package prefabs;
- choose accepted definitions/variants and instance overrides;
- assign unique authored placed IDs;
- connect typed door, hazard, checkpoint, condition, reward, and presentation
  ports; and
- extend focused integration tests.

It may not copy application logic into the controller, store durable truth in the
scene, derive identity from names, or bypass package/public services.

### 4.4 Ownership conflict procedure

Before every serialized write, the task owner must confirm no other active task
owns the same scene, prefab, ScriptableObject, source asset, shared file, or
paired metadata. On conflict:

1. stop before editing;
2. identify the two tasks and exact overlapping path;
3. let the human lead sequence or reassign one owner; and
4. restart or rebase the later task from the accepted merge.

Serialized conflicts are not resolved by parallel edits, manual YAML merging,
copying a scene, or creating a second integration scene without approval.

## 5. Lifecycle matrix

### 5.1 Interpretation

The matrix distinguishes four operations:

- **Quick restart:** same `run_id`, same authored content, fresh attempt/restart
  generation, and rebuilt attempt-local runtime state.
- **Room reload:** same run, a replacement room projection/runtime identity, and
  the current authoritative mission/reward projection.
- **Mission restart:** an explicit mission-domain operation. The table describes
  a same-run mission restart; a policy that creates a new `run_id` follows the
  **New run** column instead.
- **New run:** a new `run_id`, source-claim namespace, runtime projection lineage,
  and run-seeded shop namespace.

A product decision may choose among stated policy options. It may not violate the
identity, monotonicity, exact-once, or authority invariants.

### 5.2 Required behavior

| State/category | Quick restart | Room reload | Mission restart, same run | New run |
|---|---|---|---|---|
| Enemies and props | Retire attempt callbacks/resources; recreate or reset health, collision, presentation, AI/session state from package initial state; retain authored IDs; resolved source cannot generate again | Replace runtime projection; retain authored IDs; project current mission/source facts; no reward regeneration from reconstruction | Reset/reproject according to mission restart rules; source resolution remains protected by the same-run claim ledger | Create fresh runtime actors/projections for the new run; authored IDs and definitions remain stable; new source operations may occur under the new run ID |
| Doors and hazards | Restore package-defined initial transient collider/presentation/trigger state; read durable conditions again through typed ports | Recreate projection and evaluate current authoritative conditions/checkpoint references; no scene-name discovery | Restore according to explicit mission restart policy; durable mission/route facts are changed only by mission authority | Fresh runtime projection and run-scoped conditions; authored door/hazard IDs remain stable |
| Projectiles and transient effects | Remove all retiring-attempt projectiles, impact effects, trails, temporary subscriptions, and pickup view objects before replacement attempt activates | Remove projection-owned projectiles/effects/subscriptions; new projection starts without stale transient objects | Clear mission-runtime transients before restart completes | No transient object crosses into the new run |
| `Generated` rewards | Preserve immutable commitment; do not regenerate; project or claim according to explicit pickup policy | Preserve and re-project/claim from authoritative commitment as policy requires | Explicit policy must retain/re-project or cancel; never create a second commitment from the same source | Old-run commitment remains in old-run history; it is not replayed into the new run unless an explicit banking/persistence operation already transferred value |
| `Projected` rewards | Dispose transient projection, preserve commitment, then re-project or follow explicit policy | Dispose old projection and re-project from commitment where valid | Explicit policy must retain/re-project or cancel; destroying the view alone is not cancellation | Old projection does not cross runs; old commitment remains old-run state |
| `Claimed` rewards | Preserve `Claimed`; retry the same aggregate application until `Applied`; never re-roll or cancel | Preserve and retry through the same operation identity | Preserve and retry; same-run restart cannot cancel or duplicate a claim | A claim must reach a terminal/recoverable result under its old-run identity; a new run must not create a duplicate application |
| `Applied` rewards | Preserve terminal state and applied operation IDs | Preserve terminal state and applied operation IDs | Preserve terminal state; an explicit compensating product transaction, not restart cleanup, is required to remove value | Applied profile-durable value follows the approved persistence model; run-only value must be represented by a separate explicit run authority rather than silently erasing the shared durable authority |
| `Cancelled` rewards | Preserve terminal state; no projection or application | Preserve terminal state | Preserve terminal state | Remains old-run terminal history |
| Money and scrap | Balances, sequences, and applied transaction IDs do not reset | Unchanged; scene/room projections read immutable snapshots | Shared applied authorities do not silently reset. Any provisional/unbanked model must be a separate explicitly owned authority/policy | Follow the approved profile/run persistence model; a new run alone is not permission to mutate durable balances without a typed transaction |
| Strongbox/equipment/premium-ammo/misc holdings | Ownership, sequences, equipment instance IDs, and applied operations do not reset | Unchanged; presentation reloads from holdings snapshot | Shared applied holdings do not silently reset. Mission-loss behavior requires an explicit provisional/banking model | Follow approved persistence model; new run receives no duplicate copies of old items |
| Shop inventory identity | Same `run_id`, `shop_id`, and `refresh_ordinal` produce the same stock; no restart refresh | Same identity and stock on revisit/reload | No automatic refresh. An approved mission policy may submit an explicit new refresh ordinal/operation | New run creates new run-seeded inventory identities; revisits within that run remain stable |
| Source claim ledgers | Retain generated/claimed/applied/cancelled source operations; same source cannot farm through restart | Retain across projection replacement | Retain for same-run restart; policy may cancel eligible unclaimed commitments but must not erase operation history | Create a fresh run-scoped ledger namespace; old ledger remains historical and is not reused for changed payloads |

### 5.3 Matrix invariants

The following apply in every column:

1. Authored `StableId` values do not change because an object is renamed,
   reparented, reloaded, restarted, or projected again.
2. A new runtime/projection/attempt identity does not imply a new reward source
   operation.
3. A new `run_id` never reuses old source, shop, claim, or transaction identities
   for changed payloads.
4. Scene cleanup cannot mutate mission truth, wallet balances, holdings, reward
   commitment truth, or source-claim truth.
5. `Claimed` remains retryable; `Applied` and `Cancelled` remain terminal.
6. Mixed application is all-or-none.
7. Shop stock does not reroll on quick restart, death, revisit, or room reload.
8. A product that needs provisional/unbanked value must model it explicitly before
   `Applied`; it must not reinterpret a durable applied authority after the fact.

## 6. Doors, hazards, and typed integration ports

Doors and hazards are package consumers of accepted typed ports. `INT-001`
serializes references; it does not implement their authorities.

Doors may consume:

- lifecycle/restart registration;
- encounter-resolution and target-destruction readers;
- mission/room condition readers;
- interaction/trigger inputs;
- wallet or holdings read ports for visible conditions;
- typed room-transition authorization; and
- open/closed collider/presentation adapters.

Hazards may consume:

- lifecycle/restart registration;
- typed player/enemy/projectile/prop classification;
- accepted combat/contact and enemy lifecycle ports;
- projectile removal;
- checkpoint/respawn lookup; and
- presentation hooks.

Neither package may use a Stage 1 controller method, global `Find*`, object names,
scene names, direct wallet mutation, direct mission mutation, or a second health
system as its ordinary integration path.

## 7. Required Stage 1 integration proof

Before `INT-001` can leave draft, its proof must establish at least:

- exactly one reachable generic scene scope;
- every persistent placement has a unique authored `StableId`;
- no global/name-derived integration path is used;
- inherited defaults and explicit instance overrides resolve as expected;
- existing movement, aiming, shooting, boost, camera, robot, turret, and prop
  behavior remains intact;
- one typed door and one typed hazard example consume their public ports;
- one reward source generates once across fifty quick restarts;
- a projected pickup can be rebuilt without duplicating reward truth;
- one claimed mixed reward applies all-or-none and duplicate claim is no-change;
- one strongbox opens once and grants mandatory positive scrap;
- one augment upgrade spends money once and replaces one owned item once;
- shop identity remains stable across revisit/reload/restart where demonstrated;
- projectiles and transient effects are fully cleaned on restart;
- no duplicate ID, missing reference, conflicting scope, or unresolved lifecycle
  warning remains; and
- focused package and integration tests remain green.

Proof must use the accepted public services. A test that passes through a
scene-local fake authority does not prove integration.

## 8. Wave 1 readiness assertions

ADR-001 is complete only when Wave 1 can proceed without inventing a second
shared concept. The architecture locks these assertions:

- `OBJ-001` owns one placed identity/capability/scope contract.
- `REW-001` owns one reward/economy vocabulary.
- `EQP-001` owns one equipment/augment definition model.
- `RNG-001` owns one deterministic algorithm and progression-curve model.
- `LED-001` owns one typed idempotent-ledger primitive.
- `PRG-001` owns one explicit progression-context provider.
- Money, scrap, and holdings later retain separate public authorities while
  composing the shared ledger semantics.
- `RAP-001` later owns one monotonic reward lifecycle and atomic application.
- `GEN-001` later owns one generator shared by runtime and simulator.
- Stage 1 has only the sequenced `DEMO-001` then `INT-001` serialized owners.

A Wave 1 task must stop and escalate if its implementation appears to require a
second wallet, inventory, reward lifecycle, progression context, random service,
scene scope, serialized owner, or generator.

## 9. Unresolved human decisions

The following choices remain intentionally open and do not grant an implementation
agent permission to choose silently:

- whether mission restart retains, re-projects, banks, or cancels eligible
  unclaimed rewards;
- whether mission restart keeps the same run ID or creates a new run;
- the exact profile-level versus run-level persistence model for money, scrap,
  and holdings;
- source-claim lifetime in a future save/resume system;
- shop refresh/reroll policy;
- boss reward timing;
- void-caused enemy kill/withdrawal/reward semantics;
- door payment and key-consumption semantics;
- the first Stage 1 progression-loop breadth; and
- all production balance values.

The required ports, identities, ownership, and exact-once boundaries are fixed
regardless of those choices. A later durable decision selects among the explicit
policy options and supplies data; it does not create a parallel architecture.

## 10. ADR-001 review checklist

Architecture review must confirm:

- [ ] the three ADR-001 documents are the only changed paths;
- [ ] every shared concept has one named owner;
- [ ] no two roadmap tasks own the same serialized path concurrently;
- [ ] `DEMO-001` is first and ownership release is explicit;
- [ ] `INT-001` is the sole final serialized owner;
- [ ] placed objects use explicit/nearest-parent scope and authored StableId;
- [ ] rewards use one monotonic lifecycle and atomic application owner;
- [ ] money, scrap, and holdings share ledger semantics but not public authority;
- [ ] simulator/runtime service parity is mandatory;
- [ ] the lifecycle matrix covers quick restart, room reload, mission restart,
      and new run for every required category;
- [ ] unresolved product/balance choices are visible and non-blocking for Wave 1;
- [ ] no Unity, scene, prefab, asset, `.meta`, handoff, generated, package, or
      project-setting change is present; and
- [ ] repository documentation/link/layout validation is recorded in the pull
      request without claiming unexecuted Unity or shell tests.

## 11. Non-goals

This ownership lock does not:

- edit a scene, controller, integration test, prefab, ScriptableObject, source
  asset, or metadata;
- change implementation or balance data;
- regenerate task backlog or registries;
- edit lifecycle handoff files;
- create a save/persistence backend;
- merge or approve any future task; or
- allow concurrent Stage 1 serialized edits.
