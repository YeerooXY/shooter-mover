# PLAY-001 — Solo and multiplayer selection

Use this packet with one isolated GitHub/Unity coding agent.

```text
Repository: YeerooXY/shooter-mover
Task: PLAY-001
Branch: agent/play-001-play-selection
Exact base commit: 645cf24f30ee6c8762214a84060e59e35df67a05
PR base: main
```

Create a fresh exact-base branch and one non-empty draft implementation PR. Do not merge it. Read `AGENTS.md`, the current handoff, and the vertical-slice README/ownership/validation files first.

## Objective

Add a real Play screen carrying the immutable HUB route/profile payload. Solo continues to Level Select. Multiplayer is a bounded unavailable/prototype route with clear feedback and no networking implementation or fake connection state.

## Dependencies

- HUB-001 merged shared route/profile payload.

## Exclusive owned files and paths

- `Assets/ShooterMover/Runtime/Application/Flow/PlaySelection/**`
- `Assets/ShooterMover/Content/Definitions/Flow/PlayModes/**`
- `Assets/ShooterMover/UI/PlaySelection/**`
- `Assets/ShooterMover/Scenes/Flow/PlaySelection/**`
- `Assets/ShooterMover/Tests/EditMode/Flow/PlaySelection/**`
- `Assets/ShooterMover/Tests/PlayMode/Flow/PlaySelection/**`
- `docs/architecture/flow/PLAY_SELECTION_V1.md`

## Forbidden paths and changes

- Do not create sockets, lobbies, matchmaking, transports, accounts, server code, or multiplayer gameplay.
- Do not edit HUB contracts, Level Select implementation, inventory/economy/rewards/XP, gameplay scenes, ProjectSettings, Packages, shared asmdefs, generated/context/dispatch files.
- Do not add fake successful multiplayer state.

## Acceptance criteria

- [ ] Screen has real Solo, Multiplayer, and Back actions.
- [ ] Solo emits the accepted Level Select route with the exact immutable incoming payload.
- [ ] Multiplayer emits a deterministic unavailable/prototype result and does not start networking or gameplay.
- [ ] Repeated input cannot emit multiple routes.
- [ ] Back returns to Hub with identical payload/fingerprint.
- [ ] Play-mode metadata is data-driven rather than embedded only in button callbacks.
- [ ] Tests cover Solo, unavailable Multiplayer, Back, repeated input, missing/invalid payload, and identity retention.

## Focused Unity test commands

```powershell
$Unity = "C:\Program Files\Unity\Hub\Editor\6000.3.19f1\Editor\Unity.exe"
New-Item -ItemType Directory -Force artifacts/test-results, artifacts/logs | Out-Null
& $Unity -batchmode -nographics -projectPath "$PWD" -runTests -testPlatform EditMode -testFilter "ShooterMover.Tests.EditMode.Flow.PlaySelection" -testResults "artifacts/test-results/PLAY-001-EditMode.xml" -logFile "artifacts/logs/PLAY-001-EditMode.log" -quit
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
& $Unity -batchmode -nographics -projectPath "$PWD" -runTests -testPlatform PlayMode -testFilter "ShooterMover.Tests.PlayMode.Flow.PlaySelection" -testResults "artifacts/test-results/PLAY-001-PlayMode.xml" -logFile "artifacts/logs/PLAY-001-PlayMode.log" -quit
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
```

Do not claim passing proof unless both XML files exist and report zero failed tests.

## Manual proof checklist

- [ ] Navigate Hub → Play with a populated payload.
- [ ] Choose Multiplayer and verify clear prototype/unavailable feedback and no network/scene activity.
- [ ] Choose Solo and verify one Level Select route carrying the exact payload.
- [ ] Spam Confirm and verify one transition.
- [ ] Use Back and verify state is unchanged.

## Merge order

Merge after HUB-001. May merge in parallel with CHAR-001, INV-002, and LEVELSEL-002.

## Asset requirements

None. Use code-owned UI consistent with the Hub.

## Known limitations

- Multiplayer is intentionally unavailable; no networking implementation is authorized.

## Parallel dispatch safety

Safe after HUB-001 because the owned application/UI/scene/test subtree is isolated.
