function validateItem() {
  const errors = [];
  const warnings = [];
  if (!state.name.trim()) errors.push("Item Name is required.");
  if (!/^[a-z0-9]+(?:-[a-z0-9]+)*$/.test(state.id)) errors.push("Item ID must use lowercase letters, numbers, and single hyphens.");
  if (!state.intendedUse.trim()) warnings.push("Intended Use is empty.");

  Object.entries(state.marks).forEach(([markId, mark]) => {
    const label = markId.toUpperCase();
    if (mark.dropLevel < 1) errors.push(`${label}: Drop Level must be at least 1.`);
    if (mark.dropWeight <= 0) errors.push(`${label}: Drop Weight must be greater than zero.`);
    if (!mark.available) warnings.push(`${label}: This mark is unavailable.`);
    if (!mark.available) return;

    if (state.kind === "gear") {
      if (!mark.slot) errors.push(`${label}: Gear Slot is required.`);
      if (!mark.art.side.trim()) errors.push(`${label}: Available gear requires Inventory Side art.`);
      const seen = new Set();
      mark.bonuses.forEach(bonus => {
        if (seen.has(bonus.stat)) errors.push(`${label}: Bonus stat ${bonus.stat} is listed more than once.`);
        seen.add(bonus.stat);
      });
      if (mark.special.code && !mark.special.notes.trim()) warnings.push(`${label}: Special Code has no notes.`);
      return;
    }

    if (mark.damage.amount <= 0) errors.push(`${label}: Damage must be greater than zero.`);
    if (mark.shot.projectiles < 1) errors.push(`${label}: Projectiles must be at least 1.`);
    if (mark.fire.mode === "semi" || mark.fire.mode === "automatic" || mark.fire.mode === "burst") {
      if (mark.fire.wavesPerSecond <= 0) errors.push(`${label}: Waves / Second must be greater than zero.`);
    }
    if (mark.fire.mode === "burst") {
      if (mark.fire.wavesPerBurst < 1) errors.push(`${label}: Waves per Burst must be at least 1.`);
      if (mark.fire.secondsBetweenWaves < 0) errors.push(`${label}: Time Between Waves cannot be negative.`);
      if (calculateBurst(mark).wait < 0) errors.push(`${label}: Burst waves take longer than the complete firing cycle.`);
    }
    if (mark.fire.mode === "charge") {
      if (mark.fire.fullChargeSeconds <= 0) errors.push(`${label}: Full Charge Time must be greater than zero.`);
      if (mark.fire.fullChargeDamage <= 0) errors.push(`${label}: Full Charge Damage must be greater than zero.`);
      warnings.push(`${label}: Charge gameplay is not implemented yet.`);
    }
    if (mark.delivery.type !== "special" && mark.delivery.range <= 0) errors.push(`${label}: Range must be greater than zero.`);
    if (["normal", "orb", "rocket"].includes(mark.delivery.type)) {
      if (mark.delivery.speed <= 0) errors.push(`${label}: Shot Speed must be greater than zero.`);
      if (mark.delivery.radius <= 0) errors.push(`${label}: Shot Radius must be greater than zero.`);
    }
    if (["orb", "rocket"].includes(mark.delivery.type) && mark.delivery.explosionRadius <= 0) errors.push(`${label}: Explosion Radius must be greater than zero.`);
    if (mark.delivery.type === "laser" && mark.delivery.beamWidth <= 0) errors.push(`${label}: Beam Width must be greater than zero.`);
    if (mark.delivery.type === "special" && !mark.special.code.trim()) errors.push(`${label}: Available Special delivery requires a Special Code.`);
    if (mark.homing.enabled) {
      if (mark.homing.turnSpeed <= 0) errors.push(`${label}: Homing Turn Speed must be greater than zero.`);
      if (mark.homing.findRange <= 0) errors.push(`${label}: Homing Find Range must be greater than zero.`);
    }
    if (mark.impact.pierce < 0) errors.push(`${label}: Pierce cannot be negative.`);
    if (mark.impact.ricochet < 0) errors.push(`${label}: Ricochet cannot be negative.`);
    if ((mark.damage.dotDamage > 0) !== (mark.damage.dotSeconds > 0)) errors.push(`${label}: DoT Damage and Duration must either both be zero or both be greater than zero.`);
    if (!mark.art.side.trim()) errors.push(`${label}: Available gun requires Inventory Side art.`);
    if (!mark.art.mounted.trim()) errors.push(`${label}: Available gun requires Mounted Top-Down art.`);
    if (mark.delivery.type !== "special" && !mark.art.projectile.trim()) errors.push(`${label}: Available gun requires Projectile or Beam art.`);
    if (mark.delivery.type !== "special" && mark.special.code && !mark.special.notes.trim()) warnings.push(`${label}: Special Code has no notes.`);
    if (mark.damage.movement < -50) warnings.push(`${label}: Movement is lower than -50%.`);
    if (mark.shot.projectiles > 30) warnings.push(`${label}: Projectile count is unusually high.`);
  });
  return { errors, warnings };
}

