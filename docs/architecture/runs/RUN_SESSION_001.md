# RUN-SESSION-001 — Permanent character to transient mission runtime

## Launch and dependency proof

- Launch branch: `agent/run-session-001-character-to-mission`
- Exact launch `main` SHA: `aa6dd3ceb228588a7303e2cf01304c5404acc943`
- `CHARACTER-COMPOSITION-001` / PR #263: merged into `main` before launch.
- `DERIVED-STATS-001` / PR #255: merged into `main` before launch.
- No existing PR titled `RUN-SESSION-001` was present before implementation.
- Existing player authority, inventory-backed weapon execution, combat-hit policy,
  status-effect authority, mission-result authority, strongbox collection verification,
  and account/character save adapters were inspected on the launch SHA and are reused.

## Ownership diagram

```text
PlayerAccountSnapshotV1 / selected CharacterInstanceSnapshotV1
        │ permanent truth; selected by CharacterCompositionCoordinatorV1
        ▼
ProductionCharacterRuntimeGraphV1
        │ exports accepted immutable snapshots only
        ▼
ProductionCharacterRunSessionStartSourceV1
        │ freezes exact character revision/fingerprint, loadout, holdings,
        │ skill allocation, equipment instances/definitions and derived inputs
        ▼
FrozenCharacterRunInputsV1 ───────► RunCombatProfileV1
        │                              immutable derived run-start projection
        ▼
RunSessionAggregateV1
        ├── existing PlayerRuntimeComposition / PlayerActorAuthority port
        ├── existing inventory-backed weapon execution port
        ├── existing StatusEffectAuthorityV1 port
        ├── narrow conditional-fact port (CONDITION-LIVE-001 boundary)
        ├── empty active-ability lifecycle port (ABILITY-RUNTIME-001 boundary)
        ├── narrow room query/lifecycle port (ROOM-JSON-LIVE-001 boundary)
        └── existing MissionRunResultAuthorityV1 port
                 │ exact-once immutable mission result
                 ▼
       downstream permanent result application (not implemented here)
```

## Responsibility table

| Component | Owns | Explicitly does not own |
|---|---|---|
| `CharacterCompositionCoordinatorV1` and selected production graph | Permanent selected character graph and subsystem save adapters | Mission health, cooldowns, effects, room state, projectiles, temporary pickups |
| `ProductionCharacterRunSessionStartSourceV1` | One start-boundary read and immutable freeze of accepted permanent inputs | Permanent mutation, save writes, gameplay state |
| `FrozenCharacterRunInputsV1` | Character revision/fingerprint, exact route/loadout/holdings/skills/equipment/stat fingerprints | Mutable authority or persisted primary truth |
| `RunSessionAuthorityV1` | Start-operation replay ledger, deterministic run identity, run lookup | Account, inventory, reward, room or save authority |
| `RunSessionAggregateV1` | Run lifecycle, restart/end replay, run-local counters/pickups/cash/statistics, immutable snapshots | Permanent XP/wallet/equipment/box mutation |
| Existing runtime ports | Their existing subsystem authority and generation-scoped behavior | A replacement authority inside the run aggregate |
| Existing mission-result authority | Exact strongbox provenance and exactly-once mission result | Reward application, box opening or permanent grants |

## Permanent versus transient state inventory

### Permanent character/account truth

- selected character instance ID, class definition, slot, display name and revision;
- account-backed component snapshots and their owning save adapters;
- production holdings/inventory and exact equipment instances;
- production loadout bindings;
- skill allocation;
- XP, money and scrap wallets;
- permanent strongbox ownership/opening state;
- event/account/achievement inputs supplied by their owning systems.

These values are read once for a new start and represented by complete input
fingerprints. `RunCombatProfileV1` is derived and is never persisted as primary
character truth.

### Run-local truth

- run ID, mission/layout, difficulty, seed and lifecycle state;
- participant/actor lifecycle generation;
- current health and position through the existing player runtime;
- weapon cooldown/fire replay and exact frozen equipment execution context;
- active status effects;
- conditional facts and active-ability lifecycle placeholders;
- room query/runtime state;
- projectiles, attack intents and contact operations;
- temporary pickups, run cash, counters and mission statistics;
- collected exact strongbox identities and provenance in the existing
  mission-result authority;
- HUD, debug, recovery and optional checkpoint projections.

## Start contract and identity

`StartRunSessionCommandV1` is versioned and immutable. It includes:

- operation ID;
- requested run ID or explicit run-instance identity material;
- selected character instance ID;
- expected character revision and fingerprint;
- mission/layout and difficulty IDs;
- deterministic seed;
- authoritative initial tick;
- event/modifier context fingerprint.

When a requested run ID is absent, run identity is derived deterministically from
the complete start identity material. Exact operation replay returns the original
immutable result object. Reusing an operation ID with changed input rejects without
creating another run. A different operation ID produces a distinct run identity even
for the same character and seed.

## Frozen character boundary

The production start source validates the selected production graph and freezes:

- the exact accepted `CharacterInstanceSnapshotV1`;
- character revision/fingerprint;
- current route/loadout sequence and fingerprint;
- holdings sequence and fingerprint;
- exact equipped equipment-instance IDs, immutable equipment payloads,
  definition IDs and runtime weapon references;
