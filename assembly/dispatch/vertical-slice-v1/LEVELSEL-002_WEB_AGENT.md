# LEVELSEL-002 — Level 1 and Level 2 selection

Use this packet with one isolated GitHub/Unity coding agent.

```text
Repository: YeerooXY/shooter-mover
Task: LEVELSEL-002
Branch: agent/levelsel-002-level-selection
Exact base commit: 645cf24f30ee6c8762214a84060e59e35df67a05
PR base: main
```

Create a fresh exact-base branch and one non-empty draft implementation PR. Do not merge it. Read `AGENTS.md`, current handoff, and the vertical-slice README/ownership/validation files first.

## Objective

Create a routeable level-selection screen driven by separate metadata. Level 1 routes to the existing visible-slice gameplay scene. Level 2 routes to a valid clearly labeled prototype scene. Preserve the immutable HUB route/profile payload and keep level metadata out of UI callback code.

## Dependencies

- HUB-001 merged shared route/profile payload.
- PLAY-001 route may be consumed after merge; do not create a competing route contract.

## Exclusive owned files and paths

- `Assets/ShooterMover/Runtime/Application/Flow/LevelSelection/**`
- `Assets/ShooterMover/Content/Definitions/Levels/Selection/**`
- `Assets/ShooterMover/UI/LevelSelection/**`
- `Assets/ShooterMover/Scenes/Flow/LevelSelection/**`
- `Assets/ShooterMover/Scenes/Prototypes/Level2Prototype.unity`
- `Assets/ShooterMover/Scenes/Prototypes/Level2Prototype.unity.meta`
- `Assets/ShooterMover/Tests/EditMode/Flow/LevelSelection/**`
- `Assets/ShooterMover/Tests/PlayMode/Flow/LevelSelection/**`
- `Assets/ShooterMover/Art/UI/LevelSelection/**`
- `docs/architecture/flow/LEVEL_SELECTION_V1.md`

## Forbidden paths and changes

- Do not edit `Assets/ShooterMover/Scenes/Prototypes/Stage1VisibleSlice.unity` or its controller.
- Do not edit MENU/HUB/Play/Results/gameplay/room/map/inventory/economy/reward/XP owners.
- Do not edit `ProjectSettings/EditorBuildSettings.asset`, Packages, shared asmdefs, generated/context/dispatch files.
- Do not encode level IDs and scene paths exclusively in button callbacks.

## Acceptance criteria

- [ ] Level metadata has stable ID, display data, scene route, availability/prototype state, recommended metadata, and deterministic catalog fingerprint.
- [ ] Validation rejects duplicate IDs, missing routes, and invalid live/prototype combinations.
- [ ] Level 1 resolves exactly to `Assets/ShooterMover/Scenes/Prototypes/Stage1VisibleSlice.unity`.
- [ ] Level 2 resolves to the owned prototype scene, visibly labeled Prototype, with a safe Back route.
- [ ] Both routes carry the exact immutable profile/loadout payload.
- [ ] Repeated input causes at most one route/load.
- [ ] Tests prove metadata/UI separation, exact routes, invalid catalog rejection, Back, and state retention.

## Focused Unity test commands

```powershell
$Unity = "C:\Program Files\Unity\Hub\Editor\6000.3.19f1\Editor\Unity.exe"
New-Item -ItemType Directory -Force artifacts/test-results, artifacts/logs | Out-Null
& $Unity -batchmode -nographics -projectPath "$PWD" -runTests -testPlatform EditMode -testFilter "ShooterMover.Tests.EditMode.Flow.LevelSelection" -testResults "artifacts/test-results/LEVELSEL-002-EditMode.xml" -logFile "artifacts/logs/LEVELSEL-002-EditMode.log" -quit
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
& $Unity -batchmode -nographics -projectPath "$PWD" -runTests -testPlatform PlayMode -testFilter "ShooterMover.Tests.PlayMode.Flow.LevelSelection" -testResults "artifacts/test-results/LEVELSEL-002-PlayMode.xml" -logFile "artifacts/logs/LEVELSEL-002-PlayMode.log" -quit
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
```

Do not claim passing proof unless both XML files exist and report zero failed tests.

## Manual proof checklist

- [ ] Navigate Solo → Level Select with a populated payload.
- [ ] Verify supplied art is a backplate only and real controls align/read correctly.
- [ ] Open Level 2, confirm a labeled prototype scene loads, and return safely.
- [ ] Open Level 1 and confirm the exact existing scene loads once.
- [ ] Verify selected profile/loadout identity reaches the destination boundary.

## Merge order

Merge after HUB-001; preferably after PLAY-001 for full route proof. May be developed in parallel after HUB with no shared-file ownership.

## Asset requirements

Copy `source-assets/user-intake/menu_screens/level_selection.png` from exact asset commit `0b1b654c1fb8cf8208904eb55041fde954cfb560` into `Assets/ShooterMover/Art/UI/LevelSelection/**`. Consume source read-only; real Unity controls remain overlays.

## Known limitations

- Player-build scene registration is outside ownership.
- Level 2 is intentionally a prototype/stub.

## Parallel dispatch safety

Safe after HUB-001. Coordinate route API only; do not edit PLAY-001 files.
