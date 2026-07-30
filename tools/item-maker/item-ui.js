function render() {
  elements.itemName.value = state.name;
  elements.itemId.value = state.id;
  elements.intendedUse.value = state.intendedUse;
  elements.gunKindButton.classList.toggle("active", state.kind === "gun");
  elements.gearKindButton.classList.toggle("active", state.kind === "gear");
  elements.packageKind.textContent = title(state.kind);
  elements.packageFile.textContent = `${state.id || "item"}.item.json`;
  elements.idExample.textContent = `${state.id || "item"}.mk1`;
  elements.activeMarkId.textContent = `${state.id || "item"}.${activeMark}`;
  elements.copyPreviousButton.classList.toggle("hidden", activeMark === "mk1");
  document.querySelectorAll("[data-mark]").forEach(button => button.classList.toggle("active", button.dataset.mark === activeMark));

  elements.markEditor.innerHTML = state.kind === "gun"
    ? renderGunEditor(state.marks[activeMark])
    : renderGearEditor(state.marks[activeMark]);

  bindEditorInputs();
  renderPreview();
  renderCalculated();
  renderChecks();
  elements.jsonPreview.textContent = JSON.stringify(cleanPackage(), null, 2);
}

function renderDrop(mark) {
  return section("Drop", `<div class="form-grid">
    ${selectField("Rarity", "rarity", mark.rarity, rarityOptions)}
    ${field("Drop Level", "dropLevel", mark.dropLevel)}
    ${field("Drop Weight", "dropWeight", mark.dropWeight)}
    <div class="field"><label>Status</label>${checkboxField("Available", "available", mark.available)}</div>
  </div>`, true);
}

