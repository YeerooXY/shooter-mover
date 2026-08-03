"use strict";

const schema = window.EnemySchema;
const $ = id => document.getElementById(id);
const fields = ["id", "name", "type", "hp", "speed", "move", "gun", "damage", "range", "detect", "scale", "drops", "art", "radius", "offsetX", "offsetY"];
let mutationToken = "";
let enemies = [];
let guns = [];
let currentId = null;
let leveling = null;
let jsonEditing = false;

function defaultEnemy() {
  return {
    id: "new-enemy",
    name: "New Enemy",
    type: "shooter",
    hp: 16,
    speed: 3.5,
    gun: guns[0]?.id || "",
    range: 7,
    detect: 16,
    scale: 0.7,
    drops: "normal",
    art: "new-enemy",
    body: { shape: "circle", radius: 0.45, offset: { x: 0, y: 0 } }
  };
}

function numberValue(id) {
  const value = Number($(id).value);
  return Number.isFinite(value) ? value : 0;
}

function compactEnemy() {
  const type = $("type").value;
  const enemy = {
    id: $("id").value.trim(),
    name: $("name").value.trim(),
    type,
    hp: numberValue("hp"),
    scale: numberValue("scale"),
    body: {
      shape: "circle",
      radius: numberValue("radius"),
      offset: { x: numberValue("offsetX"), y: numberValue("offsetY") }
    }
  };
  const speed = numberValue("speed");
  if (speed > 0 || $("move").value !== "stationary") enemy.speed = speed;
  if ($("move").value !== "direct") enemy.move = $("move").value;
  if (type === "shooter") enemy.gun = $("gun").value.trim();
  else enemy.damage = numberValue("damage");
  if (numberValue("range") > 0) enemy.range = numberValue("range");
  if (numberValue("detect") > 0) enemy.detect = numberValue("detect");
  if ($("drops").value.trim()) enemy.drops = $("drops").value.trim();
  if ($("art").value.trim()) enemy.art = $("art").value.trim();
  return enemy;
}

function applyEnemyToForm(enemy, persistedId, preserveJson) {
  $("id").value = enemy.id || "";
  $("name").value = enemy.name || "";
  $("type").value = enemy.type || "shooter";
  $("hp").value = enemy.hp ?? 16;
  $("speed").value = enemy.speed ?? 0;
  $("move").value = enemy.move || "direct";
  $("gun").value = enemy.gun || "";
  $("damage").value = enemy.damage ?? 0;
  $("range").value = enemy.range ?? 0;
  $("detect").value = enemy.detect ?? 0;
  $("scale").value = enemy.scale ?? 0.7;
  $("drops").value = enemy.drops || "";
  $("art").value = enemy.art || "";
  $("shape").value = enemy.body?.shape || "circle";
  $("radius").value = enemy.body?.radius ?? 0.45;
  $("offsetX").value = enemy.body?.offset?.x ?? 0;
  $("offsetY").value = enemy.body?.offset?.y ?? 0;
  currentId = persistedId || null;
  $("id").readOnly = Boolean(currentId);
  jsonEditing = Boolean(preserveJson);
  sync();
}

function currentEnemy() {
  if (jsonEditing) {
    try { return JSON.parse($("json").value); }
    catch (_) { return null; }
  }
  return compactEnemy();
}

function syncType() {
  const shooter = $("type").value === "shooter";
  $("gunRow").hidden = !shooter;
  $("damageRow").hidden = shooter;
}

function syncJson() {
  if (jsonEditing) return;
  $("json").value = `${JSON.stringify(compactEnemy(), null, 2)}\n`;
}

function clientErrors(enemy) {
  const errors = enemy ? schema.validateEnemy(enemy) : ["Advanced JSON is not valid JSON."];
  if (enemy?.type === "shooter" && !guns.some(gun => gun.id === enemy.gun)) {
    errors.push(`gun '${enemy.gun || ""}' is not present in Content/Weapons.`);
  }
  if (currentId && enemy?.id !== currentId) {
    errors.push("A loaded enemy ID is fixed in the first iteration. Use New enemy to create another ID.");
  }
  return errors;
}

function renderChecks(message) {
  const errors = clientErrors(currentEnemy());
  if (message && !errors.length) {
    $("checks").innerHTML = `<div class="check ok">${escapeHtml(message)}</div>`;
    return errors;
  }
  $("checks").innerHTML = errors.length
    ? errors.map(error => `<div class="check">${escapeHtml(error)}</div>`).join("")
    : '<div class="check ok">Enemy definition is valid.</div>';
  return errors;
}

function escapeHtml(value) {
  return String(value).replace(/[&<>"]/g, char => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;" }[char]));
}

function renderList() {
  const query = $("search").value.trim().toLowerCase();
  const visible = enemies.filter(enemy => `${enemy.name} ${enemy.id} ${enemy.type}`.toLowerCase().includes(query));
  $("enemyList").innerHTML = visible.map(enemy => `
    <button class="enemy-item ${enemy.id === currentId ? "active" : ""}" data-id="${enemy.id}">
      <strong>${escapeHtml(enemy.name)}</strong><span>${escapeHtml(enemy.type)} · ${escapeHtml(enemy.id)}</span>
    </button>`).join("") || '<p class="path">No enemies yet.</p>';
  document.querySelectorAll(".enemy-item").forEach(button => button.addEventListener("click", () => loadEnemy(button.dataset.id)));
}

