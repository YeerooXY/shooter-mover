"use strict";

const gameplayEditor = document.querySelector("#gameplayEditor");
const jsonWorkspace = document.querySelector("#jsonWorkspace");
const gameplayModeButton = document.querySelector("#gameplayModeButton");
const jsonModeButton = document.querySelector("#jsonModeButton");

const gameplayRarities = ["common", "rare", "epic", "legendary", "artifact"];
const gameplayProjectileTypes = ["bullet", "orb", "rocket", "beam"];
const gameplayDamageTypes = ["physical", "energy", "thermal", "chemical"];
const gameplayFireModes = ["semi-automatic", "automatic", "burst"];
const gameplayOwnership = [["shared", "Same for MK1–MK3"], ["mark", "Different by Mark"]];
const gameplayEffectOwnership = [["off", "Not used"], ...gameplayOwnership];

let weaponEditorMode = "gameplay";
let gameplayHasRendered = false;
let gameplayRenderQueued = false;

function gameplayClone(value) { return JSON.parse(JSON.stringify(value || {})); }
function gameplayObject(value) { return value && typeof value === "object" && !Array.isArray(value); }
function gameplayNumber(value, fallback = 0) {
  const parsed = Number(value);
  return Number.isFinite(parsed) ? parsed : fallback;
}
function gameplayTitle(value) {
  return String(value || "").replace(/[-_]+/g, " ").replace(/\b\w/g, character => character.toUpperCase());
}
function gameplayOptions(values, selected) {
  return values.map(value => {
    const optionValue = Array.isArray(value) ? value[0] : value;
    const optionLabel = Array.isArray(value) ? value[1] : gameplayTitle(value);
    return `<option value="${escapeHtml(optionValue)}"${optionValue === selected ? " selected" : ""}>${escapeHtml(optionLabel)}</option>`;
  }).join("");
}
function gameplayInput(label, key, value, options = {}) {
  const type = options.type || "number";
  const step = type === "number" ? ` step="${options.step || "any"}"` : "";
  const min = options.min === undefined ? "" : ` min="${options.min}"`;
  const max = options.max === undefined ? "" : ` max="${options.max}"`;
  const full = options.full ? " full" : "";
  return `<div class="field${full}"><label>${escapeHtml(label)}</label><input data-g-key="${escapeHtml(key)}" type="${type}" value="${escapeHtml(value ?? "")}"${step}${min}${max}></div>`;
}
function gameplayTextarea(label, key, value) {
  return `<div class="field full"><label>${escapeHtml(label)}</label><textarea data-g-key="${escapeHtml(key)}">${escapeHtml(value || "")}</textarea></div>`;
}
function gameplaySelect(label, key, value, values) {
  return `<div class="field"><label>${escapeHtml(label)}</label><select data-g-key="${escapeHtml(key)}">${gameplayOptions(values, value)}</select></div>`;
}
function gameplayCheckbox(label, key, checked) {
  return `<label class="checkbox-row"><input data-g-key="${escapeHtml(key)}" type="checkbox"${checked ? " checked" : ""}> <span>${escapeHtml(label)}</span></label>`;
}
function gameplayControl(key) { return gameplayEditor.querySelector(`[data-g-key="${key}"]`); }
function gameplayValue(key, fallback = "") {
  const control = gameplayControl(key);
  return control ? control.value : fallback;
}
function gameplayNumericValue(key, fallback = 0) { return gameplayNumber(gameplayValue(key, fallback), fallback); }
function gameplayChecked(key, fallback = false) {
  const control = gameplayControl(key);
  return control ? control.checked : fallback;
}

