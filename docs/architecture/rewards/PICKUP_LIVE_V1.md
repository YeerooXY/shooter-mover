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

`ExistingRunSessionPickupPortV1` adapts the current `RunSessionAggregateV1`. It validates
active run/lifecycle and the exact player actor/participant, then admits the immutable
collection fact through `RunSessionAggregateV1.AdmitFact`. The typed pickup journal remains
inside the run-local pickup authority and is visible through immutable snapshots.

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
object names, `FindObjectOfType`, or Stage 1 constants.

When source position is unavailable, the authority creates a recoverable
`PendingSourcePosition` record with the already-derived pickup identity. Exact retry after
the position becomes available promotes that same record to `Available`; it does not
reroll or replace the reward.

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

## Collection semantics

Unity trigger callbacks only submit `RunPickupCollectionCommandV1`, containing:

- collection operation ID;
- pickup ID;
- exact generated child ID;
- run ID and lifecycle generation;
- collector entity and participant IDs;
- expected immutable pickup fingerprint.

The authority validates the complete context while holding one synchronization gate.

- First valid command records the exact immutable fact and transitions
  `Available -> Collected` once.
- Exact operation replay returns the original accepted fact.
- Conflicting operation reuse rejects.
- Wrong run, stale/future lifecycle, wrong pickup-child pairing, wrong collector, missing
  attribution, changed fingerprint, or unavailable pickup rejects without mutation.
- Concurrent collision callbacks cannot create two collection records.
- The physical view hides and is retired only after accepted collection or accepted replay.

Collection means only:

> The player picked this exact reward up during this exact run.

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
| Source position unresolved | Exact child retained as `PendingSourcePosition` | Same child and pickup ID are retried |
| Presentation mapping missing | Pickup remains `Available` | Registry may be fixed and synchronized again |
| Prefab instantiation throws | Pickup remains `Available` | Synchronization retries without new identity |
| Run Session record rejects | Pickup remains `Available` | Same collection command may be retried |
| Conflicting identity reuse | No mutation | Producer/context must be corrected |
| Stale lifecycle | No mutation | Only current Run Session generation is eligible |

## Composition and parallel-work boundary

`RunPickupLiveCompositionV1.Create` is a pickup-specific seam accepting the existing
`RunSessionAggregateV1` and one `IRunPickupSourcePositionPortV1`. It avoids edits to shared
production composition while PR #278 owns enemy attack scheduling, projectile/melee
realization, Combat Hit Policy routing, player-damage adapters, and enemy catalog migration.

No file from those paths and no `Stage1VisibleSliceController.cs` behavior is changed.
Production Bootstrap/room wiring can inject this seam after the parallel composition work is
settled without changing pickup authority semantics.

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
