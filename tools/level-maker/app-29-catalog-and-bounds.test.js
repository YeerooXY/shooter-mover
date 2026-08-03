"use strict";

const assert = require("assert");
const fs = require("fs");
const path = require("path");

const source = fs.readFileSync(
  path.join(__dirname, "app-29-catalog-and-bounds.js"),
  "utf8"
);
const index = fs.readFileSync(path.join(__dirname, "index.html"), "utf8");

assert.match(source, /const coreAssets = defaultCatalog\.map/);
assert.match(source, /for \(const asset of coreAssets\)/);
assert.match(source, /bottom:\s*auto\s*!important/);
assert.match(source, /height:\s*auto\s*!important/);
assert.match(source, /width:\s*max-content\s*!important/);
assert.match(source, /max-height:\s*calc\(100% - 72px\)/);
assert.match(source, /restoreAuthoringCatalog\(\);\s*previousRenderAssets\(\)/);
assert.ok(
  index.indexOf("app-29-catalog-and-bounds.js")
    > index.indexOf("app-28-level-editor-ux.js"),
  "catalog and bounds repair must load after the unified picker"
);

console.log("app-29 catalog and bounds source guards passed");
