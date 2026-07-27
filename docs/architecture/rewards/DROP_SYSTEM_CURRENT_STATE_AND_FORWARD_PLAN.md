# DROP-SYSTEM-AUDIT-001 — Current state and five-step forward plan

## Status and scope

This document records a static source audit of the current production drop path and proposes
an implementation order that maximizes visible gameplay progress without creating replacement
reward, wallet, pickup, strongbox, Run Session, or persistence authorities.

Snapshot date: **2026-07-27**.

The audit covers:

- cash, scrap, and strongbox rewards from enemy deaths;
- future rewards from destructible props;
- physical pickup realization and collection;
- run-local reward custody;
- permanent character transfer and saving;
- strongbox ownership and opening;
- practical parallel-work limits for adjacent feature branches.

This is a planning document only. It changes no production behavior and claims no Unity
compilation, automated test execution, or manual gameplay acceptance.

## Executive finding

The project already contains most of the difficult reward infrastructure:

- data-driven enemy reward-source profiles;
- deterministic money, scrap, strongbox, and explicit-no-drop outcomes;
- canonical enemy and prop terminal-drop consumers;
- authoritative run-local pickup identity, availability, replay, and collection state;
- reconstructable Unity pickup presentation;
- an ordered Run Session collected-reward journal;
- durable mixed-reward transfer into the selected character;
- money, scrap, equipment-holdings, and unopened-strongbox authorities;
- exactly-once transfer receipts and restart recovery;
- a real strongbox opening and reward-application service.

The principal defect is production composition:

```text
current authored enemy death
  -> canonical enemy terminal transition
  -> room marks the enemy defeated
  -> room-clear and door state update
  -> XP consumer: NoRewardPort
  -> drop consumer: NoRewardPort
  -> kill-stat consumer: NoRewardPort
```

The current authored combat loop therefore does not merely lose pickups after collection. It
does not create production rewards at all.

A second break exists at mission completion:

```text
final authored exit
  -> ProductionPlayableLevelControllerV1.HandleFinalExitReached
  -> direct return to Hub
```

The current route does not end a shared Run Session, durably transfer collected rewards, publish
a normal Results handoff, or save the payout before returning to Hub.

The best next move is to reconnect the current enemy runtime to the retained canonical reward
pipeline and finish one complete vertical path:

```text
kill enemy
  -> generate exact cash, scrap, or strongbox child
  -> display physical pickup
  -> collect into the shared run journal
  -> finish the level
  -> durably transfer to the selected character
  -> show Results
  -> reload and retain the reward
```

## Current production state

### Working and connected

The current authored level already has a sound combat/room-clear path:

- `RoomEnemyActor2D` binds a room-owned GameObject to one factory-created enemy runtime;
- damage reaches the canonical enemy health and death transition;
- enemy identity includes run, room runtime, room, placement, actor, participant, definition,
  level, and lifecycle facts;
- terminal collision is disabled through the enemy-runtime downstream boundary;
- the room authority records defeated occupants;
- room-clear and door synchronization remain authority-owned.

Relevant production paths:

- `Assets/ShooterMover/Runtime/UnityAdapters/Missions/Rooms/RoomEnemyActor2D.cs`
- `Assets/ShooterMover/Runtime/UnityAdapters/Missions/Rooms/RoomEnemySpawner2D.cs`
- `Assets/ShooterMover/Runtime/UnityAdapters/Missions/Rooms/RoomRuntimeComposition2D.cs`
- `Assets/ShooterMover/Runtime/UnityAdapters/Missions/Rooms/RoomPresentationScene2D.cs`

### Authored reward data already exists

The current enemy catalogue contains real `drop_profile` references, including:

- `drop-source.small-enemy`;
- `drop-source.normal-enemy`;
- `drop-source.large-enemy`;
- `drop-source.explicit-no-drop`.

`ProductionRewardSourceCatalogV1` already maps these profiles to money, scrap, strongbox, and
no-drop outcomes. The current profiles are sufficient to prove integration and should not be
replaced by scene-specific random rolls.

