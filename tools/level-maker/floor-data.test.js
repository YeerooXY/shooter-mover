"use strict";

const assert = require("assert");
const FloorData = require("./floor-data");

function makeOldRoom(width, height, floor) {
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

function assertNoOldFloorNames(room) {
  for (const oldName of ["tiles", "tileGridEnabled", "floorObject"]) {
    assert.strictEqual(oldName in room, false, `${oldName} must not exist on a live room`);
  }
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

const crossRoom = FloorData.prepareRoom(makeOldRoom(width, height, cross));
assert.ok(crossRoom.floor.cells instanceof Uint16Array);
assert.strictEqual(crossRoom.floor.cells.length, width * height);
assert.strictEqual(crossRoom.floor.count, 99);
assertNoOldFloorNames(crossRoom);

const savedCrossRoom = FloorData.saveRoom(crossRoom);
assert.strictEqual(savedCrossRoom.floor.format, "grid");
assert.strictEqual(savedCrossRoom.floor.rows.length, height);
assert.strictEqual(savedCrossRoom.floor.rows[0].length, width);
assertNoOldFloorNames(savedCrossRoom);
assert.deepStrictEqual(FloorData.readFloor(FloorData.openRoom(savedCrossRoom)), cross);

const unityTiles = FloorData.buildUnityTiles(crossRoom);
assert.strictEqual(unityTiles.length, 3);
const rebuiltFromUnity = FloorData.openUnityTiles(
  { id: "room.test", bounds: { width, height }, entities: [], doors: [] },
  unityTiles
);
assertNoOldFloorNames(rebuiltFromUnity);
assert.deepStrictEqual(FloorData.readFloor(rebuiltFromUnity), cross);

const fullRoom = FloorData.prepareRoom({
  id: "room.full",
  bounds: { width: 12, height: 8 },
  floorObject: "tile.floor-metal",
  tileGridEnabled: false,
  tiles: [],
});
assertNoOldFloorNames(fullRoom);
assert.ok(fullRoom.floor.cells instanceof Uint16Array);
assert.strictEqual(fullRoom.floor.count, 96);
assert.strictEqual(FloorData.isFullFloor(fullRoom), true);
assert.deepStrictEqual(FloorData.buildUnityTiles(fullRoom), [
  {
    object: "tile.floor-metal",
    fill: { from: [-6, -4], to: [6, 4] },
  },
]);
FloorData.setDefaultFloorTile(fullRoom, "tile.floor-hazard");
assert.strictEqual(FloorData.defaultFloorTile(fullRoom), "tile.floor-hazard");
assert.strictEqual(FloorData.getFloorTile(fullRoom, 0, 0), "tile.floor-hazard");

const largeRoom = {
  id: "room.large",
  bounds: { width: 200, height: 200 },
  floor: FloorData.makeFloor(200, 200, "tile.floor-industrial"),
  entities: [],
  doors: [],
};
FloorData.prepareRoom(largeRoom);
assertNoOldFloorNames(largeRoom);
assert.ok(largeRoom.floor.cells instanceof Uint16Array);
assert.strictEqual(largeRoom.floor.cells.length, 40_000);
assert.strictEqual(largeRoom.floor.cells.byteLength, 80_000);
assert.strictEqual(largeRoom.floor.count, 40_000);
assert.strictEqual(Object.prototype.hasOwnProperty.call(largeRoom.floor, "0"), false);

FloorData.clearFloorTile(largeRoom, 10, 12);
assert.strictEqual(FloorData.getFloorTile(largeRoom, 10, 12), null);
assert.strictEqual(largeRoom.floor.count, 39_999);
FloorData.setFloorTile(largeRoom, 10, 12, "tile.floor-metal");
assert.strictEqual(FloorData.getFloorTile(largeRoom, 10, 12), "tile.floor-metal");
assert.strictEqual(largeRoom.floor.count, 40_000);

const resizeRoom = FloorData.prepareRoom({
  id: "room.resize",
  bounds: { width: 3, height: 2 },
  floorObject: "tile.floor-industrial",
  tileGridEnabled: true,
  tiles: [
    { x: 0, y: 0, object: "tile.floor-a" },
    { x: 2, y: 1, object: "tile.floor-b" },
  ],
});
resizeRoom.bounds = { width: 5, height: 4 };
FloorData.prepareRoom(resizeRoom);
assert.strictEqual(resizeRoom.floor.width, 5);
assert.strictEqual(resizeRoom.floor.height, 4);
assert.strictEqual(FloorData.getFloorTile(resizeRoom, 0, 0), "tile.floor-a");
assert.strictEqual(FloorData.getFloorTile(resizeRoom, 2, 1), "tile.floor-b");
assert.strictEqual(FloorData.getFloorTile(resizeRoom, 4, 3), null);
resizeRoom.bounds = { width: 2, height: 1 };
FloorData.prepareRoom(resizeRoom);
assert.strictEqual(FloorData.getFloorTile(resizeRoom, 0, 0), "tile.floor-a");
assert.strictEqual(resizeRoom.floor.count, 1);
assertNoOldFloorNames(resizeRoom);

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
const manyTileRoom = FloorData.prepareRoom(makeOldRoom(65, 1, manyTiles));
const manyTileSave = FloorData.writeFloor(manyTileRoom);
assert.strictEqual(manyTileSave.format, "number-grid");
assert.strictEqual(manyTileSave.legend.length, 66);
assert.deepStrictEqual(
  FloorData.readFloor(FloorData.openRoom({ ...FloorData.saveRoom(manyTileRoom), floor: manyTileSave })),
  manyTiles
);

console.log("Level Maker floor tests passed.");
