"use strict";

const assert = require("assert");
const WeaponDps = require("./weapon-dps");

const settings = WeaponDps.defaultSettings();

assert.deepStrictEqual(WeaponDps.validateSettings(settings), []);
assert.strictEqual(WeaponDps.targetAtLevel(settings, 1), 4);
assert.strictEqual(WeaponDps.targetAtLevel(settings, 110), 200);
assert.ok(Math.abs(WeaponDps.targetAtLevel(settings, 122) - 307.6611) < 0.0001);
assert.strictEqual(WeaponDps.rarityTargetAtLevel(settings, 110, "common"), 200);
assert.strictEqual(WeaponDps.rarityTargetAtLevel(settings, 110, "epic"), 332);
assert.strictEqual(WeaponDps.rarityTargetAtLevel(settings, 110, "artifact"), 600);

{
  const estimates = WeaponDps.buildEstimates(200, settings);
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

{
  const temporary = WeaponDps.defaultSettings();
  temporary.rawWeaponCurve.startDps = 8;
  assert.strictEqual(WeaponDps.targetAtLevel(temporary, 1), 8);
  assert.strictEqual(WeaponDps.targetAtLevel(WeaponDps.defaultSettings(), 1), 4);
}

console.log("Weapon balance checks passed: browser-only defaults, 4→200 raw curve, future-level extrapolation, rarity suggestions, and build layers.");
