"use strict";

const http = require("http");
const fs = require("fs");
const path = require("path");
const { execFileSync } = require("child_process");
const crypto = require("crypto");
const { URL } = require("url");

const root = path.resolve(process.argv.includes("--repo") ? process.argv[process.argv.indexOf("--repo") + 1] : path.join(__dirname, "..", ".."));
const port = Number(process.argv.includes("--port") ? process.argv[process.argv.indexOf("--port") + 1] : 4173);
const locations = { "gun-family": ["Content/Items/Guns", ".gun.json"], "gear-set": ["Content/Items/Gear", ".gear.json"] };
const weaponFiles = ["weapon.json", "mk1.json", "mk2.json", "mk3.json"];
const mutationToken = crypto.randomBytes(24).toString("hex");

function git(args) { return execFileSync("git", args, { cwd: root, encoding: "utf8", stdio: ["ignore", "pipe", "pipe"] }).trim(); }
function send(res, status, value) {
  const body = JSON.stringify(value);
  res.writeHead(status, {
    "Content-Type": "application/json; charset=utf-8",
    "Content-Length": Buffer.byteLength(body),
    "Cache-Control": "no-store"
  });
  res.end(body);
}
function safePackage(kind, id) {
  const location = locations[kind];
  if (!location || !/^[a-z0-9]+(?:-[a-z0-9]+)*$/.test(id)) throw new Error("Invalid package identity.");
  const file = path.resolve(root, location[0], id + location[1]), base = path.resolve(root, location[0]) + path.sep;
  if (!file.startsWith(base)) throw new Error("Package path escaped the content folder.");
  return file;
}
function safeWeaponFolder(category, folder) {
  if (!/^[a-z0-9]+(?:[-_][a-z0-9]+)*$/.test(category || "")) throw new Error("Invalid weapon category folder.");
  if (!/^[a-z0-9]+(?:_[a-z0-9]+)*$/.test(folder || "")) throw new Error("Invalid weapon folder.");
  const base = path.resolve(root, "Content/Weapons");
  const target = path.resolve(base, category, folder);
  if (!target.startsWith(base + path.sep)) throw new Error("Weapon path escaped the content folder.");
  return target;
}
function readBody(req) {
  return new Promise((resolve, reject) => {
    let data = "";
    req.on("data", chunk => {
      data += chunk;
      if (data.length > 2_000_000) reject(new Error("Request too large."));
    });
    req.on("end", () => {
      try { resolve(JSON.parse(data || "{}")); }
      catch (error) { reject(error); }
    });
    req.on("error", reject);
  });
}
function status() {
  const changes = git(["status", "--porcelain"]).split(/\r?\n/).filter(Boolean);
  let behind = 0;
  try { behind = Number(git(["rev-list", "--count", "HEAD..@{upstream}"])) || 0; } catch (_) {}
  return { branch: git(["branch", "--show-current"]), clean: changes.length === 0, changed: changes.length, behind };
}
function readWeaponFolder(category, folder) {
  const target = safeWeaponFolder(category, folder);
  const files = {};
  weaponFiles.forEach(name => {
    const file = path.join(target, name);
    if (!fs.existsSync(file)) throw new Error(`Missing ${name}.`);
    files[name] = JSON.parse(fs.readFileSync(file, "utf8"));
  });
  return files;
}
function listWeaponFolders() {
  const base = path.resolve(root, "Content/Weapons");
  if (!fs.existsSync(base)) return [];
  const weapons = [];
  for (const categoryEntry of fs.readdirSync(base, { withFileTypes: true }).filter(entry => entry.isDirectory()).sort((a, b) => a.name.localeCompare(b.name))) {
    const categoryPath = path.join(base, categoryEntry.name);
    for (const weaponEntry of fs.readdirSync(categoryPath, { withFileTypes: true }).filter(entry => entry.isDirectory()).sort((a, b) => a.name.localeCompare(b.name))) {
      const sharedFile = path.join(categoryPath, weaponEntry.name, "weapon.json");
      if (!fs.existsSync(sharedFile)) continue;
      try {
        const shared = JSON.parse(fs.readFileSync(sharedFile, "utf8"));
        weapons.push({ category: categoryEntry.name, folder: weaponEntry.name, name: shared.name || weaponEntry.name });
      } catch (_) {
        weapons.push({ category: categoryEntry.name, folder: weaponEntry.name, name: weaponEntry.name });
      }
    }
  }
  return weapons;
}
function saveWeaponFolder(category, folder, files) {
  if (!files || typeof files !== "object" || Array.isArray(files)) throw new Error("Weapon files are required.");
  const received = Object.keys(files).sort();
  const expected = weaponFiles.slice().sort();
  if (received.length !== expected.length || received.some((name, index) => name !== expected[index])) throw new Error("Expected exactly weapon.json and mk1.json–mk3.json.");
  weaponFiles.forEach(name => {
    const value = files[name];
    if (!value || typeof value !== "object" || Array.isArray(value)) throw new Error(`${name}: root must be an object.`);
  });

  const target = safeWeaponFolder(category, folder);
  const categoryPath = path.dirname(target);
  const nonce = `${process.pid}-${Date.now()}-${crypto.randomBytes(4).toString("hex")}`;
  const stagingRoot = path.join(categoryPath, `.item-maker-${nonce}`);
  const stagingFolder = path.join(stagingRoot, folder);
  const backup = target + `.backup-${nonce}`;
  fs.mkdirSync(stagingFolder, { recursive: true });
  try {
    weaponFiles.forEach(name => fs.writeFileSync(path.join(stagingFolder, name), JSON.stringify(files[name], null, 2) + "\n"));
    let validation;
    try {
      validation = execFileSync(process.execPath, [path.join(__dirname, "validate-weapon-folder.js"), stagingFolder], {
        cwd: root, encoding: "utf8", stdio: ["ignore", "pipe", "pipe"]
      }).trim();
    } catch (error) {
      throw new Error(String(error.stderr || error.stdout || error.message).trim());
    }

    const hadTarget = fs.existsSync(target);
    if (hadTarget) fs.renameSync(target, backup);
    try {
      fs.renameSync(stagingFolder, target);
    } catch (error) {
      if (hadTarget && fs.existsSync(backup) && !fs.existsSync(target)) fs.renameSync(backup, target);
      throw error;
    }
    fs.rmSync(stagingRoot, { recursive: true, force: true });
    if (hadTarget) fs.rmSync(backup, { recursive: true, force: true });
    return { validation, saved: path.relative(root, target).replace(/\\/g, "/") };
  } catch (error) {
    fs.rmSync(stagingRoot, { recursive: true, force: true });
    throw error;
  }
}

