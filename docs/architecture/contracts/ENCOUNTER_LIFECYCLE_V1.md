# Encounter Lifecycle v1

## Status

Encounter Lifecycle v1 is the engine-independent contract boundary among a loaded
encounter runtime, its room projection, enemy/combat facts, authoritative mission
events, and local verification tooling.

It consumes:

- Combat Messages v1 (`VitalMessage`) for terminal combat resolution;
- Mission Messages v1 (`MissionEventEnvelope` and `RoomClearedEvent`) for durable
  completion;
- Room Projection v1 (`RoomProjectionIdentity`) for loaded-room identity.

It does **not** implement AI, spawning, scene loading, rewards, persistence, or
mission state.

## Authority boundary

A loaded encounter may keep runtime-local lifecycle state: start admission,
reinforcement order, retreat state, lockdown state, participant resolution, and
budget samples. None of those facts permanently clear a room.

Durable completion exists only when mission-domain logic emits a matching
`MissionEventEnvelope` whose payload is `RoomClearedEvent`. The encounter
completion wrapper validates that the mission run, room, and encounter IDs match
the loaded encounter.

```text
Room projection + encounter definition
        |
        v
EncounterRuntimeIdentity
        |
        v
Ready --Start--> Active --BeginRetreat--> Retreating
                     |                         |
                     +-- Reinforcement         +-- Withdrawal
                     +-- Lockdown              +-- Combat resolution
                     +-- Withdrawal            |
                     +-- Combat resolution     |
                     +------------ all participants resolved
                                      |
                                      v
                      matching Mission RoomCleared event
                                      |
                                      v
                                  Completed
```

`Completed` is an immutable runtime view of a durable mission event. It is not a
second source of mission truth.

## Identity

`EncounterRuntimeIdentity` contains four explicit identities:

| Field | Meaning |
|---|---|
| `EncounterId` | Durable encounter/content identity |
| `RuntimeId` | One loaded runtime instance |
| `RunId` | Authoritative mission run |
| `Room` | One loaded `RoomProjectionIdentity` |

Two runtime instances of the same encounter remain distinct. A reload can create
a new runtime ID without changing the durable encounter ID.

## Generic participant entries

`EncounterParticipantEntry` carries:

- entry ID;
- actor/runtime ID;
- role ID;
- explicit zero-based order.

Entry collections are copied, sorted by explicit order, and must be contiguous
from zero with unique entry and actor IDs. The contract does not contain an enemy
subtype switch.

The same entry envelope maps every amended Stage 1 enemy role:

| Stage 1 role | Representative role ID |
|---|---|
| Pursuer Drone | `enemy.pursuer-drone` |
| Ram Droid | `enemy.ram-droid` |
| Mobile Blaster Droid | `enemy.mobile-blaster-droid` |
| Blaster Turret | `enemy.blaster-turret` |
| Four-Blaster Elite | `enemy.four-blaster-elite` |

Benchmark-arena and short-route encounters use the same start, reinforcement,
retreat, lockdown, withdrawal, combat-resolution, and completion messages. They
do not receive route-specific or elite-specific envelopes.

## Start

`EncounterStartMessage` contains the runtime identity, one message ID, the
performance budget, and the ordered initial entries.

Rules:

1. The first matching start moves `Ready` to `Active`.
2. An exact retry is `NoChange`.
3. A different start after admission is rejected as `AlreadyStarted`.
4. Initial entries must fit the concurrent-participant budget.
5. A start targeting another runtime identity is rejected.

Repeat-start behavior is therefore deterministic without treating a retry as a
second spawn request.

## Reinforcements

`EncounterReinforcementMessage` contains a zero-based reinforcement index and an
ordered entry batch.

Lifecycle admission is strict:

1. the next index equals the count of already accepted reinforcement batches;
2. an exact accepted batch retry is `NoChange`;
3. conflicting reuse of an accepted index is `ReinforcementConflict`;
4. skipping an index is `ReinforcementOutOfOrder`;
5. actor IDs cannot repeat an initial or reinforcement participant;
6. the batch and resulting active participant count must fit the start budget;
7. no new reinforcement is admitted after retreat begins or completion occurs.

An actor added by a reinforcement batch may withdraw or resolve through Combat
Messages v1 immediately after that batch is admitted. Leaving during a
reinforcement sequence requires no special message.

## Retreat, lockdown, and withdrawal

`EncounterRetreatMessage` declares one encounter-wide retreat transition with a
source and explicit reason. It does not contain AI decisions.