Relevant paths:

- `Assets/ShooterMover/Resources/EnemyCatalog/enemy_catalog_v2.json`
- `Assets/ShooterMover/Runtime/Application/Rewards/Drops/ProductionRewardSourceCatalogV1.cs`
- `Assets/ShooterMover/Runtime/Application/Rewards/Drops/ProductionStrongboxTierSelectionCatalogV1.cs`

### Canonical reward generation exists but is not composed

`TerminalDropBindingCompositionV1` already builds canonical enemy and prop terminal consumers.
The enemy consumer adapts an `EnemyDeathFactV1`, runs deterministic personal reward generation,
and admits each accepted generated child into the pending-delivery boundary.

Relevant paths:

- `Assets/ShooterMover/Runtime/TerminalDropBinding/TerminalDropBindingCompositionV1.cs`
- `Assets/ShooterMover/Runtime/TerminalDropBinding/TerminalDropGenerationAuthorityV1.cs`
- `Assets/ShooterMover/Runtime/TerminalDropBinding/TerminalPersonalRewardGenerationAuthorityV1.cs`
- `Assets/ShooterMover/Runtime/Application/Rewards/Drops/PersonalRewardGenerationServiceV1.cs`

The current `RoomEnemySpawner2D.BuildDownstreamPorts()` instead constructs one `NoRewardPort`
and supplies it as the XP, drop, and kill-stat consumer. That is the immediate enemy-drop cut.

### Physical pickup authority exists but is absent from the playable scene

`RunLocalPickupAuthorityV1` already owns exact run-local pickup truth:

- stable pickup and generated-child identity;
- run and lifecycle identity;
- frozen source placement and world position;
- available and collected state;
- collector validation;
- exact replay and conflicting-duplicate handling;
- ordered collection facts recorded through the Run Session.

`RunPickupPresenter2D` is a reconstructable projection. Destroying or rebuilding its Unity
objects does not change authoritative pickup state.

Relevant paths:

- `Assets/ShooterMover/Runtime/RunPickups/RunLocalPickupAuthorityV1.cs`
- `Assets/ShooterMover/Runtime/RunPickups/RunLocalPickupCollectionAuthorityV1.cs`
- `Assets/ShooterMover/Runtime/UnityAdapters/Rewards/RunPickups/RunPickupAuthorityHost2D.cs`
- `Assets/ShooterMover/Runtime/UnityAdapters/Rewards/RunPickups/RunPickupPresenter2D.cs`
- `Assets/ShooterMover/Runtime/UnityAdapters/Rewards/RunPickups/RunPickupCollector2D.cs`
- `Assets/ShooterMover/Runtime/UnityAdapters/Rewards/RunPickups/RunPickupSourcePositionRegistry2D.cs`

The current `PlayableLevel` scene does not compose these components.

### Permanent payout and recovery exist but are bypassed

The selected character graph already owns:

- money;
- scrap;
- exact equipment holdings;
- exact unopened strongboxes;
- strongbox opening state;
- save adapters and character-scoped recovery state.

`DROP-PERSIST-PROOF-001` added a durable collected-run transfer with prepared custody,
exactly-once receipts, mixed money/scrap/equipment/strongbox application, atomic save behavior,
and recovery after reload.

Relevant paths:

- `Assets/ShooterMover/Runtime/Application/Rewards/CollectedRunTransfers/`
- `Assets/ShooterMover/Runtime/Application/Runs/Session/RunSessionCollectedRewardAuthorityV1.cs`
- `Assets/ShooterMover/Runtime/Application/Runs/Session/RunSessionDurableEndV1.cs`
- `Assets/ShooterMover/Runtime/Application/Flow/Production/ProductionCharacterStrongboxCompositionV1.cs`
- `Assets/ShooterMover/UI/ProductionFlow/ProductionCollectedRunRewardResultsOverlay.cs`

The current `ProductionPlayableLevelControllerV1.HandleFinalExitReached()` returns directly to
the Hub and bypasses this boundary.

### Props are one integration stage behind enemies

