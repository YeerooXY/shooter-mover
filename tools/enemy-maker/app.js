"use strict";

const $ = (selector, root = document) => root.querySelector(selector);
const $$ = (selector, root = document) => [...root.querySelectorAll(selector)];
const refKeys = ["guns", "views", "moves", "ai", "effects", "perks", "mods", "xp", "loot"];
const refs = Object.fromEntries(refKeys.map((key) => [key, new Set()]));
let catalogsLoaded = false;

const form = $("#enemyForm");
const issuesBox = $("#issues");
const catalogState = $("#catalogState");
const previewDialog = $("#previewDialog");
const preview = $("#preview");

function num(value) {
  return Number(value);
}

function list(value) {
  return String(value || "")
    .split(",")
    .map((item) => item.trim())
    .filter(Boolean);
}

function effectList(value) {
  return list(value).map((token) => {
    const split = token.indexOf(":");
    return split < 1
      ? { kind: "", id: token }
      : { kind: token.slice(0, split).trim(), id: token.slice(split + 1).trim() };
  });
}

function effectText(effects) {
  return (effects || []).map((effect) => `${effect.kind}:${effect.id}`).join(", ");
}

function field(root, name) {
  return root.querySelector(`[data-field="${name}"]`);
}

function cloneTemplate(id) {
  return document.importNode($(id).content, true).firstElementChild;
}

function addMount(value = {}) {
  const row = cloneTemplate("#mountTemplate");
  field(row, "id").value = value.id || "mount.primary";
  field(row, "x").value = value.position?.x ?? 0.5;
  field(row, "y").value = value.position?.y ?? 0;
  field(row, "dx").value = value.direction?.x ?? 1;
  field(row, "dy").value = value.direction?.y ?? 0;
  $(".remove", row).addEventListener("click", () => row.remove());
  $("#mounts").append(row);
}

function attackFields(kind) {
  if (kind === "gun") {
    return `
      <div class="grid cols-4 attack-fields">
        <label>Gun<input data-detail="gun" list="guns" /></label>
        <label>Mount IDs<input data-detail="mounts" placeholder="mount.left, mount.right" /></label>
        <label>Fire mode
          <select data-detail="fireMode">
            <option value="alternating">Alternating</option>
            <option value="simultaneous">Simultaneous</option>
          </select>
        </label>
        <label>Order
          <select data-detail="order">
            <option value="listed">Listed</option>
            <option value="cycle">Cycle</option>
            <option value="weighted">Weighted</option>
          </select>
        </label>
        <label>Shot count<input data-detail="shots" type="number" min="1" step="1" value="1" /></label>
        <label>Shot interval<input data-detail="interval" type="number" min="0" step="0.01" value="0" /></label>
      </div>`;
  }
  if (kind === "melee") {
    return `
      <div class="grid cols-4 attack-fields">
        <label>Range<input data-detail="range" type="number" min="0.01" step="0.01" value="1" /></label>
        <label>Wind-up<input data-detail="windUp" type="number" min="0" step="0.01" value="0.2" /></label>
        <label>Active time<input data-detail="active" type="number" min="0.01" step="0.01" value="0.1" /></label>
        <label>Recovery<input data-detail="recovery" type="number" min="0" step="0.01" value="0.4" /></label>
        <label class="effects-row">Effects<input data-detail="effects" placeholder="damage:effect.melee-hit, knockback:effect.push" /></label>
      </div>`;
  }
  if (kind === "charge") {
    return `
      <div class="grid cols-4 attack-fields">
        <label>Speed<input data-detail="speed" type="number" min="0.01" step="0.01" value="8" /></label>
        <label>Distance<input data-detail="distance" type="number" min="0.01" step="0.01" value="5" /></label>
        <label>Wind-up<input data-detail="windUp" type="number" min="0" step="0.01" value="0.4" /></label>
        <label>Recovery<input data-detail="recovery" type="number" min="0" step="0.01" value="0.6" /></label>
        <label class="effects-row">Effects<input data-detail="effects" placeholder="damage:effect.charge-hit, knockback:effect.push" /></label>
      </div>`;
  }
  return `
    <div class="grid cols-3 attack-fields">
      <label>Wind-up<input data-detail="windUp" type="number" min="0" step="0.01" value="0.5" /></label>
      <label class="span-2">Effects<input data-detail="effects" placeholder="explosion:effect.enemy-blast, burn:effect.burn" /></label>
    </div>`;
}

