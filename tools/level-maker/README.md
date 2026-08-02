# Shooter Mover Level Maker

A dependency-free browser editor for authoring playable Shooter Mover room graphs.

## Start it

From the repository root on Windows:

```powershell
.\tools\level-maker\Start-LevelMaker.ps1
```

Or run `serve.bat` from this folder. Both launch `start-server.js`, which serves
the editor on `http://127.0.0.1:4174`, keeps the existing level publishing API,
and adds local editor-state storage beneath Unity's ignored `Library` folder.

## Saved data

The Level Maker keeps three kinds of data separate:

- **Level data** — rooms, floors, entities, doors, encounters, connections, and
  level settings. This is saved to `Content/Levels/<target>.level.json`.
- **Editor data** — active room, selected object and asset, tool, brush, zoom,
  pan, snap, and custom asset shortcuts. This is saved locally to
  `Library/ShooterMover/LevelMaker/<target>.editor.json`.
- **Project assets** — the current enemy, prop, floor, door, and decor catalogue.
  This is rebuilt from the repository when the editor opens.

Editor data does not enter the committed level file. The browser also keeps a
local-storage copy and uses `navigator.sendBeacon()` during `pagehide` so a small
pending editor update can still reach the local server while the tab closes.

## Project flow

- **Open level** loads an editable project when one exists. Older combined
  project files remain supported.
- When rebuilding from generated Unity room files, every floor area is expanded
  back into the editable floor grid, preserving holes and multiple floor types.
- **Save to project** validates and atomically writes the compact editable level
  plus the generated Unity source JSON.
- **Playtest** performs the same validated write and refreshes the playable-level
  catalogue.
- **Export project file** downloads the compact editable level without editor
  state or the discovered asset catalogue.

Unity owns compilation. The project postprocessor notices imported `level.json`
files and invokes the existing `LevelGridAssetCompiler`. The browser does not
generate C#, edit `.meta` files, or write compiled Unity assets.

## Floor storage

Editable level files use a compact grid instead of one JSON object per cell.
Most rooms use string rows with a short legend:

```json
{
  "floor": {
    "format": "grid",
    "legend": {
      ".": null,
      "0": "tile.floor-industrial",
      "1": "tile.floor-metal"
    },
    "rows": [
      "....0....",
      "....0....",
      "000010000"
    ]
  }
}
```

`.` means no floor. When a room uses more than 64 floor types, the Level Maker
uses `number-grid` rows containing numeric legend indexes instead.

While editing, each room uses a compact floor buffer:

```text
room.floor.tiles = [null, "tile.floor-industrial", "tile.floor-metal"]
room.floor.cells = Uint16Array(...)
```

Each cell stores a small number that points into `room.floor.tiles`. A filled
200 × 200 room therefore uses one 40,000-cell typed array (80 KB) instead of
40,000 `{ x, y, object }` JavaScript objects. Painting and erasing update the
cell directly with `y * width + x` indexing.

During Unity export, `buildFloorAreas()` reads horizontal sections from this
buffer and extends matching sections downward to create deterministic,
non-overlapping rectangular areas. Unity continues receiving the existing
`floor.json` structure:

```json
{
  "object": "tile.floor-industrial",
  "fill": {
    "from": [-12, -7],
    "to": [12, 7]
  }
}
```

Enemies, props, doors, encounters, and other individual gameplay objects remain
ordinary JSON objects.

## State and undo

The live browser state is divided into:

```text
state.level
state.editor
state.assets
```

Undo and redo store the same compact level representation used by project saves.
Typed floor buffers are rebuilt when an undo point is restored, while zoom, pan,
the active room, the selected asset, and brush settings remain untouched.

Temporary non-enumerable room properties keep older Level Maker code working
with names such as `floorObject`, `tileGridEnabled`, and `tiles`. They are not
written to project files and can be removed after the remaining UI code uses the
new floor helpers directly.

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
- Indestructible walls are reusable 1x1 and 2x2 prop pieces. They have collision
  but no health or destruction behavior.
- Select a wall and press `Delete` or `Backspace` to erase it.
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

Publishing writes repository-owned JSON:

- `level.json`
- `map.json`
- per-room `room.json`
- `doors.json`
- `floor.json`
- `enemies.json`
- `props.json`
- `decor.json`
- `encounter.json`
- the compact editable project document
- the generated playable-level catalogue JSON

The runtime package includes room size and player start, tiled floors, enemy
definitions and tiers, props, walls, doors, clear conditions, and room graph
connections. Unsupported gameplay metadata remains in the editable project until
the runtime gains a typed representation.

## Tests

From `tools/level-maker`:

```powershell
node floor-data.test.js
node level-state.test.js
node level-save.test.js
node app-19-save.test.js
node editor-file.test.js
```

The GitHub Actions workflow runs the same tests and syntax-checks the browser and
server modules.
