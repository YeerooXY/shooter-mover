# LEVELDES-001 — Level designer foundation

Use this packet with one isolated GitHub/Unity coding agent.

```text
Repository: YeerooXY/shooter-mover
Task: LEVELDES-001
Branch: agent/leveldes-001-level-authoring-foundation
Exact base commit: 645cf24f30ee6c8762214a84060e59e35df67a05
PR base: main
```

Create a fresh exact-base branch and one non-empty draft PR. Do not merge it. Read `AGENTS.md`, current handoff, vertical-slice README/ownership/validation, and the existing OBJ/DOOR/PROP/PICK/SRC authoring contracts first.

## Objective

Create drag-and-drop Unity authoring components and prefabs for rooms, doors, player spawns, enemy spawns, props, pickups, and exits. Support stable identity, room association, alignment/grid metadata, sorting, collision policy, map metadata, and actionable editor validation. Do not build a complete map editor.

## Dependencies

- Merged OBJ-001, DOOR-001, PROP-001, PICK-001, SRC-001, and enemy packages.
- ROOM-001 may be consumed after merge. Before that, represent room identity through StableId/read-only adapters, not new graph truth.

## Exclusive owned files and paths

- `Assets/ShooterMover/ContentPackages/LevelDesign/Foundation/**`
- `Assets/ShooterMover/Runtime/UnityAdapters/Authoring/LevelDesign/**`
- `Assets/ShooterMover/Editor/LevelDesign/Foundation/**`
- `Assets/ShooterMover/Tests/EditMode/LevelDesign/Foundation/**`
- `Assets/ShooterMover/Art/Environment/Doors/UserIntake/**`
- `docs/authoring/LEVEL_DESIGN_FOUNDATION_V1.md`

## Forbidden paths and changes

- Do not edit existing door/enemy/prop/pickup packages or prefabs.
- Do not edit ROOM-001, gameplay scenes/controller, map/transition logic, economy/reward/XP authorities, or registries.
- Do not create a complete graph/map editor, procedural generator, or name-based identity.
- Do not edit ProjectSettings, Packages, shared asmdefs, generated/context/dispatch files.

## Acceptance criteria

- [ ] Provides authoring components/prefabs for room anchor, configured door, player spawn, enemy spawn, prop placement, pickup anchor/spawn, and exit/entry.
- [ ] Every object supports accepted stable identity policy and duplicate validation.
- [ ] Metadata includes room identity, grid/alignment, sorting, collision policy, map coordinate/visibility, and explicit restart behavior where relevant.
- [ ] Door authoring consumes DOOR-001 and supports closed/open visual plus collision policy.
- [ ] Enemy/prop/pickup authoring references existing definitions/profiles/prefabs without duplicating behavior.
- [ ] Inspector/editor validation reports missing definitions, duplicate IDs, invalid room links/grid/colliders/spawns/reward overrides, and missing presentation.
- [ ] Designers can drag objects into a clean scene and configure them without script edits.
- [ ] EditMode tests validate data, prefab composition, cross references, duplicate identity, and no name dependence.

## Focused Unity test command

```powershell
$Unity = "C:\Program Files\Unity\Hub\Editor\6000.3.19f1\Editor\Unity.exe"
New-Item -ItemType Directory -Force artifacts/test-results, artifacts/logs | Out-Null
& $Unity -batchmode -nographics -projectPath "$PWD" -runTests -testPlatform EditMode -testFilter "ShooterMover.Tests.EditMode.LevelDesign.Foundation" -testResults "artifacts/test-results/LEVELDES-001-EditMode.xml" -logFile "artifacts/logs/LEVELDES-001-EditMode.log" -quit
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
```

Passing claims require the XML file with zero failures.

## Manual proof checklist

- [ ] In a blank scene drag one of every provided authoring prefab/component.
- [ ] Assign stable IDs, align to grid, change sorting/collision, and inspect feedback.
- [ ] Configure a door with supplied open-door art and verify pivot/alpha/collision preview.
- [ ] Create duplicate/missing references and verify actionable validation.
- [ ] Confirm no existing package prefab or gameplay scene changed.

## Merge order

First wave. May merge before ROOM-001 if it avoids a hard dependency; run cross-validation after ROOM-001. Must merge before DEMO-005.

## Asset requirements

Copy `source-assets/user-intake/map_items/door_open.png` from exact asset commit `0b1b654c1fb8cf8208904eb55041fde954cfb560` into `Assets/ShooterMover/Art/Environment/Doors/UserIntake/**`. Preserve real alpha, pivot, scale, and import settings. Source is read-only.

## Known limitations

- No complete map editor, automatic pathfinding, or final production art.
- Moving-droid art is unavailable and remains a future swap.

## Parallel dispatch safety

Safe immediately with ROOM-001 because ownership is separate. Do not add ROOM-001 files.
