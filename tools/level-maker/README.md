# Shooter Mover Level Maker

A dependency-free browser editor for authoring playable Shooter Mover room graphs.

## Start it

From the repository root on Windows:

```powershell
.\tools\level-maker\Start-LevelMaker.ps1
```

Or run `serve.bat` from this folder. Node.js serves the editor on
`http://127.0.0.1:4174` and exposes a project-local save API. The API listens only
on localhost and requires the mutation token supplied to the loaded editor.

## Project flow

- **Open project level** loads an existing canonical level package.
- **Save to project** validates and atomically writes the editable project plus
  canonical JSON beneath the repository's level-content roots.
- **Publish to Unity** performs the same validated write and refreshes the
  playable-level catalogue.
- **Export project file** downloads a portable `.smlvl.json` backup. It does not
  inject C# or Unity metadata.

Unity owns compilation. The project postprocessor notices imported `level.json`
files and invokes the existing `LevelGridAssetCompiler`. The browser never scans
arbitrary Unity folders, generates C#, edits `.meta` files, or writes compiled
assets.

## Level graph

The graph has three modes:

1. **Open room** — click a room node to enter its focused room editor.
2. **Arrange** — drag rooms around the snapped graph grid.
3. **Connect doors** — drag a yellow door socket onto a door socket in another
   room.

Connections are written to `map.json`.

## Focused room editing

- Room width and height use whole 1 × 1 cells.
- **Single** places one object per click.
- Floor brushes always paint continuously while held. Select a floor asset,
  press `F`, then click and drag across the room.
- **Paint** stamps enemies and props across visited cells.
- Props snap to the nearest tile center when placed, painted, or dragged.
- Walls are drag-drawn segments.
- Doors are constrained to the nearest room edge when placed or dragged.
- Enemy placements choose tier 1–4. Runtime health multipliers are
  `1× / 2× / 4× / 8×`; damage multipliers are `1× / 1.5× / 2× / 3×`.

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

## Canonical output

Publishing writes only repository-owned JSON:

- `level.json`
- `map.json`
- per-room `room.json`
- `doors.json`
- `floor.json`
- `enemies.json`
- `props.json`
- `decor.json`
- `encounter.json`
- the editable project document
- the generated playable-level catalogue JSON

The runtime package includes room size and player start, tiled floors, enemy
definitions and tiers, props, walls, doors, clear conditions, and room graph
connections. Unsupported metadata remains in the editable project document until
the runtime gains a typed representation.
