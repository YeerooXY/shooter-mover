"use strict";

const assert = require("assert");
const { validateEnemy, validateLeveling, strengthAt, levelColor, resolvedStats } = require("./enemy-schema.js");

const leveling = {
  minLevel: 1,
  maxLevel: 100,
  strengthAtMax: 50,
  damagePower: 0.35,
  colors: [
    { level: 1, color: "#55D66B" },
    { level: 50, color: "#F2A23D" },
    { level: 100, color: "#A653DF" }
  ]
};

const popcorn = {
  id: "popcorn",
  name: "Popcorn",
  type: "popcorn",
  hp: 4,
  speed: 8,
  damage: 15,
  scale: 0.45,
  drops: "cash-or-scrap",
  art: "popcorn",
  body: { shape: "circle", radius: 0.35, offset: { x: 0, y: 0 } }
};

assert.deepStrictEqual(validateLeveling(leveling), []);
assert.deepStrictEqual(validateEnemy(popcorn, "popcorn"), []);
assert.strictEqual(strengthAt(1, leveling), 1);
assert(Math.abs(strengthAt(100, leveling) - 50) < 1e-9);
assert.strictEqual(levelColor(1, leveling), "#55D66B");
assert.strictEqual(levelColor(100, leveling), "#A653DF");
const level100 = resolvedStats(popcorn, 100, leveling);
assert(level100.hp > popcorn.hp);
assert(level100.damage > popcorn.damage);
assert(level100.hp / popcorn.hp < level100.damage / popcorn.damage * 3, "Popcorn health should not absorb the full strength curve.");

const future = structuredClone(popcorn);
future.body = { shape: "polygon", points: [{ x: 0, y: 1 }, { x: -1, y: -1 }, { x: 1, y: -1 }], offset: { x: 0, y: 0 } };
assert(validateEnemy(future).some(error => error.includes("reserved but not supported")));

const copiedGunDamage = { ...popcorn, type: "shooter", gun: "rattler.mk1", damage: 3 };
assert(validateEnemy(copiedGunDamage).some(error => error.includes("canonical gun")));

console.log("Enemy Maker checks passed.");
