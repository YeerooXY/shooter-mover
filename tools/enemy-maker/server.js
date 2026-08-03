"use strict";

const http = require("http");
const fs = require("fs");
const path = require("path");
const crypto = require("crypto");
const { URL } = require("url");
const { validateEnemy, validateLeveling } = require("./enemy-schema.js");

function parseArgument(name, fallback) {
  const index = process.argv.indexOf(name);
  return index >= 0 ? process.argv[index + 1] : fallback;
}

function createEnemyMaker(options = {}) {
  const root = path.resolve(options.root || parseArgument("--repo", path.join(__dirname, "..", "..")));
  const requestedPort = Number(options.port ?? parseArgument("--port", 4174));
  const content = path.resolve(root, "Content/Enemies");
  const weapons = path.resolve(root, "Content/Weapons");
  const token = crypto.randomBytes(24).toString("hex");
  const staticTypes = {
    ".html": "text/html",
    ".js": "text/javascript",
    ".css": "text/css",
    ".json": "application/json",
    ".svg": "image/svg+xml"
  };

  function sendJson(res, status, value) {
    const body = JSON.stringify(value);
    res.writeHead(status, {
      "Content-Type": "application/json; charset=utf-8",
      "Content-Length": Buffer.byteLength(body),
      "Cache-Control": "no-store"
    });
    res.end(body);
  }

  function sendFile(res, file) {
    const body = fs.readFileSync(file);
    res.writeHead(200, {
      "Content-Type": `${staticTypes[path.extname(file)] || "application/octet-stream"}; charset=utf-8`,
      "Content-Length": body.length,
      "Cache-Control": "no-store"
    });
    res.end(body);
  }

  function readBody(req) {
    return new Promise((resolve, reject) => {
      let data = "";
      req.on("data", chunk => {
        data += chunk;
        if (data.length > 1_000_000) reject(new Error("Request too large."));
      });
      req.on("end", () => {
        try { resolve(JSON.parse(data || "{}")); }
        catch (error) { reject(error); }
      });
      req.on("error", reject);
    });
  }

  function safeEnemyFile(id) {
    if (!/^[a-z0-9]+(?:-[a-z0-9]+)*$/.test(id || "")) throw new Error("Invalid enemy ID.");
    const file = path.resolve(content, `${id}.json`);
    if (!file.startsWith(content + path.sep)) throw new Error("Enemy path escaped its content folder.");
    return file;
  }

  function atomicWrite(file, value) {
    fs.mkdirSync(path.dirname(file), { recursive: true });
    const temporary = `${file}.${process.pid}.${Date.now()}.tmp`;
    fs.writeFileSync(temporary, `${JSON.stringify(value, null, 2)}\n`);
    fs.renameSync(temporary, file);
  }

  function listEnemies() {
    if (!fs.existsSync(content)) return [];
    return fs.readdirSync(content)
      .filter(name => name.endsWith(".json") && name !== "leveling.json")
      .sort()
      .map(name => {
        const id = name.slice(0, -5);
        try {
          const enemy = JSON.parse(fs.readFileSync(path.join(content, name), "utf8"));
          return { id, name: enemy.name || id, type: enemy.type || "unknown" };
        } catch (_) {
          return { id, name: id, type: "invalid" };
        }
      });
  }

  function listGuns() {
    if (!fs.existsSync(weapons)) return [];
    const result = [];
    const categories = fs.readdirSync(weapons, { withFileTypes: true })
      .filter(entry => entry.isDirectory())
      .sort((a, b) => a.name.localeCompare(b.name));

    for (const category of categories) {
      const categoryPath = path.join(weapons, category.name);
      const families = fs.readdirSync(categoryPath, { withFileTypes: true })
        .filter(entry => entry.isDirectory())
        .sort((a, b) => a.name.localeCompare(b.name));

      for (const family of families) {
        const familyPath = path.join(categoryPath, family.name);
        const sharedFile = path.join(familyPath, "weapon.json");
        if (!fs.existsSync(sharedFile)) continue;
        let shared;
        try { shared = JSON.parse(fs.readFileSync(sharedFile, "utf8")); }
        catch (_) { continue; }

        for (let mark = 1; mark <= 3; mark += 1) {
          const markFile = path.join(familyPath, `mk${mark}.json`);
          if (!fs.existsSync(markFile)) continue;
          let markValue;
          try { markValue = JSON.parse(fs.readFileSync(markFile, "utf8")); }
          catch (_) { continue; }
          if (markValue.available === false) continue;
          result.push({
            id: `${family.name}.mk${mark}`,
            name: `${shared.name || family.name} MK${mark}`,
            family: family.name,
            category: category.name,
            mark
          });
        }
      }
    }
    return result.sort((a, b) => a.name.localeCompare(b.name) || a.id.localeCompare(b.id));
  }

  function validateGunReference(enemy) {
    if (enemy.type !== "shooter") return [];
    const known = new Set(listGuns().map(gun => gun.id));
    return known.has(enemy.gun) ? [] : [`gun '${enemy.gun}' is not a canonical definition under Content/Weapons.`];
  }

  async function api(req, res, url) {
    if (req.method === "GET" && url.pathname === "/api/status") {
      return sendJson(res, 200, {
        token,
        content: path.relative(root, content).replace(/\\/g, "/"),
        weapons: path.relative(root, weapons).replace(/\\/g, "/")
      });
    }
    if (req.method === "GET" && url.pathname === "/api/enemies") return sendJson(res, 200, { enemies: listEnemies() });
    if (req.method === "GET" && url.pathname === "/api/guns") return sendJson(res, 200, { guns: listGuns() });
    if (req.method === "GET" && url.pathname === "/api/enemy") {
      const id = url.searchParams.get("id");
      const file = safeEnemyFile(id);
      if (!fs.existsSync(file)) return sendJson(res, 404, { error: "Enemy not found." });
      return sendJson(res, 200, { enemy: JSON.parse(fs.readFileSync(file, "utf8")) });
    }
    if (req.method === "GET" && url.pathname === "/api/leveling") {
      const file = path.join(content, "leveling.json");
      if (!fs.existsSync(file)) return sendJson(res, 404, { error: "leveling.json not found." });
      return sendJson(res, 200, { leveling: JSON.parse(fs.readFileSync(file, "utf8")) });
    }
    if (req.method !== "GET" && req.headers["x-enemy-maker-token"] !== token) {
      throw new Error("Mutation token is missing or invalid.");
    }
    if (req.method === "PUT" && url.pathname === "/api/enemy") {
      const body = await readBody(req);
      const enemy = body.enemy;
      const previousId = body.previousId || null;
      const errors = validateEnemy(enemy).concat(validateGunReference(enemy || {}));
      if (previousId && previousId !== enemy?.id) {
        errors.push("Changing the ID of an existing enemy is not supported yet. Create a new enemy instead.");
      }
      if (errors.length) return sendJson(res, 400, { errors });

      const target = safeEnemyFile(enemy.id);
      if (!previousId && fs.existsSync(target)) {
        return sendJson(res, 409, { errors: [`Enemy '${enemy.id}' already exists. Load it instead of overwriting it as new.`] });
      }
      if (previousId && !fs.existsSync(safeEnemyFile(previousId))) {
        return sendJson(res, 409, { errors: [`Enemy '${previousId}' no longer exists on disk.`] });
      }
      atomicWrite(target, enemy);
      return sendJson(res, 200, { saved: path.relative(root, target).replace(/\\/g, "/") });
    }
    if (req.method === "PUT" && url.pathname === "/api/leveling") {
      const body = await readBody(req);
      const errors = validateLeveling(body.leveling);
      if (errors.length) return sendJson(res, 400, { errors });
      const file = path.join(content, "leveling.json");
      atomicWrite(file, body.leveling);
      return sendJson(res, 200, { saved: path.relative(root, file).replace(/\\/g, "/") });
    }
    return sendJson(res, 404, { error: "Not found." });
  }

  const server = http.createServer(async (req, res) => {
    try {
      const address = server.address();
      const activePort = address && typeof address === "object" ? address.port : requestedPort;
      const url = new URL(req.url, `http://127.0.0.1:${activePort}`);
      if (url.pathname.startsWith("/api/")) return await api(req, res, url);
      const requested = url.pathname === "/" ? "index.html" : url.pathname.slice(1);
      const base = path.resolve(__dirname);
      const file = path.resolve(base, requested);
      if (!file.startsWith(base + path.sep) || !fs.existsSync(file) || !fs.statSync(file).isFile()) {
        return sendJson(res, 404, { error: "Not found." });
      }
      return sendFile(res, file);
    } catch (error) {
      return sendJson(res, 500, { error: error.message || String(error) });
    }
  });

  function start() {
    return new Promise((resolve, reject) => {
      server.once("error", reject);
      server.listen(requestedPort, "127.0.0.1", () => {
        server.removeListener("error", reject);
        resolve(server.address().port);
      });
    });
  }

  return { root, content, weapons, server, start, listEnemies, listGuns };
}

if (require.main === module) {
  const maker = createEnemyMaker();
  maker.start().then(port => {
    console.log(`Enemy Maker: http://127.0.0.1:${port}/`);
    console.log(`Enemy definitions: ${maker.content}`);
    console.log(`Canonical weapons: ${maker.weapons}`);
  }).catch(error => {
    console.error(error.stack || error.message || String(error));
    process.exitCode = 1;
  });
}

module.exports = { createEnemyMaker };
