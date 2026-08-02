"use strict";

const assert = require("assert");
const LevelSave = require("./level-save");

const crossTiles = [];
for (let y = 0; y < 5; y++) crossTiles.push({ x: 2, y, object: "tile.floor-industrial" });
for (let x = 0; x < 5; x++) {
  if (x !== 2) crossTiles.push({ x, y: 2, object: "tile.floor-industrial" });
}

const state = {
  format: "shooter-mover-web-level-project",
  editorVersion: 1,
  schemaVersion: 2,
  level: {
    id: "level.test",
    name: "Test Level",
    targetFolder: "test",
    startRoomId: "room.test-start",
    finalRoomId: "room.test-start",
    finalExitDoorId: "door.test-exit",
  },
  rooms: [
    {
      id: "room.test-start",
      bounds: { width: 5, height: 5 },
      floorObject: "tile.floor-industrial",
      tileGridEnabled: true,
      tiles: crossTiles,
      entities: [],
      doors: [],
    },
  ],
  connections: [],
  logic: [],
  catalog: [
    { id: "enemy.test", type: "enemy", source: "EnemyCatalog" },
    { id: "tile.custom", type: "floor", source: "manual" },
  ],
  activeRoomId: "room.test-start",
  editor: {
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
assert.strictEqual(levelFile.rooms[0].floor.format, "grid");
assert.ok(!("tiles" in levelFile.rooms[0]));
assert.ok(!("tileGridEnabled" in levelFile.rooms[0]));

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
assert.strictEqual(opened.rooms, opened.level.rooms);
assert.strictEqual(opened.activeRoomId, "room.test-start");
assert.strictEqual(opened.editor.tool, "tile");
assert.strictEqual(opened.editor.zoom, 48);
assert.strictEqual(opened.level.rooms[0].tiles.length, 9);
assert.deepStrictEqual(opened.catalog, editorFile.customAssets);
assert.deepStrictEqual(Object.keys(opened), [
  "format",
  "schemaVersion",
  "level",
  "editor",
  "assets",
]);

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
const oldEditorFile = LevelSave.makeEditorFile(oldCombinedFile);
const upgraded = LevelSave.openLevelFile(
  oldCombinedFile,
  oldEditorFile,
  { tool: "select", zoom: 32 }
);
assert.strictEqual(upgraded.editor.tool, "door");
assert.strictEqual(upgraded.level.rooms[0].tileGridEnabled, false);
assert.deepStrictEqual(upgraded.catalog, [
  { id: "door.custom", type: "door", source: "manual" },
]);

console.log("Level Maker clean-save tests passed.");
