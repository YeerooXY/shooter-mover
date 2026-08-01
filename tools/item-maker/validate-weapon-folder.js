"use strict";

const fs = require("fs");
const path = require("path");

const sharedFields = new Set([
  "name", "description", "category", "rarity", "projectileType",
  "damageType", "fire", "shot", "projectile", "beam", "impact",
  "homing", "dot", "art"
]);
const markFields = new Set([
  "peakLevel", "damage", "fire", "homing", "dot", "explosion", "art"
]);
const fireFields = new Set(["mode", "rate", "shotsPerBurst", "secondsBetweenShots"]);
const homingFields = new Set(["acquisitionRange", "turnRate", "activationDelay", "targetPolicy", "reacquire"]);
const dotFields = new Set(["damagePerSecond", "duration", "ticksPerSecond", "maxStacks", "refreshDuration"]);
const dotNumberFields = ["damagePerSecond", "duration", "ticksPerSecond", "maxStacks"];

function fail(message) { throw new Error(message); }
function has(value, key) { return Object.prototype.hasOwnProperty.call(value, key); }
function isObject(value) { return value && typeof value === "object" && !Array.isArray(value); }
function finiteNumber(value) { return typeof value === "number" && Number.isFinite(value); }
function positive(value) { return finiteNumber(value) && value > 0; }
function nonNegative(value) { return finiteNumber(value) && value >= 0; }

function readObject(file) {
  let value;
  try { value = JSON.parse(fs.readFileSync(file, "utf8")); }
  catch (error) { fail(`${path.basename(file)}: malformed JSON: ${error.message}`); }
  if (!isObject(value)) fail(`${path.basename(file)}: root must be an object`);
  rejectNonFinite(value, path.basename(file));
  return value;
}

function rejectNonFinite(value, label) {
  if (typeof value === "number" && !Number.isFinite(value)) fail(`${label}: non-finite number`);
  if (Array.isArray(value)) value.forEach(item => rejectNonFinite(item, label));
  else if (isObject(value)) Object.values(value).forEach(item => rejectNonFinite(item, label));
}

function rejectUnknown(value, allowed, label) {
  const unknown = Object.keys(value).filter(key => !allowed.has(key));
  if (unknown.length) fail(`${label}: unknown field(s): ${unknown.join(", ")}`);
}

function requireObject(value, key, label) {
  if (!isObject(value[key])) fail(`${label}.${key}: object is required`);
  return value[key];
}

function requireText(value, key, label) {
  if (typeof value[key] !== "string" || !value[key].trim()) fail(`${label}.${key}: text is required`);
}

function requireBoolean(value, key, label) {
  if (typeof value[key] !== "boolean") fail(`${label}.${key}: true or false is required`);
}

function validateFire(fire, label) {
  rejectUnknown(fire, fireFields, label);
  requireText(fire, "mode", label);
  if (!["semi-automatic", "automatic", "burst"].includes(fire.mode)) fail(`${label}.mode: unsupported fire mode`);
  if (!positive(fire.rate)) fail(`${label}.rate: positive cycles per second are required`);

  if (fire.mode === "burst") {
    if (!Number.isInteger(fire.shotsPerBurst) || fire.shotsPerBurst < 2) fail(`${label}.shotsPerBurst: burst requires at least two shots`);
    if (!positive(fire.secondsBetweenShots)) fail(`${label}.secondsBetweenShots: positive burst spacing is required`);
    const cycleSeconds = 1 / fire.rate;
    const emissionSeconds = (fire.shotsPerBurst - 1) * fire.secondsBetweenShots;
    if (cycleSeconds <= emissionSeconds) fail(`${label}: burst rate leaves no recovery time after the final burst shot`);
  } else if (has(fire, "shotsPerBurst") || has(fire, "secondsBetweenShots")) {
    fail(`${label}: burst shot count and spacing are only valid in burst mode`);
  }
}

function validateHoming(homing, label) {
  rejectUnknown(homing, homingFields, label);
  if (!positive(homing.acquisitionRange)) fail(`${label}.acquisitionRange: positive target search range is required`);
  if (!positive(homing.turnRate)) fail(`${label}.turnRate: positive turn speed is required`);
  if (!nonNegative(homing.activationDelay)) fail(`${label}.activationDelay: non-negative delay is required`);
  requireText(homing, "targetPolicy", label);
  if (homing.targetPolicy !== "closest-to-aim") fail(`${label}.targetPolicy: unsupported target choice`);
  requireBoolean(homing, "reacquire", label);
}

