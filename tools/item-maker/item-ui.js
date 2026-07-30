"use strict";

function render() {
  elements.itemName.value = state.name;
  elements.itemId.value = state.id;
  elements.intendedUse.value = state.intendedUse;
  const isGun = state.kind === "gun-family";
  elements.gunKindButton.classList.toggle("active", isGun);
  elements.gearKindButton.classList.toggle("active", !isGun);
  elements.packageKind.textContent = isGun ? "Gun Family" : "Gear Set";
  elements.packageFile.textContent = packageFileName();
  elements.idExample.textContent = isGun
    ? `${state.id || "item"}.mk1`
    : `equipment.gear-${state.id || "set"}-headpiece-mk1`;
  elements.activeMarkId.textContent = `${state.id || "item"}.${activeMark}`;
  elements.copyPreviousButton.classList.toggle("hidden", activeMark === "mk1");
  document.querySelectorAll("[data-mark]").forEach(button =>
    button.classList.toggle("active", button.dataset.mark === activeMark));
  elements.markEditor.innerHTML = isGun ? renderGunEditor() : renderGearEditor();
  bindEditorInputs();
  renderPreview();
  renderCalculated();
  renderChecks();
  elements.jsonPreview.textContent = JSON.stringify(cleanPackage(), null, 2);
}

function packageFileName() {
  const suffix = state.kind === "gun-family" ? ".gun.json" : ".gear.json";
  return `${state.id || "item"}${suffix}`;
}

function renderProgression(mark) {
  return section("Mark and Drop", `<div class="form-grid">
    ${field("Peak Drop Level", "peakDropLevel", mark.peakDropLevel)}
    ${field("Craft Level", "craftLevel", mark.craftLevel)}
    ${field("Drop Weight", "dropWeight", mark.dropWeight)}
    ${field("Minimum Box Tier", "minimumBoxTier", mark.minimumBoxTier)}
    ${field("Maximum Augment Slots", "maxAugmentSlots", mark.maxAugmentSlots)}
    <div class="field"><label>Status</label>${checkboxField("Available", "available", mark.available)}</div>
  </div>`, true);
}

