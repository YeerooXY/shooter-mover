"use strict";

const fs = require("fs");
const path = require("path");
const { execFileSync } = require("child_process");

const fileNames = ["weapon.json", "mk1.json", "mk2.json", "mk3.json"];

function fail(message) { throw new Error(message); }
function isObject(value) { return value && typeof value === "object" && !Array.isArray(value); }

function readObject(file) {
  const value = JSON.parse(fs.readFileSync(file, "utf8"));
  if (!isObject(value)) fail(`${path.basename(file)}: root must be an object`);
  return value;
}

function mergeObjects(shared, mark) {
  const result = {};
  for (const [key, value] of Object.entries(shared)) {
    result[key] = isObject(value) ? mergeObjects(value, {}) : value;
  }
  for (const [key, value] of Object.entries(mark)) {
    result[key] = isObject(value) && isObject(result[key])
      ? mergeObjects(result[key], value)
      : value;
  }
  return result;
}

function compileWeaponFolder(folder) {
  const resolved = path.resolve(folder);
  const slug = path.basename(resolved);
  const categoryFolder = path.basename(path.dirname(resolved));

  execFileSync(
    process.execPath,
    [path.join(__dirname, "validate-weapon-folder.js"), resolved],
    { encoding: "utf8", stdio: ["ignore", "pipe", "pipe"] });

  const shared = readObject(path.join(resolved, "weapon.json"));
  const definitions = [1, 2, 3].map(mark => {
    const markSource = readObject(path.join(resolved, `mk${mark}.json`));
    return {
      definitionId: `gun_${slug}_mk${mark}_01`,
      familyId: slug,
      mark,
      variant: 1,
      ...mergeObjects(shared, markSource)
    };
  });

  return {
    familyId: slug,
    categoryFolder,
    sourceFiles: fileNames,
    definitions
  };
}

function main() {
  if (!process.argv[2]) fail("Usage: node compile-weapon-folder.js <weapon-folder>");
  process.stdout.write(JSON.stringify(compileWeaponFolder(process.argv[2]), null, 2) + "\n");
}

if (require.main === module) {
  try { main(); }
  catch (error) {
    const detail = String(error.stderr || error.stdout || error.message).trim();
    console.error(detail);
    process.exitCode = 1;
  }
}

module.exports = { compileWeaponFolder };
