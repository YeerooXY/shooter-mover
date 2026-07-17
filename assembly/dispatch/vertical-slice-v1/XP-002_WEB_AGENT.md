# XP-002 — Enemy XP rewards

Use this packet with one isolated GitHub/Unity coding agent.

```text
Repository: YeerooXY/shooter-mover
Task: XP-002
Branch: agent/xp-002-enemy-xp-rewards
Exact base commit: 645cf24f30ee6c8762214a84060e59e35df67a05
PR base: main
```

Create a fresh exact-base branch and one non-empty draft PR. Do not merge it. Read XP-001, enemy lifecycle/destruction contracts, `AGENTS.md`, current handoff, and vertical-slice ownership/validation before editing.

## Objective

Add configurable XP values to the standing turret, mobile blaster droid, pursuer drone, ram droid, and a reusable future-enemy contract. Convert accepted enemy-destruction facts into exactly-once XP-001 grants. Definitions remain declarative; only XP-001 mutates XP.

## Dependencies

- XP-001 merged on the launch base.
- Merged enemy lifecycle/destruction facts and stable placed/source operation identities.

## Exclusive owned files and paths

- `Assets/ShooterMover/Runtime/Application/Progression/Experience/EnemyRewards/**`
- `Assets/ShooterMover/Runtime/UnityAdapters/Progression/Experience/EnemyRewards/**`
- `Assets/ShooterMover/Tests/EditMode/Progression/Experience/EnemyRewards/**`
- `Assets/ShooterMover/Tests/PlayMode/Progression/Experience/EnemyRewards/**`
- `Assets/ShooterMover/ContentPackages/Enemies/BlasterTurret/BlasterTurretDefinition.cs`
- `Assets/ShooterMover/ContentPackages/Enemies/BlasterTurret/BlasterTurretDefinition.asset`
- `Assets/ShooterMover/ContentPackages/Enemies/MobileBlasterDroid/MobileBlasterDroidDefinition.cs`
- `Assets/ShooterMover/ContentPackages/Enemies/MobileBlasterDroid/MobileBlasterDroidDefinition.asset`
- `Assets/ShooterMover/ContentPackages/Enemies/PursuerDrone/PursuerDroneDefinition.cs`
- `Assets/ShooterMover/ContentPackages/Enemies/PursuerDrone/PursuerDroneDefinition.asset`
- `Assets/ShooterMover/ContentPackages/Enemies/RamDroid/RamDroidDefinition.cs`
- `Assets/ShooterMover/ContentPackages/Enemies/RamDroid/RamDroidDefinition.asset`
- `docs/architecture/progression/ENEMY_XP_REWARDS_V1.md`

## Forbidden paths and changes

- Do not edit enemy prefabs, AI/runtime/presentation/package classes, combat, scenes, Stage1 controller, reward/drop paths, wallets, holdings, or reward authorities.
- Do not grant XP directly from definitions or arbitrary MonoBehaviour callbacks.
- Do not use GameObject name, runtime GUID, Unity instance ID, time, or callback count as identity.
- Do not edit XP-001 core authority/curve contracts beyond public API consumption.
- Do not edit ProjectSettings, Packages, shared asmdefs, generated/context/dispatch files.

## Acceptance criteria

- [ ] All four definitions expose a non-negative author-facing XP value; future definitions can implement the same reusable contract.
- [ ] Validation rejects negative values and explicitly permits/handles zero-XP enemies.
- [ ] Destruction creates one positive XP request using permanent run/source/destruction operation identity and deterministic fingerprint.
- [ ] Duplicate death callbacks/messages, replay, snapshot import, and quick restart produce no extra XP.
- [ ] Distinct enemy instances grant independently even with the same definition.
- [ ] XP-001 level-up facts remain intact and available to consumers.
- [ ] Tests cover all named enemies, zero/negative validation, distinct instances, duplicate/conflicting death, level-up, restart, and import/replay.

## Focused Unity test commands

```powershell
$Unity = "C:\Program Files\Unity\Hub\Editor\6000.3.19f1\Editor\Unity.exe"
New-Item -ItemType Directory -Force artifacts/test-results, artifacts/logs | Out-Null
& $Unity -batchmode -nographics -projectPath "$PWD" -runTests -testPlatform EditMode -testFilter "ShooterMover.Tests.EditMode.Progression.Experience.EnemyRewards" -testResults "artifacts/test-results/XP-002-EditMode.xml" -logFile "artifacts/logs/XP-002-EditMode.log" -quit
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
& $Unity -batchmode -nographics -projectPath "$PWD" -runTests -testPlatform PlayMode -testFilter "ShooterMover.Tests.PlayMode.Progression.Experience.EnemyRewards" -testResults "artifacts/test-results/XP-002-PlayMode.xml" -logFile "artifacts/logs/XP-002-PlayMode.log" -quit
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
```

Passing claims require both XML files with zero failures.

## Manual proof checklist

- [ ] Inspect each named enemy definition asset and locate XP value.
- [ ] Destroy each enemy once in focused fixtures and verify configured XP.
- [ ] Trigger duplicate death/event delivery and verify one grant.
- [ ] Quick-restart and replay/import the same operation; verify no duplicate.
- [ ] Destroy a new instance of the same definition and verify an independent grant.

## Merge order

First wave; merge before DEMO-005. Independent of DROP-001.

## Asset requirements

None.

## Known limitations

- Values are initial configurable content and may be balanced later.
- This task does not author the XP curve.

## Parallel dispatch safety

Safe immediately with DROP-001. DROP-001 is forbidden from editing the exact enemy definition files above.