The generic room renderer creates `RoomPlacedInstance2D` for all authored placements, but only
enemy placements receive a live terminal source and enemy-runtime binding. Current authored
props do not yet have a production placement-to-prop-runtime composition, destruction adapter,
or terminal-drop consumer.

Prop drops should therefore reuse the completed enemy reward/pickup/persistence path rather than
being the first implementation target.

## Ownership invariants for future work

Every step below must preserve these invariants:

1. Enemy and prop runtimes emit immutable terminal facts; they do not mutate wallets or choose
   presentation objects.
2. Reward profiles and deterministic generation remain the only reward-selection authority.
3. `RunLocalPickupAuthorityV1` remains the sole run-local pickup-state authority.
4. Unity pickup objects remain reconstructable views, not reward truth.
5. Collection means only that the exact reward was collected in the exact run.
6. Permanent money, scrap, equipment, and strongbox mutation occurs only through the existing
   reward-application and collected-run transfer authorities.
7. Mission completion is not committed until durable transfer acceptance succeeds.
8. Exact replay must not duplicate a reward; conflicting identity reuse must reject.
9. Uncollected pickups must not enter the permanent character payout.
10. A reward must never leak to another character slot.

# Five-step forward plan

## Step 1 — `RUN-REWARD-COMPOSITION-001`: establish one shared production run-reward spine

### Goal

Create one production composition for the selected character and authored level that owns or
resolves exactly one:

- active `RunSessionAggregateV1`;
- frozen reward environment and participant context;
- terminal-drop binding composition;
- pending-admission delivery bridge;
- run-local pickup authority and source-position registry;
- durable collected-reward transfer boundary.

Replace the scene-authored placeholder reward context with exact run identity, selected
character, participant, level, difficulty, deterministic seed, lifecycle, and frozen player/drop
level inputs.

### Integration rule

Refactor `RoomEnemySpawner2D` to accept typed downstream ports from production composition. For
this task, connect only the drop consumer. XP and kill statistics may remain explicit no-ops until
their own audited task is ready.

### Acceptance evidence

- one enemy death reaches the real enemy terminal-drop consumer;
- exact replay does not generate a second batch;
- wrong run, lifecycle, participant, source profile, or context fails closed;
- room-clear behavior remains unchanged;
- no wallet, holdings, or strongbox state changes at enemy death.

### Why first

XP, drops, kill statistics, Results, and later prop drops all need the same run identity and
composition boundary. Building separate feature-owned Run Sessions would create irreconcilable
ownership debt.

## Step 2 — `ENEMY-DROP-VISIBLE-001`: make cash, scrap, and strongboxes physically collectible

### Goal

Complete the first visible proof inside the authored combat loop:

```text
one enemy death
  -> exact money child
  -> exact scrap child
  -> exact strongbox child
  -> three physical pickups at the frozen death position
  -> accepted collection into the Run Session journal
  -> run HUD totals update
```

Use a canonical test-level reward override or designated proof source so one enemy deterministically
produces all three reward kinds. Do not depend on rare production probabilities for integration
acceptance.

### Presentation rule

Use recognizably different views:

- cash or credits pickup;
- scrap/metal pickup;
- tier-specific strongbox pickup using the existing box presentation direction.

A pickup toast and HUD must read accepted collection facts or the authoritative run projection,
not GameObject destruction or presenter counts.

### Acceptance evidence

- pickups appear at the frozen enemy death position;
- presentation failure leaves the exact pickup available for reconstruction;
- the view disappears only after accepted collection or accepted exact replay;
- repeated trigger callbacks do not duplicate collection;
- leaving a pickup on the floor keeps it out of collected totals.

### Visible payoff

This is the earliest step that makes the game visibly feel like a loot-driven shooter while still
exercising the real reward architecture.

## Step 3 — `RUN-REWARD-PERSIST-001`: finish the level through durable payout and Results

### Goal

Replace direct final-exit-to-Hub behavior with the canonical completion route:

