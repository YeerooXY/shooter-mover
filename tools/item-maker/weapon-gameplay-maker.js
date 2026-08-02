"use strict";

const gameplayEditor = document.querySelector("#gameplayEditor");
const weaponGlobalEditor = document.querySelector("#weaponGlobalEditor");
const weaponVisualPreview = document.querySelector("#weaponVisualPreview");
const jsonWorkspace = document.querySelector("#jsonWorkspace");
const gameplayModeButton = document.querySelector("#gameplayModeButton");
const jsonModeButton = document.querySelector("#jsonModeButton");

const gameplayRarities = ["common", "rare", "epic", "legendary", "artifact"];
const gameplayDamageTypes = ["physical", "energy", "thermal", "chemical"];
const gameplayBaseFireModes = [["automatic", "Automatic"], ["semi-automatic", "Semi-automatic"]];
const gameplayWeaponTypes = [
  ["blaster", "Bullet / blaster"],
  ["shotgun", "Shotgun"],
  ["orb", "Orb"],
  ["rocket", "Rocket"],
  ["beam", "Beam"],
  ["special", "Special (future)"]
];
const gameplayTypeDefinitions = {
  blaster: { category: "normal-firearm", projectileType: "bullet", projectiles: 1, spread: 0 },
  shotgun: { category: "shotgun", projectileType: "bullet", projectiles: 6, spread: 24 },
  orb: { category: "orb", projectileType: "orb", projectiles: 1, spread: 0 },
  rocket: { category: "rocket", projectileType: "rocket", projectiles: 1, spread: 0 },
  beam: { category: "beam", projectileType: "beam", projectiles: 1, spread: 0 },
  special: { category: "special", projectileType: "bullet", projectiles: 1, spread: 0 }
};

let weaponEditorMode = "gameplay";
let gameplayHasRendered = false;
let gameplayRenderQueued = false;
let gameplayActiveMark = 1;

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
  const readonly = options.readonly ? " readonly" : "";
  return `<div class="field${full}"><label>${escapeHtml(label)}</label><input data-g-key="${escapeHtml(key)}" type="${type}" value="${escapeHtml(value ?? "")}"${step}${min}${max}${readonly}></div>`;
}
function gameplayTextarea(label, key, value) {
  return `<div class="field full"><label>${escapeHtml(label)}</label><textarea data-g-key="${escapeHtml(key)}">${escapeHtml(value || "")}</textarea></div>`;
}
function gameplaySelect(label, key, value, values, options = {}) {
  const full = options.full ? " full" : "";
  return `<div class="field${full}"><label>${escapeHtml(label)}</label><select data-g-key="${escapeHtml(key)}">${gameplayOptions(values, value)}</select></div>`;
}
function gameplayCheckbox(label, key, checked) {
  return `<label class="inline-check"><input data-g-key="${escapeHtml(key)}" type="checkbox"${checked ? " checked" : ""}> <span>${escapeHtml(label)}</span></label>`;
}
function gameplayControl(key) { return document.querySelector(`[data-g-key="${key}"]`); }
function gameplayValue(key, fallback = "") {
  const control = gameplayControl(key);
  return control ? control.value : fallback;
}
function gameplayNumericValue(key, fallback = 0) { return gameplayNumber(gameplayValue(key, fallback), fallback); }
function gameplayChecked(key, fallback = false) {
  const control = gameplayControl(key);
  return control ? control.checked : fallback;
}
function gameplayFeatureEnabled(name) {
  return document.querySelector(`[data-feature="${name}"]`)?.classList.contains("active") || false;
}

function gameplayInferType(shared) {
  if (shared.category === "special") return "special";
  if (shared.projectileType === "rocket") return "rocket";
  if (shared.projectileType === "beam") return "beam";
  if (shared.projectileType === "orb") return "orb";
  if (shared.projectileType === "bullet") {
    const projectileCount = Number(shared.shot?.projectiles || 1);
    const spread = Number(shared.shot?.spread || 0);
    if (shared.category === "shotgun" || shared.category === "spread-firearm" || projectileCount > 1 || spread > 0) return "shotgun";
  }
  return "blaster";
}
function gameplayHasDot(shared, marks) {
  return gameplayObject(shared.dot) || marks.some(mark => gameplayObject(mark.dot));
}
function gameplayHomingSource(shared, marks) {
  return shared.homing || marks.find(mark => gameplayObject(mark.homing))?.homing || {};
}
function gameplayDotSource(shared, mark) {
  const sharedDot = shared.dot || {};
  return { ...sharedDot, ...(mark.dot || {}) };
}
function gameplayExplosionSource(mark) { return mark.explosion || {}; }

