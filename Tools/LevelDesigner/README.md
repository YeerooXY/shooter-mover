# Shooter Mover Web Level Designer

A dependency-free browser editor for building Shooter Mover schema-v2 room packages.

## Run

Open `index.html` directly, or start the included local server:

- Windows: `serve.bat`
- macOS/Linux: `./serve.sh`

Then open `http://localhost:8765`.

## Level graph

The graph has three modes:

1. **Open room** — click a room node to enter its focused room editor.
2. **Arrange** — drag rooms around the snapped graph grid.
3. **Connect doors** — drag a yellow door socket onto a door socket in another room.

Connections are written to `map.json`.

## Focused room editing

Opening a room gives the canvas the full workspace. The asset catalogue and inspector become temporary drawers.

- Room width and height use whole 1 × 1 cells.
- X and Y use one device-pixel-adjusted scale, so cells stay square on mobile and high-DPI displays.
- Minor grid lines, major four-cell lines, room borders, and cell-center dots remain visible at practical zoom levels.
- **Single** places one object per click.
- **Paint** stamps enemies, props, or floor tiles across visited cells.
- Props snap to the nearest tile center when placed, painted, or dragged.
- Walls are drag-drawn segments.
- Doors snap to the nearest room edge.

Useful shortcuts:

- `1` — single placement
- `2` — paint placement
- `F` — floor tile brush
- `X` — tile eraser
- `G` — level graph
- `V` — select
- `W` — wall
- `D` — door
- `P` — prop
- `Q` / `E` — rotate selected object
- `Esc` — close a drawer or return to the graph

## Unity catalogue scan

Use **Scan Unity project** to collect known IDs from JSON files and prefab filenames. The scanner skips common generated folders when the browser directory-picker API is available.

Prefab filename IDs are guesses and are marked for verification. JSON-backed object IDs are preferred.

## Export

**Export playable ZIP** writes the existing Shooter Mover package shape:

- `level.json`
- `map.json`
- per-room `room.json`
- `doors.json`
- `floor.json`
- `enemies.json`
- `props.json`
- `decor.json`
- `encounter.json`
- editable `.smlvl.json` project data
- optional Unity auto-compiler bridge

Painted floor cells are rectangle-compressed before export.

Currently emitted into the playable runtime schema:

- room size and player start
- tiled floors
- enemy IDs, levels, positions, and rotations
- props and wall instances
- doors and room-completion rules
- room graph connections

Retained in editor project metadata pending matching runtime support:

- per-instance drop overrides
- detailed wall dimensions
- generic custom logic
- teleporters

## Import into Shooter Mover

1. Export the playable ZIP.
2. Extract it into the Unity project root.
3. Let Unity refresh.
4. The optional bridge calls the existing `LevelGridAssetCompiler` for imported web-level markers.

The default target folder is `Level1`. A custom target creates a separate generated room-content resource and may also need registration in the game's explicit level-selection catalogue.
