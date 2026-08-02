"use strict";

(function exposeFloorData(root, factory) {
  const api = factory();
  if (typeof module === "object" && module.exports) module.exports = api;
  if (root) root.FloorData = api;
})(typeof globalThis !== "undefined" ? globalThis : this, function createFloorData() {
  const EMPTY = ".";
  const GRID_SYMBOLS = "0123456789abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ-_";

  function copy(value) {
    return value == null ? value : JSON.parse(JSON.stringify(value));
  }

  function roomSize(room) {
    return {
      width: Math.max(1, Math.round(Number(room?.bounds?.width) || 1)),
      height: Math.max(1, Math.round(Number(room?.bounds?.height) || 1)),
    };
  }

  function getFloorTile(floor, x, y, width) {
    return floor[y * width + x] || null;
  }

  function readFloor(room) {
    const { width, height } = roomSize(room);
    const floor = new Array(width * height).fill(null);

    if (room?.floor) {
      return readSavedFloor(room.floor, width, height);
    }

    if (room?.tileGridEnabled === false) {
      floor.fill(room.floorObject || null);
      return floor;
    }

    for (const tile of room?.tiles || []) {
      if (!Number.isInteger(tile?.x) || !Number.isInteger(tile?.y)) continue;
      if (tile.x < 0 || tile.y < 0 || tile.x >= width || tile.y >= height) continue;
      floor[tile.y * width + tile.x] = tile.object || null;
    }

    return floor;
  }

  function readSavedFloor(savedFloor, width, height) {
    const floor = new Array(width * height).fill(null);
    const rows = Array.isArray(savedFloor?.rows) ? savedFloor.rows : [];

    if (savedFloor?.format === "grid") {
      const legend = savedFloor.legend || {};
      for (let y = 0; y < height; y++) {
        const row = typeof rows[y] === "string" ? rows[y] : "";
        for (let x = 0; x < width; x++) {
          const symbol = row[x] || EMPTY;
          floor[y * width + x] = symbol === EMPTY ? null : legend[symbol] || null;
        }
      }
      return floor;
    }

    if (savedFloor?.format === "number-grid") {
      const legend = Array.isArray(savedFloor.legend) ? savedFloor.legend : [null];
      for (let y = 0; y < height; y++) {
        const row = Array.isArray(rows[y]) ? rows[y] : [];
        for (let x = 0; x < width; x++) {
          const index = Number(row[x]) || 0;
          floor[y * width + x] = legend[index] || null;
        }
      }
      return floor;
    }

    return floor;
  }

  function writeFloor(room) {
    const { width, height } = roomSize(room);
    const floor = readFloor(room);
    const tileNames = [...new Set(floor.filter(Boolean))].sort((a, b) => a.localeCompare(b));

    if (tileNames.length <= GRID_SYMBOLS.length) {
      const legend = { [EMPTY]: null };
      const symbolForTile = new Map();
      tileNames.forEach((tile, index) => {
        const symbol = GRID_SYMBOLS[index];
        legend[symbol] = tile;
        symbolForTile.set(tile, symbol);
      });

      const rows = [];
      for (let y = 0; y < height; y++) {
        let row = "";
        for (let x = 0; x < width; x++) {
          const tile = getFloorTile(floor, x, y, width);
          row += tile ? symbolForTile.get(tile) : EMPTY;
        }
        rows.push(row);
      }

      return { format: "grid", legend, rows };
    }

    const legend = [null, ...tileNames];
    const indexForTile = new Map(tileNames.map((tile, index) => [tile, index + 1]));
    const rows = [];
    for (let y = 0; y < height; y++) {
      const row = [];
      for (let x = 0; x < width; x++) {
        const tile = getFloorTile(floor, x, y, width);
        row.push(tile ? indexForTile.get(tile) : 0);
      }
      rows.push(row);
    }

    return { format: "number-grid", legend, rows };
  }

  function saveRoom(room) {
    const saved = copy(room) || {};
    saved.floor = writeFloor(room);
    delete saved.floorObject;
    delete saved.tileGridEnabled;
    delete saved.tiles;
    return saved;
  }

  function useFloor(room, floor) {
    const { width, height } = roomSize(room);
    const usedTiles = [...new Set(floor.filter(Boolean))];
    const completelyFilled = floor.length > 0 && floor.every(tile => tile && tile === floor[0]);

    room.floorObject = usedTiles[0] || "tile.floor-industrial";
    room.tileGridEnabled = !completelyFilled;
    room.tiles = [];

    if (!completelyFilled) {
      for (let y = 0; y < height; y++) {
        for (let x = 0; x < width; x++) {
          const object = getFloorTile(floor, x, y, width);
          if (object) room.tiles.push({ x, y, object });
        }
      }
    }

    delete room.floor;
    return room;
  }

  function openRoom(savedRoom) {
    const room = copy(savedRoom) || {};
    const { width, height } = roomSize(room);

    if (!room.floor) {
      room.floorObject ||= "tile.floor-industrial";
      room.tileGridEnabled = typeof room.tileGridEnabled === "boolean"
        ? room.tileGridEnabled
        : Array.isArray(room.tiles) && room.tiles.length > 0;
      room.tiles ||= [];
      return room;
    }

    return useFloor(room, readSavedFloor(room.floor, width, height));
  }

  function openUnityTiles(room, unityTiles) {
    const opened = copy(room) || {};
    const { width, height } = roomSize(opened);
    const floor = new Array(width * height).fill(null);

    for (const tile of unityTiles || []) {
      const from = tile?.fill?.from;
      const to = tile?.fill?.to;
      if (!tile?.object || !Array.isArray(from) || !Array.isArray(to)) continue;

      const startX = Math.max(0, Math.min(width, Math.round(Number(from[0]) + width / 2)));
      const startY = Math.max(0, Math.min(height, Math.round(Number(from[1]) + height / 2)));
      const endX = Math.max(0, Math.min(width, Math.round(Number(to[0]) + width / 2)));
      const endY = Math.max(0, Math.min(height, Math.round(Number(to[1]) + height / 2)));

      for (let y = startY; y < endY; y++) {
        for (let x = startX; x < endX; x++) {
          floor[y * width + x] = tile.object;
        }
      }
    }

    return useFloor(opened, floor);
  }

  function buildFloorAreas(floor, width, height) {
    let activeAreas = [];
    const finishedAreas = [];

    for (let y = 0; y < height; y++) {
      const rowAreas = [];
      let tile = null;
      let startX = 0;

      for (let x = 0; x <= width; x++) {
        const nextTile = x < width ? getFloorTile(floor, x, y, width) : null;
        if (nextTile === tile) continue;

        if (tile !== null) {
          rowAreas.push({ tile, x: startX, y, width: x - startX, height: 1 });
        }

        tile = nextTile;
        startX = x;
      }

      const rowByShape = new Map(
        rowAreas.map(area => [`${area.tile}\n${area.x}\n${area.width}`, area])
      );
      const nextActiveAreas = [];

      for (const area of activeAreas) {
        const key = `${area.tile}\n${area.x}\n${area.width}`;
        if (!rowByShape.has(key)) {
          finishedAreas.push(area);
          continue;
        }

        area.height += 1;
        nextActiveAreas.push(area);
        rowByShape.delete(key);
      }

      nextActiveAreas.push(...rowByShape.values());
      activeAreas = nextActiveAreas;
    }

    finishedAreas.push(...activeAreas);
    finishedAreas.sort((a, b) =>
      a.tile.localeCompare(b.tile) ||
      a.y - b.y ||
      a.x - b.x ||
      a.height - b.height ||
      a.width - b.width
    );

    return finishedAreas;
  }

  function buildUnityTiles(room) {
    const { width, height } = roomSize(room);
    const floor = readFloor(room);
    return buildFloorAreas(floor, width, height).map(area => ({
      object: area.tile,
      fill: {
        from: [-width / 2 + area.x, -height / 2 + area.y],
        to: [
          -width / 2 + area.x + area.width,
          -height / 2 + area.y + area.height,
        ],
      },
    }));
  }

  return {
    EMPTY,
    GRID_SYMBOLS,
    getFloorTile,
    readFloor,
    writeFloor,
    saveRoom,
    openRoom,
    openUnityTiles,
    buildFloorAreas,
    buildUnityTiles,
  };
});
