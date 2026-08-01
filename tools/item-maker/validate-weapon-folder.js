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
const markOwnedBlocks = ["fire", "homing", "dot", "explosion"];

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

function validateFire(fire, label) {
  requireText(fire, "mode", label);
  if (!["semi-automatic", "automatic", "burst"].includes(fire.mode)) fail(`${label}.mode: unsupported fire mode`);
  if (!positive(fire.rate)) fail(`${label}.rate: positive number is required`);
}

function validateShared(weapon) {
  rejectUnknown(weapon, sharedFields, "weapon.json");
  ["name", "category", "rarity", "projectileType", "damageType"].forEach(key => requireText(weapon, key, "weapon.json"));
  if (!["bullet", "orb", "rocket", "beam"].includes(weapon.projectileType)) fail("weapon.json.projectileType: unsupported type");

  const shot = requireObject(weapon, "shot", "weapon.json");
  if (!Number.isInteger(shot.projectiles) || shot.projectiles < 1) fail("weapon.json.shot.projectiles: positive integer is required");
  if (!nonNegative(shot.spread)) fail("weapon.json.shot.spread: non-negative number is required");

  const impact = requireObject(weapon, "impact", "weapon.json");
  if (!has(impact, "ricochet")) fail("weapon.json.impact.ricochet: explicit value is required");
  if (!Number.isInteger(impact.pierce) || impact.pierce < 0) fail("weapon.json.impact.pierce: non-negative integer is required");
  if (!nonNegative(impact.ricochet)) fail("weapon.json.impact.ricochet: non-negative number is required");
  if (!nonNegative(impact.knockback)) fail("weapon.json.impact.knockback: non-negative number is required");

  if (weapon.projectileType === "beam") {
    if (has(weapon, "projectile")) fail("weapon.json: beam cannot contain projectile data");
    const beam = requireObject(weapon, "beam", "weapon.json");
    if (!positive(beam.range) || !positive(beam.width)) fail("weapon.json.beam: positive range and width are required");
  } else {
    if (has(weapon, "beam")) fail("weapon.json: non-beam weapon cannot contain beam data");
    const projectile = requireObject(weapon, "projectile", "weapon.json");
    if (!positive(projectile.speed) || !positive(projectile.radius) || !positive(projectile.range)) fail("weapon.json.projectile: positive speed, radius, and range are required");
  }

  const art = requireObject(weapon, "art", "weapon.json");
  ["mounted", "delivery", "trail", "impact"].forEach(key => requireText(art, key, "weapon.json.art"));
  if (has(weapon, "fire")) validateFire(requireObject(weapon, "fire", "weapon.json"), "weapon.json.fire");
}

function validateMarks(weapon, marks) {
  marks.forEach((mark, index) => {
    const label = `mk${index + 1}.json`;
    rejectUnknown(mark, markFields, label);
    if (!positive(mark.peakLevel)) fail(`${label}.peakLevel: positive number is required`);
    if (!positive(mark.damage)) fail(`${label}.damage: positive number is required`);
    const art = requireObject(mark, "art", label);
    requireText(art, "side", `${label}.art`);
    if (has(mark, "fire")) validateFire(requireObject(mark, "fire", label), `${label}.fire`);
  });

  markOwnedBlocks.forEach(block => {
    const shared = has(weapon, block);
    const count = marks.filter(mark => has(mark, block)).length;
    if (shared && count) fail(`${block}: cannot be owned by both weapon.json and a Mark file`);
    if (!shared && count !== 0 && count !== 3) fail(`${block}: when Mark-owned, all three Mark files must provide it`);
  });
  if (!has(weapon, "fire") && !marks.every(mark => has(mark, "fire"))) fail("fire: define it once in weapon.json or completely in all three Mark files");
}

function main() {
  if (!process.argv[2]) fail("Usage: node validate-weapon-folder.js <weapon-folder>");
  const folder = path.resolve(process.argv[2]);
  const slug = path.basename(folder);
  if (!/^[a-z0-9]+(?:_[a-z0-9]+)*$/.test(slug)) fail("Weapon folder name must use lowercase letters, digits, and underscores only");

  const expected = ["weapon.json", "mk1.json", "mk2.json", "mk3.json"];
  const actual = fs.readdirSync(folder).filter(name => name.endsWith(".json")).sort();
  const missing = expected.filter(name => !actual.includes(name));
  const extra = actual.filter(name => !expected.includes(name));
  if (missing.length || extra.length) fail(`Expected exactly weapon.json and mk1.json–mk3.json; missing: ${missing.join(", ") || "none"}; extra: ${extra.join(", ") || "none"}`);

  const weapon = readObject(path.join(folder, "weapon.json"));
  const marks = [1, 2, 3].map(mark => readObject(path.join(folder, `mk${mark}.json`)));
  validateShared(weapon);
  validateMarks(weapon, marks);

  const ids = marks.map((_, index) => `gun_${slug}_mk${index + 1}_01`);
  console.log(`Validated ${weapon.name}: ${ids.join(", ")}`);
}

try { main(); }
catch (error) { console.error(error.message); process.exitCode = 1; }