function gameplayRenderGlobal(shared, marks, weaponType) {
  const fire = shared.fire || marks[0].fire || { mode: "automatic", rate: 1 };
  const burstEnabled = fire.mode === "burst";
  const baseMode = burstEnabled ? "automatic" : fire.mode;
  const homingEnabled = gameplayObject(shared.homing) || marks.some(mark => gameplayObject(mark.homing));
  const dotEnabled = gameplayHasDot(shared, marks);
  const activeMark = marks[gameplayActiveMark - 1];

  weaponGlobalEditor.innerHTML = `
    <div class="weapon-global-grid">
      ${gameplaySelect("Weapon type", "settings.weaponType", weaponType, gameplayWeaponTypes, { full: true })}
      ${gameplayInput("Weapon name", "shared.name", shared.name || "", { type: "text", full: true })}
      ${gameplaySelect("Rarity", "shared.rarity", shared.rarity || "common", gameplayRarities)}
      ${gameplaySelect("Damage type", "shared.damageType", shared.damageType || "physical", gameplayDamageTypes)}
      ${gameplaySelect("Trigger", "settings.baseFireMode", baseMode || "automatic", gameplayBaseFireModes)}
      ${gameplayInput("Cycles / second", "shared.fire.rate", fire.rate ?? 1, { min: 0.000001 })}
    </div>
    <div class="editor-note">Weapon type also chooses the folder category and projectile delivery, so incompatible combinations cannot be saved.</div>
    <div class="mark-picker" aria-label="Selected Mark">
      ${[1, 2, 3].map(mark => `<button type="button" data-select-mark="${mark}" class="${mark === gameplayActiveMark ? "active" : ""}">MK${mark}</button>`).join("")}
    </div>
    ${gameplayInput(`MK${gameplayActiveMark} drop level`, `mark.${gameplayActiveMark}.peakLevel`, activeMark.peakLevel ?? [1, 25, 50][gameplayActiveMark - 1], { min: 1, step: 1 })}
    <div class="feature-toggle-grid">
      <button type="button" class="feature-toggle${homingEnabled ? " active" : ""}" data-feature="homing">Homing</button>
      <button type="button" class="feature-toggle${dotEnabled ? " active" : ""}" data-feature="dot">DoT</button>
      <button type="button" class="feature-toggle${burstEnabled ? " active" : ""}" data-feature="burst">Burst</button>
    </div>`;

  weaponGlobalEditor.querySelectorAll("input, select, textarea").forEach(control => control.addEventListener("input", gameplayHandleInput));
  weaponGlobalEditor.querySelectorAll("[data-select-mark]").forEach(button => button.addEventListener("click", () => {
    gameplayActiveMark = Number(button.dataset.selectMark);
    gameplayRender();
  }));
  weaponGlobalEditor.querySelectorAll("[data-feature]").forEach(button => button.addEventListener("click", () => {
    button.classList.toggle("active");
    gameplayApply(false);
    gameplayRender();
  }));
}