function renderGunEditor() {
  const mark = activeMarkValue();
  const burst = calculateBurst(state.fire);
  const fireFields = `<div class="form-grid">
    ${selectField("Mode", "family.fire.mode", state.fire.mode, fireModes)}
    ${field(state.fire.mode === "burst" ? "Bursts / Second" : "Cycles / Second", "family.fire.cyclesPerSecond", state.fire.cyclesPerSecond)}
    ${state.fire.mode === "burst" ? field("Shots per Burst", "family.fire.shotsPerBurst", state.fire.shotsPerBurst) : ""}
    ${state.fire.mode === "burst" ? field("Seconds Between Burst Shots", "family.fire.secondsBetweenShots", state.fire.secondsBetweenShots) : ""}
  </div>${state.fire.mode === "burst" ? `<div class="derived-box">
    <div class="derived-row"><span>Burst firing time</span><strong>${format(burst.firingTime)} s</strong></div>
    <div class="derived-row"><span>Recovery after burst</span><strong>${format(burst.recovery)} s</strong></div>
  </div>` : ""}`;

  const shotFields = `<div class="form-grid">
    ${selectField("Pattern", "family.shot.kind", state.shot.kind, shotKinds)}
    ${field("Simultaneous Projectiles", "family.shot.projectiles", state.shot.projectiles)}
    ${field("Spread Degrees", "family.shot.spreadDegrees", state.shot.spreadDegrees)}
    ${field("Spread Randomness", "family.shot.randomnessDegrees", state.shot.randomnessDegrees)}
    ${field("Pulses per Shot", "family.shot.pulses", state.shot.pulses)}
    ${field("Seconds Between Pulses", "family.shot.secondsBetweenPulses", state.shot.secondsBetweenPulses)}
  </div>`;

  const deliveryFields = `<div class="form-grid">
    ${selectField("Type", "family.delivery.type", state.delivery.type, deliveryTypes)}
    ${state.delivery.type !== "special" ? field("Range", "family.delivery.range", state.delivery.range) : ""}
    ${["normal", "orb", "rocket"].includes(state.delivery.type) ? field("Speed", "family.delivery.speed", state.delivery.speed) : ""}
    ${["normal", "orb", "rocket"].includes(state.delivery.type) ? field("Radius", "family.delivery.radius", state.delivery.radius) : ""}
    ${state.delivery.type === "laser" ? field("Beam Width", "family.delivery.beamWidth", state.delivery.beamWidth) : ""}
  </div>`;

  const homingFields = `<div class="form-grid">
    ${selectField("Guidance", "family.guidance.mode", state.guidance.mode, ["unguided", "homing"])}
    ${state.guidance.mode === "homing" ? field("Acquisition Range", "family.guidance.acquisitionRange", state.guidance.acquisitionRange) : ""}
    ${state.guidance.mode === "homing" ? field("Turn Rate / Second", "family.guidance.turnRateDegreesPerSecond", state.guidance.turnRateDegreesPerSecond) : ""}
    ${state.guidance.mode === "homing" ? field("Activation Delay", "family.guidance.activationDelaySeconds", state.guidance.activationDelaySeconds) : ""}
    ${state.guidance.mode === "homing" ? selectField("Target Policy", "family.guidance.targetPolicy", state.guidance.targetPolicy, ["closest-to-aim", "nearest-in-range", "current-locked-target"]) : ""}
    ${state.guidance.mode === "homing" ? `<div class="field"><label>Target Loss</label>${checkboxField("Reacquire", "family.guidance.reacquire", state.guidance.reacquire)}</div>` : ""}
  </div>`;

  const effectFields = `<div class="form-grid">
    <div class="field"><label>Explosion</label>${checkboxField("Enabled", "family.effects.explosion.enabled", state.effects.explosion.enabled)}</div>
    ${state.effects.explosion.enabled ? field("Explosion Radius", "family.effects.explosion.radius", state.effects.explosion.radius) : ""}
    ${state.effects.explosion.enabled ? field("Minimum Damage Multiplier", "family.effects.explosion.minimumDamageMultiplier", state.effects.explosion.minimumDamageMultiplier) : ""}
    <div class="field"><label>Damage Over Time</label>${checkboxField("Enabled", "family.effects.damageOverTime.enabled", state.effects.damageOverTime.enabled)}</div>
    ${state.effects.damageOverTime.enabled ? field("Duration", "family.effects.damageOverTime.durationSeconds", state.effects.damageOverTime.durationSeconds) : ""}
    ${state.effects.damageOverTime.enabled ? field("Ticks / Second", "family.effects.damageOverTime.ticksPerSecond", state.effects.damageOverTime.ticksPerSecond) : ""}
    ${state.effects.damageOverTime.enabled ? field("Maximum Stacks", "family.effects.damageOverTime.maximumStacks", state.effects.damageOverTime.maximumStacks) : ""}
    <div class="field"><label>Chain Arc</label>${checkboxField("Enabled", "family.effects.chain.enabled", state.effects.chain.enabled)}</div>
    ${state.effects.chain.enabled ? field("Maximum Targets", "family.effects.chain.maximumTargets", state.effects.chain.maximumTargets) : ""}
    ${state.effects.chain.enabled ? field("Acquisition Range", "family.effects.chain.acquisitionRange", state.effects.chain.acquisitionRange) : ""}
    ${state.effects.chain.enabled ? field("Damage Retained / Jump", "family.effects.chain.retainedDamagePerJump", state.effects.chain.retainedDamagePerJump) : ""}
  </div>`;

  return [
    section("Family", `<div class="form-grid">
      ${selectField("Rarity", "family.rarity", state.rarity, rarityOptions)}
      ${selectField("Category", "family.category", state.category, gunCategories)}
      ${selectField("Damage Type", "family.damageType", state.damageType, damageTypes)}
      ${selectField("Runtime Status", "family.runtimeStatus", state.runtimeStatus, ["live", "runtime-pending"])}
    </div>`, true),
    renderProgression(mark),
    section("Fire", fireFields, true),
    section("Shot", shotFields, true),
    section("Delivery", deliveryFields, true),
    section("Guidance", homingFields),
    section("Impact", `<div class="form-grid">
      ${field("Pierce", "family.impact.pierce", state.impact.pierce)}
      ${field("Ricochet", "family.impact.ricochet", state.impact.ricochet)}
      ${state.impact.ricochet > 0 ? field("Speed Retained / Bounce", "family.impact.retainedSpeedPerRicochet", state.impact.retainedSpeedPerRicochet) : ""}
      ${field("Knockback", "family.impact.knockback", state.impact.knockback)}
    </div>`),
    section("Effects", effectFields),
    section("MK Damage", `<div class="form-grid">
      ${field("Direct Damage", "damage.direct", mark.damage.direct)}
      ${field("Primary Area Damage", "damage.area", mark.damage.area)}
      ${field("DoT / Second", "damage.dotPerSecond", mark.damage.dotPerSecond)}
    </div>`, true),
    section("MK Art", renderGunArt(mark)),
    state.runtimeStatus === "runtime-pending"
      ? section("Pending Feature", `<div class="form-grid">
          ${field("Feature ID", "family.pendingFeature.id", state.pendingFeature.id, "text")}
          ${textareaField("Implementation Notes", "family.pendingFeature.notes", state.pendingFeature.notes)}
        </div>`, true)
      : ""
  ].join("");
}

