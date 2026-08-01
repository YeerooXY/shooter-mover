"use strict";

const fileNames = ["weapon.json", "mk1.json", "mk2.json", "mk3.json"];
const elements = {
  newWeaponButton: document.querySelector("#newWeaponButton"),
  saveButton: document.querySelector("#saveButton"),
  saveStatus: document.querySelector("#saveStatus"),
  repoStatus: document.querySelector("#repoStatus"),
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
let loadedIdentity = null;
let files = makeDefaultFiles();

function format(value) { return JSON.stringify(value, null, 2) + "\n"; }
function escapeHtml(value) {
  return String(value ?? "").replace(/&/g, "&amp;").replace(/</g, "&lt;")
    .replace(/>/g, "&gt;").replace(/"/g, "&quot;").replace(/'/g, "&#039;");
}
function folderSlug(value) {
  return String(value || "").toLowerCase().trim().replace(/[^a-z0-9]+/g, "_").replace(/^_+|_+$/g, "");
}
function categorySlug(value) {
  return String(value || "").toLowerCase().trim().replace(/[^a-z0-9_-]+/g, "-").replace(/^[-_]+|[-_]+$/g, "");
}
function makeDefaultFiles() {
  return {
    "weapon.json": format({
      name: "New Weapon",
      description: "",
      category: "orb",
      rarity: "common",
      projectileType: "orb",
      damageType: "physical",
      fire: { mode: "automatic", rate: 1 },
      shot: { projectiles: 1, spread: 0 },
      projectile: { speed: 10, radius: 0.2, range: 25 },
      impact: { pierce: 1, ricochet: 0, knockback: 0 },
      art: { mounted: "gun_new_weapon_mounted", delivery: "gun_new_weapon_projectile", trail: "gun_new_weapon_trail", impact: "gun_new_weapon_impact" }
    }),
    "mk1.json": format({ peakLevel: 1, damage: 1, art: { side: "gun_new_weapon_mk1_side" } }),
    "mk2.json": format({ peakLevel: 25, damage: 2, art: { side: "gun_new_weapon_mk2_side" } }),
    "mk3.json": format({ peakLevel: 50, damage: 3, art: { side: "gun_new_weapon_mk3_side" } })
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

function setDirty(value = true) {
  dirty = value;
  elements.saveStatus.textContent = value ? "Unsaved changes" : "Saved";
  elements.saveStatus.className = "status-pill " + (value ? "warn" : "good");
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

function localChecks() {
  const category = elements.categoryInput.value.trim();
  const folder = elements.folderInput.value.trim();
  const result = parseFiles();
  if (!/^[a-z0-9]+(?:[-_][a-z0-9]+)*$/.test(category)) result.errors.push("Category folder must be lowercase and filesystem-safe.");
  if (!/^[a-z0-9]+(?:_[a-z0-9]+)*$/.test(folder)) result.errors.push("Weapon folder must use lowercase letters, digits, and underscores only.");
  if (loadedIdentity && (loadedIdentity.category !== category || loadedIdentity.folder !== folder)) {
    result.errors.push("Changing a loaded folder identity would create a second folder. Use New Weapon for a new identity.");
  }
  elements.checks.innerHTML = result.errors.length
    ? result.errors.map(error => `<div class="issue error">⛔ ${escapeHtml(error)}</div>`).join("")
    : `<div class="issue ok">✓ JSON parses locally. Repository save will run the full folder validator.</div>`;
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

async function refreshList() {
  const list = await api("/api/weapon-folders");
  elements.weaponList.innerHTML = list.weapons.length
    ? list.weapons.map(item => `<button type="button" class="weapon-entry" data-category="${escapeHtml(item.category)}" data-folder="${escapeHtml(item.folder)}">${escapeHtml(item.name)}<small>${escapeHtml(item.category)}/${escapeHtml(item.folder)}</small></button>`).join("")
    : `<div class="help">No split weapon folders yet.</div>`;
  elements.weaponList.querySelectorAll(".weapon-entry").forEach(button => button.addEventListener("click", async () => {
    if (dirty && !confirm("Discard unsaved changes?")) return;
    try { await loadWeapon(button.dataset.category, button.dataset.folder); }
    catch (error) { alert(error.message); }
  }));
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
}

function newWeapon() {
  if (dirty && !confirm("Discard unsaved changes?")) return;
  files = makeDefaultFiles();
  elements.categoryInput.value = "orb";
  elements.folderInput.value = "new_weapon";
  loadedIdentity = null;
  activeFile = "weapon.json";
  setDirty();
  render();
  elements.folderInput.focus();
}

async function saveWeapon() {
  const check = localChecks();
  if (check.errors.length) { alert("Fix the listed problems before saving."); return; }
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
    setDirty(false);
    render();
    await refreshList();
    elements.checks.innerHTML = `<div class="issue ok">✓ ${escapeHtml(result.validation)}</div>`;
  } catch (error) { alert(error.message); }
}

elements.newWeaponButton.addEventListener("click", newWeapon);
elements.saveButton.addEventListener("click", saveWeapon);
elements.jsonEditor.addEventListener("input", () => { files[activeFile] = elements.jsonEditor.value; setDirty(); localChecks(); });
elements.categoryInput.addEventListener("input", () => {
  elements.categoryInput.value = categorySlug(elements.categoryInput.value);
  setDirty(); renderIdentity(); localChecks();
});
elements.folderInput.addEventListener("input", () => {
  elements.folderInput.value = folderSlug(elements.folderInput.value);
  setDirty(); renderIdentity(); localChecks();
});
window.addEventListener("beforeunload", event => { if (!dirty) return; event.preventDefault(); event.returnValue = ""; });

(async function start() {
  try {
    const status = await api("/api/status");
    mutationToken = status.mutationToken;
    elements.repoStatus.textContent = `${status.branch} · ${status.clean ? "clean" : `${status.changed} changed`}`;
    elements.repoStatus.className = `repo-state ${status.clean ? "good" : "warn"}`;
    await refreshList();
  } catch (error) {
    elements.repoStatus.textContent = "Offline: start the local Item Maker helper.";
    elements.repoStatus.className = "repo-state warn";
    elements.saveButton.disabled = true;
  }
  render();
})();
