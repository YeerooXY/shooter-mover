# PICKUP-LIVE-001 — Physical reward realization and run-local collection

## Scope

This boundary begins with an accepted `PendingTerminalDropAdmissionResultV1` from
`DROP-FACT-BIND-001` and ends after the exact generated reward child is recorded as
collected in the one shared production `RunSessionAggregateV1`.

```text
accepted pending terminal-drop batch
  -> retained exact-admission delivery queue
  -> exact GeneratedTerminalDropRewardV1 child
  -> RunLocalPickupAuthorityV1 attached to shared Run Session
  -> reconstructable Unity presentation
  -> RunPickupCollectionCommandV1
  -> immutable RunPickupCollectionFactV1
  -> RunSessionCollectedRewardV1 in the shared aggregate
```

It deliberately does **not** apply money, scrap, strongboxes, or equipment to permanent
character authorities. Mission-result transfer, holdings mutation, account save, and
crash-safe downstream proof remain owned by `DROP-PERSIST-PROOF-001`.

## One production Run Session

Stage 1 has exactly one production aggregate, owned by
`Stage1PlayableLoopCompositionV1` on the `ENEMY-ATTACK-PATTERN-LIVE-001` integration line.

```text
shared RunSessionAggregateV1
├── player and frozen weapons
├── canonical conditions and status effects
├── active abilities
├── rooms
├── mission result
├── enemy attack consumers
└── pickup journal and physical pickup consumer
```

`Stage1RunPickupBootstrap2D` is a consumer only. It calls
`TryResolveSharedRunSession`, passes that exact aggregate to
`RunPickupLiveCompositionV1.Create`, and owns only pickup-specific source, realization,
presentation, and collector adapters.

It does not construct `RunSessionAuthorityV1`, submit `StartRunSessionCommandV1`, create a
runtime-port factory, or provide replacement player, weapon, condition, status, ability,
room, or mission-result ports. The former `Stage1PickupRunSessionPortsV1.cs` shadow graph
was deleted.

PR #279 is intentionally stacked on PR #278 until the shared aggregate and authoritative
in-place restart are merged. After #278 merges, #279 may be retargeted to `main` without
changing pickup ownership.

## Generated reward ownership

`DROP-FACT-BIND-001` owns terminal adaptation, canonical DROP operation construction,
deterministic GEN execution, child ordering, exact generated child identity, and batch
fingerprint. `TerminalDropRunPickupAdapterV1` copies those immutable facts into the pickup
boundary. It performs no profile lookup and has no DROP or GEN dependency.

Enemy and prop terminal consumers may publish the accepted pending-admission result to the
retained delivery queue. Exact redelivery crosses the idempotent admission and pickup
realization boundaries; it never creates a replacement child or reruns an already accepted
DROP operation.

## Retained transactional delivery

`Stage1PendingAdmissionPickupBridgeV1` retains each accepted admission by exact canonical
DROP operation and batch fingerprint.

```text
accepted admission
  -> enqueue immutable admission
  -> resolve exact terminal source
  -> register authoritative position
  -> realize exact child batch
  -> synchronize presentation
  -> acknowledge and remove queue record
```

The record is removed only after realization returns `Realized` or `ExactReplay` and the
presenter acknowledges a successful synchronization. Temporary failures retain the same
admission for a later attempt.

The queue survives release and reconfiguration of its Unity runtime dependencies. Therefore
tearing down and rebuilding the pickup projection does not lose an already admitted reward.
Exact admission redelivery is an exact replay; conflicting operation reuse is rejected.

The enemy terminal observer marks its terminal event delivered only after the retained queue
accepts custody. The prop adapter stores the immutable `Destroyed` fact and retries
generation, admission, position registration, and queue handoff until acknowledged.

## Physical pickup authority

`RunLocalPickupAuthorityV1` owns run-local pickup truth:

- stable pickup identity;
- exact generated reward child identity and fingerprint;
- source DROP operation, terminal event, source entity, placement, and batch fingerprint;
- run identity and lifecycle generation;
- attributed participant;
- reward kind, content identity, and quantity;
- authoritative world-spawn context;
- pending-source-position, available, collected, cancelled, or rejected state;
- collector entity and participant;
- collection operation, order, and authoritative Run Session tick;
- replay and conflict records.

A Unity `GameObject` owns none of these facts.

## Shared Run Session journal

`ExistingRunSessionPickupPortV1` adapts the shared aggregate. For each accepted collection it
projects one `RunSessionCollectedRewardV1` containing the exact pickup, generated child,
source DROP operation, provenance, reward content and quantity, world-spawn context,
collector, collection operation, order, and authoritative tick.

`RunSessionAggregateV1.RecordCollectedRunReward` validates the active lifecycle and exact
player actor/participant, admits the record through the aggregate fact boundary, and stores
it in a lifecycle-filtered journal.

The shared journal is the sole collection-order authority. `NextCollectedRewardOrder` is
derived from records in the current lifecycle; the pickup authority has no independent or
cross-lifecycle sequence counter.

The adapter does not call `RecordCollectedStrongbox`, RAP, wallet, holdings, inventory, save,
Results transfer, or another mission-result authority.

## Identity derivation

One pickup identity is derived from:

- run identity;
- Run Session lifecycle generation;
- canonical terminal DROP operation identity;
- exact generated reward child identity;
- generated child fingerprint.

