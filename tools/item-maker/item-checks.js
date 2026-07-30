"use strict";

function validateItem() {
  const errors = [], warnings = [];
  if (!state.name.trim()) errors.push("Name is required.");
  if (!/^[a-z0-9]+(?:-[a-z0-9]+)*$/.test(state.id)) errors.push("ID must use lowercase letters or digits separated by single hyphens.");
  if (!state.intendedUse.trim()) warnings.push("Intended use is empty.");
  if (!Array.isArray(state.marks) || state.marks.length !== 3 || state.marks.some((mark, index) => mark.mark !== index + 1)) {
    errors.push("A package must contain MK1, MK2, and MK3 exactly once and in order.");
    return { errors, warnings };
  }
  state.marks.forEach((mark, index) => {
    const label = `MK${index + 1}`;
    if (mark.peakDropLevel < 1) errors.push(`${label}: Peak drop level must be at least 1.`);
    if (mark.craftLevel < 1) errors.push(`${label}: Craft level must be at least 1.`);
    if (mark.dropWeight <= 0) errors.push(`${label}: Drop weight must be greater than zero.`);
    if (mark.minimumBoxTier < 1 || mark.minimumBoxTier > 11) errors.push(`${label}: Box tier must be 1–11.`);
    if (mark.maxAugmentSlots < 0 || mark.maxAugmentSlots > 10) errors.push(`${label}: Augment slots must be 0–10.`);
    if (index && mark.peakDropLevel - state.marks[index - 1].peakDropLevel < 20) warnings.push(`${label}: Peak level is less than 20 levels after the previous mark.`);
    if (!mark.available) warnings.push(`${label}: Unavailable; it will not enter loot.`);
  });
  if (state.kind === "gun-family") validateGun(errors, warnings);
  else if (state.kind === "gear-set") validateGear(errors, warnings);
  else errors.push("Package kind is not supported.");
  return { errors, warnings };
}

function validateGun(errors, warnings) {
  if (!rarityOptions.includes(state.rarity)) errors.push("Gun rarity is invalid.");
  if (!damageTypes.includes(state.damageType)) errors.push("Gun damage type is invalid.");
  if (!gunCategories.includes(state.category)) errors.push("Gun category is invalid.");
  if (!fireModes.includes(state.fire.mode)) errors.push("Fire mode is invalid.");
  if (!shotKinds.includes(state.shot.kind)) errors.push("Shot pattern is invalid.");
  if (!deliveryTypes.includes(state.delivery.type)) errors.push("Delivery type is invalid.");
  if (state.fire.cyclesPerSecond <= 0) errors.push("Fire cycles per second must be positive.");
  if (state.fire.mode === "burst") {
    if (!Number.isInteger(state.fire.shotsPerBurst) || state.fire.shotsPerBurst < 2) errors.push("Burst shots must be an integer of at least 2.");
    if (state.fire.secondsBetweenShots <= 0) errors.push("Burst shot interval must be positive.");
    if (calculateBurst(state.fire).recovery < 0) errors.push("Burst shots do not fit inside the configured cycle.");
  }
  if (!Number.isInteger(state.shot.projectiles) || state.shot.projectiles < 1) errors.push("Projectiles must be a positive integer.");
  if (!Number.isInteger(state.shot.pulses) || state.shot.pulses < 1) errors.push("Pulses must be a positive integer.");
  if (state.shot.spreadDegrees < 0 || state.shot.randomnessDegrees < 0) errors.push("Spread values cannot be negative.");
  if (state.delivery.type !== "special" && state.delivery.range <= 0) errors.push("Range must be positive.");
  if (["normal", "orb", "rocket"].includes(state.delivery.type) && (state.delivery.speed <= 0 || state.delivery.radius <= 0)) errors.push("Projectile delivery requires positive speed and radius.");
  if (state.delivery.type === "laser" && state.delivery.beamWidth <= 0) errors.push("Laser delivery requires positive beam width.");
  if (state.guidance.mode === "homing" && (state.guidance.acquisitionRange <= 0 || state.guidance.turnRateDegreesPerSecond <= 0)) errors.push("Homing requires positive range and turn rate.");
  if (state.impact.pierce < 0 || state.impact.ricochet < 0) errors.push("Pierce and ricochet cannot be negative.");
  if (state.effects.explosion.enabled && state.effects.explosion.radius <= 0) errors.push("Enabled explosion requires a radius.");
  if (state.effects.damageOverTime.enabled && (state.effects.damageOverTime.durationSeconds <= 0 || state.effects.damageOverTime.ticksPerSecond <= 0)) errors.push("Enabled damage over time requires positive duration and tick rate.");
  if (state.effects.chain.enabled && (state.effects.chain.maximumTargets < 2 || state.effects.chain.acquisitionRange <= 0)) errors.push("Enabled chain arc requires at least two targets and positive range.");
  if (state.runtimeStatus === "runtime-pending" && !state.pendingFeature.id.trim()) errors.push("Runtime-pending guns require a feature ID.");
  state.marks.forEach(mark => {
    if (!mark.available) return;
    const label = `MK${mark.mark}`;
    if (mark.damage.direct <= 0 && mark.damage.area <= 0 && mark.damage.dotPerSecond <= 0) errors.push(`${label}: An available gun must deal damage.`);
    if (!mark.art.side.trim()) errors.push(`${label}: Inventory art reference is required.`);
    if (!mark.art.mounted.trim()) errors.push(`${label}: Mounted art reference is required.`);
    if (state.runtimeStatus === "runtime-pending") warnings.push(`${label}: Loot/inventory metadata is valid, but firing must fail explicitly.`);
  });
}

