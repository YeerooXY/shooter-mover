"use strict";

const fs = require("fs");
const path = require("path");
const { validateEnemy } = require("../enemy-maker/enemy-schema.js");

function projectEnemyDefinitionAssets(repositoryRoot) {
  const root = path.resolve(repositoryRoot, "Content", "Enemies");
  if (!fs.existsSync(root)) return [];

  return fs.readdirSync(root, { withFileTypes: true })
    .filter(entry => entry.isFile() && entry.name.endsWith(".json") && entry.name !== "leveling.json")
    .sort((left, right) => left.name.localeCompare(right.name))
    .flatMap(entry => {
      const definitionId = entry.name.slice(0, -5);
      const file = path.join(root, entry.name);
      try {
        const definition = JSON.parse(fs.readFileSync(file, "utf8"));
        if (validateEnemy(definition, definitionId).length) return [];
        return [{
          id: `enemy.${definition.id}`,
          definitionId: definition.id,
          label: definition.name,
          type: "enemy",
          source: path.relative(repositoryRoot, file).replace(/\\/g, "/"),
          art: definition.art || null,
        }];
      } catch {
        return [];
      }
    });
}

module.exports = { projectEnemyDefinitionAssets };
