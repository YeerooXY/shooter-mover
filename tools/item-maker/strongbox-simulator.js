"use strict";

const statusElement = document.querySelector("#simStatus");
const levelInput = document.querySelector("#simLevel");
const rollButton = document.querySelector("#rollButton");
const rollGrid = document.querySelector("#rollGrid");
let weaponFamilies = [];

function escapeHtml(value) {
  return String(value ?? "").replace(/&/g, "&amp;").replace(/</g, "&lt;")
    .replace(/>/g, "&gt;").replace(/\"/g, "&quot;").replace(/'/g, "&#039;");
}
function mergeObjects(shared, mark) {
  const result = JSON.parse(JSON.stringify(shared || {}));
  Object.entries(mark || {}).forEach(([key, value]) => {
    if (value && typeof value === "object" && !Array.isArray(value) && result[key] && typeof result[key] === "object" && !Array.isArray(result[key])) {
      result[key] = mergeObjects(result[key], value);
    } else result[key] = value;
  });
  return result;
}
async function api(path) {
  const response = await fetch(path);
  const body = await response.json().catch(() => ({}));
  if (!response.ok) throw new Error(body.error || `${response.status} ${response.statusText}`);
  return body;
}
function selectedDefinition(family, level) {
  const available = family.definitions.filter(definition => Number(definition.peakLevel) <= level);
  if (available.length) return available.sort((a, b) => Number(b.peakLevel) - Number(a.peakLevel))[0];
  return family.definitions.slice().sort((a, b) => Number(a.peakLevel) - Number(b.peakLevel))[0];
}
function rawDps(definition) {
  const rate = Number(definition.fire?.rate || 0);
  const burst = definition.fire?.mode === "burst" ? Number(definition.fire?.shotsPerBurst || 1) : 1;
  const projectiles = Number(definition.shot?.projectiles || 1);
  return Number(definition.damage || 0) * rate * burst * projectiles;
}
function renderCard(family, definition, slot) {
  return `<article class="sim-card">
    <div class="meta">Offer ${slot} · ${escapeHtml(definition.rarity || "common")}</div>
    <h2>${escapeHtml(definition.name || family.name)} MK${escapeHtml(definition.mark)}</h2>
    <div class="meta">Peak level ${escapeHtml(definition.peakLevel)} · ${escapeHtml(definition.projectileType)} · ${escapeHtml(definition.damageType)}</div>
    <div class="sim-stats">
      <span>Damage</span><strong>${escapeHtml(definition.damage)}</strong>
      <span>Rate</span><strong>${escapeHtml(definition.fire?.rate || 0)}/s</strong>
      <span>Projectiles</span><strong>${escapeHtml(definition.shot?.projectiles || 1)}</strong>
      <span>Raw DPS</span><strong>${escapeHtml(Number(rawDps(definition).toFixed(2)))}</strong>
      <span>Pierce</span><strong>${escapeHtml(definition.impact?.pierce || 0)}</strong>
      <span>Range</span><strong>${escapeHtml(definition.projectile?.range || definition.beam?.range || "—")}</strong>
    </div>
  </article>`;
}
function roll() {
  if (!weaponFamilies.length) return;
  const level = Math.max(1, Number(levelInput.value) || 1);
  const cards = [];
  for (let slot = 1; slot <= 6; slot += 1) {
    const family = weaponFamilies[Math.floor(Math.random() * weaponFamilies.length)];
    cards.push(renderCard(family, selectedDefinition(family, level), slot));
  }
  rollGrid.innerHTML = cards.join("");
}

rollButton.addEventListener("click", roll);

(async function start() {
  try {
    const list = await api("/api/weapon-folders");
    weaponFamilies = await Promise.all(list.weapons.map(async item => {
      const folder = await api(`/api/weapon-folder?category=${encodeURIComponent(item.category)}&folder=${encodeURIComponent(item.folder)}`);
      const shared = folder.files["weapon.json"];
      return {
        name: shared.name || item.name,
        definitions: [1, 2, 3].map(mark => ({ ...mergeObjects(shared, folder.files[`mk${mark}.json`]), mark }))
      };
    }));
    statusElement.textContent = `${weaponFamilies.length} weapon families loaded`;
    statusElement.className = "status-pill good";
    if (weaponFamilies.length) roll();
    else rollGrid.innerHTML = `<div class="sim-empty">No split weapon folders were found.</div>`;
  } catch (error) {
    statusElement.textContent = "Offline";
    statusElement.className = "status-pill warn";
    rollGrid.innerHTML = `<div class="sim-empty">Start the local Item Maker helper to load the current weapon folders.<br><br>${escapeHtml(error.message)}</div>`;
    rollButton.disabled = true;
  }
})();
