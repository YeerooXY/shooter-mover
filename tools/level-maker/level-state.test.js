"use strict";

const assert = require("assert");
const LevelState = require("./level-state");

const state = {
  format: "shooter-mover-web-level-project",
  schemaVersion: 2,
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
  catalog: [{ id: "enemy.test", type: "enemy" }],
  activeRoomId: "room.two",
  editor: {
    tool: "tile",
    selectedId: "enemy.one",
    zoom: 48,
  },
};

LevelState.addOldNames(state);
assert.strictEqual(state.level.rooms.length, 2);
assert.strictEqual(state.level.connections.length, 1);
assert.strictEqual(state.level.logic.length, 1);
assert.strictEqual(state.assets.length, 1);
assert.strictEqual(state.editor.activeRoomId, "room.two");
assert.strictEqual(state.rooms, state.level.rooms);
assert.strictEqual(state.catalog, state.assets);
assert.deepStrictEqual(Object.keys(state), [
  "format",
  "schemaVersion",
  "level",
  "editor",
  "assets",
]);

state.rooms = [{ id: "room.changed", entities: [], doors: [] }];
assert.strictEqual(state.level.rooms[0].id, "room.changed");
state.activeRoomId = "room.changed";
assert.strictEqual(state.editor.activeRoomId, "room.changed");

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

const cleanLevel = LevelState.levelFile(state);
assert.ok(!("rooms" in cleanLevel.level));
assert.ok(!("connections" in cleanLevel.level));
assert.ok(!("logic" in cleanLevel.level));
assert.strictEqual(cleanLevel.rooms.length, 2);

console.log("Level Maker state tests passed.");
