"use strict";

const assert = require("assert");
const FloorData = require("./floor-data");
const LevelSave = require("./level-save");

const room = {
  id: "room.test-start",
  bounds: { width: 5, height: 5 },
  floor: FloorData.makeFloor(5, 5, null),
  entities: [],
  doors: [],
};
for (let y = 0; y < 5; y++) FloorData.setFloorTile(room, 2, y, "tile.floor-industrial");
for (let x = 0; x < 5; x++) FloorData.setFloorTile(room, x, 2, "tile.floor-industrial");

const state = {
  format: "shooter-mover-web-level-project",
  schemaVersion: 4,
  level: {
    id: "level.test",
    name: "Test Level",
    targetFolder: "test",
    startRoomId: "room.test-start",
    finalRoomId: "room.test-start",
    finalExitDoorId: "door.test-exit",
    rooms: [room],
    connections: [],
    logic: [],
  },
  assets: [
    { id: "enemy.test", type: "enemy", source: "EnemyCatalog" },
    { id: "tile.custom", type: "floor", source: "manual" },
  ],
  editor: {
    activeRoomId: "room.test-start",
    customAssets: [],
    tool: "tile",
    selectedAssetId: "tile.custom",
    zoom: 48,
    pan: [12, -4],
    brushWidth: 4,
    brushHeight: 2,
  },
};

const levelFile = LevelSave.makeLevelFile(state);
assert.strictEqual(levelFile.schemaVersion, 4);
assert.deepStrictEqual(Object.keys(levelFile), [
  "format",
  "schemaVersion",
  "level",
  "rooms",
  "connections",
  "logic",
]);
assert.ok(!("editor" in levelFile));
assert.ok(!("activeRoomId" in levelFile));
assert.ok(!("catalog" in levelFile));
assert.ok(!JSON.stringify(levelFile).includes("selectedAssetId"));
assert.ok(!JSON.stringify(levelFile).includes('"cells"'));
assert.strictEqual(levelFile.rooms[0].floor.format, "grid");
for (const oldName of ["tiles", "tileGridEnabled", "floorObject"]) {
  assert.strictEqual(oldName in levelFile.rooms[0], false);
}

const editorFile = LevelSave.makeEditorFile(state);
assert.strictEqual(editorFile.activeRoomId, "room.test-start");
assert.strictEqual(editorFile.editor.tool, "tile");
assert.deepStrictEqual(editorFile.customAssets, [
  { id: "tile.custom", type: "floor", source: "manual" },
]);

const opened = LevelSave.openLevelFile(levelFile, editorFile, {
  tool: "select",
  zoom: 32,
  pan: [0, 0],
});
assert.strictEqual(opened.level.id, "level.test");
assert.strictEqual(opened.level.rooms.length, 1);
assert.strictEqual(opened.editor.activeRoomId, "room.test-start");
assert.strictEqual(opened.editor.tool, "tile");
assert.strictEqual(opened.editor.zoom, 48);
assert.ok(opened.level.rooms[0].floor.cells instanceof Uint16Array);
assert.strictEqual(opened.level.rooms[0].floor.cells.length, 25);
assert.strictEqual(opened.level.rooms[0].floor.count, 9);
assert.deepStrictEqual(opened.assets, editorFile.customAssets);
assert.deepStrictEqual(Object.keys(opened), [
  "format",
  "schemaVersion",
  "level",
  "editor",
  "assets",
]);
for (const oldName of ["rooms", "connections", "logic", "catalog", "activeRoomId"]) {
  assert.strictEqual(oldName in opened, false, `${oldName} must not exist on live state`);
}
for (const oldName of ["tiles", "tileGridEnabled", "floorObject"]) {
  assert.strictEqual(oldName in opened.level.rooms[0], false, `${oldName} must not exist on live rooms`);
}

const compactAgain = LevelSave.makeLevelFile(opened);
assert.deepStrictEqual(compactAgain.rooms[0].floor, levelFile.rooms[0].floor);
assert.ok(!JSON.stringify(compactAgain).includes('"cells"'));

const oldCombinedFile = {
  format: "shooter-mover-web-level-project",
  schemaVersion: 2,
  level: {
    id: "level.old-test",
    name: "Old Test",
    targetFolder: "old-test",
    startRoomId: "room.old-start",
  },
  rooms: [
    {
      id: "room.old-start",
      bounds: { width: 3, height: 2 },
      floorObject: "tile.floor-metal",
      tileGridEnabled: false,
      tiles: [],
      entities: [],
      doors: [],
    },
  ],
  connections: [],
  logic: [],
  activeRoomId: "room.old-start",
  editor: { tool: "door", zoom: 20 },
  catalog: [
    { id: "enemy.cached", type: "enemy", source: "EnemyCatalog" },
    { id: "door.custom", type: "door", source: "manual" },
  ],
};
const upgraded = LevelSave.openLevelFile(
  oldCombinedFile,
  null,
  { tool: "select", zoom: 32 }
);
assert.strictEqual(upgraded.editor.tool, "door");
assert.strictEqual(upgraded.editor.activeRoomId, "room.old-start");
assert.ok(upgraded.level.rooms[0].floor.cells instanceof Uint16Array);
assert.strictEqual(FloorData.isFullFloor(upgraded.level.rooms[0]), true);
assert.deepStrictEqual(upgraded.assets, [
  { id: "door.custom", type: "door", source: "manual" },
]);
for (const oldName of ["rooms", "connections", "logic", "catalog", "activeRoomId"]) {
  assert.strictEqual(oldName in upgraded, false);
}
for (const oldName of ["tiles", "tileGridEnabled", "floorObject"]) {
  assert.strictEqual(oldName in upgraded.level.rooms[0], false);
}

const damaged = JSON.parse(JSON.stringify(levelFile));
damaged.rooms[0].floor.rows[0] = "bad";
assert.throws(
  () => LevelSave.checkLevelFile(damaged),
  /must contain exactly 5 cells/
);

const unknownSymbol = JSON.parse(JSON.stringify(levelFile));
unknownSymbol.rooms[0].floor.rows[0] = "....?";
assert.throws(
  () => LevelSave.checkLevelFile(unknownSymbol),
  /uses an unknown symbol/
);

console.log("Level Maker clean-save tests passed.");