function renderChecks() {
  const result = validateItem();
  const items = [];
  if (result.errors.length === 0) items.push(`<div class="issue ok">✓ No blocking problems</div>`);
  result.errors.forEach(message => items.push(`<div class="issue error">⛔ ${escapeHtml(message)}</div>`));
  result.warnings.forEach(message => items.push(`<div class="issue warning">⚠ ${escapeHtml(message)}</div>`));
  elements.checksPanel.innerHTML = items.join("");
  if (result.errors.length > 0) {
    elements.saveStatus.className = "status-pill bad";
    elements.saveStatus.textContent = `${result.errors.length} problem${result.errors.length === 1 ? "" : "s"}`;
  } else if (dirty) {
    elements.saveStatus.className = "status-pill warn";
    elements.saveStatus.textContent = "Unsaved changes";
  }
}

function cleanPackage() {
  return clone(state);
}

function switchKind(kind) {
  if (state.kind === kind) return;
  if (dirty && !confirm("Replace the current item with a new " + kind + "?")) return;
  state = makeItem(kind);
  idTracksName = true;
  activeMark = "mk1";
  Object.keys(previews).forEach(key => delete previews[key]);
  setDirty();
  render();
}

function newItem(kind) {
  if (dirty && !confirm("Discard the current item and create a new " + kind + "?")) return;
  state = makeItem(kind);
  idTracksName = true;
  activeMark = "mk1";
  Object.keys(previews).forEach(key => delete previews[key]);
  setDirty();
  render();
  elements.itemName.focus();
}

function normalizeImportedItem(raw) {
  if (!raw || (raw.kind !== "gun" && raw.kind !== "gear")) throw new Error("Package kind must be gun or gear.");
  const base = makeItem(raw.kind);
  base.id = String(raw.id || "");
  base.name = String(raw.name || "");
  base.intendedUse = String(raw.intendedUse || "");
  ["mk1", "mk2", "mk3"].forEach(markId => {
    if (!raw.marks || !raw.marks[markId]) throw new Error(`Package is missing ${markId.toUpperCase()}.`);
    base.marks[markId] = mergeShape(base.marks[markId], raw.marks[markId]);
  });
  return base;
}

function mergeShape(base, incoming) {
  if (Array.isArray(base)) return Array.isArray(incoming) ? clone(incoming) : clone(base);
  if (base && typeof base === "object") {
    const result = {};
    Object.keys(base).forEach(key => { result[key] = mergeShape(base[key], incoming ? incoming[key] : undefined); });
    return result;
  }
  return incoming === undefined ? base : incoming;
}

async function importFile(file) {
  const text = await file.text();
  const parsed = JSON.parse(text);
  state = normalizeImportedItem(parsed);
  idTracksName = false;
  activeMark = "mk1";
  Object.keys(previews).forEach(key => delete previews[key]);
  setDirty(false);
  render();
}

function exportPackage() {
  const result = validateItem();
  if (result.errors.length > 0) {
    alert("Fix the blocking problems before export. Unfinished marks can be exported by turning Available off.");
    return;
  }
  const json = JSON.stringify(cleanPackage(), null, 2) + "\n";
  const blob = new Blob([json], { type: "application/json" });
  const url = URL.createObjectURL(blob);
  const anchor = document.createElement("a");
  anchor.href = url;
  anchor.download = `${state.id}.item.json`;
  document.body.appendChild(anchor);
  anchor.click();
  anchor.remove();
  URL.revokeObjectURL(url);
  setDirty(false);
  renderChecks();
}
