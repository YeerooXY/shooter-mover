"use strict";

const assert = require("assert");
const LevelState = require("./level-state");

const state = LevelState.makeState(
  {
    format: "shooter-mover-web-level-project",
    schemaVersion: 4,
    level: {
      id: "level.test",
      name: "Test",
      targetFolder: "test",
      startRoomId: "room.one",
    },
    rooms: [
      { id: "room.one", entities: [{ id: "enemy.one" }], doors: [] },
      { id: "room.two", entities: [], doors: [] },
    ],
    connections: [{ id: "connection.one" }],
    logic: [{ id: "logic.one" }],
  },
  {
    version: 1,
    levelId: "level.test",
    activeRoomId: "room.two",
    editor: {
      tool: "tile",
      selectedId: "enemy.one",
      zoom: 48,
    },
    customAssets: [],
  },
  [{ id: "enemy.test", type: "enemy" }],
  { tool: "select", zoom: 32 }
);

assert.strictEqual(state.level.rooms.length, 2);
assert.strictEqual(state.level.connections.length, 1);
assert.strictEqual(state.level.logic.length, 1);
assert.strictEqual(state.assets.length, 1);
assert.strictEqual(state.editor.activeRoomId, "room.two");
assert.deepStrictEqual(Object.keys(state), [
  "format",
  "schemaVersion",
  "level",
  "editor",
  "assets",
]);
for (const oldName of ["rooms", "connections", "logic", "catalog", "activeRoomId"]) {
  assert.strictEqual(oldName in state, false, `${oldName} must not exist on live state`);
}

const editorBeforeUndo = JSON.parse(JSON.stringify(state.editor));
const snapshot = LevelState.levelSnapshot(state);
state.level.name = "Changed name";
state.editor.zoom = 99;
LevelState.restoreLevel(state, snapshot);
assert.strictEqual(state.level.name, "Test");
assert.strictEqual(state.editor.zoom, 99);
assert.notDeepStrictEqual(state.editor, editorBeforeUndo);

state.editor.activeRoomId = "room.missing";
state.editor.selectedId = "enemy.missing";
LevelState.fixEditor(state);
assert.strictEqual(state.editor.activeRoomId, "room.one");
assert.strictEqual(state.editor.selectedId, null);

state.level.startRoomId = "room.deleted";
state.editor.activeRoomId = "room.missing";
LevelState.fixEditor(state);
assert.strictEqual(state.editor.activeRoomId, "room.one");

const cleanLevel = LevelState.levelFile(state);
assert.ok(!("rooms" in cleanLevel.level));
assert.ok(!("connections" in cleanLevel.level));
assert.ok(!("logic" in cleanLevel.level));
assert.strictEqual(cleanLevel.rooms.length, 2);

console.log("Level Maker state tests passed.");
