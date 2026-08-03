"use strict";

const assert = require("assert");
const fs = require("fs");
const os = require("os");
const path = require("path");
const { projectEnemyDefinitionAssets } = require("./enemy-definition-assets.js");

function writeJson(file, value) {
  fs.mkdirSync(path.dirname(file), { recursive: true });
  fs.writeFileSync(file, `${JSON.stringify(value, null, 2)}\n`, "utf8");
}

function enemy(id, name = id) {
  return {
    schema: 1,
    id,
    name,
    tags: ["droid"],
    hp: 16,
    healthPower: 0.7,
    movement: { kind: "stationary", speed: 0 },
    detectionRange: 16,
    mounts: [],
    attacks: [],
    art: id,
    body: { shape: "circle", radius: 0.45, offset: { x: 0, y: 0 } },
  };
}

const root = fs.mkdtempSync(path.join(os.tmpdir(), "level-maker-enemies-"));
try {
  writeJson(path.join(root, "Content/Enemies/leveling.json"), { maxLevel: 100 });
  writeJson(path.join(root, "Content/Enemies/gunner-droid.json"), enemy("gunner-droid", "Gunner Droid"));
  writeJson(path.join(root, "Content/Enemies/scatter-droid.json"), enemy("scatter-droid", "Scatter Droid"));
  writeJson(path.join(root, "Content/Enemies/wrong-file.json"), enemy("different-id", "Wrong File"));
  writeJson(path.join(root, "Content/Enemies/legacy.json"), { id: "legacy", name: "Legacy" });
  fs.writeFileSync(path.join(root, "Content/Enemies/broken.json"), "{", "utf8");

  assert.deepStrictEqual(projectEnemyDefinitionAssets(root), [
    {
      id: "enemy.gunner-droid",
      definitionId: "gunner-droid",
      label: "Gunner Droid",
      type: "enemy",
      source: "Content/Enemies/gunner-droid.json",
      art: "gunner-droid",
    },
    {
      id: "enemy.scatter-droid",
      definitionId: "scatter-droid",
      label: "Scatter Droid",
      type: "enemy",
      source: "Content/Enemies/scatter-droid.json",
      art: "scatter-droid",
    },
  ]);

  assert.deepStrictEqual(projectEnemyDefinitionAssets(path.join(root, "missing")), []);
  console.log("Level Maker enemy definition asset tests passed.");
} finally {
  fs.rmSync(root, { recursive: true, force: true });
}
