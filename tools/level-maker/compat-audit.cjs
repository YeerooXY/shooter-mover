"use strict";

const fs = require("fs");
const path = require("path");

const root = __dirname;
const patterns = [
  /state\.rooms\b/,
  /state\.connections\b/,
  /state\.logic\b/,
  /state\.catalog\b/,
  /state\.activeRoomId\b/,
  /\broom\.tiles\b/,
  /\broom\.floorObject\b/,
  /\broom\.tileGridEnabled\b/,
  /\br\.tiles\b/,
  /\br\.floorObject\b/,
  /\br\.tileGridEnabled\b/,
  /\bproject\.rooms\b/,
  /\bproject\.catalog\b/,
  /\bproject\.activeRoomId\b/,
];

for (const name of fs.readdirSync(root).sort()) {
  if (!/^app-.*\.js$/.test(name)) continue;
  const file = path.join(root, name);
  const lines = fs.readFileSync(file, "utf8").split(/\r?\n/);
  lines.forEach((line, index) => {
    if (patterns.some(pattern => pattern.test(line))) {
      console.log(`${name}:${index + 1}: ${line.trim()}`);
    }
  });
}
