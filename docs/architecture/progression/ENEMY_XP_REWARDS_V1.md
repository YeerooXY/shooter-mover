# Enemy XP Rewards V1

## Purpose

XP-002 converts accepted enemy-destruction facts into exactly-once XP-001 grants without moving authority into enemy combat, Unity callbacks, or enemy definitions.

The implementation has three boundaries:

1. **Authoring catalog** — `EnemyExperienceRewardCatalogAssetV1` stores level bands keyed by an enemy definition `StableId`.
2. **Application service** — `EnemyExperienceRewardServiceV1` resolves the configured amount and converts one `EnemyDestroyedNotification` into one XP-001 request.
3. **Unity composition decorator** — `EnemyExperienceRewardingAuthorityV1` wraps an existing `IEnemyActor2DAuthority`, returns the original combat result unchanged, and forwards only accepted EN-002 destruction notifications.

XP-001 remains the sole mutable player-XP authority.

## Supported enemy definitions

The initial catalog recognizes these canonical definition identities:

- `enemy.blaster-turret`
- `enemy.mobile-blaster-droid`
- `enemy.pursuer-drone`
- `enemy.ram-droid`

The application contract is reusable. A future enemy participates by adding a catalog definition for its stable definition identity; no XP logic needs to be added to its combat behavior.

## Level authoring

Each definition contains contiguous inclusive bands covering enemy levels 1 through 100. Every band stores a non-negative `long` XP amount.

Validation requires:

- a valid canonical enemy definition `StableId`;
- one or more bands;
- exact coverage from level 1 through level 100;
- no gap or overlap;
- no negative XP;
- no duplicate enemy definition identity.

Zero-XP enemies are valid. Their destruction returns `ZeroRewardNoChange` and does not submit an invalid zero-value request to XP-001.

The values in the default Stage 1 catalog are initial configurable placeholders. Balance may change by editing the catalog, without changing the grant service or XP authority.

## Source-operation identity

A destruction operation is derived only from permanent identity inputs:

```text
schema = enemy-xp-operation-v1
run_stable_id
enemy_actor_stable_id
operation_kind = enemy-destroyed
```

The length-prefixed canonical representation is SHA-256 hashed. The lowercase hash becomes:

```text
xp-operation.<sha256>
```

The authored XP amount is intentionally **not** part of the source-operation identity. XP-001 includes the amount in its command fingerprint. Therefore:

- replaying the same death with the same amount returns `DuplicateNoChange`;
- replaying the same death after an amount/configuration change returns `ConflictingDuplicate`;
- neither case grants additional XP;
- repeated callbacks or retry-generated destruction event IDs for the same placed actor in the same run still map to one operation;
- two distinct placed enemy actor identities grant independently, even when they share one enemy definition;
- the same placed enemy identity in a different run grants independently because the run identity changes.

No GameObject name, Unity instance ID, runtime GUID, wall-clock time, callback count, or frame index participates in identity.

## Destruction boundary

EN-002 is authoritative for enemy health and lifecycle. XP-002 consumes only `EnemyDestroyedNotification` facts already emitted by `EnemyActorStepper`.

`EnemyExperienceRewardingAuthorityV1` is a decorator around the existing EN-003 authority port:

```text
EnemyActorCommand
  -> existing IEnemyActor2DAuthority.Apply
  -> original EnemyActorStepResult returned unchanged
  -> each EnemyDestroyedNotification forwarded to XP-002
  -> XP-001 Grant
```

Damage, contact, death, despawn, movement, and reset behavior remain owned by the existing enemy systems.

## Retry, restart, and persistence behavior

The decorator clears only its presentation-ready `LastRewardFacts` on enemy reset. It does not clear XP-001 history.

As a result:

- repeated death callbacks/messages are no-ops after the first grant;
- a quick restart in the same run cannot grant the same operation twice;
- importing an XP-001 snapshot restores the accepted operation identities;
- replaying a restored destruction fact produces no additional XP;
- level-up facts produced by XP-001 remain available through `EnemyExperienceRewardFactV1.LevelUpFacts`.

A new run must provide a new stable run identity. Reusing a run identity intentionally reuses the same exactly-once scope.

## Composition

Production composition provides:

- the existing `IEnemyActor2DAuthority` instance;
- the shared `IPlayerExperienceAuthorityV1` instance;
- a validated enemy XP catalog;
- the permanent run identity;
- the enemy definition identity;
- the authored enemy level.

The wrapper is then supplied wherever the original `IEnemyActor2DAuthority` would have been supplied. No enemy prefab, combat package, or XP-001 implementation change is required.

## Validation

Focused test namespaces:

```text
ShooterMover.Tests.EditMode.Progression.Experience.EnemyRewards
ShooterMover.Tests.PlayMode.Progression.Experience.EnemyRewards
```

Focused Unity commands:

```powershell
$Unity = "C:\Program Files\Unity\Hub\Editor\6000.3.19f1\Editor\Unity.exe"
New-Item -ItemType Directory -Force artifacts/test-results, artifacts/logs | Out-Null
& $Unity -batchmode -nographics -projectPath "$PWD" -runTests -testPlatform EditMode -testFilter "ShooterMover.Tests.EditMode.Progression.Experience.EnemyRewards" -testResults "artifacts/test-results/XP-002-EditMode.xml" -logFile "artifacts/logs/XP-002-EditMode.log" -quit
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
& $Unity -batchmode -nographics -projectPath "$PWD" -runTests -testPlatform PlayMode -testFilter "ShooterMover.Tests.PlayMode.Progression.Experience.EnemyRewards" -testResults "artifacts/test-results/XP-002-PlayMode.xml" -logFile "artifacts/logs/XP-002-PlayMode.log" -quit
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
```

A passing Unity claim requires both XML files to report zero failures. Authored tests cover the four named enemies, level resolution, zero/negative validation, deterministic identities, distinct instances, duplicate/conflicting death, level-up facts, quick restart, and snapshot import/replay.