function gameplayFireFields(prefix, value = {}, title = "") {
  return `<div class="mark-card" data-fire-card="${escapeHtml(prefix)}">
    ${title ? `<h3>${escapeHtml(title)}</h3>` : ""}
    ${gameplaySelect("Fire mode", `${prefix}.mode`, value.mode || "automatic", gameplayFireModes)}
    ${gameplayInput("Cycles per second", `${prefix}.rate`, value.rate ?? 1, { min: 0.000001 })}
    <div data-burst-fields>
      ${gameplayInput("Shots in each burst", `${prefix}.shotsPerBurst`, value.shotsPerBurst ?? 3, { min: 2, step: 1 })}
      ${gameplayInput("Seconds between burst shots", `${prefix}.secondsBetweenShots`, value.secondsBetweenShots ?? 0.08, { min: 0.000001 })}
    </div>
  </div>`;
}
function gameplayHomingFields(prefix, value = {}, title = "") {
  return `<div class="mark-card">
    ${title ? `<h3>${escapeHtml(title)}</h3>` : ""}
    ${gameplayInput("Target search range", `${prefix}.acquisitionRange`, value.acquisitionRange ?? 20, { min: 0.000001 })}
    ${gameplayInput("Turn speed (degrees/sec)", `${prefix}.turnRate`, value.turnRate ?? 180, { min: 0.000001 })}
    ${gameplayInput("Homing delay", `${prefix}.activationDelay`, value.activationDelay ?? 0, { min: 0 })}
    ${gameplaySelect("Target choice", `${prefix}.targetPolicy`, value.targetPolicy || "closest-to-aim", [["closest-to-aim", "Closest to aim"]])}
    ${gameplayCheckbox("Find another target if needed", `${prefix}.reacquire`, value.reacquire !== false)}
  </div>`;
}
function gameplayDotFields(prefix, value = {}, title = "") {
  return `<div class="mark-card">
    ${title ? `<h3>${escapeHtml(title)}</h3>` : ""}
    ${gameplayInput("Damage per second", `${prefix}.damagePerSecond`, value.damagePerSecond ?? 1, { min: 0.000001 })}
    ${gameplayInput("Duration (seconds)", `${prefix}.duration`, value.duration ?? 3, { min: 0.000001 })}
    ${gameplayInput("Damage ticks per second", `${prefix}.ticksPerSecond`, value.ticksPerSecond ?? 3, { min: 0.000001 })}
    ${gameplayInput("Maximum stacks", `${prefix}.maxStacks`, value.maxStacks ?? 1, { min: 1, step: 1 })}
  </div>`;
}
function gameplayExplosionFields(prefix, value = {}, title) {
  return `<div class="mark-card"><h3>${escapeHtml(title)}</h3>
    ${gameplayInput("Explosion radius", `${prefix}.radius`, value.radius ?? 2, { min: 0.000001 })}
    ${gameplayInput("Damage at outer edge", `${prefix}.edgeDamageMultiplier`, value.edgeDamageMultiplier ?? 0.5, { min: 0, max: 1 })}
  </div>`;
}
function gameplayInferDotOwnership(shared, marks) {
  const numerical = ["damagePerSecond", "duration", "ticksPerSecond", "maxStacks"];
  if (gameplayObject(shared.dot) && numerical.some(field => Object.prototype.hasOwnProperty.call(shared.dot, field))) return "shared";
  return marks.every(mark => gameplayObject(mark.dot)) ? "mark" : "off";
}

