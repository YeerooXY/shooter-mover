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

## First targeted self-audit repairs

The first hostile self-review identified two gameplay blockers and several strictness gaps. The branch repairs them as follows:

- progression/return and default door gating no longer depend on room coordinates;
- the stable connection `from` endpoint is progression and the `to` endpoint is return, so moving rooms cannot reverse gameplay;
- playable export migrates existing room folders by stable `room_id`, preserving enemies, props, decor and encounter sidecars across coordinate changes;
- deleted-room folders are removed from the disposable stage and conflicting ownership is rejected;
- the staged export must compile and pass the existing V1 importer before replacing the previous destination;
- present non-empty encounters must be complete schema-V2 documents;
- unknown encounter door IDs, duplicate exact-door rules and invalid rule selectors/gates are rejected;
- null required sidecar arrays are rejected instead of normalized to empty;
- map-node coordinates and slots must match the authoritative room index.

## Second hostile re-audit repairs

A complete second pass found four additional real edge cases and repaired them:

1. **Unrelated destination overwrite** — playable export previously accepted any existing folder, even one whose `level.json` belonged to another level. The exporter now checks exact `level_id` ownership before staging or mutation.
2. **Deleted-coordinate reuse** — a surviving room could not move into a coordinate+slot vacated by a deleted room because the stale folder was removed too late. Deleted-room folders are now removed from the disposable stage before active room folders are assigned.
3. **Broad encounter-rule ambiguity** — a rule matching by `exit_type` or `link_kind` could overlap an automatically generated exact-door default and then be rejected by the retained V1 importer. The compiler now resolves authored matches first, generates defaults only for unmatched doors, rejects overlapping rules and rejects rules that match no traversable runtime door.
4. **Silently ignored player starts** — non-start rooms could contain `player_start` and the compiler silently ignored them; the authoritative start could also be outside room bounds. The compiler now requires exactly one player start on the configured start room and validates that it lies inside the authored bounds.

The same pass also moved optional-enemy validation, finite rotation checks and background/foreground placement validation into the V2 compiler rather than relying only on the downstream compatibility importer.

## Unity-facing static pass

A separate assembly/API-focused pass checked the new tests and editor code against the existing assembly definitions and room-runtime APIs. It found one additional Unity-specific migration defect:

- Unity folder `.meta` files were not moved or deleted with room directories. A moved room could lose its folder GUID, leave orphaned metadata behind, or inherit the GUID of a deleted room when reusing its coordinate.

The migration now moves the room folder and its sibling `.meta` as one ownership unit, deletes deleted-room folder metadata, and removes folder-less stale metadata before assigning a path. Dedicated EditMode tests verify both ordinary room moves and deleted-coordinate reuse preserve only the surviving room’s folder GUID.

## Static verification performed in the connected environment

- inspected merged PR #333 and current `main` authoring/runtime boundaries;
- inspected `RoomContentJsonImporterV1`, including exact door-rule matching and ambiguity behavior;
- confirmed the compiler output remains a `RoomContentJsonPackageV1` and is validated through the existing importer before Unity assets are written;
- checked the new EditMode and PlayMode code against the referenced assembly definitions and established room-runtime test APIs;
- generated and parsed every tracked V2 and generated V1 JSON document;
- verified the V2 room index is `room ID → coordinate+slot folder`, while runtime links use only stable room ID + door ID;
- verified exact connections are starter-east → single-west and single-east → double-west, with source endpoints retaining progression semantics regardless of grid placement;
- verified the only unconnected traversable endpoint is the declared double-room final exit;
- verified generated arrivals are one unit inward at `x = 10` or `x = -10`, inside 24×14 room bounds;
- verified the generated V1 route targets exact arrival IDs rather than spawn kinds;
- verified default encounter compilation produces `all-enemies`, source/final `room-complete` gates and destination `always` gates;
- verified authored broad door rules are matched before defaults using the retained importer’s selector semantics;
- verified the compiled runtime asset references the manifest and all 15 generated room documents;
- verified the production catalogue selects the existing gameplay scene and `Level1EnemyCatalog`;
- inspected stable-ID folder migration, folder metadata ownership, deleted-coordinate reuse, destination ownership and the transactional staged compile/import gate;
- confirmed PR #336 has no submitted reviews, inline review threads or discussion comments at the time of this audit.

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

`LevelGridV2SecondAuditRegressionTests` adds focused coverage for:

- broad authored rules replacing defaults without V1 ambiguity;
- unmatched authored rules being rejected;
- non-start-room player starts being rejected;
- out-of-bounds start positions being rejected;
- unknown optional enemy IDs being rejected by the V2 compiler;
- export destination level-ownership rejection;
- a surviving room reusing a coordinate vacated by a deleted room while retaining only its own sidecars.

`LevelGridV2UnityMetadataRegressionTests` adds focused coverage for:

- moving a room folder while preserving its Unity folder GUID metadata;
- reusing a deleted room’s coordinate without adopting its deleted folder GUID.

## PlayMode coverage authored

`LevelGridV2CompiledAssetPlayModeTests` performs two checks against the tracked build-included resource:

1. imports the package after a runtime frame and verifies the exact three-room, three-Droid graph;
2. constructs the existing `RoomLiveRuntimeAuthorityV1` and exercises the compiled route:
   - zero-enemy starter completion and first door availability;
   - exact destination arrivals;
   - always-open return doors;
   - one-Droid progression gating;
   - two-Droid final-exit gating after the first and second terminal reports;
   - final-exit traversal;
   - a fresh authority instance restoring the initial `0 / 1 / 2` occupant state.

These tests exercise the existing imported room authority, but they do not replace scene-level presentation, collision, weapon combat or full application restart acceptance.

## Unity validation status

Not performed in this connected environment:

- Unity 6000.3.19f1 import/domain reload;
- C# compilation by Unity/Roslyn with project-generated references;
- Console missing-script, missing-assembly and serialization scan;
- EditMode test execution/XML result;
- PlayMode test execution/XML result;
- scene-based gameplay.

The pull request must remain draft until these checks run in a real Unity checkout.

## Manual gameplay checklist — pending

1. Open the three-room graph in Unity.
2. Move at least one connected room to the opposite side of its neighbour, export, and confirm the same progression/return doors remain gated.
3. Confirm that room’s enemies, props, decor, encounter sidecars and folder GUID remain attached after the move.
4. Delete a different room, move a surviving room into the deleted room’s old coordinate+slot, export, and confirm only the survivor’s sidecars and folder GUID remain.
5. Attempt export into another level’s non-empty folder and confirm the operation is blocked before replacement.
6. Compile the tracked compiler-ready V2 package.
7. Enter **COMBAT LOOP TEST** through normal Level Selection.
8. Confirm spawn in `STARTER ROOM` at `[-9, 0]`.
9. Traverse the exact east door into `SINGLE CONTACT` and arrive at its west-door anchor `[-10, 0]`.
10. Confirm one Mobile Blaster Droid and that the east progression door is closed before clear.
11. Defeat the Droid and confirm the east door opens through existing room-complete authority.
12. Enter `CROSSFIRE` at its west-door anchor `[-10, 0]`.
13. Confirm two Mobile Blaster Droids and final-exit gating.
14. Defeat both and trigger the final exit exactly once.
15. Restart the application and repeat without exporting or regenerating scene-local content.

## No replacement authorities

This change does not add another enemy system, weapon system, room-clear poller, traversal authority, completion authority, gameplay scene, or runtime filesystem reader. It compiles V2 into the current room runtime input and reuses all existing combat-loop authorities.
