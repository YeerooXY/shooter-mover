"use strict";

const fs = require("fs");
const path = require("path");
const {
  validateEnemy,
  validateShot,
  validateLeveling
} = require("./enemy-schema.js");

const PRESENTATION_ID = "presentation.enemy-compact";

function readJson(file) {
  return JSON.parse(fs.readFileSync(file, "utf8"));
}

function validDocuments(folder, validator, excluded = new Set()) {
  if (!fs.existsSync(folder)) return [];
  return fs.readdirSync(folder)
    .filter(name => name.endsWith(".json") && !excluded.has(name))
    .sort()
    .flatMap(name => {
      const id = name.slice(0, -5);
      const file = path.join(folder, name);
      try {
        const value = readJson(file);
        return validator(value, id).length ? [] : [value];
      } catch (_) {
        return [];
      }
    });
}

function writeChanged(file, content) {
  if (fs.existsSync(file) && fs.readFileSync(file, "utf8") === content) return false;
  fs.mkdirSync(path.dirname(file), { recursive: true });
  fs.writeFileSync(file, content, "utf8");
  return true;
}

function roomRegistry(enemies) {
  const rows = enemies.map(enemy => `            Definition("${enemy.id}")`);
  const body = rows.length ? `${rows.join(",\n")}\n` : "";
  return `using ShooterMover.Application.Missions.Rooms.Content;
using ShooterMover.Domain.Common;

namespace ShooterMover.Content.Definitions.Missions.Rooms
{
    // Generated from validated Content/Enemies files by tools/enemy-maker/runtime-export.js.
    public static class CompactEnemyRoomObjectRegistry
    {
        public const string PresentationStableId = "${PRESENTATION_ID}";

        public static RoomContentObjectDefinition[] Create()
        {
            return new[]
            {
${body}            };
        }

        private static RoomContentObjectDefinition Definition(string enemyId)
        {
            return new RoomContentObjectDefinition(
                StableId.Parse("enemy." + enemyId),
                RoomContentObjectKind.Enemy,
                StableId.Parse("enemy." + enemyId),
                StableId.Parse(PresentationStableId));
        }
    }
}
`;
}

function exportRuntimeCatalog(root) {
  const enemiesFolder = path.join(root, "Content", "Enemies");
  const shotsFolder = path.join(root, "Content", "EnemyShots");
  const levelingFile = path.join(enemiesFolder, "leveling.json");
  const leveling = readJson(levelingFile);
  const levelingErrors = validateLeveling(leveling);
  if (levelingErrors.length) {
    throw new Error(`Cannot export enemy runtime catalog:\n${levelingErrors.join("\n")}`);
  }

  const enemies = validDocuments(
    enemiesFolder,
    validateEnemy,
    new Set(["leveling.json"])
  );
  const shots = validDocuments(shotsFolder, validateShot);
  const catalog = {
    schema: 1,
    leveling,
    shots,
    enemies
  };

  const resourceFile = path.join(
    root,
    "Assets",
    "ShooterMover",
    "Resources",
    "Enemies",
    "CompactEnemyCatalog.json"
  );
  const registryFile = path.join(
    root,
    "Assets",
    "ShooterMover",
    "Content",
    "Definitions",
    "Missions",
    "Rooms",
    "CompactEnemyRoomObjectRegistry.cs"
  );

  return {
    enemyCount: enemies.length,
    shotCount: shots.length,
    resourceFile,
    registryFile,
    resourceChanged: writeChanged(
      resourceFile,
      `${JSON.stringify(catalog, null, 2)}\n`
    ),
    registryChanged: writeChanged(registryFile, roomRegistry(enemies))
  };
}

module.exports = { PRESENTATION_ID, exportRuntimeCatalog };