function gameplayRenderMain(shared, marks, weaponType) {
  const mark = marks[gameplayActiveMark - 1];
  const projectile = shared.projectile || {};
  const beam = shared.beam || {};
  const impact = shared.impact || {};
  const fire = shared.fire || { mode: "automatic", rate: 1 };
  const homing = gameplayHomingSource(shared, marks);
  const dot = gameplayDotSource(shared, mark);
  const explosion = gameplayExplosionSource(mark);
  const sharedArt = shared.art || {};
  const markArt = mark.art || {};
  const homingEnabled = gameplayFeatureEnabled("homing") || gameplayObject(shared.homing) || marks.some(item => gameplayObject(item.homing));
  const dotEnabled = gameplayFeatureEnabled("dot") || gameplayHasDot(shared, marks);
  const burstEnabled = gameplayFeatureEnabled("burst") || fire.mode === "burst";
  const isBeam = weaponType === "beam";
  const isShotgun = weaponType === "shotgun";
  const isRocket = weaponType === "rocket";
  const isSpecial = weaponType === "special";

  gameplayEditor.innerHTML = `
    <div class="editor-toolbar">
      <strong>MK${gameplayActiveMark}</strong>
      <span class="help">Level ${escapeHtml(mark.peakLevel ?? [1, 25, 50][gameplayActiveMark - 1])}</span>
      <div class="spacer"></div>
      ${gameplayActiveMark > 1 ? `<button type="button" data-copy-from="${gameplayActiveMark - 1}">Copy MK${gameplayActiveMark - 1}</button>` : ""}
      ${gameplayActiveMark !== 1 ? `<button type="button" data-copy-from="1">Copy MK1</button>` : ""}
    </div>
    <div class="editor-flow">
      <div class="editor-group">
        <h2>Damage and shot</h2>
        <div class="editor-grid">
          ${gameplayInput("Damage", `mark.${gameplayActiveMark}.damage`, mark.damage ?? 1, { min: 0.000001 })}
          ${gameplayInput(isShotgun ? "Pellets" : "Projectiles / shot", "shared.shot.projectiles", shared.shot?.projectiles ?? gameplayTypeDefinitions[weaponType].projectiles, { min: 1, step: 1 })}
          ${(isShotgun || isSpecial) ? gameplayInput("Spread (degrees)", "shared.shot.spread", shared.shot?.spread ?? gameplayTypeDefinitions[weaponType].spread, { min: 0 }) : ""}
          ${burstEnabled ? gameplayInput("Shots / burst", "shared.fire.shotsPerBurst", fire.shotsPerBurst ?? 3, { min: 2, step: 1 }) : ""}
          ${burstEnabled ? gameplayInput("Burst shot gap", "shared.fire.secondsBetweenShots", fire.secondsBetweenShots ?? 0.08, { min: 0.000001 }) : ""}
        </div>
      </div>

      <div class="editor-group">
        <h2>${isBeam ? "Beam" : "Projectile"}</h2>
        <div class="editor-grid">
          ${isBeam
            ? `${gameplayInput("Range", "shared.beam.range", beam.range ?? 25, { min: 0.000001 })}${gameplayInput("Width", "shared.beam.width", beam.width ?? 0.2, { min: 0.000001 })}`
            : `${gameplayInput("Speed", "shared.projectile.speed", projectile.speed ?? 20, { min: 0.000001 })}${gameplayInput("Radius", "shared.projectile.radius", projectile.radius ?? 0.1, { min: 0.000001 })}${gameplayInput("Range", "shared.projectile.range", projectile.range ?? 25, { min: 0.000001 })}`}
        </div>
      </div>

      <div class="editor-group">
        <h2>Impact</h2>
        <div class="editor-grid three">
          ${gameplayInput("Pierce", "shared.impact.pierce", impact.pierce ?? 1, { min: 0, step: 1 })}
          ${gameplayInput("Ricochet", "shared.impact.ricochet", impact.ricochet ?? 0, { min: 0 })}
          ${gameplayInput("Knockback", "shared.impact.knockback", impact.knockback ?? 0, { min: 0 })}
        </div>
      </div>

      ${isRocket ? `<div class="editor-group">
        <h2>Explosion</h2>
        <div class="editor-grid two">
          ${gameplayInput("Explosion radius", `mark.${gameplayActiveMark}.explosion.radius`, explosion.radius ?? 2, { min: 0.000001 })}
          ${gameplayInput("Damage at outer edge", `mark.${gameplayActiveMark}.explosion.edgeDamageMultiplier`, explosion.edgeDamageMultiplier ?? 0.5, { min: 0, max: 1 })}
        </div>
      </div>` : ""}

      ${homingEnabled ? `<div class="editor-group">
        <h2>Homing</h2>
        <div class="editor-grid">
          ${gameplayInput("Search range", "shared.homing.acquisitionRange", homing.acquisitionRange ?? 20, { min: 0.000001 })}
          ${gameplayInput("Turn speed", "shared.homing.turnRate", homing.turnRate ?? 180, { min: 0.000001 })}
          ${gameplayInput("Activation delay", "shared.homing.activationDelay", homing.activationDelay ?? 0, { min: 0 })}
          ${gameplayCheckbox("Reacquire target", "shared.homing.reacquire", homing.reacquire !== false)}
        </div>
      </div>` : ""}

      ${dotEnabled ? `<div class="editor-group">
        <h2>Damage over time · MK${gameplayActiveMark}</h2>
        <div class="editor-grid">
          ${gameplayInput("DPS / stack", `mark.${gameplayActiveMark}.dot.damagePerSecond`, dot.damagePerSecond ?? 1, { min: 0.000001 })}
          ${gameplayInput("Duration", `mark.${gameplayActiveMark}.dot.duration`, dot.duration ?? 3, { min: 0.000001 })}
          ${gameplayInput("Ticks / second", `mark.${gameplayActiveMark}.dot.ticksPerSecond`, dot.ticksPerSecond ?? 3, { min: 0.000001 })}
          ${gameplayInput("Maximum stacks", `mark.${gameplayActiveMark}.dot.maxStacks`, dot.maxStacks ?? 1, { min: 1, step: 1 })}
          ${gameplayCheckbox("Refresh duration on another hit", "settings.dotRefreshDuration", shared.dot?.refreshDuration !== false)}
        </div>
      </div>` : ""}

      <div class="editor-group">
        <h2>Art</h2>
        <div class="editor-grid two">
          ${gameplayInput(`MK${gameplayActiveMark} side PNG / art ID`, `mark.${gameplayActiveMark}.art.side`, markArt.side || "", { type: "text" })}
          ${gameplayInput(`MK${gameplayActiveMark} mounted PNG / art ID`, `mark.${gameplayActiveMark}.art.mounted`, markArt.mounted || sharedArt.mounted || "", { type: "text" })}
          ${gameplayInput(isBeam ? "Beam PNG / art ID" : "Projectile PNG / art ID", "shared.art.delivery", sharedArt.delivery || "", { type: "text" })}
          ${gameplayInput("Trail PNG / art ID", "shared.art.trail", sharedArt.trail || "", { type: "text" })}
          ${gameplayInput("Impact PNG / art ID", "shared.art.impact", sharedArt.impact || "", { type: "text" })}
        </div>
        <div class="editor-note">A direct image path previews immediately. Stable art IDs remain visible so missing bindings are obvious.</div>
      </div>

      ${isSpecial ? `<div class="editor-note">Special weapons currently use bullet travel in saved data until a dedicated special delivery is implemented.</div>` : ""}
      ${gameplayTextarea("Gameplay notes", "shared.description", shared.description || "")}
    </div>`;

  gameplayEditor.querySelectorAll("input, select, textarea").forEach(control => control.addEventListener("input", gameplayHandleInput));
  gameplayEditor.querySelectorAll("[data-copy-from]").forEach(button => button.addEventListener("click", () => {
    gameplayCopyMark(Number(button.dataset.copyFrom), gameplayActiveMark);
  }));
}