function validateDotNumbers(dot, label, requireRefresh) {
  rejectUnknown(dot, dotFields, label);
  dotNumberFields.forEach(field => {
    if (!positive(dot[field])) fail(`${label}.${field}: positive number is required`);
  });
  if (!Number.isInteger(dot.maxStacks) || dot.maxStacks < 1) fail(`${label}.maxStacks: positive whole number is required`);
  if (requireRefresh) requireBoolean(dot, "refreshDuration", label);
  else if (has(dot, "refreshDuration")) fail(`${label}.refreshDuration: define refresh behaviour once in the shared weapon stats`);
}

function validateExplosion(explosion, label) {
  rejectUnknown(explosion, new Set(["radius", "edgeDamageMultiplier"]), label);
  if (!positive(explosion.radius)) fail(`${label}.radius: positive explosion radius is required`);
  if (!finiteNumber(explosion.edgeDamageMultiplier)
      || explosion.edgeDamageMultiplier < 0
      || explosion.edgeDamageMultiplier > 1) {
    fail(`${label}.edgeDamageMultiplier: outer-edge damage must be between 0 and 1`);
  }
}

function validateShared(weapon) {
  rejectUnknown(weapon, sharedFields, "weapon.json");
  ["name", "category", "rarity", "projectileType", "damageType"].forEach(key => requireText(weapon, key, "weapon.json"));
  if (has(weapon, "description") && typeof weapon.description !== "string") fail("weapon.json.description: text is required");
  if (!["common", "rare", "epic", "legendary", "artifact"].includes(weapon.rarity)) fail("weapon.json.rarity: unsupported rarity");
  if (!["physical", "energy", "thermal", "chemical"].includes(weapon.damageType)) fail("weapon.json.damageType: unsupported damage type");
  if (!["bullet", "orb", "rocket", "beam"].includes(weapon.projectileType)) fail("weapon.json.projectileType: unsupported projectile type");

  const shot = requireObject(weapon, "shot", "weapon.json");
  rejectUnknown(shot, new Set(["projectiles", "spread"]), "weapon.json.shot");
  if (!Number.isInteger(shot.projectiles) || shot.projectiles < 1) fail("weapon.json.shot.projectiles: at least one projectile is required");
  if (!nonNegative(shot.spread)) fail("weapon.json.shot.spread: non-negative spread is required");

  const impact = requireObject(weapon, "impact", "weapon.json");
  rejectUnknown(impact, new Set(["pierce", "ricochet", "knockback"]), "weapon.json.impact");
  if (!has(impact, "ricochet")) fail("weapon.json.impact.ricochet: explicit value is required");
  if (!Number.isInteger(impact.pierce) || impact.pierce < 0) fail("weapon.json.impact.pierce: non-negative whole number is required");
  if (!nonNegative(impact.ricochet)) fail("weapon.json.impact.ricochet: non-negative value is required");
  if (!nonNegative(impact.knockback)) fail("weapon.json.impact.knockback: non-negative value is required");

  if (weapon.projectileType === "beam") {
    if (has(weapon, "projectile")) fail("weapon.json: beam cannot contain projectile speed or radius");
    const beam = requireObject(weapon, "beam", "weapon.json");
    rejectUnknown(beam, new Set(["range", "width"]), "weapon.json.beam");
    if (!positive(beam.range) || !positive(beam.width)) fail("weapon.json.beam: positive range and width are required");
  } else {
    if (has(weapon, "beam")) fail("weapon.json: non-beam weapon cannot contain beam data");
    const projectile = requireObject(weapon, "projectile", "weapon.json");
    rejectUnknown(projectile, new Set(["speed", "radius", "range"]), "weapon.json.projectile");
    if (!positive(projectile.speed) || !positive(projectile.radius) || !positive(projectile.range)) {
      fail("weapon.json.projectile: positive speed, radius, and range are required");
    }
  }

  const art = requireObject(weapon, "art", "weapon.json");
  rejectUnknown(art, new Set(["mounted", "delivery", "trail", "impact"]), "weapon.json.art");
  ["delivery", "trail", "impact"].forEach(key => requireText(art, key, "weapon.json.art"));
  if (has(art, "mounted")) requireText(art, "mounted", "weapon.json.art");
  if (has(weapon, "fire")) validateFire(requireObject(weapon, "fire", "weapon.json"), "weapon.json.fire");
  if (has(weapon, "homing")) validateHoming(requireObject(weapon, "homing", "weapon.json"), "weapon.json.homing");
}

