"use strict";

const assert = require("assert");
const LevelSave = require("./level-save");

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
      bounds: { width: 24, height: 14 },
      entities: [],
      doors: [],
      tiles: [],
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
assert.strictEqual(levelFile.schemaVersion, 3);
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
assert.strictEqual(opened.activeRoomId, "room.test-start");
assert.strictEqual(opened.editor.tool, "tile");
assert.strictEqual(opened.editor.zoom, 48);
assert.deepStrictEqual(opened.catalog, editorFile.customAssets);

const oldCombinedFile = {
  ...levelFile,
  schemaVersion: 2,
  activeRoomId: "room.test-start",
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
assert.deepStrictEqual(upgraded.catalog, [
  { id: "door.custom", type: "door", source: "manual" },
]);

console.log("Level Maker clean-save tests passed.");
