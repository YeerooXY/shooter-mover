"use strict";

const S = window.EnemySchema;
const $ = id => document.getElementById(id);
const baseIds = [
  "id", "name", "tags", "art", "drops", "traitPool", "hp",
  "healthPower", "detectionRange", "moveKind", "moveSpeed",
  "radius", "offsetX", "offsetY"
];

let token = "";
let enemies = [];
let shots = [];
let leveling = null;
let model = null;
let currentId = null;
let jsonMode = false;
let selectedMount = 0;
let selectedAttack = 0;
let dragMount = null;
let dragStart = null;
let mountZoom = 1;
const mountHistory = [];

const MIN_ZOOM = 0.5;
const MAX_ZOOM = 5;
const BASE_MOUNT_UNIT = 86;
const MAX_MOUNT_HISTORY = 50;

const copy = value => JSON.parse(JSON.stringify(value));
const esc = value => String(value).replace(/[&<>\"]/g, character => ({
  "&": "&amp;",
  "<": "&lt;",
  ">": "&gt;",
  '"': "&quot;"
}[character]));
const num = element => {
  const value = Number(element.value);
  return Number.isFinite(value) ? value : 0;
};
const parseTags = value => value.split(",").map(item => item.trim()).filter(Boolean);
const clamp = (value, min, max) => Math.max(min, Math.min(max, value));

function createMount(index = 0) {
  return {
    id: index ? `gun-${index + 1}` : "center-gun",
    position: { x: index * 0.25, y: 0.24 },
    rotation: 0,
    art: "gun.small-cannon"
  };
}

function createAttack() {
  return {
    id: "rifle-burst",
    kind: "shot",
    shot: shots[0]?.id || "small-bullet",
    emitters: [model?.mounts?.[0]?.id || "center-gun"].filter(Boolean),
    firePattern: "single",
    cooldown: 1.5,
    sequence: { triggers: 4, interval: 0.2 },
    volley: { shotsPerTrigger: 1, spread: 0, distribution: "even" },
    range: { min: 2, max: 12 },
    damage: [{ type: "kinetic", amount: 3 }]
  };
}

function createFreshEnemy() {
  const enemy = {
    schema: 1,
    id: "new-droid",
    name: "New Droid",
    tags: ["droid", "ground", "mobile", "ranged"],
    hp: 16,
    healthPower: 0.7,
    movement: { kind: "strafe", speed: 3.5 },
    detectionRange: 16,
    mounts: [createMount()],
    attacks: [],
    drops: "normal",
    art: "new-droid",
    body: { shape: "circle", radius: 0.45, offset: { x: 0, y: 0 } }
  };
  model = enemy;
  enemy.attacks.push(createAttack());
  return enemy;
}

function readBaseForm() {
  if (!model) return;
  model.schema = 1;
  model.id = $("id").value.trim();
  model.name = $("name").value.trim();
  model.tags = parseTags($("tags").value);
  model.hp = num($("hp"));
  model.healthPower = num($("healthPower"));
  model.detectionRange = num($("detectionRange"));
  model.movement = {
    kind: $("moveKind").value,
    speed: num($("moveSpeed"))
  };
  model.body = {
    shape: "circle",
    radius: num($("radius")),
    offset: { x: num($("offsetX")), y: num($("offsetY")) }
  };

  for (const key of ["art", "drops", "traitPool"]) {
    const value = $(key).value.trim();
    if (value) model[key] = value;
    else delete model[key];
  }
}

function resetMountHistory() {
  mountHistory.length = 0;
  dragStart = null;
  updateUndoButton();
}

function updateUndoButton() {
  const button = $("undoMountDrag");
  if (!button) return;
  button.disabled = mountHistory.length === 0;
  button.title = mountHistory.length
    ? `Undo last mount drag (${mountHistory.length} available)`
    : "No mount drag to undo";
}