function setDetail(row, name, value) {
  const input = row.querySelector(`[data-detail="${name}"]`);
  if (input && value !== undefined && value !== null) input.value = value;
}

function renderAttack(row, value = {}) {
  const kind = field(row, "kind").value;
  const slot = $("[data-slot='fields']", row);
  slot.innerHTML = attackFields(kind);

  if (kind === "gun") {
    setDetail(row, "gun", value.gun);
    setDetail(row, "mounts", (value.plan?.mounts || []).join(", "));
    setDetail(row, "fireMode", value.plan?.fire_mode);
    setDetail(row, "order", value.plan?.order);
    setDetail(row, "shots", value.plan?.shots);
    setDetail(row, "interval", value.plan?.interval);
  } else if (kind === "melee") {
    setDetail(row, "range", value.melee?.range);
    setDetail(row, "windUp", value.melee?.wind_up);
    setDetail(row, "active", value.melee?.active);
    setDetail(row, "recovery", value.melee?.recovery);
    setDetail(row, "effects", effectText(value.melee?.effects));
  } else if (kind === "charge") {
    setDetail(row, "speed", value.charge?.speed);
    setDetail(row, "distance", value.charge?.distance);
    setDetail(row, "windUp", value.charge?.wind_up);
    setDetail(row, "recovery", value.charge?.recovery);
    setDetail(row, "effects", effectText(value.charge?.effects));
  } else {
    setDetail(row, "windUp", value.explode?.wind_up);
    setDetail(row, "effects", effectText(value.explode?.effects));
  }
}

function addAttack(value = {}) {
  const row = cloneTemplate("#attackTemplate");
  field(row, "id").value = value.id || "attack.primary";
  field(row, "kind").value = value.kind || "gun";
  field(row, "kind").addEventListener("change", () => renderAttack(row));
  $(".remove", row).addEventListener("click", () => row.remove());
  $("#attacks").append(row);
  renderAttack(row, value);
}

function addVariant(value = {}) {
  const row = cloneTemplate("#variantTemplate");
  field(row, "id").value = value.id || "variant.armored";
  field(row, "mods").value = (value.mods || []).join(", ");
  $(".remove", row).addEventListener("click", () => row.remove());
  $("#variants").append(row);
}

function addPhase(value = {}) {
  const row = cloneTemplate("#phaseTemplate");
  field(row, "id").value = value.id || "phase.two";
  field(row, "health").value = value.health ?? 0.5;
  field(row, "mods").value = (value.mods || []).join(", ");
  $(".remove", row).addEventListener("click", () => row.remove());
  $("#phases").append(row);
}

function readAttack(row) {
  const kind = field(row, "kind").value;
  const attack = { kind, id: field(row, "id").value.trim() };
  const detail = (name) => row.querySelector(`[data-detail="${name}"]`)?.value;

  if (kind === "gun") {
    attack.gun = String(detail("gun") || "").trim();
    attack.plan = {
      mounts: list(detail("mounts")),
      fire_mode: detail("fireMode"),
      order: detail("order"),
      shots: num(detail("shots")),
      interval: num(detail("interval")),
    };
  } else if (kind === "melee") {
    attack.melee = {
      range: num(detail("range")),
      wind_up: num(detail("windUp")),
      active: num(detail("active")),
      recovery: num(detail("recovery")),
      effects: effectList(detail("effects")),
    };
  } else if (kind === "charge") {
    attack.charge = {
      speed: num(detail("speed")),
      distance: num(detail("distance")),
      wind_up: num(detail("windUp")),
      recovery: num(detail("recovery")),
      effects: effectList(detail("effects")),
    };
  } else {
    attack.explode = {
      wind_up: num(detail("windUp")),
      effects: effectList(detail("effects")),
    };
  }
  return attack;
}

