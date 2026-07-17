# SHOPUI-001 — Functional hub shop screen

Use this packet with one isolated GitHub/Unity coding agent.

```text
Repository: YeerooXY/shooter-mover
Task: SHOPUI-001
Branch: agent/shopui-001-functional-shop-screen
Exact base commit: 645cf24f30ee6c8762214a84060e59e35df67a05
PR base: main
```

Create a fresh dependency-complete branch and one non-empty draft PR. Do not merge it.

## Objective

Build the Hub Shop screen as a presentation/controller over SHOP-001, MON-001, INV-001, GEN-001, and RAP-001. Show deterministic stock/money and execute validated purchases through existing authorities only.

## Dependencies

- HUB-001 merged.
- SHOP-001, MON-001, INV-001, GEN-001, RAP-001 merged.
- Prefer WEAPON-DATA-001 for real content; otherwise use existing fixtures without inventing definitions.

## Exclusive owned files and paths

- `Assets/ShooterMover/Runtime/Application/Shops/Presentation/**`
- `Assets/ShooterMover/UI/Shop/**`
- `Assets/ShooterMover/Scenes/Flow/Shop/**`
- `Assets/ShooterMover/Tests/EditMode/Shops/Presentation/**`
- `Assets/ShooterMover/Tests/PlayMode/Flow/Shop/**`
- `Assets/ShooterMover/Art/UI/Shop/**`
- `docs/authoring/SHOP_SCREEN_V1.md`

## Forbidden paths and changes

- Do not edit SHOP/MON/INV/GEN/RAP authority/domain or production balance.
- Do not directly debit money/grant equipment or fake sell/salvage.
- Do not edit HUB/other screens/gameplay, ProjectSettings, Packages, shared asmdefs, generated/context/dispatch files.

## Acceptance criteria

- [ ] Projects deterministic stock, prices, sold state, item/instance data, and money from authority snapshots.
- [ ] Stock remains stable for identical run/shop/refresh context and revisit.
- [ ] Purchase uses accepted SHOP command and displays applied, insufficient, sold, pending/retry, duplicate, and rejected facts.
- [ ] Success changes money/equipment exactly once through authorities.
- [ ] Duplicate definitions remain valid distinct instances.
- [ ] Back returns to Hub with identical route/profile payload; revisit retains authority state.
- [ ] UI never becomes stock/price/money/holdings truth.
- [ ] Tests cover projection, duplicates, success, insufficient funds, sold, duplicate input, retry, revisit, and no direct mutation.

## Focused Unity test commands

```powershell
$Unity = "C:\Program Files\Unity\Hub\Editor\6000.3.19f1\Editor\Unity.exe"
New-Item -ItemType Directory -Force artifacts/test-results, artifacts/logs | Out-Null
& $Unity -batchmode -nographics -projectPath "$PWD" -runTests -testPlatform EditMode -testFilter "ShooterMover.Tests.EditMode.Shops.Presentation" -testResults "artifacts/test-results/SHOPUI-001-EditMode.xml" -logFile "artifacts/logs/SHOPUI-001-EditMode.log" -quit
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
& $Unity -batchmode -nographics -projectPath "$PWD" -runTests -testPlatform PlayMode -testFilter "ShooterMover.Tests.PlayMode.Flow.Shop" -testResults "artifacts/test-results/SHOPUI-001-PlayMode.xml" -logFile "artifacts/logs/SHOPUI-001-PlayMode.log" -quit
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
```

Passing claims require both XML files with zero failures.

## Manual proof checklist

- [ ] Open Shop from Hub with deterministic context.
- [ ] Verify supplied artwork is a backplate and real stock controls align.
- [ ] Purchase once and verify exact authority facts.
- [ ] Test insufficient funds/repeated purchase with no extra mutation.
- [ ] Leave/revisit and verify stock/sold state persists.

## Merge order

Second wave after HUB; preferably after WEAPON-DATA-001. May merge with SKILLUI/CRAFTUI.

## Asset requirements

Copy `source-assets/user-intake/menu_screens/shop_template.png` from exact asset commit `0b1b654c1fb8cf8208904eb55041fde954cfb560` into `Assets/ShooterMover/Art/UI/Shop/**`. Real values/controls remain overlays.

## Known limitations

- Selling is excluded unless a merged authority supports it.
- Final balance/content is outside UI ownership.

## Parallel dispatch safety

Safe with SKILLUI/CRAFTUI after dependencies; paths are isolated.