function fill(enemy, persistedId = null, preserveJson = false, keepHistory = false) {
  model = copy(enemy);
  model.mounts = Array.isArray(model.mounts) ? model.mounts : [];
  model.attacks = Array.isArray(model.attacks) ? model.attacks : [];

  $("id").value = model.id || "";
  $("name").value = model.name || "";
  $("tags").value = (model.tags || []).join(", ");
  $("art").value = model.art || "";
  $("drops").value = model.drops || "";
  $("traitPool").value = model.traitPool || "";
  $("hp").value = model.hp ?? 16;
  $("healthPower").value = model.healthPower ?? 0.7;
  $("detectionRange").value = model.detectionRange ?? 16;
  $("moveKind").value = model.movement?.kind || "direct";
  $("moveSpeed").value = model.movement?.speed ?? 0;
  $("shape").value = model.body?.shape || "circle";
  $("radius").value = model.body?.radius ?? 0.45;
  $("offsetX").value = model.body?.offset?.x ?? 0;
  $("offsetY").value = model.body?.offset?.y ?? 0;

  currentId = persistedId;
  $("id").readOnly = Boolean(persistedId);
  jsonMode = preserveJson;
  selectedMount = Math.min(selectedMount, Math.max(0, model.mounts.length - 1));
  selectedAttack = Math.min(selectedAttack, Math.max(0, model.attacks.length - 1));

  if (!keepHistory) resetMountHistory();
  renderMounts();
  renderAttacks();
  sync();
}

function currentEnemy() {
  if (jsonMode) {
    try {
      return JSON.parse($("json").value);
    } catch (_) {
      return null;
    }
  }
  readBaseForm();
  return model;
}

function validationErrors(enemy) {
  const errors = enemy ? S.validateEnemy(enemy) : ["Advanced JSON is not valid JSON."];
  const knownShots = new Set(shots.map(shot => shot.id));
  for (const attack of enemy?.attacks || []) {
    if (attack.kind === "shot" && !knownShots.has(attack.shot)) {
      errors.push(`attack '${attack.id || "unknown"}' references unknown Enemy Shot '${attack.shot || ""}'.`);
    }
  }
  if (currentId && enemy?.id !== currentId) {
    errors.push("A loaded enemy ID is fixed. Use New enemy to create another identity.");
  }
  return errors;
}

function renderChecks(successMessage = "") {
  const errors = validationErrors(currentEnemy());
  $("checks").innerHTML = errors.length
    ? errors.map(error => `<div class="check">${esc(error)}</div>`).join("")
    : `<div class="check ok">${esc(successMessage || "Enemy definition is valid.")}</div>`;
  return errors;
}

function sync() {
  if (!jsonMode && model) $("json").value = `${JSON.stringify(model, null, 2)}\n`;
  renderChecks();
  drawPreview();
  renderEnemyList();
  updateUndoButton();
}

function renderEnemyList() {
  const query = $("search").value.trim().toLowerCase();
  const visible = enemies.filter(enemy =>
    `${enemy.name} ${enemy.id} ${enemy.movement}`.toLowerCase().includes(query)
  );
  $("enemyList").innerHTML = visible.map(enemy => `
    <button class="enemy-item ${enemy.id === currentId ? "active" : ""}" data-id="${esc(enemy.id)}">
      <strong>${esc(enemy.name)}</strong>
      <span>${esc(enemy.movement)} · ${enemy.mountCount} mounts · ${enemy.attackCount} attacks</span>
    </button>`).join("") || '<p class="path">No enemies yet.</p>';
  document.querySelectorAll(".enemy-item").forEach(button => {
    button.onclick = () => loadEnemy(button.dataset.id);
  });
}

function renderShots() {
  $("shotOptions").innerHTML = shots.map(shot =>
    `<option value="${esc(shot.id)}">${esc(shot.kind)} · range ${shot.range}</option>`
  ).join("");
  $("shotCount").textContent = shots.length
    ? `${shots.length} reusable Enemy Shot definitions found.`
    : "No valid Enemy Shot definitions found under Content/EnemyShots.";
}

