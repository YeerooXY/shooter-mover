"use strict";

const assert = require("assert");
const {
  validateEnemy,
  validateShot,
  validateLeveling,
  strengthAt,
  levelColor,
  resolvedStats,
  projectilesPerSequence
} = require("./enemy-schema.js");

const leveling = {
  minLevel: 1,
  maxLevel: 100,
  strengthAtMax: 50,
  damagePower: 0.35,
  colors: [
    { level: 1, color: "#55D66B" },
    { level: 50, color: "#F0B040" },
    { level: 100, color: "#A653DF" }
  ]
};

const shot = {
  schema: 1,
  id: "small-bullet",
  delivery: { kind: "projectile", speed: 32, radius: 0.06, range: 18 },
  impact: { pierce: 1, ricochet: 0, knockback: 0 },
  art: { delivery: "enemy-shot.small-bullet" }
};

const gunner = {
  schema: 1,
  id: "gunner-droid",
  name: "Gunner Droid",
  tags: ["droid", "ground", "mobile", "ranged"],
  hp: 16,
  healthPower: 0.7,
  movement: { kind: "strafe", speed: 3.5 },
  detectionRange: 16,
  mounts: [
    { id: "left-gun", position: { x: -0.25, y: 0.2 }, rotation: 0 },
    { id: "right-gun", position: { x: 0.25, y: 0.2 }, rotation: 0 }
  ],
  attacks: [
    {
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
    }
  ],
  drops: "normal",
  art: "gunner-droid",
  body: { shape: "circle", radius: 0.45, offset: { x: 0, y: 0 } }
};

assert.deepStrictEqual(validateLeveling(leveling), []);
assert.deepStrictEqual(validateShot(shot), []);
assert.deepStrictEqual(validateEnemy(gunner), []);
assert.strictEqual(strengthAt(1, leveling), 1);
assert.strictEqual(strengthAt(100, leveling), 50);
assert.strictEqual(levelColor(1, leveling), "#55D66B");
assert.strictEqual(levelColor(100, leveling), "#A653DF");
assert.strictEqual(projectilesPerSequence(gunner.attacks[0]), 8);

const level100 = resolvedStats(gunner, 100, leveling);
assert(level100.hp > gunner.hp);
assert(level100.damageMultiplier > 1);
assert(level100.hp / gunner.hp < level100.strength);

const alternating = structuredClone(gunner.attacks[0]);
alternating.firePattern = "alternate";
assert.strictEqual(projectilesPerSequence(alternating), 4);

const ambiguousSingle = structuredClone(gunner);
ambiguousSingle.attacks[0].firePattern = "single";
assert(validateEnemy(ambiguousSingle).some(error => error.includes("exactly one emitter")));

const badEmitter = structuredClone(gunner);
badEmitter.attacks[0].emitters = ["missing-gun"];
assert(validateEnemy(badEmitter).some(error => error.includes("unknown mount")));

const duplicateMount = structuredClone(gunner);
duplicateMount.mounts[1].id = "left-gun";
assert(validateEnemy(duplicateMount).some(error => error.includes("Duplicate mount ID")));

const ambiguousVolley = structuredClone(gunner);
delete ambiguousVolley.attacks[0].volley.shotsPerTrigger;
assert(validateEnemy(ambiguousVolley).some(error => error.includes("shotsPerTrigger")));

const legacy = { ...gunner, type: "shooter", gun: "rattler.mk1", scale: 0.7 };
assert(validateEnemy(legacy).some(error => error.includes("type is not supported")));
assert(validateEnemy(legacy).some(error => error.includes("gun is not supported")));
assert(validateEnemy(legacy).some(error => error.includes("scale is not supported")));

const ellipse = { ...gunner, body: { shape: "ellipse", size: { x: 1, y: 2 }, offset: { x: 0, y: 0 } } };
assert(validateEnemy(ellipse).some(error => error.includes("reserved but not supported")));

const wrongFileId = validateEnemy(gunner, "other-enemy");
assert(wrongFileId.some(error => error.includes("must match the file name")));

console.log("Enemy Maker schema tests passed.");
