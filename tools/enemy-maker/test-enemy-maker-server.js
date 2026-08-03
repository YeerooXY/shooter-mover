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

async function json(url, options = {}) {
  const response = await fetch(url, options);
  const body = await response.json();
  return { response, body };
}

(async () => {
  const root = fs.mkdtempSync(path.join(os.tmpdir(), "enemy-maker-"));
  writeJson(path.join(root, "Content/Enemies/leveling.json"), {
    minLevel: 1,
    maxLevel: 100,
    strengthAtMax: 50,
    damagePower: 0.35,
    colors: [
      { level: 1, color: "#55D66B" },
      { level: 100, color: "#A653DF" }
    ]
  });
  writeJson(path.join(root, "Content/EnemyShots/small-bullet.json"), {
    schema: 1,
    id: "small-bullet",
    delivery: { kind: "projectile", speed: 32, radius: 0.06, range: 18 },
    impact: { pierce: 1, ricochet: 0, knockback: 0 },
    art: { delivery: "enemy-shot.small-bullet" }
  });

  const maker = createEnemyMaker({ root, port: 0 });
  const port = await maker.start();
  const base = `http://127.0.0.1:${port}`;

  try {
    const status = await json(`${base}/api/status`);
    assert.strictEqual(status.response.status, 200);
    const token = status.body.token;
    assert.strictEqual(status.body.shots, "Content/EnemyShots");

    const shotList = await json(`${base}/api/shots`);
    assert.deepStrictEqual(shotList.body.shots.map(item => item.id), ["small-bullet"]);

    const enemy = {
      schema: 1,
      id: "gunner-droid",
      name: "Gunner Droid",
      tags: ["droid", "mobile", "ranged"],
      hp: 16,
      healthPower: 0.7,
      movement: { kind: "strafe", speed: 3.5 },
      detectionRange: 16,
      mounts: [
        { id: "left-gun", position: { x: -0.25, y: 0.2 }, rotation: 0 },
        { id: "right-gun", position: { x: 0.25, y: 0.2 }, rotation: 0 }
      ],
      attacks: [{
        id: "dual-burst",
        kind: "shot",
        shot: "small-bullet",
        emitters: ["left-gun", "right-gun"],
        firePattern: "simultaneous",
        cooldown: 1.5,
        sequence: { triggers: 4, interval: 0.2 },
        volley: { shotsPerTrigger: 1, spread: 0, distribution: "even" },
        range: { min: 2, max: 12 },
        damage: [{ type: "kinetic", amount: 3 }]
      }],
      drops: "normal",
      art: "gunner-droid",
      body: { shape: "circle", radius: 0.45, offset: { x: 0, y: 0 } }
    };

    const save = await json(`${base}/api/enemy`, {
      method: "PUT",
      headers: { "Content-Type": "application/json", "x-enemy-maker-token": token },
      body: JSON.stringify({ enemy, previousId: null })
    });
    assert.strictEqual(save.response.status, 200);
    assert(fs.existsSync(path.join(root, "Content/Enemies/gunner-droid.json")));

    const load = await json(`${base}/api/enemy?id=gunner-droid`);
    assert.deepStrictEqual(load.body.enemy, enemy);

    const list = await json(`${base}/api/enemies`);
    assert.strictEqual(list.body.enemies[0].mountCount, 2);
    assert.strictEqual(list.body.enemies[0].attackCount, 1);

    const duplicate = await json(`${base}/api/enemy`, {
      method: "PUT",
      headers: { "Content-Type": "application/json", "x-enemy-maker-token": token },
      body: JSON.stringify({ enemy, previousId: null })
    });
    assert.strictEqual(duplicate.response.status, 409);

    const renamed = { ...enemy, id: "renamed-droid" };
    const rename = await json(`${base}/api/enemy`, {
      method: "PUT",
      headers: { "Content-Type": "application/json", "x-enemy-maker-token": token },
      body: JSON.stringify({ enemy: renamed, previousId: "gunner-droid" })
    });
    assert.strictEqual(rename.response.status, 400);

    const unknownShot = structuredClone(enemy);
    unknownShot.attacks[0].shot = "missing-shot";
    const unknown = await json(`${base}/api/enemy`, {
      method: "PUT",
      headers: { "Content-Type": "application/json", "x-enemy-maker-token": token },
      body: JSON.stringify({ enemy: unknownShot, previousId: "gunner-droid" })
    });
    assert.strictEqual(unknown.response.status, 400);
    assert(unknown.body.errors.some(error => error.includes("unknown Enemy Shot")));

    const badEmitter = structuredClone(enemy);
    badEmitter.attacks[0].emitters = ["missing-mount"];
    const emitter = await json(`${base}/api/enemy`, {
      method: "PUT",
      headers: { "Content-Type": "application/json", "x-enemy-maker-token": token },
      body: JSON.stringify({ enemy: badEmitter, previousId: "gunner-droid" })
    });
    assert.strictEqual(emitter.response.status, 400);
    assert(emitter.body.errors.some(error => error.includes("unknown mount")));

    const ellipse = structuredClone(enemy);
    ellipse.body = { shape: "ellipse", size: { x: 1, y: 2 }, offset: { x: 0, y: 0 } };
    const futureShape = await json(`${base}/api/enemy`, {
      method: "PUT",
      headers: { "Content-Type": "application/json", "x-enemy-maker-token": token },
      body: JSON.stringify({ enemy: ellipse, previousId: "gunner-droid" })
    });
    assert.strictEqual(futureShape.response.status, 400);
    assert(futureShape.body.errors.some(error => error.includes("reserved but not supported")));

    console.log("Enemy Maker HTTP tests passed.");
  } finally {
    await new Promise(resolve => maker.server.close(resolve));
    fs.rmSync(root, { recursive: true, force: true });
  }
})().catch(error => {
  console.error(error.stack || error.message || String(error));
  process.exitCode = 1;
});