function renderMounts() {
  $("mounts").innerHTML = model.mounts.map((mount, index) => `
    <div class="editor-card mount-card ${index === selectedMount ? "selected" : ""}" data-index="${index}">
      <div class="card-heading">
        <strong>Mount ${index + 1}</strong>
        <button class="danger-link remove-mount" type="button">Remove</button>
      </div>
      <div class="compact-grid mount-grid">
        <label>ID <input data-field="id" value="${esc(mount.id || "")}"></label>
        <label>Position X <input data-field="position.x" type="number" step=".01" value="${mount.position?.x ?? 0}"></label>
        <label>Position Y <input data-field="position.y" type="number" step=".01" value="${mount.position?.y ?? 0}"></label>
        <label>Rotation ° <input data-field="rotation" type="number" step="1" value="${mount.rotation ?? 0}"></label>
        <label class="span-2">Gun art <input data-field="art" value="${esc(mount.art || "")}" placeholder="optional"></label>
      </div>
    </div>`).join("") || '<p class="help">No mounts. Contact-only enemies may leave this empty.</p>';
}

function options(values, selected) {
  return values.map(value =>
    `<option value="${value}" ${value === selected ? "selected" : ""}>${value}</option>`
  ).join("");
}

function renderAttacks() {
  $("attacks").innerHTML = model.attacks.map((attack, index) => {
    const damage = attack.damage?.[0] || { type: "kinetic", amount: 1 };
    const isShot = attack.kind === "shot";
    const chosenEmitters = new Set(attack.emitters || []);
    return `
      <div class="editor-card attack-card ${index === selectedAttack ? "selected" : ""}" data-index="${index}">
        <div class="card-heading">
          <strong>Attack ${index + 1}</strong>
          <div class="card-actions">
            <button class="ghost tiny duplicate-attack" type="button">Duplicate</button>
            <button class="danger-link remove-attack" type="button">Remove</button>
          </div>
        </div>
        <div class="compact-grid">
          <label>ID <input data-field="id" value="${esc(attack.id || "")}"></label>
          <label>Kind <select data-field="kind">${options(["shot", "contact", "suicide"], attack.kind)}</select></label>
          <label>Cooldown s <input data-field="cooldown" type="number" min="0" step=".05" value="${attack.cooldown ?? 0}"></label>
          <label>Damage type <select data-field="damage.0.type">${options(["kinetic", "thermal", "electric", "explosive", "impact"], damage.type)}</select></label>
          <label>Direct damage <input data-field="damage.0.amount" type="number" min=".01" step=".1" value="${damage.amount ?? 1}"></label>
          <label>Min range <input data-field="range.min" type="number" min="0" step=".1" value="${attack.range?.min ?? 0}"></label>
          <label>Max range <input data-field="range.max" type="number" min=".01" step=".1" value="${attack.range?.max ?? 1}"></label>
        </div>
        <div class="shot-fields" ${isShot ? "" : "hidden"}>
          <h3>Shot firing</h3>
          <div class="compact-grid">
            <label>Enemy Shot <input data-field="shot" list="shotOptions" value="${esc(attack.shot || "")}"></label>
            <label>Fire pattern <select data-field="firePattern">${options(["single", "simultaneous", "alternate", "round-robin"], attack.firePattern)}</select></label>
            <label>Triggers <input data-field="sequence.triggers" type="number" min="1" step="1" value="${attack.sequence?.triggers ?? 1}"></label>
            <label>Interval s <input data-field="sequence.interval" type="number" min="0" step=".01" value="${attack.sequence?.interval ?? 0}"></label>
            <label>Shots per trigger <input data-field="volley.shotsPerTrigger" type="number" min="1" step="1" value="${attack.volley?.shotsPerTrigger ?? 1}"></label>
            <label>Spread ° <input data-field="volley.spread" type="number" min="0" step="1" value="${attack.volley?.spread ?? 0}"></label>
            <label>Distribution <select data-field="volley.distribution">${options(["even", "random"], attack.volley?.distribution)}</select></label>
            <label>Emitters
              <select data-field="emitters" multiple>
                ${model.mounts.map(mount => `<option value="${esc(mount.id)}" ${chosenEmitters.has(mount.id) ? "selected" : ""}>${esc(mount.id)}</option>`).join("")}
              </select>
            </label>
          </div>
        </div>
      </div>`;
  }).join("") || '<p class="help">No attacks. This is valid for harmless or purely scripted enemies.</p>';
}

