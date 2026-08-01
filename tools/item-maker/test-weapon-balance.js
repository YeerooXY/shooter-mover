"use strict";

const assert = require("assert");
const fs = require("fs");
const path = require("path");
const WeaponDps = require("./weapon-dps");

const balanceFile = path.join(__dirname, "..", "..", "Content", "Balance", "weapon-dps-targets.json");
const stored = JSON.parse(fs.readFileSync(balanceFile, "utf8"));

assert.deepStrictEqual(WeaponDps.validateTargets(stored), []);
assert.strictEqual(WeaponDps.targetAtLevel(stored, 1), 4);
assert.strictEqual(WeaponDps.targetAtLevel(stored, 110), 200);
assert.ok(Math.abs(WeaponDps.targetAtLevel(stored, 122) - 307.6611) < 0.0001);
assert.strictEqual(WeaponDps.rarityTargetAtLevel(stored, 110, "common"), 200);
assert.strictEqual(WeaponDps.rarityTargetAtLevel(stored, 110, "epic"), 332);
assert.strictEqual(WeaponDps.rarityTargetAtLevel(stored, 110, "artifact"), 600);

{
  const estimates = WeaponDps.buildEstimates(200, stored);
  assert.strictEqual(estimates.developedWeapon, 600);
  assert.strictEqual(estimates.withGear, 1200);
  assert.strictEqual(estimates.withSkills, 1800);
  assert.strictEqual(estimates.completeBuild, 2160);
  assert.strictEqual(estimates.optimizedBuild, 4000);
  assert.strictEqual(estimates.normalTotalMultiplier, 10.8);
  assert.strictEqual(estimates.optimizedTotalMultiplier, 20);
}

{
  const result = WeaponDps.calculate({
    fire: { mode: "automatic", rate: 4 },
    shot: { projectiles: 1 },
    damage: 1
  }, 4);
  assert.strictEqual(result.totalDps, 4);
  assert.strictEqual(result.differencePercent, 0);
  assert.strictEqual(result.suggestedDamage, 1);
}

console.log("Weapon balance checks passed: 4→200 raw curve, future-level extrapolation, rarity suggestions, and build layers.");
