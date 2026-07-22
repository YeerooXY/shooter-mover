# PICKUP-LIVE-001 — Physical reward realization and run-local collection

## Scope

This boundary begins with an accepted `PendingTerminalDropAdmissionResultV1` from
`DROP-FACT-BIND-001` and ends after the exact generated reward child is recorded as
collected in the active Run Session.

```text
accepted pending terminal-drop batch
  -> exact GeneratedTerminalDropRewardV1 child
  -> RunLocalPickupAuthorityV1
  -> reconstructable Unity presentation
  -> RunPickupCollectionCommandV1
  -> immutable RunPickupCollectionFactV1 admitted by RunSessionAggregateV1
```

It deliberately does **not** apply money, scrap, strongboxes, or equipment to permanent
character authorities. Mission result transfer, permanent holdings mutation, account save,
and crash-safe downstream proof remain owned by `DROP-PERSIST-PROOF-001`.

## Ownership

### Generated reward

`DROP-FACT-BIND-001` owns terminal adaptation, canonical DROP operation construction,
deterministic GEN execution, child ordering, exact generated child identity, and batch
fingerprint. `TerminalDropRunPickupAdapterV1` only copies those immutable facts into the
pickup boundary. It performs no profile lookup and has no DROP or GEN dependency.

`EnemyTerminalDropFactConsumerV1` and `PropTerminalDropFactConsumerV1` may publish the
accepted pending-admission result to one downstream
`IPendingTerminalDropAdmissionConsumerV1`. Exact redelivery still crosses the idempotent
pending-admission and pickup realization boundaries; it never invokes DROP or GEN again.

### Physical pickup

`RunLocalPickupAuthorityV1` owns run-local pickup truth:

- stable pickup identity;
- exact generated reward child identity and fingerprint;
- source DROP operation, terminal event, source entity, placement, and batch fingerprint;
- run identity and lifecycle generation;
- attributed participant;
- reward kind, content identity, and quantity;
- authoritative world-spawn context;
- pending-source-position, available, collected, cancelled, or rejected state vocabulary;
- collector entity and participant;
- collection operation, order, and authoritative Run Session tick;
- replay and conflict records.

A Unity `GameObject` owns none of these facts.

### Run Session

`ExistingRunSessionPickupPortV1` adapts the current `RunSessionAggregateV1`. It projects
one `RunSessionCollectedRewardV1` containing the exact pickup, generated child, source DROP
operation, terminal/source provenance, reward content and quantity, world-spawn context,
collector, collection operation, order, and authoritative tick.

`RunSessionAggregateV1.RecordCollectedRunReward` validates the active lifecycle and exact
player actor/participant, admits the typed record fingerprint through the existing Run
Session fact replay boundary, and stores the exact immutable record in a lifecycle-filtered
run-local journal. The pickup authority also retains its immutable world projection so
presentation can be reconstructed without querying Unity objects.

The Run Session journal is the sole collection-order authority. Its
`NextCollectedRewardOrder` is derived from records in the current lifecycle. The pickup
authority does not retain a global or cross-lifecycle collection counter. After lifecycle
restart, the first accepted collection is order `1` even when older lifecycle records exist.

The adapter does not call `RecordCollectedStrongbox`, the mission-result port, RAP, wallet,
holdings, inventory, save, or Results transfer.

## Identity derivation

One pickup identity is derived from:

- run identity;
- Run Session lifecycle generation;
- canonical terminal DROP operation identity;
- exact generated reward child identity;
- generated child fingerprint.

The derivation does not include frame time, Unity instance ID, object name, hierarchy path,
scene search result, player position, or random input.

Consequences:

- redelivery of one exact child resolves to the same pickup;
- two children containing the same definition remain distinct;
- two Emerald strongboxes remain two exact box instances;
- presentation reconstruction cannot create a second authoritative pickup;
- conflicting child or operation reuse rejects before mutation.

## Source-position resolution

`IRunPickupSourcePositionPortV1` is the narrow source-position boundary. The included
`RunPickupSourcePositionRegistry2D` accepts typed registrations for the exact run,
lifecycle, source entity, optional authored placement, room, committed world position, and
source-position fingerprint.

Pickup realization never uses random nearby coordinates, the current player position,
object names, `FindObjectOfType`, or Stage 1 position constants.

When source position is unavailable, the authority creates a recoverable
`PendingSourcePosition` record with the already-derived pickup identity. Exact retry after
the position becomes available promotes that same record to `Available`; it does not
reroll or replace the reward.

Production Stage 1 enemy integration captures the terminal enemy transform when the existing
enemy authority becomes destroyed. Production destructible-prop integration subscribes the
existing typed `DestructibleProp2D.Destroyed` event and captures the authored collider centre
or transform. Both routes register exact run, lifecycle, source entity, placement, room, and
position facts before forwarding the accepted #277 admission to pickup realization.

## Stackable and unique rewards

