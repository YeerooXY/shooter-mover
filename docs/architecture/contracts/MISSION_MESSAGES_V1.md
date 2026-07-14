# Mission Messages v1

## Status and scope

Mission Messages v1 defines the immutable, engine-independent protocol boundary
between application requests and future mission-domain state transitions.

It consumes:

- `StableId` v1 for command, event, run, room, encounter, checkpoint, bank,
  transaction, mission and completion identities;
- Identity v1 `ContentVersion` through `MissionPayloadVersion`.

This contract does **not** implement `MissionRunState`, snapshots, journals,
persistence, scene references, network transport, or mission rules. Passing the
protocol gate means only that an envelope is known, version-compatible and in
sequence. Domain code still decides whether the requested transition is valid.

## Immutable envelope model

Envelope, version, sequence, evaluation, rejection and concrete payload types
are sealed and expose getter-only state. The two public payload roots are
abstract with internal constructors, so consumers can read the typed hierarchy
but cannot inject mutable external implementations. The gate is static. The
entire namespace is Unity-free.

### MissionCommandEnvelope

Every command carries:

1. `CommandId` — idempotency identity for one logical request;
2. `RunId` — the mission run the request addresses;
3. `PayloadVersion` — mission contract version plus Identity v1 content version;
4. `ExpectedSequence` — the current committed mission sequence observed by the
   caller;
5. an explicitly typed command payload.

A command envelope requests consideration. It is not proof that a room is
clear, a checkpoint is available, rewards are bankable, or completion is
allowed.

### MissionEventEnvelope

Every event carries:

1. `EventId` — identity of the accepted fact;
2. `CommandId` — the causing request;
3. `RunId`;
4. `PayloadVersion`;
5. a positive committed `Sequence`;
6. an explicitly typed event payload.

Event sequence zero is reserved for the initial state before any committed
mission event. The first committed event uses sequence one.

### MissionRejectionEnvelope

A rejection copies the request identity, run identity, payload version,
expected sequence and command type, then adds:

- the current sequence used for admission;
- one explicit `MissionRejectionType`.

Rejections describe protocol admission only. Future mission-domain rejection
messages may be added by a versioned extension; they must not overload these
protocol reasons.

## Sequence semantics

`MissionSequence` is a non-negative signed 64-bit value.

- A command is **current** when `ExpectedSequence == current sequence`.
- It is **stale** when `ExpectedSequence < current sequence`.
- It is **future** when `ExpectedSequence > current sequence`.
- An accepted event advances the committed position by exactly one.

Consumers order committed events by `MissionEventEnvelope.Sequence`. They do
not infer durable order from Unity frame time, wall-clock time, file order, or
scene load order.

## Payload version semantics

`MissionPayloadVersion` contains:

```text
mission_contract_version=<positive integer>
content_catalog_version=<Identity v1 catalog version>
content_definition_fingerprint=<Identity v1 canonical sha256 fingerprint>
```

The complete value participates in equality and deterministic hashing. A
consumer admits only the exact supported value. It must not interpret a command
under a different mission schema or content definition fingerprint.

## Deterministic admission and rejection order

`MissionCommandGate` applies protocol checks in this exact order:

1. command-ID duplicate check;
2. supported payload version;
3. known command type;
4. expected sequence relation;
5. protocol acceptance.

The ordering is contractual.

### Duplicate and timeout retry behavior

When the caller times out after submission, it retries the exact same envelope
with the same `CommandId`.

- An exact previously observed envelope returns `DuplicateCommand`.
- Reusing the same `CommandId` with any changed run, version, expected sequence,
  type, or payload returns `ConflictingDuplicateCommand`.

Duplicate identity is checked before sequence. Therefore, if the original
command was accepted and advanced the run from sequence four to five, retrying
that exact command still returns `DuplicateCommand`; it is not reclassified as
`StaleSequence`.

The gate does not recreate a prior event or persistence result. The future
application/domain owner decides how a duplicate caller retrieves the already
recorded outcome.

### Version, type and sequence behavior

After duplicate checks:

- a mismatched `MissionPayloadVersion` returns
  `UnsupportedPayloadVersion` without interpreting the payload;
- an explicit `UnknownMissionCommandPayload` returns `UnknownCommandType`;
- a lower expected sequence returns `StaleSequence`;
- a higher expected sequence returns `FutureSequence`.

The same inputs always create equal rejection envelopes with the same canonical
field order and deterministic hash.

## Command and event types in v1

| Request payload | Command type | Accepted event payload | Event type |
|---|---|---|---|
| `RoomClearRequest` | `RoomClear` | `RoomClearedEvent` | `RoomCleared` |
| `CheckpointActivationRequest` | `CheckpointActivation` | `CheckpointActivatedEvent` | `CheckpointActivated` |
| `RewardBankingRequest` | `RewardBanking` | `RewardsBankedEvent` | `RewardsBanked` |
| `MissionCompletionRequest` | `MissionCompletion` | `MissionCompletedEvent` | `MissionCompleted` |

These pairs define vocabulary, not transition rules. A command does not emit an
event automatically. Future mission-domain logic validates state and constructs
accepted events.

## Representative request traces

The examples use illustrative StableIds and omit construction syntax that does
not affect the protocol.

### Room-clear request

```text
command_id=command.clear-room-0001
run_id=run.factory-run-0001
expected_sequence=4
command_type=room-clear
payload:
room_id=room.factory-receiving
encounter_id=encounter.receiving-wave
```

The request reports that an encounter lane wants the mission domain to consider
the room clear. It does not decide durable cleared state.

### Checkpoint activation request

```text
command_id=command.activate-checkpoint-0001
run_id=run.factory-run-0001
expected_sequence=5
command_type=checkpoint-activation
payload:
checkpoint_id=checkpoint.teleport-a
room_id=room.factory-teleport-a
```

The mission domain later decides whether the checkpoint exists, is reachable,
and may become active.

### Reward banking request

```text
command_id=command.bank-rewards-0001
run_id=run.factory-run-0001
expected_sequence=6
command_type=reward-banking
payload:
bank_id=bank.secure-storage-a
```

Banking remains separate from teleport activation. The request does not supply
or mutate reward balances. Future mission/reward state determines the exact
provisional commitments and emits a `RewardsBankedEvent` with a stable
transaction identity only after the transition is accepted.

### Mission completion request

```text
command_id=command.complete-mission-0001
run_id=run.factory-run-0001
expected_sequence=7
command_type=mission-completion
payload:
mission_id=mission.factory-shutdown
```

The request asks for completion validation. It does not decide that objectives,
boss state, production shutdown, reward resolution, or return-to-menu behavior
are complete.

## Rejection types

| Rejection | Meaning |
|---|---|
| `DuplicateCommand` | The same command ID and exactly equal envelope were already observed. |
| `ConflictingDuplicateCommand` | The command ID was reused for a different envelope. |
| `UnsupportedPayloadVersion` | Mission schema or Identity v1 content identity is unsupported. |
| `UnknownCommandType` | The command payload type is not understood by v1. |
| `StaleSequence` | The caller expected an earlier committed position. |
| `FutureSequence` | The caller expected a position not yet committed. |

No free-form error precedence exists. Callers branch on the enum and may use the
canonical envelope for diagnostics or deterministic fixtures.

## Durability boundary

Architecture may later journal checkpoint activation, banking, unique rewards,
route/objective changes, completion, and suspend/resume boundaries when their
own tasks define persistence. Mission Messages v1 does not create that journal.

Ordinary movement, shots, damage ticks, enemy actions, Unity frames and
presentation state are not mission command/event journal entries merely because
these envelopes exist.

## Explicit non-goals

Mission Messages v1 does not add:

- `MissionRunState` or another authoritative mission model;
- snapshots, journal entries, replay, migration or recovery;
- reward balances, inventory, checkpoint rules or completion rules;
- scene, GameObject, MonoBehaviour or ScriptableObject references;
- clocks, frame/tick counters or ordinary tick journaling;
- persistence acknowledgements;
- network serialization, transport, retry timers or remote services;
- registries, generators or mutable universal message objects.