function renderGunArt(mark) {
  return `<div class="form-grid">
    ${artField("Inventory Side", "art.side", mark.art.side, "side")}
    ${artField("Mounted Top-Down", "art.mounted", mark.art.mounted, "mounted")}
    ${artField("Projectile / Beam", "art.delivery", mark.art.delivery, "delivery")}
    ${artField("Trail", "art.trail", mark.art.trail, "trail")}
    ${artField("Impact", "art.impact", mark.art.impact, "impact")}
    ${artField("Explosion", "art.explosion", mark.art.explosion, "explosion")}
  </div>`;
}

function renderGearEditor() {
  const mark = activeMarkValue();
  return [
    section("Set", `<div class="form-grid">
      ${selectField("Rarity", "family.rarity", state.rarity, rarityOptions)}
    </div>`, true),
    renderProgression(mark),
    ...gearSlots.map(slot => renderGearPiece(slot, mark.pieces[slot]))
  ].join("");
}

function renderGearPiece(slot, piece) {
  const modifierRows = piece.modifiers.length === 0
    ? `<div class="notice">No modifiers yet.</div>`
    : piece.modifiers.map((modifier, index) => `<div class="bonus-row gear-modifier-row">
        <select data-piece="${slot}" data-modifier-index="${index}" data-modifier-field="target">${optionList(gearStats, modifier.target)}</select>
        <select data-piece="${slot}" data-modifier-index="${index}" data-modifier-field="operation">${optionList(modifierOperations, modifier.operation)}</select>
        <input data-piece="${slot}" data-modifier-index="${index}" data-modifier-field="value" type="number" step="any" value="${escapeHtml(modifier.value)}">
        <button type="button" class="danger remove-modifier" data-piece="${slot}" data-modifier-index="${index}" title="Remove modifier">×</button>
      </div>`).join("");

  return section(title(slot), `<div class="form-grid">
      ${field("Piece Name", `pieces.${slot}.name`, piece.name, "text")}
      ${field("Maximum Augment Slots", `pieces.${slot}.maxAugmentSlots`, piece.maxAugmentSlots)}
      ${artField("Inventory Side", `pieces.${slot}.art`, piece.art, `${slot}-side`)}
    </div>
    <div class="divider"></div>
    <div id="modifierRows-${slot}">${modifierRows}</div>
    <button type="button" class="small add-modifier" data-piece="${slot}" style="margin-top:10px">+ Add Modifier</button>`, slot === "headpiece");
}

function artField(label, path, value, previewKey) {
  return `<div class="field"><label>${escapeHtml(label)}</label><div class="art-row">
    <input data-path="${path}" type="text" value="${escapeHtml(value)}" placeholder="stable art reference">
    <button type="button" class="small preview-file-button" data-preview-key="${previewKey}">Preview</button>
  </div><input class="preview-file-input" data-preview-key="${previewKey}" type="file" accept="image/*" hidden></div>`;
}

function bindEditorInputs() {
  elements.markEditor.querySelectorAll("[data-path]").forEach(control => {
    const eventName = control.tagName === "SELECT" || control.type === "checkbox" ? "change" : "input";
    control.addEventListener(eventName, () => {
      const value = control.type === "checkbox" ? control.checked : control.type === "number" ? number(control.value) : control.value;
      setEditorPath(control.dataset.path, value);
      setDirty();
      if (eventName === "change") render();
      else refreshOutput();
    });
  });

  elements.markEditor.querySelectorAll(".preview-file-button").forEach(button => button.addEventListener("click", () => {
    elements.markEditor.querySelector(`.preview-file-input[data-preview-key="${button.dataset.previewKey}"]`)?.click();
  }));
  elements.markEditor.querySelectorAll(".preview-file-input").forEach(input => input.addEventListener("change", () => {
    const file = input.files?.[0];
    if (!file) return;
    const reader = new FileReader();
    reader.onload = () => {
      previews[`${activeMark}:${input.dataset.previewKey}`] = reader.result;
      renderPreview();
    };
    reader.readAsDataURL(file);
  }));

  elements.markEditor.querySelectorAll("[data-modifier-index]").forEach(control =>
    control.addEventListener(control.tagName === "SELECT" ? "change" : "input", () => {
      const mark = activeMarkValue();
      const modifier = mark.pieces[control.dataset.piece].modifiers[Number(control.dataset.modifierIndex)];
      modifier[control.dataset.modifierField] = control.dataset.modifierField === "value" ? number(control.value) : control.value;
      setDirty();
      if (control.tagName === "SELECT") render(); else refreshOutput();
    }));
  elements.markEditor.querySelectorAll(".remove-modifier").forEach(button => button.addEventListener("click", () => {
    activeMarkValue().pieces[button.dataset.piece].modifiers.splice(Number(button.dataset.modifierIndex), 1);
    setDirty();
    render();
  }));
  elements.markEditor.querySelectorAll(".add-modifier").forEach(button => button.addEventListener("click", () => {
    activeMarkValue().pieces[button.dataset.piece].modifiers.push({
      target: "combat.maximum-health", operation: "flat", value: 0
    });
    setDirty();
    render();
  }));
}

