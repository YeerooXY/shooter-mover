# Pursuer Drone Package (EN-004)

## Package manifest

| Item | Value |
|---|---|
| Package ID | `enemy.pursuer-drone` |
| Classification | Ordinary Stage 1 enemy |
| Damage channel | Contact |
| Weight | Standard |
| Movement reference | `module.enemy-direct-pursuit` |
| Attack reference | `module.enemy-ordinary-contact` |
| Telegraph reference | `module.enemy-contact-telegraph` |
| Capabilities | Direct pursuit; ordinary contact damage |
| Definition version | 1 |
| Registry behavior | Contributes an input descriptor only; writes no generated registry |

The package is independently owned under this folder. It does not edit or duplicate
shared enemy adapters, player movement, combat foundations, encounter scenes, mission
state, save data, or generated outputs.

## Runtime composition

`PursuerDronePackage` is the package composition root.

- `PursuerDroneAuthority` owns the current immutable `EnemyActorState` and delegates every
  damage, contact, death, and reset transition to EN-002 `EnemyActorStepper`.
- `PursuerDroneDecisionSource` implements EN-003 `IEnemyActor2DDecisionSource` and returns
  one direct normalized velocity toward the explicit target.
- EN-003 `EnemyActor2DAdapter` bounds and applies that decision only to the drone's own
  `Rigidbody2D`.
- EN-003 `EnemyTarget2DAdapter` remains the hit intake.
- EN-003 `EnemyContact2DAdapter` remains the contact/cadence translator and reports the
  bounded mover-damage request. It never receives or writes a player body.
- Session restart is delegated through `EnemyActor2DAdapter.Restart`, which resets the
  package authority, decision sequence, fixed-step index, callback identity, and contact
  grace state as one operation.

The caller supplies an explicit player target source, player collider, actor identity,
and mover identity. There is no scene search, service locator, generated registry lookup,
or implicit encounter ownership.

## Default tuning

| Setting | Default |
|---|---:|
| Maximum health | 12 |
| Direct pursuit speed | 4 units/s |
| Stopping distance | 0.2 units |
| Ordinary contact damage request | 2 |
| Contact cadence / grace | 0.5 s |
| Simultaneous contact window | 0.02 s |
| Registered mover-collider capacity | 4 |
| Warning pulse | 0.6 s |

Every value is validated against a package hard bound before configuration. The ordinary
contact policy applies no damage more frequently than its cadence and cannot write player
velocity.

## Temporary readability presentation

The prefab uses package-generated temporary grayscale sprites:

- a broad, asymmetric arrow-like body silhouette;
- two detached side fins that visibly pulse in and out while pursuit is active; and
- warning fins that disappear when disabled or destroyed.

Shape separation and motion carry the warning, so recognition does not depend on hue.
This is placeholder presentation only and is intended to be replaced by final package
art without changing gameplay authority.

## Lifecycle behavior

- **Target available and alive:** direct pursuit is projected through the shared adapter.
- **Inside stopping distance:** the decision is zero velocity.
- **Target lost:** EN-003 returns `TargetUnavailable` and clears drone velocity.
- **Drone destroyed:** EN-002 marks the actor terminal; EN-003 clears velocity and rejects
  later contact damage.
- **Package disabled:** the shared actor/contact adapters deactivate and clear velocity.
- **Session restart:** initial health and cadence return, stale callbacks are removed, and
  pursuit resumes only when the package was active.

## Focused verification

Run with the pinned Unity editor on Windows:

```text
"C:\Program Files\Unity\Hub\Editor\6000.3.19f1\Editor\Unity.exe" -batchmode -nographics -projectPath . -runTests -testPlatform PlayMode -testFilter ShooterMover.Tests.PlayMode.Enemies.PursuerDronePackageTests -testResults Artifacts\TestResults\PursuerDronePackageTests.xml -logFile Artifacts\Logs\PursuerDronePackageTests.log -quit
```

The fixture covers direct pursuit, bounded contact cadence, target loss, death, disable,
restart, EN-003 port/adapter consumption, 2D-only source boundaries, package-local assets,
and non-color presentation cues.

Manual proof is limited to opening `PursuerDrone.prefab` in isolation and confirming the
broad silhouette and moving paired fins remain legible in grayscale. EN-004 does not add
the prefab to an encounter scene and makes no encounter-integration claim.

## Non-goals

No pathfinding, navigation framework, ranged attack, elite behavior, final art, scene
editing, generated registry, persistence, reward behavior, or player velocity write is
part of this package.

## One-unit rollback

Remove this folder and
`Assets/ShooterMover/Tests/PlayMode/Enemies/PursuerDronePackageTests.cs` together, including
their inseparable Unity metadata. No scene, registry, project setting, save schema, or
shared-adapter cleanup is required.