function gameplayLooksLikeImage(value) {
  return /^(data:|blob:|https?:\/\/|\.\.\/|\.\/|\/)/i.test(value) || /\.(png|jpe?g|gif|webp)(\?.*)?$/i.test(value);
}
function gameplayArtCard(label, value) {
  const safe = String(value || "").trim();
  const stage = gameplayLooksLikeImage(safe)
    ? `<img src="${escapeHtml(safe)}" alt="${escapeHtml(label)}" onerror="this.replaceWith(Object.assign(document.createElement('div'),{className:'art-preview-placeholder',textContent:'Image could not be loaded'}))">`
    : `<div class="art-preview-placeholder">${safe ? escapeHtml(safe) : "No art set"}</div>`;
  return `<div class="art-preview">
    <div class="art-preview-title">${escapeHtml(label)}</div>
    <div class="art-preview-stage">${stage}</div>
    <div class="art-preview-status ${safe ? "set" : "missing"}">${safe ? (gameplayLooksLikeImage(safe) ? "Image path set" : "Art ID set") : "Missing"}</div>
  </div>`;
}
function gameplayRenderVisuals(shared, marks, weaponType) {
  if (!weaponVisualPreview) return;
  const mark = marks[gameplayActiveMark - 1];
  const art = shared.art || {};
  const markArt = mark.art || {};
  weaponVisualPreview.innerHTML = `
    <div class="visual-mark-label">MK${gameplayActiveMark} · ${escapeHtml(gameplayTitle(weaponType))}</div>
    <div class="visual-grid">
      ${gameplayArtCard("Weapon", markArt.side || markArt.mounted || art.mounted || "")}
      ${gameplayArtCard(weaponType === "beam" ? "Beam" : "Projectile", art.delivery || "")}
    </div>
    <div class="visual-secondary">
      <span>Mounted: ${escapeHtml(markArt.mounted || art.mounted || "not set")}</span>
      <span>Trail: ${escapeHtml(art.trail || "not set")}</span>
      <span>Impact: ${escapeHtml(art.impact || "not set")}</span>
    </div>`;
}

