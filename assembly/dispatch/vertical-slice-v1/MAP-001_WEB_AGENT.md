# MAP-001 — Mission map screen

Use this packet with one isolated GitHub/Unity coding agent.

```text
Repository: YeerooXY/shooter-mover
Task: MAP-001
Branch: agent/map-001-mission-map-presentation
Exact base commit: 645cf24f30ee6c8762214a84060e59e35df67a05
PR base: main
```

Create a fresh dependency-complete branch and one non-empty draft PR. Do not merge it.

## Objective

Add a mission-map presentation layer driven entirely by ROOM-001 definition and runtime state. Show current, visited, available, locked/closed, and completed room/exit state without copying graph truth into UI assets or callbacks.

## Dependencies

- ROOM-001 merged.
- ROOMTRANS-001 may develop concurrently; final integration proof consumes its public state/facts.

## Exclusive owned files and paths

- `Assets/ShooterMover/Runtime/Application/Missions/MapPresentation/**`
- `Assets/ShooterMover/UI/MissionMap/**`
- `Assets/ShooterMover/Tests/EditMode/Missions/MapPresentation/**`
- `Assets/ShooterMover/Tests/PlayMode/Missions/MissionMap/**`
- `docs/architecture/missions/MISSION_MAP_PRESENTATION_V1.md`

## Forbidden paths and changes

- Do not edit ROOM-001 or ROOMTRANS-001 truth, gameplay scenes/HUD, route payload, doors, level authoring, save authority, inventory/economy/reward/XP.
- Do not store a second graph or duplicate connections in UI/prefabs.
- Do not edit ProjectSettings, Packages, shared asmdefs, generated/context/dispatch files.

## Acceptance criteria

- [ ] View model is a pure deterministic projection from ROOM definition/state plus display-layout metadata.
- [ ] Distinguishes current, visited, completed, available, and locked/closed states.
- [ ] Unknown/unvisited rooms obey explicit reveal policy and do not expose secrets automatically.
- [ ] New immutable room snapshots/facts update UI without mutating room state.
- [ ] Layout metadata references stable room IDs and rejects missing/duplicate coordinates/references.
- [ ] Map opens/closes and returns to gameplay without state changes.
- [ ] Tests cover all visual states, reveal policy, updates, invalid metadata, and no duplicate graph truth.

## Focused Unity test commands

```powershell
$Unity = "C:\Program Files\Unity\Hub\Editor\6000.3.19f1\Editor\Unity.exe"
New-Item -ItemType Directory -Force artifacts/test-results, artifacts/logs | Out-Null
& $Unity -batchmode -nographics -projectPath "$PWD" -runTests -testPlatform EditMode -testFilter "ShooterMover.Tests.EditMode.Missions.MapPresentation" -testResults "artifacts/test-results/MAP-001-EditMode.xml" -logFile "artifacts/logs/MAP-001-EditMode.log" -quit
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
& $Unity -batchmode -nographics -projectPath "$PWD" -runTests -testPlatform PlayMode -testFilter "ShooterMover.Tests.PlayMode.Missions.MissionMap" -testResults "artifacts/test-results/MAP-001-PlayMode.xml" -logFile "artifacts/logs/MAP-001-PlayMode.log" -quit
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
```

Passing claims require both XML files with zero failures.

## Manual proof checklist

- [ ] Open map in Room 1 and verify current/available/locked indicators.
- [ ] Transition in a fixture and verify Room 1 visited, Room 2 current.
- [ ] Mark completion and verify completed presentation.
- [ ] Lock/close an exit and verify distinct presentation.
- [ ] Compare room-state fingerprints before/after map open/close; unchanged.

## Merge order

Second wave after ROOM-001; final proof after ROOMTRANS-001. Merge before DEMO-005.

## Asset requirements

No map artwork supplied; use accessible code-owned shapes/icons.

## Known limitations

- Authored coordinates are supported; a complete map editor is not.
- Reveal policy remains bounded to existing room metadata.

## Parallel dispatch safety

Safe with ROOMTRANS-001 after ROOM-001; ownership is projection/UI only.