function collectPackage() {
  return {
    schema: 1,
    version: $("#version").value.trim(),
    enemy: {
      id: $("#enemyId").value.trim(),
      view: $("#view").value.trim(),
      body: {
        travel: $("#travel").value,
        radius: num($("#radius").value),
        mass: num($("#mass").value),
        mounts: $$(".mount-row").map((row) => ({
          id: field(row, "id").value.trim(),
          position: { x: num(field(row, "x").value), y: num(field(row, "y").value) },
          direction: { x: num(field(row, "dx").value), y: num(field(row, "dy").value) },
        })),
      },
      stats: { health: num($("#health").value) },
      sense: { range: num($("#senseRange").value), arc: num($("#senseArc").value) },
      move: $("#move").value.trim(),
      ai: $("#ai").value.trim(),
      attacks: $$(".attack-row").map(readAttack),
      tiers: $$(".tier:checked").map((input) => num(input.value)),
      variants: $$(".variant-row").map((row) => ({
        id: field(row, "id").value.trim(),
        mods: list(field(row, "mods").value),
      })),
      perks: {
        fixed: list($("#fixedPerks").value),
        pool: list($("#perkPool").value),
        rolls: num($("#perkRolls").value),
      },
      phases: $$(".phase-row").map((row) => ({
        id: field(row, "id").value.trim(),
        health: num(field(row, "health").value),
        mods: list(field(row, "mods").value),
      })),
      xp: $("#xp").value.trim(),
      loot: $("#loot").value.trim(),
      clear_role: $("#clearRole").value,
    },
  };
}