function wait(milliseconds) { return new Promise(resolve => setTimeout(resolve, milliseconds)); }
async function requestStrongboxPreview(body) {
  const playerLevel = Number(body.playerLevel);
  const tierNumber = Number(body.tierNumber);
  const mode = body.mode === "analysis" ? "analysis" : "single";
  const sampleCount = mode === "analysis" ? Number(body.sampleCount || 1000) : 1;
  if (!Number.isInteger(playerLevel) || playerLevel < 0) throw new Error("Player level must be a non-negative integer.");
  if (!Number.isInteger(tierNumber) || tierNumber < 1 || tierNumber > 11) throw new Error("Strongbox tier must be between 1 and 11.");
  if (!Number.isInteger(sampleCount) || sampleCount < 1 || sampleCount > 10000) throw new Error("Analysis samples must be between 1 and 10,000.");
  const seed = String(body.seed ?? "").trim();
  if (!/^\d+$/.test(seed)) throw new Error("Seed must be an unsigned integer.");

  const requestId = crypto.randomUUID();
  const folder = path.join(root, "Temp", "ShooterMoverStrongboxPreview");
  const requestFile = path.join(folder, `${requestId}.request.json`);
  const responseFile = path.join(folder, `${requestId}.response.json`);
  fs.mkdirSync(folder, { recursive: true });
  const temporary = requestFile + ".tmp";
  fs.writeFileSync(temporary, JSON.stringify({
    requestId,
    mode,
    playerLevel,
    tierNumber,
    seed,
    sampleCount
  }));
  fs.renameSync(temporary, requestFile);

  const timeout = mode === "analysis" ? Math.min(120000, 15000 + sampleCount * 20) : 15000;
  const deadline = Date.now() + timeout;
  while (Date.now() < deadline && !fs.existsSync(responseFile)) await wait(50);
  if (!fs.existsSync(responseFile)) {
    fs.rmSync(requestFile, { force: true });
    throw new Error("Unity Strongbox bridge did not answer. Open this project in Unity and wait for scripts to finish compiling.");
  }
  const response = JSON.parse(fs.readFileSync(responseFile, "utf8"));
  fs.rmSync(responseFile, { force: true });
  return response;
}