function setPath(object, path, value) {
  const parts = path.split(".");
  let target = object;
  for (let index = 0; index < parts.length - 1; index += 1) {
    const key = /^\d+$/.test(parts[index]) ? Number(parts[index]) : parts[index];
    if (target[key] == null) target[key] = /^\d+$/.test(parts[index + 1]) ? [] : {};
    target = target[key];
  }
  const finalPart = parts.at(-1);
  target[/^\d+$/.test(finalPart) ? Number(finalPart) : finalPart] = value;
}

function ensureShotFields(attack) {
  if (attack.kind !== "shot") {
    for (const key of ["shot", "emitters", "firePattern", "sequence", "volley"]) delete attack[key];
    return;
  }
  attack.shot ??= shots[0]?.id || "small-bullet";
  attack.emitters = Array.isArray(attack.emitters) && attack.emitters.length
    ? attack.emitters
    : [model.mounts[0]?.id].filter(Boolean);
  attack.firePattern ??= "single";
  attack.sequence ??= { triggers: 1, interval: 0 };
  attack.volley ??= { shotsPerTrigger: 1, spread: 0, distribution: "even" };
}

function levelingFromForm() {
  return {
    minLevel: 1,
    maxLevel: Number($("maxLevel").value),
    strengthAtMax: Number($("strengthAtMax").value),
    damagePower: Number($("damagePower").value),
    colors: [...document.querySelectorAll(".color-stop")].map(row => ({
      level: Number(row.querySelector(".stop-level").value),
      color: row.querySelector(".stop-color").value.toUpperCase()
    })).sort((left, right) => left.level - right.level)
  };
}

function renderLeveling() {
  $("maxLevel").value = leveling.maxLevel;
  $("strengthAtMax").value = leveling.strengthAtMax;
  $("damagePower").value = leveling.damagePower;
  $("level").max = leveling.maxLevel;
  renderColorStops();
}

function renderColorStops() {
  $("colorStops").innerHTML = leveling.colors.map((stop, index) => `
    <div class="color-stop" data-index="${index}">
      <input class="stop-level" type="number" min="1" max="${leveling.maxLevel}" value="${stop.level}">
      <input class="stop-color" type="color" value="${stop.color}">
      <button class="remove-stop" type="button">×</button>
    </div>`).join("");
  document.querySelectorAll(".color-stop input").forEach(input => input.oninput = updateLeveling);
  document.querySelectorAll(".remove-stop").forEach(button => {
    button.onclick = () => {
      if (leveling.colors.length <= 2) return;
      leveling.colors.splice(Number(button.parentElement.dataset.index), 1);
      renderColorStops();
      sync();
    };
  });
  renderGradient();
}

function updateLeveling() {
  leveling = levelingFromForm();
  renderGradient();
  sync();
}

function renderGradient() {
  const max = Math.max(2, leveling.maxLevel);
  const stops = leveling.colors.map(stop =>
    `${stop.color} ${((stop.level - 1) / (max - 1)) * 100}%`
  ).join(", ");
  $("gradient").style.background = `linear-gradient(90deg, ${stops})`;
}

function mountUnit() {
  return BASE_MOUNT_UNIT * mountZoom;
}

function mountScreenPosition(mount, canvas, unit = mountUnit()) {
  return {
    x: canvas.width / 2 + mount.position.x * unit,
    y: canvas.height / 2 - mount.position.y * unit
  };
}

function setMountZoom(value) {
  mountZoom = Math.round(clamp(Number(value), MIN_ZOOM, MAX_ZOOM) * 10) / 10;
  $("mountZoom").value = String(mountZoom);
  $("zoomValue").textContent = `${Math.round(mountZoom * 100)}%`;
  drawPreview();
}

