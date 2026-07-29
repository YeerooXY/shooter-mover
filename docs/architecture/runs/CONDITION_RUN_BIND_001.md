# CONDITION-RUN-BIND-001 — Authoritative Run Session condition composition

## Launch and dependency proof

- Task: `CONDITION-RUN-BIND-001`
- Branch: `agent/condition-run-bind-001-runtime-composition`
- Exact launch `main` SHA: `bcb6cae1291a8ed196b053dbadf26a9332e0250c`
- `CONDITION-LIVE-001` / PR #269: merged into the launch SHA.
- `RUN-SESSION-001` / PR #270: merged before the launch SHA.
- No merged or open competing `CONDITION-RUN-BIND-001` implementation was found before branch creation.

## Ownership diagram

```text
selected account-backed character
        |
        v
ConditionBoundRunSessionStartSource
        |
        v
FrozenCharacterRunInputs  (immutable permanent inputs)
        |
        v
RunSessionAggregate  ---- authoritative run ID / generation / tick / terminal state
        |
        +---- player runtime port
        +---- inventory-backed weapon port
        +---- ExistingConditionLiveRunPort
        |         |
        |         +---- ConditionLiveState
        |                 +---- participant FactWindowConditionState
        |                 +---- participant StatusEffectState
        |                 +---- FactWindowStatusEffectBridge
        |
        +---- ConditionOwnedStatusEffectRunPort
        |         (projection over the same condition-owned effect authorities)
        +---- ability placeholder
        +---- room lifecycle port
        +---- mission-result port
```

There is one active temporary-effect owner per participant: the `StatusEffectState` constructed inside `ConditionLiveState`. The Run Session status-effect port is a projection/lifecycle participant over that same state and never constructs another status-effect authority.

`ConditionBoundRunSessionStartSource` is the canonical account-backed production entry point. It delegates permanent character freezing to the existing production source, composes the real condition-bound runtime factory, and fails closed if the condition port is not authoritative or the status-effect projection does not reference that exact condition runtime.

## Gameplay-fact flow

```text
immutable EnemyDeathFact
        |
        v
RunSessionAggregate.DeliverConditionGameplayFact
  - exact run admission
  - current lifecycle admission
  - terminal-run admission
        |
        v
ExistingConditionLiveRunPort
        |
        v
ConditionLiveState.Ingest
  - adapter lookup
  - source-fact canonical fingerprint
  - delivery replay / conflict validation
  - EnemyDeathConditionFactBridge
  - participant fact-window evaluation
  - condition-to-effect bridge
  - condition-owned StatusEffectState
```

The Run Session does not canonicalize `EnemyDeathFact`, duplicate the enemy-death adapter, count kills, or inspect skill names. Unattributed death facts fail closed in the merged adapter.

## Run clock and lifecycle

`OwningRunClock` and `OwningRunLifecycle` are concrete typed adapters used by the condition authority. Once the runtime is bound, both project the owning `RunSessionAggregate` directly. A temporary call-scoped projection is used only while one admitted fact or restart is synchronously crossing the aggregate boundary; it is cleared when the aggregate commits the accepted tick/generation and cannot become an independently advancing clock.

- Condition delivery may move the authoritative run tick forward only after an accepted/exact-replay downstream result.
- Condition advance rejects aggregate tick regression before condition mutation.
- The merged condition runtime remains authoritative for advance replay and participant-wide advance prevalidation.
- Run ID and lifecycle generation must match at the aggregate and condition boundaries.

## Participant identity mapping

`IRunConditionParticipantSeedProvider` returns immutable participant seeds containing:

- participant ID;
- character ID;
- actor/entity ID;
- actor lifecycle generation;
- persistent skill-allocation fingerprint.

The default provider maps the exact selected player runtime snapshot and frozen selected character. Multiplayer or companion composition may provide multiple immutable seeds without changing condition logic.

`IRunConditionDefinitionProvider` is the narrow content projection boundary for class, skill, and event-authored condition/status definitions. It owns no mutable run state. The reference kill-burst fixture used by tests is ordinary `FactWindowEffectFixture` data.

## Restart sequence

1. `RunSessionAggregate` validates run identity, current generation, replacement generation and tick.
2. Every lifecycle port prevalidates before any port mutates.
3. The condition port resolves every replacement participant and definition into one complete `ConditionRunDefinition` and retains that exact immutable graph for commit.
4. Existing player/weapon ports restart according to their established order.
5. The status-effect projection performs no independent mutation and confirms the same prevalidated condition transition.
6. The condition port projects the pending replacement lifecycle to the merged lifecycle adapter and calls `ConditionLiveState.Reconstruct` with the exact graph prepared during prevalidation; it does not resolve content again after other ports have moved.
7. Reconstruction rebuilds participant windows/effect authorities and clears retired delivery and advance replay state.
8. The aggregate commits the replacement generation and tick exactly once.
9. Frozen permanent inputs and ranked-skill allocation remain unchanged.

