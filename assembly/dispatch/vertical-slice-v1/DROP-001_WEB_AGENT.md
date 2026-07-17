# DROP-001 — Gameplay reward drops

Use this packet with one isolated GitHub/Unity coding agent.

```text
Repository: YeerooXY/shooter-mover
Task: DROP-001
Branch: agent/drop-001-gameplay-reward-drops
Exact base commit: 645cf24f30ee6c8762214a84060e59e35df67a05
PR base: main
```

Create a fresh exact-base branch and one non-empty draft PR. Do not merge it. Read REW/SRC/GEN/RAP/PICK/INV/MON/SCR and enemy/prop destruction contracts plus the vertical-slice ownership matrix first.

## Objective

Connect accepted enemy/prop destruction facts to configurable reward profiles and physical pickup spawning. Support money-only, strongbox-only, scrap, ammunition, miscellaneous, default odds, and per-instance forced overrides. Existing reward/pickup/holding authorities remain the sole truth.

## Dependencies

- Merged REW-001, SRC-001, GEN-001, RAP-001, PICK-001, INV-001, MON-001, SCR-001, enemy lifecycle, and PROP-001 facts.

## Exclusive owned files and paths

- `Assets/ShooterMover/Runtime/Application/Rewards/GameplayDrops/**`
- `Assets/ShooterMover/Runtime/UnityAdapters/Rewards/GameplayDrops/**`
- `Assets/ShooterMover/Content/Definitions/Rewards/GameplayDrops/**`
- `Assets/ShooterMover/Tests/EditMode/Rewards/GameplayDrops/**`
- `Assets/ShooterMover/Tests/PlayMode/Rewards/GameplayDrops/**`
- `docs/architecture/rewards/GAMEPLAY_DROPS_V1.md`

## Forbidden paths and changes

- Do not edit enemy/prop definitions, runtime, prefabs, or XP fields.
- Do not edit or duplicate PICK/SRC/GEN/RAP/INV/MON/SCR algorithms.
- Do not edit gameplay scenes/controller, level-design components, HUD, Results, or strongbox opening.
- Do not award directly at destruction time or mutate wallets/holdings directly.
- Do not edit ProjectSettings, Packages, shared asmdefs, generated/context/dispatch files.

## Acceptance criteria

- [ ] Generic request consumes stable run/source/destruction operation identity and one resolved SRC profile/override.
- [ ] Supports inherit, none, replace, append guaranteed, money-only, strongbox-only/tier range, scrap, ammunition, and miscellaneous using existing vocabulary where available.
- [ ] Deterministic context produces stable grants, pickup IDs, and trace/fingerprint.
- [ ] Duplicate destruction callbacks/replays do not spawn or award extra value.
- [ ] Spawned value uses PICK-001 physical pickups; collection applies through RAP and accepted child authorities.
- [ ] Forced overrides are authoring inputs, not alternate algorithms.
- [ ] Tests cover all categories, odds/no-drop, forced overrides, duplicates, distinct sources, restart, impossible profiles, and identity stability.

## Focused Unity test commands

```powershell
$Unity = "C:\Program Files\Unity\Hub\Editor\6000.3.19f1\Editor\Unity.exe"
New-Item -ItemType Directory -Force artifacts/test-results, artifacts/logs | Out-Null
& $Unity -batchmode -nographics -projectPath "$PWD" -runTests -testPlatform EditMode -testFilter "ShooterMover.Tests.EditMode.Rewards.GameplayDrops" -testResults "artifacts/test-results/DROP-001-EditMode.xml" -logFile "artifacts/logs/DROP-001-EditMode.log" -quit
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
& $Unity -batchmode -nographics -projectPath "$PWD" -runTests -testPlatform PlayMode -testFilter "ShooterMover.Tests.PlayMode.Rewards.GameplayDrops" -testResults "artifacts/test-results/DROP-001-PlayMode.xml" -logFile "artifacts/logs/DROP-001-PlayMode.log" -quit
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
```

Passing claims require both XML files with zero failures.

## Manual proof checklist

- [ ] Configure default enemy and prop profiles in a task-owned fixture.
- [ ] Force money, strongbox, scrap, ammunition, miscellaneous, and no-drop outcomes and inspect physical pickups.
- [ ] Destroy one source repeatedly and verify one deterministic set.
- [ ] Collect through normal collision and verify authorities change once.
- [ ] Quick-restart/replay and verify no duplicate value.

## Merge order

First wave; merge before DEV-001 and DEMO-005.

## Asset requirements

Use existing pickup presentation/placeholders.

## Known limitations

- If ammunition lacks accepted shared vocabulary, add only a narrow typed payload inside owned paths; do not create a global ammo authority.
- Final scene placement belongs to DEMO-005.

## Parallel dispatch safety

Safe immediately with XP-002; no enemy/prop package edits are allowed.
