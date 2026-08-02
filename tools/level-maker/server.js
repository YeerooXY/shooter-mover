"use strict";

const crypto = require("crypto");
const fs = require("fs");
const http = require("http");
const path = require("path");
const { execFileSync } = require("child_process");
const { URL } = require("url");
const { createAssetPreviewService } = require("./asset-preview-service");

const args = process.argv.slice(2);
const repo = path.resolve(argument("--repo") || path.join(__dirname, "..", ".."));
const port = Number(argument("--port") || 4174);
const token = crypto.randomBytes(24).toString("hex");
const projectsRoot = path.join(repo, "Content", "Levels");
const levelSourcesRoot = path.join(
  repo,
  "Assets",
  "ShooterMover",
  "Content",
  "Definitions",
  "Missions",
  "Rooms",
  "Levels"
);
const levelSourcePrefix =
  "Assets/ShooterMover/Content/Definitions/Missions/Rooms/Levels/";
const assetPreviews = createAssetPreviewService(repo);

function argument(name) {
  const index = args.indexOf(name);
  return index >= 0 ? args[index + 1] : "";
}

function git(gitArgs) {
  return execFileSync("git", gitArgs, {
    cwd: repo,
    encoding: "utf8",
    stdio: ["ignore", "pipe", "pipe"],
  }).trim();
}

function send(response, status, value) {
  const body = JSON.stringify(value);
  response.writeHead(status, {
    "Content-Type": "application/json; charset=utf-8",
    "Content-Length": Buffer.byteLength(body),
    "Cache-Control": "no-store",
  });
  response.end(body);
}

function readBody(request) {
  return new Promise((resolve, reject) => {
    let value = "";
    request.on("data", chunk => {
      value += chunk;
      if (value.length > 12_000_000) reject(new Error("Request too large."));
    });
    request.on("end", () => {
      try {
        resolve(JSON.parse(value || "{}"));
      } catch (error) {
        reject(error);
      }
    });
    request.on("error", reject);
  });
}

function slug(value) {
  const result = String(value || "").trim().toLowerCase();
  if (!/^[a-z0-9]+(?:-[a-z0-9]+)*$/.test(result)) {
    throw new Error("Level target must use lowercase words separated by hyphens.");
  }
  return result;
}

function projectFile(value) {
  const result = path.resolve(projectsRoot, `${slug(value)}.level.json`);
  assertInside(result, projectsRoot, "Level project path escaped Content/Levels.");
  return result;
}

function validateProject(project) {
  if (!project || project.format !== "shooter-mover-web-level-project") {
    throw new Error("Level project schema is invalid.");
  }
  if (!project.level
      || !/^level\.[a-z0-9]+(?:-[a-z0-9]+)*$/.test(project.level.id)) {
    throw new Error("A canonical level.* ID is required.");
  }
  if (!Array.isArray(project.rooms) || project.rooms.length === 0) {
    throw new Error("At least one room is required.");
  }
  slug(project.level.targetFolder);
}

function assertInside(file, root, message) {
  const relative = path.relative(path.resolve(root), path.resolve(file));
  if (relative.startsWith("..") || path.isAbsolute(relative)) throw new Error(message);
}

function atomicWrite(file, text) {
  fs.mkdirSync(path.dirname(file), { recursive: true });
  const temporary = `${file}.${process.pid}.tmp`;
  fs.writeFileSync(temporary, text, "utf8");
  fs.renameSync(temporary, file);
}

function publishSourceDocuments(files) {
  const entries = Object.entries(files || {});
  if (entries.length === 0) throw new Error("No Unity source documents were supplied.");

  for (const [relativePath, text] of entries) {
    const normalized = String(relativePath).replace(/\\/g, "/");
    if (!normalized.startsWith(levelSourcePrefix)
        || normalized.includes("..")
        || !normalized.endsWith(".json")) {
      throw new Error(`Rejected generated path: ${normalized}`);
    }
    const file = path.resolve(repo, normalized);
    assertInside(file, levelSourcesRoot, "Generated path escaped the level source root.");
    JSON.parse(text);
    atomicWrite(file, text);
  }
  return entries.length;
}