function gameplayRender() {
  const result = parseFiles();
  if (result.errors.length) {
    gameplayEditor.innerHTML = `<div class="gameplay-warning">Advanced JSON contains an error. Fix it there before using the gameplay editor.<br><br>${result.errors.map(escapeHtml).join("<br>")}</div>`;
    gameplayHasRendered = true;
    return;
  }

  const shared = result.parsed["weapon.json"];
  const marks = [1, 2, 3].map(mark => result.parsed[`mk${mark}.json`]);
  const fireOwnership = gameplayObject(shared.fire) ? "shared" : "mark";
  const homingOwnership = gameplayObject(shared.homing) ? "shared" : (marks.every(mark => gameplayObject(mark.homing)) ? "mark" : "off");
  const dotOwnership = gameplayInferDotOwnership(shared, marks);
  const explosionOwnership = marks.every(mark => gameplayObject(mark.explosion)) ? "mark" : "off";
  const mountedOwnership = shared.art?.mounted ? "shared" : "mark";
  const projectile = shared.projectile || {};
  const beam = shared.beam || {};
  const impact = shared.impact || {};
  const sharedArt = shared.art || {};
  const sharedDot = shared.dot || {};

  gameplayEditor.innerHTML = `
    <section class="gameplay-section"><h2>Weapon</h2><div class="gameplay-section-body gameplay-form-grid two">
      ${gameplayInput("Weapon name", "shared.name", shared.name || "", { type: "text" })}
      ${gameplaySelect("Rarity", "shared.rarity", shared.rarity || "common", gameplayRarities)}
      ${gameplayTextarea("Gameplay description", "shared.description", shared.description || "")}
      ${gameplaySelect("Projectile type", "shared.projectileType", shared.projectileType || "bullet", gameplayProjectileTypes)}
      ${gameplaySelect("Damage type", "shared.damageType", shared.damageType || "physical", gameplayDamageTypes)}
    </div></section>

    <section class="gameplay-section"><h2>Firing</h2><div class="gameplay-section-body">
      <div class="ownership-row">${gameplaySelect("Firing behaviour", "settings.fireOwnership", fireOwnership, gameplayOwnership)}</div>
      <div data-fire-shared>${gameplayFireFields("shared.fire", shared.fire || marks[0].fire || {})}</div>
      <div class="mark-grid" data-fire-marks>${marks.map((mark, index) => gameplayFireFields(`mark.${index + 1}.fire`, mark.fire || shared.fire || {}, `MK${index + 1}`)).join("")}</div>
      <div class="gameplay-note">Projectile count and burst shots are separate. A shotgun can fire several pellets in one firing cycle.</div>
    </div></section>

    <section class="gameplay-section"><h2>Shot and travel</h2><div class="gameplay-section-body">
      <div class="gameplay-form-grid">
        ${gameplayInput("Projectiles per shot", "shared.shot.projectiles", shared.shot?.projectiles ?? 1, { min: 1, step: 1 })}
        ${gameplayInput("Spread (degrees)", "shared.shot.spread", shared.shot?.spread ?? 0, { min: 0 })}
      </div>
      <div class="gameplay-form-grid" data-projectile-fields>
        ${gameplayInput("Projectile speed", "shared.projectile.speed", projectile.speed ?? 20, { min: 0.000001 })}
        ${gameplayInput("Projectile radius", "shared.projectile.radius", projectile.radius ?? 0.1, { min: 0.000001 })}
        ${gameplayInput("Range", "shared.projectile.range", projectile.range ?? 25, { min: 0.000001 })}
      </div>
      <div class="gameplay-form-grid two" data-beam-fields>
        ${gameplayInput("Beam range", "shared.beam.range", beam.range ?? projectile.range ?? 25, { min: 0.000001 })}
        ${gameplayInput("Beam width", "shared.beam.width", beam.width ?? 0.2, { min: 0.000001 })}
      </div>
    </div></section>

    <section class="gameplay-section"><h2>Hits</h2><div class="gameplay-section-body gameplay-form-grid">
      ${gameplayInput("Pierce", "shared.impact.pierce", impact.pierce ?? 1, { min: 0, step: 1 })}
      ${gameplayInput("Ricochet", "shared.impact.ricochet", impact.ricochet ?? 0, { min: 0 })}
      ${gameplayInput("Knockback", "shared.impact.knockback", impact.knockback ?? 0, { min: 0 })}
    </div></section>

    <section class="gameplay-section"><h2>MK1–MK3</h2><div class="gameplay-section-body mark-grid">
      ${marks.map((mark, index) => `<div class="mark-card"><h3>MK${index + 1}</h3>
        ${gameplayInput("Peak drop level", `mark.${index + 1}.peakLevel`, mark.peakLevel ?? [1, 25, 50][index], { min: 1, step: 1 })}
        ${gameplayInput("Damage", `mark.${index + 1}.damage`, mark.damage ?? 1, { min: 0.000001 })}
      </div>`).join("")}
    </div></section>

    <section class="gameplay-section"><h2>Homing</h2><div class="gameplay-section-body">
      <div class="ownership-row">${gameplaySelect("Homing", "settings.homingOwnership", homingOwnership, gameplayEffectOwnership)}</div>
      <div data-homing-shared>${gameplayHomingFields("shared.homing", shared.homing || marks[0].homing || {})}</div>
      <div class="mark-grid" data-homing-marks>${marks.map((mark, index) => gameplayHomingFields(`mark.${index + 1}.homing`, mark.homing || shared.homing || {}, `MK${index + 1}`)).join("")}</div>
      <div class="effect-disabled" data-homing-off>This weapon flies straight and does not steer toward enemies.</div>
    </div></section>

    <section class="gameplay-section"><h2>Damage over time</h2><div class="gameplay-section-body">
      <div class="ownership-row">
        ${gameplaySelect("Damage over time", "settings.dotOwnership", dotOwnership, gameplayEffectOwnership)}
        ${gameplayCheckbox("Refresh duration when applied again", "settings.dotRefreshDuration", sharedDot.refreshDuration !== false)}
      </div>
      <div data-dot-shared>${gameplayDotFields("shared.dot", shared.dot || marks[0].dot || {})}</div>
      <div class="mark-grid" data-dot-marks>${marks.map((mark, index) => gameplayDotFields(`mark.${index + 1}.dot`, mark.dot || shared.dot || {}, `MK${index + 1}`)).join("")}</div>
      <div class="effect-disabled" data-dot-off>This weapon deals only direct hit damage.</div>
    </div></section>

    <section class="gameplay-section"><h2>Explosion</h2><div class="gameplay-section-body">
      <div class="ownership-row">${gameplaySelect("Explosion", "settings.explosionOwnership", explosionOwnership, [["off", "Not used"], ["mark", "Explosion values by Mark"]])}</div>
      <div class="mark-grid" data-explosion-marks>${marks.map((mark, index) => gameplayExplosionFields(`mark.${index + 1}.explosion`, mark.explosion || {}, `MK${index + 1}`)).join("")}</div>
      <div class="effect-disabled" data-explosion-off>This weapon does not create an area explosion.</div>
      <div class="gameplay-note">Outer-edge damage is a fraction of normal weapon damage. There is no separate area-damage value.</div>
    </div></section>

    <section class="gameplay-section"><h2>Weapon art</h2><div class="gameplay-section-body">
      <div class="ownership-row">${gameplaySelect("Mounted weapon art", "settings.mountedOwnership", mountedOwnership, gameplayOwnership)}</div>
      <div class="gameplay-form-grid">
        ${gameplayInput("Projectile / beam art", "shared.art.delivery", sharedArt.delivery || "", { type: "text" })}
        ${gameplayInput("Trail art", "shared.art.trail", sharedArt.trail || "", { type: "text" })}
        ${gameplayInput("Impact art", "shared.art.impact", sharedArt.impact || "", { type: "text" })}
        <div data-mounted-shared>${gameplayInput("Mounted art", "shared.art.mounted", sharedArt.mounted || marks[0].art?.mounted || "", { type: "text" })}</div>
      </div>
      <div class="mark-grid" style="margin-top:10px">${marks.map((mark, index) => `<div class="mark-card"><h3>MK${index + 1}</h3>
        ${gameplayInput("Side art", `mark.${index + 1}.art.side`, mark.art?.side || "", { type: "text" })}
        <div data-mounted-mark>${gameplayInput("Mounted art", `mark.${index + 1}.art.mounted`, mark.art?.mounted || sharedArt.mounted || "", { type: "text" })}</div>
      </div>`).join("")}</div>
    </div></section>`;

  gameplayEditor.querySelectorAll("input, select, textarea").forEach(control => control.addEventListener("input", gameplayHandleInput));
  gameplayUpdateVisibility();
  gameplayHasRendered = true;
}

