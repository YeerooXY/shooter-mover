"use strict";

const assert = require("assert");
const FloorData = require("./floor-data");

function makeRoom(width, height, floor) {
  const tiles = [];
  for (let y = 0; y < height; y++) {
    for (let x = 0; x < width; x++) {
      const object = floor[y * width + x];
      if (object) tiles.push({ x, y, object });
    }
  }
  return {
    id: "room.test",
    bounds: { width, height },
    floorObject: "tile.floor-industrial",
    tileGridEnabled: true,
    tiles,
    entities: [],
    doors: [],
  };
}

const width = 50;
const height = 50;
const cross = new Array(width * height).fill(null);
for (let y = 0; y < height; y++) cross[y * width + 24] = "tile.floor-industrial";
for (let x = 0; x < width; x++) cross[24 * width + x] = "tile.floor-industrial";

const crossAreas = FloorData.buildFloorAreas(cross, width, height);
assert.deepStrictEqual(crossAreas, [
  { tile: "tile.floor-industrial", x: 24, y: 0, width: 1, height: 24 },
  { tile: "tile.floor-industrial", x: 0, y: 24, width: 50, height: 1 },
  { tile: "tile.floor-industrial", x: 24, y: 25, width: 1, height: 25 },
]);

const crossRoom = makeRoom(width, height, cross);
const savedCrossRoom = FloorData.saveRoom(crossRoom);
assert.strictEqual(savedCrossRoom.floor.format, "grid");
assert.strictEqual(savedCrossRoom.floor.rows.length, height);
assert.strictEqual(savedCrossRoom.floor.rows[0].length, width);
assert.ok(!("tiles" in savedCrossRoom));
assert.deepStrictEqual(
  FloorData.readFloor(FloorData.openRoom(savedCrossRoom)),
  cross
);

const unityTiles = FloorData.buildUnityTiles(crossRoom);
assert.strictEqual(unityTiles.length, 3);
const rebuiltFromUnity = FloorData.openUnityTiles(
  { id: "room.test", bounds: { width, height }, entities: [], doors: [] },
  unityTiles
);
assert.deepStrictEqual(FloorData.readFloor(rebuiltFromUnity), cross);

const fullRoom = {
  id: "room.full",
  bounds: { width: 12, height: 8 },
  floorObject: "tile.floor-metal",
  tileGridEnabled: false,
  tiles: [],
};
assert.deepStrictEqual(FloorData.buildUnityTiles(fullRoom), [
  {
    object: "tile.floor-metal",
    fill: { from: [-6, -4], to: [6, 4] },
  },
]);

const checkerWidth = 20;
const checkerHeight = 20;
const checker = new Array(checkerWidth * checkerHeight).fill(null).map((_, index) => {
  const x = index % checkerWidth;
  const y = Math.floor(index / checkerWidth);
  return (x + y) % 2 ? "tile.floor-a" : "tile.floor-b";
});
assert.strictEqual(
  FloorData.buildFloorAreas(checker, checkerWidth, checkerHeight).length,
  checkerWidth * checkerHeight
);

const manyTiles = Array.from({ length: 65 }, (_, index) => `tile.floor-${index}`);
const manyTileRoom = makeRoom(65, 1, manyTiles);
const manyTileSave = FloorData.writeFloor(manyTileRoom);
assert.strictEqual(manyTileSave.format, "number-grid");
assert.strictEqual(manyTileSave.legend.length, 66);
assert.deepStrictEqual(
  FloorData.readFloor(FloorData.openRoom({ ...manyTileRoom, floor: manyTileSave })),
  manyTiles
);

console.log("Level Maker floor tests passed.");
