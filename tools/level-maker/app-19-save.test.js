"use strict";

const fs = require("fs");
const vm = require("vm");
const LevelSave = require("./level-save");

const listeners = new Map();
const elements = new Map();
const local = new Map();

function makeTarget() {
  return {
    addEventListener(name, callback) {
      listeners.set(`${name}:${callback.name}`, callback);
    },
    removeEventListener() {},
  };
}

const context = {
  console,
  LevelSave,
  localStorage: {
    getItem(key) {
      return local.get(key) || null;
    },
    setItem(key, value) {
      local.set(key, value);
    },
  },
  setTimeout,
  clearTimeout,
  structuredClone,
  writeRecoveryDraft() {},
  scheduleRecoverySave() {},
  recoverySaveTimer: null,
  recoveryRestoredAt: "",
  recoveryWriteError: "",
  defaultCatalog: [{ id: "enemy.base", type: "enemy", source: "default" }],
  clone: structuredClone,
  state: {
    format: "shooter-mover-web-level-project",
    schemaVersion: 2,
    level: {
      id: "level.test",
      name: "Test",
      targetFolder: "test",
      startRoomId: "room.start",
    },
    rooms: [
      {
        id: "room.start",
        entities: [],
        doors: [],
        bounds: { width: 4, height: 4 },
        tiles: [],
      },
    ],
    connections: [],
    logic: [],
    catalog: [{ id: "enemy.cached", type: "enemy", source: "repo" }],
    activeRoomId: "room.start",
    editor: { tool: "tile", selectedAssetId: "enemy.cached" },
  },
  initialState() {
    return { editor: { tool: "select", zoom: 32, pan: [0, 0] } };
  },
  normalize() {},
  document: makeTarget(),
  window: makeTarget(),
  canvas: makeTarget(),
  validate() {
    return [];
  },
  showValidation() {},
  setStatus() {},
  helper: async () => ({
    projectPath: "Content/Levels/test.level.json",
    fileCount: 1,
  }),
  buildExportFiles() {
    return {};
  },
  pretty: value => JSON.stringify(value, null, 2),
  cleanSlug: value => value,
  download() {},
  Blob,
  pushHistory() {},
  snapshot() {
    return "{}";
  },
  fitRoom() {},
  renderAll() {},
  alert() {},
  prompt() {
    return null;
  },
  encodeURIComponent,
  $: selector => {
    if (!elements.has(selector)) elements.set(selector, {});
    return elements.get(selector);
  },
};

vm.createContext(context);
vm.runInContext(fs.readFileSync("./app-19-save.js", "utf8"), context);

const savedLevel = LevelSave.makeLevelFile(context.state);
assertMissing(savedLevel, "editor");
assertMissing(savedLevel, "catalog");
assertMissing(savedLevel, "activeRoomId");

context.writeRecoveryDraft();
if (!local.has(LevelSave.levelRecoveryKey())) {
  throw new Error("Level recovery was not saved.");
}
if (!local.has(LevelSave.editorStorageKey("level.test"))) {
  throw new Error("Editor state was not saved.");
}

console.log("Level Maker browser save smoke test passed.");

function assertMissing(value, key) {
  if (key in value) throw new Error(`Clean level save leaked ${key}.`);
}
