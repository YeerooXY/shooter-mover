"use strict";

const SCHEMA_GUN = "shooter-mover.gun-family/1";
const SCHEMA_GEAR = "shooter-mover.gear-set/1";
const rarityOptions = ["common", "rare", "epic", "legendary", "artifact"];
const damageTypes = ["physical", "thermal", "chemical", "energy"];
const gunCategories = [
  "automatic-rifle", "shotgun", "precision", "rocket-launcher",
  "orb-launcher", "beam", "spray", "special"
];
const fireModes = ["semi-automatic", "automatic", "burst"];
const shotKinds = ["single", "spread", "pulse-spread", "twin-barrel", "volley", "beam", "spray"];
const deliveryTypes = ["normal", "orb", "rocket", "laser", "special"];
const gearSlots = ["headpiece", "body-armor", "legs", "boots"];
const modifierOperations = ["flat", "percentage", "multiplicative"];
const gearStats = [
  { id: "combat.maximum-health", label: "Maximum Health", live: true },
  { id: "combat.armor", label: "Armor", live: true },
  { id: "combat.movement-speed", label: "Movement Speed", live: true },
  { id: "combat.physical-damage-resistance", label: "Kinetic Resistance", live: true },
  { id: "combat.energy-damage-resistance", label: "Light Resistance", live: true },
  { id: "combat.thermal-damage-resistance", label: "Thermal Resistance", live: true },
  { id: "combat.chemical-damage-resistance", label: "Chemical Resistance", live: true },
  { id: "combat.outgoing-damage-multiplier", label: "Global Damage", live: true },
  { id: "combat.gun-damage-multiplier", label: "Gun Damage", live: true },
  { id: "combat.gun-fire-rate-multiplier", label: "Gun Fire Rate", live: true },
  { id: "combat.critical-chance", label: "Critical Chance", live: true },
  { id: "combat.critical-multiplier", label: "Critical Multiplier", live: true },
  { id: "combat.contact-damage", label: "Contact Damage", live: false },
  { id: "combat.health-regeneration", label: "Health Regeneration", live: false }
];

const elements = {
  newGunButton: document.querySelector("#newGunButton"),
  newGearButton: document.querySelector("#newGearButton"),
  importButton: document.querySelector("#importButton"),
  importInput: document.querySelector("#importInput"),
  exportButton: document.querySelector("#exportButton"),
  saveRepoButton: document.querySelector("#saveRepoButton"),
  fetchButton: document.querySelector("#fetchButton"),
  pullButton: document.querySelector("#pullButton"),
  repoStatus: document.querySelector("#repoStatus"),
  packageList: document.querySelector("#packageList"),
  gunKindButton: document.querySelector("#gunKindButton"),
  gearKindButton: document.querySelector("#gearKindButton"),
  itemName: document.querySelector("#itemName"),
  itemId: document.querySelector("#itemId"),
  intendedUse: document.querySelector("#intendedUse"),
  useNameForId: document.querySelector("#useNameForId"),
  idExample: document.querySelector("#idExample"),
  packageKind: document.querySelector("#packageKind"),
  packageFile: document.querySelector("#packageFile"),
  activeMarkId: document.querySelector("#activeMarkId"),
  copyPreviousButton: document.querySelector("#copyPreviousButton"),
  copyFirstToAllButton: document.querySelector("#copyFirstToAllButton"),
  markEditor: document.querySelector("#markEditor"),
  previewPanel: document.querySelector("#previewPanel"),
  calculatedPanel: document.querySelector("#calculatedPanel"),
  checksPanel: document.querySelector("#checksPanel"),
  jsonPreview: document.querySelector("#jsonPreview"),
  copyJsonButton: document.querySelector("#copyJsonButton"),
  saveStatus: document.querySelector("#saveStatus")
};

let activeMark = "mk1";
let dirty = false;
let idTracksName = true;
let state = makeItem("gun");
let repositoryConnected = false;
const previews = {};

function defaultProgression(mark) {
  return {
    mark,
    available: false,
    peakDropLevel: mark === 1 ? 1 : (mark === 2 ? 40 : 79),
    craftLevel: mark === 1 ? 1 : (mark === 2 ? 45 : 84),
    dropWeight: 1,
    minimumBoxTier: 1,
    maxAugmentSlots: mark + 1
  };
}

function makeGunMark(mark) {
  return Object.assign(defaultProgression(mark), {
    damage: { direct: 1, area: 0, dotPerSecond: 0 },
    art: { side: "", mounted: "", delivery: "", trail: "", impact: "", explosion: "" }
  });
}

function makeGearPiece(slot) {
  return {
    name: "",
    maxAugmentSlots: 2,
    art: "",
    modifiers: [],
    pendingModules: []
  };
}