function gameplayRender() {
  const result = parseFiles();
  if (result.errors.length) {
    weaponGlobalEditor.innerHTML = `<div class="gameplay-warning">Fix Advanced JSON before using the gameplay editor.</div>`;
    gameplayEditor.innerHTML = `<div class="gameplay-warning">${result.errors.map(escapeHtml).join("<br>")}</div>`;
    gameplayHasRendered = true;
    return;
  }
  const shared = result.parsed["weapon.json"];
  const marks = [1, 2, 3].map(mark => result.parsed[`mk${mark}.json`]);
  const weaponType = gameplayInferType(shared);
  gameplayRenderGlobal(shared, marks, weaponType);
  gameplayRenderMain(shared, marks, weaponType);
  gameplayRenderVisuals(shared, marks, weaponType);
  gameplayHasRendered = true;
}

function gameplayUpdateVisibility() {
  // Irrelevant controls do not exist in the DOM; the selected type and feature buttons drive the whole form.
}
function gameplayReadHoming() {
  return {
    acquisitionRange: gameplayNumericValue("shared.homing.acquisitionRange", 20),
    turnRate: gameplayNumericValue("shared.homing.turnRate", 180),
    activationDelay: gameplayNumericValue("shared.homing.activationDelay", 0),
    targetPolicy: "closest-to-aim",
    reacquire: gameplayChecked("shared.homing.reacquire", true)
  };
}
function gameplayReadDot(markNumber, existing = {}) {
  return {
    damagePerSecond: gameplayNumericValue(`mark.${markNumber}.dot.damagePerSecond`, existing.damagePerSecond ?? 1),
    duration: gameplayNumericValue(`mark.${markNumber}.dot.duration`, existing.duration ?? 3),
    ticksPerSecond: gameplayNumericValue(`mark.${markNumber}.dot.ticksPerSecond`, existing.ticksPerSecond ?? 3),
    maxStacks: gameplayNumericValue(`mark.${markNumber}.dot.maxStacks`, existing.maxStacks ?? 1)
  };
}