async function api(req, res, url) {
  if (req.method === "GET" && url.pathname === "/api/status") return send(res, 200, { ...status(), mutationToken });
  if (req.method === "GET" && url.pathname === "/api/packages") {
    const packages = [];
    for (const [kind, [folder, suffix]] of Object.entries(locations)) {
      const dir = path.join(root, folder);
      if (!fs.existsSync(dir)) continue;
      for (const name of fs.readdirSync(dir).filter(x => x.endsWith(suffix)).sort()) {
        const value = JSON.parse(fs.readFileSync(path.join(dir, name), "utf8"));
        packages.push({ kind, id: value.id, name: value.name });
      }
    }
    return send(res, 200, { packages });
  }
  if (req.method === "GET" && url.pathname === "/api/package") {
    const file = safePackage(url.searchParams.get("kind"), url.searchParams.get("id"));
    return send(res, 200, { package: JSON.parse(fs.readFileSync(file, "utf8")) });
  }
  if (req.method === "GET" && url.pathname === "/api/weapon-folders") return send(res, 200, { weapons: listWeaponFolders() });
  if (req.method === "GET" && url.pathname === "/api/weapon-folder") {
    const category = url.searchParams.get("category"), folder = url.searchParams.get("folder");
    return send(res, 200, { category, folder, files: readWeaponFolder(category, folder) });
  }
  if (req.method === "POST" && url.pathname === "/api/strongbox-preview") {
    const body = await readBody(req);
    const result = await requestStrongboxPreview(body);
    return send(res, result.ok ? 200 : 400, result);
  }
  if (req.method !== "GET" && req.headers["x-item-maker-token"] !== mutationToken) throw new Error("Mutation token is missing or invalid.");
  if (req.method === "POST" && url.pathname === "/api/fetch") {
    git(["fetch", "--prune", "origin"]);
    return send(res, 200, status());
  }
  if (req.method === "POST" && url.pathname === "/api/pull") {
    if (!status().clean) throw new Error("Pull refused: the worktree is not clean.");
    git(["pull", "--ff-only"]);
    return send(res, 200, status());
  }
  if (req.method === "PUT" && url.pathname === "/api/package") {
    const body = await readBody(req), value = body.package;
    if (!value || !locations[value.kind] || value.$schema !== (value.kind === "gun-family" ? "shooter-mover.gun-family/1" : "shooter-mover.gear-set/1")) throw new Error("Package schema is invalid.");
    const file = safePackage(value.kind, value.id);
    fs.mkdirSync(path.dirname(file), { recursive: true });
    const temp = file + ".tmp";
    fs.writeFileSync(temp, JSON.stringify(value, null, 2) + "\n");
    fs.renameSync(temp, file);
    execFileSync(process.execPath, [path.join(__dirname, "compile-packages.js"), root], { cwd: root, stdio: "pipe" });
    return send(res, 200, { saved: path.relative(root, file).replace(/\\/g, "/") });
  }
  if (req.method === "PUT" && url.pathname === "/api/weapon-folder") {
    const body = await readBody(req);
    return send(res, 200, saveWeaponFolder(body.category, body.folder, body.files));
  }
  send(res, 404, { error: "Not found." });
}

const mime = { ".html": "text/html; charset=utf-8", ".js": "text/javascript; charset=utf-8", ".css": "text/css; charset=utf-8" };
http.createServer(async (req, res) => {
  try {
    const url = new URL(req.url, `http://${req.headers.host || "127.0.0.1"}`);
    if (url.pathname.startsWith("/api/")) return await api(req, res, url);
    const requested = url.pathname === "/" ? "index.html" : url.pathname.slice(1);
    const file = path.resolve(__dirname, requested);
    if (!file.startsWith(__dirname + path.sep) || !fs.existsSync(file) || fs.statSync(file).isDirectory()) return send(res, 404, { error: "Not found." });
    let body = fs.readFileSync(file);
    if (requested === "strongbox-simulator.html") {
      body = Buffer.from(body.toString("utf8").replace("</body>", '<script src="strongbox-production.js"></script><script src="strongbox-analysis-ui.js"></script><script src="strongbox-level-coverage.js"></script><script src="strongbox-analysis-cleanup.js"></script></body>'));
    }
    res.writeHead(200, { "Content-Type": mime[path.extname(file)] || "application/octet-stream", "Content-Length": body.length });
    res.end(body);
  } catch (error) {
    send(res, 400, { error: error.message });
  }
}).listen(port, "127.0.0.1", () => {
  console.log(`Item Maker: http://127.0.0.1:${port}`);
  console.log(`Repository: ${root}`);
});
