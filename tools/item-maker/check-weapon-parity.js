"use strict";

const fs = require("fs");
const path = require("path");
const { compileWeaponFolder } = require("./compile-weapon-folder");

function fail(message) { throw new Error(message); }
function isObject(value) { return value && typeof value === "object" && !Array.isArray(value); }

function readJson(file) {
  try { return JSON.parse(fs.readFileSync(file, "utf8")); }
  catch (error) { fail(`${path.basename(file)}: ${error.message}`); }
}

function projectDefinition(definition) {
  return {
    definitionId: definition.definitionId,
    familyId: definition.familyId,
    mark: definition.mark,
    variant: definition.variant,
    name: definition.name,
    category: definition.category,
    rarity: definition.rarity,
    projectileType: definition.projectileType,
    damageType: definition.damageType,
    peakLevel: definition.peakLevel,
    damage: definition.damage,
    fire: definition.fire,
    shot: definition.shot,
    projectile: definition.projectile || null,
    impact: definition.impact,
    homing: definition.homing || null,
    dot: definition.dot || null,
    explosion: definition.explosion || null,
    art: {
      side: definition.art && definition.art.side || null,
      mounted: definition.art && definition.art.mounted || null,
      delivery: definition.art && definition.art.delivery || null,
      trail: definition.art && definition.art.trail || null,
      impact: definition.art && definition.art.impact || null
    }
  };
}

function collectDifferences(expected, actual, location, differences) {
  if (Array.isArray(expected) || Array.isArray(actual)) {
    if (!Array.isArray(expected) || !Array.isArray(actual)) {
      differences.push(`${location}: expected ${describe(expected)}, got ${describe(actual)}`);
      return;
    }
    if (expected.length !== actual.length) {
      differences.push(`${location}.length: expected ${expected.length}, got ${actual.length}`);
    }
    const count = Math.max(expected.length, actual.length);
    for (let index = 0; index < count; index++) {
      if (index >= expected.length) differences.push(`${location}[${index}]: unexpected ${describe(actual[index])}`);
      else if (index >= actual.length) differences.push(`${location}[${index}]: missing ${describe(expected[index])}`);
      else collectDifferences(expected[index], actual[index], `${location}[${index}]`, differences);
    }
    return;
  }

  if (isObject(expected) || isObject(actual)) {
    if (!isObject(expected) || !isObject(actual)) {
      differences.push(`${location}: expected ${describe(expected)}, got ${describe(actual)}`);
      return;
    }
    const keys = Array.from(new Set([...Object.keys(expected), ...Object.keys(actual)])).sort();
    keys.forEach(key => {
      const next = location ? `${location}.${key}` : key;
      if (!Object.prototype.hasOwnProperty.call(expected, key)) differences.push(`${next}: unexpected ${describe(actual[key])}`);
      else if (!Object.prototype.hasOwnProperty.call(actual, key)) differences.push(`${next}: missing ${describe(expected[key])}`);
      else collectDifferences(expected[key], actual[key], next, differences);
    });
    return;
  }

  if (!Object.is(expected, actual)) {
    differences.push(`${location}: expected ${describe(expected)}, got ${describe(actual)}`);
  }
}

function describe(value) {
  if (value === undefined) return "undefined";
  return JSON.stringify(value);
}

function main() {
  if (!process.argv[2] || !process.argv[3]) {
    fail("Usage: node check-weapon-parity.js <weapon-folder> <expected-json>");
  }

  const folder = path.resolve(process.argv[2]);
  const expectedFile = path.resolve(process.argv[3]);
  const compiled = compileWeaponFolder(folder);
  const actual = {
    familyId: compiled.familyId,
    definitions: compiled.definitions.map(projectDefinition)
  };
  const expected = readJson(expectedFile);
  const differences = [];
  collectDifferences(expected, actual, "", differences);

  if (differences.length) {
    const shown = differences.slice(0, 50);
    const remainder = differences.length - shown.length;
    fail([
      `Weapon parity failed with ${differences.length} difference(s):`,
      ...shown.map(difference => `- ${difference}`),
      ...(remainder ? [`- ...and ${remainder} more`] : [])
    ].join("\n"));
  }

  console.log(`${compiled.familyId} parity passed: ${compiled.definitions.length} definitions, 0 differences.`);
}

try { main(); }
catch (error) { console.error(error.message); process.exitCode = 1; }
