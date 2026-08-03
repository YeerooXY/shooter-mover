"use strict";

const http = require("http");
const fs = require("fs");
const path = require("path");
const crypto = require("crypto");
const { URL } = require("url");
const { validateEnemy, validateLeveling } = require("./enemy-schema.js");

const root = path.resolve(process.argv.includes("--repo") ? process.argv[process.argv.indexOf("--repo") + 1] : path.join(__dirname, "..", ".."));
const port = Number(process.argv.includes("--port") ? process.argv[process.argv.indexOf("--port") + 1] : 4174);
const content = path.resolve(root, "Content/Enemies");
const token = crypto.randomBytes(24).toString("hex");
const staticTypes = { ".html": "text/html", ".js": "text/javascript", ".css": "text/css", ".json": "application/json", ".svg": "image/svg+xml" };

function sendJson(res, status, value) {
  const body = JSON.stringify(value);
  res.writeHead(status, { "Content-Type": "application/json; charset=utf-8", "Content-Length": Buffer.byteLength(body), "Cache-Control": "no-store" });
  res.end(body);
}

function sendFile(res, file) {
  const body = fs.readFileSync(file);
  res.writeHead(200, { "Content-Type": `${staticTypes[path.extname(file)] || "application/octet-stream"}; charset=utf-8`, "Content-Length": body.length, "Cache-Control": "no-store" });
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
      try { resolve(JSON.parse(data || "{}")); } catch (error) { reject(error); }
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

async function api(req, res, url) {
  if (req.method === "GET" && url.pathname === "/api/status") return sendJson(res, 200, { token, content: path.relative(root, content).replace(/\\/g, "/") });
  if (req.method === "GET" && url.pathname === "/api/enemies") return sendJson(res, 200, { enemies: listEnemies() });
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
  if (req.method !== "GET" && req.headers["x-enemy-maker-token"] !== token) throw new Error("Mutation token is missing or invalid.");
  if (req.method === "PUT" && url.pathname === "/api/enemy") {
    const body = await readBody(req);
    const enemy = body.enemy;
    const originalId = body.originalId === null || body.originalId === undefined ? null : String(body.originalId);
    const errors = validateEnemy(enemy, originalId || enemy?.id);
    if (originalId && enemy?.id !== originalId) errors.push("Loaded enemy IDs cannot be renamed in place. Create a new enemy instead.");
    if (errors.length) return sendJson(res, 400, { errors });
    atomicWrite(safeEnemyFile(enemy.id), enemy);
    return sendJson(res, 200, { saved: path.relative(root, safeEnemyFile(enemy.id)).replace(/\\/g, "/") });
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
    const url = new URL(req.url, `http://127.0.0.1:${port}`);
    if (url.pathname.startsWith("/api/")) return await api(req, res, url);
    const requested = url.pathname === "/" ? "index.html" : url.pathname.slice(1);
    const file = path.resolve(__dirname, requested);
    if (!file.startsWith(path.resolve(__dirname) + path.sep) || !fs.existsSync(file) || !fs.statSync(file).isFile()) return sendJson(res, 404, { error: "Not found." });
    return sendFile(res, file);
  } catch (error) {
    return sendJson(res, 500, { error: error.message || String(error) });
  }
});

server.listen(port, "127.0.0.1", () => {
  console.log(`Enemy Maker: http://127.0.0.1:${port}/`);
  console.log(`Enemy definitions: ${content}`);
});