function walk(folder, visit) {
  if (!fs.existsSync(folder)) return;
  for (const entry of fs.readdirSync(folder, { withFileTypes: true })) {
    const fullPath = path.join(folder, entry.name);
    if (entry.isDirectory()) walk(fullPath, visit);
    else visit(fullPath);
  }
}

function addAsset(found, id, label, source) {
  if (typeof id !== "string") return;
  const type = id.startsWith("enemy.") ? "enemy"
    : id.startsWith("prop.") ? "prop"
      : id.startsWith("tile.") ? "floor"
        : id.startsWith("door.") ? "door"
          : id.startsWith("decor.") || id.startsWith("presentation.") ? "decor"
            : null;
  if (!type || found.has(id)) return;
  found.set(id, {
    id,
    label: label || id.split(".").pop().replace(/-/g, " "),
    type,
    source,
  });
}

function collectAssets(value, source, found) {
  if (Array.isArray(value)) {
    value.forEach(item => collectAssets(item, source, found));
    return;
  }
  if (!value || typeof value !== "object") return;
  const label = value.display_name || value.name || value.label;
  addAsset(found, value.id, label, source);
  addAsset(found, value.object, label, source);
  addAsset(found, value.definition_id, label, source);
  addAsset(found, value.runtime_object, label, source);
  Object.values(value).forEach(item => collectAssets(item, source, found));
}

function projectAssets() {
  const found = new Map();
  const roots = [
    "Assets/ShooterMover/Content/Definitions/Enemies",
    "Assets/ShooterMover/Content/Definitions/Missions/Rooms",
    "Assets/ShooterMover/ContentPackages",
  ];
  for (const relativeRoot of roots) {
    walk(path.join(repo, relativeRoot), file => {
      if (path.extname(file) !== ".json" || fs.statSync(file).size > 4_000_000) return;
      try {
        const relative = path.relative(repo, file).replace(/\\/g, "/");
        collectAssets(JSON.parse(fs.readFileSync(file, "utf8")), relative, found);
      } catch {
        // Malformed or unrelated JSON is not part of the authoring catalogue.
      }
    });
  }
  return [...found.values()].sort(
    (left, right) => left.type.localeCompare(right.type) || left.id.localeCompare(right.id)
  );
}

function shortHash(value) {
  return crypto.createHash("sha256").update(value, "utf8").digest("hex").slice(0, 8);
}

function assetStem(value) {
  return value
    .split(/[^a-zA-Z0-9]+/)
    .filter(Boolean)
    .map(segment => segment[0].toUpperCase() + segment.slice(1))
    .join("");
}

function resourcePath(levelId) {
  return levelId === "level.level-1"
    ? "Levels/Level1RoomContent"
    : `Levels/${assetStem(levelId)}_${shortHash(levelId)}RoomContent`;
}

function targetForLevel(levelId) {
  return levelId.replace(/^level\./, "");
}

function canonicalLevelFolders() {
  const values = [];
  walk(levelSourcesRoot, file => {
    if (path.basename(file) !== "level.json") return;
    try {
      const level = JSON.parse(fs.readFileSync(file, "utf8"));
      values.push({
        id: level.level_id,
        name: level.display_name || level.level_id,
        target: targetForLevel(level.level_id),
        folder: path.dirname(file),
      });
    } catch {
      // Unity's compiler will report malformed source packages.
    }
  });
  return values.sort((left, right) => left.id.localeCompare(right.id));
}

function readJson(folder, name) {
  return JSON.parse(fs.readFileSync(path.join(folder, name), "utf8"));
}

function readOptionalJson(folder, name, fallback) {
  const file = path.join(folder, name);
  return fs.existsSync(file) ? JSON.parse(fs.readFileSync(file, "utf8")) : fallback;
}

