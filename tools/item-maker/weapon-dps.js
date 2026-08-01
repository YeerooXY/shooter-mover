"use strict";

(function exposeWeaponDps(root, factory) {
  const api = factory();
  if (typeof module !== "undefined" && module.exports) module.exports = api;
  if (root) root.WeaponDps = api;
})(typeof globalThis !== "undefined" ? globalThis : this, function createWeaponDps() {
  const SCHEMA = "shooter-mover.weapon-dps-targets/1";
  const DEFAULT_MAX_LEVEL = 110;

  function finite(value) {
    return typeof value === "number" && Number.isFinite(value);
  }

  function positive(value) {
    return finite(value) && value > 0;
  }

  function round(value, digits = 4) {
    const scale = 10 ** digits;
    return Math.round((value + Number.EPSILON) * scale) / scale;
  }

  function emptyTargets(maxLevel = DEFAULT_MAX_LEVEL) {
    const targets = {};
    for (let level = 1; level <= maxLevel; level += 1) targets[String(level)] = null;
    return { $schema: SCHEMA, maxLevel, targets };
  }

  function validateTargets(value) {
    const errors = [];
    if (!value || typeof value !== "object" || Array.isArray(value)) return ["DPS targets must be an object."];
    if (value.$schema !== SCHEMA) errors.push(`$schema must be ${SCHEMA}.`);
    if (!Number.isInteger(value.maxLevel) || value.maxLevel < 1 || value.maxLevel > 1000) errors.push("maxLevel must be an integer from 1 to 1000.");
    if (!value.targets || typeof value.targets !== "object" || Array.isArray(value.targets)) errors.push("targets must be an object keyed by level.");
    if (errors.length) return errors;

    const expected = new Set();
    for (let level = 1; level <= value.maxLevel; level += 1) expected.add(String(level));
    const actual = Object.keys(value.targets);
    const missing = [...expected].filter(level => !Object.prototype.hasOwnProperty.call(value.targets, level));
    const extra = actual.filter(level => !expected.has(level));
    if (missing.length) errors.push(`Missing DPS target level(s): ${missing.join(", ")}.`);
    if (extra.length) errors.push(`Unexpected DPS target level(s): ${extra.join(", ")}.`);

    actual.forEach(level => {
      const target = value.targets[level];
      if (target !== null && !positive(target)) errors.push(`Level ${level} DPS target must be blank or greater than zero.`);
    });
    return errors;
  }

  function normalizeTargets(value, maxLevel = DEFAULT_MAX_LEVEL) {
    const normalized = emptyTargets(maxLevel);
    if (!value || typeof value !== "object" || !value.targets) return normalized;
    for (let level = 1; level <= maxLevel; level += 1) {
      const target = value.targets[String(level)];
      normalized.targets[String(level)] = positive(target) ? target : null;
    }
    return normalized;
  }

  function generateCurve(startDps, endDps, maxLevel = DEFAULT_MAX_LEVEL, mode = "linear") {
    if (!positive(startDps) || !positive(endDps)) throw new Error("Start and end DPS must be greater than zero.");
    if (!Number.isInteger(maxLevel) || maxLevel < 1) throw new Error("Maximum level must be a positive integer.");
    if (!["linear", "exponential"].includes(mode)) throw new Error("Curve must be linear or exponential.");
    const result = emptyTargets(maxLevel);
    for (let level = 1; level <= maxLevel; level += 1) {
      const progress = maxLevel === 1 ? 0 : (level - 1) / (maxLevel - 1);
      const value = mode === "exponential"
        ? startDps * ((endDps / startDps) ** progress)
        : startDps + ((endDps - startDps) * progress);
      result.targets[String(level)] = round(value, 4);
    }
    return result;
  }

  function targetAtLevel(config, level) {
    if (!config || !config.targets || !Number.isInteger(level)) return null;
    const value = config.targets[String(level)];
    return positive(value) ? value : null;
  }

  function shotsPerSecond(definition) {
    const fire = definition && definition.fire || {};
    const rate = positive(fire.rate) ? fire.rate : 0;
    const burstShots = fire.mode === "burst" && Number.isInteger(fire.shotsPerBurst) && fire.shotsPerBurst > 0
      ? fire.shotsPerBurst
      : 1;
    return rate * burstShots;
  }

  function calculate(definition, targetDps = null) {
    const projectiles = definition && definition.shot && Number.isInteger(definition.shot.projectiles)
      ? Math.max(1, definition.shot.projectiles)
      : 1;
    const attacksPerSecond = shotsPerSecond(definition);
    const hitInstancesPerSecond = attacksPerSecond * projectiles;
    const damage = positive(definition && definition.damage) ? definition.damage : 0;
    const directDps = damage * hitInstancesPerSecond;

    const dot = definition && definition.dot || null;
    let sustainableStacks = 0;
    let dotDps = 0;
    if (dot && positive(dot.damagePerSecond) && positive(dot.duration) && Number.isInteger(dot.maxStacks) && dot.maxStacks > 0) {
      const possibleStacks = Math.max(1, Math.ceil(hitInstancesPerSecond * dot.duration));
      sustainableStacks = Math.min(dot.maxStacks, possibleStacks);
      dotDps = dot.damagePerSecond * sustainableStacks;
    }

    const totalDps = directDps + dotDps;
    const suggestedDamage = positive(targetDps) && hitInstancesPerSecond > 0
      ? Math.max(0, (targetDps - dotDps) / hitInstancesPerSecond)
      : null;
    const difference = positive(targetDps) ? totalDps - targetDps : null;
    const differencePercent = positive(targetDps) ? (difference / targetDps) * 100 : null;

    return {
      attacksPerSecond: round(attacksPerSecond),
      projectiles,
      hitInstancesPerSecond: round(hitInstancesPerSecond),
      directDps: round(directDps),
      sustainableStacks,
      dotDps: round(dotDps),
      totalDps: round(totalDps),
      targetDps: positive(targetDps) ? targetDps : null,
      difference: difference === null ? null : round(difference),
      differencePercent: differencePercent === null ? null : round(differencePercent, 2),
      suggestedDamage: suggestedDamage === null ? null : round(suggestedDamage)
    };
  }

  return {
    SCHEMA,
    DEFAULT_MAX_LEVEL,
    emptyTargets,
    validateTargets,
    normalizeTargets,
    generateCurve,
    targetAtLevel,
    shotsPerSecond,
    calculate
  };
});