function gameplayUpdateVisibility() {
  const fireOwnership = gameplayValue("settings.fireOwnership", "shared");
  gameplayEditor.querySelector("[data-fire-shared]")?.classList.toggle("hidden", fireOwnership !== "shared");
  gameplayEditor.querySelector("[data-fire-marks]")?.classList.toggle("hidden", fireOwnership !== "mark");
  gameplayEditor.querySelectorAll("[data-fire-card]").forEach(card => {
    card.querySelector("[data-burst-fields]")?.classList.toggle("hidden", gameplayValue(`${card.dataset.fireCard}.mode`, "automatic") !== "burst");
  });

  const projectileType = gameplayValue("shared.projectileType", "bullet");
  gameplayEditor.querySelector("[data-projectile-fields]")?.classList.toggle("hidden", projectileType === "beam");
  gameplayEditor.querySelector("[data-beam-fields]")?.classList.toggle("hidden", projectileType !== "beam");

  ["homing", "dot"].forEach(effect => {
    const ownership = gameplayValue(`settings.${effect}Ownership`, "off");
    gameplayEditor.querySelector(`[data-${effect}-shared]`)?.classList.toggle("hidden", ownership !== "shared");
    gameplayEditor.querySelector(`[data-${effect}-marks]`)?.classList.toggle("hidden", ownership !== "mark");
    gameplayEditor.querySelector(`[data-${effect}-off]`)?.classList.toggle("hidden", ownership !== "off");
  });

  const explosion = gameplayValue("settings.explosionOwnership", "off");
  gameplayEditor.querySelector("[data-explosion-marks]")?.classList.toggle("hidden", explosion !== "mark");
  gameplayEditor.querySelector("[data-explosion-off]")?.classList.toggle("hidden", explosion !== "off");

  const mounted = gameplayValue("settings.mountedOwnership", "mark");
  gameplayEditor.querySelector("[data-mounted-shared]")?.classList.toggle("hidden", mounted !== "shared");
  gameplayEditor.querySelectorAll("[data-mounted-mark]").forEach(element => element.classList.toggle("hidden", mounted !== "mark"));
}

