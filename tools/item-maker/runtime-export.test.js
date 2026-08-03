"use strict";

const assert = require("assert");
const fs = require("fs");
const os = require("os");
const path = require("path");
const {
  GENERATED_RELATIVE_PATH,
  exportRuntimeCatalog
} = require("./runtime-export.js");

const root = fs.mkdtempSync(path.join(os.tmpdir(), "weapon-runtime-export-"));
const folder = path.join(root, "Content", "Weapons", "normal-firearm", "rattler");
fs.mkdirSync(folder, { recursive: true });

fs.writeFileSync(path.join(folder, "weapon.json"), JSON.stringify({
  name: "Rattler",
  category: "normal-firearm",
  rarity: "common",
  projectileType: "bullet",
  damageType: "physical",
  fire: { mode: "automatic", rate: 4 },
  shot: { projectiles: 1, spread: 0 },
  projectile: { speed: 20, radius: 0.1, range: 25 },
  impact: { pierce: 1, ricochet: 0, knockback: 0 },
  art: {
    delivery: "gun-delivery-art.normal-physical.v1",
    trail: "gun-trail-art.normal-physical.v1",
    impact: "gun-impact-art.normal-physical.v1"
  }
}, null, 2));

for (let mark = 1; mark <= 3; mark++) {
  fs.writeFileSync(path.join(folder, `mk${mark}.json`), JSON.stringify({
    peakLevel: mark === 1 ? 1 : mark === 2 ? 25 : 50,
    damage: mark,
    art: {
      side: `gun-art.rattler.mk${mark}.side-v1`,
      mounted: `gun-art.rattler.mk${mark}.mounted-top-v1`
    }
  }, null, 2));
}

try {
  const first = exportRuntimeCatalog(root);
  assert.equal(first.familyCount, 1);
  assert.equal(first.definitionCount, 3);
  assert.equal(first.generatedChanged, true);

  const generated = fs.readFileSync(
    path.join(root, GENERATED_RELATIVE_PATH),
    "utf8"
  );
  assert.match(generated, /FamilyCount = 1/);
  assert.match(generated, /DefinitionCount = 3/);
  assert.match(generated, /rattler\.mk1/);
  assert.match(generated, /gun-art\.rattler\.mk3\.mounted-top-v1/);

  const second = exportRuntimeCatalog(root);
  assert.equal(second.generatedChanged, false);
  console.log("Weapon runtime catalogue export passed.");
} finally {
  fs.rmSync(root, { recursive: true, force: true });
}
