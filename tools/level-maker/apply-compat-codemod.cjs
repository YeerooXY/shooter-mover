"use strict";

const fs = require("fs");
const path = require("path");

const root = __dirname;
const replacements = [
  [/state\.activeRoomId\b/g, "state.editor.activeRoomId"],
  [/state\.connections\b/g, "state.level.connections"],
  [/state\.catalog\b/g, "state.assets"],
  [/state\.rooms\b/g, "state.level.rooms"],
  [/state\.logic\b/g, "state.level.logic"],
];

for (const name of fs.readdirSync(root).sort()) {
  if (!/^app-.*\.js$/.test(name)) continue;
  const file = path.join(root, name);
  let text = fs.readFileSync(file, "utf8");
  for (const [pattern, replacement] of replacements) {
    text = text.replace(pattern, replacement);
  }
  if (name === "app-8.js" || name === "app-9.js") {
    text = text
      .replace(/project\.activeRoomId\b/g, "project.editor.activeRoomId")
      .replace(/project\.catalog\b/g, "project.assets")
      .replace(/project\?\.catalog\b/g, "project?.assets")
      .replace(/project\.rooms\b/g, "project.level.rooms")
      .replace(/project\?\.rooms\b/g, "project?.level?.rooms");
  }
  fs.writeFileSync(file, text);
}