- skill allocation snapshot and fingerprint;
- injected class/level/equipment/augment/skill/event inputs consumed by the
  existing derived-stat composer;
- `DerivedCharacterStatsSnapshotV1` and `RunCombatProfileV1`;
- complete aggregate input fingerprint.

The active run never re-reads Hub state. A later start performs a fresh freeze, so
accepted Hub changes affect the later run only.

## Restart contract

`RestartRunSessionCommandV1` is explicit; scene reload is not restart authority.
Restart performs a complete preflight across every lifecycle port before committing:

1. validate run ID, generation and authoritative tick;
2. require replacement generation to increment exactly once;
3. preflight player, weapon, status, condition, ability and room ports;
4. restart every existing runtime port;
5. clear or retain run-local state according to `RunRestartPolicyV1`;
6. publish deterministic restart/debug fingerprints.

The same run ID is preserved. The existing player runtime restores health and advances
its lifecycle generation. Weapon restart clears authored projectiles, attack intents
and contact operations while generation-keyed weapon execution rejects stale facts.
Status effects advance their existing lifecycle and clear transient effects according
to their own authority. Generic fact admission rejects previous-generation damage,
projectile, effect, cast and contact facts.

## Snapshot and checkpoint boundary

The run exposes immutable versioned projections for:

- HUD;
- debug presentation;
- recovery diagnostics;
- deterministic test comparison;
- optional run checkpoint data.

`RunCheckpointV1` carries an explicit `IsPermanentCharacterTruth == false` invariant.
It is separate from account/character saves and cannot silently become permanent
character truth.

## End Run

`RunSessionAggregateV1.End` delegates to the existing
`MissionRunResultAuthorityV1`. Exact replay returns the original immutable result.
Conflicting operation reuse rejects without additional mutation.

The terminal receipt preserves:

- exact run and selected character identities;
- expected character revision/fingerprint;
- mission/layout, difficulty and deterministic seed;
- frozen input and combat-profile fingerprints;
- run-local statistics/counters/pickups/cash snapshot;
- exact collected strongbox identities and provenance through the existing mission
  result payload;
- deterministic receipt fingerprint.

Ending or displaying a result does not grant XP, mutate wallets, add equipment, open
or consume boxes, reroll rewards, or persist the run profile. Permanent result
application remains a downstream atomic operation targeting the exact character and
expected revision.

## Duplicate-authority audit

This change creates **no** duplicate:

- character authority;
- player-health authority;
- loadout or inventory authority;
- strongbox authority;
- room authority;
- reward or XP authority;
- mission-result authority;
- save authority.

The run code is a coordinator/aggregate over existing public contracts. It contains
only run-local state that had no permanent owner.

## Test coverage

Focused engine-neutral EditMode coverage includes:

- distinct run identities for distinct start operations;
- exact start replay and conflicting operation reuse;
- frozen stats across later Hub changes and fresh inputs for a later run;
- exact equipment-instance preservation and duplicate-definition separation;
- run-local health, cooldowns, effects, position and temporary pickups;
- no permanent character mutation during ordinary execution;
- restart identity preservation, lifecycle increment and authored reset policy;
- stale damage/effect/projectile generation rejection;
- exact end replay, conflicting end rejection and no permanent reward application;
- exact strongbox identities/provenance in the terminal result;
- participant/run isolation;
- deterministic immutable HUD/debug/recovery/checkpoint fingerprints;
- production composition from account-backed character selection to run creation,
  including a public Hub loadout mutation visible only to a subsequent run.

Suggested focused commands:

```bash
Unity -batchmode -nographics -quit \
  -projectPath . \
  -runTests -testPlatform EditMode \
  -assemblyNames ShooterMover.Tests.EditMode.RunSessions \
  -testResults artifacts/run-session-editmode.xml \
  -logFile artifacts/run-session-editmode.log

Unity -batchmode -nographics -quit \
  -projectPath . \
  -runTests -testPlatform PlayMode \
  -testResults artifacts/run-session-playmode.xml \
  -logFile artifacts/run-session-playmode.log
```

## Changed-file boundary

Production changes are isolated to:

- `Assets/ShooterMover/Runtime/Application/Runs/Session/`
- `Assets/ShooterMover/Runtime/UnityAdapters/Players/RunSessions/`

Tests and proof are isolated to:

- `Assets/ShooterMover/Tests/EditMode/RunSessions/`
- `docs/architecture/runs/RUN_SESSION_001.md`

`Stage1VisibleSliceController.cs` is not edited.

## Known limitations and later integration points

- **BOX-PERSIST-001:** permanent box transfer/application remains downstream; this
  task only preserves exact run collection identity and provenance.
- **ROOM-JSON-LIVE-001:** the run exposes narrow room ports but does not perform the
  complete JSON Level 1 scene cutover.
- **CONDITION-LIVE-001:** conditional facts are consumed through a narrow lifecycle
  port. No concurrent condition/status integration path is implemented here.
- **ABILITY-RUNTIME-001:** an empty run-owned lifecycle port is supplied; active
  ability behavior is intentionally absent.
- Scene presentation/bootstrap wiring can construct the concrete runtime port factory
  later without changing the engine-neutral aggregate or permanent ownership model.
