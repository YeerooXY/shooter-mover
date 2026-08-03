"use strict";

(function expose(root, factory) {
  const api = factory();
  if (typeof module === "object" && module.exports) module.exports = api;
  if (root) root.EnemySchema = api;
})(typeof globalThis !== "undefined" ? globalThis : this, function build() {
  const ID = /^[a-z0-9]+(?:-[a-z0-9]+)*$/;
  const TYPES = new Set(["shooter", "contact", "pouncer", "popcorn"]);
  const MOVES = new Set(["direct", "wander", "strafe", "fly", "stationary"]);
  const SHAPES = Object.freeze({
    circle: "supported",
    box: "planned",
    ellipse: "planned",
    polygon: "planned"
  });

  function isObject(value) {
    return value !== null && typeof value === "object" && !Array.isArray(value);
  }

  function finite(value) {
    return typeof value === "number" && Number.isFinite(value);
  }

  function positive(value) {
    return finite(value) && value > 0;
  }

  function optionalText(errors, value, path) {
    if (value !== undefined && (typeof value !== "string" || value.trim() === "")) {
      errors.push(`${path} must be a non-empty string when provided.`);
    }
  }

  function validateOffset(errors, value, path) {
    if (!isObject(value)) {
      errors.push(`${path} must be an object.`);
      return;
    }
    if (!finite(value.x)) errors.push(`${path}.x must be a finite number.`);
    if (!finite(value.y)) errors.push(`${path}.y must be a finite number.`);
  }

  function validateBody(errors, body) {
    if (!isObject(body)) {
      errors.push("body is required and must be an object.");
      return;
    }
    if (typeof body.shape !== "string" || !Object.hasOwn(SHAPES, body.shape)) {
      errors.push("body.shape must be circle, box, ellipse, or polygon.");
      return;
    }
    if (SHAPES[body.shape] !== "supported") {
      errors.push(`body.shape '${body.shape}' is reserved but not supported by the first runtime/editor iteration.`);
      return;
    }
    if (!positive(body.radius)) errors.push("body.radius must be greater than zero for a circle.");
    validateOffset(errors, body.offset, "body.offset");
    const allowed = new Set(["shape", "radius", "offset"]);
    Object.keys(body).forEach(key => {
      if (!allowed.has(key)) errors.push(`body.${key} is not supported for a circle.`);
    });
  }

  function validateEnemy(enemy, expectedId) {
    const errors = [];
    if (!isObject(enemy)) return ["Enemy root must be an object."];
    if (typeof enemy.id !== "string" || !ID.test(enemy.id)) {
      errors.push("id must use lower-case letters, digits, and single hyphens.");
    }
    if (expectedId && enemy.id !== expectedId) errors.push(`id must match the file name '${expectedId}'.`);
    if (typeof enemy.name !== "string" || enemy.name.trim() === "") errors.push("name is required.");
    if (!TYPES.has(enemy.type)) errors.push("type must be shooter, contact, pouncer, or popcorn.");
    if (!positive(enemy.hp)) errors.push("hp must be greater than zero.");
    if (enemy.speed !== undefined && (!finite(enemy.speed) || enemy.speed < 0)) {
      errors.push("speed must be zero or greater when provided.");
    }
    if (enemy.move !== undefined && !MOVES.has(enemy.move)) {
      errors.push("move must be direct, wander, strafe, fly, or stationary.");
    }
    if (!positive(enemy.scale)) errors.push("scale must be greater than zero.");
    if (enemy.type === "shooter" && (typeof enemy.gun !== "string" || enemy.gun.trim() === "")) {
      errors.push("gun is required for a shooter.");
    }
    if (enemy.type !== "shooter" && enemy.gun !== undefined) {
      errors.push("gun is only valid for a shooter in the first iteration.");
    }
    if (enemy.type !== "shooter" && !positive(enemy.damage)) {
      errors.push("damage is required for non-shooter contact attacks.");
    }
    if (enemy.type === "shooter" && enemy.damage !== undefined) {
      errors.push("Shooter damage comes from the canonical gun and must not be copied into the enemy.");
    }
    if (enemy.range !== undefined && !positive(enemy.range)) errors.push("range must be greater than zero when provided.");
    if (enemy.detect !== undefined && !positive(enemy.detect)) errors.push("detect must be greater than zero when provided.");
    optionalText(errors, enemy.drops, "drops");
    optionalText(errors, enemy.art, "art");
    validateBody(errors, enemy.body);

    const allowed = new Set(["id", "name", "type", "hp", "speed", "move", "gun", "damage", "range", "detect", "scale", "drops", "art", "body"]);
    Object.keys(enemy).forEach(key => {
      if (!allowed.has(key)) errors.push(`${key} is not a supported enemy field.`);
    });
    return errors;
  }

  function validateLeveling(value) {
    const errors = [];
    if (!isObject(value)) return ["Leveling root must be an object."];
    if (!Number.isInteger(value.minLevel) || value.minLevel !== 1) errors.push("minLevel must be 1.");
    if (!Number.isInteger(value.maxLevel) || value.maxLevel < 2) errors.push("maxLevel must be an integer greater than 1.");
    if (!positive(value.strengthAtMax) || value.strengthAtMax < 1) errors.push("strengthAtMax must be at least 1.");
    if (!positive(value.damagePower)) errors.push("damagePower must be greater than zero.");
    if (!Array.isArray(value.colors) || value.colors.length < 2) {
      errors.push("colors must contain at least two level/color stops.");
    } else {
      let previous = -Infinity;
      value.colors.forEach((stop, index) => {
        if (!isObject(stop)) {
          errors.push(`colors[${index}] must be an object.`);
          return;
        }
        if (!Number.isInteger(stop.level)) errors.push(`colors[${index}].level must be an integer.`);
        if (Number.isInteger(stop.level) && stop.level <= previous) errors.push("Color stop levels must be strictly increasing.");
        previous = stop.level;
        if (typeof stop.color !== "string" || !/^#[0-9a-fA-F]{6}$/.test(stop.color)) {
          errors.push(`colors[${index}].color must be a six-digit hex color.`);
        }
      });
      if (value.colors[0]?.level !== value.minLevel) errors.push("The first color stop must match minLevel.");
      if (value.colors[value.colors.length - 1]?.level !== value.maxLevel) errors.push("The last color stop must match maxLevel.");
    }
    const allowed = new Set(["minLevel", "maxLevel", "strengthAtMax", "damagePower", "colors"]);
    Object.keys(value).forEach(key => {
      if (!allowed.has(key)) errors.push(`${key} is not a supported leveling field.`);
    });
    return errors;
  }

  function clamp(value, min, max) {
    return Math.max(min, Math.min(max, value));
  }

  function strengthAt(level, leveling) {
    const t = clamp((level - leveling.minLevel) / (leveling.maxLevel - leveling.minLevel), 0, 1);
    return Math.pow(leveling.strengthAtMax, t);
  }

  function parseHex(value) {
    return [1, 3, 5].map(start => Number.parseInt(value.slice(start, start + 2), 16));
  }

  function toHex(channels) {
    return `#${channels.map(value => Math.round(clamp(value, 0, 255)).toString(16).padStart(2, "0")).join("")}`.toUpperCase();
  }

  function levelColor(level, leveling) {
    const stops = leveling.colors;
    if (level <= stops[0].level) return stops[0].color.toUpperCase();
    if (level >= stops[stops.length - 1].level) return stops[stops.length - 1].color.toUpperCase();
    for (let index = 1; index < stops.length; index += 1) {
      const right = stops[index];
      if (level > right.level) continue;
      const left = stops[index - 1];
      const t = (level - left.level) / (right.level - left.level);
      const a = parseHex(left.color);
      const b = parseHex(right.color);
      return toHex(a.map((value, channel) => value + (b[channel] - value) * t));
    }
    return stops[stops.length - 1].color.toUpperCase();
  }

  function resolvedStats(enemy, level, leveling) {
    const strength = strengthAt(level, leveling);
    const hpMultiplier = Math.pow(strength, enemy.scale);
    const damageMultiplier = Math.pow(strength, leveling.damagePower);
    return {
      strength,
      hpMultiplier,
      damageMultiplier,
      hp: enemy.hp * hpMultiplier,
      damage: enemy.damage === undefined ? null : enemy.damage * damageMultiplier,
      color: levelColor(level, leveling)
    };
  }

  return { ID, TYPES, MOVES, SHAPES, validateEnemy, validateLeveling, strengthAt, levelColor, resolvedStats };
});