A failed definition/participant prevalidation leaves every lifecycle port and all condition/effect state untouched. The rejected operation remains deterministically replayable, and a new operation can retry after the underlying definition source becomes valid.

## Modifier projection boundary

`RunSessionAggregate.ExportConditionModifierProjection(participantId)` returns the merged `LiveModifierSnapshot` emitted by that participant's condition-owned status-effect authority. Combat/stat execution can consume this as a run-local overlay. `RunCombatProfile` remains frozen and is never rebuilt or mutated by active conditions.

## Snapshot and checkpoint projection

`RunConditionLiveSnapshot` exposes immutable:

- run ID and condition lifecycle generation;
- authoritative condition tick;
- condition-definition fingerprint;
- participant identity and actor generation;
- latest accepted condition tick;
- active condition IDs;
- active effect count and status-effect fingerprint;
- participant `LiveModifierSnapshot` projection;
- accepted source-fact count;
- complete condition runtime fingerprint.

The normal Run Session debug/recovery/checkpoint snapshot records every lifecycle-port fingerprint, so the real condition port fingerprint is present in the standard transient diagnostics. `RunSessionAggregate.ExportConditionCheckpoint()` additionally combines that ordinary `RunCheckpoint` with the full immutable condition/effect/modifier projection in `RunConditionCheckpoint`, validating exact run and lifecycle identity. No durable checkpoint persistence is introduced, and neither checkpoint form is permanent character truth.

## Replay and fingerprint boundaries

- Run Session owns admission by run, lifecycle, terminal state and authoritative advance tick.
- Condition Runtime owns source canonicalization, source fingerprint conflict detection, delivery replay, accepted-source replay, fact-window replay, effect replay, advance replay and reconstruction replay.
- The outer delivery command fingerprint intentionally excludes source object contents so it cannot become a second source-fact canonicalizer.
- The adapter retains only an immutable presentation record to label exact advance replay; the merged authority still decides whether changed operation facts conflict.

## Permanent versus transient state

Permanent and frozen for one run:

- selected character identity/revision/fingerprint;
- class, loadout and concrete equipment-instance identities;
- ranked skill allocation;
- derived character stats and frozen run combat profile;
- event/modifier context fingerprint.

Transient and reconstructed on restart:

- accepted condition facts;
- fact windows and active condition IDs;
- temporary status-effect stacks;
- modifier overlays;
- condition delivery/advance replay state;
- latest accepted condition/effect ticks.

No active window or temporary effect is written to account/character saves.

## Changed-file audit

Intended production changes are limited to:

- Run Session condition contracts, aggregate admission methods, and condition checkpoint projection;
- one `partial` declaration and constructor bind in `RunSessionAggregate`;
- the condition/run integration assembly and canonical account-backed start source;
- focused engine-neutral EditMode and production-composition tests;
- this architecture document and Unity metadata.

## Duplicate-authority proof

This task creates no second:

- run authority — `RunSessionAggregate` remains authoritative;
- condition authority — one merged `ConditionLiveState` is wrapped;
- status-effect authority — active effects remain condition-owned;
- modifier system — existing `LiveModifierSnapshot` is projected;
- skill runtime — definitions come through an immutable provider;
- enemy-death authority — `EnemyDeathFact` and its merged adapter are reused;
- save model — all new state is transient.

## Excluded downstream tasks

Unchanged and explicitly excluded:

- enemy attack-pattern definitions/execution/tests;
- enemy content authoring;
- Box/strongbox persistence and reward application;
- active ability implementation;
- Room JSON live cutover;
- Stage 1 controller retirement;
- final HUD/VFX;
- permanent save adapters;
- enemy health/death validation;
- player health, Combat Hit Policy, critical-hit and mission-result authorities.

## Validation commands

Expected Unity version from the merged condition task: `6000.3.19f1`.

```bash
Unity -batchmode -nographics -quit \
  -projectPath . \
  -runTests -testPlatform EditMode \
  -assemblyNames ShooterMover.Tests.EditMode.RunConditionBinding \
  -testResults artifacts/condition-run-bind-001-editmode.xml \
  -logFile artifacts/condition-run-bind-001-editmode.log

Unity -batchmode -nographics -quit \
  -projectPath . \
  -runTests -testPlatform EditMode \
  -assemblyNames ShooterMover.Tests.EditMode.ConditionRuntime,ShooterMover.Tests.EditMode.RunSessions \
  -testResults artifacts/condition-run-bind-001-regression-editmode.xml \
  -logFile artifacts/condition-run-bind-001-regression-editmode.log
```

The draft PR remains non-merge-ready until Unity compilation, focused tests, applicable integration coverage, and zero-failure XML are available.
