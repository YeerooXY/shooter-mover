"use strict";

(function exposeWeaponDps(root, factory) {
  const api = factory();
  if (typeof module !== "undefined" && module.exports) module.exports = api;
  if (root) root.WeaponDps = api;
})(typeof globalThis !== "undefined" ? globalThis : this, function createWeaponDps() {
  const SCHEMA = "shooter-mover.weapon-dps-targets/2";
  const LEGACY_SCHEMA = "shooter-mover.weapon-dps-targets/1";
  const DEFAULT_MAX_LEVEL = 122;
  const DEFAULT_CONFIG = Object.freeze({
    $schema: SCHEMA,
    rawWeaponCurve: Object.freeze({
      startLevel: 1,
      startDps: 4,
      referenceLevel: 110,
      referenceDps: 200,
      maxAuthoredLevel: DEFAULT_MAX_LEVEL
    }),
    rarityMultipliers: Object.freeze({
      common: 1,
      rare: 1.25,
      epic: 1.66,
      legendary: 2,
      artifact: 3
    }),
    buildMultipliers: Object.freeze({
      weaponUpgrades: 3,
      gear: 2,
      skills: 1.5,
      accountProgression: 1.2,
      optimizedTotal: 20
    })
  });

  function finite(value) {
    return typeof value === "number" && Number.isFinite(value);
  }

  function positive(value) {
    return finite(value) && value > 0;
  }

  function positiveInteger(value) {
    return Number.isInteger(value) && value > 0;
  }

  function round(value, digits = 4) {
    const scale = 10 ** digits;
    return Math.round((value + Number.EPSILON) * scale) / scale;
  }

  function clone(value) {
    return JSON.parse(JSON.stringify(value));
  }

  function emptyTargets() {
    return clone(DEFAULT_CONFIG);
  }

  function validateLegacyTargets(value) {
    const errors = [];
    if (!Number.isInteger(value.maxLevel) || value.maxLevel < 1 || value.maxLevel > 1000) errors.push("maxLevel must be an integer from 1 to 1000.");
    if (!value.targets || typeof value.targets !== "object" || Array.isArray(value.targets)) return errors.concat("targets must be an object keyed by level.");
    for (let level = 1; level <= value.maxLevel; level += 1) {
      if (!positive(value.targets[String(level)])) errors.push(`Level ${level} DPS target must be greater than zero.`);
    }
    return errors;
  }

  function validateTargets(value) {
    const errors = [];
    if (!value || typeof value !== "object" || Array.isArray(value)) return ["Weapon balance must be an object."];
    if (value.$schema === LEGACY_SCHEMA) return validateLegacyTargets(value);
    if (value.$schema !== SCHEMA) errors.push(`$schema must be ${SCHEMA}.`);

    const curve = value.rawWeaponCurve;
    if (!curve || typeof curve !== "object" || Array.isArray(curve)) {
      errors.push("rawWeaponCurve must be an object.");
    } else {
      if (!positiveInteger(curve.startLevel)) errors.push("rawWeaponCurve.startLevel must be a positive integer.");
      if (!positive(curve.startDps)) errors.push("rawWeaponCurve.startDps must be greater than zero.");
      if (!positiveInteger(curve.referenceLevel)) errors.push("rawWeaponCurve.referenceLevel must be a positive integer.");
      if (!positive(curve.referenceDps)) errors.push("rawWeaponCurve.referenceDps must be greater than zero.");
      if (!positiveInteger(curve.maxAuthoredLevel) || curve.maxAuthoredLevel > 10000) errors.push("rawWeaponCurve.maxAuthoredLevel must be an integer from 1 to 10000.");
      if (positiveInteger(curve.startLevel) && positiveInteger(curve.referenceLevel) && curve.referenceLevel <= curve.startLevel) {
        errors.push("rawWeaponCurve.referenceLevel must be greater than startLevel.");
      }
      if (positiveInteger(curve.referenceLevel) && positiveInteger(curve.maxAuthoredLevel) && curve.maxAuthoredLevel < curve.referenceLevel) {
        errors.push("rawWeaponCurve.maxAuthoredLevel must be at least referenceLevel.");
      }
    }

    const rarity = value.rarityMultipliers;
    if (!rarity || typeof rarity !== "object" || Array.isArray(rarity)) {
      errors.push("rarityMultipliers must be an object.");
    } else {
      ["common", "rare", "epic", "legendary", "artifact"].forEach(key => {
        if (!positive(rarity[key])) errors.push(`rarityMultipliers.${key} must be greater than zero.`);
      });
    }

    const build = value.buildMultipliers;
    if (!build || typeof build !== "object" || Array.isArray(build)) {
      errors.push("buildMultipliers must be an object.");
    } else {
      ["weaponUpgrades", "gear", "skills", "accountProgression", "optimizedTotal"].forEach(key => {
        if (!positive(build[key])) errors.push(`buildMultipliers.${key} must be greater than zero.`);
      });
      if (["weaponUpgrades", "gear", "skills", "accountProgression", "optimizedTotal"].every(key => positive(build[key]))) {
        const normalTotal = build.weaponUpgrades * build.gear * build.skills * build.accountProgression;
        if (build.optimizedTotal < normalTotal) errors.push("buildMultipliers.optimizedTotal must be at least the normal completed-build multiplier.");
      }
    }

    return errors;
  }

  function normalizeTargets(value) {
    const normalized = emptyTargets();
    if (!value || typeof value !== "object") return normalized;

    if (value.$schema === "shooter-mover.weapon-dps-targets/1") {
      const maxLevel = positiveInteger(value.maxLevel) ? value.maxLevel : normalized.rawWeaponCurve.referenceLevel;
      const start = value.targets && positive(value.targets["1"]) ? value.targets["1"] : normalized.rawWeaponCurve.startDps;
      const end = value.targets && positive(value.targets[String(maxLevel)]) ? value.targets[String(maxLevel)] : normalized.rawWeaponCurve.referenceDps;
      normalized.rawWeaponCurve.startDps = start;
      normalized.rawWeaponCurve.referenceLevel = maxLevel;
      normalized.rawWeaponCurve.referenceDps = end;
      normalized.rawWeaponCurve.maxAuthoredLevel = Math.max(maxLevel, DEFAULT_MAX_LEVEL);
      return normalized;
    }

    if (value.rawWeaponCurve && typeof value.rawWeaponCurve === "object") {
      Object.assign(normalized.rawWeaponCurve, value.rawWeaponCurve);
    }
    if (value.rarityMultipliers && typeof value.rarityMultipliers === "object") {
      Object.assign(normalized.rarityMultipliers, value.rarityMultipliers);
    }
    if (value.buildMultipliers && typeof value.buildMultipliers === "object") {
      Object.assign(normalized.buildMultipliers, value.buildMultipliers);
    }
    normalized.$schema = SCHEMA;
    return normalized;
  }

  function generateCurve(startDps, endDps, maxLevel = 110, mode = "linear") {
    if (!positive(startDps) || !positive(endDps)) throw new Error("Start and end DPS must be greater than zero.");
    if (!positiveInteger(maxLevel)) throw new Error("Maximum level must be a positive integer.");
    if (!["linear", "exponential"].includes(mode)) throw new Error("Curve must be linear or exponential.");
    const targets = {};
    for (let level = 1; level <= maxLevel; level += 1) {
      const progress = maxLevel === 1 ? 0 : (level - 1) / (maxLevel - 1);
      const value = mode === "exponential"
        ? startDps * ((endDps / startDps) ** progress)
        : startDps + ((endDps - startDps) * progress);
      targets[String(level)] = round(value, 4);
    }
    return { $schema: LEGACY_SCHEMA, maxLevel, targets };
  }

  function targetAtLevel(config, level) {
    if (!positiveInteger(level)) return null;
    const normalized = normalizeTargets(config);
    if (validateTargets(normalized).length) return null;
    const curve = normalized.rawWeaponCurve;
    const progress = (level - curve.startLevel) / (curve.referenceLevel - curve.startLevel);
    const value = curve.startDps * ((curve.referenceDps / curve.startDps) ** progress);
    return positive(value) ? round(value, 4) : null;
  }

  function rarityTargetAtLevel(config, level, rarity) {
    const rawTarget = targetAtLevel(config, level);
    if (!positive(rawTarget)) return null;
    const normalized = normalizeTargets(config);
    const multiplier = positive(normalized.rarityMultipliers[String(rarity || "common").toLowerCase()])
      ? normalized.rarityMultipliers[String(rarity || "common").toLowerCase()]
      : normalized.rarityMultipliers.common;
    return round(rawTarget * multiplier, 4);
  }

  function buildEstimates(rawDps, config) {
    if (!positive(rawDps)) return null;
    const normalized = normalizeTargets(config);
    const build = normalized.buildMultipliers;
    const developedWeapon = rawDps * build.weaponUpgrades;
    const withGear = developedWeapon * build.gear;
    const withSkills = withGear * build.skills;
    const completeBuild = withSkills * build.accountProgression;
    return {
      rawWeapon: round(rawDps),
      developedWeapon: round(developedWeapon),
      withGear: round(withGear),
      withSkills: round(withSkills),
      completeBuild: round(completeBuild),
      optimizedBuild: round(rawDps * build.optimizedTotal),
      normalTotalMultiplier: round(build.weaponUpgrades * build.gear * build.skills * build.accountProgression),
      optimizedTotalMultiplier: round(build.optimizedTotal)
    };
  }

  function shotsPerSecond(definition) {
    const fire = definition && definition.fire || {};
    const rate = positive(fire.rate) ? fire.rate : 0;
    const burstShots = fire.mode === "burst" && Number.isInteger(fire.shotsPerBurst) && fire.shotsPerBurst > 0
      ? fire.shotsPerBurst
      : 1;
    return rate * burstShots;
  }

  function calculate(definition, suggestedDps = null) {
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
    const suggestedDamage = positive(suggestedDps) && hitInstancesPerSecond > 0
      ? Math.max(0, (suggestedDps - dotDps) / hitInstancesPerSecond)
      : null;
    const difference = positive(suggestedDps) ? totalDps - suggestedDps : null;
    const differencePercent = positive(suggestedDps) ? (difference / suggestedDps) * 100 : null;

    return {
      attacksPerSecond: round(attacksPerSecond),
      projectiles,
      hitInstancesPerSecond: round(hitInstancesPerSecond),
      directDps: round(directDps),
      sustainableStacks,
      dotDps: round(dotDps),
      totalDps: round(totalDps),
      targetDps: positive(suggestedDps) ? suggestedDps : null,
      difference: difference === null ? null : round(difference),
      differencePercent: differencePercent === null ? null : round(differencePercent, 2),
      suggestedDamage: suggestedDamage === null ? null : round(suggestedDamage)
    };
  }

  return {
    SCHEMA,
    DEFAULT_MAX_LEVEL,
    DEFAULT_CONFIG,
    emptyTargets,
    validateTargets,
    normalizeTargets,
    generateCurve,
    targetAtLevel,
    rarityTargetAtLevel,
    buildEstimates,
    shotsPerSecond,
    calculate
  };
});