```text
final exit accepted
  -> freeze exact completion command
  -> prepare collected-reward transfer
  -> create mission result
  -> apply money, scrap, and unopened strongboxes
  -> atomically save selected character
  -> publish human-readable Results
  -> allow Hub return
```

### Commit point

The gameplay completion commit point is durable transfer acceptance, not door entry, scene
transition, Results presentation, in-memory wallet mutation, or pickup destruction.

### Acceptance evidence

- mixed cash, scrap, and one unopened strongbox transfer exactly once;
- uncollected pickups are excluded;
- Results retry cannot duplicate payout;
- save/reload retains the same wallet and box totals;
- a prepared transfer recovers exactly once after restart;
- another character slot remains unchanged;
- transfer failure does not report successful mission completion.

### Player-facing result

After this step, the complete core loop is real:

```text
kill -> see loot -> collect -> finish -> receive payout -> reload and keep it
```

## Step 4 — `STRONGBOX-LOOP-VISIBLE-001`: connect acquired boxes to opening, reveal, and inventory

### Goal

Make the collected strongbox visibly useful from the permanent character state:

```text
Hub unopened-box count
  -> select exact box instance
  -> open through StrongboxOpeningServiceV1
  -> tier/opening presentation
  -> reveal exact generated equipment
  -> apply to exact holdings
  -> save
  -> display in inventory/loadout surfaces
```

The box should retain its exact instance identity until opening. The weapon or equipment payload
should be generated through the existing strongbox policy and reward application, not preselected
when the enemy dies.

### Acceptance evidence

- one collected box appears as one unopened permanent instance;
- opening the same operation is exact replay and cannot grant twice;
- opening failure retains recoverable state;
- the revealed item is the same exact item added to holdings;
- save/reload preserves opened/unopened state and the granted equipment.

### Dependency note

This task should consume the final canonical weapon/equipment catalogue. It should not merge a
parallel replacement catalogue into the drop integration branch.

## Step 5 — `DROP-SOURCES-EXPAND-001`: add props, mission rewards, modifiers, and balance tuning

### Goal

Expand the proven path rather than creating new reward routes:

1. bind authored prop placements to the existing generic prop runtime;
2. route immutable prop destruction facts into the existing prop terminal-drop consumer;
3. add hidden treasure and rare-prop profiles;
4. add mission-end cash, scrap, and box rewards through the same durable transfer plan;
5. connect farming/economy modifiers through explicit reward-context inputs;
6. tune production probabilities and pacing with simulator and gameplay evidence;
7. add magnetism, rarity beams, sounds, and richer pickup VFX as projection-only features.

### Acceptance evidence

- enemy, prop, hidden-loot, and mission-end sources converge on one generation, pickup, journal,
  transfer, and persistence model;
- ordinary content additions require definitions/assets/tests rather than shared-controller edits;
- cash, scrap, and box-chance modifiers change only their documented reward classes;
- presentation enhancements cannot change reward quantity, identity, or collection truth.

# Parallel-work plan

## Recommended capacity: three active lanes, one central integration owner

The repository can reasonably support **up to three concurrent feature lanes** when ownership and
file boundaries are explicit:

| Lane | Work type | Concurrency rule |
|---|---|---|
| A — central gameplay integration | Run Session, enemy downstream ports, pickup composition, final exit, Results, durable payout | Exactly one active implementation branch |
| B — isolated presentation/content | pickup sprites/VFX, HUD mock/projection, box art, reward-source definitions, focused content tests | May run in parallel if it does not create authority or edit Lane A composition files |
| C — isolated systems/tooling | level editor/tooling, separate weapon runtime packages, simulations, documentation, non-overlapping data work | May run in parallel after checking dependency and file overlap |

The practical recommendation is therefore:

> **One central integration feature plus two isolated features at the same time.**

More than three active branches is possible mechanically, but the review/rebase cost is likely to
outweigh progress while the playable production composition is still being assembled.

## Features that should not be separate concurrent central branches

The following features share the same production seams and should be coordinated or sequenced:

