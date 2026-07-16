# PLAYER EXPERIENCE V1

## Scope

XP-001 owns the engine-independent player experience authority under
`Progression/Experience`. It does not mutate enemies, destructible props, scenes,
wallets, inventories, prefabs, or serialized Stage 1 integration.

The permanent authority identity is `authority.player-experience`.

## Level and skill-point model

- Fresh state is player level 1 with cumulative XP 0.
- Player levels are limited to the closed interval 1 through 100.
- Total awarded skill points always equal the current player level.
- Therefore a fresh level-1 player starts with one awarded skill point.
- Every crossed level boundary emits one ordered `PlayerLevelUpFactV1` with
  `SkillPointsGranted = 1`.
- Spending or refunding skill points is outside XP-001; this authority records only
  the total awarded by player-level progression.

## Deterministic XP curve

`PlayerExperienceCurveV1` consumes the accepted shared
`SoftActivationCurveParameters` and `ProgressionCurveMath.EvaluateSoftActivation`
infrastructure. Authored inputs are:

- minimum XP required for one level;
- maximum XP required for one level;
- the nominal level at which the full-cost portion of the curve is reached;
- shared early-tail and post-nominal shape parameters.

The normalized shared curve is mapped to a positive integer XP cost using
`MidpointRounding.AwayFromZero`. All 99 level costs and cumulative thresholds are
precomputed with checked `Int64` arithmetic. Curve construction rejects any
configuration whose cumulative level-100 threshold overflows.

The canonical curve fingerprint includes every authored input and every resulting
level cost. Snapshot import requires an exact curve-fingerprint match.

## XP state and level cap

`PlayerExperienceStateV1` exposes:

- `CumulativeExperience`: every accepted XP unit, including over-cap XP;
- `ProgressionExperience`: cumulative XP clamped to the level-100 threshold;
- `OverflowExperience`: cumulative XP beyond that threshold;
- current player level;
- XP already earned inside the current level;
- total XP required for the next level;
- remaining XP to the next level;
- total awarded skill points.

At level 100, XP-to-next-level values are zero. Further valid grants remain
exactly-once transactions and increase `CumulativeExperience` and
`OverflowExperience`; they never create level 101 or additional skill points.

## ProgressionContext integration

The authority implements `IProgressionContextProvider`. Fresh construction and
every successful XP grant project the derived player level into the existing
immutable `ProgressionContext`, preserving region level, difficulty identity,
difficulty value, and canonical progression tags.

XP-001 does not change the shared context contract or impose its 1–100 cap on
other systems that use `ProgressionContext`.

## Exactly-once grants

A grant is keyed by its permanent `SourceOperationStableId`.

The command fingerprint contains:

- schema identity;
- source-operation identity;
- positive XP amount.

Outcomes are:

- `Applied`: the source identity was unseen and XP was committed;
- `DuplicateNoChange`: the same source identity and command fingerprint were
  replayed;
- `ConflictingDuplicate`: the source identity was reused with different content;
- explicit invalid-input or arithmetic-overflow outcomes.

Only `Applied` increments sequence, changes XP, updates `ProgressionContext`, or
returns level-up facts. Exact and conflicting duplicates produce no additional XP.

## Snapshot export and import

`PlayerExperienceSnapshotV1` contains:

- schema version and authority identity;
- monotonic applied-grant sequence;
- curve fingerprint;
- cumulative XP;
- the current immutable `ProgressionContext`;
- canonical source-operation grant records;
- a SHA-256 snapshot fingerprint.

Import validates the complete snapshot before mutation:

- supported schema, authority, and exact curve fingerprint;
- snapshot fingerprint;
- nonnegative sequence and cumulative XP;
- context level matches the XP-derived level;
- one unique source operation per record;
- positive grant amounts;
- command fingerprints;
- unique contiguous applied sequences;
- record count equals sequence;
- checked record sum equals cumulative XP.

A failed import is atomic and leaves existing state unchanged. Importing the
currently loaded snapshot is an explicit `DuplicateNoChange`. Successfully
imported source records retain replay protection after restart.

## Level-up fact consumption

The returned `PlayerExperienceGrantFactV1.LevelUpFacts` collection is the
authoritative event batch for a grant. Consumers may translate those immutable
facts into presentation or analytics events after a successful grant. They must
not infer level-ups by polling or replay level-up side effects for duplicate
grants.

## Validation

Focused EditMode coverage verifies:

- fresh level-1 and skill-point state;
- deterministic configurable curve behavior;
- exact duplicate and conflicting duplicate grants;
- cumulative thresholds and multi-level grants;
- level-100 cap and explicit overflow XP;
- snapshot round trip and retained replay protection;
- fingerprint, semantic, and curve-mismatch import rejection;
- invalid grant and curve inputs;
- cumulative `Int64` overflow behavior;
- absence of `UnityEngine` dependencies.

## Rollback

Before merge, close the draft PR and delete
`agent/xp-001-player-experience`.

After merge, revert the XP-001 commits. No existing progression context, shared
curve implementation, scene, prefab, production balance, wallet, inventory, or
reward source requires migration to remove this authority.
