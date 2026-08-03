"use strict";

(function exposeFloorData(root, factory) {
  const api = factory();
  if (typeof module === "object" && module.exports) module.exports = api;
  if (root) root.FloorData = api;
})(typeof globalThis !== "undefined" ? globalThis : this, function createFloorData() {
  const EMPTY = ".";
  const GRID_SYMBOLS = "0123456789abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ-_";
  const DEFAULT_TILE = "tile.floor-industrial";

  function copy(value) {
    if (value == null) return value;
    if (typeof structuredClone === "function") return structuredClone(value);
    return JSON.parse(JSON.stringify(value));
  }

  function roomSize(room) {
    return {
      width: Math.max(1, Math.round(Number(room?.bounds?.width) || 1)),
      height: Math.max(1, Math.round(Number(room?.bounds?.height) || 1)),
    };
  }

  function makeFloor(width, height, tile = null) {
    const floorWidth = Math.max(1, Math.round(Number(width) || 1));
    const floorHeight = Math.max(1, Math.round(Number(height) || 1));
    const floor = {
      width: floorWidth,
      height: floorHeight,
      tiles: [null],
      cells: new Uint16Array(floorWidth * floorHeight),
      count: 0,
      defaultTile: tile || DEFAULT_TILE,
    };
    if (tile) fillCells(floor, tile);
    return floor;
  }

  function isFloor(value) {
    return Boolean(
      value &&
      Array.isArray(value.tiles) &&
      value.cells instanceof Uint16Array &&
      Number.isInteger(value.width) &&
      Number.isInteger(value.height)
    );
  }

  function roomFloor(room) {
    return isFloor(room?.floor) ? room.floor : prepareRoom(room).floor;
  }

  function tileNumber(floor, tile) {
    if (!tile) return 0;
    const existing = floor.tiles.indexOf(tile);
    if (existing >= 0) return existing;
    if (floor.tiles.length >= 65_535) throw new Error("This room uses too many floor types.");
    floor.tiles.push(tile);
    return floor.tiles.length - 1;
  }

  function floorTile(floor, x, y) {
    if (!isFloor(floor)) return null;
    if (x < 0 || y < 0 || x >= floor.width || y >= floor.height) return null;
    return floor.tiles[floor.cells[y * floor.width + x]] || null;
  }

  function getFloorTile(source, x, y, width) {
    if (source?.floor && isFloor(source.floor)) return floorTile(source.floor, x, y);
    if (isFloor(source)) return floorTile(source, x, y);
    return source?.[y * width + x] || null;
  }

  function setFloorCell(floor, x, y, tile) {
    const index = y * floor.width + x;
    const before = floor.cells[index];
    const after = tileNumber(floor, tile);
    if (before === after) return false;
    if (before === 0 && after !== 0) floor.count += 1;
    if (before !== 0 && after === 0) floor.count -= 1;
    floor.cells[index] = after;
    if (tile) floor.defaultTile = tile;
    return true;
  }

  function setFloorTile(room, x, y, tile) {
    const floor = roomFloor(room);
    if (x < 0 || y < 0 || x >= floor.width || y >= floor.height) return false;
    return setFloorCell(floor, x, y, tile);
  }

  function clearFloorTile(room, x, y) {
    return setFloorTile(room, x, y, null);
  }

  function fillCells(floor, tile) {
    const value = tileNumber(floor, tile);
    floor.cells.fill(value);
    floor.count = value === 0 ? 0 : floor.cells.length;
    if (tile) floor.defaultTile = tile;
    return floor;
  }

  function fillFloor(room, tile) {
    fillCells(roomFloor(room), tile);
    return room;
  }

  function clearFloor(room) {
    return fillFloor(room, null);
  }

  function setDefaultFloorTile(room, tile) {
    const floor = roomFloor(room);
    const wasFull = isFullFloor(room);
    floor.defaultTile = tile || DEFAULT_TILE;
    if (wasFull) fillCells(floor, floor.defaultTile);
    return room;
  }

  function copyFloor(source) {
    const floor = isFloor(source?.floor) ? source.floor : source;
    if (!isFloor(floor)) throw new Error("Cannot copy an invalid floor.");
    return {
      width: floor.width,
      height: floor.height,
      tiles: [...floor.tiles],
      cells: new Uint16Array(floor.cells),
      count: floor.count,
      defaultTile: floor.defaultTile || DEFAULT_TILE,
    };
  }

  function resizeFloor(room, width, height) {
    const current = roomFloor(room);
    const nextWidth = Math.max(1, Math.round(Number(width) || 1));
    const nextHeight = Math.max(1, Math.round(Number(height) || 1));
    if (current.width === nextWidth && current.height === nextHeight) return room;

    const next = {
      width: nextWidth,
      height: nextHeight,
      tiles: [...current.tiles],
      cells: new Uint16Array(nextWidth * nextHeight),
      count: 0,
      defaultTile: current.defaultTile || DEFAULT_TILE,
    };
    const copyWidth = Math.min(current.width, nextWidth);
    const copyHeight = Math.min(current.height, nextHeight);

    for (let y = 0; y < copyHeight; y++) {
      for (let x = 0; x < copyWidth; x++) {
        const value = current.cells[y * current.width + x];
        next.cells[y * nextWidth + x] = value;
        if (value !== 0) next.count += 1;
      }
    }

    room.floor = next;
    return room;
  }

  function readSavedFloor(savedFloor, width, height) {
    const floor = makeFloor(width, height, null);
    const rows = Array.isArray(savedFloor?.rows) ? savedFloor.rows : [];

    if (savedFloor?.format === "grid") {
      const legend = savedFloor.legend || {};
      for (let y = 0; y < height; y++) {
        const row = typeof rows[y] === "string" ? rows[y] : "";
        for (let x = 0; x < width; x++) {
          const symbol = row[x] || EMPTY;
          const tile = symbol === EMPTY ? null : legend[symbol] || null;
          if (tile) setFloorCell(floor, x, y, tile);
        }
      }
      return floor;
    }

    if (savedFloor?.format === "number-grid") {
      const legend = Array.isArray(savedFloor.legend) ? savedFloor.legend : [null];
      for (let y = 0; y < height; y++) {
        const row = Array.isArray(rows[y]) ? rows[y] : [];
        for (let x = 0; x < width; x++) {
          const tile = legend[Number(row[x]) || 0] || null;
          if (tile) setFloorCell(floor, x, y, tile);
        }
      }
    }
    return floor;
  }

  function floorFromOldRoom(room, width, height) {
    const defaultTile = room.floorObject || DEFAULT_TILE;
    if (room.tileGridEnabled === false) return makeFloor(width, height, defaultTile);

    const floor = makeFloor(width, height, null);
    floor.defaultTile = defaultTile;
    for (const tile of Array.isArray(room.tiles) ? room.tiles : []) {
      if (!Number.isInteger(tile?.x) || !Number.isInteger(tile?.y)) continue;
      if (tile.x < 0 || tile.y < 0 || tile.x >= width || tile.y >= height) continue;
      if (tile.object) setFloorCell(floor, tile.x, tile.y, tile.object);
    }
    return floor;
  }

  function prepareRoom(room) {
    if (!room || typeof room !== "object") throw new Error("Cannot prepare an invalid room.");
    const { width, height } = roomSize(room);

    if (isFloor(room.floor)) {
      if (room.floor.width !== width || room.floor.height !== height) resizeFloor(room, width, height);
      return room;
    }

    room.floor = room.floor?.format
      ? readSavedFloor(room.floor, width, height)
      : floorFromOldRoom(room, width, height);

    delete room.floorObject;
    delete room.tileGridEnabled;
    delete room.tiles;
    return room;
  }

  function prepareRooms(rooms) {
    for (const room of rooms || []) prepareRoom(room);
    return rooms;
  }

  function isFullFloor(room) {
    const floor = roomFloor(room);
    if (floor.count !== floor.cells.length || floor.cells.length === 0) return false;
    const first = floor.cells[0];
    if (first === 0) return false;
    for (let i = 1; i < floor.cells.length; i++) {
      if (floor.cells[i] !== first) return false;
    }
    return true;
  }

  function defaultFloorTile(room) {
    const floor = roomFloor(room);
    return floor.defaultTile || floor.tiles.find(Boolean) || DEFAULT_TILE;
  }

  function readFloor(room) {
    const floor = roomFloor(room);
    return Array.from(floor.cells, value => floor.tiles[value] || null);
  }

  function writeFloor(room) {
    const floor = roomFloor(room);
    const usedNumbers = new Set();
    for (const value of floor.cells) if (value !== 0) usedNumbers.add(value);
    const tileNames = [...usedNumbers]
      .map(value => floor.tiles[value])
      .filter(Boolean)
      .sort((a, b) => a.localeCompare(b));

    if (tileNames.length <= GRID_SYMBOLS.length) {
      const legend = { [EMPTY]: null };
      const symbolForTile = new Map();
      tileNames.forEach((tile, index) => {
        const symbol = GRID_SYMBOLS[index];
        legend[symbol] = tile;
        symbolForTile.set(tile, symbol);
      });
      const rows = [];
      for (let y = 0; y < floor.height; y++) {
        let row = "";
        for (let x = 0; x < floor.width; x++) {
          const tile = floorTile(floor, x, y);
          row += tile ? symbolForTile.get(tile) : EMPTY;
        }
        rows.push(row);
      }
      return { format: "grid", legend, rows };
    }

    const legend = [null, ...tileNames];
    const indexForTile = new Map(tileNames.map((tile, index) => [tile, index + 1]));
    const rows = [];
    for (let y = 0; y < floor.height; y++) {
      const row = [];
      for (let x = 0; x < floor.width; x++) {
        const tile = floorTile(floor, x, y);
        row.push(tile ? indexForTile.get(tile) : 0);
      }
      rows.push(row);
    }
    return { format: "number-grid", legend, rows };
  }

  function saveRoom(room) {
    prepareRoom(room);
    const saved = { ...room, floor: writeFloor(room) };
    return JSON.parse(JSON.stringify(saved));
  }

  function openRoom(savedRoom) {
    return prepareRoom(copy(savedRoom) || {});
  }

  function openUnityTiles(room, unityTiles) {
    const opened = copy(room) || {};
    const { width, height } = roomSize(opened);
    opened.floor = makeFloor(width, height, null);

    for (const tile of unityTiles || []) {
      const from = tile?.fill?.from;
      const to = tile?.fill?.to;
      if (!tile?.object || !Array.isArray(from) || !Array.isArray(to)) continue;
      const startX = Math.max(0, Math.min(width, Math.round(Number(from[0]) + width / 2)));
      const startY = Math.max(0, Math.min(height, Math.round(Number(from[1]) + height / 2)));
      const endX = Math.max(0, Math.min(width, Math.round(Number(to[0]) + width / 2)));
      const endY = Math.max(0, Math.min(height, Math.round(Number(to[1]) + height / 2)));
      for (let y = startY; y < endY; y++) {
        for (let x = startX; x < endX; x++) setFloorCell(opened.floor, x, y, tile.object);
      }
    }
    return prepareRoom(opened);
  }

  function buildFloorAreas(source, width, height) {
    const liveFloor = isFloor(source?.floor) ? source.floor : isFloor(source) ? source : null;
    const areaWidth = liveFloor ? liveFloor.width : width;
    const areaHeight = liveFloor ? liveFloor.height : height;
    const tileAt = liveFloor
      ? (x, y) => floorTile(liveFloor, x, y)
      : (x, y) => getFloorTile(source, x, y, width);
    let activeAreas = [];
    const finishedAreas = [];

    for (let y = 0; y < areaHeight; y++) {
      const rowAreas = [];
      let tile = null;
      let startX = 0;
      for (let x = 0; x <= areaWidth; x++) {
        const nextTile = x < areaWidth ? tileAt(x, y) : null;
        if (nextTile === tile) continue;
        if (tile !== null) rowAreas.push({ tile, x: startX, y, width: x - startX, height: 1 });
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
    const floor = roomFloor(room);
    return buildFloorAreas(floor).map(area => ({
      object: area.tile,
      fill: {
        from: [-floor.width / 2 + area.x, -floor.height / 2 + area.y],
        to: [
          -floor.width / 2 + area.x + area.width,
          -floor.height / 2 + area.y + area.height,
        ],
      },
    }));
  }

  return {
    EMPTY,
    GRID_SYMBOLS,
    DEFAULT_TILE,
    makeFloor,
    copyFloor,
    prepareRoom,
    prepareRooms,
    resizeFloor,
    getFloorTile,
    setFloorTile,
    clearFloorTile,
    fillFloor,
    clearFloor,
    setDefaultFloorTile,
    defaultFloorTile,
    isFullFloor,
    readFloor,
    writeFloor,
    saveRoom,
    openRoom,
    openUnityTiles,
    buildFloorAreas,
    buildUnityTiles,
  };
});