function gameplayReadFire(prefix) {
  const mode = gameplayValue(`${prefix}.mode`, "automatic");
  const fire = { mode, rate: gameplayNumericValue(`${prefix}.rate`, 1) };
  if (mode === "burst") {
    fire.shotsPerBurst = gameplayNumericValue(`${prefix}.shotsPerBurst`, 3);
    fire.secondsBetweenShots = gameplayNumericValue(`${prefix}.secondsBetweenShots`, 0.08);
  }
  return fire;
}
function gameplayReadHoming(prefix) {
  return {
    acquisitionRange: gameplayNumericValue(`${prefix}.acquisitionRange`, 20),
    turnRate: gameplayNumericValue(`${prefix}.turnRate`, 180),
    activationDelay: gameplayNumericValue(`${prefix}.activationDelay`, 0),
    targetPolicy: gameplayValue(`${prefix}.targetPolicy`, "closest-to-aim"),
    reacquire: gameplayChecked(`${prefix}.reacquire`, true)
  };
}
function gameplayReadDot(prefix, includeRefresh) {
  const dot = {
    damagePerSecond: gameplayNumericValue(`${prefix}.damagePerSecond`, 1),
    duration: gameplayNumericValue(`${prefix}.duration`, 3),
    ticksPerSecond: gameplayNumericValue(`${prefix}.ticksPerSecond`, 3),
    maxStacks: gameplayNumericValue(`${prefix}.maxStacks`, 1)
  };
  if (includeRefresh) dot.refreshDuration = gameplayChecked("settings.dotRefreshDuration", true);
  return dot;
}

function gameplayApply() {
  if (!gameplayHasRendered) return;
  const parsed = parseFiles();
  if (parsed.errors.length) return;
  const shared = gameplayClone(parsed.parsed["weapon.json"]);
  const marks = [1, 2, 3].map(mark => gameplayClone(parsed.parsed[`mk${mark}.json`]));

  shared.name = gameplayValue("shared.name", "New Weapon").trim();
  const description = gameplayValue("shared.description", "").trim();
  if (description) shared.description = description; else delete shared.description;
  shared.category = elements.categoryInput.value.trim();
  shared.rarity = gameplayValue("shared.rarity", "common");
  shared.projectileType = gameplayValue("shared.projectileType", "bullet");
  shared.damageType = gameplayValue("shared.damageType", "physical");
  shared.shot = {
    projectiles: gameplayNumericValue("shared.shot.projectiles", 1),
    spread: gameplayNumericValue("shared.shot.spread", 0)
  };

  if (shared.projectileType === "beam") {
    delete shared.projectile;
    shared.beam = { range: gameplayNumericValue("shared.beam.range", 25), width: gameplayNumericValue("shared.beam.width", 0.2) };
  } else {
    delete shared.beam;
    shared.projectile = {
      speed: gameplayNumericValue("shared.projectile.speed", 20),
      radius: gameplayNumericValue("shared.projectile.radius", 0.1),
      range: gameplayNumericValue("shared.projectile.range", 25)
    };
  }

  shared.impact = {
    pierce: gameplayNumericValue("shared.impact.pierce", 1),
    ricochet: gameplayNumericValue("shared.impact.ricochet", 0),
    knockback: gameplayNumericValue("shared.impact.knockback", 0)
  };

  if (gameplayValue("settings.fireOwnership", "shared") === "shared") {
    shared.fire = gameplayReadFire("shared.fire");
    marks.forEach(mark => delete mark.fire);
  } else {
    delete shared.fire;
    marks.forEach((mark, index) => { mark.fire = gameplayReadFire(`mark.${index + 1}.fire`); });
  }

  const homing = gameplayValue("settings.homingOwnership", "off");
  if (homing === "shared") {
    shared.homing = gameplayReadHoming("shared.homing");
    marks.forEach(mark => delete mark.homing);
  } else if (homing === "mark") {
    delete shared.homing;
    marks.forEach((mark, index) => { mark.homing = gameplayReadHoming(`mark.${index + 1}.homing`); });
  } else {
    delete shared.homing;
    marks.forEach(mark => delete mark.homing);
  }

  const dot = gameplayValue("settings.dotOwnership", "off");
  if (dot === "shared") {
    shared.dot = gameplayReadDot("shared.dot", true);
    marks.forEach(mark => delete mark.dot);
  } else if (dot === "mark") {
    shared.dot = { refreshDuration: gameplayChecked("settings.dotRefreshDuration", true) };
    marks.forEach((mark, index) => { mark.dot = gameplayReadDot(`mark.${index + 1}.dot`, false); });
  } else {
    delete shared.dot;
    marks.forEach(mark => delete mark.dot);
  }

  if (gameplayValue("settings.explosionOwnership", "off") === "mark") {
    marks.forEach((mark, index) => {
      mark.explosion = {
        radius: gameplayNumericValue(`mark.${index + 1}.explosion.radius`, 2),
        edgeDamageMultiplier: gameplayNumericValue(`mark.${index + 1}.explosion.edgeDamageMultiplier`, 0.5)
      };
    });
  } else marks.forEach(mark => delete mark.explosion);

  if (!gameplayObject(shared.art)) shared.art = {};
  shared.art.delivery = gameplayValue("shared.art.delivery", "").trim();
  shared.art.trail = gameplayValue("shared.art.trail", "").trim();
  shared.art.impact = gameplayValue("shared.art.impact", "").trim();

  const mounted = gameplayValue("settings.mountedOwnership", "mark");
  if (mounted === "shared") {
    shared.art.mounted = gameplayValue("shared.art.mounted", "").trim();
    marks.forEach(mark => { if (gameplayObject(mark.art)) delete mark.art.mounted; });
  } else delete shared.art.mounted;

  marks.forEach((mark, index) => {
    mark.peakLevel = gameplayNumericValue(`mark.${index + 1}.peakLevel`, [1, 25, 50][index]);
    mark.damage = gameplayNumericValue(`mark.${index + 1}.damage`, 1);
    if (!gameplayObject(mark.art)) mark.art = {};
    mark.art.side = gameplayValue(`mark.${index + 1}.art.side`, "").trim();
    if (mounted === "mark") mark.art.mounted = gameplayValue(`mark.${index + 1}.art.mounted`, "").trim();
  });

  files["weapon.json"] = format(shared);
  marks.forEach((mark, index) => { files[`mk${index + 1}.json`] = format(mark); });
  elements.jsonEditor.value = files[activeFile];
  setDirty();
  renderIdentity();
  localChecks();
  if (typeof renderCompiledPreview === "function") renderCompiledPreview();
}

