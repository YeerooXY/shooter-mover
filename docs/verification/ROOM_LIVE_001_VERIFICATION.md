# ROOM-LIVE-001 Verification

## Launch boundary

- Repository: `YeerooXY/shooter-mover`
- Branch: `agent/room-live-001-runtime`
- Exact launch/base SHA: `f00482a2a86232275517e8b992a9f290be07a152`
- PR base: `main`

## Focused Unity commands

EditMode:

```text
Unity -batchmode -projectPath . -runTests -testPlatform EditMode -testFilter ShooterMover.Tests.EditMode.Missions.Rooms.RoomLiveRuntimeAuthorityTests -testResults Temp/room-live-001-editmode.xml
```

PlayMode:

```text
Unity -batchmode -projectPath . -runTests -testPlatform PlayMode -testFilter ShooterMover.Tests.PlayMode.Missions.Rooms.RoomRuntimeComposition2DTests -testResults Temp/room-live-001-playmode.xml
```

`-quit` is omitted because Unity Test Framework exits after `-runTests`.

## Coverage

EditMode coverage includes:

- ten concrete enemy instances with one shared definition;
- required occupant blocking clear;
- closed door before completion;
- exactly-once door opening under operation replay;
- defeated occupant and collected-drop retention after return;
- authored-state restoration after restart;
- unknown runtime exits failing closed;
- malformed next-room links failing closed;
- terminal-room return/final exits;
- deterministic canonical JSON and graph fingerprints.

PlayMode coverage includes:

- definition-driven placement and door creation;
- Room 1 moving-droid identity and Room 2 turret identity;
- completion-gated forward, return, and final doors;
- return to Room 1 without enemy respawn;
- restart presentation reconstruction;
- final-exit event emission only after Room 2 completion.

## Checks performed in the implementation environment

- all added C# compilation units parse without syntax-error nodes using a C# grammar parser;
- all literal `StableId.Parse(...)` values satisfy the repository's exact-one-dot canonical
  format and component rules;
- generated connection, door-link, and internal operation IDs use deterministic SHA-256
  tokens and valid `StableId.Create(...)` components;
- generic room composition contains no Stage 1, moving-droid, turret, or enemy-type branch;
- `Stage1VisibleSliceController.cs` is not changed.

## Unity limitation

Unity is not installed in the execution environment used to prepare this branch. Therefore no
passing EditMode or PlayMode XML result is claimed here. The commands above are the exact
focused commands required to produce `Temp/room-live-001-editmode.xml` and
`Temp/room-live-001-playmode.xml` before merge.