function drawPreview() {
  const canvas = $("preview");
  const context = canvas.getContext("2d");
  const enemy = currentEnemy();

  context.clearRect(0, 0, canvas.width, canvas.height);
  context.fillStyle = "#090d13";
  context.fillRect(0, 0, canvas.width, canvas.height);
  context.strokeStyle = "#151d29";
  context.lineWidth = 1;
  for (let x = 20; x < canvas.width; x += 20) {
    context.beginPath();
    context.moveTo(x, 0);
    context.lineTo(x, canvas.height);
    context.stroke();
  }
  for (let y = 20; y < canvas.height; y += 20) {
    context.beginPath();
    context.moveTo(0, y);
    context.lineTo(canvas.width, y);
    context.stroke();
  }

  if (!enemy || !leveling || S.validateEnemy(enemy).length || S.validateLeveling(leveling).length) return;

  const level = Number($("level").value);
  const stats = S.resolvedStats(enemy, level, leveling);
  const centerX = canvas.width / 2;
  const centerY = canvas.height / 2;
  const rangeUnit = 12;
  const localUnit = mountUnit();

  const ring = (radius, color, dash = []) => {
    if (!(radius > 0)) return;
    context.save();
    context.strokeStyle = color;
    context.setLineDash(dash);
    context.beginPath();
    context.arc(centerX, centerY, radius * rangeUnit, 0, Math.PI * 2);
    context.stroke();
    context.restore();
  };

  ring(enemy.detectionRange, "rgba(140,170,220,.35)", [7, 7]);
  for (const attack of enemy.attacks || []) ring(attack.range?.max, "rgba(255,177,43,.28)", [3, 5]);

  const bodyX = centerX + enemy.body.offset.x * localUnit;
  const bodyY = centerY - enemy.body.offset.y * localUnit;
  context.fillStyle = stats.color;
  context.strokeStyle = "#111722";
  context.lineWidth = Math.max(4, 8 * Math.sqrt(mountZoom));
  context.beginPath();
  context.arc(bodyX, bodyY, enemy.body.radius * localUnit, 0, Math.PI * 2);
  context.fill();
  context.stroke();

  context.strokeStyle = "#dce4ef";
  context.lineWidth = 2;
  context.beginPath();
  context.moveTo(centerX, centerY);
  context.lineTo(centerX, centerY - Math.min(100, 48 * Math.sqrt(mountZoom)));
  context.stroke();

  const selected = enemy.attacks[selectedAttack] || enemy.attacks[0];
  for (let index = 0; index < enemy.mounts.length; index += 1) {
    const mount = enemy.mounts[index];
    const point = mountScreenPosition(mount, canvas, localUnit);
    const angle = (mount.rotation || 0) * Math.PI / 180;
    const directionLength = Math.min(70, 25 * Math.sqrt(mountZoom));
    const directionX = Math.sin(angle) * directionLength;
    const directionY = -Math.cos(angle) * directionLength;

    context.strokeStyle = index === selectedMount ? "#fff1c9" : "#ffb12b";
    context.lineWidth = index === selectedMount ? 3 : 2;
    context.beginPath();
    context.moveTo(point.x, point.y);
    context.lineTo(point.x + directionX, point.y + directionY);
    context.stroke();

    context.fillStyle = "#ffb12b";
    context.beginPath();
    context.arc(point.x, point.y, index === selectedMount ? 9 : 7, 0, Math.PI * 2);
    context.fill();
    context.fillStyle = "#111";
    context.font = "10px sans-serif";
    context.textAlign = "center";
    context.fillText(String(index + 1), point.x, point.y + 3);
  }

  if (selected?.kind === "shot" && selected.volley?.spread > 0) {
    context.save();
    context.strokeStyle = "rgba(255,177,43,.62)";
    context.lineWidth = 1.5;
    context.setLineDash([4, 4]);
    for (const emitterId of selected.emitters || []) {
      const mount = enemy.mounts.find(candidate => candidate.id === emitterId);
      if (!mount) continue;
      const point = mountScreenPosition(mount, canvas, localUnit);
      const halfSpread = selected.volley.spread / 2;
      for (const degrees of [(mount.rotation || 0) - halfSpread, (mount.rotation || 0) + halfSpread]) {
        const radians = degrees * Math.PI / 180;
        const length = Math.min(160, 78 * Math.sqrt(mountZoom));
        context.beginPath();
        context.moveTo(point.x, point.y);
        context.lineTo(point.x + Math.sin(radians) * length, point.y - Math.cos(radians) * length);
        context.stroke();
      }
    }
    context.restore();
  }

  const projectiles = selected ? S.projectilesPerSequence(selected) : 0;
  const directDamage = selected ? S.directDamagePerHit(selected) : 0;
  $("levelValue").textContent = level;
  $("previewHp").textContent = stats.hp.toFixed(stats.hp < 100 ? 1 : 0);
  $("previewDamage").textContent = `×${stats.damageMultiplier.toFixed(2)}`;
  $("previewProjectiles").textContent = projectiles || "—";
  $("previewBurst").textContent = selected
    ? (projectiles * directDamage * stats.damageMultiplier).toFixed(1)
    : "—";
  $("previewStrength").textContent = `×${stats.strength.toFixed(2)}`;
  $("previewColor").textContent = stats.color;
  $("shapeBadge").textContent = enemy.body.shape;
}

