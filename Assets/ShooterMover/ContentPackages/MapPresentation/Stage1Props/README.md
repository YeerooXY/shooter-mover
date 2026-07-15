# VS-ART-002 Temporary Stage 1 Props

This package contains six deliberately temporary, presentation-only industrial props for the first visible slice. Every prefab is removable and owns no collision, navigation, input, enemy/player hook, mission logic, reward logic, persistence, registry, or scene authority.

## Prefabs

| Prefab | Visual footprint at scale 1 | Intended placement |
|---|---:|---|
| `TMP_Prop_Crate.prefab` | about 1.5 x 1.5 world units | Beside walls, loading bays, or room corners; rotate in 90-degree steps. |
| `TMP_Prop_FloorVent.prefab` | about 1.7 x 1.0 units | Flat against open floor or near machinery; leave movement lanes visually clear. |
| `TMP_Prop_ServiceConsole.prefab` | about 1.5 x 1.4 units | Against a wall, pillar, or dead-end alcove; cyan screen faces the room. |
| `TMP_Prop_SupportPillar.prefab` | about 1.3 x 1.7 units | Structural rhythm near room edges; do not imply collision until a scene owner adds separate authority. |
| `TMP_Prop_PipeCluster.prefab` | about 1.7 x 1.4 units | Utility corner, maintenance bay, or wall-adjacent machinery cluster. |
| `TMP_Prop_WarningMarker.prefab` | about 1.7 x 1.7 units | Under hazards, route transitions, or reserved encounter space; it remains decorative only. |

## Rendering and scale language

- Each prefab contains only `Transform`, `SpriteRenderer`, and `TemporaryStage1PropRenderer`.
- The renderer creates a 96 x 96 point-filtered temporary sprite at 48 pixels per unit, giving a common 2 x 2 world-unit canvas.
- Shapes use the same dark outline, steel values, and cyan/amber/red accents so silhouettes remain readable over the current dark floor.
- Default sorting order is `5`. For a floor shell that renders above it, move the prop slightly toward the camera or adjust sorting only from the eventual scene-composition owner.
- Prefer scale `1,1,1`. Use uniform scaling from `0.85` to `1.15` only when repetition needs variation.

## Verification in Unity 6000.3.19f1

1. Open every prefab in Prefab Mode and confirm the temporary sprite appears without entering Play Mode.
2. Confirm each root has exactly one `SpriteRenderer` and one `TemporaryStage1PropRenderer`.
3. Confirm there is no `Collider2D`, `Rigidbody2D`, trigger, input component, gameplay adapter, scene reference, or package/registry reference.
4. Temporarily drag the six prefabs into an empty local test scene over a background near RGB `0.025, 0.035, 0.055`; verify the outline and at least one light/accent region remain distinct at Game-view scale.
5. Delete those local scene instances without saving. VS-ART-002 does not own a scene edit.

## Temporary debt and replacement

These procedural pixel silhouettes are prototype debt, not final art. Replace or delete the entire `Stage1Props` folder when representative prop art is accepted. Scene composition should reference the prefab roots rather than individual generated textures; the generated sprites are unsaved and recreated automatically.

## Rollback

Remove `Assets/ShooterMover/ContentPackages/MapPresentation/Stage1Props/` and its inseparable metadata. If `MapPresentation` has no other packages at rollback time, its now-empty folder metadata may also be removed. No gameplay, save, registry, scene, or package migration is required.