It does not include frame time, Unity instance ID, object name, hierarchy path, player
position, scene search, or random input.

Consequences:

- exact redelivery resolves to the same pickup;
- two children with one content definition remain distinct;
- two Emerald strongboxes remain two exact instances;
- presentation reconstruction cannot create another authoritative pickup;
- conflicting child or operation reuse rejects before mutation.

## Source-position resolution

`IRunPickupSourcePositionPortV1` is the engine-neutral boundary.
`RunPickupSourcePositionRegistry2D` stores the exact run, lifecycle, source entity, optional
placement, room, committed world position, and source-position fingerprint.

Production enemies use the actual dying enemy transform. Production props use the typed
`DestructibleProp2D.Destroyed` event and the collider centre or transform. There is no random
coordinate, player-position fallback, GameObject-name authority, or Stage 1 position
constant.

An unresolved or throwing source leaves the exact admission queued. At the engine-neutral
boundary, unresolved source position may also produce `PendingSourcePosition`; retry promotes
the same pickup identity to `Available`.

## Stackable and unique rewards

A stackable child such as money or scrap becomes one pickup containing the exact generated
quantity. Strongbox and equipment-reference children retain exact child/instance identities.
Unique children are never merged by content definition.

## Unity presentation and collection

`RunPickupPresenter2D` maintains at most one `RunRewardPickup2D` view per exact available
pickup in the projected room. Missing mappings, invalid presentation data, instantiation
exceptions, or temporary presenter unavailability do not consume the queue or pickup.

The production player receives `RunPickupCollector2D` identities from the shared Run Session
player snapshot. Tags, names, hierarchy paths, collider IDs, and callback counts are not
authority.

A trigger submits `RunPickupCollectionCommandV1` containing the collection operation, pickup,
exact child, run/lifecycle, collector identities, and expected pickup fingerprint.

- First valid collection records the exact child and transitions `Available -> Collected`.
- Exact replay returns the original fact while the lifecycle remains current.
- Wrong run/lifecycle, child pairing, collector, fingerprint, or unavailable pickup rejects.
- Concurrent callbacks cannot create two collection records.
- The view hides only after accepted collection or accepted exact replay.

Collection means only:

> The player picked this exact reward up during this exact run.

## Authoritative restart

PR #278 owns mission restart through `RunSessionAggregateV1.Restart`. The authority and
aggregate object, run ID, frozen character/loadout inputs, fact replay history, and journal
storage survive. The lifecycle generation advances exactly once and transient ports reset
through the aggregate transaction.

Pickup integration observes the same aggregate object. On lifecycle change it refreshes only
current-lifecycle collector and terminal-source bindings, retires stale queued admissions,
and keeps the pickup authority attached to the shared aggregate. It does not construct a new
Run Session.

Old-lifecycle pickups and replays are filtered or rejected stale. The first accepted
collection in the replacement lifecycle uses order `1` because the shared journal derives
ordering from current-lifecycle records.

## Failure and retry behavior

| Failure | Authoritative result | Retry behavior |
|---|---|---|
| Source binding/position unavailable or throws | Exact admission remains queued | Same immutable admission retries |
| Position registration temporarily fails | Exact admission remains queued | Same source and identity retry |
| Run Session context unavailable or realization throws/rejects temporarily | No accepted delivery transition | Same admission retries |
| Presenter unavailable, mapping missing, or instantiation fails | Realized pickup remains authoritative and queue remains pending | Realization exact-replays; presentation retries |
| Runtime projection teardown/recomposition | Queue remains retained while runtime dependency is released | Replacement runtime resumes exact admission |
| Exact admission redelivery | Exact replay | No duplicate pickup |
| Conflicting operation/fingerprint reuse | No mutation | Rejected as conflict |
| Collection record rejects or throws | Pickup remains available | Same collection command may retry |
| Stale lifecycle | No mutation | Only current lifecycle is eligible |

## Validation

Authored coverage includes:

- source position unavailable once, then success;
- Run Session context unavailable once, then success;
- presenter unavailable once, then success through exact realization replay;
- exception during first delivery;
- exact admission redelivery without duplicate realization;
- runtime release/recomposition retaining an admitted reward;
- source architecture guard forbidding a pickup Run Session owner;
- lifecycle-one and lifecycle-two collection order both beginning at `1`;
- physical admission, terminal position, Physics2D trigger, and exactly-once collection.

Required commands:

```bash
Unity -batchmode -nographics -projectPath . -quit \
  -logFile artifacts/pickup-live-001-compile.log
```

```bash
Unity -batchmode -nographics -projectPath . \
  -runTests -testPlatform EditMode \
  -testFilter ShooterMover.Tests.EditMode.RunPickups \
  -testResults artifacts/test-results/pickup-live-001-editmode.xml \
  -logFile artifacts/test-results/pickup-live-001-editmode.log
```

```bash
Unity -batchmode -nographics -projectPath . \
  -runTests -testPlatform PlayMode \
  -testFilter ShooterMover.Tests.PlayMode.RunPickups \
  -testResults artifacts/test-results/pickup-live-001-playmode.xml \
  -logFile artifacts/test-results/pickup-live-001-playmode.log
```

These commands have not been executed in the connector-only environment. No compilation,
XML artifact, or pass count is claimed.
