# ROOM-001 — Room graph and mission layout model

Use this packet with one isolated GitHub/Unity coding agent.

```text
Repository: YeerooXY/shooter-mover
Task: ROOM-001
Branch: agent/room-001-room-graph-model
Exact base commit: 645cf24f30ee6c8762214a84060e59e35df67a05
PR base: main
```

Create a fresh exact-base branch and one non-empty draft PR. Do not merge it. Read `AGENTS.md`, current handoff, and the vertical-slice README/ownership/validation files first.

## Objective

Define an engine-independent deterministic room graph and mission-layout model with stable room identities, connections, entry/exit identities, door links, visited/completed state, and validation. Ship authored data for a two-room Level 1 graph without editing gameplay scenes.

## Dependencies

- Merged StableId, room projection/mission-message, and placed-object identity foundations.

## Exclusive owned files and paths

- `Assets/ShooterMover/Runtime/Domain/Missions/Rooms/**`
- `Assets/ShooterMover/Runtime/Contracts/Missions/Rooms/**`
- `Assets/ShooterMover/Runtime/Application/Missions/Rooms/**`
- `Assets/ShooterMover/Content/Definitions/Missions/Rooms/**`
- `Assets/ShooterMover/Tests/EditMode/Missions/Rooms/**`
- `docs/architecture/missions/ROOM_GRAPH_V1.md`

## Forbidden paths and changes

- Do not edit existing mission/room contracts outside the owned leaf.
- Do not edit scenes, prefabs, doors, authoring components, map UI, transitions, save transport, gameplay, ProjectSettings, Packages, shared asmdefs, generated/context/dispatch files.
- Do not make UI coordinates or GameObject names the source of room truth.

## Acceptance criteria

- [ ] Definition includes stable mission-layout, room, connection, exit, entry, and optional door-link identities; directionality; lock/availability metadata; and deterministic fingerprints.
- [ ] Runtime state tracks current, visited, completed, and exit-availability state separately from immutable definitions.
- [ ] A valid two-room Level 1 graph is traversable from configured start to terminal room.
- [ ] Validation rejects duplicate IDs, missing endpoints, dangling door links, invalid exits/self-links, mismatched reverse links, unreachable required rooms, and invalid start/terminal rooms.
- [ ] Snapshot/export/import is deterministic, definition-fingerprint-bound, and atomic on rejection.
- [ ] APIs are Unity-independent and usable by gameplay, map, save, and authoring consumers.
- [ ] Tests cover valid graph, every required validation failure, visit/complete transitions, canonical ordering, and snapshot round trip.

## Focused Unity test command

```powershell
$Unity = "C:\Program Files\Unity\Hub\Editor\6000.3.19f1\Editor\Unity.exe"
New-Item -ItemType Directory -Force artifacts/test-results, artifacts/logs | Out-Null
& $Unity -batchmode -nographics -projectPath "$PWD" -runTests -testPlatform EditMode -testFilter "ShooterMover.Tests.EditMode.Missions.Rooms" -testResults "artifacts/test-results/ROOM-001-EditMode.xml" -logFile "artifacts/logs/ROOM-001-EditMode.log" -quit
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
```

Do not claim passing proof unless the XML exists and reports zero failed tests.

## Manual proof checklist

- [ ] Inspect the authored Level 1 graph and verify exactly two stable rooms plus configured links.
- [ ] Produce a textual/debug projection of start room, exit, destination, door link, visited, and completed state.
- [ ] Introduce duplicate/missing IDs and verify actionable validation.
- [ ] Export/import state and verify identical fingerprint/current room.

## Merge order

First wave; merge before ROOMTRANS-001, MAP-001, and DEMO-005.

## Asset requirements

None; model/data only.

## Known limitations

- No Unity room placement, transition animation, or complete save transport.
- The two-room content definition is not a gameplay-scene edit.

## Parallel dispatch safety

Safe immediately in parallel with MENU-002, LEVELDES-001, XP-002, DROP-001, and WEAPON-DATA-001.