function renderGunEditor(mark) {
  const fire = mark.fire;
  const delivery = mark.delivery;
  const homing = mark.homing;
  const damage = mark.damage;
  const burst = calculateBurst(mark);

  let fireFields = `<div class="form-grid">
    ${selectField("Mode", "fire.mode", fire.mode, ["semi", "automatic", "burst", "charge"])}
  `;
  if (fire.mode === "semi" || fire.mode === "automatic") {
    fireFields += `${field("Waves / Second", "fire.wavesPerSecond", fire.wavesPerSecond)}</div>
      <div class="derived-box"><div class="derived-row"><span>Time between shots</span><strong>${format(safeDivide(1, fire.wavesPerSecond))} s</strong></div></div>`;
  } else if (fire.mode === "burst") {
    fireFields += `${field("Average Waves / Second", "fire.wavesPerSecond", fire.wavesPerSecond)}
      ${field("Waves per Burst", "fire.wavesPerBurst", fire.wavesPerBurst)}
      ${field("Time Between Waves", "fire.secondsBetweenWaves", fire.secondsBetweenWaves)}</div>
      <div class="derived-box">
        <div class="derived-row"><span>Burst every</span><strong>${format(burst.interval)} s</strong></div>
        <div class="derived-row"><span>Burst firing time</span><strong>${format(burst.firingTime)} s</strong></div>
        <div class="derived-row"><span>Wait after burst</span><strong>${format(burst.wait)} s</strong></div>
      </div>`;
  } else {
    fireFields += `${field("Full Charge Time", "fire.fullChargeSeconds", fire.fullChargeSeconds)}
      ${field("Maximum Hold Time", "fire.maxHoldSeconds", fire.maxHoldSeconds)}
      ${field("Full Charge Damage", "fire.fullChargeDamage", fire.fullChargeDamage)}
      <div class="field"><label>Auto-fire</label>${checkboxField("Auto-fire at full charge", "fire.autoFireAtFull", fire.autoFireAtFull)}</div></div>
      <div class="derived-box"><div class="derived-row"><span>Charge speed</span><strong>${format(safeDivide(100, fire.fullChargeSeconds), 1)}% / s</strong></div></div>
      <div class="notice warn" style="margin-top:10px">Charge values can be authored and exported, but game-side charge firing still needs implementation.</div>`;
  }

  const dotFields = `<div class="form-grid">
    ${field("DoT Damage / Second", "damage.dotDamage", damage.dotDamage)}
    ${field("DoT Duration", "damage.dotSeconds", damage.dotSeconds)}
  </div>
  <div class="derived-box"><div class="derived-row"><span>Total DoT per hit</span><strong>${format(damage.dotDamage * damage.dotSeconds)}</strong></div></div>`;

  let deliveryFields = `<div class="form-grid">${selectField("Type", "delivery.type", delivery.type, ["normal", "orb", "rocket", "laser", "special"])}`;
  if (delivery.type === "normal") {
    deliveryFields += `${field("Speed", "delivery.speed", delivery.speed)}${field("Range", "delivery.range", delivery.range)}${field("Shot Radius", "delivery.radius", delivery.radius)}</div>`;
  } else if (delivery.type === "orb") {
    deliveryFields += `${field("Speed", "delivery.speed", delivery.speed)}${field("Range", "delivery.range", delivery.range)}${field("Orb Radius", "delivery.radius", delivery.radius)}${field("Explosion Radius", "delivery.explosionRadius", delivery.explosionRadius)}</div>`;
  } else if (delivery.type === "rocket") {
    deliveryFields += `${field("Speed", "delivery.speed", delivery.speed)}${field("Range", "delivery.range", delivery.range)}${field("Rocket Radius", "delivery.radius", delivery.radius)}${field("Explosion Radius", "delivery.explosionRadius", delivery.explosionRadius)}</div>`;
  } else if (delivery.type === "laser") {
    deliveryFields += `${field("Range", "delivery.range", delivery.range)}${field("Beam Width", "delivery.beamWidth", delivery.beamWidth)}</div>`;
  } else {
    deliveryFields += `</div><div class="form-grid" style="margin-top:12px">${field("Special Code", "special.code", mark.special.code, "text")}${textareaField("Special Notes", "special.notes", mark.special.notes)}</div>`;
  }

  const homingFields = `<div class="form-grid">
    <div class="field"><label>Homing</label>${checkboxField("Enabled", "homing.enabled", homing.enabled)}</div>
    ${homing.enabled ? `${field("Turn Speed", "homing.turnSpeed", homing.turnSpeed)}${field("Find Range", "homing.findRange", homing.findRange)}${field("Start Delay", "homing.startDelay", homing.startDelay)}<div class="field"><label>Target loss</label>${checkboxField("Find another target", "homing.findAnotherTarget", homing.findAnotherTarget)}</div>` : ""}
  </div>${homing.enabled ? `<div class="derived-box"><div class="derived-row"><span>Strength</span><strong>${homingStrength(homing.turnSpeed)}</strong></div></div>` : ""}`;

  return [
    renderDrop(mark),
    section("Fire", fireFields, true),
    section("Shot", `<div class="form-grid">${field("Projectiles", "shot.projectiles", mark.shot.projectiles)}${field("Spread", "shot.spread", mark.shot.spread)}</div>`, true),
    section("Damage", `<div class="form-grid">${field("Damage", "damage.amount", damage.amount)}${selectField("Damage Type", "damage.type", damage.type, damageTypes)}${field("Movement", "damage.movement", damage.movement)}</div><div class="divider"></div><div class="help" style="margin-bottom:8px">Set both DoT values to zero when the gun has no damage over time.</div>${dotFields}`, true),
    section("Delivery", deliveryFields, true),
    section("Homing", homingFields),
    section("Impact", `<div class="form-grid three">${field("Pierce", "impact.pierce", mark.impact.pierce)}${field("Ricochet", "impact.ricochet", mark.impact.ricochet)}${field("Knockback", "impact.knockback", mark.impact.knockback)}</div>`),
    section("Art", renderGunArt(mark)),
    delivery.type === "special" ? "" : section("Special", `<div class="form-grid">${field("Special Code", "special.code", mark.special.code, "text")}${textareaField("Special Notes", "special.notes", mark.special.notes)}</div>`)
  ].join("");
}