function setEditorPath(path, value) {
  const isFamily = path.startsWith("family.");
  const root = isFamily ? state : activeMarkValue();
  const parts = (isFamily ? path.substring(7) : path).split(".");
  const last = parts.pop();
  parts.reduce((current, part) => current[part], root)[last] = value;
}

function refreshOutput() {
  renderCalculated();
  renderChecks();
  elements.jsonPreview.textContent = JSON.stringify(cleanPackage(), null, 2);
}

function renderPreview() {
  if (state.kind === "gear-set") {
    elements.previewPanel.innerHTML = gearSlots.map(slot =>
      previewBlock(title(slot), previews[`${activeMark}:${slot}-side`])).join("");
    return;
  }
  elements.previewPanel.innerHTML = `${previewBlock("Inventory Side", previews[`${activeMark}:side`])}
    <div class="preview-grid">
      <div>${previewBlock("Mounted", previews[`${activeMark}:mounted`])}</div>
      <div>${previewBlock(title(state.delivery.type), previews[`${activeMark}:delivery`])}</div>
    </div>`;
}

function previewBlock(label, source) {
  return `<div class="preview-label">${escapeHtml(label)}</div><div class="preview-box">${
    source ? `<img src="${source}" alt="${escapeHtml(label)} preview">` : "No local preview"
  }</div>`;
}

function renderCalculated() {
  const mark = activeMarkValue();
  let rows = [];
  if (state.kind === "gear-set") {
    let count = 0;
    gearSlots.forEach(slot => {
      mark.pieces[slot].modifiers.forEach(modifier => {
        count++;
        rows.push([`${title(slot)} · ${statLabel(modifier.target)}`, signedModifier(modifier)]);
      });
    });
    if (count === 0) rows.push(["Modifiers", "None"]);
  } else {
    const projectilesPerCycle = state.shot.projectiles * state.shot.pulses
      * (state.fire.mode === "burst" ? state.fire.shotsPerBurst : 1);
    const directPerCycle = mark.damage.direct * projectilesPerCycle;
    const primaryAreaPerCycle = mark.damage.area * projectilesPerCycle;
    const cycles = state.fire.cyclesPerSecond;
    rows = [
      ["Damage / Projectile", format(mark.damage.direct)],
      ["Projectiles / Cycle", format(projectilesPerCycle, 0)],
      ["Direct DPS", format(directPerCycle * cycles)],
      ["Primary Area DPS", format(primaryAreaPerCycle * cycles)],
      ["Maintainable DoT DPS", format(mark.damage.dotPerSecond)],
      ["Primary Target DPS", format((directPerCycle + primaryAreaPerCycle) * cycles + mark.damage.dotPerSecond)]
    ];
  }
  elements.calculatedPanel.innerHTML = rows.map(([label, value]) =>
    `<div class="stat-row"><span>${escapeHtml(label)}</span><strong>${escapeHtml(value)}</strong></div>`).join("");
}

function statLabel(target) {
  return gearStats.find(value => value.id === target)?.label || target;
}

function signedModifier(modifier) {
  const prefix = modifier.value > 0 ? "+" : "";
  const suffix = modifier.operation === "percentage" ? "%" : modifier.operation === "multiplicative" ? "×" : "";
  return `${prefix}${format(modifier.value)}${suffix}${gearStats.find(value => value.id === modifier.target)?.live ? "" : " (pending)"}`;
}

function calculateBurst(fire) {
  if (fire.mode !== "burst" || fire.cyclesPerSecond <= 0) return { firingTime: 0, recovery: 0 };
  const firingTime = Math.max(0, fire.shotsPerBurst - 1) * fire.secondsBetweenShots;
  return { firingTime, recovery: (1 / fire.cyclesPerSecond) - firingTime };
}

function format(value, digits = 2) {
  return Number.isFinite(Number(value)) ? Number(value).toFixed(digits).replace(/\.00$/, "") : "0";
}