function gameplayApply(typeChanged = false) {
  if (!gameplayHasRendered) return;
  const parsed = parseFiles();
  if (parsed.errors.length) return;
  const shared = gameplayClone(parsed.parsed["weapon.json"]);
  const marks = [1, 2, 3].map(mark => gameplayClone(parsed.parsed[`mk${mark}.json`]));
  const weaponType = gameplayValue("settings.weaponType", gameplayInferType(shared));
  const type = gameplayTypeDefinitions[weaponType] || gameplayTypeDefinitions.blaster;
  const activeIndex = gameplayActiveMark - 1;
  const activeMark = marks[activeIndex];

  shared.name = gameplayValue("shared.name", shared.name || "New Weapon").trim();
  const description = gameplayValue("shared.description", shared.description || "").trim();
  if (description) shared.description = description; else delete shared.description;
  shared.category = type.category;
  shared.projectileType = type.projectileType;
  shared.rarity = gameplayValue("shared.rarity", shared.rarity || "common");
  shared.damageType = gameplayValue("shared.damageType", shared.damageType || "physical");
  elements.categoryInput.value = type.category;

  const existingShot = shared.shot || {};
  const projectiles = typeChanged
    ? type.projectiles
    : gameplayNumericValue("shared.shot.projectiles", existingShot.projectiles ?? type.projectiles);
  const spread = (weaponType === "shotgun" || weaponType === "special")
    ? (typeChanged ? type.spread : gameplayNumericValue("shared.shot.spread", existingShot.spread ?? type.spread))
    : 0;
  shared.shot = { projectiles: Math.max(1, Math.round(projectiles)), spread: Math.max(0, spread) };

  const burst = gameplayFeatureEnabled("burst");
  shared.fire = {
    mode: burst ? "burst" : gameplayValue("settings.baseFireMode", "automatic"),
    rate: gameplayNumericValue("shared.fire.rate", shared.fire?.rate ?? 1)
  };
  if (burst) {
    shared.fire.shotsPerBurst = gameplayNumericValue("shared.fire.shotsPerBurst", shared.fire.shotsPerBurst ?? 3);
    shared.fire.secondsBetweenShots = gameplayNumericValue("shared.fire.secondsBetweenShots", shared.fire.secondsBetweenShots ?? 0.08);
  }
  marks.forEach(mark => delete mark.fire);

  if (weaponType === "beam") {
    delete shared.projectile;
    shared.beam = {
      range: gameplayNumericValue("shared.beam.range", shared.beam?.range ?? 25),
      width: gameplayNumericValue("shared.beam.width", shared.beam?.width ?? 0.2)
    };
  } else {
    delete shared.beam;
    shared.projectile = {
      speed: gameplayNumericValue("shared.projectile.speed", shared.projectile?.speed ?? 20),
      radius: gameplayNumericValue("shared.projectile.radius", shared.projectile?.radius ?? 0.1),
      range: gameplayNumericValue("shared.projectile.range", shared.projectile?.range ?? 25)
    };
  }

  shared.impact = {
    pierce: gameplayNumericValue("shared.impact.pierce", shared.impact?.pierce ?? 1),
    ricochet: gameplayNumericValue("shared.impact.ricochet", shared.impact?.ricochet ?? 0),
    knockback: gameplayNumericValue("shared.impact.knockback", shared.impact?.knockback ?? 0)
  };

  if (gameplayFeatureEnabled("homing")) {
    shared.homing = gameplayReadHoming();
    marks.forEach(mark => delete mark.homing);
  } else {
    delete shared.homing;
    marks.forEach(mark => delete mark.homing);
  }

  if (gameplayFeatureEnabled("dot")) {
    shared.dot = { refreshDuration: gameplayChecked("settings.dotRefreshDuration", shared.dot?.refreshDuration !== false) };
    marks.forEach((mark, index) => {
      if (index === activeIndex) mark.dot = gameplayReadDot(index + 1, mark.dot || {});
      else if (!gameplayObject(mark.dot)) mark.dot = { damagePerSecond: 1, duration: 3, ticksPerSecond: 3, maxStacks: 1 };
    });
  } else {
    delete shared.dot;
    marks.forEach(mark => delete mark.dot);
  }

  if (weaponType === "rocket") {
    marks.forEach((mark, index) => {
      if (index === activeIndex) {
        mark.explosion = {
          radius: gameplayNumericValue(`mark.${index + 1}.explosion.radius`, mark.explosion?.radius ?? 2),
          edgeDamageMultiplier: gameplayNumericValue(`mark.${index + 1}.explosion.edgeDamageMultiplier`, mark.explosion?.edgeDamageMultiplier ?? 0.5)
        };
      } else if (!gameplayObject(mark.explosion)) mark.explosion = { radius: 2, edgeDamageMultiplier: 0.5 };
    });
  } else marks.forEach(mark => delete mark.explosion);

  activeMark.peakLevel = gameplayNumericValue(`mark.${gameplayActiveMark}.peakLevel`, activeMark.peakLevel ?? [1, 25, 50][activeIndex]);
  activeMark.damage = gameplayNumericValue(`mark.${gameplayActiveMark}.damage`, activeMark.damage ?? 1);

  if (!gameplayObject(shared.art)) shared.art = {};
  shared.art.delivery = gameplayValue("shared.art.delivery", shared.art.delivery || "").trim();
  shared.art.trail = gameplayValue("shared.art.trail", shared.art.trail || "").trim();
  shared.art.impact = gameplayValue("shared.art.impact", shared.art.impact || "").trim();
  const sharedMounted = shared.art.mounted || "";
  marks.forEach(mark => {
    if (!gameplayObject(mark.art)) mark.art = {};
    if (!mark.art.mounted && sharedMounted) mark.art.mounted = sharedMounted;
  });
  delete shared.art.mounted;
  activeMark.art.side = gameplayValue(`mark.${gameplayActiveMark}.art.side`, activeMark.art.side || "").trim();
  activeMark.art.mounted = gameplayValue(`mark.${gameplayActiveMark}.art.mounted`, activeMark.art.mounted || "").trim();

  files["weapon.json"] = format(shared);
  marks.forEach((mark, index) => { files[`mk${index + 1}.json`] = format(mark); });
  elements.jsonEditor.value = files[activeFile];
  setDirty();
  renderIdentity();
  localChecks();
  gameplayRenderVisuals(shared, marks, weaponType);
  document.dispatchEvent(new CustomEvent("weapon-maker-change"));
  const pulse = document.createComment("weapon-maker-change");
  gameplayEditor.appendChild(pulse);
  pulse.remove();
}

