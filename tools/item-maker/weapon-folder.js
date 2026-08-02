"use strict";

const fileNames = ["weapon.json", "mk1.json", "mk2.json", "mk3.json"];
const elements = {
  newWeaponButton: document.querySelector("#newWeaponButton"),
  saveButton: document.querySelector("#saveButton"),
  saveStatus: document.querySelector("#saveStatus"),
  repoStatus: document.querySelector("#repoStatus"),
  weaponSearchInput: document.querySelector("#weaponSearchInput"),
  weaponList: document.querySelector("#weaponList"),
  categoryInput: document.querySelector("#categoryInput"),
  folderInput: document.querySelector("#folderInput"),
  sourcePath: document.querySelector("#sourcePath"),
  fileTabs: document.querySelector("#fileTabs"),
  jsonEditor: document.querySelector("#jsonEditor"),
  generatedIds: document.querySelector("#generatedIds"),
  checks: document.querySelector("#checks")
};

let activeFile = "weapon.json";
let mutationToken = "";
let dirty = false;
let saving = false;
let helperOnline = true;
let loadedIdentity = null;
let weaponCatalogue = [];
let files = makeDefaultFiles();

function format(value) { return JSON.stringify(value, null, 2) + "\n"; }
function escapeHtml(value) {
  return String(value ?? "").replace(/&/g, "&amp;").replace(/</g, "&lt;")
    .replace(/>/g, "&gt;").replace(/\"/g, "&quot;").replace(/'/g, "&#039;");
}
function folderSlug(value) {
  return String(value || "").toLowerCase().trim().replace(/[^a-z0-9]+/g, "_").replace(/^_+|_+$/g, "");
}
function makeDefaultFiles() {
  return {
    "weapon.json": format({
      name: "New Weapon",
      description: "",
      category: "normal-firearm",
      rarity: "common",
      projectileType: "bullet",
      damageType: "physical",
      fire: { mode: "automatic", rate: 4 },
      shot: { projectiles: 1, spread: 0 },
      projectile: { speed: 20, radius: 0.1, range: 25 },
      impact: { pierce: 1, ricochet: 0, knockback: 0 },
      art: { delivery: "", trail: "", impact: "" }
    }),
    "mk1.json": format({ peakLevel: 1, damage: 1, art: { side: "", mounted: "" } }),
    "mk2.json": format({ peakLevel: 25, damage: 2, art: { side: "", mounted: "" } }),
    "mk3.json": format({ peakLevel: 50, damage: 3, art: { side: "", mounted: "" } })
  };
}

async function api(url, options = {}) {
  const response = await fetch(url, {
    ...options,
    headers: {
      "Content-Type": "application/json",
      ...(options.method && options.method !== "GET" ? { "X-Item-Maker-Token": mutationToken } : {}),
      ...(options.headers || {})
    }
  });
  const body = await response.json().catch(() => ({}));
  if (!response.ok) throw new Error(body.error || `${response.status} ${response.statusText}`);
  return body;
}

function updateSaveState() {
  if (saving) {
    elements.saveButton.disabled = true;
    elements.saveButton.textContent = "Saving…";
    elements.saveStatus.textContent = "Saving…";
    elements.saveStatus.className = "status-pill";
    return;
  }

  elements.saveButton.textContent = "Save Weapon";
  elements.saveButton.disabled = !helperOnline || (!!loadedIdentity && !dirty);
  if (dirty) {
    elements.saveStatus.textContent = "Unsaved changes";
    elements.saveStatus.className = "status-pill warn";
  } else if (loadedIdentity) {
    elements.saveStatus.textContent = "Saved";
    elements.saveStatus.className = "status-pill good";
  } else {
    elements.saveStatus.textContent = "Not saved";
    elements.saveStatus.className = "status-pill";
  }
}

function setDirty(value = true) {
  dirty = value;
  updateSaveState();
}
function captureActiveFile() { files[activeFile] = elements.jsonEditor.value; }
function parseFiles() {
  captureActiveFile();
  const parsed = {};
  const errors = [];
  fileNames.forEach(name => {
    try {
      const value = JSON.parse(files[name]);
      if (!value || typeof value !== "object" || Array.isArray(value)) throw new Error("root must be an object");
      parsed[name] = value;
    } catch (error) { errors.push(`${name}: ${error.message}`); }
  });
  return { parsed, errors };
}

function isObject(value) { return value && typeof value === "object" && !Array.isArray(value); }
function positive(value) { return typeof value === "number" && Number.isFinite(value) && value > 0; }
function nonNegative(value) { return typeof value === "number" && Number.isFinite(value) && value >= 0; }
function requiredText(value, label, errors) {
  if (typeof value !== "string" || !value.trim()) errors.push(`${label} is required.`);
}
function validateBrowserData(result) {
  if (result.errors.length) return;
  const shared = result.parsed["weapon.json"];
  const marks = [1, 2, 3].map(mark => result.parsed[`mk${mark}.json`]);

  requiredText(shared.name, "Weapon name", result.errors);
  requiredText(shared.category, "Weapon category", result.errors);
  requiredText(shared.rarity, "Rarity", result.errors);
  requiredText(shared.projectileType, "Projectile type", result.errors);
  requiredText(shared.damageType, "Damage type", result.errors);
  if (shared.category !== elements.categoryInput.value.trim()) {
    result.errors.push("The selected weapon type and saved category folder do not match.");
  }
  if (shared.category === "special") {
    result.errors.push("Special weapons are not implemented yet. Choose a supported weapon type.");
  }

  if (!isObject(shared.fire) || !positive(shared.fire.rate)) {
    result.errors.push("Cycles per second must be positive.");
  } else if (shared.fire.mode === "burst") {
    if (!Number.isInteger(shared.fire.shotsPerBurst) || shared.fire.shotsPerBurst < 2) {
      result.errors.push("Burst requires at least two shots.");
    }
    if (!positive(shared.fire.secondsBetweenShots)) {
      result.errors.push("Burst shot gap must be positive.");
    } else if (Number.isInteger(shared.fire.shotsPerBurst)
      && (1 / shared.fire.rate) <= (shared.fire.shotsPerBurst - 1) * shared.fire.secondsBetweenShots) {
      result.errors.push("Burst timing leaves no recovery time after the final shot.");
    }
  }

  if (!isObject(shared.shot)
    || !Number.isInteger(shared.shot.projectiles)
    || shared.shot.projectiles < 1
    || !nonNegative(shared.shot.spread)) {
    result.errors.push("Shot count must be a positive whole number and spread cannot be negative.");
  }

  if (shared.projectileType === "beam") {
    if (!isObject(shared.beam) || !positive(shared.beam.range) || !positive(shared.beam.width)) {
      result.errors.push("Beam range and width must be positive.");
    }
  } else if (!isObject(shared.projectile)
    || !positive(shared.projectile.speed)
    || !positive(shared.projectile.radius)
    || !positive(shared.projectile.range)) {
    result.errors.push("Projectile speed, radius, and range must be positive.");
  }

  if (!isObject(shared.impact)
    || !Number.isInteger(shared.impact.pierce)
    || shared.impact.pierce < 0
    || !nonNegative(shared.impact.ricochet)
    || !nonNegative(shared.impact.knockback)) {
    result.errors.push("Pierce must be a non-negative whole number; ricochet and knockback cannot be negative.");
  }

  if (!isObject(shared.art)) {
    result.errors.push("Shared projectile art settings are required.");
  } else {
    requiredText(shared.art.delivery, "Projectile / beam art", result.errors);
    requiredText(shared.art.trail, "Trail art", result.errors);
    requiredText(shared.art.impact, "Impact art", result.errors);
  }

  marks.forEach((mark, index) => {
    const label = `MK${index + 1}`;
    if (!Number.isInteger(mark.peakLevel) || mark.peakLevel < 1) result.errors.push(`${label} drop level must be a positive whole number.`);
    if (!positive(mark.damage)) result.errors.push(`${label} damage must be positive.`);
    if (!isObject(mark.art)) result.errors.push(`${label} art settings are required.`);
    else {
      requiredText(mark.art.side, `${label} side art`, result.errors);
      requiredText(mark.art.mounted, `${label} mounted art`, result.errors);
    }

    if (isObject(mark.dot)) {
      if (!positive(mark.dot.damagePerSecond)
        || !positive(mark.dot.duration)
        || !positive(mark.dot.ticksPerSecond)
        || !Number.isInteger(mark.dot.maxStacks)
        || mark.dot.maxStacks < 1) {
        result.errors.push(`${label} damage-over-time values must be positive, with whole-number stacks.`);
      }
    }

    if (isObject(mark.explosion)
      && (!positive(mark.explosion.radius)
        || !nonNegative(mark.explosion.edgeDamageMultiplier)
        || mark.explosion.edgeDamageMultiplier > 1)) {
      result.errors.push(`${label} explosion radius must be positive and outer-edge damage must be between 0 and 1.`);
    }
  });
}

function showChecks(errors, successText = "Browser checks passed. Save runs the final repository validator.") {
  elements.checks.innerHTML = errors.length
    ? errors.map(error => `<div class="issue error">⛔ ${escapeHtml(error)}</div>`).join("")
    : `<div class="issue ok">✓ ${escapeHtml(successText)}</div>`;
}

function localChecks() {
  const category = elements.categoryInput.value.trim();
  const folder = elements.folderInput.value.trim();
  const result = parseFiles();
  if (!/^[a-z0-9]+(?:[-_][a-z0-9]+)*$/.test(category)) result.errors.push("The selected weapon type produced an invalid category folder.");
  if (!/^[a-z0-9]+(?:_[a-z0-9]+)*$/.test(folder)) result.errors.push("Weapon key must use lowercase letters, digits, and underscores only.");
  if (loadedIdentity && (loadedIdentity.category !== category || loadedIdentity.folder !== folder)) {
    result.errors.push("A saved weapon cannot silently change its folder identity. Use New Weapon for a new identity.");
  }
  validateBrowserData(result);
  showChecks(result.errors);
  return result;
}

function render() {
  elements.fileTabs.innerHTML = fileNames.map(name =>
    `<button type="button" data-file="${name}" class="${name === activeFile ? "active" : ""}">${name}</button>`).join("");
  elements.fileTabs.querySelectorAll("[data-file]").forEach(button => button.addEventListener("click", () => {
    captureActiveFile();
    activeFile = button.dataset.file;
    render();
  }));
  elements.jsonEditor.value = files[activeFile];
  renderIdentity();
  localChecks();
}

function renderIdentity() {
  const category = elements.categoryInput.value.trim() || "category";
  const folder = elements.folderInput.value.trim() || "weapon";
  elements.sourcePath.textContent = `Content/Weapons/${category}/${folder}/`;
  elements.generatedIds.innerHTML = [1, 2, 3].map(mark =>
    `<div class="stat-row"><span>MK${mark}</span><strong class="mono generated-id">gun_${escapeHtml(folder)}_mk${mark}_01</strong></div>`).join("");
}

function weaponTypeLabel(item) {
  const projectile = String(item.projectileType || "").toLowerCase();
  const category = String(item.category || "").toLowerCase();
  if (projectile === "beam") return "Beam";
  if (projectile === "rocket") return "Rocket";
  if (projectile === "orb") return "Orb";
  if (category.includes("shotgun") || category.includes("spread")) return "Shotgun";
  if (category === "special") return "Special";
  return "Blaster";
}

async function enrichWeapon(item) {
  try {
    const detail = await api(`/api/weapon-folder?category=${encodeURIComponent(item.category)}&folder=${encodeURIComponent(item.folder)}`);
    const shared = detail.files["weapon.json"] || {};
    const levels = [1, 2, 3].map(mark => Number(detail.files[`mk${mark}.json`]?.peakLevel)).filter(Number.isFinite);
    return {
      ...item,
      rarity: String(shared.rarity || "common").toLowerCase(),
      projectileType: shared.projectileType || "",
      levels
    };
  } catch (_) {
    return { ...item, rarity: "", projectileType: "", levels: [] };
  }
}

function renderWeaponList() {
  const query = String(elements.weaponSearchInput?.value || "").trim().toLowerCase();
  const filtered = weaponCatalogue.filter(item => {
    const search = [
      item.name,
      item.category,
      item.folder,
      item.rarity,
      weaponTypeLabel(item),
      ...(item.levels || [])
    ].join(" ").toLowerCase();
    return !query || search.includes(query);
  });

  elements.weaponList.innerHTML = filtered.length
    ? filtered.map(item => {
      const selected = loadedIdentity
        && loadedIdentity.category === item.category
        && loadedIdentity.folder === item.folder;
      const rarity = item.rarity ? item.rarity.toUpperCase() : "UNKNOWN";
      const levels = item.levels?.length ? item.levels.join(" / ") : "levels unavailable";
      return `<button type="button" class="weapon-entry${selected ? " active" : ""}" data-category="${escapeHtml(item.category)}" data-folder="${escapeHtml(item.folder)}">
        <span class="weapon-entry-title">${escapeHtml(item.name)}</span>
        <span class="weapon-entry-meta"><b class="rarity-${escapeHtml(item.rarity || "unknown")}">${escapeHtml(rarity)}</b> · ${escapeHtml(weaponTypeLabel(item))} · ${escapeHtml(levels)}</span>
        <small>${escapeHtml(item.category)}/${escapeHtml(item.folder)}</small>
      </button>`;
    }).join("")
    : `<div class="help">${weaponCatalogue.length ? "No weapons match this search." : "No weapon folders yet."}</div>`;

  elements.weaponList.querySelectorAll(".weapon-entry").forEach(button => button.addEventListener("click", async () => {
    if (dirty && !confirm("Discard unsaved changes?")) return;
    button.disabled = true;
    try {
      await loadWeapon(button.dataset.category, button.dataset.folder);
    } catch (error) {
      showChecks([error.message]);
    } finally {
      button.disabled = false;
    }
  }));
}

async function refreshList() {
  const list = await api("/api/weapon-folders");
  weaponCatalogue = await Promise.all(list.weapons.map(enrichWeapon));
  renderWeaponList();
}

async function loadWeapon(category, folder) {
  const result = await api(`/api/weapon-folder?category=${encodeURIComponent(category)}&folder=${encodeURIComponent(folder)}`);
  fileNames.forEach(name => { files[name] = format(result.files[name]); });
  elements.categoryInput.value = category;
  elements.folderInput.value = folder;
  loadedIdentity = { category, folder };
  activeFile = "weapon.json";
  setDirty(false);
  render();
  renderWeaponList();
}

function newWeapon() {
  if (dirty && !confirm("Discard unsaved changes?")) return;
  files = makeDefaultFiles();
  elements.categoryInput.value = "normal-firearm";
  elements.folderInput.value = "new_weapon";
  loadedIdentity = null;
  activeFile = "weapon.json";
  setDirty(true);
  render();
  renderWeaponList();
  queueMicrotask(() => document.querySelector('[data-g-key="shared.name"]')?.focus());
}

async function saveWeapon() {
  if (saving) return;
  const check = localChecks();
  if (check.errors.length) {
    elements.saveStatus.textContent = "Fix weapon checks";
    elements.saveStatus.className = "status-pill bad";
    elements.checks.scrollIntoView({ behavior: "smooth", block: "nearest" });
    return;
  }

  saving = true;
  updateSaveState();
  elements.checks.innerHTML = `<div class="issue">Saving and running the repository validator…</div>`;
  try {
    const result = await api("/api/weapon-folder", {
      method: "PUT",
      body: JSON.stringify({
        category: elements.categoryInput.value.trim(),
        folder: elements.folderInput.value.trim(),
        files: check.parsed
      })
    });
    loadedIdentity = { category: elements.categoryInput.value.trim(), folder: elements.folderInput.value.trim() };
    fileNames.forEach(name => { files[name] = format(check.parsed[name]); });
    dirty = false;
    render();
    await refreshList();
    showChecks([], result.validation);
  } catch (error) {
    elements.saveStatus.textContent = "Save failed";
    elements.saveStatus.className = "status-pill bad";
    showChecks([error.message]);
  } finally {
    saving = false;
    updateSaveState();
  }
}

elements.newWeaponButton.addEventListener("click", newWeapon);
elements.saveButton.addEventListener("click", saveWeapon);
elements.weaponSearchInput?.addEventListener("input", renderWeaponList);
elements.jsonEditor.addEventListener("input", () => { files[activeFile] = elements.jsonEditor.value; setDirty(); localChecks(); });
window.addEventListener("beforeunload", event => { if (!dirty) return; event.preventDefault(); event.returnValue = ""; });
window.addEventListener("keydown", event => {
  if (!(event.ctrlKey || event.metaKey) || event.key.toLowerCase() !== "s") return;
  event.preventDefault();
  saveWeapon();
});

(async function start() {
  try {
    const status = await api("/api/status");
    mutationToken = status.mutationToken;
    elements.repoStatus.textContent = `${status.branch} · ${status.clean ? "clean" : `${status.changed} changed`}`;
    elements.repoStatus.className = `repo-state ${status.clean ? "good" : "warn"}`;
    await refreshList();
  } catch (error) {
    helperOnline = false;
    elements.repoStatus.textContent = "Offline: start the local Item Maker helper.";
    elements.repoStatus.className = "repo-state warn";
    showChecks([error.message]);
  }
  updateSaveState();
  render();
})();
