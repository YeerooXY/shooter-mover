"use strict";

const fs = require("fs");
const path = require("path");
const { execFileSync, spawnSync } = require("child_process");

const CORE_PATH = path.join(__dirname, "import-pr288-weapons-core.js");
const GENERATED_PATH = "Assets/ShooterMover/Runtime/Application/Guns/Catalog/GunCatalogue.Pr288.Generated.cs";
const REPORT_PATH = "Documentation/Weapons/PR288_CONVERSION_REPORT.md";
const WEIGHT_NOTE = "- Historical PR #288 selection weights are migration evidence only. Generated JSON does not author a weight; current peak-level and rarity logic owns live distribution.";

function fail(message) {
  throw new Error(message);
}

function repoRoot() {
  return execFileSync("git", ["rev-parse", "--show-toplevel"], { encoding: "utf8" }).trim();
}

function normalizeGenerated(root) {
  const file = path.join(root, GENERATED_PATH);
  let text = fs.readFileSync(file, "utf8");
  const weightPattern = /(new Pr288MarkSource\(\r?\n\s+\d+, \d+,\r?\n)\s+[-+0-9.eEdD]+,\r?\n/g;
  let weightCount = 0;
  text = text.replace(weightPattern, (match, prefix) => {
    weightCount += 1;
    return `${prefix}                            1d,\n`;
  });
  if (weightCount !== 33) {
    fail(`Expected 33 generated Mark weights, normalized ${weightCount}.`);
  }

  const markPattern = /\s*new Pr288MarkSource\(\r?\n\s*(\d+), (\d+),\r?\n\s*1d,\r?\n\s*(FireSettings\.[^\n]+),\r?\n\s*(GunShotPattern\.[^\n]+),\r?\n\s*([^\n]+),\r?\n\s*(GunDamageCategory\.[^,]+),\r?\n\s*(\d+), ([^,]+),\r?\n\s*([^,]+), ([^,]+), ([^)]+)\)(,?)/g;
  let compactCount = 0;
  text = text.replace(markPattern, (match, mark, peak, fire, shot, damage, category, pierce, knockback, range, speed, radius, comma) => {
    compactCount += 1;
    return `\n                        new Pr288MarkSource(${mark}, ${peak}, 1d, ${fire}, ${shot}, ${damage}, ${category}, ${pierce}, ${knockback}, ${range}, ${speed}, ${radius})${comma}`;
  });
  if (compactCount !== 33) {
    fail(`Expected 33 generated Mark records, compacted ${compactCount}.`);
  }
  fs.writeFileSync(file, text, "utf8");
}

function normalizeReport(root) {
  const file = path.join(root, REPORT_PATH);
  let text = fs.readFileSync(file, "utf8");
  const anchor = "- Because current production rarity is family-owned, generated families use the highest normalized rarity among their Marks.\n";
  if (!text.includes(WEIGHT_NOTE)) {
    if (!text.includes(anchor)) {
      fail("Conversion report rarity mapping anchor is missing.");
    }
    text = text.replace(anchor, `${anchor}${WEIGHT_NOTE}\n`);
  }
  fs.writeFileSync(file, text, "utf8");
}

function main() {
  const root = repoRoot();
  const result = spawnSync(process.execPath, [CORE_PATH], {
    cwd: root,
    encoding: "utf8",
    maxBuffer: 32 * 1024 * 1024
  });
  if (result.status !== 0) {
    fail(["PR #288 core importer failed.", result.stdout, result.stderr].filter(Boolean).join("\n"));
  }
  normalizeGenerated(root);
  normalizeReport(root);
  process.stdout.write(result.stdout);
}

if (require.main === module) {
  try {
    main();
  } catch (error) {
    console.error(error.stack || error.message);
    process.exitCode = 1;
  }
}