function validate(pkg) {
  const found = [];
  const ids = new Map();
  const stable = /^[a-z0-9][a-z0-9._-]*$/;
  const add = (path, message) => found.push(`${path}: ${message}`);
  const id = (value, path) => {
    if (!stable.test(value || "")) add(path, "Invalid or missing stable ID.");
  };
  const positive = (value, path) => {
    if (!Number.isFinite(value) || value <= 0) add(path, "Must be greater than zero.");
  };
  const nonNegative = (value, path) => {
    if (!Number.isFinite(value) || value < 0) add(path, "Must be zero or greater.");
  };
  const unique = (value, path, group) => {
    id(value, path);
    const key = `${group}|${value}`;
    if (ids.has(key)) add(path, `Duplicate ID; first used at ${ids.get(key)}.`);
    else ids.set(key, path);
  };
  const ref = (kind, value, path) => {
    id(value, path);
    if (catalogsLoaded && !refs[kind].has(value)) add(path, `Missing ${kind} catalog reference.`);
  };

  if (!catalogsLoaded) add("$catalogs", "Load a canonical catalog snapshot before export.");
  id(pkg.version, "$.version");
  id(pkg.enemy.id, "$.enemy.id");
  ref("views", pkg.enemy.view, "$.enemy.view");
  ref("moves", pkg.enemy.move, "$.enemy.move");
  ref("ai", pkg.enemy.ai, "$.enemy.ai");
  ref("xp", pkg.enemy.xp, "$.enemy.xp");
  ref("loot", pkg.enemy.loot, "$.enemy.loot");
  positive(pkg.enemy.body.radius, "$.enemy.body.radius");
  positive(pkg.enemy.body.mass, "$.enemy.body.mass");
  positive(pkg.enemy.stats.health, "$.enemy.stats.health");
  positive(pkg.enemy.sense.range, "$.enemy.sense.range");
  if (!(pkg.enemy.sense.arc > 0 && pkg.enemy.sense.arc <= 360)) add("$.enemy.sense.arc", "Must be between 0 and 360.");

  const mountIds = new Set();
  pkg.enemy.body.mounts.forEach((mount, index) => {
    const path = `$.enemy.body.mounts[${index}]`;
    unique(mount.id, `${path}.id`, "mount");
    mountIds.add(mount.id);
    if (![mount.position.x, mount.position.y, mount.direction.x, mount.direction.y].every(Number.isFinite)) add(path, "Coordinates must be finite.");
    if ((mount.direction.x ** 2) + (mount.direction.y ** 2) <= 0) add(`${path}.direction`, "Direction cannot be zero.");
  });

  if (!pkg.enemy.attacks.length) add("$.enemy.attacks", "At least one attack is required.");
  pkg.enemy.attacks.forEach((attack, index) => {
    const path = `$.enemy.attacks[${index}]`;
    unique(attack.id, `${path}.id`, "attack");
    if (attack.kind === "gun") {
      ref("guns", attack.gun, `${path}.gun`);
      if (!attack.plan.mounts.length) add(`${path}.plan.mounts`, "At least one mount is required.");
      attack.plan.mounts.forEach((mount, mountIndex) => {
        id(mount, `${path}.plan.mounts[${mountIndex}]`);
        if (!mountIds.has(mount)) add(`${path}.plan.mounts[${mountIndex}]`, "Mount does not exist on this body.");
      });
      positive(attack.plan.shots, `${path}.plan.shots`);
      nonNegative(attack.plan.interval, `${path}.plan.interval`);
    } else {
      const data = attack[attack.kind];
      const effects = data?.effects || [];
      if (!effects.length) add(`${path}.${attack.kind}.effects`, "At least one typed effect is required.");
      effects.forEach((effect, effectIndex) => {
        const effectPath = `${path}.${attack.kind}.effects[${effectIndex}]`;
        if (!["damage", "burn", "explosion", "slow", "knockback"].includes(effect.kind)) add(`${effectPath}.kind`, "Unknown effect kind.");
        ref("effects", effect.id, `${effectPath}.id`);
      });
      if (attack.kind === "melee") {
        positive(data.range, `${path}.melee.range`);
        nonNegative(data.wind_up, `${path}.melee.wind_up`);
        positive(data.active, `${path}.melee.active`);
        nonNegative(data.recovery, `${path}.melee.recovery`);
      } else if (attack.kind === "charge") {
        positive(data.speed, `${path}.charge.speed`);
        positive(data.distance, `${path}.charge.distance`);
        nonNegative(data.wind_up, `${path}.charge.wind_up`);
        nonNegative(data.recovery, `${path}.charge.recovery`);
      } else if (attack.kind === "explode") {
        nonNegative(data.wind_up, `${path}.explode.wind_up`);
        if (!effects.some((effect) => effect.kind === "explosion")) add(`${path}.explode.effects`, "Explode attacks require an explosion effect.");
      }
    }
  });

  if (!pkg.enemy.tiers.length) add("$.enemy.tiers", "Select at least one tier.");

  const fixed = new Set(pkg.enemy.perks.fixed);
  pkg.enemy.perks.fixed.forEach((perk, index) => ref("perks", perk, `$.enemy.perks.fixed[${index}]`));
  pkg.enemy.perks.pool.forEach((perk, index) => {
    ref("perks", perk, `$.enemy.perks.pool[${index}]`);
    if (fixed.has(perk)) add(`$.enemy.perks.pool[${index}]`, "A perk cannot be fixed and rollable.");
  });
  if (!Number.isInteger(pkg.enemy.perks.rolls) || pkg.enemy.perks.rolls < 0 || pkg.enemy.perks.rolls > pkg.enemy.perks.pool.length) add("$.enemy.perks.rolls", "Roll count must fit the pool.");

  pkg.enemy.variants.forEach((variant, index) => {
    const path = `$.enemy.variants[${index}]`;
    unique(variant.id, `${path}.id`, "variant");
    variant.mods.forEach((mod, modIndex) => ref("mods", mod, `${path}.mods[${modIndex}]`));
  });

  let previous = 1;
  pkg.enemy.phases.forEach((phase, index) => {
    const path = `$.enemy.phases[${index}]`;
    unique(phase.id, `${path}.id`, "phase");
    if (!(phase.health > 0 && phase.health < previous)) add(`${path}.health`, "Thresholds must be strictly descending between 0 and 1.");
    previous = phase.health;
    phase.mods.forEach((mod, modIndex) => ref("mods", mod, `${path}.mods[${modIndex}]`));
  });

  return found;
}

function showIssues(items) {
  issuesBox.hidden = items.length === 0;
  issuesBox.textContent = items.join("\n");
}

function build() {
  const pkg = collectPackage();
  const issues = validate(pkg);
  showIssues(issues);
  return { pkg, issues };
}