function renderGunArt(mark) {
  return `<div class="form-grid">
    ${artField("Inventory Side", "art.side", mark.art.side, "side")}
    ${artField("Mounted Top-Down", "art.mounted", mark.art.mounted, "mounted")}
    ${artField(mark.delivery.type === "laser" ? "Beam" : "Projectile", "art.projectile", mark.art.projectile, "projectile")}
    ${artField("Trail", "art.trail", mark.art.trail, "trail")}
    ${artField("Impact", "art.impact", mark.art.impact, "impact")}
    ${artField("Explosion", "art.explosion", mark.art.explosion, "explosion")}
  </div>`;
}

function artField(label, path, value, previewKey) {
  return `<div class="field"><label>${escapeHtml(label)}</label><div class="art-row"><input data-path="${path}" type="text" value="${escapeHtml(value)}" placeholder="asset reference"><button type="button" class="small preview-file-button" data-preview-key="${previewKey}">Preview</button></div><input class="preview-file-input" data-preview-key="${previewKey}" type="file" accept="image/*" hidden></div>`;
}

function renderGearEditor(mark) {
  const bonusRows = mark.bonuses.length === 0
    ? `<div class="notice">No bonuses yet.</div>`
    : mark.bonuses.map((bonus, index) => `<div class="bonus-row">
        <select data-bonus-index="${index}" data-bonus-field="stat">${optionList(gearStats, bonus.stat)}</select>
        <input data-bonus-index="${index}" data-bonus-field="value" type="number" step="any" value="${escapeHtml(bonus.value)}">
        <button type="button" class="danger remove-bonus" data-bonus-index="${index}" title="Remove bonus">×</button>
      </div>`).join("");

  return [
    renderDrop(mark),
    section("Gear", `<div class="form-grid">${selectField("Slot", "slot", mark.slot, gearSlots)}${field("Augment Slots", "augmentSlots", mark.augmentSlots)}</div>`, true),
    section("Bonuses", `<div id="bonusRows">${bonusRows}</div><button id="addBonusButton" type="button" class="small" style="margin-top:10px">+ Add Bonus</button>`, true),
    section("Art", `<div class="form-grid">${artField("Inventory Side", "art.side", mark.art.side, "side")}</div>`, true),
    section("Special", `<div class="form-grid">${field("Special Code", "special.code", mark.special.code, "text")}${textareaField("Special Notes", "special.notes", mark.special.notes)}</div>`)
  ].join("");
}

