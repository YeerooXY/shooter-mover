# LEVEL-GRID-V2-RUNTIME-001 verification record

## Branch provenance

- requested branch: `agent/level-grid-v2-runtime-001`
- exact refreshed starting `main` SHA: `7defe07dfea16a4435567f0dc053b195d6b5705e`
- target: `main`
- pull request policy: draft only; no merge; no auto-merge

## Implemented route

```text
level.authored-json-combat-loop-test
→ existing PlayableLevel scene
→ compiled CombatLoopTestRoomContent resource
→ room.combat-loop-starter
→ room.combat-loop-single
→ room.combat-loop-double
→ final exit
```

Authored enemy counts are exactly `0 / 1 / 2`, using the existing `enemy.moving-droid` content definition and the existing Level 1 enemy catalogue.

## Static verification performed in the connected environment

- inspected merged PR #333 and current `main` authoring/runtime boundaries;
- inspected `RoomContentJsonImporterV1`, its DTO contract, object catalogue boundary, `JsonRoomContentDefinition2D`, `JsonRoomRuntimeBootstrap2D`, the generic playable-level controller and production level catalogue;
- confirmed the compiler output remains a `RoomContentJsonPackageV1` and is validated through the existing importer before Unity assets are written;
- generated and parsed every tracked V2 and generated V1 JSON document;
- verified the V2 room index is `room ID → coordinate+slot folder`, while runtime links use only room ID + door ID;
- verified exact connections are starter-east ↔ single-west and single-east ↔ double-west;
- verified the only unconnected traversable endpoint is the declared double-room final exit;
- verified generated arrivals are one unit inward at `x = 10` or `x = -10`, inside 24×14 room bounds;
- verified the generated V1 route targets exact arrival IDs rather than spawn kinds;
- verified default encounter compilation produces `all-enemies` and forward/final `room-complete` gates;
- verified the compiled runtime asset references the manifest and all 15 generated room documents;
- verified the production catalogue selects the existing gameplay scene and `Level1EnemyCatalog`.

## EditMode coverage authored

`LevelGridV2CompilerTests` covers:

- valid three-room V2 compile through the existing V1 importer;
- deterministic destination arrival generation;
- missing and empty encounter defaults;
- malformed encounter rejection;
- unknown room reference rejection;
- duplicate coordinate+slot rejection;
- endpoint reuse rejection;
- unresolved traversable-door rejection;
- missing start-room rejection;
- inaccessible room rejection;
- invalid final-exit rejection;
- nested endpoint hierarchy resolving relative to the owning room.

## PlayMode coverage authored

`LevelGridV2CompiledAssetPlayModeTests` loads the tracked resource after a runtime frame, imports it through `JsonRoomContentDefinition2D`, and verifies the build-included package contains the exact three authored Droid placements.

## Unity validation status

Not performed in this connected environment:

- Unity 6000.3.19f1 import/domain reload;
- C# compilation;
- Console missing-script, missing-assembly and serialization scan;
- EditMode test execution/XML result;
- PlayMode test execution/XML result;
- scene-based gameplay.

The pull request must remain draft until these checks run in a real Unity checkout.

## Manual gameplay checklist — pending

1. Open the three-room graph in Unity.
2. Export the compiler-ready V2 package.
3. Compile it with **Compile Tracked Combat Loop Grid V2**.
4. Enter **COMBAT LOOP TEST** through normal Level Selection.
5. Confirm spawn in `STARTER ROOM` at `[-9, 0]`.
6. Traverse the exact east door into `SINGLE CONTACT` and arrive at its west-door anchor `[-10, 0]`.
7. Confirm one Mobile Blaster Droid and that the east progression door is closed before clear.
8. Defeat the Droid and confirm the east door opens through existing room-complete authority.
9. Enter `CROSSFIRE` at its west-door anchor `[-10, 0]`.
10. Confirm two Mobile Blaster Droids and final-exit gating.
11. Defeat both and trigger the final exit exactly once.
12. Restart the application and repeat without exporting or regenerating scene-local content.

## No replacement authorities

This change does not add another enemy system, weapon system, room-clear poller, traversal authority, completion authority, gameplay scene, or runtime filesystem reader. It compiles V2 into the current room runtime input and reuses all existing combat-loop authorities.
