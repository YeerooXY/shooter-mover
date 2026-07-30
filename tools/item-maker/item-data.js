"use strict";

const rarityOptions = ["common", "uncommon", "rare", "epic", "legendary", "artifact"];
const damageTypes = ["physical", "energy", "fire", "cold", "electric", "chemical", "special"];
const gearSlots = ["armor", "helmet", "core", "utility"];
const gearStats = ["max-health", "armor", "movement", "damage", "fire-rate"];

const elements = {
  newGunButton: document.querySelector("#newGunButton"),
  newGearButton: document.querySelector("#newGearButton"),
  importButton: document.querySelector("#importButton"),
  importInput: document.querySelector("#importInput"),
  exportButton: document.querySelector("#exportButton"),
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
const previews = {};

function makeGunMark() {
  return {
    available: false,
    rarity: "common",
    dropLevel: 1,
    dropWeight: 1,
    fire: {
      mode: "automatic",
      wavesPerSecond: 4,
      wavesPerBurst: 3,
      secondsBetweenWaves: 0.05,
      fullChargeSeconds: 1.5,
      maxHoldSeconds: 3,
      fullChargeDamage: 3,
      autoFireAtFull: false
    },
    shot: { projectiles: 1, spread: 0 },
    damage: {
      amount: 1,
      type: "physical",
      dotDamage: 0,
      dotSeconds: 0,
      movement: 0
    },
    delivery: {
      type: "normal",
      speed: 20,
      range: 25,
      radius: 0.1,
      explosionRadius: 0,
      beamWidth: 0.2
    },
    homing: {
      enabled: false,
      turnSpeed: 180,
      findRange: 20,
      startDelay: 0,
      findAnotherTarget: true
    },
    impact: { pierce: 0, ricochet: 0, knockback: 0 },
    art: { side: "", mounted: "", projectile: "", trail: "", impact: "", explosion: "" },
    special: { code: "", notes: "" }
  };
}

function makeGearMark() {
  return {
    available: false,
    rarity: "common",
    dropLevel: 1,
    dropWeight: 1,
    slot: "armor",
    augmentSlots: 0,
    bonuses: [],
    art: { side: "" },
    special: { code: "", notes: "" }
  };
}

function makeItem(kind) {
  const markFactory = kind === "gun" ? makeGunMark : makeGearMark;
  return {
    kind,
    id: "",
    name: "",
    intendedUse: "",
    marks: {
      mk1: markFactory(),
      mk2: markFactory(),
      mk3: markFactory()
    }
  };
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
  elements.saveStatus.textContent = value ? "Unsaved changes" : "Exported";
  elements.saveStatus.className = "status-pill " + (value ? "warn" : "good");
}

function optionList(values, selected) {
  return values.map(value => `<option value="${escapeHtml(value)}"${value === selected ? " selected" : ""}>${title(value)}</option>`).join("");
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