function canonicalProject(entry) {
  const level = readJson(entry.folder, "level.json");
  const map = readJson(entry.folder, "map.json");
  const rooms = level.rooms.map(reference => {
    const folder = path.join(entry.folder, "Rooms", reference.folder);
    const room = readJson(folder, "room.json");
    const doors = readJson(folder, "doors.json").doors;
    const enemies = readJson(folder, "enemies.json").enemies;
    const props = readJson(folder, "props.json").props;
    const floor = readJson(folder, "floor.json");
    const encounter = readOptionalJson(folder, "encounter.json", {
      completion: "all-enemies",
      optional_enemy_ids: [],
      door_rules: [],
    });
    return {
      id: room.room_id,
      displayName: room.display_name,
      grid: room.grid_position,
      slot: room.slot,
      bounds: {
        width: room.runtime_bounds.size[0],
        height: room.runtime_bounds.size[1],
      },
      playerStart: room.player_start
        ? {
            position: room.player_start.position,
            rotation: room.player_start.rotation,
          }
        : null,
      floorObject: floor.tiles[0]?.object || "tile.floor-industrial",
      tileGridEnabled: false,
      tiles: [],
      entities: [
        ...enemies.map(enemy => ({
          id: enemy.id,
          kind: "enemy",
          object: enemy.object,
          tier: Number(enemy.tier || enemy.level || 1),
          position: enemy.position,
          rotation: enemy.rotation,
          optional: (encounter.optional_enemy_ids || []).includes(enemy.id),
        })),
        ...props.map(prop => ({
          id: prop.id,
          kind: "prop",
          object: prop.object,
          position: prop.position,
          rotation: prop.rotation,
          blocksMovement: true,
          layer: "default",
        })),
      ],
      doors: doors.map(door => ({
        id: door.door_id,
        kind: "door",
        position: door.current_local_position,
        rotation: 0,
        side: door.side,
        placementMode: door.placement_mode,
        traversable: door.traversable,
        visibleOnMap: door.visible_on_map,
        runtimeObject: door.runtime_object,
        openWhen: (encounter.door_rules || []).some(
          rule => rule.match?.door_id === door.door_id
        )
          ? "room-complete"
          : "always",
      })),
      encounter: { completion: encounter.completion },
      visibleOnMap: true,
    };
  });

  return {
    format: "shooter-mover-web-level-project",
    editorVersion: 1,
    schemaVersion: 2,
    level: {
      id: level.level_id,
      name: level.display_name || level.level_id,
      targetFolder: entry.target,
      startRoomId: level.start_room_id,
      finalRoomId: level.final_exit.room_id,
      finalExitDoorId: level.final_exit.door_id,
    },
    rooms,
    connections: (map.connections || []).map(connection => ({
      id: connection.connection_id,
      fromDoorId: connection.from.door_id,
      toDoorId: connection.to.door_id,
      travelPolicy: connection.travel_policy,
    })),
    logic: [],
    catalog: [],
    activeRoomId: level.start_room_id,
    editor: {
      tool: "select",
      viewMode: "room",
      mapMode: "open",
      placementMode: "single",
      focusRoom: true,
      selectedId: null,
      selectedAssetId: "enemy.moving-droid",
      zoom: 32,
      pan: [0, 0],
      snap: true,
      snapSize: 1,
      roomView: { zoom: 32, pan: [0, 0] },
      mapView: { zoom: 22, pan: [0, 0] },
    },
  };
}

function savedProjects() {
  fs.mkdirSync(projectsRoot, { recursive: true });
  return fs.readdirSync(projectsRoot)
    .filter(name => name.endsWith(".level.json"))
    .sort()
    .map(name => JSON.parse(fs.readFileSync(path.join(projectsRoot, name), "utf8")));
}

function rebuildPlayableCatalog() {
  const projects = savedProjects();
  for (const entry of canonicalLevelFolders()) {
    if (!projects.some(project => project.level.id === entry.id)) {
      projects.push(canonicalProject(entry));
    }
  }
  projects.sort((left, right) => left.level.id.localeCompare(right.level.id));
  const levels = projects.map((project, index) => ({
    level_id: project.level.id,
    display_name: project.level.name,
    description: project.level.description || `Play ${project.level.name}.`,
    room_content_resource: resourcePath(project.level.id),
    enemy_catalog_resource: "Levels/Level1EnemyCatalog",
    player_presentation: "presentation.player-default",
    recommended_player_level: Number(project.level.recommendedPlayerLevel || 1),
    recommended_equipment_level: Number(project.level.recommendedEquipmentLevel || 1),
    recommended_party_size: Number(project.level.recommendedPartySize || 1),
    difficulty_label: project.level.difficultyLabel || "STANDARD",
    sort_order: Number(project.level.sortOrder || (index + 1) * 10),
  }));
  atomicWrite(
    path.join(repo, "Assets", "ShooterMover", "Resources", "Levels",
      "PlayableLevelCatalog.json"),
    `${JSON.stringify({ schema_version: 1, levels }, null, 2)}\n`
  );
}

