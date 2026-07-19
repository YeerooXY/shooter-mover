# ROOM-LIVE-001 Verification

## Launch boundary

- Repository: `YeerooXY/shooter-mover`
- Branch: `agent/room-live-001-runtime`
- Exact launch/base SHA: `f00482a2a86232275517e8b992a9f290be07a152`
- PR base: `main`
- Controller and scene edits: forbidden and absent

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

## EditMode coverage

- ten concrete enemy instances sharing one reusable definition remain independent;
- required enemy remains alive: not clear, not complete, gate closed;
- configured completion condition opens its door exactly once under replay;
- Room 2 return and final doors use different condition IDs;
- clear, visited, current, active, and mission-completed facts remain distinct;
- early terminal fact for an unvisited room does not complete it or open doors;
- defeated occupant and collected-drop retention after return;
- authored-state restoration and lifecycle-generation increment after restart;
- unknown runtime exits and malformed definition links fail closed;
- deterministic canonical serialization and explicit authored exit type;
- no public mutable `OccupancyAuthority`, `MissionLayout`, or concrete composition
  `Authority` escape hatch.

## PlayMode coverage

- definition-driven room, placement, and door construction;
- real `MobileBlasterDroidRuntime2D` component instantiated for Room 1;
- real `BlasterTurretPackage` component instantiated for Room 2;
- real EN-002 destroyed state produced by lethal `HitMessage` application;
- generic package-independent terminal relay forwards exact room instance identity;
- forward door remains closed before the real droid terminal fact;
- Room 2 return door opens on its independent entered-room condition;
- final door remains closed until the real turret terminal fact;
- return to Rooms 1 and 2 does not respawn defeated real enemy instances;
- final-exit event after Room 2 completion;
- restart reconstructs the authored real droid instance under lifecycle generation 2;
- closed configured gate rejects traversal.

## Static checks performed in the implementation environment

- every added/updated C# compilation unit parses without syntax-error nodes using a C#
  grammar parser;
- every literal `StableId.Parse(...)` value satisfies the repository's exact-one-dot
  canonical format;
- generated connection, door-link, relay-operation, and internal-operation IDs use
  deterministic SHA-256 tokens and canonical `StableId.Create(...)` components;
- generic room runtime files contain no Stage 1, moving-droid, Blaster Turret, room-number,
  or enemy-type branch;
- `RoomRuntimeComposition2D` is reduced from 609 lines to a thin composition boundary;
- condition evaluation, door gating, retained facts, traversal, replay, projection
  construction, and presentation lifecycle are separate sealed collaborators;
- `Stage1VisibleSliceController.cs` remains byte-identical to `main`;
- the published branch diff is audited before PR update.

## Unity limitation and merge gate

Unity is not installed in the implementation environment, and no applicable GitHub
Actions Unity run is available. Therefore this change does **not** claim passing EditMode
or PlayMode XML.

Before the draft is marked ready, run the two focused commands above and attach or commit:

- `Temp/room-live-001-editmode.xml`;
- `Temp/room-live-001-playmode.xml`.
