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

## Targeted self-audit repairs

A hostile self-review identified two gameplay blockers and several strictness gaps. The branch now repairs them as follows:

- progression/return and default door gating no longer depend on room coordinates;
- the stable connection `from` endpoint is progression and the `to` endpoint is return, so moving rooms cannot reverse gameplay;
- playable export migrates existing room folders by stable `room_id`, preserving enemies, props, decor and encounter sidecars across coordinate changes;
- deleted-room folders are removed from the disposable stage and conflicting ownership is rejected;
- the staged export must compile and pass the existing V1 importer before replacing the previous destination;
- present non-empty encounters must be complete schema-V2 documents;
- unknown encounter door IDs, duplicate exact-door rules and invalid rule selectors/gates are rejected;
- null required sidecar arrays are rejected instead of normalized to empty;
- map-node coordinates and slots must match the authoritative room index.

## Static verification performed in the connected environment

- inspected merged PR #333 and current `main` authoring/runtime boundaries;
- inspected `RoomContentJsonImporterV1`, its DTO contract, object catalogue boundary, `JsonRoomContentDefinition2D`, `JsonRoomRuntimeBootstrap2D`, the generic playable-level controller and production level catalogue;
- confirmed the compiler output remains a `RoomContentJsonPackageV1` and is validated through the existing importer before Unity assets are written;
- generated and parsed every tracked V2 and generated V1 JSON document;
- verified the V2 room index is `room ID → coordinate+slot folder`, while runtime links use only stable room ID + door ID;
- verified exact connections are starter-east → single-west and single-east → double-west, with source endpoints retaining progression semantics regardless of grid placement;
- verified the only unconnected traversable endpoint is the declared double-room final exit;
- verified generated arrivals are one unit inward at `x = 10` or `x = -10`, inside 24×14 room bounds;
- verified the generated V1 route targets exact arrival IDs rather than spawn kinds;
- verified default encounter compilation produces `all-enemies`, source/final `room-complete` gates and destination `always` gates;
- verified the compiled runtime asset references the manifest and all 15 generated room documents;
- verified the production catalogue selects the existing gameplay scene and `Level1EnemyCatalog`;
- inspected the stable-ID folder migration and transactional staged compile/import gate.

## EditMode coverage authored

`LevelGridV2CompilerTests` covers:

- valid three-room V2 compile through the existing V1 importer;
- deterministic destination arrival generation;
- missing and exactly empty encounter defaults;
- malformed and partial encounter rejection;
- unknown encounter-door rejection;
- null required-array rejection;
- unknown room reference rejection;
- duplicate coordinate+slot rejection;
- map/index coordinate disagreement rejection;
- endpoint reuse rejection;
- unresolved traversable-door rejection;
- missing start-room rejection;
- inaccessible room rejection;
- invalid final-exit rejection;
- coordinate-independent progression/return and gate semantics;
- stable-ID folder migration with sidecar preservation;
- nested endpoint hierarchy resolving relative to the owning room.

## PlayMode coverage authored

`LevelGridV2CompiledAssetPlayModeTests` loads the tracked resource after a runtime frame, imports it through `JsonRoomContentDefinition2D`, and verifies the build-included package contains the exact three authored Droid placements.

This test remains an asset/import smoke test. The full traversal, combat gating, final completion and restart route still requires the manual and broader runtime acceptance below.

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
2. Move at least one connected room to the opposite side of its neighbour, export, and confirm the same progression/return doors remain gated.
3. Confirm that room's enemies, props, decor and encounter sidecars remain attached after the move.
4. Compile the tracked compiler-ready V2 package.
5. Enter **COMBAT LOOP TEST** through normal Level Selection.
6. Confirm spawn in `STARTER ROOM` at `[-9, 0]`.
7. Traverse the exact east door into `SINGLE CONTACT` and arrive at its west-door anchor `[-10, 0]`.
8. Confirm one Mobile Blaster Droid and that the east progression door is closed before clear.
9. Defeat the Droid and confirm the east door opens through existing room-complete authority.
10. Enter `CROSSFIRE` at its west-door anchor `[-10, 0]`.
11. Confirm two Mobile Blaster Droids and final-exit gating.
12. Defeat both and trigger the final exit exactly once.
13. Restart the application and repeat without exporting or regenerating scene-local content.

## No replacement authorities

This change does not add another enemy system, weapon system, room-clear poller, traversal authority, completion authority, gameplay scene, or runtime filesystem reader. It compiles V2 into the current room runtime input and reuses all existing combat-loop authorities.
