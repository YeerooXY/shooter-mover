# ROOMTRANS-001 — Moving between rooms

Use this packet with one isolated GitHub/Unity coding agent.

```text
Repository: YeerooXY/shooter-mover
Task: ROOMTRANS-001
Branch: agent/roomtrans-001-room-transitions
Exact base commit: 645cf24f30ee6c8762214a84060e59e35df67a05
PR base: main
```

Create a fresh dependency-complete branch and one non-empty draft PR. Do not merge it. Verify ROOM-001 and HUB-001 are merged before editing.

## Objective

Implement room-to-room transition orchestration through configured ROOM-001 exits/door links. Preserve player route/profile state and mission room state, support deterministic restart, and keep transition authority independent from fades, animations, and map presentation.

## Dependencies

- ROOM-001 merged.
- HUB-001 merged immutable route/profile payload.
- Merged reusable door and placed-object identity/runtime contracts.
- LEVELDES-001 may be consumed after merge, but core transition tests must remain independent.

## Exclusive owned files and paths

- `Assets/ShooterMover/Runtime/Application/Missions/RoomTransitions/**`
- `Assets/ShooterMover/Runtime/UnityAdapters/Missions/RoomTransitions/**`
- `Assets/ShooterMover/Tests/EditMode/Missions/RoomTransitions/**`
- `Assets/ShooterMover/Tests/PlayMode/Missions/RoomTransitions/**`
- `docs/architecture/missions/ROOM_TRANSITIONS_V1.md`

## Forbidden paths and changes

- Do not edit ROOM-001 definitions/contracts/state, door packages, map UI, scenes, player controller, inventory/economy/reward/XP authorities, or presentation effects.
- Do not load by GameObject name, hierarchy order, runtime GUID, or Unity instance ID.
- Do not edit ProjectSettings, Packages, shared asmdefs, generated/context/dispatch files.

## Acceptance criteria

- [ ] Command contains stable mission/run/current-room/exit identity and expected state fingerprint/sequence.
- [ ] Admission validates current room, exit, door/lock/open state, destination/entry, and duplicate/conflicting command identity.
- [ ] Applied transition changes current room, records visit state, preserves history and exact route/profile payload.
- [ ] Exact replay is deterministic no-change; conflicting duplicate rejects.
- [ ] Restart explicitly restores configured initial/checkpoint state without duplicate facts.
- [ ] Unity adapter binds configured authoring/door exits to application service without owning fades/animation.
- [ ] Tests cover two-way/one-way links, locked/closed exits, invalid exits, duplicate/conflict, restart/import, and preserved state.

## Focused Unity test commands

```powershell
$Unity = "C:\Program Files\Unity\Hub\Editor\6000.3.19f1\Editor\Unity.exe"
New-Item -ItemType Directory -Force artifacts/test-results, artifacts/logs | Out-Null
& $Unity -batchmode -nographics -projectPath "$PWD" -runTests -testPlatform EditMode -testFilter "ShooterMover.Tests.EditMode.Missions.RoomTransitions" -testResults "artifacts/test-results/ROOMTRANS-001-EditMode.xml" -logFile "artifacts/logs/ROOMTRANS-001-EditMode.log" -quit
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
& $Unity -batchmode -nographics -projectPath "$PWD" -runTests -testPlatform PlayMode -testFilter "ShooterMover.Tests.PlayMode.Missions.RoomTransitions" -testResults "artifacts/test-results/ROOMTRANS-001-PlayMode.xml" -logFile "artifacts/logs/ROOMTRANS-001-PlayMode.log" -quit
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
```

Passing claims require both XML files with zero failures.

## Manual proof checklist

- [ ] Use a task-owned two-room test fixture.
- [ ] Enter an unavailable exit and verify no state/movement mutation.
- [ ] Unlock/open the exit, transition once, and verify deterministic destination entry.
- [ ] Return where permitted and verify visited/completed state persists.
- [ ] Quick-restart repeatedly and verify initial state/no duplicate facts.

## Merge order

Second wave after ROOM-001 and HUB-001; merge before DEMO-005. MAP-001 may develop concurrently after ROOM-001.

## Asset requirements

None. Presentation effects are separate.

## Known limitations

- Generalized streaming/additive-loading and transition polish are outside V1.

## Parallel dispatch safety

Safe with MAP/UI/DEV work only after dependencies merge; exact paths are isolated.