`EncounterLockdownMessage` explicitly engages or releases lockdown. Exact
reapplication of the current state is `NoChange`.

An engaged lockdown:

- rejects a new retreat transition;
- rejects withdrawal facts that claim an actor left through the encounter
  boundary;
- does not suppress terminal combat facts.

After lockdown is released, retreat and withdrawal may proceed. A repeated
identical withdrawal is `NoChange`; a different resolution for an already
resolved actor is rejected.

`EncounterWithdrawalMessage` supports retreat, route exit, and runtime-unload
reasons. Withdrawal is runtime-local resolution and does not clear the room.

## Combat resolution

`EncounterCombatResolutionMessage` wraps the accepted Combat Messages v1
`VitalMessage`. It accepts only a terminal `VitalResult.Destroyed` fact with a
destroyed `VitalState`.

This avoids a second encounter-specific damage or death DTO. The combat event ID,
source, target, channel, and vital state remain the Combat v1 fact.

## Completion once

`EncounterCompletionMessage` requires a Mission Messages v1
`MissionEventEnvelope` with `RoomClearedEvent`.

Construction fails unless:

- event run ID equals encounter run ID;
- event room ID equals the encounter room ID;
- event encounter ID equals the encounter ID.

Lifecycle completion is admitted only after every known initial and reinforcement
participant has either withdrawn or been destroyed.

The first matching completion applies. An exact retry is `NoChange`. A different
completion after completion is rejected as `AlreadyCompleted`. This gives
verification tooling an explicit completion-once rule while leaving durable
authority in Mission Messages v1.

## Performance budget messages

`EncounterPerformanceBudget` declares four verification limits:

| Budget | Meaning |
|---|---|
| `MaximumConcurrentParticipants` | Maximum unresolved participants admitted at once |
| `MaximumPendingReinforcementEntries` | Maximum entries in one reinforcement batch |
| `MaximumCombatMessagesPerTick` | Verification limit for combat message volume |
| `MaximumFrameTimeMilliseconds` | Verification frame-time ceiling |

`EncounterBudgetSample` records one immutable observation for one encounter
runtime. `EncounterBudgetEvaluation.Evaluate` returns violations in this fixed
order:

1. concurrent participants;
2. pending reinforcement entries;
3. combat messages per tick;
4. frame time.

Budgets and samples are finite, non-negative or positive as appropriate, and
deterministic. They are evidence inputs only: a budget violation does not change
difficulty, damage, rewards, AI, or mission state.

## Rejection and idempotence summary

| Operation | Exact repeat | Conflicting/out-of-order case |
|---|---|---|
| Start | `NoChange` | `AlreadyStarted` |
| Reinforcement | `NoChange` | conflict or out-of-order |
| Retreat | `NoChange` | already started or lockdown |
| Lockdown | `NoChange` when state already matches | completed/not started |
| Withdrawal | `NoChange` | unknown/already-resolved actor or lockdown |
| Combat resolution | `NoChange` | unknown/already-resolved actor |
| Completion | `NoChange` | participants remain or already completed |

Every rejection preserves the same lifecycle object.

## Verification fixtures

The focused EditMode fixture is:

```text
ShooterMover.Tests.EditMode.Contracts.EncounterContractTests
```

It covers:

- deterministic repeat start;
- entry and reinforcement ordering;
- conflicting and skipped reinforcement indices;
- retreat admission;
- lockdown engagement and release;
- withdrawal, including an actor added by reinforcement;
- Combat v1 terminal resolution;
- matching Mission v1 durable completion;
- completion exactly once;
- participant-remains rejection;
- budget validation, evaluation, and reinforcement-budget rejection;
- generic mapping of the four ordinary Stage 1 roles and Four-Blaster Elite;
- getter-only immutable, Unity-free contract shapes.

Manual/playable review should trace one benchmark encounter and one route
encounter with the same envelopes. Reviewers should map Pursuer Drone, Ram Droid,
Mobile Blaster Droid, Blaster Turret, and Four-Blaster Elite through role IDs,
then confirm that only the Mission v1 room-cleared event becomes durable.

## Non-goals

Encounter Lifecycle v1 adds no:

- AI, tactics, pathfinding, or spawn implementation;
- scene, prefab, or content package;
- reward, checkpoint, route, objective, or completion authority;
- save, snapshot, journal, registry, or migration format;
- network transport;
- damage, projectile, or enemy implementation;
- special envelope for an elite, ordinary role, arena, or route.
