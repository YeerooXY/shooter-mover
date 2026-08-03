"use strict";

const assert = require("assert");
const fs = require("fs");
const path = require("path");

const source = fs.readFileSync(path.join(__dirname, "app-28-level-editor-ux.js"), "utf8");

assert.match(source, /#asset-group-switch\s*\{[\s\S]*display:\s*none\s*!important/);
assert.match(source, /document\.querySelector\("#asset-group-switch"\)\?\.remove\(\)/);
assert.match(source, /unified-asset-palette/);
assert.match(source, /scrollbar-gutter:\s*stable/);
assert.match(source, /data-room-asset-type/);
assert.doesNotMatch(source, /Interactive items/);
assert.doesNotMatch(source, /Static items/);
assert.doesNotMatch(source, /preferredGroupForType/);
assert.doesNotMatch(source, /installGroupReset/);

console.log("app-28 level editor UX source guards passed");
