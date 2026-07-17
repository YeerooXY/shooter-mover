# CRAFTUI-001 — Functional hub crafting screen

Use this packet with one isolated GitHub/Unity coding agent.

```text
Repository: YeerooXY/shooter-mover
Task: CRAFTUI-001
Branch: agent/craftui-001-functional-crafting-screen
Exact base commit: 645cf24f30ee6c8762214a84060e59e35df67a05
PR base: main
```

Create a fresh dependency-complete branch and one non-empty draft PR. Do not merge it.

## Objective

Build the Hub Crafting screen as a presentation/controller over CRA-001, SCR-001, INV-001, GEN-001, and RAP-001. Display recipes, target equipment, unlock state, cost/balance, preview, and exact results while all mutation remains in existing authorities.

## Dependencies

- HUB-001 merged.
- CRA-001, SCR-001, INV-001, GEN-001, RAP-001 merged.
- WEAPON-DATA-001 may supply real targets; do not duplicate weapon content.

## Exclusive owned files and paths

- `Assets/ShooterMover/Runtime/Application/Crafting/Presentation/**`
- `Assets/ShooterMover/UI/Crafting/**`
- `Assets/ShooterMover/Scenes/Flow/Crafting/**`
- `Assets/ShooterMover/Tests/EditMode/Crafting/Presentation/**`
- `Assets/ShooterMover/Tests/PlayMode/Flow/Crafting/**`
- `docs/authoring/CRAFTING_SCREEN_V1.md`

## Forbidden paths and changes

- Do not edit crafting/SCR/INV/GEN/RAP authorities, recipes/algorithms, or production balance.
- Do not directly spend scrap or grant equipment.
- Do not edit HUB/other screens/gameplay, ProjectSettings, Packages, shared asmdefs, generated/context/dispatch files.

## Acceptance criteria

- [ ] Projects recipes, target definitions, natural/actual unlock, eligibility, deterministic preview, cost/balance, and result state.
- [ ] Craft uses accepted CRA command and displays success, locked, insufficient, stale/invalid, pending/retry, duplicate, and rejected facts.
- [ ] Success spends scrap and grants one exact immutable equipment instance once through authorities.
- [ ] Different crafted instances may share a definition while retaining unique IDs.
- [ ] Repeated input/revisit/retry cannot double spend/grant.
- [ ] Back returns to Hub with identical payload.
- [ ] Tests cover locked/eligible, cost/balance, success, insufficient, duplicate/conflict, retry, duplicate definitions, revisit, and no direct mutation.

## Focused Unity test commands

```powershell
$Unity = "C:\Program Files\Unity\Hub\Editor\6000.3.19f1\Editor\Unity.exe"
New-Item -ItemType Directory -Force artifacts/test-results, artifacts/logs | Out-Null
& $Unity -batchmode -nographics -projectPath "$PWD" -runTests -testPlatform EditMode -testFilter "ShooterMover.Tests.EditMode.Crafting.Presentation" -testResults "artifacts/test-results/CRAFTUI-001-EditMode.xml" -logFile "artifacts/logs/CRAFTUI-001-EditMode.log" -quit
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
& $Unity -batchmode -nographics -projectPath "$PWD" -runTests -testPlatform PlayMode -testFilter "ShooterMover.Tests.PlayMode.Flow.Crafting" -testResults "artifacts/test-results/CRAFTUI-001-PlayMode.xml" -logFile "artifacts/logs/CRAFTUI-001-PlayMode.log" -quit
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
```

Passing claims require both XML files with zero failures.

## Manual proof checklist

- [ ] Open Crafting from Hub with deterministic recipe/player/scrap state.
- [ ] Inspect locked/available recipes and preview.
- [ ] Craft one recipe; verify one spend and one instance grant.
- [ ] Repeat/test insufficient scrap; no duplicate value.
- [ ] Return/revisit and verify authority state persists.

## Merge order

Second wave after HUB; may merge with SHOPUI/SKILLUI.

## Asset requirements

No crafting artwork is present at asset intake; create a functional code-owned UI ready for later art.

## Known limitations

- No dismantling/salvage.
- Final recipe/balance content is outside UI scope.

## Parallel dispatch safety

Safe with SHOPUI/SKILLUI after dependencies; paths are isolated.