function gameplayCopyMark(sourceNumber, targetNumber) {
  const parsed = parseFiles();
  if (parsed.errors.length) return;
  const source = gameplayClone(parsed.parsed[`mk${sourceNumber}.json`]);
  const target = gameplayClone(parsed.parsed[`mk${targetNumber}.json`]);
  const targetLevel = target.peakLevel;
  const targetArt = gameplayClone(target.art || {});
  const copy = { ...source, peakLevel: targetLevel, art: targetArt };
  copy.art.side = targetArt.side || source.art?.side || "";
  copy.art.mounted = targetArt.mounted || source.art?.mounted || "";
  files[`mk${targetNumber}.json`] = format(copy);
  elements.jsonEditor.value = files[activeFile];
  setDirty();
  localChecks();
  gameplayRender();
  document.dispatchEvent(new CustomEvent("weapon-maker-change"));
  const pulse = document.createComment("weapon-maker-change");
  gameplayEditor.appendChild(pulse);
  pulse.remove();
}

function gameplayHandleInput(event) {
  const key = event.target.dataset.gKey || "";
  if (key === "shared.name" && !loadedIdentity) {
    elements.folderInput.value = folderSlug(event.target.value) || "new_weapon";
  }
  const typeChanged = key === "settings.weaponType";
  gameplayApply(typeChanged);
  if (typeChanged) gameplayRender();
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
  weaponGlobalEditor.closest(".panel").classList.toggle("json-mode-muted", !gameplay);
  jsonWorkspace.classList.toggle("hidden", gameplay);
  gameplayModeButton.classList.toggle("active", gameplay);
  jsonModeButton.classList.toggle("active", !gameplay);
  if (gameplay) gameplayRender(); else elements.jsonEditor.value = files[activeFile];
}

gameplayModeButton.addEventListener("click", () => setWeaponEditorMode("gameplay"));
jsonModeButton.addEventListener("click", () => setWeaponEditorMode("json"));
elements.jsonEditor.addEventListener("input", () => {
  if (weaponEditorMode === "json") gameplayHasRendered = false;
});
new MutationObserver(() => {
  if (elements.fileTabs.children.length) gameplayQueueRender();
}).observe(elements.fileTabs, { childList: true });

gameplayEditor.classList.remove("hidden");
jsonWorkspace.classList.add("hidden");
gameplayModeButton.classList.add("active");
jsonModeButton.classList.remove("active");