function renderGuns() {
  $("gunOptions").innerHTML = guns.map(gun => `<option value="${escapeHtml(gun.id)}">${escapeHtml(gun.name)} · ${escapeHtml(gun.category)}</option>`).join("");
  $("gunCount").textContent = guns.length ? `${guns.length} canonical gun definitions found.` : "No canonical gun definitions found.";
}

function renderLeveling() {
  $("maxLevel").value = leveling.maxLevel;
  $("strengthAtMax").value = leveling.strengthAtMax;
  $("damagePower").value = leveling.damagePower;
  renderColorStops();
  $("level").max = leveling.maxLevel;
}

function readLevelingForm() {
  return {
    minLevel: 1,
    maxLevel: Number($("maxLevel").value),
    strengthAtMax: Number($("strengthAtMax").value),
    damagePower: Number($("damagePower").value),
    colors: Array.from(document.querySelectorAll(".color-stop")).map(row => ({
      level: Number(row.querySelector(".stop-level").value),
      color: row.querySelector(".stop-color").value.toUpperCase()
    })).sort((a, b) => a.level - b.level)
  };
}

function renderColorStops() {
  $("colorStops").innerHTML = leveling.colors.map((stop, index) => `
    <div class="color-stop" data-index="${index}">
      <input class="stop-level" type="number" min="1" max="${leveling.maxLevel}" step="1" value="${stop.level}" aria-label="Stop level">
      <input class="stop-color" type="color" value="${stop.color}" aria-label="Stop color">
      <button type="button" class="remove-stop" aria-label="Remove stop">×</button>
    </div>`).join("");
  document.querySelectorAll(".color-stop input").forEach(input => input.addEventListener("input", updateLevelingFromForm));
  document.querySelectorAll(".remove-stop").forEach(button => button.addEventListener("click", event => {
    if (leveling.colors.length <= 2) return;
    const index = Number(event.currentTarget.parentElement.dataset.index);
    leveling.colors.splice(index, 1);
    renderColorStops();
    sync();
  }));
  renderGradient();
}

function updateLevelingFromForm() {
  leveling = readLevelingForm();
  renderGradient();
  sync();
}

function renderGradient() {
  const max = Math.max(2, leveling.maxLevel);
  const stops = leveling.colors.map(stop => `${stop.color} ${((stop.level - 1) / (max - 1)) * 100}%`).join(", ");
  $("gradient").style.background = `linear-gradient(90deg, ${stops})`;
}

function drawPreview() {
  const canvas = $("preview");
  const ctx = canvas.getContext("2d");
  const enemy = currentEnemy();
  ctx.clearRect(0, 0, canvas.width, canvas.height);
  ctx.fillStyle = "#090d13";
  ctx.fillRect(0, 0, canvas.width, canvas.height);
  ctx.strokeStyle = "#151d29";
  ctx.lineWidth = 1;
  for (let x = 20; x < canvas.width; x += 20) { ctx.beginPath(); ctx.moveTo(x, 0); ctx.lineTo(x, canvas.height); ctx.stroke(); }
  for (let y = 20; y < canvas.height; y += 20) { ctx.beginPath(); ctx.moveTo(0, y); ctx.lineTo(canvas.width, y); ctx.stroke(); }
  if (!enemy || !leveling || schema.validateEnemy(enemy).length || schema.validateLeveling(leveling).length) return;

  const level = Number($("level").value);
  const stats = schema.resolvedStats(enemy, level, leveling);
  const cx = canvas.width / 2;
  const cy = canvas.height / 2;
  const unit = 18;
  const drawRing = (radius, color, dash) => {
    if (!(radius > 0)) return;
    ctx.save();
    ctx.strokeStyle = color;
    ctx.lineWidth = 1.5;
    ctx.setLineDash(dash);
    ctx.beginPath();
    ctx.arc(cx, cy, radius * unit, 0, Math.PI * 2);
    ctx.stroke();
    ctx.restore();
  };
  drawRing(enemy.detect, "rgba(140,170,220,.35)", [7, 7]);
  drawRing(enemy.range, "rgba(255,177,43,.55)", [3, 5]);

  const bodyX = cx + enemy.body.offset.x * unit;
  const bodyY = cy - enemy.body.offset.y * unit;
  ctx.fillStyle = stats.color;
  ctx.strokeStyle = "#111722";
  ctx.lineWidth = 8;
  ctx.beginPath();
  ctx.arc(bodyX, bodyY, enemy.body.radius * unit * 4, 0, Math.PI * 2);
  ctx.fill();
  ctx.stroke();
  ctx.strokeStyle = "#dce4ef";
  ctx.lineWidth = 2;
  ctx.beginPath();
  ctx.moveTo(bodyX, bodyY);
  ctx.lineTo(bodyX + 45, bodyY);
  ctx.stroke();
  ctx.fillStyle = "#dce4ef";
  ctx.beginPath();
  ctx.arc(cx, cy, 3, 0, Math.PI * 2);
  ctx.fill();

  $("levelValue").textContent = level;
  $("previewHp").textContent = stats.hp.toFixed(stats.hp < 100 ? 1 : 0);
  $("previewDamage").textContent = enemy.damage === undefined ? `gun ×${stats.damageMultiplier.toFixed(2)}` : stats.damage.toFixed(1);
  $("previewStrength").textContent = `×${stats.strength.toFixed(2)}`;
  $("previewColor").textContent = stats.color;
  $("shapeBadge").textContent = enemy.body.shape;
}