function loadCatalogs(data) {
  for (const key of refKeys) {
    if (!Array.isArray(data[key])) throw new Error(`Catalog snapshot needs an array named '${key}'.`);
    refs[key] = new Set(data[key].map(String));
  }
  catalogsLoaded = true;
  const datalists = {
    guns: "guns",
    views: "views",
    moves: "moves",
    ai: "aiList",
    effects: "effects",
    perks: "perks",
    mods: "mods",
    xp: "xpList",
    loot: "lootList",
  };
  for (const [key, id] of Object.entries(datalists)) {
    const target = $(`#${id}`);
    target.replaceChildren(...[...refs[key]].sort().map((value) => {
      const option = document.createElement("option");
      option.value = value;
      return option;
    }));
  }
  const count = refKeys.reduce((sum, key) => sum + refs[key].size, 0);
  catalogState.textContent = `Catalog snapshot loaded: ${count} references.`;
}

function setValue(selector, value) {
  $(selector).value = value ?? "";
}

function openPackage(pkg) {
  if (!pkg || pkg.schema !== 1 || !pkg.enemy) throw new Error("Unsupported enemy package.");
  const enemy = pkg.enemy;
  setValue("#version", pkg.version);
  setValue("#enemyId", enemy.id);
  setValue("#view", enemy.view);
  setValue("#clearRole", enemy.clear_role);
  setValue("#move", enemy.move);
  setValue("#ai", enemy.ai);
  setValue("#xp", enemy.xp);
  setValue("#loot", enemy.loot);
  setValue("#travel", enemy.body.travel);
  setValue("#radius", enemy.body.radius);
  setValue("#mass", enemy.body.mass);
  setValue("#health", enemy.stats.health);
  setValue("#senseRange", enemy.sense.range);
  setValue("#senseArc", enemy.sense.arc);

  $("#mounts").replaceChildren();
  (enemy.body.mounts || []).forEach(addMount);
  $("#attacks").replaceChildren();
  (enemy.attacks || []).forEach(addAttack);
  $$(".tier").forEach((input) => { input.checked = (enemy.tiers || []).includes(num(input.value)); });
  setValue("#fixedPerks", (enemy.perks?.fixed || []).join(", "));
  setValue("#perkPool", (enemy.perks?.pool || []).join(", "));
  setValue("#perkRolls", enemy.perks?.rolls ?? 0);
  $("#variants").replaceChildren();
  (enemy.variants || []).forEach(addVariant);
  $("#phases").replaceChildren();
  (enemy.phases || []).forEach(addPhase);
  showIssues([]);
}

async function readFile(file) {
  return JSON.parse(await file.text());
}

function download(pkg) {
  const text = `${JSON.stringify(pkg, null, 2)}\n`;
  const blob = new Blob([text], { type: "application/json" });
  const link = document.createElement("a");
  link.href = URL.createObjectURL(blob);
  link.download = `${pkg.enemy.id.replace(/[^a-z0-9._-]/gi, "-")}.enemy.json`;
  link.click();
  URL.revokeObjectURL(link.href);
}

$("#addMount").addEventListener("click", () => addMount({ id: `mount.${$$('.mount-row').length + 1}` }));
$("#addAttack").addEventListener("click", () => addAttack({ id: `attack.${$$('.attack-row').length + 1}` }));
$("#addVariant").addEventListener("click", () => addVariant({ id: `variant.${$$('.variant-row').length + 1}` }));
$("#addPhase").addEventListener("click", () => addPhase({ id: `phase.${$$('.phase-row').length + 2}` }));

$("#catalogFile").addEventListener("change", async (event) => {
  try {
    const file = event.target.files[0];
    if (file) loadCatalogs(await readFile(file));
  } catch (error) {
    showIssues([error.message]);
  } finally {
    event.target.value = "";
  }
});

$("#packageFile").addEventListener("change", async (event) => {
  try {
    const file = event.target.files[0];
    if (file) openPackage(await readFile(file));
  } catch (error) {
    showIssues([error.message]);
  } finally {
    event.target.value = "";
  }
});

$("#previewButton").addEventListener("click", () => {
  const { pkg } = build();
  preview.textContent = JSON.stringify(pkg, null, 2);
  previewDialog.showModal();
});

$("#exportButton").addEventListener("click", () => {
  const { pkg, issues } = build();
  if (!issues.length) download(pkg);
});

$("#closePreview").addEventListener("click", () => previewDialog.close());
form.addEventListener("submit", (event) => event.preventDefault());

addMount();
addAttack();
