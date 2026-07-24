# WEAPON-FIRING-001 — Canonical deterministic firing scheduler

## Purpose

`WeaponFiringScheduler` is the canonical engine-independent authority that decides when an immutable `EffectiveWeapon` fires. It owns trigger transitions, cadence, catch-up, deterministic shot sequencing, accepted schedules, and bounded replay handling. It does not execute projectiles, effects, guidance, impacts, Unity objects, or production composition.

## Pure transition boundary

```csharp
WeaponFiringDecision Schedule(
    WeaponFiringRequest request,
    WeaponFiringSessionState previousState)
```

The scheduler retains immutable configuration only. Gameplay and replay state remain caller-owned immutable snapshots.

## Unified bounded operation replay

`WeaponFiringReplayRecord` is the single immutable replay authority for successful firing operations. It is discriminated as either:

- an accepted schedule containing one or more real authoritative emissions; or
- a successful state-changing transition containing no schedule.

A transition-only receipt binds actor, participant, equipment instance, weapon definition, lifecycle generation, source `FireOperationId`, complete request fingerprint, effective weapon fingerprint, original successful status, stable result code, source simulation tick, resulting track fingerprint, and authoritative `OperationSequence`. It has canonical text and its own deterministic fingerprint.

An exact retained retry returns the original result without mutating state again. Accepted retries return the retained schedule; transition-only retries return the original transition status with no schedule. Reusing a retained operation ID with altered request content returns `ConflictingDuplicate` and does not mutate state.

`WeaponFiringDecision.Kind` explicitly distinguishes newly accepted emissions, replayed emissions, newly successful transition-only decisions, replayed transition-only decisions, and rejection. Callers do not need to infer replay semantics from nullable schedules or diagnostic strings.

No empty schedule, fake emission, zero-damage projectile, or dummy cadence shot is created for transition replay.

## Authoritative operation order

Each firing track carries a monotonic `NextOperationSequence`. Every newly successful operation—accepted emission or transition-only—receives the current sequence and advances the track exactly once. Rejections, exact replays, conflicting duplicates, invalid requests, and expired requests do not advance it.

Replay pruning is ordered only by `OperationSequence`. Simulation tick and lexical `FireOperationId` ordering do not determine age. Therefore, with a capacity of two, operations processed as `m`, `z`, then `a` at the same tick retain `z` and `a`, regardless of lexical ID order.

`FirstRetainedOperationSequence` identifies the retained operation boundary. Retained receipts must have unique, strictly increasing, contiguous operation sequences from that boundary through `NextOperationSequence - 1`.

## Shot-sequence separation

Operation sequence and shot sequence are independent authorities:

- every newly successful operation advances operation sequence;
- only real accepted emissions advance `NextGlobalShotSequence`;
- pruning transition-only receipts never advances `FirstRetainedShotSequence`;
- pruning an accepted schedule advances `FirstRetainedShotSequence` through that schedule's last shot;
- retained accepted schedules remain exactly contiguous from `FirstRetainedShotSequence` to `NextGlobalShotSequence`.

## Bounded replay history and expiry

Replay retention is bounded per active firing track. Each track carries:

- `FirstRetainedOperationSequence`;
- `NextOperationSequence`;
- `FirstRetainedShotSequence`;
- `NextGlobalShotSequence`;
- `ReplayRetentionFloor`;
- `CumulativeHistoryFingerprint`.

Ordinary release, waiting, held, no-emission, and accepted transitions preserve retention metadata through `WeaponFiringTrackState.WithTransition(...)`. Only `WeaponFiringSessionState.WithTransition(...)` may advance retention metadata while pruning records.

Pruning starts from the existing `CumulativeHistoryFingerprint` and folds each newly pruned operation receipt into that chain. Operation pruning advances `FirstRetainedOperationSequence`; only accepted-schedule pruning advances `FirstRetainedShotSequence`.

The public request contract does not carry a previously assigned operation sequence, so after a receipt has been pruned the scheduler cannot distinguish an old duplicate from a genuinely new operation with the same simulation tick. The deterministic conservative policy remains tick-based: pruning an operation raises `ReplayRetentionFloor` beyond its source tick. Retained operations are looked up before this expiry check, so the newest retained same-tick operation always replays correctly; unknown operations at a pruned tick are conservatively rejected as `ReplayExpired` rather than risk duplicate execution.

## State and restore validation

`WeaponFiringSessionState.HasValidFingerprint()` and `TryRestore(...)` validate:

- unique replay keys;
- valid receipt and track fingerprints;
- per-track operation-sequence uniqueness, ordering and contiguity;
- consistency with `FirstRetainedOperationSequence` and `NextOperationSequence`;
- accepted-schedule shot continuity;
- transition-only receipts having no effect on shot continuity;
- cumulative pruned-history presence after operation or shot pruning.

Restore remains limited to already constructed in-memory object graphs for rollback, branching, and reconciliation. This PR does not expose process-boundary reconstruction from plain serialized save data.

## Session lifetime boundary

Per-track replay retention does not bound the number of tracks. The owning runtime/session authority must:

- discard the complete firing session state when the run ends;
- stop retaining obsolete lifecycle-generation tracks after permanent despawn, respawn replacement, reconnect replacement, or permanent unequip;
- avoid carrying one firing session snapshot across an unbounded number of lifecycle generations.

A dedicated deterministic lifecycle-retirement API is still a follow-up. PR #306 must establish the production run/session ownership boundary before this state is used as an indefinitely retained runtime authority.

## Validation policy

No automated tests are added under the current prototype policy. Compilation and focused manual validation must be reported separately based on the environment actually available.