A generated stackable child such as money or scrap becomes one pickup containing the exact
generated quantity.

DROP-FACT-BIND-001 already emits one child per unique strongbox or equipment-reference unit.
The pickup boundary preserves each child identity. It never merges unique children based on
shared content definition.

## Unity presentation

`RunPickupPresenter2D` queries `ExportAvailablePickups` and maintains at most one
`RunRewardPickup2D` view per exact pickup ID in the currently projected room.

`RunPickupPresentationRegistry2D` resolves an exact content mapping first and then an
optional reward-kind fallback. Money, scrap, strongboxes, and equipment references use the
same presenter and view classes; adding ordinary content requires presentation data rather
than another pickup controller.

A view may configure a prefab, sprite, scale, trigger radius, and label. A missing registry
route, invalid style, null instantiation, or prefab exception is diagnostic and retryable.
It does not collect, cancel, or discard the authoritative pickup.

The production player receives `RunPickupCollector2D` identities directly from the active
Run Session player snapshot. Tags, hierarchy names, collider instance IDs, and GameObject
names are not collector authority.

## Collection semantics

Unity trigger callbacks only submit `RunPickupCollectionCommandV1`, containing:

- collection operation ID;
- pickup ID;
- exact generated child ID;
- run ID and lifecycle generation;
- collector entity and participant IDs;
- expected immutable pickup fingerprint.

The authority validates the complete context while holding one synchronization gate.

- First valid command records the exact immutable child in the Run Session journal and
  transitions `Available -> Collected` once.
- Exact operation replay returns the original accepted collection fact while the lifecycle
  remains current; the same replay rejects as stale after a Run Session generation change.
- Conflicting operation reuse rejects.
- Wrong run, stale/future lifecycle, wrong pickup-child pairing, wrong collector, missing
  attribution, changed fingerprint, or unavailable pickup rejects without mutation.
- Concurrent collision callbacks cannot create two collection records.
- The physical view hides and is retired only after accepted collection or accepted replay.

Collection means only:

> The player picked this exact reward up during this exact run.

## Lifecycle restart

Stage 1 already increments the live player lifecycle generation when a mission attempt is
restarted. `Stage1RunPickupBootstrap2D` observes that generation. A run-ID or lifecycle
change tears down the old transient pickup projection and composes the same production route
for the current generation:

- a current-lifecycle Run Session journal;
- a current-lifecycle source-position registry;
- current-lifecycle enemy/prop terminal bindings;
- current player actor and participant collector identities;
- a current-room presenter projection.

The retired lifecycle cannot realize or collect new pickups. The new lifecycle begins with
`NextCollectedRewardOrder == 1`. No permanent holding is rebuilt or mutated.

## Room re-entry and reconstruction

Presentation lifetime is separate from authority lifetime.

- Leaving a room removes only its views.
- Returning queries the same available snapshots and reconstructs the same pickup IDs.
- Collected pickups are absent from the available projection and do not respawn.
- Repeated pending-batch delivery is idempotent.
- A stale Run Session generation cannot restore or collect old pickups.

No disk checkpoint or account persistence is added here.

## Failure and retry behavior

| Failure | Authoritative result | Retry behavior |
|---|---|---|
| Run Session/player context unavailable or throws | Structured rejection; no pickup mutation | Same realization or collection command may retry |
| Source position unresolved or throws | Exact child retained or rejected without replacement identity | Same child and pickup ID are retried |
| Presentation mapping missing | Pickup remains `Available` | Registry may be fixed and synchronized again |
| Prefab instantiation throws | Pickup remains `Available` | Synchronization retries without new identity |
| Run Session record rejects or throws | Pickup remains `Available` | Same collection command may be retried |
| Conflicting identity reuse | No mutation | Producer/context must be corrected |
| Stale lifecycle | No mutation | Only current Run Session generation is eligible |

## Production composition

`RunPickupLiveCompositionV1.Create` remains the narrow pickup seam accepting one existing
`RunSessionAggregateV1` and one `IRunPickupSourcePositionPortV1`.

`Stage1RunPickupBootstrap2D` connects the production Stage 1 player, rooms, enemy authorities,
#277 terminal-drop composition, source positions, pickup authority, presenter, and collector.
`Stage1RunPickupPropBootstrap2D` adds the existing destructible-prop terminal event route to
the same pickup authority. Both components are installed when the accepted Stage 1 scene is
loaded through the production flow. `Stage1VisibleSliceController.cs` remains unchanged.

The production bridge PlayMode fixture covers exact accepted #277 admission, committed
terminal position, physical pickup creation, real 2D trigger collection, exact single Run
Session record, and view retirement. The repository still requires Unity execution of this
fixture and the production Bootstrap -> Hub -> Level 1 route before merge.

## Validation commands

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

These commands and output paths are the required merge proof. They have not been executed in
the connector-only environment used to author this patch; no XML pass counts are claimed.
