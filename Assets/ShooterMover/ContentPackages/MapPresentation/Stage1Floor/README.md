# VS-ART-001 — Temporary Stage 1 Floor

## Status

This package is **temporary visible-slice presentation**, not final art. The requested local source image (`tile_concept_1.jfif`) was unavailable to the implementation environment, so Nemo authorized a removable default placeholder that can be replaced later.

The package owns presentation only. It contains no collider, camera behavior, scene composition, player/enemy/weapon hook, mission logic, persistence, or gameplay authority.

## Contents

- `Stage1FloorVisual.prefab` — reusable presentation root sized for the current 48 × 27 prototype room.
- `Stage1FloorVisual.cs` — deterministic editor/runtime fallback generator and the replacement seam for later art.

Opening the prefab in Prefab Mode or instantiating it creates one hidden, non-saved `SpriteRenderer`. The renderer is destroyed with the component and does not become a persistent scene dependency.

## Temporary rendering setup

Fallback setup:

- deterministic 64 × 64 RGBA32 procedural tile;
- no mipmaps;
- bilinear filtering;
- repeat wrap mode;
- full-rect sprite mesh;
- 4 world units per tile;
- `SpriteDrawMode.Tiled` with continuous tiling;
- 48 × 27 default floor footprint;
- sorting order `-100`;
- project default sprite material unless `materialOverride` is explicitly assigned.

The palette intentionally stays within dark blue-grey metal values. Bright bevels and wear marks are sparse so cyan player presentation, orange thruster/projectile presentation, enemy telegraphs, and warning effects retain clear value contrast.

## Replacing the placeholder

Import the approved floor concept under this package folder as a Unity sprite, then assign it to `replacementSprite` on `Stage1FloorVisual.prefab`. No code, scene, collision, or gameplay change should be required.

Recommended import settings for the replacement:

- Texture Type: `Sprite (2D and UI)`;
- Sprite Mode: `Single`;
- Mesh Type: `Full Rect`;
- Filter Mode: `Bilinear` for the current painted-metal direction;
- Generate Mip Maps: off for the current orthographic prototype camera;
- Compression: none or low enough to avoid ringing along dark seams;
- remove or repair contrasting edge pixels before assignment so repeated tiles remain seamless.

A later final-art pass may replace the sprite, palette, and optional material while preserving the prefab footprint and presentation-only boundary.

## Readability constraints

- Do not raise the floor into the player/projectile sorting range.
- Avoid bright full-tile outlines or high-frequency noise beneath combat actors.
- Keep major seams wider and lower-contrast than projectile silhouettes.
- Verify the tile at the prototype orthographic camera scale (`5.4`) rather than only in a zoomed texture inspector.
- Do not add collision to this prefab; room collision remains separately owned.

## Verification

Manual editor check:

1. Open `Stage1FloorVisual.prefab` in Prefab Mode.
2. Confirm the dark-metal tile fills a 48 × 27 area and remains centered on the prefab origin.
3. Pan and zoom around tile boundaries and confirm there are no bright gaps or obvious texture discontinuities.
4. Confirm the prefab and generated surface contain no `Collider2D`, `Rigidbody2D`, camera, or gameplay components.
5. Assign a temporary test sprite to `replacementSprite`, confirm it replaces the fallback, then clear the field and confirm the procedural tile returns.

No final-art, scene-integration, or playable-combat proof is claimed by this package.

## Rollback

Remove `Assets/ShooterMover/ContentPackages/MapPresentation/Stage1Floor/` and its inseparable folder metadata. No scene, gameplay system, registry, save data, project setting, or generated artifact requires rollback.