function sync() {
  syncType();
  syncJson();
  renderChecks();
  drawPreview();
  renderList();
}

async function request(url, options = {}) {
  const headers = { ...(options.headers || {}) };
  if (options.method && options.method !== "GET") headers["x-enemy-maker-token"] = mutationToken;
  const response = await fetch(url, { ...options, headers });
  const result = await response.json();
  if (!response.ok) throw new Error((result.errors || [result.error || "Request failed."]).join("\n"));
  return result;
}

async function loadEnemy(id) {
  const result = await request(`/api/enemy?id=${encodeURIComponent(id)}`);
  applyEnemyToForm(result.enemy, id, false);
}

async function loadAll() {
  const [status, list, gunData, levelData] = await Promise.all([
    request("/api/status"),
    request("/api/enemies"),
    request("/api/guns"),
    request("/api/leveling")
  ]);
  mutationToken = status.token;
  $("contentPath").textContent = `${status.content} · guns from ${status.weapons}`;
  enemies = list.enemies;
  guns = gunData.guns;
  leveling = levelData.leveling;
  renderGuns();
  renderLeveling();
  if (enemies.length) await loadEnemy(enemies[0].id);
  else applyEnemyToForm(defaultEnemy(), null, false);
}

async function saveCurrent() {
  const active = document.querySelector(".tab.active").dataset.workspace;
  $("save").disabled = true;
  $("save").textContent = "Saving…";
  try {
    if (active === "leveling") {
      leveling = readLevelingForm();
      const errors = schema.validateLeveling(leveling);
      if (errors.length) throw new Error(errors.join("\n"));
      await request("/api/leveling", {
        method: "PUT",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ leveling })
      });
      renderChecks("Leveling saved successfully.");
    } else {
      const enemy = currentEnemy();
      const errors = clientErrors(enemy);
      if (errors.length) throw new Error(errors.join("\n"));
      await request("/api/enemy", {
        method: "PUT",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ enemy, previousId: currentId })
      });
      currentId = enemy.id;
      const list = await request("/api/enemies");
      enemies = list.enemies;
      applyEnemyToForm(enemy, currentId, false);
      renderChecks("Enemy saved successfully.");
    }
  } catch (error) {
    $("checks").innerHTML = error.message.split("\n").map(line => `<div class="check">${escapeHtml(line)}</div>`).join("");
  } finally {
    $("save").disabled = false;
    $("save").textContent = "Save";
  }
}

function switchWorkspace(name) {
  document.querySelectorAll(".tab").forEach(tab => tab.classList.toggle("active", tab.dataset.workspace === name));
  $("enemyWorkspace").hidden = name !== "enemy";
  $("levelingWorkspace").hidden = name !== "leveling";
  $("jsonWorkspace").hidden = name !== "json";
  $("newEnemy").hidden = name === "leveling";
  if (name === "json" && !jsonEditing) syncJson();
}

fields.forEach(id => $(id).addEventListener("input", () => {
  jsonEditing = false;
  sync();
}));
$("shape").addEventListener("change", sync);
$("level").addEventListener("input", drawPreview);
$("search").addEventListener("input", renderList);
$("newEnemy").addEventListener("click", () => applyEnemyToForm(defaultEnemy(), null, false));
$("save").addEventListener("click", saveCurrent);
$("addColor").addEventListener("click", () => {
  leveling.colors.push({ level: Math.max(2, leveling.maxLevel - 1), color: "#FFFFFF" });
  leveling.colors.sort((a, b) => a.level - b.level);
  renderColorStops();
  sync();
});
["maxLevel", "strengthAtMax", "damagePower"].forEach(id => $(id).addEventListener("input", updateLevelingFromForm));
$("json").addEventListener("input", () => {
  jsonEditing = true;
  try {
    const parsed = JSON.parse($("json").value);
    applyEnemyToForm(parsed, currentId, true);
  } catch (_) {
    renderChecks();
    drawPreview();
  }
});
document.querySelectorAll(".tab").forEach(tab => tab.addEventListener("click", () => switchWorkspace(tab.dataset.workspace)));
window.addEventListener("keydown", event => {
  if ((event.ctrlKey || event.metaKey) && event.key.toLowerCase() === "s") {
    event.preventDefault();
    saveCurrent();
  }
});

loadAll().catch(error => {
  $("checks").innerHTML = `<div class="check">${escapeHtml(error.message)}</div>`;
});