function validateGear(errors, warnings) {
  if (!rarityOptions.includes(state.rarity)) errors.push("Gear rarity is invalid.");
  state.marks.forEach(mark => gearSlots.forEach(slot => {
    const piece = mark.pieces?.[slot], label = `MK${mark.mark} ${title(slot)}`;
    if (!piece) { errors.push(`${label}: Piece is missing.`); return; }
    if (!mark.available) return;
    if (!piece.name.trim()) errors.push(`${label}: Name is required.`);
    if (!piece.art.trim()) errors.push(`${label}: Art reference is required.`);
    const seen = new Set();
    piece.modifiers.forEach(modifier => {
      const stat = gearStats.find(item => item.id === modifier.target);
      if (!stat) errors.push(`${label}: Unknown target ${modifier.target}.`);
      if (!modifierOperations.includes(modifier.operation)) errors.push(`${label}: Unknown modifier operation.`);
      if (seen.has(modifier.target)) errors.push(`${label}: ${modifier.target} is listed twice.`);
      seen.add(modifier.target);
      if (stat && !stat.live && !piece.pendingModules.includes(stat.id)) errors.push(`${label}: Pending stat ${stat.label} must be declared in pendingModules.`);
    });
    piece.pendingModules.forEach(id => {
      if (!gearStats.some(item => item.id === id && !item.live)) errors.push(`${label}: Unknown pending module ${id}.`);
    });
    if (piece.pendingModules.length) warnings.push(`${label}: Pending modules are metadata only.`);
  }));
}

function renderChecks() {
  const result = validateItem(), items = [];
  if (!result.errors.length) items.push(`<div class="issue ok">✓ No blocking problems</div>`);
  result.errors.forEach(message => items.push(`<div class="issue error">⛔ ${escapeHtml(message)}</div>`));
  result.warnings.forEach(message => items.push(`<div class="issue warning">⚠ ${escapeHtml(message)}</div>`));
  elements.checksPanel.innerHTML = items.join("");
  if (result.errors.length) {
    elements.saveStatus.className = "status-pill bad";
    elements.saveStatus.textContent = `${result.errors.length} problem${result.errors.length === 1 ? "" : "s"}`;
  } else if (dirty) {
    elements.saveStatus.className = "status-pill warn";
    elements.saveStatus.textContent = "Unsaved changes";
  }
}

function cleanPackage() { return clone(state); }
function normalizeImportedItem(raw) {
  if (!raw || ![SCHEMA_GUN, SCHEMA_GEAR].includes(raw.$schema)) throw new Error("Not a current Shooter Mover gun-family or gear-set package.");
  return mergeShape(raw.$schema === SCHEMA_GUN ? makeGun() : makeGear(), raw);
}
function mergeShape(base, incoming) {
  if (Array.isArray(base)) return Array.isArray(incoming) && incoming.length === base.length ? base.map((value, i) => mergeShape(value, incoming[i])) : clone(base);
  if (base && typeof base === "object") {
    const result = {};
    Object.keys(base).forEach(key => { result[key] = mergeShape(base[key], incoming?.[key]); });
    return result;
  }
  return incoming === undefined ? base : incoming;
}
async function importFile(file) {
  state = normalizeImportedItem(JSON.parse(await file.text()));
  idTracksName = false; activeMark = "mk1";
  Object.keys(previews).forEach(key => delete previews[key]);
  setDirty(false); render();
}
function exportPackage() {
  if (validateItem().errors.length) { alert("Fix blocking problems before export."); return; }
  const blob = new Blob([JSON.stringify(cleanPackage(), null, 2) + "\n"], { type: "application/json" });
  const url = URL.createObjectURL(blob), anchor = document.createElement("a");
  anchor.href = url; anchor.download = packageFileName(); document.body.appendChild(anchor);
  anchor.click(); anchor.remove(); URL.revokeObjectURL(url); setDirty(false); renderChecks();
}