async function request(url, options = {}) {
  const headers = { ...(options.headers || {}) };
  if (options.method && options.method !== "GET") headers["x-enemy-maker-token"] = token;
  const response = await fetch(url, { ...options, headers });
  const result = await response.json();
  if (!response.ok) throw new Error((result.errors || [result.error || "Request failed."]).join("\n"));
  return result;
}

async function loadEnemy(id) {
  const result = await request(`/api/enemy?id=${encodeURIComponent(id)}`);
  selectedMount = 0;
  selectedAttack = 0;
  setMountZoom(1);
  fill(result.enemy, id);
}

async function loadAll() {
  const [status, list, shotData, levelData] = await Promise.all([
    request("/api/status"),
    request("/api/enemies"),
    request("/api/shots"),
    request("/api/leveling")
  ]);
  token = status.token;
  $("contentPath").textContent = `${status.content} · shots from ${status.shots}`;
  enemies = list.enemies;
  shots = shotData.shots;
  leveling = levelData.leveling;
  renderShots();
  renderLeveling();
  setMountZoom(1);
  if (enemies.length) await loadEnemy(enemies[0].id);
  else fill(createFreshEnemy());
}

async function saveCurrent() {
  const activeWorkspace = document.querySelector(".tab.active").dataset.workspace;
  $("save").disabled = true;
  $("save").textContent = "Saving…";
  try {
    if (activeWorkspace === "leveling") {
      leveling = levelingFromForm();
      const errors = S.validateLeveling(leveling);
      if (errors.length) throw new Error(errors.join("\n"));
      await request("/api/leveling", {
        method: "PUT",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ leveling })
      });
      renderChecks("Leveling saved successfully.");
      return;
    }

    const enemy = currentEnemy();
    const errors = validationErrors(enemy);
    if (errors.length) throw new Error(errors.join("\n"));
    await request("/api/enemy", {
      method: "PUT",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ enemy, previousId: currentId })
    });
    currentId = enemy.id;
    enemies = (await request("/api/enemies")).enemies;
    fill(enemy, currentId, false, true);
    renderChecks("Enemy saved successfully.");
  } catch (error) {
    $("checks").innerHTML = error.message.split("\n").map(line => `<div class="check">${esc(line)}</div>`).join("");
  } finally {
    $("save").disabled = false;
    $("save").textContent = "Save";
  }
}

function switchWorkspace(name) {
  if (name !== "json" && jsonMode) {
    try {
      fill(JSON.parse($("json").value), currentId);
    } catch (_) {
      // Keep the current guided form when advanced JSON is incomplete.
    }
  }
  document.querySelectorAll(".tab").forEach(tab => tab.classList.toggle("active", tab.dataset.workspace === name));
  $("enemyWorkspace").hidden = name !== "enemy";
  $("levelingWorkspace").hidden = name !== "leveling";
  $("jsonWorkspace").hidden = name !== "json";
  $("newEnemy").hidden = name === "leveling";
  if (name === "json" && !jsonMode) sync();
}

