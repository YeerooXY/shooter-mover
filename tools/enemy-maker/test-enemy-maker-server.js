"use strict";

const assert = require("assert");
const fs = require("fs");
const os = require("os");
const path = require("path");
const { createEnemyMaker } = require("./server.js");

function writeJson(file, value) {
  fs.mkdirSync(path.dirname(file), { recursive: true });
  fs.writeFileSync(file, `${JSON.stringify(value, null, 2)}\n`);
}

async function readJson(url, options) {
  const response = await fetch(url, options);
  const value = await response.json();
  return { response, value };
}

async function main() {
  const root = fs.mkdtempSync(path.join(os.tmpdir(), "enemy-maker-"));
  const leveling = {
    minLevel: 1,
    maxLevel: 100,
    strengthAtMax: 50,
    damagePower: 0.35,
    colors: [
      { level: 1, color: "#55D66B" },
      { level: 100, color: "#A653DF" }
    ]
  };
  writeJson(path.join(root, "Content/Enemies/leveling.json"), leveling);
  writeJson(path.join(root, "Content/Weapons/normal-firearm/rattler/weapon.json"), { name: "Rattler" });
  writeJson(path.join(root, "Content/Weapons/normal-firearm/rattler/mk1.json"), { available: true });
  writeJson(path.join(root, "Content/Weapons/normal-firearm/rattler/mk2.json"), { available: true });
  writeJson(path.join(root, "Content/Weapons/normal-firearm/rattler/mk3.json"), { available: false });

  const maker = createEnemyMaker({ root, port: 0 });
  const port = await maker.start();
  const base = `http://127.0.0.1:${port}`;

  try {
    const status = await readJson(`${base}/api/status`);
    assert.strictEqual(status.response.status, 200);
    const token = status.value.token;
    assert(token);

    const guns = await readJson(`${base}/api/guns`);
    assert.deepStrictEqual(guns.value.guns.map(gun => gun.id), ["rattler.mk1", "rattler.mk2"]);

    const enemy = {
      id: "rattler-droid",
      name: "Rattler Droid",
      type: "shooter",
      hp: 16,
      speed: 3.5,
      move: "strafe",
      gun: "rattler.mk1",
      range: 7,
      detect: 16,
      scale: 0.7,
      drops: "normal",
      art: "rattler-droid",
      body: { shape: "circle", radius: 0.45, offset: { x: 0, y: 0 } }
    };

    const save = await readJson(`${base}/api/enemy`, {
      method: "PUT",
      headers: { "Content-Type": "application/json", "x-enemy-maker-token": token },
      body: JSON.stringify({ enemy, previousId: null })
    });
    assert.strictEqual(save.response.status, 200);
    assert(fs.existsSync(path.join(root, "Content/Enemies/rattler-droid.json")));

    const loaded = await readJson(`${base}/api/enemy?id=rattler-droid`);
    assert.deepStrictEqual(loaded.value.enemy, enemy);

    const overwriteAsNew = await readJson(`${base}/api/enemy`, {
      method: "PUT",
      headers: { "Content-Type": "application/json", "x-enemy-maker-token": token },
      body: JSON.stringify({ enemy, previousId: null })
    });
    assert.strictEqual(overwriteAsNew.response.status, 409);

    const renamed = { ...enemy, id: "renamed-droid" };
    const rename = await readJson(`${base}/api/enemy`, {
      method: "PUT",
      headers: { "Content-Type": "application/json", "x-enemy-maker-token": token },
      body: JSON.stringify({ enemy: renamed, previousId: "rattler-droid" })
    });
    assert.strictEqual(rename.response.status, 400);
    assert(!fs.existsSync(path.join(root, "Content/Enemies/renamed-droid.json")));

    const unknownGun = { ...enemy, id: "bad-gun-droid", gun: "missing.mk1" };
    const badGun = await readJson(`${base}/api/enemy`, {
      method: "PUT",
      headers: { "Content-Type": "application/json", "x-enemy-maker-token": token },
      body: JSON.stringify({ enemy: unknownGun, previousId: null })
    });
    assert.strictEqual(badGun.response.status, 400);
    assert(badGun.value.errors.some(error => error.includes("not a canonical definition")));

    const ellipse = { ...enemy, id: "ellipse-droid", body: { shape: "ellipse", size: { x: 1, y: 2 }, offset: { x: 0, y: 0 } } };
    const badShape = await readJson(`${base}/api/enemy`, {
      method: "PUT",
      headers: { "Content-Type": "application/json", "x-enemy-maker-token": token },
      body: JSON.stringify({ enemy: ellipse, previousId: null })
    });
    assert.strictEqual(badShape.response.status, 400);

    console.log("Enemy Maker server tests passed.");
  } finally {
    await new Promise(resolve => maker.server.close(resolve));
    fs.rmSync(root, { recursive: true, force: true });
  }
}

main().catch(error => {
  console.error(error.stack || error.message || String(error));
  process.exitCode = 1;
});
