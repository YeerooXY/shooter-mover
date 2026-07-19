# ENEMY-001 reusable enemy runtime foundation

## Ownership and dependency boundary

`EnemyActorState` and `EnemyActorStepper` remain the only enemy health, processed-damage,
terminal-lifecycle, and encounter-resolution authorities. ENEMY-001 adds read-only projections
and deterministic decision values in the engine-neutral `ShooterMover.GameplayEntities`
assembly. It does not copy health or lifecycle transitions into a second actor.

`EnemyRuntimeProjection` maps the actor's `ActorId` to ENTITY-001
`GameplayEntityIdentity.EntityInstanceId` and its `RoleId` to a definition ID. Lifecycle
generation is stored beside the stable identity and is deliberately absent from identity equality
and hashing. Optional run-participant ownership remains separate from entity, definition, target,
decision, attack-event, and lifecycle-generation identities.

The dependency head used for this implementation is
`8d040a15e2535ce221667d3cea00c6bf03ad9d40` from PR #215. At that head:

- `GameplayEntityIdentity` equality and hashing exclude lifecycle generation;
- generic identity is exposed as `EntityInstanceId`; player `ActorInstanceId` is a convenience alias;
- damage and healing commands carry distinct source actor and optional source run-participant IDs;
- lifecycle rejection statuses are reachable and covered by public-behavior tests;
- ENTITY-001 focused tests do not use source-substring architecture assertions.

PR #215 has since merged. ENEMY-001 was rebased onto the resulting `main` commit
`d763605c13379ae4946c8fe8a2a7e1ed7fb1a1b3` before final verification.

## Runtime flow

Unity adapters gather transforms, velocities, range/arc results, and externally queried
line-of-sight into an immutable `EnemyPerceptionSnapshot`. A small policy evaluates that snapshot
once and returns both `EnemyDecisionSnapshot` and `EnemyDebugSnapshot`. The debug object is built
from the exact selected target and decision; it never reruns AI.

`EnemyPerceptionBuilder` is the shared pure geometry boundary for adapters that start from raw
positions. It derives distance, normalized direction, inclusive detection-radius membership, and
inclusive vision-arc membership. Only line-of-sight remains an externally supplied physics/query
result. The selected target's derived distance and detection/arc/line-of-sight results are copied
into the debug snapshot used by later visualization.

Vision and attack arcs remain distinct. Perception records whether a candidate is visible within
the adapter-supplied vision arc. At decision time, the policy independently evaluates the same
observer facing and target direction against `EnemyDecisionProfile.AttackArcDegrees`. An attack is
requested only when range, line-of-sight, vision arc, and attack arc all accept the target. Debug
data exposes both arc results so a wide-sensing, narrow-firing enemy remains truthful.

`EnemyAttackIntent` identifies an attack definition/capability rather than a weapon type. The same
contract can therefore request a projectile, melee strike, pounce, disposable impact, beam, area,
summon, or support executor. ENEMY-001 provides no executor registry or Unity debug renderer.
`EnemyPounceCommitment` freezes origin, direction, target point, and initial target. A later impact
observation may identify a contacted entity but cannot steer or replace that commitment.

Room completion consumes `EnemyRuntimeProjection.BlocksRoomClear`. Active required enemies and
active objective entities block; optional and non-participating entities do not; terminal actors do
not. No package or hierarchy name is involved.

## Existing enemy migration map

| Package | Current definition owner | Runtime actor owner | Movement policy | Attack reference/type | Targeting | Room-clear role | XP/drop references | Unity adapter/presentation | Remaining migration work |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| Blaster Turret | `BlasterTurretDefinition` | `BlasterTurretAuthority` over `EnemyActorState` | Stationary cadence/facing | Blaster projectile module | Authored target plus physics line-of-fire source | Required enemy (expected projection) | External EN-002 terminal fact; reward authoring remains separate | `BlasterTurretPackage`, authoring, scene context, presentation | Build perception externally and project cadence request to generic attack intent |
| Mobile Blaster Droid | `MobileBlasterDroidDefinition`, including shared projection factory | `MobileBlasterDroidRuntime2D` over `EnemyActorState` | Approach/retreat velocity in runtime | Mobile blaster projectile, projected as its existing attack reference | Configured `EnemyTarget2DAdapter` | Required enemy (implemented projection) | External XP/drop consumers | Runtime2D and temporary presentation | Move live decision calculation to supplied perception and execute generic intent |
| Pursuer Drone | `PursuerDroneDefinition` | `PursuerDroneAuthority` over `EnemyActorState` | `PursuerDroneDecisionSource` direct pursuit | Ordinary contact/melee capability | Supplied `IEnemyTarget2DSource` | Required enemy (expected projection) | External terminal-fact consumers | Package, target adapter, presentation | Project contact attack and decision/debug snapshots through common contracts |
| Ram Droid | `RamDroidDefinition` | `RamDroidRuntime2D` over `EnemyActorState` | Warning then committed movement | Disposable-impact/charge capability | Configured player target | Required enemy (expected projection) | External terminal-fact consumers | Runtime2D and temporary presentation | Split selection, wind-up, commitment, impact, recovery and use frozen commitment |
| Four Blaster Elite | Static package descriptors/constants | `FourBlasterEliteSession` over `EnemyActorState` | Stationary boss cadence | Four ordered blaster execution plans | Coordinates supplied to `Advance` | Required enemy/boss (expected projection) | External terminal-fact consumers; no direct award | Engine-neutral session plus package presentation consumers | Replace weapon-shaped request boundary with four attack intents and add placed adapter |

The role entries marked "expected projection" describe the capability each current Stage 1 enemy
should author during later package migration; this PR does not edit scenes, prefabs, or all five
packages to serialize that role.

## Explicitly outside scope

- attack executors and registries;
- complete package migration;
- Unity perception queries, renderer, or debug UI;
- room transitions and encounter orchestration;
- reward, XP, money, inventory, drops, statistics, kills, or assists;
- player weapons, scenes, prefabs, sprites, and demo-controller changes.

## Focused verification

```powershell
& <PINNED_UNITY_EDITOR> -batchmode -nographics -projectPath "$PWD" -runTests `
  -testPlatform EditMode `
  -testFilter "ShooterMover.Tests.EditMode.Enemies" `
  -testResults "artifacts/test-results/ENEMY-001-EditMode.xml" `
  -logFile "artifacts/logs/ENEMY-001-EditMode.log"
```

Verified locally with Unity `6000.3.19f1` after rebasing onto merged `main`: full project script
compilation passed and the focused enemy filter passed 46 of 46 EditMode tests, with no failed,
skipped, or inconclusive tests. `-quit` is intentionally omitted because `-runTests` exits after
completion and Unity 6.3 may process `-quit` before the Test Framework starts. Temporary XML/log
artifacts were removed after recording the result; the shared Unity Library cache was retained.
