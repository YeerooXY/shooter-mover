# BOXUI-001 — Results to Strongbox Opening

Use this packet with one isolated GitHub/Unity coding agent.

```text
Repository: YeerooXY/shooter-mover
Task: BOXUI-001
Branch: agent/boxui-001-results-strongbox-flow
Exact base commit: 645cf24f30ee6c8762214a84060e59e35df67a05
PR base: main
```

Do not dispatch until RUN-001 and DEV-001 merge. Then create a fresh dependency-complete branch and one non-empty draft PR. Do not merge it.

## Objective

Build the real Results presentation for pending collected boxes and route one selected exact strongbox instance into the existing Strongbox Opening scene. Preserve box/equipment instance identity and guarantee repeated input cannot reroll, consume, or award twice.

## Dependencies

- RUN-001 merged immutable result payload.
- DEV-001 merged exact unopened-box Results route.
- BOXSCENE-001 merged `Assets/ShooterMover/Scenes/StrongboxOpening/StrongboxOpening.unity` and controller.
- BOX-001, RAP-001, INV-001, SCR-001, and HUB-001 merged.

## Exclusive owned files and paths

- `Assets/ShooterMover/Runtime/Application/Flow/ResultsStrongbox/**`
- `Assets/ShooterMover/UI/Results/**`
- `Assets/ShooterMover/UI/StrongboxOpeningFlow/**`
- `Assets/ShooterMover/Scenes/Flow/Results/**`
- `Assets/ShooterMover/Tests/EditMode/Flow/ResultsStrongbox/**`
- `Assets/ShooterMover/Tests/PlayMode/Flow/ResultsStrongbox/**`
- `Assets/ShooterMover/Art/UI/Results/**`
- `docs/architecture/flow/RESULTS_STRONGBOX_FLOW_V1.md`

## Forbidden paths and changes

- Do not edit the existing StrongboxOpening scene/controller.
- Do not edit or duplicate BOX/RAP/INV/SCR authorities/algorithms.
- Listing/displaying a box must not consume/open it.
- Do not reroll on reload, repeated click, Back, or retry.
- Do not edit gameplay/DEV paths, ProjectSettings, Packages, shared asmdefs, generated/context/dispatch files.

## Acceptance criteria

- [ ] Results projects the immutable run result and lists every pending unopened box by exact instance ID, tier, and display data.
- [ ] Separate boxes remain separate even when rewards share one weapon definition.
- [ ] Selecting one creates one immutable route command referencing that exact box and run/result identity.
- [ ] Existing BOX/RAP/INV authorities perform generation/opening/application; UI grants nothing.
- [ ] Repeated confirm/navigation/reload/retry/Back cannot award twice.
- [ ] Returning to Results marks only the opened exact instance while preserving other pending boxes.
- [ ] Invalid/stale/already-opened selection rejects clearly without mutation.
- [ ] Tests cover zero/one/multiple boxes, duplicate definitions, exact identity, repeat input, interrupted retry, return state, and Main Menu route.

## Focused Unity test commands

```powershell
$Unity = "C:\Program Files\Unity\Hub\Editor\6000.3.19f1\Editor\Unity.exe"
New-Item -ItemType Directory -Force artifacts/test-results, artifacts/logs | Out-Null
& $Unity -batchmode -nographics -projectPath "$PWD" -runTests -testPlatform EditMode -testFilter "ShooterMover.Tests.EditMode.Flow.ResultsStrongbox" -testResults "artifacts/test-results/BOXUI-001-EditMode.xml" -logFile "artifacts/logs/BOXUI-001-EditMode.log" -quit
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
& $Unity -batchmode -nographics -projectPath "$PWD" -runTests -testPlatform PlayMode -testFilter "ShooterMover.Tests.PlayMode.Flow.ResultsStrongbox" -testResults "artifacts/test-results/BOXUI-001-PlayMode.xml" -logFile "artifacts/logs/BOXUI-001-PlayMode.log" -quit
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
```

Passing claims require both XML files with zero failures.

## Manual proof checklist

- [ ] Enter Results with multiple exact unopened boxes from DEV-001.
- [ ] Verify artwork is a backplate and pending-box rows/buttons are real UI.
- [ ] Open one exact instance through the existing four-stage scene.
- [ ] Spam Confirm/Back/re-enter and verify one opening/application.
- [ ] Return and verify only that box is marked opened.
- [ ] Open another box yielding a duplicate definition and verify a distinct equipment instance.

## Merge order

Second wave after RUN-001 and DEV-001; merge before DEMO-005.

## Asset requirements

Copy `source-assets/user-intake/menu_screens/results_screen.png` from exact asset commit `0b1b654c1fb8cf8208904eb55041fde954cfb560` into `Assets/ShooterMover/Art/UI/Results/**`. Real controls remain overlays.

## Known limitations

- No bulk-open or advanced reveal polish.
- UI does not own run truth or reward generation.

## Parallel dispatch safety

Blocked until RUN/DEV merge; safe afterward with room/map and hub sub-screens.