- enemy drops;
- enemy XP and level-up integration;
- kill statistics and mission Results;
- shared Run Session creation/restart;
- final-exit completion;
- collected-reward persistence;
- prop terminal rewards once props enter the current room runtime.

In particular, XP and drops should not independently rewrite
`RoomEnemySpawner2D.BuildDownstreamPorts()`. Step 1 should first expose one typed downstream-port
composition so later XP and kill-stat work can plug into the same owner.

## Features that can safely proceed beside the drop plan

Subject to exact file inspection, these are good candidates for separate lanes:

- level-grid/editor work that stays under Editor/authoring paths;
- pickup and strongbox art assets;
- presentation-only pickup prefabs and effects;
- additional weapon runtime packages that do not edit reward composition;
- documentation and simulator reports;
- isolated enemy or prop content definitions using already registered mechanics.

## Strongbox and weapon overlap warning

Strongbox opening consumes the canonical weapon/equipment catalogue and touches selected-character
holdings and reward application. A weapon-catalogue branch and a strongbox-opening branch may run
in parallel only when their contracts and owned files are disjoint. Avoid concurrent edits to:

- `ProductionCharacterStrongboxCompositionV1.cs`;
- strongbox hybrid loot catalogue/policy files;
- canonical equipment projection files;
- production character reward-application composition.

## Suggested scheduling

```text
Lane A: Step 1 -> Step 2 -> Step 3
Lane B: pickup/box presentation assets -> Step 4 presentation
Lane C: level tooling or isolated weapon/runtime work

After Step 3:
Lane A: Step 4 or XP integration
Lane B: prop presentation/content
Lane C: simulator/balance work

After Step 4:
Lane A: Step 5 prop/runtime integration
Lane B: farming/economy modifiers
Lane C: VFX, audio, tuning, content expansion
```

# Recommended immediate dispatch

The next implementation PR should be **Step 1**, with a deliberately narrow scope:

> Create one selected-character production Run Session/reward composition, inject a real enemy
> drop consumer into `RoomEnemySpawner2D`, retain XP and kill-stat no-ops explicitly, and prove
> that one canonical enemy death reaches an accepted pending reward admission without mutating
> permanent character state.

Do not combine Step 1 with final strongbox opening animation, prop runtime cutover, XP level-up,
or full balance tuning.

# Failure matrix for the five-step plan

| Condition | Required result |
|---|---|
| Repeated enemy terminal callback | Exact replay; no second generated batch |
| Same operation ID with different fact | Conflicting duplicate; no mutation |
| Enemy moves or is destroyed after terminal fact | Pickup retains frozen death position |
| Pickup presentation cannot instantiate | Authoritative pickup remains available |
| Presenter is rebuilt | One view reconstructs from the same pickup identity |
| Collection recording rejects or throws | Pickup remains available |
| Wrong actor, participant, run, or lifecycle collects | Collection rejects |
| Room traversal occurs | Old-room views retire without deleting authoritative reward state |
| Pickup remains uncollected at completion | Excluded from permanent transfer |
| Results or completion callback repeats | Exact replay; no second payout |
| Save fails before durable commit | Same exact transfer may retry |
| Durable state is uncertain | Preserve custody; do not report normal completion |
| Restart with prepared custody | Recover and apply exactly once |
| Different character is selected | Reward cannot transfer to that character |
| Cleanup fails after durable commit | Committed payout remains committed |
| Strongbox open request repeats | Same receipt/item; no second item grant |
| Prop terminal fact repeats | Same reward operation; no duplicate pickup |

# Validation status

## Static source review

Completed for the main paths named in this document.

## Structural integration review

Completed at source level. The audit identified the current no-op enemy reward port, missing
playable pickup composition, direct final-exit-to-Hub route, and absent live authored-prop reward
binding.

## Not executed

- Unity compilation;
- EditMode tests;
- PlayMode tests;
- automated workflow execution;
- manual gameplay acceptance;
- save/reload gameplay proof;
- performance testing.

The future implementation PRs must report these evidence levels separately and remain draft while
required execution evidence is missing.