function makeGearMark(mark) {
  return Object.assign(defaultProgression(mark), {
    pieces: {
      headpiece: makeGearPiece("headpiece"),
      "body-armor": makeGearPiece("body-armor"),
      legs: makeGearPiece("legs"),
      boots: makeGearPiece("boots")
    }
  });
}

function makeGun() {
  return {
    $schema: SCHEMA_GUN,
    kind: "gun-family",
    id: "",
    name: "",
    intendedUse: "",
    rarity: "common",
    category: "automatic-rifle",
    damageType: "physical",
    runtimeStatus: "live",
    fire: { mode: "automatic", cyclesPerSecond: 4, shotsPerBurst: 3, secondsBetweenShots: 0.08 },
    shot: {
      kind: "single", projectiles: 1, spreadDegrees: 0,
      randomnessDegrees: 0, pulses: 1, secondsBetweenPulses: 0
    },
    delivery: { type: "normal", speed: 20, radius: 0.1, range: 25, beamWidth: 0.2 },
    guidance: {
      mode: "unguided", acquisitionRange: 20, turnRateDegreesPerSecond: 180,
      activationDelaySeconds: 0, targetPolicy: "closest-to-aim", reacquire: true
    },
    impact: { pierce: 1, ricochet: 0, retainedSpeedPerRicochet: 1, knockback: 0 },
    effects: {
      explosion: { enabled: false, radius: 0, minimumDamageMultiplier: 0.5 },
      damageOverTime: { enabled: false, durationSeconds: 0, ticksPerSecond: 2, maximumStacks: 1, refreshesDuration: true },
      chain: { enabled: false, maximumTargets: 1, acquisitionRange: 0, retainedDamagePerJump: 1 }
    },
    pendingFeature: { id: "", notes: "" },
    marks: [makeGunMark(1), makeGunMark(2), makeGunMark(3)]
  };
}

function makeGear() {
  return {
    $schema: SCHEMA_GEAR,
    kind: "gear-set",
    id: "",
    name: "",
    intendedUse: "",
    rarity: "common",
    marks: [makeGearMark(1), makeGearMark(2), makeGearMark(3)]
  };
}

function makeItem(kind) {
  return kind === "gear" || kind === "gear-set" ? makeGear() : makeGun();
}

function activeMarkValue() {
  return state.marks[Number(activeMark.substring(2)) - 1];
}

function clone(value) { return JSON.parse(JSON.stringify(value)); }

function slugify(text) {
  return String(text || "")
    .normalize("NFKD")
    .replace(/[\u0300-\u036f]/g, "")
    .toLowerCase()
    .trim()
    .replace(/[^a-z0-9]+/g, "-")
    .replace(/^-+|-+$/g, "");
}

function number(value, fallback = 0) {
  const parsed = Number(value);
  return Number.isFinite(parsed) ? parsed : fallback;
}

function setDirty(value = true) {
  dirty = value;
  elements.saveStatus.textContent = value ? "Unsaved changes" : "Saved";
  elements.saveStatus.className = "status-pill " + (value ? "warn" : "good");
}

function optionList(values, selected) {
  return values.map(value => {
    const optionValue = typeof value === "string" ? value : value.id;
    const optionLabel = typeof value === "string" ? title(value) : value.label + (value.live ? "" : " (pending)");
    return `<option value="${escapeHtml(optionValue)}"${optionValue === selected ? " selected" : ""}>${escapeHtml(optionLabel)}</option>`;
  }).join("");
}

function title(value) {
  return String(value || "").replace(/[-_]+/g, " ").replace(/\b\w/g, match => match.toUpperCase());
}

function escapeHtml(value) {
  return String(value ?? "")
    .replace(/&/g, "&amp;")
    .replace(/</g, "&lt;")
    .replace(/>/g, "&gt;")
    .replace(/"/g, "&quot;")
    .replace(/'/g, "&#039;");
}

function field(label, path, value, type = "number", extra = "") {
  const step = type === "number" ? ` step="any"` : "";
  return `<div class="field"><label>${escapeHtml(label)}</label><input data-path="${path}" type="${type}" value="${escapeHtml(value)}"${step} ${extra}></div>`;
}

function selectField(label, path, value, values) {
  return `<div class="field"><label>${escapeHtml(label)}</label><select data-path="${path}">${optionList(values, value)}</select></div>`;
}

function checkboxField(label, path, checked) {
  return `<label class="checkbox-row"><input data-path="${path}" type="checkbox"${checked ? " checked" : ""}> <span>${escapeHtml(label)}</span></label>`;
}

function textareaField(label, path, value) {
  return `<div class="field full"><label>${escapeHtml(label)}</label><textarea data-path="${path}">${escapeHtml(value)}</textarea></div>`;
}

function section(name, content, open = false) {
  return `<details class="section"${open ? " open" : ""}><summary>${escapeHtml(name)}</summary><div class="section-body">${content}</div></details>`;
}
