"use strict";

const assert = require("assert");
const fs = require("fs");
const path = require("path");

const root = __dirname;
const appFiles = fs.readdirSync(root)
  .filter(name => /^app-.*\.js$/.test(name))
  .sort();

const forbiddenAppPatterns = [
  [/state\.rooms\b/, "state.rooms"],
  [/state\.connections\b/, "state.connections"],
  [/state\.logic\b/, "state.logic"],
  [/state\.catalog\b/, "state.catalog"],
  [/state\.activeRoomId\b/, "state.activeRoomId"],
  [/\b(?:room|r)\.(?:tiles|floorObject|tileGridEnabled)\b/, "old live room floor property"],
  [/LevelState\.addOldNames\b/, "LevelState.addOldNames"],
];

for (const name of appFiles) {
  const text = fs.readFileSync(path.join(root, name), "utf8");
  for (const [pattern, label] of forbiddenAppPatterns) {
    assert.strictEqual(pattern.test(text), false, `${name} still uses ${label}`);
  }
}

const levelState = fs.readFileSync(path.join(root, "level-state.js"), "utf8");
for (const label of ["addOldNames", "Object.defineProperty(state", "Object.defineProperties(state"]) {
  assert.strictEqual(levelState.includes(label), false, `level-state.js still contains ${label}`);
}

const floorData = fs.readFileSync(path.join(root, "floor-data.js"), "utf8");
for (const label of ["addOldFloorNames", "tileView", "tileViews", "aliasedRooms", "Object.defineProperties(room"]) {
  assert.strictEqual(floorData.includes(label), false, `floor-data.js still contains ${label}`);
}

console.log("Level Maker compatibility-name guard passed.");
