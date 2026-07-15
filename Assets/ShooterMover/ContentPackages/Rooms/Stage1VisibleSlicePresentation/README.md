# Stage 1 Visible-Slice Room Presentation

Task: `VS-002`

This package is a detachable, presentation-only dark industrial room shell for
`VS-007` to instantiate. It does not own collision, room geometry, encounters,
mission state, rewards, persistence, camera behavior, or gameplay truth.

## Default bindings

- Floor: the merged VS-001 sprite
  `Assets/ShooterMover/Art/Prototype/Stage1VisibleSlice/floor_tile_concept_1.png`.
- Props: the merged transparent crate
  `Assets/ShooterMover/Art/Prototype/Stage1VisibleSlice/prop_crate_1.png`.
- Door and explosive: the clean transparent VS-001 replacement sprites
  `door_idea_1.png` and `prop_explosive_1.png`. Their stable Unity GUIDs keep
  the prefab binding intact while preserving the original source revisions.

The accepted floor is the default. A flat dark fallback appears only when the
floor binding is missing and displays a visible fallback marker.

## Deterministic generated hierarchy

The prefab contains one presentation component. On enable it creates these
task-local visual groups:

1. `00_Floor`
2. `10_Walls`
3. `20_Doors`
4. `30_Props`
5. `40_IntegrationMarkers`
6. `90_OptionalEffects`

Only `Transform` and `SpriteRenderer` components are generated. The package
contains no `Collider2D`, `Rigidbody2D`, `Camera`, light authority, mission,
encounter, reward, save, persistence, or gameplay-authority component.

Sorting uses the existing `Default` layer with explicit orders:

- floor `-300`;
- walls `-220`;
- optional accents `-200`;
- doors `-180`;
- props `-120`;
- integration markers `-60`.

Actors, bullets, warnings, and HUD can therefore remain at order `0` or above
and stay visually dominant.

## Integration

Instantiate `Stage1VisibleSliceRoomPresentation.prefab` without copying its
children into the integration scene. The component exposes the room bounds,
visual roots, marker roots, replaceable sprite setters, hierarchy signature,
and reduced-effects switch.

Reduced effects disables only `90_OptionalEffects`; floor, walls, doors, props,
and integration markers remain visible. Removing the prefab removes the entire
presentation package without changing accepted gameplay state.

## Verification boundary

Focused PlayMode tests live under the owned
`VisibleSliceRoomPresentation` test folder. Unity compilation, PlayMode XML/log,
and the required 1920x1080 capture must be produced in Unity `6000.3.19f1`.
Static connector review alone is not executable evidence.