function bindEditorInputs() {
  elements.markEditor.querySelectorAll("[data-path]").forEach(control => {
    const eventName = control.tagName === "SELECT" || control.type === "checkbox" ? "change" : "input";
    control.addEventListener(eventName, () => {
      setPath(state.marks[activeMark], control.dataset.path, control.type === "checkbox" ? control.checked : control.type === "number" ? number(control.value) : control.value);
      setDirty();
      if (eventName === "change") render();
      else refreshOutput();
    });
  });

  elements.markEditor.querySelectorAll(".preview-file-button").forEach(button => button.addEventListener("click", () => {
    const input = elements.markEditor.querySelector(`.preview-file-input[data-preview-key="${button.dataset.previewKey}"]`);
    input?.click();
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

  elements.markEditor.querySelectorAll("[data-bonus-index]").forEach(control => control.addEventListener(control.tagName === "SELECT" ? "change" : "input", () => {
    const bonus = state.marks[activeMark].bonuses[Number(control.dataset.bonusIndex)];
    bonus[control.dataset.bonusField] = control.dataset.bonusField === "value" ? number(control.value) : control.value;
    setDirty();
    if (control.tagName === "SELECT") render();
    else refreshOutput();
  }));

  elements.markEditor.querySelectorAll(".remove-bonus").forEach(button => button.addEventListener("click", () => {
    state.marks[activeMark].bonuses.splice(Number(button.dataset.bonusIndex), 1);
    setDirty();
    render();
  }));

  elements.markEditor.querySelector("#addBonusButton")?.addEventListener("click", () => {
    state.marks[activeMark].bonuses.push({ stat: "max-health", value: 0 });
    setDirty();
    render();
  });
}

function setPath(root, path, value) {
  const parts = path.split(".");
  const last = parts.pop();
  const target = parts.reduce((current, part) => current[part], root);
  target[last] = value;
}

function refreshOutput() {
  renderCalculated();
  renderChecks();
  elements.jsonPreview.textContent = JSON.stringify(cleanPackage(), null, 2);
}

function renderPreview() {
  const mark = state.marks[activeMark];
  if (state.kind === "gear") {
    elements.previewPanel.innerHTML = previewBlock("Inventory Side", previews[`${activeMark}:side`]);
    return;
  }
  elements.previewPanel.innerHTML = `${previewBlock("Inventory Side", previews[`${activeMark}:side`])}
    <div class="preview-grid">
      <div>${previewBlock("Mounted", previews[`${activeMark}:mounted`])}</div>
      <div>${previewBlock(title(mark.delivery.type === "laser" ? "beam" : mark.delivery.type === "normal" ? "projectile" : mark.delivery.type), previews[`${activeMark}:projectile`])}</div>
    </div>`;
}

function previewBlock(label, source) {
  return `<div class="preview-label">${escapeHtml(label)}</div><div class="preview-box">${source ? `<img src="${source}" alt="${escapeHtml(label)} preview">` : "No local preview"}</div>`;
}

function renderCalculated() {
  const mark = state.marks[activeMark];
  let rows = [];
  if (state.kind === "gear") {
    rows = mark.bonuses.length === 0
      ? [["Bonuses", "None"]]
      : mark.bonuses.map(bonus => [title(bonus.stat), signed(bonus.value, bonus.stat)]);
  } else {
    const damageWave = mark.damage.amount * mark.shot.projectiles;
    rows.push(["Damage / Projectile", format(mark.damage.amount)]);
    rows.push(["Damage / Wave", format(damageWave)]);
    rows.push(["Movement", signed(mark.damage.movement, "movement")]);
    if (mark.fire.mode !== "charge") {
      rows.push(["Direct DPS", format(damageWave * mark.fire.wavesPerSecond)]);
      rows.push(["Projectiles / Second", format(mark.shot.projectiles * mark.fire.wavesPerSecond)]);
    } else {
      rows.push(["Full Charge Damage", format(damageWave * mark.fire.fullChargeDamage)]);
    }
    if (mark.fire.mode === "burst") {
      const burst = calculateBurst(mark);
      rows.push(["Projectiles / Burst", format(mark.shot.projectiles * mark.fire.wavesPerBurst, 0)]);
      rows.push(["Damage / Burst", format(damageWave * mark.fire.wavesPerBurst)]);
      rows.push(["Burst Every", `${format(burst.interval)} s`]);
      rows.push(["Wait After Burst", `${format(burst.wait)} s`]);
    }
    if (mark.damage.dotDamage > 0 || mark.damage.dotSeconds > 0) {
      rows.push(["Total DoT / Hit", format(mark.damage.dotDamage * mark.damage.dotSeconds)]);
    }
  }
  elements.calculatedPanel.innerHTML = rows.map(([label, value]) => `<div class="stat-row"><span>${escapeHtml(label)}</span><strong>${escapeHtml(value)}</strong></div>`).join("");
}

function signed(value, stat) {
  const numeric = number(value);
  const prefix = numeric > 0 ? "+" : "";
  const percentage = ["movement", "damage", "fire-rate"].includes(stat) ? "%" : "";
  return `${prefix}${format(numeric)}${percentage}`;
}

function calculateBurst(mark) {
  const interval = safeDivide(mark.fire.wavesPerBurst, mark.fire.wavesPerSecond);
  const firingTime = Math.max(0, mark.fire.wavesPerBurst - 1) * mark.fire.secondsBetweenWaves;
  return { interval, firingTime, wait: interval - firingTime };
}

function safeDivide(left, right) { return number(right) > 0 ? number(left) / number(right) : 0; }
function format(value, digits = 2) { return Number.isFinite(Number(value)) ? Number(value).toFixed(digits).replace(/\.00$/, "") : "0"; }

function homingStrength(turnSpeed) {
  const speed = number(turnSpeed);
  if (speed < 60) return "Weak";
  if (speed < 140) return "Medium";
  if (speed < 270) return "Strong";
  return "Very Strong";
}
