# ROOM-DATA-001 verification

## Launch boundary

- Repository: `YeerooXY/shooter-mover`
- Branch: `agent/room-data-001-json-room-content`
- Exact launch SHA: `8b4d4e401827735d10082f6f3b04a5d1b2103920`
- Target: `main`
- Unity baseline: `6000.3.19f1`

## Scope

This change adds a JSON authoring/import foundation. It does not cut the current playable Level 1 over to JSON-spawned enemies and does not modify scenes, prefabs, player damage, enemy combat, XP, drops, mission results, or existing ROOM-LIVE authority code.

## Static checks completed

- Branch comparison is ahead-only from the exact launch SHA.
- All changed production paths are additions under room-content application, content-definition, Unity authoring-adapter, test, and documentation paths.
- Existing `AuthorableRoomGraphDefinitionV1` remains the compiled ROOM-LIVE contract.
- Enemy object/type and level remain distinct sidecar facts.
- Ordinary placements require no authored ID.
- Optional IDs are used only for cross-references.
- Generated concrete IDs are deterministic from room and placement facts.
- Tile fills expand inclusively and are bounded to 10,000 tiles per room.
- XP, drops, kill attribution, kill catalogs, and Results are not implemented in the room importer.

## Focused Unity proof to run

```text
Unity -batchmode -nographics -projectPath . -runTests -testPlatform EditMode -testFilter ShooterMover.Tests.EditMode.Missions.Rooms.RoomContentJsonImporterV1Tests -testResults Temp/room-data-001-editmode.xml -logFile Temp/room-data-001-editmode.log
```

Do not add `-quit`; `-runTests` exits Unity after the test run.

## Focused authored coverage

- two anonymous moving droids share one type but preserve levels 1 and 2;
- concrete generated identities remain distinct;
- reordering different anonymous placements preserves identity and fingerprint;
- an inclusive `[0,0]` to `[2,1]` tile fill expands to six unique placements;
- encounter data, not enemy placement data, owns optional room-clear role;
- room bounds, doors, exits, and spawn-kind targeting compile into the existing runtime graph;
- unknown object IDs fail without producing a partial content bundle.

## Manual authoring acceptance

1. Open the sample JSON under `Assets/ShooterMover/Content/Definitions/Missions/Rooms/Json/Level1`.
2. Add another anonymous moving droid with a different `level` and position.
3. Import through `JsonRoomContentDefinition2D` and confirm both enemy sidecars carry `enemy.moving-droid`, distinct levels, and distinct generated instance IDs.
4. Reorder the two nonidentical anonymous entries and confirm their generated IDs and full content fingerprint remain unchanged.
5. Change a tile fill rectangle and confirm the expanded visual placement count matches the inclusive area.
6. Add a second spawn of the same kind to a target room and confirm a kind-only door link rejects as ambiguous until an optional spawn ID is supplied.
7. Confirm the current playable Level 1 remains unchanged; live arbitrary-enemy spawning is a separate factory/cutover task.

## Verification status

Connector-side source and comparison review completed. Unity compilation and test execution are unavailable in this connected environment and are not claimed.
