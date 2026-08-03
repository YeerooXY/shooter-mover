"use strict";

const assert = require("node:assert/strict");
const fs = require("node:fs");
const path = require("node:path");

const source = fs.readFileSync(
  path.join(__dirname, "app-25-map-door-shortcut.js"),
  "utf8"
);

assert.match(source, /state\.editor\.viewMode !== "map"/);
assert.match(source, /event\.key\.toLowerCase\(\) !== "d"/);
assert.match(source, /event\.stopImmediatePropagation\(\)/);
assert.match(source, /setMapMode\("connect"\)/);
assert.match(source, /Click inside a room to place a door on its nearest edge/);

console.log("map door shortcut tests passed");