function gameplayHandleInput(event) {
  if (event.target.dataset.gKey === "shared.name" && !loadedIdentity) {
    elements.folderInput.value = folderSlug(event.target.value) || "new_weapon";
    renderIdentity();
  }
  gameplayUpdateVisibility();
  gameplayApply();
}
function gameplayQueueRender() {
  if (gameplayRenderQueued) return;
  gameplayRenderQueued = true;
  queueMicrotask(() => {
    gameplayRenderQueued = false;
    if (weaponEditorMode === "gameplay") gameplayRender();
  });
}
function setWeaponEditorMode(mode) {
  captureActiveFile();
  weaponEditorMode = mode;
  const gameplay = mode === "gameplay";
  gameplayEditor.classList.toggle("hidden", !gameplay);
  jsonWorkspace.classList.toggle("hidden", gameplay);
  gameplayModeButton.classList.toggle("active", gameplay);
  jsonModeButton.classList.toggle("active", !gameplay);
  if (gameplay) gameplayRender(); else elements.jsonEditor.value = files[activeFile];
}

gameplayModeButton.addEventListener("click", () => setWeaponEditorMode("gameplay"));
jsonModeButton.addEventListener("click", () => setWeaponEditorMode("json"));
elements.categoryInput.addEventListener("input", () => {
  if (weaponEditorMode === "gameplay" && gameplayHasRendered) gameplayApply();
});
elements.jsonEditor.addEventListener("input", () => {
  if (weaponEditorMode === "json") gameplayHasRendered = false;
});
new MutationObserver(() => {
  if (elements.fileTabs.children.length) gameplayQueueRender();
}).observe(elements.fileTabs, { childList: true });

// Display gameplay mode immediately, but wait for the core editor's first render before reading files.
gameplayEditor.classList.remove("hidden");
jsonWorkspace.classList.add("hidden");
gameplayModeButton.classList.add("active");
jsonModeButton.classList.remove("active");
