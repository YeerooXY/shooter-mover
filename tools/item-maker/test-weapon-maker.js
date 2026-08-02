"use strict";

const assert = require("assert");
const fs = require("fs");
const os = require("os");
const path = require("path");
const { execFileSync } = require("child_process");
const WeaponDps = require("./weapon-dps");

const toolRoot = __dirname;
const validator = path.join(toolRoot, "validate-weapon-folder.js");
const gameplayScript = path.join(toolRoot, "weapon-gameplay-maker.js");
const qolScript = path.join(toolRoot, "weapon-maker-qol.js");
const balanceScript = path.join(toolRoot, "weapon-balance.js");

function baseWeapon(overrides = {}) {
  return {
    name: "Test Weapon",
    category: "test",
    rarity: "common",
    projectileType: "bullet",
    damageType: "physical",
    fire: { mode: "automatic", rate: 4 },
    shot: { projectiles: 1, spread: 0 },
    projectile: { speed: 20, radius: 0.1, range: 25 },
    impact: { pierce: 1, ricochet: 0, knockback: 0 },
    art: {
      delivery: "test-delivery",
      trail: "test-trail",
      impact: "test-impact"
    },
    ...overrides
  };
}

function baseMarks() {
  return [1, 2, 3].map(mark => ({
    peakLevel: mark === 1 ? 1 : mark === 2 ? 25 : 50,
    damage: 1,
    art: {
      side: `test-mk${mark}-side`,
      mounted: `test-mk${mark}-mounted`
    }
  }));
}

function writeFolder(root, weapon, marks) {
  const folder = path.join(root, "test_weapon");
  fs.mkdirSync(folder, { recursive: true });
  fs.writeFileSync(path.join(folder, "weapon.json"), JSON.stringify(weapon, null, 2));
  marks.forEach((mark, index) => fs.writeFileSync(path.join(folder, `mk${index + 1}.json`), JSON.stringify(mark, null, 2)));
  return folder;
}

function validateCase(name, weapon, marks, shouldPass = true) {
  const root = fs.mkdtempSync(path.join(os.tmpdir(), "weapon-maker-"));
  const folder = writeFolder(root, weapon, marks);
  let error = null;
  try {
    execFileSync(process.execPath, [validator, folder], { encoding: "utf8", stdio: ["ignore", "pipe", "pipe"] });
  } catch (caught) {
    error = String(caught.stderr || caught.stdout || caught.message).trim();
  } finally {
    fs.rmSync(root, { recursive: true, force: true });
  }

  if (shouldPass) assert.strictEqual(error, null, `${name} should pass: ${error}`);
  else assert.ok(error, `${name} should fail`);
}

[gameplayScript, qolScript, balanceScript].forEach(script => {
  execFileSync(process.execPath, ["--check", script], { stdio: "pipe" });
});

validateCase("automatic weapon", baseWeapon(), baseMarks());

{
  const weapon = baseWeapon();
  delete weapon.fire;
  const marks = baseMarks();
  marks[0].fire = { mode: "semi-automatic", rate: 2 };
  marks[1].fire = { mode: "automatic", rate: 4 };
  marks[2].fire = { mode: "burst", rate: 4 / 3, shotsPerBurst: 3, secondsBetweenShots: 0.08 };
  marks.forEach((mark, index) => {
    mark.homing = {
      acquisitionRange: 20 + (index * 2),
      turnRate: 150 + (index * 30),
      activationDelay: 0.15 - (index * 0.05),
      targetPolicy: "closest-to-aim",
      reacquire: true
    };
  });
  validateCase("Mark-specific burst and homing", weapon, marks);
}

{
  const weapon = baseWeapon({
    damageType: "chemical",
    dot: { refreshDuration: true }
  });
  const marks = baseMarks();
  marks.forEach((mark, index) => {
    mark.dot = {
      damagePerSecond: 1.5 + (index * 0.75),
      duration: 3 + index,
      ticksPerSecond: 3 + index,
      maxStacks: 1 + index
    };
  });
  validateCase("stacking damage", weapon, marks);
}

{
  const weapon = baseWeapon({
    projectileType: "rocket",
    damageType: "thermal",
    projectile: { speed: 18, radius: 0.28, range: 38 }
  });
  const marks = baseMarks();
  marks.forEach((mark, index) => {
    mark.explosion = {
      radius: 2 + (index * 0.5),
      edgeDamageMultiplier: 0.5 - (index * 0.1)
    };
  });
  validateCase("rocket explosion", weapon, marks);
}

{
  const weapon = baseWeapon({ projectileType: "beam", beam: { range: 25, width: 0.2 } });
  delete weapon.projectile;
  validateCase("beam", weapon, baseMarks());
}

{
  const weapon = baseWeapon({
    fire: { mode: "burst", rate: 20, shotsPerBurst: 3, secondsBetweenShots: 0.08 }
  });
  validateCase("impossible burst timing", weapon, baseMarks(), false);
}

assert.strictEqual(WeaponDps.calculate({ fire: { mode: "automatic", rate: 4 }, shot: { projectiles: 1 }, damage: 1 }).totalDps, 4);
assert.strictEqual(WeaponDps.calculate({ fire: { mode: "automatic", rate: 2 }, shot: { projectiles: 3 }, damage: 1 }).totalDps, 6);
assert.strictEqual(WeaponDps.calculate({ fire: { mode: "burst", rate: 4 / 3, shotsPerBurst: 3 }, shot: { projectiles: 1 }, damage: 1 }).totalDps, 4);

{
  const result = WeaponDps.calculate({
    fire: { mode: "automatic", rate: 1 },
    shot: { projectiles: 1 },
    damage: 4,
    dot: { damagePerSecond: 2, duration: 3, maxStacks: 3 }
  }, 12);
  assert.strictEqual(result.directDps, 4);
  assert.strictEqual(result.dotDps, 6);
  assert.strictEqual(result.totalDps, 10);
  assert.strictEqual(result.suggestedDamage, 6);
}

{
  const curve = WeaponDps.generateCurve(4, 114, 110, "linear");
  assert.strictEqual(curve.targets["1"], 4);
  assert.strictEqual(curve.targets["110"], 114);
}

{
  const curve = WeaponDps.generateCurve(4, 400, 110, "exponential");
  assert.strictEqual(curve.targets["1"], 4);
  assert.strictEqual(curve.targets["110"], 400);
}

assert.deepStrictEqual(WeaponDps.validateSettings(WeaponDps.defaultSettings()), []);

console.log("Weapon Maker checks passed: gameplay shapes, invalid burst rejection, DPS math, and browser-only curve calculations.");