function handleMountField(event) {
  const card = event.target.closest(".mount-card");
  if (!card || !event.target.dataset.field) return;
  const index = Number(card.dataset.index);
  const path = event.target.dataset.field;
  setPath(model.mounts[index], path, event.target.type === "number" ? num(event.target) : event.target.value.trim());
  if (path === "id" || path.startsWith("position.")) resetMountHistory();
  selectedMount = index;
  if (path === "id" && event.type === "change") renderAttacks();
  sync();
}

function handleAttackField(event) {
  const card = event.target.closest(".attack-card");
  if (!card || !event.target.dataset.field) return;
  const index = Number(card.dataset.index);
  const attack = model.attacks[index];
  const path = event.target.dataset.field;
  let value;
  if (path === "emitters") value = [...event.target.selectedOptions].map(option => option.value);
  else value = event.target.type === "number" ? num(event.target) : event.target.value;
  setPath(attack, path, value);
  if (path === "kind") {
    ensureShotFields(attack);
    renderAttacks();
  }
  selectedAttack = index;
  sync();
}

function canvasPoint(event) {
  const canvas = $("preview");
  const rect = canvas.getBoundingClientRect();
  return {
    x: (event.clientX - rect.left) * canvas.width / rect.width,
    y: (event.clientY - rect.top) * canvas.height / rect.height
  };
}

function hitMount(point) {
  const canvas = $("preview");
  const unit = mountUnit();
  let best = null;
  let distance = 16;
  for (let index = 0; index < (model?.mounts || []).length; index += 1) {
    const screen = mountScreenPosition(model.mounts[index], canvas, unit);
    const candidateDistance = Math.hypot(point.x - screen.x, point.y - screen.y);
    if (candidateDistance < distance) {
      best = index;
      distance = candidateDistance;
    }
  }
  return best;
}

function dragSelectedMount(event) {
  if (dragMount === null) return;
  const canvas = $("preview");
  const point = canvasPoint(event);
  const unit = mountUnit();
  const mount = model.mounts[dragMount];
  mount.position.x = Math.round(((point.x - canvas.width / 2) / unit) * 100) / 100;
  mount.position.y = Math.round(((canvas.height / 2 - point.y) / unit) * 100) / 100;

  const card = document.querySelector(`.mount-card[data-index="${dragMount}"]`);
  if (card) {
    card.querySelector('[data-field="position.x"]').value = mount.position.x;
    card.querySelector('[data-field="position.y"]').value = mount.position.y;
  }
  sync();
}

function beginMountDrag(index) {
  dragMount = selectedMount = index;
  const mount = model.mounts[index];
  dragStart = {
    index,
    id: mount.id,
    before: copy(mount.position)
  };
}

function finishMountDrag() {
  if (dragMount === null || !dragStart) {
    dragMount = null;
    dragStart = null;
    return;
  }
  const mount = model.mounts[dragMount];
  const changed = mount && (
    mount.position.x !== dragStart.before.x || mount.position.y !== dragStart.before.y
  );
  if (changed) {
    mountHistory.push({ ...dragStart, after: copy(mount.position) });
    if (mountHistory.length > MAX_MOUNT_HISTORY) mountHistory.shift();
  }
  dragMount = null;
  dragStart = null;
  updateUndoButton();
}

function undoLastMountDrag() {
  const entry = mountHistory.pop();
  if (!entry || !model) return;
  let index = model.mounts.findIndex(mount => mount.id === entry.id);
  if (index < 0 && model.mounts[entry.index]) index = entry.index;
  if (index < 0) {
    updateUndoButton();
    return;
  }
  model.mounts[index].position = copy(entry.before);
  selectedMount = index;
  renderMounts();
  sync();
}

function isEditableElement(element) {
  return element && ["INPUT", "TEXTAREA", "SELECT"].includes(element.tagName);
}

baseIds.forEach(id => {
  $(id).oninput = () => {
    jsonMode = false;
    sync();
  };
});

$("shape").onchange = sync;
$("level").oninput = drawPreview;
$("search").oninput = renderEnemyList;
$("save").onclick = saveCurrent;
$("newEnemy").onclick = () => {
  selectedMount = 0;
  selectedAttack = 0;
  setMountZoom(1);
  fill(createFreshEnemy());
  switchWorkspace("enemy");
};