async function api(request, response, url) {
  if (request.method === "GET" && url.pathname === "/api/asset-previews") {
    return send(response, 200, assetPreviews.previews(projectAssets()));
  }
  if (request.method === "GET" && url.pathname === "/api/asset-image") {
    assetPreviews.sendImage(response, url.searchParams.get("path") || "");
    return;
  }
  if (request.method === "GET" && url.pathname === "/api/status") {
    return send(response, 200, {
      branch: git(["branch", "--show-current"]),
      clean: git(["status", "--porcelain"]).length === 0,
      mutationToken: token,
    });
  }
  if (request.method === "GET" && url.pathname === "/api/level-assets") {
    return send(response, 200, { assets: projectAssets() });
  }
  if (request.method === "GET" && url.pathname === "/api/levels") {
    const byId = new Map(
      canonicalLevelFolders().map(entry => [
        entry.id,
        { id: entry.id, name: entry.name, target: entry.target },
      ])
    );
    for (const project of savedProjects()) {
      byId.set(project.level.id, {
        id: project.level.id,
        name: project.level.name,
        target: project.level.targetFolder,
      });
    }
    return send(response, 200, {
      levels: [...byId.values()].sort((left, right) => left.id.localeCompare(right.id)),
    });
  }
  if (request.method === "GET" && url.pathname === "/api/level") {
    const target = slug(url.searchParams.get("target"));
    const saved = projectFile(target);
    if (fs.existsSync(saved)) {
      return send(response, 200, {
        project: JSON.parse(fs.readFileSync(saved, "utf8")),
      });
    }
    const entry = canonicalLevelFolders().find(level => level.target === target);
    if (!entry) throw new Error("The requested level does not exist.");
    return send(response, 200, { project: canonicalProject(entry) });
  }

  if (request.method !== "GET"
      && request.headers["x-level-maker-token"] !== token) {
    throw new Error("Mutation token is missing or invalid.");
  }
  if (request.method === "PUT" && url.pathname === "/api/level") {
    const value = await readBody(request);
    validateProject(value.project);
    const fileCount = publishSourceDocuments(value.files);
    const destination = projectFile(value.project.level.targetFolder);
    atomicWrite(destination, `${JSON.stringify(value.project, null, 2)}\n`);
    rebuildPlayableCatalog();
    return send(response, 200, {
      projectPath: path.relative(repo, destination).replace(/\\/g, "/"),
      fileCount,
    });
  }
  return send(response, 404, { error: "Not found." });
}

const contentTypes = {
  ".html": "text/html; charset=utf-8",
  ".js": "text/javascript; charset=utf-8",
  ".css": "text/css; charset=utf-8",
};

http.createServer(async (request, response) => {
  try {
    const url = new URL(request.url, `http://${request.headers.host || "127.0.0.1"}`);
    if (url.pathname.startsWith("/api/")) return await api(request, response, url);
    const relative = url.pathname === "/" ? "index.html" : url.pathname.slice(1);
    const file = path.resolve(__dirname, relative);
    assertInside(file, __dirname, "Static path escaped the Level Maker.");
    if (!fs.existsSync(file) || fs.statSync(file).isDirectory()) {
      return send(response, 404, { error: "Not found." });
    }
    const value = fs.readFileSync(file);
    response.writeHead(200, {
      "Content-Type": contentTypes[path.extname(file)] || "application/octet-stream",
      "Content-Length": value.length,
    });
    response.end(value);
  } catch (error) {
    send(response, 400, { error: error.message });
  }
}).listen(port, "127.0.0.1", () => {
  console.log(`Level Maker: http://127.0.0.1:${port}`);
  console.log(`Repository: ${repo}`);
});