function validateMarks(weapon, marks) {
  marks.forEach((mark, index) => {
    const label = `mk${index + 1}.json`;
    rejectUnknown(mark, markFields, label);
    if (!positive(mark.peakLevel) || !Number.isInteger(mark.peakLevel)) fail(`${label}.peakLevel: positive whole level is required`);
    if (!positive(mark.damage)) fail(`${label}.damage: positive damage is required`);
    const art = requireObject(mark, "art", label);
    rejectUnknown(art, new Set(["side", "mounted"]), `${label}.art`);
    requireText(art, "side", `${label}.art`);
    if (has(art, "mounted")) requireText(art, "mounted", `${label}.art`);
    if (has(mark, "fire")) validateFire(requireObject(mark, "fire", label), `${label}.fire`);
    if (has(mark, "homing")) validateHoming(requireObject(mark, "homing", label), `${label}.homing`);
    if (has(mark, "explosion")) validateExplosion(requireObject(mark, "explosion", label), `${label}.explosion`);
  });

  validateBlockOwnership(weapon, marks, "fire", true);
  validateBlockOwnership(weapon, marks, "homing", false);

  const explosions = marks.filter(mark => has(mark, "explosion")).length;
  if (explosions !== 0 && explosions !== 3) fail("explosion: all three Marks must define explosion values");
  if (explosions === 3 && weapon.projectileType !== "rocket") fail("explosion: only rocket weapons currently support authored explosions");

  const sharedArt = requireObject(weapon, "art", "weapon.json");
  const sharedMounted = has(sharedArt, "mounted");
  const mountedCount = marks.filter(mark => has(mark.art, "mounted")).length;
  if (sharedMounted && mountedCount) fail("art.mounted: choose shared mounted art or Mark-specific mounted art, not both");
  if (!sharedMounted && mountedCount !== 3) fail("art.mounted: all three Marks must provide mounted art when it is not shared");

  validateDotOwnership(weapon, marks);
}

function validateBlockOwnership(weapon, marks, block, required) {
  const shared = has(weapon, block);
  const count = marks.filter(mark => has(mark, block)).length;
  if (shared && count) fail(`${block}: choose shared gameplay values or Mark-specific values, not both`);
  if (!shared && count !== 0 && count !== 3) fail(`${block}: all three Marks must provide the complete gameplay values`);
  if (required && !shared && count !== 3) fail(`${block}: firing behaviour is required`);
}

function validateDotOwnership(weapon, marks) {
  const shared = has(weapon, "dot") ? requireObject(weapon, "dot", "weapon.json") : null;
  const markCount = marks.filter(mark => has(mark, "dot")).length;
  if (markCount !== 0 && markCount !== 3) fail("dot: all three Marks must provide damage-over-time values");

  if (!shared && markCount === 0) return;
  if (!shared) {
    marks.forEach((mark, index) => validateDotNumbers(requireObject(mark, "dot", `mk${index + 1}.json`), `mk${index + 1}.json.dot`, true));
    return;
  }

  rejectUnknown(shared, dotFields, "weapon.json.dot");
  const sharedHasNumbers = dotNumberFields.some(field => has(shared, field));
  if (sharedHasNumbers) {
    if (markCount) fail("dot: shared numerical values cannot also be defined by Marks");
    validateDotNumbers(shared, "weapon.json.dot", true);
    return;
  }

  requireBoolean(shared, "refreshDuration", "weapon.json.dot");
  if (markCount !== 3) fail("dot: shared refresh behaviour requires complete damage-over-time values in all three Marks");
  marks.forEach((mark, index) => validateDotNumbers(requireObject(mark, "dot", `mk${index + 1}.json`), `mk${index + 1}.json.dot`, false));
}

function main() {
  if (!process.argv[2]) fail("Usage: node validate-weapon-folder.js <weapon-folder>");
  const folder = path.resolve(process.argv[2]);
  const slug = path.basename(folder);
  if (!/^[a-z0-9]+(?:_[a-z0-9]+)*$/.test(slug)) fail("Weapon key must use lowercase letters, digits, and underscores only");

  const expected = ["weapon.json", "mk1.json", "mk2.json", "mk3.json"];
  const actual = fs.readdirSync(folder).filter(name => name.endsWith(".json")).sort();
  const missing = expected.filter(name => !actual.includes(name));
  const extra = actual.filter(name => !expected.includes(name));
  if (missing.length || extra.length) {
    fail(`Expected exactly weapon.json and mk1.json–mk3.json; missing: ${missing.join(", ") || "none"}; extra: ${extra.join(", ") || "none"}`);
  }

  const weapon = readObject(path.join(folder, "weapon.json"));
  const marks = [1, 2, 3].map(mark => readObject(path.join(folder, `mk${mark}.json`)));
  validateShared(weapon);
  validateMarks(weapon, marks);

  const ids = marks.map((_, index) => `gun_${slug}_mk${index + 1}_01`);
  console.log(`Validated ${weapon.name}: ${ids.join(", ")}`);
}

try { main(); }
catch (error) { console.error(error.message); process.exitCode = 1; }