$("addMount").onclick = () => {
  readBaseForm();
  model.mounts.push(createMount(model.mounts.length));
  selectedMount = model.mounts.length - 1;
  renderMounts();
  renderAttacks();
  sync();
};

$("addAttack").onclick = () => {
  readBaseForm();
  model.attacks.push(createAttack());
  selectedAttack = model.attacks.length - 1;
  renderAttacks();
  sync();
};

$("mounts").oninput = handleMountField;
$("mounts").onchange = handleMountField;
$("mounts").onclick = event => {
  const card = event.target.closest(".mount-card");
  if (!card) return;
  const index = Number(card.dataset.index);
  selectedMount = index;
  if (event.target.classList.contains("remove-mount")) {
    const removedId = model.mounts[index].id;
    model.mounts.splice(index, 1);
    for (const attack of model.attacks) attack.emitters = attack.emitters?.filter(id => id !== removedId);
    selectedMount = Math.max(0, Math.min(index, model.mounts.length - 1));
    resetMountHistory();
    renderMounts();
    renderAttacks();
  } else {
    renderMounts();
  }
  sync();
};

$("attacks").oninput = handleAttackField;
$("attacks").onchange = handleAttackField;
$("attacks").onclick = event => {
  const card = event.target.closest(".attack-card");
  if (!card) return;
  const index = Number(card.dataset.index);
  selectedAttack = index;
  if (event.target.classList.contains("remove-attack")) {
    model.attacks.splice(index, 1);
    selectedAttack = Math.max(0, Math.min(index, model.attacks.length - 1));
  } else if (event.target.classList.contains("duplicate-attack")) {
    const duplicated = copy(model.attacks[index]);
    duplicated.id += "-copy";
    model.attacks.splice(index + 1, 0, duplicated);
    selectedAttack = index + 1;
  }
  renderAttacks();
  sync();
};

$("json").oninput = () => {
  jsonMode = true;
  try {
    fill(JSON.parse($("json").value), currentId, true);
  } catch (_) {
    renderChecks();
  }
};

$("addColor").onclick = () => {
  leveling.colors.push({ level: Math.max(2, Math.round(leveling.maxLevel / 2)), color: "#FFFFFF" });
  leveling.colors.sort((left, right) => left.level - right.level);
  renderColorStops();
  sync();
};

["maxLevel", "strengthAtMax", "damagePower"].forEach(id => $(id).oninput = updateLeveling);
document.querySelectorAll(".tab").forEach(tab => tab.onclick = () => switchWorkspace(tab.dataset.workspace));

$("undoMountDrag").onclick = undoLastMountDrag;
$("mountZoom").oninput = event => setMountZoom(event.target.value);
$("zoomOut").onclick = () => setMountZoom(mountZoom - 0.2);
$("zoomIn").onclick = () => setMountZoom(mountZoom + 0.2);
$("resetZoom").onclick = () => setMountZoom(1);

window.onkeydown = event => {
  const key = event.key.toLowerCase();
  if ((event.ctrlKey || event.metaKey) && key === "s") {
    event.preventDefault();
    saveCurrent();
  } else if ((event.ctrlKey || event.metaKey) && key === "z" && !isEditableElement(document.activeElement) && mountHistory.length) {
    event.preventDefault();
    undoLastMountDrag();
  }
};

$("preview").onwheel = event => {
  event.preventDefault();
  setMountZoom(mountZoom + (event.deltaY < 0 ? 0.2 : -0.2));
};

$("preview").onpointerdown = event => {
  const index = hitMount(canvasPoint(event));
  if (index === null) return;
  beginMountDrag(index);
  $("preview").setPointerCapture(event.pointerId);
  renderMounts();
  drawPreview();
};
$("preview").onpointermove = dragSelectedMount;
$("preview").onpointerup = event => {
  if (dragMount !== null) $("preview").releasePointerCapture(event.pointerId);
  finishMountDrag();
};
$("preview").onpointercancel = finishMountDrag;

loadAll().catch(error => {
  $("checks").innerHTML = `<div class="check">${esc(error.message || error)}</div>`;
});
